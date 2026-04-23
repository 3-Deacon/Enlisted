using Enlisted.Features.Content;
using Enlisted.Features.Interface.Models;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class StoryletAgencyTests
{
    [Theory]
    [InlineData("stance_drift", DispatchDomain.Personal, DispatchSourceKind.ServiceStance, DispatchSurfaceHint.You)]
    [InlineData("stance_interrupt", DispatchDomain.Personal, DispatchSourceKind.ServiceStance, DispatchSurfaceHint.You)]
    [InlineData("order_accept", DispatchDomain.Personal, DispatchSourceKind.Order, DispatchSurfaceHint.Upcoming)]
    [InlineData("order_phase", DispatchDomain.Personal, DispatchSourceKind.Order, DispatchSurfaceHint.You)]
    [InlineData("order_outcome", DispatchDomain.Personal, DispatchSourceKind.Order, DispatchSurfaceHint.SinceLastMuster)]
    [InlineData("activity_override", DispatchDomain.Camp, DispatchSourceKind.ActivityOverride, DispatchSurfaceHint.CampActivities)]
    [InlineData("modal_incident", DispatchDomain.Personal, DispatchSourceKind.ModalIncident, DispatchSurfaceHint.ModalOnly)]
    [InlineData("news_flavor", DispatchDomain.Personal, DispatchSourceKind.Flavor, DispatchSurfaceHint.Auto)]
    [InlineData("realm_dispatch", DispatchDomain.Kingdom, DispatchSourceKind.Flavor, DispatchSurfaceHint.Dispatches)]
    [InlineData("unknown_role", DispatchDomain.Personal, DispatchSourceKind.Unknown, DispatchSurfaceHint.Auto)]
    public void DefaultForRoleMapsToExpectedRoute(
        string role,
        DispatchDomain domain,
        DispatchSourceKind sourceKind,
        DispatchSurfaceHint surfaceHint)
    {
        var agency = StoryletAgency.DefaultForRole(role);

        Assert.Equal(domain, agency.ToDomain());
        Assert.Equal(sourceKind, agency.ToSourceKind());
        Assert.Equal(surfaceHint, agency.ToSurfaceHint());
    }
}
