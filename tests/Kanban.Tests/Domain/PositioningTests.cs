using Kanban.Domain.Boards;

namespace Kanban.Tests.Domain;

public class PositioningTests
{
    [Theory]
    [InlineData(null, null, Positioning.Step)]
    [InlineData(1024.0, null, 2048.0)]
    [InlineData(null, 1024.0, 0.0)]
    [InlineData(1024.0, 2048.0, 1536.0)]
    public void Between_returns_expected_position(double? before, double? after, double expected)
    {
        Assert.Equal(expected, Positioning.Between(before, after));
    }

    [Fact]
    public void At_rebalances_when_gap_is_exhausted()
    {
        var rebalanced = false;
        var position = Positioning.At([1.0, 1.0 + 1e-9], 1, () => rebalanced = true);

        Assert.True(rebalanced);
        Assert.Equal(1.5 * Positioning.Step, position);
    }
}
