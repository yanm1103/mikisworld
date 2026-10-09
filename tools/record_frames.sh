#!/bin/sh
# Dumps reference frames (176x144 RGB, text excluded) from the ORIGINAL JAR for a sample of ticks of each scenario.
# Output: traces/frames/<scenario>_<seed>/frame_<tick>.{rgb,png}
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/tools" || exit 1
CP="harness/out;../extracted/MikisWorld"
STEP_MAIN=${STEP_MAIN:-100}
# regular sampling + the first ticks after every state change (so short states such as wipes and game over are covered)
frames() { # step max scenario seed
  { seq 0 "$1" "$2"
    awk '$1!~/^#/ && $1!="tick" { if ($2 != prev) { for (k = 0; k < 4; k++) print $1 + k * 7 } prev = $2 }' ../traces/$3_$4.trace
  } | awk '$1 >= 0 && $1 <= '"$2"'' | sort -n | uniq | tr '
' ',' | sed 's/,$//'
}
rec() { # scenario seed ticks step startArea
  mode=main; [ "$1" = bonus ] && mode=bonus
  d=../traces/frames/$1_$2; rm -rf "$d"; mkdir -p "$d"
  java -Dframes=$(frames $4 $(( $3 - 1 )) $1 $2) -DframeDir=$d -cp "$CP" Trace $mode "$2" "$3" ../traces/$1_$2.in $5 > /dev/null 2>&1
  echo "$1_$2: $(ls $d/*.rgb | wc -l) frames"
}
rec main 1 8000 $STEP_MAIN
rec main 2 8000 $STEP_MAIN
rec main 4 8000 $STEP_MAIN
rec main 5 8000 $STEP_MAIN
rec area1 1 4000 $STEP_MAIN 1
rec area1 2 4000 $STEP_MAIN 1
rec area2 1 4000 $STEP_MAIN 2
rec area3 1 4000 $STEP_MAIN 3
rec bonus 1 3000 50
rec bonus 2 3000 50
