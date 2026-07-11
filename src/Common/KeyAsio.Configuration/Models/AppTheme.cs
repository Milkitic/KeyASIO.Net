using System.ComponentModel;

namespace KeyAsio.Configuration.Models;

public enum AppTheme
{
    [Description("Settings_FollowSystem")]
    System,
    [Description("Settings_Theme_Light")]
    Light,
    [Description("Settings_Theme_Dark")]
    Dark
}