// See https://aka.ms/new-console-template for more information

using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Sync;
using KeyAsio.Shared.Sync.AudioProviders;
using KeyAsio.Shared.Sync.Services;
using KeyAsio.Plugins.Abstractions;
using KeyAsio.Shared.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
var appSettings = new AppSettings();
services.AddSingleton(appSettings);
services.AddSingleton<SharedViewModel>();
services.AddSingleton<GameplayAudioService>();
services.AddSingleton<BeatmapHitsoundLoader>();
services.AddSingleton<SfxPlaybackService>();
services.AddSingleton<IPluginManager, PluginManager>();
services.AddSingleton<GameplaySessionManager>();
services.AddSingleton<SyncSessionContext>();
services.AddSingleton<SyncController>();
var provider = services.BuildServiceProvider();

var audioEngine = provider.GetRequiredService<AudioEngine>();
var realtimeProperties = provider.GetRequiredService<SyncSessionContext>();
var playSessionService = provider.GetRequiredService<GameplaySessionManager>();
var hitsoundNodeService = provider.GetRequiredService<BeatmapHitsoundLoader>();
var osuDir = new OsuDirectory(@"E:\Games\osu!\Songs\807527 IOSYS - Miracle Hinacle");
await osuDir.InitializeAsync("IOSYS - Miracle Hinacle (FAMoss) [Lunatic].osu", ignoreWaveFiles: false);
var osuFile = osuDir.OsuFiles[0];
playSessionService.OsuFile = osuFile;

var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
var logger = provider.GetRequiredService<ILogger<ManiaHitsoundSequencer>>();
var cacheService = provider.GetRequiredService<GameplayAudioService>();
var playSession = provider.GetRequiredService<GameplaySessionManager>();
var maniaAudioProvider = new ManiaHitsoundSequencer(logger, appSettings, realtimeProperties, audioEngine, cacheService, playSession);

List<PlayableNode> keyList = new();
List<HitsoundNode> playbackList = new();

maniaAudioProvider.FillAudioList(hitsoundList, keyList, playbackList);
hitsoundNodeService.KeyList.AddRange(keyList);
maniaAudioProvider.SeekTo(0);

return;