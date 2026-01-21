## Release notes

### Summary
This release mainly fixes bugs related to the KeyASIO launcher and audio processing, and refactors and optimizes internal components.

### Enhancements
- Refactor internal collection implementations (replaced `RangeObservableCollection`).
- Optimize file reading logic in SkinManager (using `Coosu.Shared.IO`).

### Fixes
- Fix handling of `SignalKeepAlive` in `EnhancedVolumeSampleProvider`.
- Fix KeyASIO launcher to correctly start the original executable using the `start` command.

### Miscellaneous
- Code cleanup and documentation updates.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.3...v4.0.0-alpha.4
