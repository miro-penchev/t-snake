using TSnake.Core;
using TSnake.Screens;

namespace TSnake.Tests;

public class EndMessagesTests
{
    [Theory]
    [InlineData(EndReason.HitSelf)]
    [InlineData(EndReason.HitObstacle)]
    [InlineData(EndReason.BoardFull)]
    public void EveryEndingReasonHasANonEmptyHeadline(EndReason reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(EndMessages.Headline(reason)));
    }

    [Fact]
    public void TheThreeOutcomesHaveDistinctHeadlines()
    {
        string self = EndMessages.Headline(EndReason.HitSelf);
        string obstacle = EndMessages.Headline(EndReason.HitObstacle);
        string boardFull = EndMessages.Headline(EndReason.BoardFull);

        Assert.Equal(3, new HashSet<string> { self, obstacle, boardFull }.Count);
    }

    [Fact]
    public void TheWinHeadlineReadsAsAWin()
    {
        Assert.Contains("win", EndMessages.Headline(EndReason.BoardFull), StringComparison.OrdinalIgnoreCase);
    }
}
