# Changelog

## v4.0.0-alpha.1

### Summary
This release introduces a major architectural overhaul, extracting the audio engine into a standalone `KeyAsio.Audio` project and upgrading the framework to .NET 10. Performance has been significantly boosted with SIMD/AVX-512 vectorization support for audio processing. Additionally, the application now utilizes a modern State Machine for logic management and fully adopts Dependency Injection.

### Features
- **Professional Audio Limiter**: Added professional-grade audio limiters with unified interfaces for better dynamic range control.
- **Audio Object Pooling**: Implemented object pooling for audio providers to reduce memory allocation overhead.
- **SIMD/AVX-512 Support**: Introduced vectorized operations for audio mixing and sample conversion, delivering substantial performance gains.
- **State Machine**: Implemented a real-time state machine to handle complex playback state transitions and combined changes.

### Enhancements
- **.NET 10 Upgrade**: Upgraded project framework to .NET 10.0.
- **Audio Engine Extraction**: Decoupled core audio logic into a separate `KeyAsio.Audio` project.
- **Dependency Injection**: Refactored the entire application to use Dependency Injection (DI) for better service management.
- **Logging**: Migrated the logging system to `Microsoft.Extensions.Logging`.
- **Service Isolation**: Extracted logic for audio caching, playback, and hitsound management into dedicated services (`AudioCacheService`, `AudioPlaybackService`).
- **Parallel Caching**: Refactored audio caching to use `Parallel.ForEachAsync` for faster loading.

### Performance
- **Vectorized Audio Processing**: Optimized PCM16-to-float conversion and mixing using SIMD/AVX-512 instructions.
- **Unmanaged Memory**: Utilized unmanaged memory for audio caching to reduce GC pressure.
- **Async Audio Recycling**: Moved audio recycling logic to a dedicated thread.
- **Memory Optimization**: Improved memory alignment and bounds checking; optimized buffer usage.

### Fixes
- Fixed resource disposal issues in audio caching to prevent memory leaks.
- Fixed unhandled Tasks when stopping music and issues with Mods change notifications.
- Improved memory scan initialization in `ProcessMemoryDataFinder`.

### Miscellaneous
- Added NAudio as a submodule dependency.
- Updated NuGet package dependencies.
- Added a benchmark project for performance testing audio conversions.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v3.2.0...v4.0.0-alpha.1
