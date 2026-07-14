# Architecture

The solution uses the application executable as the composition root. Lower layers do not resolve services from an
`IServiceProvider`; dependencies are supplied through constructors or narrow capability interfaces.

## Project boundaries

- `KeyAsio.Common` contains implementation-independent utilities and collections.
- `KeyAsio.Plugins.Contracts` is the only API surface plugins should compile against. It depends only on stable public
  data/audio packages and never exposes the host service container.
- `KeyAsio.Configuration` owns persisted settings and their serialization.
- `KeyAsio.Core.*` owns audio, memory and beatmap-audio primitives.
- `KeyAsio.Sync` owns game-state acquisition and synchronized playback orchestration.
- `KeyAsio.Application` owns host runtime services, plugin loading, localization and skin discovery.
- `KeyAsio` owns Avalonia/Suki presentation, OS integration and dependency injection composition.

Dependencies point inward toward contracts and primitives. In particular, `KeyAsio.Sync` has no dependency on
`KeyAsio.Application`, and neither plugin contracts nor application services depend on Avalonia/Suki.

## Audio device transactions

All application-level device changes go through `IAudioDeviceOperationCoordinator`. A transition is serialized,
stops the current device, starts the requested device, and only then commits settings. A failed start or persistence
operation restores both the previous runtime device and the previous persisted configuration. Cache invalidation is a
post-transition concern and cannot corrupt the transaction.

## Plugin contract migration

The contract is intentionally breaking. A plugin project should reference `KeyAsio.Plugins.Contracts` and must not
reference `KeyAsio.Application`, `KeyAsio.Configuration`, `KeyAsio.Sync`, Avalonia host services, Suki managers or
Octokit.

| Previous dependency | Contract capability |
| --- | --- |
| Root `IServiceProvider` | Explicit properties on `IPluginContext` |
| `AppSettings` | `IPluginSettings` |
| `GameplaySessionManager` / `SyncSessionContext` | `IGameplaySession` |
| `AudioCacheManager` / `CachedAudio` | `IGameplaySession.TryCreateCachedAudioProvider` |
| Beatmap resource catalog implementation | `TryResolveResource` / `TryResolveAudioResource` |
| Dispatcher, dialog and toast managers | `IPluginInteractionService` |
| Logger resolved from DI | `IPluginContext.LoggerFactory` |
| `Octokit.Release` | `UpdateRelease` and `UpdateAsset` |

The host shares already-loaded assemblies with each collectible plugin load context and resolves plugin-private managed
and native dependencies from the plugin output directory. This preserves type identity for the contract while keeping
plugin-only dependencies unloadable.
