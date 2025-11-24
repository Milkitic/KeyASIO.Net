using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio.Caching;

namespace KeyAsio.Shared.Models;

public readonly record struct PlaybackInfo(CachedAudio CachedAudio, HitsoundNode HitsoundNode);