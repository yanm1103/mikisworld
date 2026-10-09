#!/bin/sh
# Records golden traces of the ORIGINAL MikisWorld2.jar (headless). Output: traces/mw2_<name>_<seed>.{in,trace}
# Needs extracted/MikisWorld2 and the Nokia shims in tools/harness/out/com (copied from the first jar).
#
# Mork kills a randomly-playing character within ~1500 ticks, so most scenarios run with -Dkeeplives=3 (the harness tops the
# lives up before each tick; the C# test runner does the same). The "die" scenarios run without it to cover game over.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/tools" || exit 1
mkdir -p harness/out "$ROOT/traces"
javac -nowarn -d harness/out $(find harness/stubs -name '*.java') harness/src/Trace.java || exit 1
[ -d harness/out/com ] || cp -r ../extracted/MikisWorld/com harness/out/
CP="harness/out;../extracted/MikisWorld2"
LEVEL="../extracted/MikisWorld2/level.bin"
rec() { # name scenario seed ticks startArea keeplives
  python gen_inputs.py "$LEVEL" "$2" "$3" "$4" --v2 > ../traces/mw2_$1_$3.in
  mode=main; [ "$2" = bonus ] && mode=bonus
  java -Dscreen.w=128 -Dscreen.h=128 -Dnames=harness/names_mw2.properties -Dkeeplives=$6 -cp "$CP" Trace $mode "$3" "$4" ../traces/mw2_$1_$3.in $5 > ../traces/mw2_$1_$3.trace 2>&1
}
for s in 1 2 3 4 5 6; do rec main main $s 8000 "" 3; done
for s in 1 2 3; do rec die main $s 6000 "" 0; done
for s in 1 2 3; do rec area1 area1 $s 4000 1 3; rec area2 area2 $s 4000 2 3; rec area3 area3 $s 4000 3 3; done
for s in 1 2; do rec bonus bonus $s 3000 "" 0; done
ls ../traces/mw2_*.trace | wc -l
