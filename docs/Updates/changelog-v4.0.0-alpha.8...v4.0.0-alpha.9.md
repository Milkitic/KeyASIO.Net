[Forum page](https://osu.ppy.sh/community/forums/topics/1602658)

## Release notes

### Summary
A large audio-architecture release that decouples the playback engine from the mixer, introduces a standalone **music transport** with play/pause/seek/loop and variable-speed support, and adds true **MP3 gapless playback** by parsing LAME/Xing/VBRI encoder-delay metadata. The UI layer has been upgraded to **Avalonia 12**, with smooth scrolling and a rewritten in-house localization system that drops the Antelcat.I18N dependency.


### Features
- **Standalone Music Transport**: New `StandaloneMusicTransport` / `IMusicPlaybackSource` / `IMusicPlaybackSink` abstraction lets osu! beatmap audio be loaded, played, paused, seeked, looped and rate-changed independently of the hitsound mixer, with automatic output re-wiring when the rate processor swaps. (`StandaloneMusicTransport.cs`, `AudioFileMusicPlaybackSource.cs`, `SilentMusicPlaybackSource.cs`)
- **MP3 Gapless Playback**: Parses LAME/Xing and Fraunhofer VBRI encoder-delay tags via the new `MpegAudioFrameScanner` and `Mp3GaplessInfo`, then trims encoder delay/padding with `Mp3GaplessAudioTrimmer` so MP3s line up sample-accurately with osu!'s BASS engine. (`Caching/Mp3GaplessInfo.cs`, `Caching/Mp3GaplessAudioTrimmer.cs`, `Utils/MpegAudioFrameScanner.cs`)
- **Variable Playback Rate**: New `IPlaybackRateProcessor` / `IPlaybackRateProcessorFactory` contracts and `PlaybackRateState` enable speed changes without re-creating the source; `NoPlaybackRateProcessorFactory` is provided as a fallback. (`IPlaybackRateProcessor.cs`, `IPlaybackRateProcessorFactory.cs`, `PlaybackRateState.cs`)
- **Playback Event Timeline Scheduler**: New `PlaybackEventTimelineScheduler` in the new `KeyAsio.Core.OsuAudio` project provides ordered, seek-aware scheduling of playback events with backward-jump detection. (`Core.OsuAudio/Timeline/PlaybackEventTimelineScheduler.cs`)
- **Custom Localization System**: Replaces the `Antelcat.I18N.Avalonia` dependency with an in-house `LocalizationService` + `I18NExtension` + `ILanguagePreferenceStore`, scanning culture directories and binding through a version-bumping converter so language switches propagate live. (`Shared/Localization/LocalizationService.cs`, `I18NExtension.cs`, `LanguageManager.cs`)
- **Smooth Scrolling**: Added `SmoothScroll.Avalonia` theme for inertia-based scrolling across the app. (`App.axaml`)

### Enhancements
- **AudioEngine Hardening**: Device start/stop is now `Lock`-guarded; mixers are preserved across same-format device restarts; ASIO `DriverResetRequest` is now handled at the engine level with automatic re-initialization; device lifecycle events (`DeviceStarted`/`Stopped`/`Error`) are exposed; subclasses can rewrite device descriptions via `PrepareDeviceDescriptionForCreation` / `RestoreDeviceDescriptionForState` hooks (e.g. for latency↔buffer-frame conversion). (`AudioEngine.cs`)
- **Mixer Operation Ordering**: `QueueMixingSampleProvider` now funnels add/remove through a single `PendingOperation` queue, applying additions before removals per read cycle and draining the queue cleanly on `Clear`, eliminating the add-then-remove same-frame race. (`QueueMixingSampleProvider.cs`)
- **OsuAudio Module Extraction**: Hitsound types moved into the new `KeyAsio.Core.OsuAudio` namespace/project, decoupling beatmap audio analysis from the runtime audio engine. (`Core.OsuAudio/KeyAsio.Core.OsuAudio.csproj`, namespace moves across `BeatmapSetContext.cs`, `ControlEvent.cs`, etc.)
- **Audio Settings Persistence**: Device settings are applied and persisted when starting the device; DBus-related plumbing added. (`AudioEngine.cs`, `DeviceDescription.cs`, `DeviceComparer.cs`)

### Fixes
- **Mixer Input Leak on Replacement**: Fixed sources not being removed from the mixer when replaced; queue operations were refactored to ensure correct ordering and recycling. (`QueueMixingSampleProvider.cs`)
- **Timeline Backward-Seek Matching**: Improved time-point lookup with tolerance so seeking backwards reliably re-syncs the next event instead of skipping or retriggering. (`PlaybackEventTimelineScheduler.cs`)
- **Null-Safety in Mixers**: `WaveFormat` is no longer nullable; constructor validates mixer input formats eagerly instead of throwing later during reads. (`QueueMixingSampleProvider.cs`)

### Miscellaneous
- **Avalonia 12 Upgrade**: Bumped Avalonia to 12.0.5, SukiUI to 7.0.1, Material.Icons.Avalonia to 3.0.2, Svg.Controls.Skia.Avalonia to 12.0.0.13, and added SmoothScroll.Avalonia 12.0.0.12.
- **Dependency Bumps**: CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.9, Sentry.Extensions.Logging 6.6.0, SharpCompress 0.49.1, Blake3, z440.atl.core, Milki.Extensions, NAudio submodule, System.Numerics.Tensors, and Microsoft.Windows.CsWin32 0.3.298.
- **Removed**: `Antelcat.I18N.Avalonia` dependency and the legacy `KeyAsio.Services.LanguageManager`; obsolete `OsuPlayback` project and audio bus removed; `MathEx.cs` deleted.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.8...v4.0.0-alpha.9