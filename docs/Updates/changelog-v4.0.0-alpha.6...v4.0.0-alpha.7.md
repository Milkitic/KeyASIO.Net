[Forum page](https://osu.ppy.sh/community/forums/topics/1602658)

## Release notes

### Summary
This release introduces a real-time **RTSS OSD monitoring** overlay so you can watch live gameplay data (time, score, combo, hit errors and judgement stats) on top of osu!, and opens up the app to **UI plugins** that can inject their own controls into the main window. Under the hood, the MP3 decoder has been rewritten to match osu!'s exact sample-accurate timing, eliminating a long-standing per-file audio offset.


### Features
- **RTSS OSD Monitoring**: Added an RTSS (RivaTuner Statistics Server) on-screen display that streams SyncSessionContext data at ~100 FPS, including current time, mods, combo, score, judgement counts and a rolling hit-error window (64-sample average). Colorized keys make critical values quick to scan. Toggle it from *Settings → Sync → RTSS Monitoring*. (`src/Common/KeyAsio.Shared/Services/RtssMonitorService.cs`, `RtssOsdWriter.cs`)
- **UI Plugin Support**: Plugins can now provide an Avalonia `Control` via the new `IUserInterfacePlugin` interface; the first registered UI plugin is rendered in the main window's title-bar area. (`src/Common/KeyAsio.Shared/Plugins/IUserInterfacePlugin.cs`, `MainWindow.axaml`)
- **Live Judgement & Hit-Error Reading**: The memory scanner now exposes `SyncStatistics` (300/100/50/geki/katu/miss) and a `SyncHitErrors` stream through `ISyncContext`, with new rule definitions in `osu_memory_rules.json`.

### Enhancements
- **Sample-Accurate MP3 Decoding**: Replaced MediaFoundation-based MP3 reading with `Mp3FileReaderBase` (ACM → DMO fallback). This preserves the MP3 encoder-delay semantics used by osu!'s BASS engine (`BASS_CONFIG_MP3_OLDGAPS=1`), removing the per-file fixed timing offset that previously varied between songs. (`src/Core/KeyAsio.Core.Audio/Wave/AudioFileReader.cs:CreateMp3ReaderStream`)
- **CuttingEdge Score Support**: Score reading now prefers a dedicated `ScoreBase` pointer and falls back across CuttingEdge/Legacy definitions depending on the osu! build, improving accuracy on CuttingEdge and Tourney clients. (`MemoryScan.cs`, `osu_memory_rules.json`)
- **Robust Session Startup**: Beatmap filenames are now trimmed before starting a gameplay session, and null/empty values are handled explicitly instead of crashing. (`PlayingState.cs`, `GameplaySessionManager.cs`)

### Fixes
- **MP3 Timing Offset**: Fixed a per-file audio desync caused by MediaFoundation stripping LAME/Xing gapless metadata; MP3 files are now decoded frame-by-frame. (`AudioFileReader.cs`)
- **Unsupported Media Format**: MediaFoundation `MF_E_UNSUPPORTED_BYTESTREAM_TYPE` is now surfaced as a clear "Unsupported file format" error instead of an opaque COM exception. (`AudioFileReader.cs:GetMediaFoundationReader`)
- **Sync Null Handling**: Trimmed and null-checked beatmap filenames in the sync pipeline to prevent exceptions when the osu! memory state is incomplete. (`PlayingState.cs`)

### Miscellaneous
- Added `System.Management`, `System.IdentityModel.Tokens.Jwt` and `System.Security.Cryptography.ProtectedData` package references to `KeyAsio.Shared.csproj`.
- `UpdateService.CheckRulesUpdateAsync` now short-circuits in DEBUG builds to speed up local development.
- `ISyncContext` extended with `Statistics` and `HitErrors` properties; `SyncSessionContext` resets them when leaving the Playing state.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.6...v4.0.0-alpha.7