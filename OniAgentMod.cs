using HarmonyLib;
using KMod;
using OniAgent.Networking;
using OniAgent.Settings;
using OniAgent.Snapshot;
using UnityEngine;

namespace OniAgent
{
    public class OniAgentMod : UserMod2
    {
        private static ApiServer apiServer;
        private static LedgyxPushClient pushClient;
        private static CriticalEventPushClient criticalEventPushClient;
        private static EnvironmentalPushClient environmentalPushClient;
        private static LedgyxSseClient sseClient;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            Debug.Log("[OniAgent] OnLoad: applying Harmony patches.");
            harmony.PatchAll();

            var settings = SettingsManager.Load();

            criticalEventPushClient = new CriticalEventPushClient(settings);
            criticalEventPushClient.Start();

            var runner = new GameObject("OniAgentRunner");
            Object.DontDestroyOnLoad(runner);
            var ticker = runner.AddComponent<SnapshotTicker>();
            ticker.Configure(
                settings.OperationalCadenceSeconds,
                settings.CriticalCadenceSeconds,
                settings.EnvironmentalCadenceSeconds,
                settings.EnvironmentalSectorSizeCells,
                criticalEventPushClient);

            apiServer = new ApiServer(settings);
            apiServer.Start();

            pushClient = new LedgyxPushClient(settings);
            pushClient.Start();

            environmentalPushClient = new EnvironmentalPushClient(settings);
            environmentalPushClient.Start();

            sseClient = new LedgyxSseClient(settings);
            sseClient.Start();

            Debug.Log("[OniAgent] OnLoad: mod loaded successfully.");
        }
    }
}
