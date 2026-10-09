#!/bin/sh
# Decodes every .ott with the independent Python reference decoder (tools/decode_ott.py) into traces/<name>.ott.json.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$ROOT/traces"
cd "$ROOT/tools" || exit 1
for f in ../extracted/MikisWorld/*.ott; do
  python -I decode_ott.py "$f" --json > "../traces/$(basename "$f").json" || exit 1
done
ls ../traces/*.ott.json | wc -l
# MikisWorld 2's tunes (traces/mw2_<name>.ott.json)
for f in ../extracted/MikisWorld2/*.ott; do
  python -I decode_ott.py "$f" --json > "../traces/mw2_$(basename "$f").json" || exit 1
done
ls ../traces/mw2_*.ott.json | wc -l
