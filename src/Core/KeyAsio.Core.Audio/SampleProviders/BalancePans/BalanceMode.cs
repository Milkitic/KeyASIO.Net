using System.ComponentModel;

namespace KeyAsio.Core.Audio.SampleProviders.BalancePans;

/// <summary>
/// 平衡控制模式
/// </summary>
public enum BalanceMode
{
    /// <summary>
    /// 关闭:
    /// 不进行任何平衡处理。
    /// </summary>
    [Description("BalanceMode_Off")]
    Off,

    /// <summary>
    /// KeyASIO Focus (品牌声场导向):
    /// 保持 Mid 信号，在收窄原始 Side 的同时将声像平滑导向目标侧。
    /// </summary>
    [Description("BalanceMode_ProMixFocus")]
    ProMixFocus,

    /// <summary>
    /// 等功率立体声声像（标准 Pan）：
    /// 以 sin/cos 增益将移出声道的信号转移到目标侧。
    /// 中央位置保持原始立体声；极端值 = [L+R, 0] 或 [0, L+R]。
    /// </summary>
    [Description("BalanceMode_ConstantPower")]
    ConstantPower,

    /// <summary>
    /// 线性立体声声像：
    /// 以线性增益将移出声道的信号转移到目标侧。
    /// 中央位置保持原始立体声；极端值 = [L+R, 0] 或 [0, L+R]。
    /// </summary>
    [Description("BalanceMode_LinearStereoPan")]
    LinearStereoPan
}
