using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Agency
{
    public sealed class ServiceStanceManager : CampaignBehaviorBase
    {
        public const string DefaultStanceId = "routine_service";

        private readonly List<ServiceStanceDefinition> _stances = new List<ServiceStanceDefinition>();

        public static ServiceStanceManager Instance { get; private set; }

        public string CurrentStanceId { get; private set; } = DefaultStanceId;

        public IReadOnlyList<ServiceStanceDefinition> Stances => _stances;

        public ServiceStanceManager()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => LoadDefinitions());
        }

        public override void SyncData(IDataStore dataStore)
        {
            var current = CurrentStanceId;
            _ = dataStore.SyncData("en_service_stance_current", ref current);
            CurrentStanceId = string.IsNullOrWhiteSpace(current) ? DefaultStanceId : current;
        }

        public ServiceStanceDefinition Current => GetDefinition(CurrentStanceId) ?? GetDefinition(DefaultStanceId) ?? DefaultDefinitions()[0];

        public bool SetCurrentStance(string stanceId)
        {
            if (string.IsNullOrWhiteSpace(stanceId))
            {
                return false;
            }

            LoadDefinitionsIfNeeded();
            var def = GetDefinition(stanceId);
            if (def == null)
            {
                ModLogger.Expected("AGENCY", "unknown_service_stance", "Unknown service stance selected", LogCtx.Of("stance", stanceId));
                return false;
            }

            CurrentStanceId = def.Id;
            ModLogger.Info("AGENCY", $"Service stance changed: {CurrentStanceId}");
            return true;
        }

        public ServiceStanceDefinition GetDefinition(string stanceId)
        {
            LoadDefinitionsIfNeeded();
            return _stances.FirstOrDefault(s => string.Equals(s.Id, stanceId, StringComparison.OrdinalIgnoreCase));
        }

        private void LoadDefinitionsIfNeeded()
        {
            if (_stances.Count == 0)
            {
                LoadDefinitions();
            }
        }

        private void LoadDefinitions()
        {
            _stances.Clear();

            try
            {
                var path = Path.Combine(ModulePaths.GetContentPath("ServiceStances"), "service_stances.json");
                if (File.Exists(path))
                {
                    var root = JObject.Parse(File.ReadAllText(path));
                    var stances = root["stances"] as JArray;
                    if (stances != null)
                    {
                        foreach (var item in stances.OfType<JObject>())
                        {
                            var def = ParseDefinition(item);
                            if (def != null)
                            {
                                _stances.Add(def);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("AGENCY", "Failed to load service stance definitions", ex);
            }

            if (_stances.Count == 0)
            {
                _stances.AddRange(DefaultDefinitions());
            }

            if (GetDefinition(CurrentStanceId) == null)
            {
                CurrentStanceId = DefaultStanceId;
            }
        }

        private static ServiceStanceDefinition ParseDefinition(JObject obj)
        {
            if (obj == null)
            {
                return null;
            }

            var id = (string)obj["id"] ?? string.Empty;
            var label = (string)obj["label"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            return new ServiceStanceDefinition
            {
                Id = id,
                Label = label,
                Summary = (string)obj["summary"] ?? label,
                Preview = (string)obj["preview"] ?? string.Empty,
                Tooltip = (string)obj["tooltip"] ?? string.Empty
            };
        }

        private static List<ServiceStanceDefinition> DefaultDefinitions()
        {
            return new List<ServiceStanceDefinition>
            {
                new ServiceStanceDefinition
                {
                    Id = DefaultStanceId,
                    Label = "Routine Service",
                    Summary = "Routine Service. Follow the column and keep your kit ready.",
                    Preview = "Default soldier routine. No special path bias.",
                    Tooltip = "Default posture. Wages and ordinary duty continue; orders override it."
                }
            };
        }
    }
}
