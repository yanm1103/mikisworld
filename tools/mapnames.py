"""Builds a harness names file (see tools/harness/names_mw2.properties) for another build of the same game.
usage: python mapnames.py <dirA> <classA> <dirB> <classB> <midletFieldA> [logicalNamesFile]
Maps the field / method names that Trace.java reads from build A (logical names) to the names in build B,
aligning fields by declaration order + type (fieldmap.py) and methods by the shape of their bytecode (classmatch.py)."""
import difflib, re, subprocess, sys
sys.path.insert(0, __import__("os").path.dirname(__file__))
import classmatch as cm
from pathlib import Path

def fields(d, c):
    out = subprocess.run(["javap", "-p", "-cp", d, c], capture_output=True, text=True).stdout
    res = []
    for line in out.splitlines():
        m = re.match(r"^\s+((?:[a-z]+ )*)([\w.\[\]$]+) (\w+);$", line)
        if m and "(" not in line:
            res.append((" ".join(x for x in m.group(1).split() if x in ("static", "final")) + "|" + m.group(2), m.group(3)))
    return res

def fmap(da, ca, db, cb):
    A, B = fields(da, ca), fields(db, cb)
    sm = difflib.SequenceMatcher(None, [a[0] for a in A], [b[0] for b in B], autojunk=False)
    m = {}
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            for k in range(i2 - i1): m[A[i1 + k][1]] = B[j1 + k][1]
    return m

def mmap(da, ca, db, cb):
    A, B = cm.methods(Path(da), ca), cm.methods(Path(db), cb)
    res = {}
    for sa, oa in A.items():
        name = re.search(r"(\w+)\(", sa).group(1)
        args = re.search(r"\((.*)\)", sa).group(1)
        cands = [(cm.ratio(oa, ob), sb) for sb, ob in B.items() if re.search(r"\((.*)\)", sb).group(1) == args]
        if cands:
            r, sb = max(cands)
            res.setdefault((name, args), []).append((r, re.search(r"(\w+)\(", sb).group(1)))
    return res

if __name__ == "__main__":
    da, ca, db, cb = sys.argv[1:5]
    fm = fmap(da, ca, db, cb)
    for k, v in sorted(fm.items()): print(f"F {k}={v}")
    for k, v in sorted(mmap(da, ca, db, cb).items()):
        if k[1] in ("", "int, int", "int", "javax.microedition.lcdui.Graphics"): print("M", k, v)
