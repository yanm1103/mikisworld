#!/bin/sh
# Dumps reference frames (128x128 RGB, text excluded) from the ORIGINAL MikisWorld2.jar for a sample of ticks of each scenario.
# Output: traces/frames/mw2_<scenario>_<seed>/frame_<tick>.{rgb,png}.  Needs traces/mw2_*.in (record_traces2.sh).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/tools" || exit 1
javac -nowarn -d harness/out $(find harness/stubs -name "*.java") harness/src/Trace.java || exit 1
CP="harness/out;../extracted/MikisWorld2"
STEP=${STEP:-100}
# regular sampling + the first ticks after every state change (short states such as wipes and game over are covered)
frames() { # step max name seed
  { seq 0 "$1" "$2"
    awk '$1!~/^#/ && $1!="tick" { if ($2 != prev) { for (k = 0; k < 4; k++) print $1 + k * 7 } prev = $2 }' ../traces/mw2_$3_$4.trace
  } | awk '$1 >= 0 && $1 <= '"$2"'' | sort -n | uniq | tr '\n' ',' | sed 's/,$//'
}
rec() { # name seed ticks step startArea keeplives mode
  d=../traces/frames/mw2_$1_$2; rm -rf "$d"; mkdir -p "$d"
  java -Dscreen.w=128 -Dscreen.h=128 -Dnames=harness/names_mw2.properties -Dkeeplives=$6 \
       -Dframes=$(frames $4 $(( $3 - 1 )) $1 $2) -DframeDir=$d -cp "$CP" Trace $7 "$2" "$3" ../traces/mw2_$1_$2.in $5 > /dev/null 2>&1
  echo "mw2_$1_$2: $(ls $d/*.rgb | wc -l) frames"
}
for s in 1 2 3; do rec main $s 8000 $STEP "" 3 main; done
for s in 1 2; do rec die $s 6000 $STEP "" 0 main; done
rec area1 1 4000 $STEP 1 3 main
rec area2 1 4000 $STEP 2 3 main
rec area3 1 4000 $STEP 3 3 main
rec bonus 1 3000 50 "" 0 bonus
