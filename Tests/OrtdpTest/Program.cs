// See https://aka.ms/new-console-template for more information

using System;
using OsuRTDataProvider;
using OsuRTDataProvider.BeatmapInfo;
using OsuRTDataProvider.Listen;
using OsuRTDataProvider.Mods;

Console.WriteLine("Hello, World!");
Setting.ListenInterval = 5;
var manager = new OsuListenerManager()
{
    
};
manager.OnPlayingTimeChanged += Manager_OnPlayingTimeChanged;
manager.OnStatusChanged += OnStatusChanged;
manager.OnComboChanged += Manager_OnComboChanged;
manager.OnHitEventsChanged += Manager_OnHitEventsChanged;
void Manager_OnHitEventsChanged(PlayType playType, System.Collections.Generic.List<HitEvent> hitEvents)
{
    Logger.Info($"Current HitEvents:{playType}");
}

manager.Start();

while (true)
{
    Console.ReadKey(true);
}

void Manager_OnPlayingTimeChanged(int ms)
{
    Logger.Info(ms.ToString());
}
void OnStatusChanged(OsuListenerManager.OsuStatus l, OsuListenerManager.OsuStatus c) =>
    Logger.Info($"Current Game Status:{c}");

void Manager_OnComboChanged(int combo)
{
    Logger.Info($"Current combo:{combo}");
}
