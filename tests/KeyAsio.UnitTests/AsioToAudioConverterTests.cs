using System.Globalization;
using KeyAsio.Converters;

namespace KeyAsio.UnitTests;

public class AsioToAudioConverterTests
{
    [Theory]
    [InlineData("ASIO", "Audio")]
    [InlineData("KeyASIO ASIO Backend", "KeyAudio Audio Backend")]
    [InlineData("asio", "asio")]
    [InlineData("WASAPI", "WASAPI")]
    public void Convert_ReplacesOnlyAsioText(string value, string expected)
    {
        var result = AsioToAudioConverter.Instance.Convert(
            value,
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_NonStringValue_ReturnsOriginalValue()
    {
        object value = 42;

        var result = AsioToAudioConverter.Instance.Convert(
            value,
            typeof(object),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.Same(value, result);
    }
}
