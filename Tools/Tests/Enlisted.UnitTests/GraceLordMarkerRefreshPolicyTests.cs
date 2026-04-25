using Enlisted.Features.Enlistment.Core;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class GraceLordMarkerRefreshPolicyTests
{
    [Fact]
    public void GetScope_ReturnsLeave_WhenPlayerIsOnLeave()
    {
        var scope = GraceLordMarkerRefreshPolicy.GetScope(isOnLeave: true, isInDesertionGracePeriod: false);

        Assert.Equal(LordMarkerScope.Leave, scope);
    }

    [Fact]
    public void GetScope_ReturnsGrace_WhenGracePeriodIsActive()
    {
        var scope = GraceLordMarkerRefreshPolicy.GetScope(isOnLeave: false, isInDesertionGracePeriod: true);

        Assert.Equal(LordMarkerScope.Grace, scope);
    }

    [Fact]
    public void GetScope_ReturnsNone_WhenNeitherLeaveNorGraceIsActive()
    {
        var scope = GraceLordMarkerRefreshPolicy.GetScope(isOnLeave: false, isInDesertionGracePeriod: false);

        Assert.Equal(LordMarkerScope.None, scope);
    }

    [Fact]
    public void ShouldRefresh_WhenThereIsNoPreviousSnapshot()
    {
        var current = new GraceLordMarkerSnapshot("party:lord", 10d, 15d);

        var shouldRefresh = GraceLordMarkerRefreshPolicy.ShouldRefresh(null, current, 5d);

        Assert.True(shouldRefresh);
    }

    [Fact]
    public void ShouldNotRefresh_WhenAnchorMatchesAndMovementIsBelowThreshold()
    {
        var previous = new GraceLordMarkerSnapshot("party:lord", 10d, 15d);
        var current = new GraceLordMarkerSnapshot("party:lord", 12d, 16d);

        var shouldRefresh = GraceLordMarkerRefreshPolicy.ShouldRefresh(previous, current, 5d);

        Assert.False(shouldRefresh);
    }

    [Fact]
    public void ShouldRefresh_WhenAnchorChanges()
    {
        var previous = new GraceLordMarkerSnapshot("settlement:town_A", 10d, 15d);
        var current = new GraceLordMarkerSnapshot("settlement:town_B", 10d, 15d);

        var shouldRefresh = GraceLordMarkerRefreshPolicy.ShouldRefresh(previous, current, 5d);

        Assert.True(shouldRefresh);
    }

    [Fact]
    public void ShouldRefresh_WhenMovementReachesThreshold()
    {
        var previous = new GraceLordMarkerSnapshot("party:lord", 10d, 15d);
        var current = new GraceLordMarkerSnapshot("party:lord", 13d, 19d);

        var shouldRefresh = GraceLordMarkerRefreshPolicy.ShouldRefresh(previous, current, 5d);

        Assert.True(shouldRefresh);
    }
}
