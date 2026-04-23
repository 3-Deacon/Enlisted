using Enlisted.Features.Interface.Models;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class DispatchRoutingTests
{
    [Fact]
    public void DefaultsRouteUntypedItemsToPersonalFeed()
    {
        var route = DispatchRoute.DefaultPersonal;

        Assert.Equal(DispatchDomain.Personal, route.Domain);
        Assert.Equal(DispatchSourceKind.Unknown, route.SourceKind);
        Assert.Equal(DispatchSurfaceHint.Auto, route.SurfaceHint);
    }

    [Fact]
    public void KingdomDispatchesOnlyUseDispatchesSurface()
    {
        var route = new DispatchRoute(
            DispatchDomain.Kingdom,
            DispatchSourceKind.Flavor,
            DispatchSurfaceHint.Dispatches);

        Assert.True(route.IsKingdomDispatch);
        Assert.False(route.IsPersonalForYou);
        Assert.False(route.IsCampActivity);
    }

    [Fact]
    public void ServiceStanceDefaultsToYouAndMusterSurfaces()
    {
        var route = new DispatchRoute(
            DispatchDomain.Personal,
            DispatchSourceKind.ServiceStance,
            DispatchSurfaceHint.You);

        Assert.True(route.IsPersonalForYou);
        Assert.True(route.CountsForMusterRecap);
        Assert.False(route.IsCampActivity);
    }

    [Fact]
    public void ActivityOverrideCanStayCampScoped()
    {
        var route = new DispatchRoute(
            DispatchDomain.Camp,
            DispatchSourceKind.ActivityOverride,
            DispatchSurfaceHint.CampActivities);

        Assert.False(route.IsPersonalForYou);
        Assert.True(route.IsCampActivity);
        Assert.False(route.IsKingdomDispatch);
    }
}
