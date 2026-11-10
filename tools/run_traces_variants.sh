#!/bin/sh
# Replays the traces of the other screen-size builds (tools/record_traces_variants.sh) on the C# port.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E " error " && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0; total=0
for f in "$ROOT"/traces/g1s_*.trace "$ROOT"/traces/g2l_*.trace; do
  b=$(basename "$f" .trace); pre=${b%%_*}; rest=${b#*_}; sc=${rest%_*}; seed=${rest#*_}
  mode=main; area=-; ticks=8000; kl=3
  case $sc in
    main) ;; die) ticks=6000; kl=0;;
    area1) area=1; ticks=4000;; area2) area=2; ticks=4000;; area3) area=3; ticks=4000;;
    bonus) mode=bonus; ticks=3000; kl=0;;
  esac
  if [ $pre = g1s ]; then
    kl=0
    out=$("$EXE" "$ROOT/extracted/MikisWorld_128x128" $mode "$seed" $ticks "$ROOT/traces/$b.in" $area "$f" 2>&1 | head -4)
  else
    out=$("$EXE" mw2 "$ROOT/extracted/MikisWorld2_176x144" $mode "$seed" $ticks "$ROOT/traces/$b.in" $area "$f" $kl 2>&1 | head -4)
  fi
  total=$((total+1))
  case "$out" in OK*) printf "%-14s %s\n" "$b" "$out";; *) fail=$((fail+1)); printf "%-14s FAIL\n%s\n" "$b" "$out" | cut -c1-400;; esac
done
echo "== $((total-fail))/$total variant traces identical"
[ $fail -eq 0 ]
