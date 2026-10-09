#!/bin/sh
# Renders the same ticks with Game2 and compares pixel-by-pixel with the frames from record_frames2.sh.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E " error " && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0; total=0
for d in "$ROOT"/traces/frames/mw2_*/; do
  b=$(basename "$d"); rest=${b#mw2_}; sc=${rest%_*}; seed=${rest#*_}
  mode=main; area=-; ticks=8000; kl=3
  case $sc in
    main) ;;
    die) ticks=6000; kl=0;;
    area1) area=1; ticks=4000;;
    area2) area=2; ticks=4000;;
    area3) area=3; ticks=4000;;
    bonus) mode=bonus; ticks=3000; kl=0;;
  esac
  list=$(ls "$d" | sed -n 's/frame_\([0-9]*\)\.rgb/\1/p' | sort -n | tr '\n' ',')
  out=$("$EXE" mw2 "$ROOT/extracted/MikisWorld2" $mode "$seed" $ticks "$ROOT/traces/$b.in" $area "$ROOT/traces/$b.trace" $kl "$d" "$list" 2>&1)
  total=$((total+1))
  echo "$out" | tail -3 | cut -c1-200 | sed "s/^/$b: /"
  case "$out" in *", 0 mismatching"*) ;; *) fail=$((fail+1));; esac
done
echo "== $((total-fail))/$total MikisWorld 2 frame sets identical"
[ $fail -eq 0 ]
