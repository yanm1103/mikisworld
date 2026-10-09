#!/bin/sh
# Replays every recorded trace on the C# port and reports OK/MISMATCH per trace.
# (Traces are recorded from the original JAR by tools/harness; see tools/gen_inputs.py.)
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DN="$ROOT/tools/dotnet/dotnet.exe"
export DOTNET_CLI_HOME="$ROOT/tools/.dotnet-home" DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DN" build "$ROOT/port/MikisWorld.TraceTest" -v q --nologo 2>&1 | grep -E "error" && exit 2
EXE="$ROOT/port/MikisWorld.TraceTest/bin/Debug/net8.0/MikisWorld.TraceTest.exe"
fail=0; total=0
for f in "$ROOT"/traces/*.trace; do
  b=$(basename "$f" .trace); sc=${b%_*}; seed=${b#*_}
  mode=main; [ "$sc" = bonus ] && mode=bonus
  case $sc in area1) area=1;; area2) area=2;; area3) area=3;; *) area=-;; esac
  case $sc in main) ticks=8000;; bonus) ticks=3000;; *) ticks=4000;; esac
  out=$("$EXE" "$ROOT/extracted/MikisWorld" $mode "$seed" $ticks "$ROOT/traces/$b.in" $area "$f" 2>&1 | head -4)
  total=$((total+1))
  case "$out" in OK*) printf "%-10s %s\n" "$b" "$out";; *) fail=$((fail+1)); printf "%-10s FAIL\n%s\n" "$b" "$out" | cut -c1-260;; esac
done
echo "== $((total-fail))/$total traces identical"
[ $fail -eq 0 ]
