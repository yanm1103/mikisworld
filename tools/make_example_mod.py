#!/usr/bin/env python3
"""Builds the "Blue Miki" example mod from YOUR copy of MikisWorld.jar.

    python tools/make_example_mod.py [path/to/MikisWorld.jar] [mods-folder]

It takes Miki's sprite sheet (player.png) out of the JAR, swaps its red and blue channels and writes
    <mods-folder>/blue-miki/mod.json
    <mods-folder>/blue-miki/player.png
This is how the sample mod mods/blue-miki was made. The artwork is Sumea's: the sample mods only recolour or replace sprites.

Needs Pillow:  pip install pillow
Then switch it on in the game: Options -> Mods (start screen), or press F5 in a game to reload after editing the picture.
"""
import io
import json
import os
import sys
import zipfile

from PIL import Image


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    jar = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, "JAR", "MikisWorld.jar")
    mods = sys.argv[2] if len(sys.argv) > 2 else os.path.join(root, "mods")
    out = os.path.join(mods, "blue-miki")
    os.makedirs(out, exist_ok=True)

    with zipfile.ZipFile(jar) as z:
        sheet = Image.open(io.BytesIO(z.read("player.png"))).convert("RGBA")
    r, g, b, a = sheet.split()
    Image.merge("RGBA", (b, g, r, a)).save(os.path.join(out, "player.png"))   # same size as the original: 128x23

    manifest = {
        "name": "Blue Miki",
        "author": "you",
        "version": "1.0",
        "description": "Swaps red and blue in Miki's sprite sheet.",
        "game": 1,
    }
    with open(os.path.join(out, "mod.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    print("written:", out)


if __name__ == "__main__":
    main()
