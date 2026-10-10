#!/bin/sh
# Records hall-of-fame reference tables from the ORIGINAL class k (tools/harness/src/HallTest.java).
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/tools/harness" || exit 1
mkdir -p out "$ROOT/traces"
javac -nowarn -d out $(find stubs -name '*.java') src/Trace.java src/HallTest.java || exit 1
for sd in 1 2 3 7 11; do
  java -cp "out;../../extracted/MikisWorld" HallTest $sd 400 > "$ROOT/traces/hall_$sd.txt"
done
# the same differential test against the class k of MikisWorld2.jar (98 % similar bytecode, one extra setter)
for sd in 1 2 3 7 11; do
  java -cp "out;../../extracted/MikisWorld2" HallTest $sd 400 > "$ROOT/traces/hall2_$sd.txt"
done
ls "$ROOT"/traces/hall_*.txt "$ROOT"/traces/hall2_*.txt | wc -l
