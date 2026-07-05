[Forum page](https://osu.ppy.sh/community/forums/topics/1602658)

## Release notes

### Summary
This is a small but important audio-engine patch. It ensures **loop/volume/balance control events are never silently dropped**, even when samples fall behind or cache misses occur, and fixes a mixing-provider race that could mistakenly remove freshly-queued playback sources.


### Fixes
- **Dropped Control Signals**: `LoopStop`, `Volume` and `Balance` control events are now dispatched regardless of latency tolerance or cache-hit state, preventing stuck loops and ignored volume/balance changes in Standard, Catch and Taiko sequencers. (`StandardHitsoundSequencer.cs`, `CatchHitsoundSequencer.cs`, `TaikoHitsoundSequencer.cs`)
- **Null CachedAudio Handling**: `PlaybackInfo.CachedAudio` is now nullable; `SfxPlaybackService` guards against missing samples for both `SampleEvent` (logs a warning) and `LoopStart` (skips the loop) instead of dereferencing null. (`PlaybackInfo.cs`, `SfxPlaybackService.cs`)
- **Mixer Add/Remove Race**: In `QueueMixingSampleProvider`, pending removals are now drained before a `Clear` and applied *after* additions within each read cycle, so an `Add` immediately followed by `Remove` in the same frame no longer leaves a dangling (or wrongly recycled) source. (`QueueMixingSampleProvider.cs`)

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.7...v4.0.0-alpha.8