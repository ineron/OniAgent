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

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            Debug.Log("[OniAgent] OnLoad: applying Harmony patches.");
            harmony.PatchAll();

            var settings = SettingsManager.Load();

            var runner = new GameObject("OniAgentRunner");
            Object.DontDestroyOnLoad(runner);
            var ticker = runner.AddComponent<SnapshotTicker>();
            ticker.Configure(settings.OperationalCadenceSeconds);

            apiServer = new ApiServer(settings);
            apiServer.Start();

            pushClient = new LedgyxPushClient(settings);
            pushClient.Start();

            Debug.Log("[OniAgent] OnLoad: mod loaded successfully.");
        }
    }
}
