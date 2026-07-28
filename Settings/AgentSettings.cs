namespace OniAgent.Settings
{
    // Loaded once at mod startup from settings.json next to the mod DLL.
    // V1 is a plain JSON file, not Klei's in-game Mod Options screen — that
    // needs its own IOptionsMenu API research and is deferred (task #6 body).
    //
    // Only the operational tier's cadence lives here so far, since that's the
    // only tier collector that exists (task #3). The 3-tier design calls for
    // per-tier cadence/threshold tuning to fold into this file as the
    // critical (#9) and environmental (#10) collectors are built.
    public class AgentSettings
    {
        public string LedgyxEndpoint = "https://app.ledgyx.com/api/oni/snapshot";
        public string ApiKey = "";
        public int OperationalCadenceSeconds = 60;

        // How often the cached snapshot is POSTed to Ledgyx. Kept separate
        // from OperationalCadenceSeconds (which can go down to a 5s minimum
        // for local /api/snapshot/* polling) because Ledgyx's ingestion
        // channel fires one AI_AGENT run per pushed row and is quota-metered,
        // not just rate-limited — pushing every 5s would burn quota fast.
        public int PushCadenceSeconds = 60;
    }
}
