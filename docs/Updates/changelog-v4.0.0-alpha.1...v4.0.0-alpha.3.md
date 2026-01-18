[Forum page](https://osu.ppy.sh/community/forums/topics/1602658)

## Release notes

### Summary
This update brings useful monitoring features like ASIO latency display and enhances MemoryScan with configurable beatmap paths. It also addresses several UI glitches and build script issues to improve overall stability and developer experience.

### Features
- **Audio**: Added real-time display for ASIO latency and actual sample count.
- **MemoryScan**: Implemented support for user-configurable osu! beatmap directory.

### Enhancements
- **UX**: Updated instructions for output device selection to be more clear.
- **Build**: Added batch files to facilitate the build and update process.

### Fixes
- **UI**: Fixed unstable width and missing text trimming for ComboBox in SyncModePage.
- **Build**: Fixed incorrect DLL path construction logic in `DotNetDllPathPatcher`.
- **Logging**: Adjusted file logging level from Trace to Debug to reduce log volume.

### Miscellaneous
- **CI**: Added ReadyToRun and Framework-dependent build variants to GitHub Actions workflow.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.1...v4.0.0-alpha.3
