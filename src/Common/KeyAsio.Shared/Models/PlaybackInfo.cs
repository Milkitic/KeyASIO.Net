using KeyAsio.Core.Audio.Caching;
using KeyAsio.Shared.Hitsounds.Playback;

namespace KeyAsio.Shared.Models;

public readonly record struct PlaybackInfo(CachedAudio CachedAudio, PlaybackEvent PlaybackEvent);