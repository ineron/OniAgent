using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace OniAgent.Settings
{
    public static class SettingsManager
    {
        private const string FileName = "settings.json";
        private const int MinCadenceSeconds = 5;
        private const int MinPushCadenceSeconds = 60;
        private const int MinCriticalCadenceSeconds = 1;
        private const int MinEnvironmentalCadenceSeconds = 60;
        private const int MinEnvironmentalSectorSizeCells = 1;
        private const int MinEnvironmentalPushCadenceSeconds = 60;

        public static AgentSettings Load()
        {
            var path = GetPath();
            AgentSettings settings;

            if (File.Exists(path))
            {
                try
                {
                    settings = JsonConvert.DeserializeObject<AgentSettings>(File.ReadAllText(path))
                        ?? new AgentSettings();
                }
                catch (Exception e)
                {
                    Debug.LogError("[OniAgent] Failed to parse settings.json, using defaults: " + e);
                    settings = new AgentSettings();
                }
            }
            else
            {
                settings = new AgentSettings();
                try
                {
                    File.WriteAllText(path, JsonConvert.SerializeObject(settings, Formatting.Indented));
                    Debug.Log("[OniAgent] No settings.json found — created default at " + path);
                }
                catch (Exception e)
                {
                    Debug.LogError("[OniAgent] Failed to write default settings.json at " + path + ": " + e);
                }
            }

            if (settings.OperationalCadenceSeconds < MinCadenceSeconds)
            {
                Debug.LogWarning("[OniAgent] OperationalCadenceSeconds " + settings.OperationalCadenceSeconds
                    + " below minimum, clamping to " + MinCadenceSeconds);
                settings.OperationalCadenceSeconds = MinCadenceSeconds;
            }

            if (settings.PushCadenceSeconds < MinPushCadenceSeconds)
            {
                Debug.LogWarning("[OniAgent] PushCadenceSeconds " + settings.PushCadenceSeconds
                    + " below minimum, clamping to " + MinPushCadenceSeconds);
                settings.PushCadenceSeconds = MinPushCadenceSeconds;
            }

            if (settings.CriticalCadenceSeconds < MinCriticalCadenceSeconds)
            {
                Debug.LogWarning("[OniAgent] CriticalCadenceSeconds " + settings.CriticalCadenceSeconds
                    + " below minimum, clamping to " + MinCriticalCadenceSeconds);
                settings.CriticalCadenceSeconds = MinCriticalCadenceSeconds;
            }

            if (settings.EnvironmentalCadenceSeconds < MinEnvironmentalCadenceSeconds)
            {
                Debug.LogWarning("[OniAgent] EnvironmentalCadenceSeconds " + settings.EnvironmentalCadenceSeconds
                    + " below minimum, clamping to " + MinEnvironmentalCadenceSeconds);
                settings.EnvironmentalCadenceSeconds = MinEnvironmentalCadenceSeconds;
            }

            if (settings.EnvironmentalSectorSizeCells < MinEnvironmentalSectorSizeCells)
            {
                Debug.LogWarning("[OniAgent] EnvironmentalSectorSizeCells " + settings.EnvironmentalSectorSizeCells
                    + " below minimum, clamping to " + MinEnvironmentalSectorSizeCells);
                settings.EnvironmentalSectorSizeCells = MinEnvironmentalSectorSizeCells;
            }

            if (settings.EnvironmentalPushCadenceSeconds < MinEnvironmentalPushCadenceSeconds)
            {
                Debug.LogWarning("[OniAgent] EnvironmentalPushCadenceSeconds " + settings.EnvironmentalPushCadenceSeconds
                    + " below minimum, clamping to " + MinEnvironmentalPushCadenceSeconds);
                settings.EnvironmentalPushCadenceSeconds = MinEnvironmentalPushCadenceSeconds;
            }

            Debug.Log("[OniAgent] Settings loaded: endpoint=" + settings.LedgyxEndpoint
                + ", apiKeySet=" + !string.IsNullOrEmpty(settings.ApiKey)
                + ", operationalCadenceSeconds=" + settings.OperationalCadenceSeconds
                + ", criticalCadenceSeconds=" + settings.CriticalCadenceSeconds
                + ", pushCadenceSeconds=" + settings.PushCadenceSeconds
                + ", environmentalCadenceSeconds=" + settings.EnvironmentalCadenceSeconds
                + ", environmentalSectorSizeCells=" + settings.EnvironmentalSectorSizeCells
                + ", environmentalEndpoint=" + settings.EnvironmentalEndpoint
                + ", environmentalPushCadenceSeconds=" + settings.EnvironmentalPushCadenceSeconds
                + ", sseEndpoint=" + settings.SseEndpoint
                + ", sseTokenSet=" + !string.IsNullOrEmpty(settings.SseToken)
                + ", criticalEventsEndpoint=" + settings.CriticalEventsEndpoint);

            return settings;
        }

        private static string GetPath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir, FileName);
        }
    }
}
