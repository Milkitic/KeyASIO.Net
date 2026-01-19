using System.Diagnostics;

namespace KeyAsio.Shared.Hitsounds.Playback;

[DebuggerDisplay("{DebuggerDisplay}")]
public class SampleEvent : PlaybackEvent
{
    /// <summary>
    /// Object identifier
    /// </summary>
    public Guid Guid { get; set; }
    public SampleLayer Layer { get; set; }

    public string DebuggerDisplay => $"PL{(ResourceOwner == ResourceOwner.UserSkin ? "D" : "")}:{Offset}: " +
                                     $"P{(int)Layer}: " +
                                     $"V{(Volume * 10):#.#}: " +
                                     $"B{(Balance * 10):#.#}: " +
                                     $"{(Filename)}";
}