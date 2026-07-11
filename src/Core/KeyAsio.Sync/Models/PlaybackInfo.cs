using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;

namespace KeyAsio.Sync.Models;

public readonly record struct PlaybackInfo(CachedAudio? CachedAudio, PlaybackEvent PlaybackEvent);
