// See https://aka.ms/new-console-template for more information

using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Audio;
using KeyAsio.Shared;
using KeyAsio.Shared.Models;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
var appSettings = new AppSettings();
services.AddSingleton(appSettings);
services.AddSingleton<SharedViewModel>();
services.AddSingleton<AudioCacheService>();
services.AddSingleton<HitsoundNodeService>();
services.AddSingleton<MusicTrackService>();
services.AddSingleton<AudioPlaybackService>();
services.AddSingleton<PlaySessionManager>();
services.AddSingleton<RealtimeProperties>();
services.AddSingleton<RealtimeModeManager>();
var provider = services.BuildServiceProvider();

var audioEngine = provider.GetRequiredService<AudioEngine>();
var realtimeProperties = provider.GetRequiredService<RealtimeProperties>();
var playSessionService = provider.GetRequiredService<PlaySessionManager>();
var hitsoundNodeService = provider.GetRequiredService<HitsoundNodeService>();
var osuDir = new OsuDirectory(@"E:\Games\osu!\Songs\807527 IOSYS - Miracle Hinacle");
await osuDir.InitializeAsync("IOSYS - Miracle Hinacle (FAMoss) [Lunatic].osu", ignoreWaveFiles: false);
var osuFile = osuDir.OsuFiles[0];
playSessionService.OsuFile = osuFile;

var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
var logger = provider.GetRequiredService<ILogger<ManiaAudioProvider>>();
var cacheService = provider.GetRequiredService<AudioCacheService>();
var playSession = provider.GetRequiredService<PlaySessionManager>();
var maniaAudioProvider = new ManiaAudioProvider(logger, appSettings, realtimeProperties, audioEngine, cacheService, playSession);

List<PlayableNode> keyList = new();
List<HitsoundNode> playbackList = new();

maniaAudioProvider.FillAudioList(hitsoundList, keyList, playbackList);
hitsoundNodeService.KeyList.AddRange(keyList);
maniaAudioProvider.ResetNodes(0);

return;