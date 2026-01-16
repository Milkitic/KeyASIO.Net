using System.Diagnostics;

namespace KeyAsio.Shared.Hitsounds.Playback;

[DebuggerDisplay("{DebuggerDisplay}")]
public class ControlEvent : PlaybackEvent
{
    public LoopChannel LoopChannel { get; internal set; }
    public ControlEventType ControlEventType { get; internal set; }

    public string DebuggerDisplay => $"CT{(UseUserSkin ? "D" : "")}:{Offset}: " +
                                     $"O{Offset}: " +
                                     $"T{(int)ControlEventType}{(ControlEventType is ControlEventType.LoopStart or ControlEventType.LoopStop ? (int)LoopChannel : "")}: " +
                                     $"V{(Volume * 10):#.#}: " +
                                     $"B{(Balance * 10):#.#}: " +
                                     $"{(Filename == null ? "" : Path.GetFileNameWithoutExtension(Filename))}";
}