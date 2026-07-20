using KeyAsio.Configuration.Models;
using KeyAsio.Sync.States;

namespace KeyAsio.UnitTests;

public sealed class PlayingStateTests
{
    [Theory]
    [InlineData(0, 30, 0, false)]
    [InlineData(1, 19, 0, false)]
    [InlineData(1, 20, 0, true)]
    [InlineData(1, 30, 29, true)]
    [InlineData(1, 30, 30, false)]
    public void StableComboBreak_PreservesMemoryWorkarounds(int score, int oldCombo, int newCombo, bool expected)
    {
        Assert.Equal(expected,
            PlayingState.ShouldPlayComboBreak(GameClientType.Stable, score, oldCombo, newCombo));
    }

    [Theory]
    [InlineData(0, 20, 0, false)]
    [InlineData(0, 21, 0, true)]
    [InlineData(0, 30, 29, false)]
    [InlineData(0, 30, 30, false)]
    public void LazerComboBreak_DoesNotRequireScoreOrJudgements(int score, int oldCombo, int newCombo, bool expected)
    {
        Assert.Equal(expected,
            PlayingState.ShouldPlayComboBreak(GameClientType.Lazer, score, oldCombo, newCombo));
    }
}
