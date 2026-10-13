<p align="center">
<img width="480" alt="collectionlogo" src="https://github.com/yanm1103/mikisworld/blob/master/port/1920x1080.png?raw=true" />
</p>

# Miki's World PC Collection (decomp + port)

A PC port of the two mobile phone games by Sumea (2002):
- **Miki's World 1: The Lost Tiara**
- **Miki's World 2: The Storm Master**

The logic of each game was 100% decompiled, translated literally and validated against the phone's code.

This repository includes both the decomp and an unofficial PC port by TwistZero (github.com/yanm1103). The original games are © 2002 Sumea; this repository contains only new code and **does not distribute the games**.

The port reads its resources straight from the `.jar` files you provide.

# PC port information

https://github.com/user-attachments/assets/2b874e0d-7876-4869-b7d0-14b03dbf071e

https://github.com/user-attachments/assets/98dbf750-d512-474f-810e-0ac2f8cb6636

## How to play

1. Download `MikisWorldCollection.exe` from the Releases tab (recommended).  
(or build it yourself: follow the technical instructions at the end of this readme)
2. Put `MikisWorld.jar` and `MikisWorld2.jar` in a folder called **`JAR`** next to the exe. **Both** are required; if one is missing, the game tells you which.
3. Open the executable and enjoy.

## Default controls

| | Keyboard | Gamepad |
|---|---|---|
| Walk | arrows ← → or `A` `D` | D-pad or left stick |
| Jump | ↑ or `W` | `A` |
| Diagonal jump | `Q` / `E` (or ↑ together with a direction) | `A` together with a direction |
| Enter portals / interact | ↓ or `S` | `X` |
| Confirm / skip cutscene | `Enter` or `Space` | `Y` (in menus and cutscenes, `A` too) |
| Menu / pause | `Esc` | `Start` |

- **Remap everything:** menu → *Options* → *Keyboard keys* or *Gamepad*  
  You can map up to two inputs per action; `Backspace`/`X` clears; *Reset to defaults* restores.
- **Numeric keypad:** the digits still work as on the phone (`4`/`6` walk, `2` jump, `1`/`3` diagonal, `8` interact, `5` confirm)  
  These keys are **not** remappable, because the secret code is typed with them. Note that the phone layout is inverted: 1/2/3 is the top row, while on a keyboard 7/8/9 is the top row.
- **Mouse:** the menus are clickable (hovering highlights, left click activates, right click goes back, the wheel scrolls the instructions). In the bonus minigame, hovering over a cell moves the cursor and a click places the orb there.
- **Start-up picture:** when the program opens, a credits picture (`port/splash.png`, 320x290) shows for 2.5 seconds; any key, mouse button or gamepad button skips it. `--no-splash` leaves it out.
- **Title screen:** any key (or click) opens the menu, and it opens by itself after 5 s, like in the original.
- **High-score name with a gamepad:** ↑/↓ change the letter, → adds one, ←/`B` erases and `A`/`Start` saves.
- **Clear the high scores:** on the high-score screen, hold `Enter` (or `A`) for 3 seconds. A short tap still just closes the screen.

## Options

Menu → *Options*. Changed settings apply to both games.

| Option | What it does |
|---|---|
| Sound | Volume from 0 to 100 % (←/→, steps of 5); 0 % is mute |
| Fullscreen | Borderless full screen (can also be toggled with `F11`) |
| Integer scale | on: the picture is magnified by a **whole-number** factor (perfect pixels) and the window snaps to whole multiples when resized (`+`/`-` change the window zoom). Off: the picture fills the window |
| View | `original`: exactly what the phone screen showed. `extended`: draws more of the level around the original screen, as much as the window allows (the window size divided by a zoom; `+`/`-` change the zoom with a 150 ms glide, by default the picture is about 1.5 times as tall as the original screen). The game itself does not change: the camera, the physics and everything validated stay as they are; only the drawing reaches further. Limits: the map is only 16 tiles wide (224 px in game 1, 192 px in game 2), so on a wide window the sides beyond the map are black, and vertically the view stops at the edges of the current area (world, final stage, sub-areas). The lives, the coin and bird/rainbow counters and the timer stay inside the map, not in the corners of the window. Menus, cutscenes and the bonus minigame keep the original screen, centred. Two small adjustments apply only in this mode: platforms and animated tiles (coins, portals) are processed for the whole picture, and in the last stage the lightning starts at, and is dropped from, the edges of the picture |
| Filter | How the picture is magnified (works with *Integer scale* on or off): `nearest` (hard pixels), `bilinear` (smoothed), `sharp` (bilinear only at the pixel edge: crisp at any zoom), `xBRZ` and `ScaleFX` (smooth the edges and diagonals of the pixel art without blurring), `LCD` (grid between the pixels, like a phone screen) and `CRT` (slight curvature, scanlines, phosphor mask and glow). Inside a level, changing the filter hides the menu and shows only `< filter >` in the top right corner, with the game paused, so you can see the result right away (←/→ change it; any other key goes back to the menu) |
| Ghosting | Trail effect of phone LCD screens: a pixel takes a few frames to reach its new colour. Works with any filter |
| Movement | `new` (default) or `original`: see below |
| Vibration | The controller rumbles when Miki is hit (on the original, the phone would vibrate). XInput only (or others through Steam Input/DS4Windows) |

## Improvements over the original

- **Movement `new`:** The character's movement was improved to make the gameplay smoother.  
Holding the jump button does not make Miki jump endlessly (each jump needs a new press); the direction you hold stays in effect after you release another key, so you can change sides while falling, when landing and **in the middle of a diagonal jump**
- **Movement `original`**: the movement as it was in the original game, which cleared all movement when any key was released. Kept only for masochists.
- **PC menus and controls:**
  - A gamepad or a keyboard can be used
  - The menus can be controlled with the mouse
  - All keys are remappable
  - A volume slider was added instead of an on/off toggle
  - Borderless full screen, integer scaling.
  - Debug menu


## Original game bugs fixed
-  **Spamming the portal button crashed the game:** Pressing the enter-portal button several times during the transition caused a double transition and could crash the game (a bug of the original game; if you set **Movement: `original`**, the bug is still there on purpose).
- **Portal transition in game 2:** The original game had a bug when entering a portal, where the transition ended halfway, so the screen only darkened and came back at once; now it slides like in the first game.


## Debug menu (`F3`)

Press `F3` to open and close the debug menu.

| Option | What it does |
|---|---|
| Invincible | no damage: hits are ignored (no life lost, no sound, no vibration) |
| Infinite lives | damage still happens (life lost, sound, vibration, spark), but the life is restored instantly |
| Hitboxes | Lets you see the hitbox of every sprite in both games |
| Unlock bonus | unlocks (or locks again) the bonus minigame, without having to collect all the coins; takes effect from the next *New game* |
| Skip level | from the world, goes to the final stage (through the normal transition); from the final stage, goes to the end of the game |

### Hitbox wireframe

Useful for artists and modders: shows what the physics actually tests, in either game.

| Colour | What it is |
|---|---|
| green | the player's collision box against the tiles |
| yellow | landing line on the platforms (and, in game 2, the player's centre point, which Mork uses) |
| grey | the player's sprite frame |
| red / orange | solid tiles / one-way platforms |
| cyan | top edge of the platforms (clouds) |
| light blue | the region of tiles the player picks up or enters (coins, key, portals) |
| light yellow | each coin, key and bird cage (rainbow in game 2) that can still be picked up |
| pink | the zone around the feet that a projectile has to reach to cause damage |
| white | projectiles (the sprite box and the point the damage test uses) |
| magenta | game 2: Mork's zone |

## Mods

Mods are folders that go over the game's own files: new sprites and tiles, a different title picture, other sounds (the Nokia `.ott` format, or plain `.wav`/`.ogg` recordings), and, for the adventurous, texts, cutscenes and levels. Some simple sample mods come in the `mods` folder of the repository and in the zip of each release. Put mods in a `mods` folder next to the executable (or in `%APPDATA%/MikisWorld/mods`), then switch them on in **Options → Mods** on the start screen. While playing, **F5** reads the pictures and sounds again, so you can edit a file and see the result at once. The game warns about things that are likely to break (a picture of the wrong size, a misspelt file name). A mod that only changes pictures and sounds keeps your saved high scores; one that changes the game itself gets its own save.

How to make one, with the sizes and names of every file: [docs/MODDING.md](docs/MODDING.md). Planned next: new levels as extra entries of the menu, and rules (lives, speeds, timers) in readable files.

## Bonus minigame secret code

Collect all the coins, or type the following code to unlock the bonus game mode!

| Game | Code |
|---|---|
| Miki's World 1: The Lost Tiara | `7 7 7 7 3 3 2 2 2 7 7 7 3 3 8` |
| Miki's World 2: The Storm Master | `8 3 3 7 7 7 2 2 2 3 3 7 7 7 7` |

## Your files

Everything lives in `%APPDATA%/MikisWorld/`: `save.json` (game 1) and `save2.json` (game 2) with the unlocked bonus and the high scores, `settings.json` with the options, `bindings.json` with the remapped controls.

## In case of a crash

The window stays open with the error.
- Press `Enter` to go back to the start screen.
- Press `C` to copy the error text.
- Press `Esc` to close.

The details are also written to `%APPDATA%/MikisWorld/crash.log`.

If you hit an error, please let us know!

---
<details>
<summary><b>Technical information (for developers)</b>: progress, history, how to build, structure, validation</summary>

### Useful parameters
- `--game 1` or `--game 2` (skips the start screen and opens one of the two games straight away)
- `--scale N` (initial window size)
- `--no-sound` (starts without sound; the saved volume is not changed)
- `--logs` (shows the program log, for reporting problems)
- `--no-splash` (no start-up picture), `--splash` (even in scripted runs)
- `--mods-dir folder` (one more place to look for mods), `--no-mods` (ignore all mods for this run), `--mods-state file.json` (where the on/off choice and the order are kept)


### Progress

> This table is updated **on every commit**. Percentages are effort estimates, not lines of code.
> Weights: decompilation 10 · logic 30 · rendering 20 · window/input 10 · menus/saves 15 · audio 15.
> Current version: **1.2.0** (see the [CHANGELOG](CHANGELOG.md)). Reference milestone of the base: tag `base-1.0`.

#### MikisWorld.jar (Miki's World 1: The Lost Tiara)

| Part | Status | Notes |
|---|---|---|
| Decompilation and analysis | **100%** | [`docs/MikisWorld-analysis.md`](docs/MikisWorld-analysis.md) |
| Game logic (physics, platforms, items, Lords, final stage, cutscenes) | **100%** | 17/17 traces identical to the original (86,624 ticks) |
| Bonus minigame (5×4, chain reaction + AI) | **100%** | identical to the original, including the AI and the RNG |
| Rendering (scene, parallax, HUD, wipes, cutscenes, minigame) | **100%** | 930 frames pixel-identical to the original (text excluded) |
| Window, scaling, input | **100%** | whole-number scaling (with *snap*) or proportional, shader image filters (sharp, xBRZ, ScaleFX, LCD, CRT) and ghosting, borderless full screen, volume 0–100 %; **remappable** keyboard and **gamepad** (*Options* screen and start screen), original/new movement, start screen with options |
| Title screen, menus, hi-scores, options, saving | **100%** | hi-scores identical to the original (5 seeds × 400 ops, on the `k` classes of both JARs); flow covered by automated tests; the "Vibrator" option rumbles the **controller** when Miki is hit (there is nothing to vibrate on a keyboard) |
| Audio (`.ott`) | **100%** | decoder validated against an independent reference (12/12); **checked by ear** with the −1 octave adjustment |
| **Estimated total** | **100%** | everything the original does is ported and validated (see "Known limits" in *Validation*) |

#### MikisWorld2.jar (Miki's World 2: The Storm Master)

Same engine as game 1 (~85% of the main class's bytecode is identical), with a 128×128 resolution, 12 px tiles,
a new chaser (Mork), rainbows in place of the birds and a different tile layer format. Details in
[`docs/MikisWorld2-analysis.md`](docs/MikisWorld2-analysis.md).

| Part | Status | Notes |
|---|---|---|
| Decompilation and analysis | **100%** | all 12 classes read and classified ("Cobertura por classe" table, in Portuguese, in [`docs/MikisWorld2-analysis.md`](docs/MikisWorld2-analysis.md)); `level.bin` validated (`parse_level.py --v2`); hi-score tested against JAR 2's own `k` class. Menus/forms (the MIDP UI API) are verified by reading + `apptest`, not by running the original |
| Game logic (Mork, rainbows, altered physics) | **100%** | `Game2`: 20/20 traces identical to the original (~100 thousand ticks), including Mork, Lords, sub-areas and the final stage |
| Rendering (128×128, individual tiles, new background) | **100%** | ~600 frames pixel-identical to the original (text excluded), including the tiles' ring cache and the minigame |
| Menus, hi-scores, saving | **100%** | the same `App` serves both games (own secret code, separate save); 4 s splash implemented but off by default |
| Audio | **100%** | 12/12 sounds validated against the reference; only `title.ott` and `gameend.ott` differ from game 1; the title plays 3×; **checked by ear** |
| Window, scaling, input | **100%** | same window, options and (remappable) controls as game 1; game choice (`--game 1\|2` or the selection screen) |
| **Estimated total** | **100%** | same weights as game 1 |

#### History

| Commit | What changed | Total MW1 · MW2 |
|---|---|---|
| initial | Decompilation, original harness, C# core validated by traces | ~45% · — |
| Renderer and window | Software renderer validated by frames; Raylib window reading the JAR | ~69% · — |
| Menus and saving | Title, menu, instructions, hi-scores, name entry, mode list, JSON save | ~82% · — |
| Audio | Nokia Ring Tone decoder + synthesizer + playback in Raylib; `play.bat` | ~95% · — |
| Game 2 decomp | MikisWorld2.jar decompiled, read and compared by bytecode; level parser v2; harness runs jar 2 | ~95% · ~9% |
| Game2 (logic) | Literal translation of game 2's class (Mork, rainbows, wrapping collision); 20/20 traces | ~95% · ~39% |
| Game2 (drawing) | 128×128 renderer validated by frames (includes quirks: 12×12 wipe snapshot, ring cache) | ~95% · ~59% |
| Two games in the same program | Generic `App` (`IMikisGame`), game choice, audio and save per JAR; test script fixes | ~95% · ~93% |
| Speech bubble fix | The intro's bubble showed up in the final cutscene (flags were not recreated on every `Load`); regression test | ~95% · ~93% |
| Window with whole-number scaling | *Snap* to whole multiples when resizing, `F11`, `+`/`-` | ~96% · ~94% |
| Sounds one octave lower | Default −1 octave (`--octave N`); audio checked by ear in both games | ~98% · ~97% |
| Documentation | README and analyses updated; optional game 2 splash; credits | ~98% · ~97% |
| Window and volume options | *Options* screen (volume 0–100 % on the wave amplitude, full screen, whole-number scaling, nearest/bilinear filter); both JARs become mandatory | ~98% · ~97% |
| Gamepad and remapping | Remappable keyboard and controller (2 inputs per action, `bindings.json`), left stick, name entry with the controller; input router with tests | ~99% · ~98% |
| Engine tweaks and start screen | Slower jump and walking animation for the protagonist, *Movement* original/revamped option (no continuous jump when holding ↑, held direction counts when falling/landing), start screen with games + shared options; `EngineTweaks` off in the tests (traces stay identical) | ~99% · ~98% |
| Closing game 2's decomp | Reading of `GameMIDlet`, `a`, `j`, `k`, `h`, `b`, `g`; differential hi-score test against game 2's `k` (found the empty name); game 2 menu order, title opens with any key / after 5 s, title sound stopped when entering game 2 | ~99% · ~99% |
| Reported bug fixes | Turning in the air on a diagonal jump (revamped); portal press during the transition ignored (cause of the double transition and the `IndexOutOfRange` crash); start screen's full screen applies in the game; *Exit* goes back to the game selection; whole-number zoom on the start screen; crash log and error screen | ~99% · ~99% |
| High scores and instructions | New row highlighted in the high scores, Instructions and hints follow the configured controls and the last device (keyboard/controller), *Movement*: "new", input reset when leaving the start screen | ~99% · ~99% |
| Controller vibration | The original's "Vibrator" becomes gamepad vibration (XInput) on damage; *Vibration* option in the Options and on the start screen | ~99% · ~99% |
| Slower Mork | In game 2 Mork moves at 2/3 speed (`EngineTweaks.MorkSpeedPercent`); off in the tests, which stay identical to the original | ~99% · ~99% |
| Mouse in the menus | Clickable menus (hover, click, right click, wheel); the game's text ("Hurry up!") no longer shows over the menu; progress at 100% in both games | 100% · 100% |
| Executable and `--logs` | `tools\publish.bat` produces `MikisWorldCollection.exe`, self-contained and single-file; windowed program, log only with `--logs`; README split into a player part and a technical part | 100% · 100% |
| Shared code in `GameBase` | State, input, movement, minigame, projectiles, camera and level scan of both games move to a base class (~1000 fewer duplicated lines); what really differs in the original stays in each game; traces and frames identical | 100% · 100% |
| Image filters | `Filter`: nearest, bilinear, sharp, xBRZ, ScaleFX, LCD, CRT (embedded GLSL shaders; they combine with whole-number scaling); `Ghosting`; live switching inside a level; also configurable on the start screen; a shader that fails to compile falls back to `nearest` | 100% · 100% |
| Mods (phase 1: files) | `mods/<name>/` folders with `mod.json` over the game's resources (`ModAssets`): pictures, sounds (`.ott`, `.wav`, `.ogg`), and any other resource by name, per game (`game1/`, `game2/`); on/off and priority in Options → Mods; F5 reloads pictures and sounds; checks (picture sizes, unknown files, broken sounds/manifest); save file code for mods that change the game itself; tests in `ModTest` | 100% · 100% |
| Extended view | `View` original/extended: the picture can be larger than the original screen (logic untouched, traces and frames identical; only the drawing reaches further, clipped to the map and the current area, black beyond); HUD (lives, coins, birds/rainbows, timer) kept inside the map; glide between zoom levels (150 ms, `ZoomTransition`); menus, cutscenes and minigame centred; mouse follows; wipes cover the whole picture; zoom with `+`/`-`; tests: black strips, centred clickable menus, wipe, and random play in five view sizes | 100% · 100% |
| Batch of tweaks (debug, mouse in the bonus, remapping) | F3 menu with *Unlock bonus* and *Skip level*; "Saved." note when closing the options; mouse in the bonus minigame; remapping keyboard/controller on the start screen too; highlighted *Reset to defaults*; end of a game always shows the high scores; the `Enter` that saves the name no longer closes the list at once; holding confirm for 3 s on the list clears the scores | 100% · 100% |
| Usability tweaks | Long lists scroll (options and menu on game 2's 128×128 screen, also with the mouse wheel); start screen hints follow the device; 100 ms pulse when *Vibration* is switched back on; `--no-sound` | 100% · 100% |
| Hitbox wireframe | debug menu (`F3`) → *Hitboxes*: draws the boxes reconstructed from the original's arithmetic (collision, landing, tiles, projectiles, Mork); drawing only, does not change the logic | 100% · 100% |
| Debug menu (`F3`) | Invincibility, infinite lives (damage happens, the life comes back), hitboxes; only while running | 100% · 100% |
| Hints and transition | "Press 8/5" hints with the configured keys, bonus message stacked above the hint, game 2 portal transition with the full snapshot (the original has 12×12) | 100% · 100% |

### How to run from the code

Needs the .NET 8 SDK and the JARs in `JAR/`.

**Windows, in the repository:** double-click `play.bat` (or run `play.bat`/`play.bat --scale 5` in a terminal).
It uses the portable SDK in `tools/dotnet` and rebuilds by itself if the code changed.

With a .NET 8 SDK installed on the system, the equivalent is:

```
dotnet run --project port/MikisWorld.Game -- [--game 1|2] [--scale N] [--octave N] [--settings file.json] [--bindings file.json] [--no-sound] [--logs] [path/to/the.jar]
```

**Executable.** `tools\publish.bat` produces `dist\MikisWorldCollection.exe`: a single file (~35 MB), self-contained (no need to install .NET); `dist/` is not versioned.
The JARs are not included: put `MikisWorld.jar` and `MikisWorld2.jar` in a `JAR` folder next to the exe. It is a windowed program (it does not open a console).

**Logs.** Only with `--logs`: raylib's log and the program's own (JARs used, configuration paths, start and end of each game). Opened from Explorer, `--logs` opens a
console window; in a terminal, it writes to it. Without `--logs` nothing is printed; crashes go to the error screen and to `crash.log`.

**Sound.** Only one sound plays at a time, like on the phone. `--octave N` adjusts the octave (default `-1`; `0` = as decoded). The volume is the one from the *Options* screen.
`--export-sounds folder` writes one WAV per sound; `--sound-test` tests the audio device; `--rumble-test` rumbles the XInput controllers for half a second.

**Engine tweaks (always on in the program).** The jump lasts twice as long (each step of the arc lasts two ticks: same height and same distance),
the protagonist's walking animation shows each sprite for twice as long, and in game 2 Mork walks at 2/3 of the original speed (each of his moves has the same
distance and duration in "steps", it just takes proportionally longer). In game 1 this also undoes an oscillation of the original: when walking on the ground the
state alternated between "walking" and "falling" every tick and the air sprite flickered. In the validation tests (traces/frames) these tweaks are off (`EngineTweaks`: jump, animation, Mork, game 2 portal transition, stacked hints).

**Movement `new`, in detail.** (1) Holding ↑ does not repeat the jump (a press up to 100 ms before landing counts); (2) only the released key is released (the direction still held counts), including
in the middle of a diagonal jump; (3) an "enter portal" press during the transition is ignored, and so are portals without a destination. The crash this avoids comes from the **original**: the extra
press is kept and, after the transition, the game uses the portal of the area it just left, which in the main area has no destination (`IndexOutOfRange`).

**Configuration files.** `settings.json` (options, without full screen), `bindings.json`, `save.json`/`save2.json` and `crash.log` in `%APPDATA%/MikisWorld/`; `--settings`/`--bindings`/`--save` change the paths.

### Structure

```
docs/                      analysis of the two games and of the file formats
port/MikisWorld.Core/      logic + software rendering (no window/audio dependency)
                             GameBase*.cs (what both games have in common: state, input, player movement, bonus minigame, projectiles, camera…) · Game*.cs (game 1) · Game2*.cs (game 2) · App.cs (menus) · RingTone.cs (audio) · Canvas.cs · InputBindings.cs (remapping) · EngineTweaks.cs
port/MikisWorld.Game/      Raylib frontend (window, text, audio); reads the resources straight from the JARs
                             InputRouter.cs (keyboard/controller → phone events) · DevicePoller.cs (reads Raylib) · Launcher.cs + LauncherControls.cs (start screen and its remapping) · PostProcessor.cs + Shaders/ (filters) · Ghosting.cs
port/MikisWorld.TraceTest/ trace/frame replay and tests of the flow, sounds, hi-scores, cutscenes, input router and engine tweaks
tools/harness/             runs the original JARs headless (MIDP stubs, real Graphics) and records traces/frames
tools/record_*.sh          record the references from the original JARs (traces, frames, hi-scores, sounds)
tools/run_*.sh             compare the C# with the references;  tools/run_tests.sh  runs everything
tools/gen_inputs.py        generates deterministic input scripts
tools/parse_level.py       independent parser of level.bin (--v2 for game 2)
tools/decode_ott.py        independent decoder of the sounds (reference)
tools/classmatch.py        matches obfuscated classes of the two JARs by bytecode (also fieldmap.py)
```

Not versioned (see `.gitignore`): `extracted/`, `decompiled/`, `traces/`, `sons/`, `tools/dotnet/`, `tools/cfr.jar`.
`JAR/` is **not** in `.gitignore`: the JARs are Sumea's material, so it is up to you whether to version them.

### Reproducing the validation environment

1. `jar xf JAR/MikisWorld.jar` into `extracted/MikisWorld/` and `jar xf JAR/MikisWorld2.jar` into `extracted/MikisWorld2/`.
2. Portable .NET 8 SDK: `dotnet-install.ps1 -Channel 8.0 -InstallDir tools/dotnet`.
3. (Optional) Decompiler: CFR 0.152 → `tools/cfr.jar`, from `repo1.maven.org/maven2/org/benf/cfr/0.152/`.
4. References, from the original JARs (JDK 11+ and Python):
   `tools/record_traces.sh`, `tools/record_traces2.sh`, `tools/record_frames.sh`, `tools/record_frames2.sh`,
   `tools/record_hall.sh`, `tools/record_ott.sh`.
5. `tools/run_tests.sh` runs everything and must end with `ALL VALIDATIONS PASSED`: 17/17 traces and 10/10 frame sets of
   game 1, 20/20 traces and 9/9 frame sets of game 2, hi-scores, sound decoder (24/24), cutscene bubbles and app flow.

### Validation

The originals are run headless (`tools/harness`) with a fixed 20 ms step, a `java.util.Random` with a fixed seed and a
sequence of keys + teleports. The complete state is recorded on every tick and the C# has to reproduce **exactly** the same
lines. For drawing, the harness implements a real `Graphics`/`Image` on top of `BufferedImage` and records frames (176×144 and
128×128); the C# renderer has to produce the same pixels. Each test was checked with **negative controls** (trace of a
different game, wrong seed) to make sure it fails on differences.

**Original bugs reproduced on purpose** (the C# fails on the same tick): falling below the map, and using a portal without a destination
in the sub-areas (`ArrayIndexOutOfBounds`).
**Original bugs the port does not reproduce:** in game 2, starting the minigame before the main level is loaded hangs the painting
(`NullPointerException` / infinite loop, because the images and the tile size do not exist yet).

Known limits: the inputs are random with teleports (not played games) and the text (the phone's font) is left out of the
pixel comparison; long games played by hand may reveal what the random tests do not cover (that is how the bubble that leaked
into the final cutscene was found).

### Audio

The 12 effects/music tracks of each game are Nokia Ring Tones (`.ott`). The decoder (`RingTone.cs`) was written from the bit
layout of the Smart Messaging format and validated against an independent Python decoder (`tools/decode_ott.py`). The synthesis is a simple
square wave, like the phone's buzzer, and only one sound plays at a time.

Two points were **inferred** (the official specification was not available): a pattern with loop value *n* plays *n+1* times,
and the octave of each note. Since almost all the decoded sounds fell between 0.7 and 2.6 kHz (too high), the port's default is
**one octave lower** — a choice confirmed by ear in both games. `--octave N` lets you adjust it.

</details>

## Third-party licenses (shaders)

The shaders in `port/MikisWorld.Game/Shaders/` come from the [libretro/glsl-shaders](https://github.com/libretro/glsl-shaders) repository, with the license headers kept in the files:

- **ScaleFX** (`scalefx-pass0..4.glsl`), by Sp00kyFox: MIT license.
- **xBRZ freescale** (`xbrz-freescale.glsl`), by Hyllian (MIT), but with parts derived from Zenju's xBRZ, which is **GPLv3**. If the project is distributed as a binary, that part carries the GPLv3 obligations.
- `sharp.glsl`, `lcd.glsl`, `crt.glsl` and `vertex.glsl` were written for this project (`sharp` follows the public "sharp-bilinear" idea from libretro). The CRT is a simple version, not CRT-Royale.
