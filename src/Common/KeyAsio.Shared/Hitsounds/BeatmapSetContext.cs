using System.Diagnostics;
using Coosu.Beatmap;
using Coosu.Beatmap.Sections;
using Coosu.Beatmap.Sections.GamePlay;
using Coosu.Beatmap.Sections.HitObject;
using Coosu.Beatmap.Sections.Timing;
using Coosu.Shared;
using KeyAsio.Shared.Hitsounds.Playback;
using KeyAsio.Shared.Utils;

namespace KeyAsio.Shared.Hitsounds;

public sealed class BeatmapSetContext
{
    private static readonly Type ObjectSamplesetType = typeof(ObjectSamplesetType);
    private static readonly AsyncSequentialWorker Worker = new(name: nameof(BeatmapSetContext));

    private readonly OsuAudioFileCache _cache = new();
    private readonly string _directory;
    private bool _isInitialized;

    public BeatmapSetContext(string directory)
    {
        _directory = new DirectoryInfo(directory).FullName;
    }

    public IReadOnlyList<OsuFile> OsuFiles { get; private set; } = [];
    public IReadOnlySet<string> WaveFiles { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public async Task InitializeAsync(string? specificOsuFilename = null, bool ignoreWaveFiles = false)
    {
        var directoryInfo = new DirectoryInfo(_directory);
        var waveFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var osuFiles = new List<OsuFile>();
        await Worker.EnqueueAsync(() =>
        {
            foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                var ext = fileInfo.Extension;
                if (OsuAudioFileCache.SupportExtensions.Contains(ext))
                {
                    if (!ignoreWaveFiles)
                    {
                        waveFiles.Add(Path.GetFileNameWithoutExtension(fileInfo.Name));
                    }

                    continue;
                }

                if (!string.Equals(ext, ".osu", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (specificOsuFilename != null &&
                    !string.Equals(specificOsuFilename, fileInfo.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                osuFiles.Add(OsuFile.ReadFromFile(fileInfo.FullName));
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        WaveFiles = waveFiles;
        OsuFiles = osuFiles;
        _isInitialized = true;
    }

    public async Task<List<PlaybackEvent>> GetHitsoundNodesAsync(OsuFile osuFile)
    {
        if (_isInitialized == false) throw new Exception("The directory was not initialized");
        if (osuFile.HitObjects == null) return [];
        if (osuFile.TimingPoints == null) return [];
        if (osuFile.General == null) return [];

        var hitObjects = osuFile.HitObjects.HitObjectList;
        var elements = new List<PlaybackEvent>(hitObjects.Count);
        await Worker.EnqueueAsync(() =>
        {
            osuFile.HitObjects.ComputeSlidersByCurrentSettings();

            var hitsoundBuffer = new List<HitsoundInfo>();
            foreach (var obj in hitObjects)
            {
                try
                {
                    AddSingleHitObject(osuFile.General, osuFile.TimingPoints, obj, elements, hitsoundBuffer);
                }
                catch (Exception e)
                {
                    throw new HitsoundAnalyzingException(
                        "Error while analyzing hitsound. Object Info: " + obj.ToSerializedString(osuFile.Version), e);
                }
            }

            if (osuFile.Events?.Samples == null)
            {
                return Task.CompletedTask;
            }

            foreach (var sampleData in osuFile.Events.Samples)
            {
                elements.Add(PlaybackEvent.Create(Guid.NewGuid(), sampleData.Offset, sampleData.Volume / 100f, 0,
                    sampleData.Filename, ResourceOwner.Beatmap, SampleLayer.Sampling));
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        return elements.OrderBy(k => k.Offset).ToList();
    }

    private void AddSingleHitObject(GeneralSection generalSection, TimingSection timingSection, RawHitObject hitObject,
        List<PlaybackEvent> elements, List<HitsoundInfo> hitsoundBuffer)
    {
        var ignoreBalance = generalSection.Mode == GameMode.Taiko;
        var ignoreBase = generalSection.Mode is GameMode.Mania or GameMode.Taiko;

        if (hitObject.ObjectType != HitObjectType.Slider)
        {
            AddCircleOrSpinnerHitObject(generalSection, timingSection, hitObject, elements, hitsoundBuffer, ignoreBalance, ignoreBase);
        }
        else
        {
            AddSliderHitObject(timingSection, hitObject, elements, hitsoundBuffer, ignoreBalance, ignoreBase);
        }
    }

    private void AddCircleOrSpinnerHitObject(GeneralSection generalSection, TimingSection timingSection, RawHitObject hitObject,
        List<PlaybackEvent> elements, List<HitsoundInfo> hitsoundBuffer, bool ignoreBalance, bool ignoreBase)
    {
        var itemOffset = hitObject.ObjectType == HitObjectType.Spinner
            ? hitObject.HoldEnd // spinner
            : hitObject.Offset; // hold & circle
        var timingPoint = timingSection.GetLine(itemOffset);

        float balance = ignoreBalance ? 0 : GetObjectBalance(hitObject.X);
        float volume = GetObjectVolume(hitObject, timingPoint);

        AnalyzeHitsoundFiles(hitObject.Hitsound,
            hitObject.SampleSet, hitObject.AdditionSet,
            timingPoint, hitObject, hitsoundBuffer, ignoreBase);

        var guid = Guid.NewGuid();
        if (generalSection.Mode == GameMode.Taiko)
        {
            var (filename, resourceOwner, hitsoundType) = hitsoundBuffer.First();
            bool isBig = (hitsoundType & HitsoundType.Finish) != 0;
            bool isBlue = (hitsoundType & HitsoundType.Clap) != 0 ||
                          (hitsoundType & HitsoundType.Whistle) != 0;

            string actualFilename;
            if (isBlue && isBig)
            {
                var indexOf = filename.IndexOf('-');
                actualFilename = string.Concat("taiko-", filename.AsSpan(0, indexOf), "-hitwhistle");
            }
            else
            {
                actualFilename = "taiko-" + filename;
            }

            var element = PlaybackEvent.Create(guid, itemOffset, volume, balance, actualFilename, resourceOwner,
                hitObject.ObjectType == HitObjectType.Spinner
                    ? SampleLayer.Secondary
                    : SampleLayer.Primary);
            elements.Add(element);
        }
        else
        {
            foreach (var (filename, resourceOwner, _) in hitsoundBuffer)
            {
                var element = PlaybackEvent.Create(guid, itemOffset, volume, balance, filename, resourceOwner,
                    hitObject.ObjectType == HitObjectType.Spinner
                        ? SampleLayer.Secondary
                        : SampleLayer.Primary);
                elements.Add(element);
            }
        }
    }

    private void AddSliderHitObject(TimingSection timingSection, RawHitObject hitObject,
        List<PlaybackEvent> elements, List<HitsoundInfo> hitsoundBuffer, bool ignoreBalance, bool ignoreBase)
    {
        AddSliderEdges(timingSection, hitObject, elements, hitsoundBuffer, ignoreBalance, ignoreBase);
        AddSliderTicks(timingSection, hitObject, elements, hitsoundBuffer, ignoreBalance, ignoreBase);
        AddSliderSliding(timingSection, hitObject, elements, hitsoundBuffer, ignoreBalance, ignoreBase);
        AddSliderBalanceChanges(hitObject, elements, ignoreBalance);
    }

    private void AddSliderEdges(TimingSection timingSection, RawHitObject hitObject,
        List<PlaybackEvent> elements, List<HitsoundInfo> hitsoundBuffer, bool ignoreBalance, bool ignoreBase)
    {
        var sliderInfo = hitObject.SliderInfo!;
        bool forceUseSlide = sliderInfo.EdgeHitsounds == null;
        var sliderEdges = sliderInfo.GetEdges();
        for (var i = 0; i < sliderEdges.Length; i++)
        {
            var item = sliderEdges[i];
            var itemOffset = item.Offset;
            var timingPoint = timingSection.GetLine(itemOffset);

            float balance = ignoreBalance ? 0 : GetObjectBalance(item.Point.X);
            float volume = GetObjectVolume(hitObject, timingPoint);

            var hitsoundType = forceUseSlide
                ? hitObject.Hitsound
                : item.EdgeHitsound;
            var addition = forceUseSlide
                ? hitObject.AdditionSet
                : item.EdgeAddition;
            var sample = forceUseSlide
                ? hitObject.SampleSet
                : item.EdgeSample;

            AnalyzeHitsoundFiles(hitsoundType,
                sample, addition,
                timingPoint, hitObject, hitsoundBuffer, ignoreBase);

            var guid = Guid.NewGuid();
            foreach (var (filename, resourceOwner, _) in hitsoundBuffer)
            {
                var element = PlaybackEvent.Create(guid, (int)itemOffset, volume, balance, filename, resourceOwner,
                    i == 0 ? SampleLayer.Primary : SampleLayer.Secondary);
                elements.Add(element);
            }
        }
    }

    private void AddSliderTicks(TimingSection timingSection, RawHitObject hitObject,
        List<PlaybackEvent> elements, List<HitsoundInfo> hitsoundBuffer, bool ignoreBalance, bool ignoreBase)
    {
        var sliderInfo = hitObject.SliderInfo!;
        var ticks = sliderInfo.GetSliderTicks();
        foreach (var sliderTick in ticks)
        {
            var itemOffset = sliderTick.Offset;

            var edgeOffset = (itemOffset - sliderInfo.StartTime) / sliderInfo.CurrentSingleDuration;
            if (Math.Abs(Math.Round(edgeOffset, 0) - edgeOffset) <= 0.01)
            {
                continue;
            }

            var timingPoint = timingSection.GetLine(itemOffset);

            float balance = ignoreBalance ? 0 : GetObjectBalance(sliderTick.Point.X);
            float volume = GetObjectVolume(hitObject, timingPoint) /* * 1.25f*/; // ticks x1.25?

            AnalyzeHitsoundFiles(HitsoundType.Tick,
                hitObject.SampleSet, hitObject.AdditionSet,
                timingPoint, hitObject, hitsoundBuffer, ignoreBase);
            var (filename, resourceOwner, _) = hitsoundBuffer.First();

            var element = PlaybackEvent.Create(Guid.NewGuid(), (int)itemOffset, volume, balance, filename,
                resourceOwner, SampleLayer.Effects);
            elements.Add(element);
        }
    }

    private void AddSliderSliding(TimingSection timingSection, RawHitObject hitObject,
        List<PlaybackEvent> elements, List<HitsoundInfo> hitsoundBuffer, bool ignoreBalance, bool ignoreBase)
    {
        var slideElements = new List<PlaybackEvent>();
        var sliderEdges = hitObject.SliderInfo!.GetEdges();

        var startOffset = hitObject.Offset;
        var endOffset = sliderEdges[sliderEdges.Length - 1].Offset;
        var timingPoint = timingSection.GetLine(startOffset);

        float balance = ignoreBalance ? 0 : GetObjectBalance(hitObject.X);
        float volume = GetObjectVolume(hitObject, timingPoint);

        // start sliding
        AnalyzeHitsoundFiles(
            (hitObject.Hitsound & HitsoundType.SlideWhistle) | HitsoundType.Slide,
            hitObject.SampleSet, hitObject.AdditionSet,
            timingPoint, hitObject, hitsoundBuffer, ignoreBase);

        foreach (var (filename, resourceOwner, hitsoundType) in hitsoundBuffer)
        {
            LoopChannel channel;
            if (hitsoundType.HasFlag(HitsoundType.Slide))
                channel = LoopChannel.Normal;
            else if (hitsoundType.HasFlag(HitsoundType.SlideWhistle))
                channel = LoopChannel.Whistle;
            else
                continue;

            var element =
                PlaybackEvent.CreateLoopSignal(startOffset, volume, balance, filename, resourceOwner, channel);
            slideElements.Add(element);
        }

        // change sample (will optimize if only adjust volume) by timing points
        var timingsOnSlider = timingSection.TimingList
            .Where(k => k.Offset > startOffset + 0.5 && k.Offset < endOffset);

        var prevTiming = timingPoint;

        foreach (var timing in timingsOnSlider)
        {
            if (timing.Track != prevTiming.Track ||
                timing.TimingSampleset != prevTiming.TimingSampleset)
            {
                volume = GetObjectVolume(hitObject, timing);
                AnalyzeHitsoundFiles(
                    (hitObject.Hitsound & HitsoundType.SlideWhistle) | HitsoundType.Slide,
                    hitObject.SampleSet, hitObject.AdditionSet,
                    timing, hitObject, hitsoundBuffer, ignoreBase);

                foreach (var (filename, resourceOwner, hitsoundType) in hitsoundBuffer)
                {
                    PlaybackEvent element;
                    if (hitsoundType.HasFlag(HitsoundType.Slide) && slideElements
                            .Last(k => k is ControlEvent { ControlEventType: ControlEventType.LoopStart })
                            .Filename == filename)
                    {
                        // optimize by only change volume
                        element = PlaybackEvent.CreateLoopVolumeSignal((int)timing.Offset, volume);
                    }
                    else
                    {
                        LoopChannel channel;
                        if (hitsoundType.HasFlag(HitsoundType.Slide))
                            channel = LoopChannel.Normal;
                        else if (hitsoundType.HasFlag(HitsoundType.SlideWhistle))
                            channel = LoopChannel.Whistle;
                        else
                            continue;

                        // new sample
                        element = PlaybackEvent.CreateLoopSignal((int)timing.Offset, volume, balance,
                            filename, resourceOwner, channel);
                    }

                    slideElements.Add(element);
                }

                prevTiming = timing;
            }
        }

        // end slide
        var stopElement = PlaybackEvent.CreateLoopStopSignal((int)endOffset, LoopChannel.Normal);
        var stopElement2 = PlaybackEvent.CreateLoopStopSignal((int)endOffset, LoopChannel.Whistle);
        slideElements.Add(stopElement);
        slideElements.Add(stopElement2);
        foreach (var slideElement in slideElements)
        {
            elements.Add(slideElement);
        }
    }

    private static void AddSliderBalanceChanges(RawHitObject hitObject, List<PlaybackEvent> elements, bool ignoreBalance)
    {
        // change balance while sliding (not supported in original game)
        var trails = hitObject.SliderInfo!.GetSliderSlides();
        foreach (var sliderTick in trails)
        {
            var balanceElement = PlaybackEvent.CreateLoopBalanceSignal((int)sliderTick.Offset,
                ignoreBalance ? 0 : GetObjectBalance(sliderTick.Point.X));
            elements.Add(balanceElement);
        }
    }

    private void AnalyzeHitsoundFiles(
        HitsoundType itemHitsound,
        ObjectSamplesetType itemSample,
        ObjectSamplesetType itemAddition,
        TimingPoint timingPoint,
        RawHitObject hitObject,
        List<HitsoundInfo> hitsoundBuffer,
        bool ignoreBase)
    {
        hitsoundBuffer.Clear();
        if (!string.IsNullOrEmpty(hitObject.FileName))
        {
            var filename = _cache.GetFileUntilFind(_directory,
                Path.GetFileNameWithoutExtension(hitObject.FileName)!,
                out _);
            hitsoundBuffer.Add(new HitsoundInfo(filename, ResourceOwner.Beatmap, itemHitsound));
            return;
        }

        // hitnormal, sliderslide
        string sampleStr = Enum.IsDefined(ObjectSamplesetType, itemSample) &&
                           itemSample != Coosu.Beatmap.Sections.HitObject.ObjectSamplesetType.Auto
            ? itemSample.ToHitsoundString(null)!
            : timingPoint.TimingSampleset.ToHitsoundString();

        // hitclap, hitfinish, hitwhistle, slidertick, sliderwhistle
        string additionStr = Enum.IsDefined(ObjectSamplesetType, itemAddition)
            ? itemAddition.ToHitsoundString(sampleStr)!
            : timingPoint.TimingSampleset.ToHitsoundString();

        Debug.Assert(sampleStr != null);
        Debug.Assert(additionStr != null);

        if (hitObject.ObjectType == HitObjectType.Slider && hitObject.SliderInfo!.EdgeHitsounds == null)
        {
            GetHitsounds(hitObject, itemHitsound, sampleStr, additionStr, hitsoundBuffer);
        }
        else
        {
            GetHitsounds(hitObject, itemHitsound, sampleStr, additionStr, hitsoundBuffer, ignoreBase);
        }

        Span<char> chars = stackalloc char[32];
        for (var i = 0; i < hitsoundBuffer.Count; i++)
        {
            var fileNameWithoutIndex = hitsoundBuffer[i].Filename;
            var hitsoundType = hitsoundBuffer[i].HitsoundType;

            int baseIndex = hitObject.CustomIndex > 0 ? hitObject.CustomIndex : timingPoint.Track;
            string indexStr = baseIndex > 1 ? baseIndex.ToString() : "";

            var fileNameWithoutExt = fileNameWithoutIndex + indexStr;

            ResourceOwner resourceOwner;
            using var filenameVsb = new ValueStringBuilder(chars);
            if (timingPoint.Track == 0)
            {
                filenameVsb.Append(fileNameWithoutExt);
                resourceOwner = ResourceOwner.UserSkin;
            }
            else if (WaveFiles.Contains(fileNameWithoutExt))
            {
                filenameVsb.Append(_cache.GetFileUntilFind(_directory, fileNameWithoutExt, out resourceOwner));
            }
            else
            {
                filenameVsb.Append(fileNameWithoutIndex);
                resourceOwner = ResourceOwner.UserSkin;
            }

            hitsoundBuffer[i] = new HitsoundInfo(filenameVsb.ToString(), resourceOwner, hitsoundType);
        }
    }

    private static float GetObjectBalance(float x)
    {
        if (x > 512) x = 512;
        else if (x < 0) x = 0;

        float balance = (x - 256f) / 256f;
        return balance;
    }

    private static float GetObjectVolume(RawHitObject obj, TimingPoint timingPoint)
    {
        return (obj.SampleVolume != 0 ? obj.SampleVolume : timingPoint.Volume) / 100f;
    }

    private static void GetHitsounds(RawHitObject rawHitObject, HitsoundType type,
        string? sampleStr, string? additionStr, List<HitsoundInfo> result, bool ignoreBase = false)
    {
        if (type == HitsoundType.Tick)
        {
            result.Add(new HitsoundInfo(additionStr + "-slidertick", ResourceOwner.Beatmap, type));
            return;
        }

        if (type.HasFlag(HitsoundType.Slide))
        {
            result.Add(new HitsoundInfo(sampleStr + "-sliderslide", ResourceOwner.Beatmap, type));
            if (rawHitObject.Hitsound == HitsoundType.Whistle)
                result.Add(new HitsoundInfo(additionStr + "-sliderwhistle", ResourceOwner.Beatmap, HitsoundType.SlideWhistle));
        }

        if (type.HasFlag(HitsoundType.SlideWhistle))
            result.Add(new HitsoundInfo(additionStr + "-sliderwhistle", ResourceOwner.Beatmap, HitsoundType.SlideWhistle));

        if (type.HasFlag(HitsoundType.Slide) || type.HasFlag(HitsoundType.SlideWhistle))
            return;

        if (type.HasFlag(HitsoundType.Whistle))
            result.Add(new HitsoundInfo(additionStr + "-hitwhistle", ResourceOwner.Beatmap, type));
        if (type.HasFlag(HitsoundType.Clap))
            result.Add(new HitsoundInfo(additionStr + "-hitclap", ResourceOwner.Beatmap, type));
        if (type.HasFlag(HitsoundType.Finish))
            result.Add(new HitsoundInfo(additionStr + "-hitfinish", ResourceOwner.Beatmap, type));

        if (ignoreBase && type != 0)
            return;

        if (type.HasFlag(HitsoundType.Normal) ||
            (type & HitsoundType.Normal) == 0)
            result.Add(new HitsoundInfo(sampleStr + "-hitnormal", ResourceOwner.Beatmap, type));
    }

    private readonly struct HitsoundInfo
    {
        public readonly string Filename;
        public readonly ResourceOwner ResourceOwner;
        public readonly HitsoundType HitsoundType;

        public HitsoundInfo(string filename, ResourceOwner resourceOwner, HitsoundType hitsoundType)
        {
            Filename = filename;
            ResourceOwner = resourceOwner;
            HitsoundType = hitsoundType;
        }

        public void Deconstruct(out string filename, out ResourceOwner resourceOwner, out HitsoundType hitsoundType)
        {
            filename = Filename;
            resourceOwner = ResourceOwner;
            hitsoundType = HitsoundType;
        }
    }
}