namespace KeyAsio.Audio.SampleProviders.BalancePans;

/// <summary>
/// 防削波策略
/// </summary>
public enum AntiClipStrategy
{
    None,

    /// <summary>
    /// 预防性衰减 - 降低混合增益(最安全,轻微音量损失)
    /// </summary>
    PreventiveAttenuation,

    /// <summary>
    /// 软削波 - 使用 tanh 限制器(音质最好,轻微失真)
    /// </summary>
    SoftClipper,

    /// <summary>
    /// 硬限制 - 直接裁剪到 ±1.0(最快,但有失真)
    /// </summary>
    HardLimit,

    /// <summary>
    /// 动态增益调整 - 实时检测并降低增益(最复杂,无失真)
    /// </summary>
    DynamicGain
}