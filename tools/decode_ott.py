"""Reference decoder for Nokia Smart Messaging ring tones (.ott), as used by MikisWorld.

Bit layout (MSB first), per the Nokia Smart Messaging spec as implemented by Gammu's GSM_DecodeNokiaRTTLRingtone:
  8  header 0x02
  7  command 0x25 (ringing tone programming) + 1 pad bit (byte align)
  7  command 0x1D (sound)
  3  song type (1 = basic)
  4  title length, then 8 bits per character
  8  pattern count (1)
  3  pattern header id, 2 pattern id, 4 loop value
  8  instruction count
  per instruction: 3 bit id
     1 note:   4 note, 3 duration, 2 duration specifier
     2 scale:  2
     3 style:  2
     4 tempo:  5
     5 volume: 4

usage: python decode_ott.py file.ott [--json]
"""
import json
import sys

BPM = [25, 28, 31, 35, 40, 45, 50, 56, 63, 70, 80, 90, 100, 112, 125, 140,
       160, 180, 200, 225, 250, 285, 320, 355, 400, 450, 500, 565, 635, 715, 800, 900]
NOTE_NAMES = ["-", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]


class Bits:
    def __init__(self, data):
        self.d, self.p = data, 0

    def read(self, n):
        v = 0
        for _ in range(n):
            byte = self.d[self.p >> 3]
            v = (v << 1) | ((byte >> (7 - (self.p & 7))) & 1)
            self.p += 1
        return v

    def align(self):
        self.p = (self.p + 7) & ~7


def decode(data):
    b = Bits(data)
    assert b.read(8) == 0x02, "bad header"
    assert b.read(7) == 0x25, "not ringing tone programming"
    b.align()
    assert b.read(7) == 0x1D, "not a sound command"
    assert b.read(3) == 1, "not a basic song"
    name = "".join(chr(b.read(8)) for _ in range(b.read(4)))
    patterns = []
    scale, style, tempo = 1, 0, 63
    for _ in range(b.read(8)):
        pid, pat, loop = b.read(3), b.read(2), b.read(4)
        assert pid == 0, "pattern header expected"
        notes = []
        for _ in range(b.read(8)):
            ins = b.read(3)
            if ins == 1:
                n, dur, spec = b.read(4), b.read(3), b.read(2)
                notes.append({"note": n, "dur": dur, "spec": spec, "scale": scale, "style": style, "bpm": tempo})
            elif ins == 2:
                scale = b.read(2)
            elif ins == 3:
                style = b.read(2)
            elif ins == 4:
                tempo = BPM[b.read(5)]
            elif ins == 5:
                b.read(4)
            else:
                raise ValueError("unknown instruction %d at bit %d" % (ins, b.p))
        patterns.append({"id": pat, "loop": loop, "notes": notes})
    # a pattern with loop value n is played n + 1 times (inferred: the spec is not available offline)
    notes = [n for p in patterns for _ in range(p["loop"] + 1) for n in p["notes"]]
    return {"name": name, "patterns": [(p["id"], p["loop"], len(p["notes"])) for p in patterns], "notes": notes,
            "bits_used": b.p, "bits_total": len(data) * 8}


SPEC = [1.0, 1.5, 9.0 / 4.0, 2.0 / 3.0]


def events(t):
    """Expanded note list with absolute durations: (note 0-12, scale 0-3, style, milliseconds)."""
    out = []
    for n in t["notes"]:
        ms = 4 * 60000.0 / n["bpm"] / (1 << n["dur"]) * SPEC[n["spec"]]
        out.append([n["note"], n["scale"], n["style"], ms])
    return out


def describe(t):
    out = []
    for n in t["notes"]:
        out.append("%s%d/%d%s" % (NOTE_NAMES[n["note"]], n["scale"], 1 << n["dur"], ".:^"[0] if n["spec"] == 1 else ""))
    return " ".join(out)


if __name__ == "__main__":
    t = decode(open(sys.argv[1], "rb").read())
    if "--json" in sys.argv:
        print(json.dumps({"name": t["name"], "events": events(t)}))
    else:
        print("%s: name=%r patterns(id,loop,notes)=%s bits %d/%d bpm=%s" % (
            sys.argv[1], t["name"], t["patterns"], t["bits_used"], t["bits_total"],
            sorted({n["bpm"] for n in t["notes"]})))
        print("  ", describe(t))
