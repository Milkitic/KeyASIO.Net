// See https://aka.ms/new-console-template for more information

using Coosu.Beatmap;
using Coosu.Beatmap.Extensions.Playback;
using KeyAsio.Gui.Realtime;
using KeyAsio.Gui.Realtime.AudioProviders;

var realtimeModeManager = new RealtimeModeManager();
var osuDir = new OsuDirectory(@"E:\Games\osu!\Songs\807527 IOSYS - Miracle Hinacle");
await osuDir.InitializeAsync("IOSYS - Miracle Hinacle (FAMoss) [Lunatic].osu", ignoreWaveFiles: false);
var osuFile = osuDir.OsuFiles[0];
realtimeModeManager.OsuFile = osuFile;

var hitsoundList = await osuDir.GetHitsoundNodesAsync(osuFile);
var maniaAudioProvider = new ManiaAudioProvider(realtimeModeManager);

List<PlayableNode> keyList = new();
List<HitsoundNode> playbackList = new();

maniaAudioProvider.FillAudioList(hitsoundList, keyList, playbackList);
realtimeModeManager.KeyList.AddRange(keyList);
maniaAudioProvider.ResetNodes(0);

return;