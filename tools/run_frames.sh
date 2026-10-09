#!/bin/sh
# Renders the same ticks with the C# port and compares them pixel-by-pixel with the frames from record_frames.sh.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E " error " && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0; total=0
for d in "$ROOT"/traces/frames/*/; do
  b=$(basename "$d"); case $b in mw2_*) continue;; esac; sc=${b%_*}; seed=${b#*_}
  mode=main; [ "$sc" = bonus ] && mode=bonus
  case $sc in area1) area=1;; area2) area=2;; area3) area=3;; *) area=-;; esac
  case $sc in main) ticks=8000;; bonus) ticks=3000;; *) ticks=4000;; esac
  ticks_list=$(ls "$d" | sed -n 's/frame_\([0-9]*\)\.rgb/\1/p' | sort -n | tr '\n' ',')
  out=$("$EXE" "$ROOT/extracted/MikisWorld" $mode "$seed" $ticks "$ROOT/traces/$b.in" $area "$ROOT/traces/$b.trace" "$d" "$ticks_list" 2>&1)
  total=$((total+1))
  echo "$out" | tail -4 | cut -c1-200 | sed "s/^/$b: /"
  case "$out" in *", 0 mismatching"*) ;; *) fail=$((fail+1));; esac
done
echo "== $((total-fail))/$total frame sets identical"
[ $fail -eq 0 ]
