#!/bin/sh
# Replays every MikisWorld 2 trace (traces/mw2_*.trace, see record_traces2.sh) on the C# port (Game2).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E " error " && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0; total=0
for f in "$ROOT"/traces/mw2_*.trace; do
  b=$(basename "$f" .trace); rest=${b#mw2_}; sc=${rest%_*}; seed=${rest#*_}
  mode=main; area=-; ticks=8000; kl=3
  case $sc in
    main) ;;
    die) ticks=6000; kl=0;;
    area1) area=1; ticks=4000;;
    area2) area=2; ticks=4000;;
    area3) area=3; ticks=4000;;
    bonus) mode=bonus; ticks=3000; kl=0;;
  esac
  out=$("$EXE" mw2 "$ROOT/extracted/MikisWorld2" $mode "$seed" $ticks "$ROOT/traces/$b.in" $area "$f" $kl 2>&1 | head -4)
  total=$((total+1))
  case "$out" in OK*) printf "%-14s %s\n" "$b" "$out";; *) fail=$((fail+1)); printf "%-14s FAIL\n%s\n" "$b" "$out" | cut -c1-330;; esac
done
echo "== $((total-fail))/$total MikisWorld 2 traces identical"
[ $fail -eq 0 ]
