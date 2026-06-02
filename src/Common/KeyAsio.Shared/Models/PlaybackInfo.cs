using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;

namespace KeyAsio.Shared.Models;

public readonly record struct PlaybackInfo(CachedAudio? CachedAudio, PlaybackEvent PlaybackEvent);
