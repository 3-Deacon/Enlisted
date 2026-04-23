using Enlisted.Features.Interface.Models;
using Newtonsoft.Json;

namespace Enlisted.Features.Content
{
    public sealed class StoryletAgency
    {
        [JsonProperty("role")]
        public string Role { get; set; } = string.Empty;

        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = string.Empty;

        [JsonProperty("surfaceHint")]
        public string SurfaceHint { get; set; } = string.Empty;

        public static StoryletAgency DefaultForRole(string role)
        {
            switch (Normalize(role))
            {
                case "stance_drift":
                case "stance_interrupt":
                    return Create(role, "personal", "service_stance", "you");
                case "order_accept":
                    return Create(role, "personal", "order", "upcoming");
                case "order_phase":
                    return Create(role, "personal", "order", "you");
                case "order_outcome":
                    return Create(role, "personal", "order", "since_last_muster");
                case "activity_override":
                    return Create(role, "camp", "activity_override", "camp_activities");
                case "modal_incident":
                    return Create(role, "personal", "modal_incident", "modal_only");
                case "news_flavor":
                    return Create(role, "personal", "flavor", "auto");
                case "realm_dispatch":
                    return Create(role, "kingdom", "flavor", "dispatches");
                default:
                    return Create(role, "personal", "unknown", "auto");
            }
        }

        public DispatchDomain ToDomain()
        {
            switch (NormalizeOrDefault(Domain, Role, a => a.Domain))
            {
                case "kingdom":
                    return DispatchDomain.Kingdom;
                case "camp":
                    return DispatchDomain.Camp;
                case "personal":
                default:
                    return DispatchDomain.Personal;
            }
        }

        public DispatchSourceKind ToSourceKind()
        {
            switch (NormalizeOrDefault(SourceKind, Role, a => a.SourceKind))
            {
                case "service_stance":
                    return DispatchSourceKind.ServiceStance;
                case "order":
                    return DispatchSourceKind.Order;
                case "activity_override":
                    return DispatchSourceKind.ActivityOverride;
                case "modal_incident":
                    return DispatchSourceKind.ModalIncident;
                case "routine":
                    return DispatchSourceKind.Routine;
                case "battle":
                    return DispatchSourceKind.Battle;
                case "muster":
                    return DispatchSourceKind.Muster;
                case "promotion":
                    return DispatchSourceKind.Promotion;
                case "condition":
                    return DispatchSourceKind.Condition;
                case "flavor":
                    return DispatchSourceKind.Flavor;
                case "unknown":
                default:
                    return DispatchSourceKind.Unknown;
            }
        }

        public DispatchSurfaceHint ToSurfaceHint()
        {
            switch (NormalizeOrDefault(SurfaceHint, Role, a => a.SurfaceHint))
            {
                case "dispatches":
                    return DispatchSurfaceHint.Dispatches;
                case "upcoming":
                    return DispatchSurfaceHint.Upcoming;
                case "you":
                    return DispatchSurfaceHint.You;
                case "since_last_muster":
                    return DispatchSurfaceHint.SinceLastMuster;
                case "camp_activities":
                    return DispatchSurfaceHint.CampActivities;
                case "modal_only":
                    return DispatchSurfaceHint.ModalOnly;
                case "auto":
                default:
                    return DispatchSurfaceHint.Auto;
            }
        }

        private static StoryletAgency Create(string role, string domain, string sourceKind, string surfaceHint)
        {
            return new StoryletAgency
            {
                Role = role ?? string.Empty,
                Domain = domain,
                SourceKind = sourceKind,
                SurfaceHint = surfaceHint
            };
        }

        private static string NormalizeOrDefault(
            string value,
            string role,
            System.Func<StoryletAgency, string> selector)
        {
            var normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized)
                ? Normalize(selector(DefaultForRole(role)))
                : normalized;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
