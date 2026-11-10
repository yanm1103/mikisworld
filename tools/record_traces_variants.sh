#!/bin/sh
# Records golden traces of the OTHER screen-size builds, running their original bytecode headless:
#   game 1 at 128x128 (extracted/MikisWorld_128x128) -> traces/g1s_<scenario>_<seed>.{in,trace}
#   game 2 at 176x144 (extracted/MikisWorld2_176x144) -> traces/g2l_<scenario>_<seed>.{in,trace}
# The harness reads the obfuscated names from tools/harness/names_mw1_128.properties / names_mw2_176.properties (made with tools/mapnames.py).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/tools" || exit 1
mkdir -p harness/out "$ROOT/traces"
javac -nowarn -d harness/out $(find harness/stubs -name '*.java') harness/src/Trace.java || exit 1
[ -d harness/out/com ] || cp -r ../extracted/MikisWorld/com harness/out/
SZ=128
rec() { # prefix dir names extraGenArgs scenario seed ticks startArea keeplives
  python gen_inputs.py "../extracted/$2/level.bin" "$5" "$6" "$7" $4 > ../traces/$1_$5_$6.in
  mode=main; [ "$5" = bonus ] && mode=bonus
  java -Dscreen.w=$SZ -Dscreen.h=$([ $SZ = 176 ] && echo 144 || echo $SZ) -Dnames=harness/names_$3.properties -Dkeeplives=$9 -cp "harness/out;../extracted/$2" Trace $mode "$6" "$7" ../traces/$1_$5_$6.in $8 > ../traces/$1_$5_$6.trace 2>&1
}
for s in 1 2 3 4 5 6; do rec g1s MikisWorld_128x128 mw1_128 "--c2 --tile 12" main $s 8000 "" 0; done
for s in 1 2 3; do rec g1s MikisWorld_128x128 mw1_128 "--c2 --tile 12" area1 $s 4000 1 0; rec g1s MikisWorld_128x128 mw1_128 "--c2 --tile 12" area2 $s 4000 2 0; rec g1s MikisWorld_128x128 mw1_128 "--c2 --tile 12" area3 $s 4000 3 0; done
for s in 1 2; do rec g1s MikisWorld_128x128 mw1_128 "--c2 --tile 12" bonus $s 3000 "" 0; done
SZ=176
for s in 1 2 3 4 5 6; do rec g2l MikisWorld2_176x144 mw2_176 "--v2 --tile 14" main $s 8000 "" 3; done
for s in 1 2 3; do rec g2l MikisWorld2_176x144 mw2_176 "--v2 --tile 14" die $s 6000 "" 0; done
for s in 1 2 3; do rec g2l MikisWorld2_176x144 mw2_176 "--v2 --tile 14" area1 $s 4000 1 3; rec g2l MikisWorld2_176x144 mw2_176 "--v2 --tile 14" area2 $s 4000 2 3; rec g2l MikisWorld2_176x144 mw2_176 "--v2 --tile 14" area3 $s 4000 3 3; done
for s in 1 2; do rec g2l MikisWorld2_176x144 mw2_176 "--v2 --tile 14" bonus $s 3000 "" 0; done
ls ../traces/g1s_*.trace ../traces/g2l_*.trace | wc -l
