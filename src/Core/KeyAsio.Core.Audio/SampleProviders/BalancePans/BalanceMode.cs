using System.ComponentModel;

namespace KeyAsio.Core.Audio.SampleProviders.BalancePans;

/// <summary>
/// 平衡控制模式
/// </summary>
public enum BalanceMode
{
    /// <summary>
    /// 等幂声像 (标准 Pan):
    /// 衰减相反声道，总音量保持恒定。
    /// 极端值 = [L, 0] 或 [0, R]。
    /// </summary>
    [Description("BalanceMode_ConstantPower")]
    ConstantPower,

    /// <summary>
    /// 交叉混合 (保留信息):
    /// 将少量相反声道信号混合到当前声道，用于保留空间感。
    /// </summary>
    [Description("BalanceMode_CrossMix")]
    CrossMix,

    /// <summary>
    /// Mid-Side 处理 (专业混音):
    /// 调整 Mid (中央) 和 Side (立体声宽度) 信号的平衡。
    /// </summary>
    [Description("BalanceMode_MidSide")]
    MidSide,

    /// <summary>
    /// 单声道混合声像 (听力辅助):
    /// 将 L+R 混合为单声道，并将其硬平移到左侧或右侧。
    /// 极端值 = [L+R, 0] 或 [0, L+R]。
    /// </summary>
    [Description("BalanceMode_BinauralMix")]
    BinauralMix,

    /// <summary>
    /// 关闭:
    /// 不进行任何平衡处理。
    /// </summary>
    [Description("BalanceMode_Off")]
    Off
}