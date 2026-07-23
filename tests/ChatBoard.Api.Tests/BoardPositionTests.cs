using ChatBoard.Api.Domain;

namespace ChatBoard.Api.Tests;

public class BoardPositionTests
{
    [Fact]
    public void Between_WithBothNeighbours_ReturnsMidpoint()
    {
        Assert.Equal(1500d, BoardPosition.Between(1000d, 2000d));
    }

    [Fact]
    public void Between_AtTopOfColumn_SitsAboveTheFirstCard()
    {
        // No card above, one below: land a gap above it.
        Assert.Equal(2000d - BoardPosition.Gap, BoardPosition.Between(null, 2000d));
    }

    [Fact]
    public void Between_AtBottomOfColumn_SitsBelowTheLastCard()
    {
        // One card above, none below: land a gap below it.
        Assert.Equal(1000d + BoardPosition.Gap, BoardPosition.Between(1000d, null));
    }

    [Fact]
    public void Between_InEmptyColumn_ReturnsGap()
    {
        Assert.Equal(BoardPosition.Gap, BoardPosition.Between(null, null));
    }

    [Fact]
    public void NeedsRebalance_WhenNeighboursHaveConverged_IsTrue()
    {
        Assert.True(BoardPosition.NeedsRebalance(1000d, 1000d + (BoardPosition.RebalanceThreshold / 2d)));
    }

    [Fact]
    public void NeedsRebalance_WhenNeighboursAreComfortable_IsFalse()
    {
        Assert.False(BoardPosition.NeedsRebalance(1000d, 2000d));
    }

    [Fact]
    public void Rebalanced_ReturnsEvenlySpacedPositions()
    {
        Assert.Equal(new[] { 1024d, 2048d, 3072d }, BoardPosition.Rebalanced(3));
    }
}
