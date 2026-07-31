#!/usr/bin/env bash
# Manual test for the Stage 2 command pipeline (POST /api/command).
# Run this on the machine where ONI + the mod are actually running
# (the mod's HTTP server only listens on localhost:9813 inside the game).
#
# First floor test: dig a 5-cell-tall band extending left/right from the
# Duplicant Printing Pod, a ladder shaft straight down from the pod, and
# a wash basin + outhouse on the new floor. All coordinates are cell
# offsets relative to the Printing Pod (x right+, y up+) — adjust the
# numbers below to taste, this is a first test, not a final layout.
#
# NOTE: dig_rect queues a normal Dig chore (duplicants walk over and dig
# it like any other dig order — nothing instant). build places the
# building already complete (skips the ghost/haul-materials/construct
# pipeline) — this is a deliberate simplification for this first
# pipeline test, not the final behavior; fast-follow is wiring buildings
# through the normal construction ghost so dupes build them too.

set -euo pipefail

HOST="${ONI_AGENT_HOST:-http://localhost:9813}"

read -r -d '' PAYLOAD <<'JSON' || true
{
  "commands": [
    { "type": "dig_rect",  "x1": -15, "x2": 15, "y1": -5, "y2": -1 },
    { "type": "build", "building": "Ladder", "x": 0, "y": -1 },
    { "type": "build", "building": "Ladder", "x": 0, "y": -2 },
    { "type": "build", "building": "Ladder", "x": 0, "y": -3 },
    { "type": "build", "building": "Ladder", "x": 0, "y": -4 },
    { "type": "build", "building": "Ladder", "x": 0, "y": -5 },
    { "type": "build", "building": "WashBasin", "x": 5, "y": -5 },
    { "type": "build", "building": "Outhouse", "x": -8, "y": -5 }
  ]
}
JSON

echo "POST $HOST/api/command"
RESPONSE=$(curl -sS -X POST "$HOST/api/command" -H "Content-Type: application/json" -d "$PAYLOAD")
echo "$RESPONSE"

BATCH_ID=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['batch_id'])")
echo ""
echo "Batch $BATCH_ID queued. Waiting a couple of seconds for the game's main thread to pick it up..."
sleep 2

echo ""
echo "GET $HOST/api/command/result?batch_id=$BATCH_ID"
curl -sS "$HOST/api/command/result?batch_id=$BATCH_ID" | python3 -m json.tool
