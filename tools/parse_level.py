"""Parser for MikisWorld.jar /level.bin, written from the decompiled d.c() + a.a(DataInputStream, int).

Usage: python -I parse_level.py <level.bin> [out.json]

Integer coordinates are stored raw (tile units / pixel units as in the file);
the game rescales some of them at load time (see docs/MikisWorld-analysis.md).
"""
import json
import struct
import sys


class R:
    def __init__(self, b):
        self.b, self.p = b, 0

    def i8(self):
        v = struct.unpack_from(">b", self.b, self.p)[0]; self.p += 1; return v

    def u8(self):
        v = self.b[self.p]; self.p += 1; return v

    def i16(self):
        v = struct.unpack_from(">h", self.b, self.p)[0]; self.p += 2; return v

    def i32(self):
        v = struct.unpack_from(">i", self.b, self.p)[0]; self.p += 4; return v

    def skip(self, n):
        self.p += n


def packed10(r):
    """count byte, then `count` bytes OR-ed into an int, 10 bits each (first byte = highest)."""
    count, v = r.i8(), 0
    for _ in range(count):
        v = ((v << 10) | r.i8()) & 0xFFFFFFFF
    return count, v


def parse(data):
    r = R(data)
    lv = {}
    n = r.i8()
    lv["platform_flags"] = [r.u8() for _ in range(n)]          # bit0 = decor only (no collision), bits1-2 = move dir, bits3-5 = range, bits6-7 = speed
    lv["lord1_platform"] = r.i8()                               # platform index that triggers the 1st "lord" encounter
    c, lv["lord1_unlock_raw"] = packed10(r)                     # 3 x 10-bit platform indices released after the encounter
    lv["lord1_pos"] = [r.i16(), r.i16()]                        # x, y (scaled *cl/16, *ck/16 at load)
    lv["lord2_platform"] = r.i8()
    c, lv["lord2_unlock_raw"] = packed10(r)
    lv["lord2_pos"] = [r.i16(), r.i16()]
    lv["start_main"] = [r.i16(), r.i16()]                       # player start, main area (x*cl, y*ck)
    lv["start_final"] = [r.i16(), r.i16()]                      # player start, final area
    k = r.i8()
    lv["subarea_spawn"] = [[r.i16(), r.i16()] for _ in range(k)]
    lv["area_main_bottom_row"] = r.i16()
    lv["area_sub_bottom_row"] = r.i16()
    lv["subarea_rows"] = [r.i16() for _ in range(k)]
    n = r.i8(); lv["gate_cells_b8"] = [r.i16() for _ in range(n)]
    n = r.i8(); lv["exit_cells_bi"] = [r.i16() for _ in range(n)]
    lv["total_coins_declared"] = r.i8()
    lv["lord_final_pos"] = [r.i16(), r.i16()]
    # tile layer container (a.a(DataInputStream,0))
    start = r.p
    layers = r.i8()
    offs = [r.i32() for _ in range(layers)]
    r.p = start + offs[0]
    w, h, bpp = r.i16(), r.i16(), r.i8()
    per = 8 if bpp != 8 else 4
    bits = 4 if bpp != 8 else 8
    tiles = []
    while len(tiles) < w * h:
        v = r.i32() & 0xFFFFFFFF
        for j in range(per):
            if len(tiles) < w * h:
                tiles.append((v >> (32 - (j + 1) * bits)) & ((1 << bits) - 1))
    lv["map"] = {"width": w, "height": h, "bpp": bpp, "layers": layers, "tiles": tiles}
    lv["_bytes_left"] = len(data) - r.p
    return lv


if __name__ == "__main__":
    lv = parse(open(sys.argv[1], "rb").read())
    m = lv["map"]
    print("map %dx%d bpp=%d layers=%d, unread bytes=%d" % (m["width"], m["height"], m["bpp"], m["layers"], lv["_bytes_left"]))
    print("platforms=%d subareas=%d coins(tile 6)=%d keys(7)=%d cages(15)=%d doors(12)=%d" % (
        len(lv["platform_flags"]), len(lv["subarea_spawn"]),
        m["tiles"].count(6), m["tiles"].count(7), m["tiles"].count(15), m["tiles"].count(12)))
    if len(sys.argv) > 2:
        json.dump(lv, open(sys.argv[2], "w"), indent=1)
