// See https://aka.ms/new-console-template for more information

using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Shared.Models;
using KeyAsio.Shared;
using KeyAsio.Shared.Realtime;
using KeyAsio.Shared.Realtime.AudioProviders;
using KeyAsio.Shared.Realtime.Services;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton(new AppSettings());
services.AddSingleton<SharedViewModel>();
services.AddSingleton<AudioCacheService>();
services.AddSingleton<HitsoundNodeService>();
services.AddSingleton<MusicTrackService>();
services.AddSingleton<AudioPlaybackService>();
services.AddSingleton<RealtimeModeManager>();
var provider = services.BuildServiceProvider();

var sharedViewModel = provider.GetRequiredService<SharedViewModel>();
var realtimeModeManager = provider.GetRequiredService<RealtimeModeManager>();
var osuDir = new OsuDirectory(@"E:\Games\osu!\Songs\807527 IOSYS - Miracle Hinacle");
await osuDir.InitializeAsync("IOSYS - Miracle Hinacle (FAMoss) [Lunatic].osu", ignoreWaveFiles: false);
var osuFile = osuDir.OsuFiles[0];
realtimeModeManager.OsuFile = osuFile;

var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
var maniaAudioProvider = new ManiaAudioProvider(realtimeModeManager, sharedViewModel);

List<PlayableNode> keyList = new();
List<HitsoundNode> playbackList = new();

maniaAudioProvider.FillAudioList(hitsoundList, keyList, playbackList);
realtimeModeManager.KeyList.AddRange(keyList);
maniaAudioProvider.ResetNodes(0);

return;