[Forum page](https://osu.ppy.sh/community/forums/topics/1602658)

## Release notes

### Features
- Added support for v4 release and upgrade button.
- Added toolbar content support to dialog window.
- Merged memory scanning implementation from v4.

### Enhancements
- Replaced `OsuMemoryDataProvider` with v4 memory reading implementation.
- Refactored code to remove `IDisposable` from ref structs and cleaned up csproj.

### Miscellaneous
- Updated package dependencies to latest versions.
- Removed unused git submodules and `.gitmodules` file.
- Fixed CI publish trigger to only run on tags.
- Enabled CI build and release on `maint/3.x` push.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v3.1.2...v3.2.0
