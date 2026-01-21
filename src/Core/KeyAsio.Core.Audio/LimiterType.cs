using System.ComponentModel;

namespace KeyAsio.Core.Audio;

public enum LimiterType
{
    [Description("Audio_LimiterType_Off")]
    Off,
    [Description("Audio_LimiterType_Master")]
    Master,
    [Description("Audio_LimiterType_Polynomial")]
    Polynomial,
    [Description("Audio_LimiterType_Soft")]
    Soft,
    [Description("Audio_LimiterType_Hard")]
    Hard,
}