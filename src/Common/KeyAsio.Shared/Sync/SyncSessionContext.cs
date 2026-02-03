using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using KeyAsio.Plugins.Abstractions.OsuMemory;
using KeyAsio.Shared.Events;
using KeyAsio.Shared.OsuMemory;
using KeyAsio.Shared.Utils;

namespace KeyAsio.Shared.Sync;

public enum GameClientType
{
    Stable, // osu!stable: 仅在 Playing 状态应用倍率
    Lazer // osu!lazer: 任何时候都应用倍率
}

public class SyncSessionContext
{
    public ValueChangedAsyncEventHandler<int>? OnComboChanged;
    public ValueChangedAsyncEventHandler<Mods>? OnPlayModsChanged;
    public ValueChangedAsyncEventHandler<OsuMemoryStatus>? OnStatusChanged;
    public ValueChangedAsyncEventHandler<BeatmapIdentifier>? OnBeatmapChanged;

    private readonly AppSettings _appSettings;

    private long _anchorTick; // 锚点 Tick
    private int _anchorTime; // 锚点时间 (ms)
    private double _playbackRate = 1.0;
    private static readonly double s_tickToMs = 1000.0 / Stopwatch.Frequency;

    // 音频同步与防倒退
    private int _lastReturnedPlayTime = int.MinValue;

    private long _frozenStartTick; // 冻结检测
    private bool _isFrozen;
    private const int FrozenTimeoutMs = 200; // 冻结超时阈值

    public SyncSessionContext(AppSettings appSettings)
    {
        _appSettings = appSettings;
        ClientType = GameClientType.Stable; // 默认值

        _anchorTick = Stopwatch.GetTimestamp();
    }

    public GameClientType ClientType { get; set; }
    public bool IsStarted { get; set; }
    public bool IsReplay { get; set; }
    public int ProcessId { get; set; } = -1;

    public string? Username
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (!string.IsNullOrEmpty(value))
            {
                _appSettings.Logging.PlayerBase64 = EncodeUtils.GetBase64String(value, Encoding.ASCII);
            }
        }
    }

    public Mods PlayMods
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;

            UpdatePlaybackRate(value);
            OnPlayModsChanged?.Invoke(oldValue, value);
        }
    }

    private void UpdatePlaybackRate(Mods mods)
    {
        var oldRate = _playbackRate;

        if (mods.HasFlag(Mods.DoubleTime) || mods.HasFlag(Mods.Nightcore))
            _playbackRate = 1.5;
        else if (mods.HasFlag(Mods.HalfTime))
            _playbackRate = 0.75;
        else
            _playbackRate = 1.0;

        // 倍率改变极其罕见，但若发生，需要重置锚点以保证连续性
        if (Math.Abs(oldRate - _playbackRate) > 0.001)
        {
            // 获取当前的预测值（不带副作用）
            int currentPredictedTime = CalculatePredictedTime();

            // 重新锚定
            _anchorTick = Stopwatch.GetTimestamp();
            _anchorTime = currentPredictedTime;

            // 重要：同步更新最后返回时间，防止因重置导致的微小回退
            _lastReturnedPlayTime = currentPredictedTime;
            _isFrozen = false;
        }
    }

    /// <summary>
    /// 根据当前状态计算预测时间。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculatePredictedTime()
    {
        if (OsuStatus is OsuMemoryStatus.NotRunning or OsuMemoryStatus.Unknown)
        {
            return _anchorTime;
        }

        double effectiveRate = _playbackRate;
        if (ClientType == GameClientType.Stable && OsuStatus != OsuMemoryStatus.Playing)
        {
            effectiveRate = 1.0;
        }

        long currentTick = Stopwatch.GetTimestamp();
        double elapsedRealMs = (currentTick - _anchorTick) * s_tickToMs;

        // 限制最大预测范围（例如防止暂停后恢复时的瞬间飞跃），这里取 500ms 作为安全上限
        // 如果是正常游玩，BaseMemoryTime 会频繁更新，不会超过这个值
        const int realMs = 100;
        if (elapsedRealMs > realMs) elapsedRealMs = realMs;

        double interpolatedMs = elapsedRealMs * effectiveRate;
        return _anchorTime + (int)interpolatedMs;
    }

    public int PlayTime
    {
        get
        {
            // 1. 获取纯计算的目标时间
            int targetTime = CalculatePredictedTime();

            // 2. 音频同步关键：单调性与跳跃保护
            if (targetTime < _lastReturnedPlayTime)
            {
                int backwardAmount = _lastReturnedPlayTime - targetTime;

                // Case A: 大幅度倒退 (> 100ms) -> 视为 Seek 或 Retry
                if (backwardAmount > 100)
                {
                    _lastReturnedPlayTime = targetTime;
                    _isFrozen = false;
                }
                // Case B: 微小倒退 -> 启动/维持冻结逻辑
                else
                {
                    var currentTick = Stopwatch.GetTimestamp();

                    if (!_isFrozen)
                    {
                        // 刚开始倒退，进入冻结
                        _isFrozen = true;
                        _frozenStartTick = currentTick;
                    }
                    else
                    {
                        // 已经冻结，检查是否超时
                        double frozenDuration = (currentTick - _frozenStartTick) * s_tickToMs;
                        if (frozenDuration > FrozenTimeoutMs)
                        {
                            // 超时：强制同步（可能是游戏真的卡顿后回退了）
                            _lastReturnedPlayTime = targetTime;
                            _isFrozen = false;
                        }
                    }

                    // 冻结期间，返回旧值
                    return _lastReturnedPlayTime;
                }
            }
            else
            {
                // 正常前进
                _lastReturnedPlayTime = targetTime;
                _isFrozen = false;
            }

            return _lastReturnedPlayTime;
        }
    }

    public int BaseMemoryTime
    {
        get => _anchorTime;
        set
        {
            var currentTick = Stopwatch.GetTimestamp();
            var oldValue = _anchorTime;

            // 1. 极小抖动过滤 (内存读取误差)
            if (value < oldValue && oldValue - value < 5) return;

            // 2. 只有值真正改变才处理
            if (value != oldValue)
            {
                int predicted = CalculatePredictedTime();

                // 动态阈值：DT 模式下允许更大的预测误差
                double dynamicThreshold = 50 * Math.Max(1.0, _playbackRate);

                // 检测是否发生了“大跳跃”（Seek / Retry）
                // 如果内存读到的新值和我们预测的值差距过大，说明预测失效，需要硬同步
                if (Math.Abs(value - predicted) > dynamicThreshold)
                {
                    // 允许跳跃，重置单调性保护
                    _lastReturnedPlayTime = int.MinValue;
                    _isFrozen = false;
                }

                _anchorTime = value;
                _anchorTick = currentTick;
            }

            LastUpdateTimestamp = currentTick;
        }
    }

    public int Combo
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;
            OnComboChanged?.Invoke(oldValue, value);
        }
    }

    public int Score { get; set; }

    public OsuMemoryStatus OsuStatus
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;

            // 状态切换时，重置所有保护逻辑，允许时间跳变
            _lastReturnedPlayTime = int.MinValue;
            _isFrozen = false;

            // 重置锚点
            _anchorTick = Stopwatch.GetTimestamp();

            OnStatusChanged?.Invoke(oldValue, value);
        }
    } = OsuMemoryStatus.Unknown;

    public string SyncedStatusText => OsuStatus is OsuMemoryStatus.NotRunning or OsuMemoryStatus.Unknown
        ? "OFFLINE"
        : "SYNCED";

    public BeatmapIdentifier Beatmap
    {
        get;
        set
        {
            if (field == value) return;
            var oldValue = field;
            field = value;
            OnBeatmapChanged?.Invoke(oldValue, value);
        }
    }

    public long LastUpdateTimestamp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }
}