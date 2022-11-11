// See https://aka.ms/new-console-template for more information

using System;
using OsuRTDataProvider;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;

Console.WriteLine("Hello, World!");
Setting.ListenInterval = 5;
var manager = new OsuListenerManager()
{

};
manager.OnBeatmapChanged += Manager_OnBeatmapChanged;

void Manager_OnBeatmapChanged(Beatmap map)
{
    Logger.Info(map.Filename);
}

manager.OnPlayingTimeChanged += Manager_OnPlayingTimeChanged;
manager.OnStatusChanged += OnStatusChanged;
manager.OnComboChanged += Manager_OnComboChanged;
manager.OnAccuracyChanged += Manager_OnAccuracyChanged;
manager.OnCount100Changed += Manager_OnCount100Changed;
manager.OnCount300Changed += Manager_OnCount300Changed;
manager.OnHealthPointChanged += Manager_OnHealthPointChanged;
manager.OnScoreChanged += Manager_OnScoreChanged;

void Manager_OnScoreChanged(int obj)
{
    Logger.Info($"score: {obj}");
}

void Manager_OnHealthPointChanged(double hp)
{
    Logger.Info($"hp: {hp}");
}

void Manager_OnCount300Changed(int hit)
{
    Logger.Info($"Count 300: {hit}");
}

void Manager_OnCount100Changed(int hit)
{
    Logger.Info($"Count 100: {hit}");
}

void Manager_OnAccuracyChanged(double acc)
{
    Logger.Info($"Current acc: {acc}");
}

void Manager_OnPlayingTimeChanged(int ms)
{
    Logger.Info(ms.ToString());
}
void OnStatusChanged(OsuListenerManager.OsuStatus l, OsuListenerManager.OsuStatus c) =>
    Logger.Info($"Current Game Status: {c}");

void Manager_OnComboChanged(int combo)
{
    Logger.Info($"Current combo: {combo}");
}


//manager.OnHitEventsChanged += Manager_OnHitEventsChanged;
//void Manager_OnHitEventsChanged(PlayType playType, System.Collections.Generic.List<HitEvent> hitEvents)
//{
//    Logger.Info($"Current HitEvents:{playType}");
//}

manager.Start();

while (true)
{
    Console.ReadKey(true);
}