using HarmonyLib;
using KMod;
using OniAgent.Snapshot;
using UnityEngine;

namespace OniAgent
{
    public class OniAgentMod : UserMod2
    {
        private static ApiServer apiServer;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            Debug.Log("[OniAgent] OnLoad: applying Harmony patches.");
            harmony.PatchAll();

            var runner = new GameObject("OniAgentRunner");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<SnapshotTicker>();

            apiServer = new ApiServer();
            apiServer.Start();

            Debug.Log("[OniAgent] OnLoad: mod loaded successfully.");
        }
    }
}
