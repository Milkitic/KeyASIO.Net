using System.Buffers;
using KeyAsio.LazerProtocol;
using KeyAsio.Sync.Sources;

namespace KeyAsio.UnitTests;

public sealed class LazerProtocolTests
{
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
