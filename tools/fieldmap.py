"""Aligns the fields of two obfuscated classes by declaration order + type (difflib), to translate names between games.
usage: python fieldmap.py <dirA> <classA> <dirB> <classB>
prints 'nameA -> nameB' for matched fields and lists the unmatched ones on each side."""
import difflib
import re
import subprocess
import sys

def fields(d, c):
    out = subprocess.run(["javap", "-p", "-cp", d, c], capture_output=True, text=True).stdout
    res = []
    for line in out.splitlines():
        m = re.match(r"^\s+((?:[a-z]+ )*)([\w.\[\]$]+) (\w+);$", line)
        if m and "(" not in line:
            mods = m.group(1).split()
            res.append((" ".join(m for m in mods if m in ("static", "final")), m.group(2), m.group(3)))
    return res

A, B = fields(sys.argv[1], sys.argv[2]), fields(sys.argv[3], sys.argv[4])
ka = [f[0] + "|" + f[1] for f in A]
kb = [f[0] + "|" + f[1] for f in B]
sm = difflib.SequenceMatcher(None, ka, kb, autojunk=False)
mapped, onlyA, onlyB = [], [], []
for tag, i1, i2, j1, j2 in sm.get_opcodes():
    if tag == "equal":
        mapped += [(A[i1 + k][2], B[j1 + k][2], A[i1 + k][1]) for k in range(i2 - i1)]
    else:
        onlyA += A[i1:i2]
        onlyB += B[j1:j2]
print("fields: A=%d B=%d matched=%d" % (len(A), len(B), len(mapped)))
print("only in A:", [(f[1], f[2]) for f in onlyA])
print("only in B:", [(f[1], f[2]) for f in onlyB])
if "--map" in sys.argv:
    for a, b, t in mapped:
        print("%s -> %s   (%s)" % (a, b, t))
