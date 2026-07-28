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

            Debug.Log("[OniAgent] Settings loaded: endpoint=" + settings.LedgyxEndpoint
                + ", apiKeySet=" + !string.IsNullOrEmpty(settings.ApiKey)
                + ", operationalCadenceSeconds=" + settings.OperationalCadenceSeconds
                + ", pushCadenceSeconds=" + settings.PushCadenceSeconds
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
