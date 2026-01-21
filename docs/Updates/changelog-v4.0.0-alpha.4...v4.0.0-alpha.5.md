[Forum page](https://osu.ppy.sh/community/forums/topics/1602658)

## Release notes

### Summary
This update introduces a brand new **Setup Wizard** to help users quickly configure performance presets, game integration, and audio devices (Still WIP in this version). On the audio processing side, multiple **Limiter Types** (including Hard Limiter) and **Balance Mode** options have been added, with related settings relocated to optimize the layout. Additionally, an audio driver status bar has been added, and automatic language adaptation for Chinese environments has been improved.

### Features
- Added **Setup Wizard** covering performance preset selection, osu! folder integration check, and detailed audio configuration sub-steps (Still WIP in this version).
- Added support for multiple **Limiter Types** (including Hard Limiter) and refactored the underlying implementation for more precise audio control.
- Added **Balance Mode** options (including Off), and moved limiter settings to the Sync Settings page for better layout.
- Added an audio driver status info bar to display driver status and provide configuration recommendations in real-time.
- (Sync) Added Replay Status tracking and Relax mod check.

### Enhancements
- Optimized Preset Selection UI with automatic detection of the currently active performance preset mode.
- Optimized Audio Settings UI; Stereo Width controls now automatically show/hide based on context.
- Improved key-only audio logic in `KeyboardBindingInitializer`. Now support skins from user's selection.
- Added configuration info logging for easier troubleshooting.

### Fixes
- Fixed incorrect Sample Volume Multiplier in Mania mode.
- Fixed Chinese localization errors related to WASAPI mode and Balance Mode.

### Miscellaneous
- Comprehensive update of Chinese localization strings.
- Refactored code logic for Wizard and Audio Limiter.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.4...v4.0.0-alpha.5
