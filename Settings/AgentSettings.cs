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

        // How often CriticalEventCollector.Collect() runs — deliberately
        // decoupled from OperationalCadenceSeconds. Fast-moving dangers
        // (oxygen depletion above all: a duplicant can go from fine to
        // suffocating within one operational tick) need a much tighter
        // polling loop than the colony/duplicant snapshot does, and since
        // CriticalEventPushClient already pushes each new event immediately
        // on detection (no cadence of its own), this setting is the actual
        // end-to-end reaction-time knob for the critical tier.
        public int CriticalCadenceSeconds = 2;

        // How often the cached snapshot is POSTed to Ledgyx. Kept separate
        // from OperationalCadenceSeconds (which can go down to a 5s minimum
        // for local /api/snapshot/* polling) because Ledgyx's ingestion
        // channel fires one AI_AGENT run per pushed row and is quota-metered,
        // not just rate-limited — pushing every 5s would burn quota fast.
        public int PushCadenceSeconds = 60;

        // Task #8 (SSE client, not yet built). Separate endpoint/credential
        // from LedgyxEndpoint/ApiKey because SSE auth is token-in-query-string
        // (confirmed against the working Obsidian sse-notes-receiver plugin —
        // see oni-obsidian-sse-plugin-reference), not the X-API-Key header
        // used by the push side. Temporary per that same decision: Ledgyx SSE
        // auth is expected to move to a header later, matching ApiKey.
        public string SseEndpoint = "";
        public string SseToken = "";

        // Task #9's critical/event tier (CriticalEventCollector) needs its
        // own Ledgyx table/endpoint, separate from LedgyxEndpoint — per the
        // 3-tier design each tier gets its own table so cadence/trigger
        // wiring can differ (see oni-colony-snapshot-3tier-cadence-design).
        // Reuses the same ApiKey/X-API-Key auth as the operational push,
        // since both are plain POST ingestion routes on the mod's own
        // outbound side (unlike SSE's query-string auth). Consumed by
        // CriticalEventPushClient (event-driven queue+worker, not a cron
        // timer like LedgyxPushClient).
        public string CriticalEventsEndpoint = "";

        // Task #10's environmental tier (EnvironmentalSnapshotCollector) is
        // periodic sampling, not change-detection — a much slower default
        // than the other two cadences, since tile temp/mass/element only
        // needs coarse tracking over time, not near-real-time reaction.
        // Minimum enforced in SnapshotTicker mirrors the other cadence
        // knobs' pattern.
        public int EnvironmentalCadenceSeconds = 900;

        // Sector chunk edge length in cells for the environmental tier — see
        // EnvironmentalSnapshotCollector.DefaultSectorSizeCells for the
        // reasoning behind the default. Push endpoint deferred to a future
        // task, same as SSE (#8); this tier is local-poll-only for now.
        public int EnvironmentalSectorSizeCells = Snapshot.EnvironmentalSnapshotCollector.DefaultSectorSizeCells;
    }
}
