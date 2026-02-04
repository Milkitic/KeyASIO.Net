## Release notes

### Summary
This release focuses on significantly improving the **First-Run Wizard** experience with a redesigned UI, better guidance, and localization support. The **Update System** has also been overhauled to support plugin-based updates and channel selection. Additionally, **Japanese localization** has been added.

### Features
- **New Update System**: Replaced the built-in updater with a more flexible plugin-based system, allowing for future extensibility.
- **Update Channels**: Added support for selecting update channels (e.g., Stable, Alpha), giving users more control over which versions to receive.
- **Automatic Memory Rules Update**: Added automatic checks and notifications for memory rule updates, ensuring compatibility with the latest osu! versions.
- **Japanese Localization**: Added full Japanese language support.
- **Enhanced First-Run Wizard**:
    - Enabled first-run wizard for new users.
    - Added scroll support for better navigation on smaller screens.
    - Dynamic navigation buttons that adapt to the current step.
    - Improved explanations for Hardware/Software modes with warnings.

### Enhancements
- **Wizard UI Overhaul**: Significantly improved the layout, spacing, and visual design of the configuration wizard.
- **Theme Awareness**: Updated UI components to use dynamic resources, improving consistency with the application theme.
- **Preset Descriptions**: Clarified names and descriptions for various preset modes to help users make better choices.
- **Wizard UX**: Removed unnecessary steps (like hitsound path configuration in some contexts) and improved flow.

### Fixes
- **osu! Process Detection**: Fixed an issue where connecting too early to the osu! process could fail; added a delay mechanism.
- **Update Notifications**: Fixed an issue where update toasts would appear even for versions marked as "skipped".
- **Wizard Settings**: Corrected issues with skin auto-loading and wizard re-enable logic.

### Miscellaneous
- **Build Artifacts**: Changed Windows release artifacts from `.7z` to `.zip` for better compatibility.
- **Dependencies**: Updated Satori dependency and download URLs.
- **Internal Refactoring**: Extensive refactoring of the Audio Engine and Wizard logic to improve code maintainability and testability.

**Full Changelog**: https://github.com/Milkitic/KeyASIO.Net/compare/v4.0.0-alpha.5...v4.0.0-alpha.6
