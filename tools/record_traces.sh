#!/bin/sh
# Records golden traces by running the ORIGINAL JAR bytecode headless (tools/harness).
# Needs: extracted/MikisWorld (jar xf), JDK 11+, python.  Output: traces/<scenario>_<seed>.{in,trace}
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/tools" || exit 1
mkdir -p harness/out "$ROOT/traces"
javac -nowarn -d harness/out $(find harness/stubs -name '*.java') harness/src/Trace.java || exit 1
CP="harness/out;../extracted/MikisWorld"
LEVEL="../extracted/MikisWorld/level.bin"
rec() { # scenario seed ticks startArea
  python gen_inputs.py "$LEVEL" "$1" "$2" "$3" > ../traces/$1_$2.in
  mode=main; [ "$1" = bonus ] && mode=bonus
  java -cp "$CP" Trace $mode "$2" "$3" ../traces/$1_$2.in $4 > ../traces/$1_$2.trace 2>&1
}
for s in 1 2 3 4 5 6; do rec main $s 8000; done
for s in 1 2 3; do rec area1 $s 4000 1; rec area2 $s 4000 2; rec area3 $s 4000 3; done
for s in 1 2; do rec bonus $s 3000; done
ls "$ROOT/traces"/*.trace | wc -l
