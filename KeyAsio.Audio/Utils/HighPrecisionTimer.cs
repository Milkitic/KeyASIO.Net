using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace KeyAsio.Audio.Utils;

/// <summary>
/// 高精度计时器，提供更可靠和准确的计时功能
/// 使用 .NET 7+ 的静态方法避免内存分配
/// </summary>
public readonly struct HighPrecisionTimer
{
    private readonly long _startTimestamp;

    [DebuggerStepThrough]
    private HighPrecisionTimer(long startTimestamp)
    {
        _startTimestamp = startTimestamp;
    }

    /// <summary>
    /// 开始计时
    /// </summary>
    /// <returns>计时器实例</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static HighPrecisionTimer StartNew()
    {
        return new HighPrecisionTimer(Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// 获取已经过的时间
    /// </summary>
    public TimeSpan Elapsed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Stopwatch.GetElapsedTime(_startTimestamp);
    }

    /// <summary>
    /// 获取已经过的毫秒数
    /// </summary>
    public double ElapsedMilliseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// 获取已经过的微秒数
    /// </summary>
    public double ElapsedMicroseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Elapsed.TotalMicroseconds;
    }

    /// <summary>
    /// 获取已经过的纳秒数
    /// </summary>
    public double ElapsedNanoseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Elapsed.TotalNanoseconds;
    }

    /// <summary>
    /// 检查是否支持高分辨率计时器
    /// </summary>
    public static bool IsHighResolution => Stopwatch.IsHighResolution;

    /// <summary>
    /// 获取计时器频率
    /// </summary>
    public static long Frequency => Stopwatch.Frequency;

    /// <summary>
    /// 执行操作并测量执行时间
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <returns>执行时间</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan Measure(Action action)
    {
        var timer = StartNew();
        action();
        return timer.Elapsed;
    }

    /// <summary>
    /// 执行异步操作并测量执行时间
    /// </summary>
    /// <param name="asyncAction">要执行的异步操作</param>
    /// <returns>执行时间</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<TimeSpan> MeasureAsync(Func<Task> asyncAction)
    {
        var timer = StartNew();
        await asyncAction();
        return timer.Elapsed;
    }

    /// <summary>
    /// 执行操作并测量执行时间，返回操作结果和执行时间
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="func">要执行的函数</param>
    /// <returns>操作结果和执行时间</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (T Result, TimeSpan Elapsed) MeasureWithResult<T>(Func<T> func)
    {
        var timer = StartNew();
        var result = func();
        return (result, timer.Elapsed);
    }

    /// <summary>
    /// 执行异步操作并测量执行时间，返回操作结果和执行时间
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="asyncFunc">要执行的异步函数</param>
    /// <returns>操作结果和执行时间</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<(T Result, TimeSpan Elapsed)> MeasureWithResultAsync<T>(Func<Task<T>> asyncFunc)
    {
        var timer = StartNew();
        var result = await asyncFunc();
        return (result, timer.Elapsed);
    }
}