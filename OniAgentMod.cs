using HarmonyLib;
using KMod;
using UnityEngine;

namespace OniAgent
{
    public class OniAgentMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            Debug.Log("[OniAgent] OnLoad: applying Harmony patches.");
            harmony.PatchAll();
            Debug.Log("[OniAgent] OnLoad: mod loaded successfully.");
        }
    }
}
