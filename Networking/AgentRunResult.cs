namespace OniAgent.Networking
{
    // What LedgyxSseClient receives for a completed agent run. Only Cycle
    // and Summary are contractual so far (confirmed 2026-07-30); the rest of
    // the "data" payload is open-ended and not yet consumed by anything, so
    // RawJson keeps the full payload around until CommandQueue (task #5)
    // defines what else it needs from it.
    //
    // ReceivedAt is fully-qualified System.DateTime, not a bare `using
    // System;` import — Assembly-CSharp defines its own global-namespace
    // `DateTime : KScreen` (a Klei UI screen type) that otherwise shadows it.
    public class AgentRunResult
    {
        public int Cycle;
        public string Summary;
        public string RawJson;
        public System.DateTime ReceivedAt;
    }
}
