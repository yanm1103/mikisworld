#!/bin/sh
# Runs every validation: state traces, rendered frames, hall of fame, application flow.
# Needs the recorded references (tools/record_traces.sh, tools/record_frames.sh, tools/record_hall.sh).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E " error " && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0

echo "== state traces";  "$ROOT/tools/run_traces.sh" | tail -1 || fail=1
echo "== rendered frames"; "$ROOT/tools/run_frames.sh" | tail -1 || fail=1

echo "== hall of fame"
for f in "$ROOT"/traces/hall_*.txt; do
  sd=$(basename "$f" .txt); sd=${sd#hall_}
  "$EXE" hall "$sd" 400 "$f" || fail=1
done

echo "== ring tone decoder"
"$EXE" ott "$ROOT/extracted/MikisWorld" "$ROOT/traces" | tail -1 || fail=1

echo "== application flow"
"$EXE" apptest "$ROOT/extracted/MikisWorld" | tail -1 || fail=1

[ $fail -eq 0 ] && echo "ALL VALIDATIONS PASSED" || echo "SOME VALIDATIONS FAILED"
exit $fail
