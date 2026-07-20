using System.Buffers;
using KeyAsio.Sync.Sources;
using OverlayAPI.LazerProtocol;

namespace KeyAsio.UnitTests;

public sealed class LazerProtocolTests
{
    [Fact]
    public void Protocol_ContainsOnlyRequiredFieldKinds()
    {
        Assert.Equal(
        [
            "ProcessId",
            "Status",
            "PlayTime",
            "Mods",
            "Combo",
            "IsReplay",
            "BeatmapFilename",
            "BeatmapFiles",
            "SkinInfos",
            "BeatmapOffset"
        ], Enum.GetNames<LazerFieldKind>());
    }

    [Theory]
    [InlineData("Score")]
    [InlineData("Username")]
    [InlineData("Statistics")]
    [InlineData("HitErrors")]
    [InlineData("UserDataDirectory")]
    [InlineData("ExeDirectory")]
    [InlineData("BeatmapFolder")]
    public void SensitiveOrNonessentialFields_AreNotPartOfProtocol(string fieldName)
    {
        Assert.DoesNotContain(fieldName, Enum.GetNames<LazerFieldKind>());
        Assert.DoesNotContain(fieldName, Enum.GetNames<LazerFieldMask>());
    }

    [Fact]
    public void BeatmapOffset_RoundTripsWithoutLosingPrecision()
    {
        var frame = new LazerDeltaFrame
        {
            Fields =
            [
                LazerDeltaField.ForDouble(LazerFieldKind.BeatmapOffset, -12.3)
            ]
        };
        var writer = new ArrayBufferWriter<byte>();

        frame.Write(writer);
        var parsed = LazerDeltaFrame.Parse(writer.WrittenSpan);

        Assert.Equal(LazerProtocolConstants.ProtocolVersion, parsed.Version);
        var field = Assert.Single(parsed.Fields);
        Assert.Equal(LazerFieldKind.BeatmapOffset, field.Kind);
        Assert.Equal(-12.3, field.DoubleValue);
    }

    [Fact]
    public void MinimalSkinInfo_RoundTrips()
    {
        var frame = new LazerDeltaFrame
        {
            Fields =
            [
                LazerDeltaField.ForSkinInfos(
                [
                    new LazerSkinInfo
                    {
                        Id = "skin-id",
                        Name = "skin-name",
                        Files = [new LazerFile { Name = "normal-hitnormal.wav", Path = "hashed-file" }]
                    }
                ])
            ]
        };
        var writer = new ArrayBufferWriter<byte>();

        frame.Write(writer);
        var parsed = LazerDeltaFrame.Parse(writer.WrittenSpan);

        var skin = Assert.Single(Assert.Single(parsed.Fields).SkinInfosValue!);
        Assert.Equal("skin-id", skin.Id);
        Assert.Equal("skin-name", skin.Name);
        Assert.Equal("normal-hitnormal.wav", Assert.Single(skin.Files).Name);
    }

    [Fact]
    public void PreviousProtocolVersion_ParsesWithoutBeatmapOffset()
    {
        var frame = new LazerDeltaFrame
        {
            Version = LazerProtocolConstants.ProtocolVersion,
            Fields =
            [
                LazerDeltaField.ForInt(LazerFieldKind.PlayTime, 1234)
            ]
        };
        var writer = new ArrayBufferWriter<byte>();

        frame.Write(writer);
        var parsed = LazerDeltaFrame.Parse(writer.WrittenSpan);

        Assert.Equal(LazerProtocolConstants.ProtocolVersion, parsed.Version);
        Assert.Equal(1234, Assert.Single(parsed.Fields).IntValue);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void LazerFrame_NonFiniteBeatmapOffsetFallsBackToZero(double offset)
    {
        var frame = new LazerIpcFrame();

        frame.Apply(new LazerDeltaFrame
        {
            Fields =
            [
                LazerDeltaField.ForDouble(LazerFieldKind.BeatmapOffset, offset)
            ]
        });

        Assert.Equal(0, frame.BeatmapOffset);
    }
}
