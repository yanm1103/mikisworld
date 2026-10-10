# Making mods

## Your first mod, step by step: Blue Miki

The goal: Miki of game 1 with different colours. It takes five minutes.

1. **Make the folder.** Create `mods/blue-miki/` next to the executable (when running from the repository with `play.bat`, use the `mods/` folder at the root of the repository: `play.bat` looks there too).
2. **Get the picture to change.** Miki's frames are in `player.png`, inside `JAR/MikisWorld.jar` (a JAR is a ZIP: open it with any archiver and take the file out). Copy it into `mods/blue-miki/`.
3. **Edit it** in any image editor, **keeping the size** (128x23: eight frames of 16 px). Keep the transparent background transparent.
4. **Say what it is.** Create `mods/blue-miki/mod.json`:

   ```json
   { "name": "Blue Miki", "author": "you", "version": "1.0", "game": 1 }
   ```

5. **Switch it on.** Start the game, then *Options → Mods*, select *Blue Miki*, press Enter (`[x]` means on). Start Miki's World 1.
6. **Edit and look again.** While playing, change `player.png` and press **F5**: the new picture appears at once.

The repository has a script that does steps 1 to 4 for you, from your own copy of the JAR (the artwork is Sumea's, so the result is not kept in the repository): `python tools/make_example_mod.py` (needs `pip install pillow`) writes `mods/blue-miki/` with Miki's red and blue swapped. Open the files to see what a mod looks like.

If something is wrong (a picture of another size, a file name with a typo), *Options → Mods* shows a `!` next to the mod and the first remarks below the list.

A mod is a folder. The game puts the mods you switch on **over** its own files: for each resource it looks in the mods first (the last mod in the list wins) and falls back to the original.

## Where mods go

Put one folder per mod in any of these places (the first one found wins if two folders have the same name):

- `mods/` next to the executable (and, with `play.bat`, the `mods/` folder at the root of the repository)
- `%APPDATA%/MikisWorld/mods/`
- any folder given with `--mods-dir <folder>`

Switch mods on and off in **Options → Mods** on the start screen: Enter toggles, ←/→ change the priority (a mod lower in the list wins over the ones above it). The choice is saved in `%APPDATA%/MikisWorld/mods.json`. `--no-mods` ignores all of them for one run.

## A mod folder

```
mods/my-mod/
  mod.json          (optional but recommended)
  player.png        applies to every game the mod targets
  coin.wav          a recording instead of the ring tone coin.ott
  game1/tiles.png   only for Miki's World 1
  game2/tiles.png   only for Miki's World 2
```

`mod.json`:

```json
{
  "name": "Blue Miki",
  "author": "you",
  "version": "1.0",
  "description": "One line about it",
  "game": 1
}
```

`game` is `1`, `2` or `0` (both, the default). Files in the folder itself apply to every game the mod targets; `game1/` and `game2/` hold files for one game only (they win over the plain ones).

## Trying your changes quickly

Press **F5** while playing: the pictures and the sounds are read again from the folders, the game keeps going. (Text, levels and cutscene data are read when the game starts, so those need a restart.)

Run with `--logs` to see what the game thinks of each mod. **Options → Mods** also shows a `!n` next to a mod with remarks, and the first ones for the selected mod. The checks are:

- a picture whose size differs from the original's (the game assumes the sizes of its sheets: tile size, player frames...)
- a file the game never asks for (a typo in a name), or a picture / sound that cannot be read
- a broken `mod.json`

## What can be replaced

Every file of the original JARs can be replaced by a file with the same name. Sizes of pictures must stay the same unless the table says otherwise.

| File | Game 1 | Game 2 | What it is |
|---|---|---|---|
| `tiles.png` | 126x42 | 108x36 | the tile sheet, 9x3 tiles of 14 px (game 1) / 12 px (game 2) |
| `player.png` | 128x23 | 90x20 | Miki's frames, 16 / 15 px wide each |
| `lord.png` | 142x26 | 120x27 | the Lords and (game 2) Mork |
| `items.png` | 75x14 | 75x14 | birds, bolts: 5 frames |
| `damage.png` | 34x14 | 34x14 | the hit spark, 2 frames |
| `numbers.png`, `time.png` | 88x10, 140x18 | same | digits of the HUD |
| `sky.png`, `clouds.png` | 36x112, 45x57 | - | game 1 only: the sky and its two cloud bands |
| `symbols.png` | 56x14 | - | game 1 cutscene symbols |
| `title.png` | 176x144 | 128x128 | the title picture |
| `splash.png` | - | 77x17 | game 2 splash |
| `end.png`, `gameover.png` | 111x72, 53x28 | 106x64, 54x28 | |
| `grid.png`, `bonus.png`, `cursor.png` | 130x105, 32x11, 60x30 | 104x93, 28x9, 50x27 | the bonus minigame |
| `*.ott` | | | the sounds, in the Nokia ring tone format |
| `coin.wav`, `coin.ogg`... | | | a recording used **instead of** the ring tone of the same name (`coin`, `cage`, `key`, `damage`, `lord`, `title`, `bonus`, `gameover`, `gamestart`, `gameend`, `onceuponatime`, `transportation`) |
| `level.bin` | | | the level (binary; a friendlier format is planned) |
| `en`, `en.hlp` | | | texts and the instructions |
| `begin.bin`, `end.bin` | | | the cutscene scripts |

## Saves

A mod that only changes pictures and sounds keeps the original save (high scores, unlocked bonus). A mod that changes the game itself (levels, texts, cutscenes, anything that is not a picture or a sound) gets its own save, named after the set of such mods that are on (`save.<code>.json`), so a different level never mixes with the original high scores.

## Planned

New levels as extra entries of the game's menu, and rules (lives, speeds, timers...) in readable files.
