#!/bin/sh
# Runs every validation: state traces, rendered frames, hall of fame, application flow.
# Needs the recorded references (tools/record_traces.sh, tools/record_frames.sh, tools/record_hall.sh).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E " error " && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0
step() { out=$("$@" 2>&1); rc=$?; echo "$out" | tail -1; [ $rc -eq 0 ] || fail=1; }

echo "== state traces";  step "$ROOT/tools/run_traces.sh"
echo "== MikisWorld 2 state traces"; step "$ROOT/tools/run_traces2.sh"
echo "== rendered frames"; step "$ROOT/tools/run_frames.sh"

echo "== MikisWorld 2 rendered frames"; step "$ROOT/tools/run_frames2.sh"

echo "== hall of fame"
for f in "$ROOT"/traces/hall_*.txt; do
  sd=$(basename "$f" .txt); sd=${sd#hall_}
  "$EXE" hall "$sd" 400 "$f" || fail=1
done
echo "== hall of fame (class k of MikisWorld2.jar)"
for f in "$ROOT"/traces/hall2_*.txt; do
  sd=$(basename "$f" .txt); sd=${sd#hall2_}
  "$EXE" hall "$sd" 400 "$f" mw2 || fail=1
done

echo "== ring tone decoder"
step "$EXE" ott "$ROOT/extracted/MikisWorld" "$ROOT/traces"
step "$EXE" ott "$ROOT/extracted/MikisWorld2" "$ROOT/traces" mw2_

echo "== cutscene bubbles do not leak between cutscenes"
step "$EXE" cutscene "$ROOT/extracted/MikisWorld" "$ROOT/extracted/MikisWorld2"

echo "== state traces of the other screen-size builds (game 1 at 128x128, game 2 at 176x144)"
if ls "$ROOT"/traces/g1s_*.trace >/dev/null 2>&1; then step "$ROOT/tools/run_traces_variants.sh"; else echo "(skipped: run tools/record_traces_variants.sh)"; fi

echo "== other screen-size builds (game 1 at 128x128, game 2 at 176x144): load, play, bonus, extended view"
if [ -d "$ROOT/extracted/MikisWorld_128x128" ]; then
  step "$EXE" variant "$ROOT/extracted/MikisWorld_128x128" 1 "$ROOT/traces/_shots/v1_128"
  step "$EXE" variant "$ROOT/extracted/MikisWorld2_176x144" 2 "$ROOT/traces/_shots/v2_176"
else echo "(skipped: extract the other builds into extracted/MikisWorld_128x128 and extracted/MikisWorld2_176x144)"; fi

echo "== application flow"
step "$EXE" apptest "$ROOT/extracted/MikisWorld" "$ROOT/extracted/MikisWorld2"

echo "== engine tweaks (slow jump/animation, revamped movement)"
step "$EXE" tweaks "$ROOT/extracted/MikisWorld" "$ROOT/extracted/MikisWorld2"

echo "== portal key spam (double transition / crash)"
step "$EXE" portalspam "$ROOT/extracted/MikisWorld" "$ROOT/extracted/MikisWorld2"

echo "== mods (overlay, order, save code, checks, reload)"
step "$EXE" mods "$ROOT/extracted/MikisWorld" "$ROOT/extracted/MikisWorld2"

echo "== zoom glide"
step "$EXE" zoom

echo "== input router (keyboard/gamepad -> keypad events)"
step "$EXE" router

[ $fail -eq 0 ] && echo "ALL VALIDATIONS PASSED" || echo "SOME VALIDATIONS FAILED"
exit $fail
