namespace Enlisted.Features.Interface.Models
{
    public enum DispatchDomain
    {
        Unknown = 0,
        Kingdom = 1,
        Personal = 2,
        Camp = 3
    }

    public enum DispatchSourceKind
    {
        Unknown = 0,
        ServiceStance = 1,
        Order = 2,
        ActivityOverride = 3,
        ModalIncident = 4,
        Routine = 5,
        Battle = 6,
        Muster = 7,
        Promotion = 8,
        Condition = 9,
        Flavor = 10
    }

    public enum DispatchSurfaceHint
    {
        Auto = 0,
        Dispatches = 1,
        Upcoming = 2,
        You = 3,
        SinceLastMuster = 4,
        CampActivities = 5,
        ModalOnly = 6
    }

    public readonly struct DispatchRoute
    {
        public static DispatchRoute DefaultPersonal =>
            new DispatchRoute(DispatchDomain.Personal, DispatchSourceKind.Unknown, DispatchSurfaceHint.Auto);

        public DispatchRoute(DispatchDomain domain, DispatchSourceKind sourceKind, DispatchSurfaceHint surfaceHint)
        {
            Domain = domain;
            SourceKind = sourceKind;
            SurfaceHint = surfaceHint;
        }

        public DispatchDomain Domain { get; }
        public DispatchSourceKind SourceKind { get; }
        public DispatchSurfaceHint SurfaceHint { get; }

        public bool IsKingdomDispatch => Domain == DispatchDomain.Kingdom &&
            (SurfaceHint == DispatchSurfaceHint.Auto || SurfaceHint == DispatchSurfaceHint.Dispatches);

        public bool IsPersonalForYou => Domain == DispatchDomain.Personal &&
            (SurfaceHint == DispatchSurfaceHint.Auto || SurfaceHint == DispatchSurfaceHint.You);

        public bool IsCampActivity => Domain == DispatchDomain.Camp ||
            SourceKind == DispatchSourceKind.ActivityOverride ||
            SurfaceHint == DispatchSurfaceHint.CampActivities;

        public bool CountsForMusterRecap => Domain == DispatchDomain.Personal &&
            (SourceKind == DispatchSourceKind.ServiceStance ||
             SourceKind == DispatchSourceKind.Order ||
             SourceKind == DispatchSourceKind.Battle ||
             SourceKind == DispatchSourceKind.Condition ||
             SourceKind == DispatchSourceKind.Promotion ||
             SourceKind == DispatchSourceKind.Muster ||
             SourceKind == DispatchSourceKind.ModalIncident);
    }
}
