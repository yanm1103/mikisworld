"""Matches classes of two obfuscated jars by bytecode shape (opcode sequences), ignoring names and constants.

usage: python classmatch.py <dirA> <dirB>      (directories with extracted .class files)
Prints, for every class of A, the most similar class of B, plus how many methods of the pair match closely.
"""
import difflib
import re
import subprocess
import sys
from pathlib import Path

OP = re.compile(r"^\s+\d+:\s+([a-z_0-9]+)")


def methods(cls_dir, name):
    out = subprocess.run(["javap", "-c", "-p", "-cp", str(cls_dir), name], capture_output=True, text=True).stdout
    ms, cur, sig = {}, None, None
    for line in out.splitlines():
        if line.startswith("  ") and not line.startswith("   ") and line.rstrip().endswith(";") and "(" in line:
            sig = line.strip()
            cur = []
            ms[sig] = cur
        elif cur is not None:
            m = OP.match(line)
            if m:
                cur.append(m.group(1))
    return {k: v for k, v in ms.items() if v}


def ratio(a, b):
    if not a or not b:
        return 0.0
    return difflib.SequenceMatcher(None, a, b, autojunk=False).ratio()


def class_names(d):
    return sorted(p.stem for p in Path(d).glob("*.class"))


def main():
    a_dir, b_dir = Path(sys.argv[1]), Path(sys.argv[2])
    A = {n: methods(a_dir, n) for n in class_names(a_dir)}
    B = {n: methods(b_dir, n) for n in class_names(b_dir)}
    flat = lambda ms: [op for v in ms.values() for op in v]
    print("%-12s %-12s %6s  %s" % ("class(A)", "best in B", "sim", "methods matching >=90% (A has / B has)"))
    for an, am in A.items():
        best, bsim = None, -1
        fa = flat(am)
        for bn, bm in B.items():
            s = ratio(fa, flat(bm))
            if s > bsim:
                best, bsim = bn, s
        close = 0
        for sig, ops in am.items():
            if any(ratio(ops, o2) >= 0.9 for o2 in B[best].values()):
                close += 1
        print("%-12s %-12s %5.0f%%  %d of %d (B has %d)" % (an, best, bsim * 100, close, len(am), len(B[best])))


if __name__ == "__main__" and not (len(sys.argv) > 1 and sys.argv[1] == "--pair"):
    main()


def pair_report(a_dir, a_name, b_dir, b_name):
    """For each method of B, its best-matching method of A (by opcode sequence), lowest similarity first."""
    A = methods(Path(a_dir), a_name)
    B = methods(Path(b_dir), b_name)
    rows = []
    for sig, ops in B.items():
        best, bs = None, -1
        for sa, oa in A.items():
            s = ratio(ops, oa)
            if s > bs:
                best, bs = sa, s
        rows.append((bs, sig, len(ops), best))
    for bs, sig, n, best in sorted(rows):
        print("%4.0f%%  %4d ops  B: %-60s  A: %s" % (bs * 100, n, sig[:60], best[:50] if best else "-"))
    used = {r[3] for r in rows}
    print("methods of A with no counterpart:", [s[:50] for s in A if s not in used])


if len(sys.argv) > 1 and sys.argv[1] == "--pair":
    pair_report(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5])
