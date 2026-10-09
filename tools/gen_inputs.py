"""Generate deterministic input scripts for tools/harness Trace (and the C# port's test runner).

usage: python gen_inputs.py <level.bin> <scenario: main|area1|area2|area3|bonus> <seed> <ticks> > script.txt

areaN scenarios are meant to be run with Trace's startArea = N (area1 = final stage, area2/3 = sub-areas).

Scenarios mix random key presses with teleports to "interesting" places taken from the real level
(coins, key, cages, doors, the Lord platforms) so the physics/collision/event code gets exercised
far beyond the starting area.
"""
import random
import sys
from parse_level import parse

TILE = 14          # bT/b6 reported by the original (see #consts line of a trace)
SCALE_NUM, SCALE_DEN = 28, 16   # cl/16, ck/16 applied to file coordinates


def platforms(lv):
    """Platforms are tile-13 runs, collected row-major (same order as d.if(byte[]))."""
    m = lv["map"]; w, h, t = m["width"], m["height"], m["tiles"]
    out = []
    for y in range(h):
        x = 0
        while x < w:
            if t[y * w + x] == 13:
                x0 = x
                while x < w and t[y * w + x] == 13:
                    x += 1
                out.append((x0, y, x - x0))
            else:
                x += 1
    return out


def main():
    lv = parse(open(sys.argv[1], "rb").read())
    scen, seed, ticks = sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
    rnd = random.Random(seed)
    m = lv["map"]; w, h, t = m["width"], m["height"], m["tiles"]
    P, AB = lv["area_main_bottom_row"], lv["area_sub_bottom_row"]
    lo, hi = {"main": (2, P - 2), "area1": (P, AB), "area2": (AB, lv["subarea_rows"][0]),
              "area3": (lv["subarea_rows"][0], lv["subarea_rows"][1])}.get(scen, (2, P - 2))
    spots = []
    for i, v in enumerate(t):
        if v in (6, 7, 15, 12, 16) and lo <= i // w < hi:
            spots.append((i % w, i // w))
    plats = [p for p in platforms(lv) if lo <= p[1] < hi]
    lord_ix = [lv["lord1_platform"], lv["lord2_platform"]]
    ev = []  # (tick, op, args)

    if scen == "bonus":
        ev.append((1, "S", [1]))
        keys = [50, 52, 54, 56, 53, 53, 53, 49, 51, 55, 57]
        tk = 8
        while tk < ticks:
            ev.append((tk, "P", [rnd.choice(keys)]))
            tk += rnd.randint(1, 12)
        # tail: release is irrelevant for the grid, but keep the script balanced
    else:
        if scen == "main":
            ev.append((rnd.randint(2, 150) if seed % 3 else ticks + 1, "P", [53]))   # skip (or watch) the intro cutscene
        tk = 4
        keys = [49, 50, 51, 52, 54, 56, 50, 51, 49, 52, 54]
        held = {}
        nxt_tp = rnd.randint(60, 200)
        while tk < ticks:
            if tk >= nxt_tp:
                nxt_tp = tk + rnd.randint(60, 400)
                r = rnd.random()
                if r < 0.15 and plats:                     # land on a platform (Lord platforms in the main area)
                    px, py, pw = plats[(rnd.choice(lord_ix) if scen == "main" else rnd.randrange(len(plats))) % len(plats)]
                    x, y = px * TILE + pw * TILE // 2 + rnd.randint(-6, 6), py * TILE - rnd.randint(8, 60)
                elif r < 0.75 and spots:                   # an item / door / cage
                    sx, sy = rnd.choice(spots)
                    x, y = sx * TILE + rnd.randint(-8, 2), sy * TILE + rnd.randint(0, 14)
                else:                                      # anywhere
                    x, y = rnd.randint(0, w * TILE - 20), rnd.randint(lo * TILE + 20, hi * TILE - 20)
                ev.append((tk, "T", [x * 16, y * 16]))
            k = rnd.choice(keys)
            ev.append((tk, "P", [k]))
            held[k] = tk + rnd.randint(1, 25)
            for kk, until in list(held.items()):
                if until <= tk:
                    ev.append((tk, "R", [kk])); del held[kk]
            tk += rnd.randint(1, 20)
    for tk, op, a in sorted(ev, key=lambda e: e[0]):
        print(tk, op, *a)


if __name__ == "__main__":
    main()
