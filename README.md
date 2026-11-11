# Rush Ahead

A 1–4 player physics party game for Android and iOS. Animals sit in a shared boat, each aiming a
hand-held air blower; air goes one way, the boat goes the other. One phone hosts; others join over
Wi-Fi LAN or online with a join code.

## Toolchain

| Tool | Version | Notes |
|---|---|---|
| Unity | **6000.3.27f1** (6.3 LTS) | Installed to `D:\Unity\Editors` via Unity Hub |
| Unity modules | Android (+ SDK/NDK/OpenJDK), iOS, Windows IL2CPP | Unity uses its own Android SDK, not the system one |
| Netcode for GameObjects | 2.13.3 | 3.x requires the Unity 6.7 beta |
| scrcpy | 4.1 | Mirror a physical Android device onto the PC |

## Architecture boundaries

Code is split into assembly definitions under `Assets/_Project/Scripts`. Dependencies point one way:

```
Content  <-  Sim  <-  AI
                 ^--  Net
                 ^--  Render
   everything  <--  UI
```

`Sim` (the authoritative simulation) must never reference `Net`, `Render`, `UI` or `AI`.
`Assets/Tests/EditMode/AsmdefBoundaryTests.cs` fails the build if that rule is broken.

## Command line

```bash
UNITY="D:/Unity/Editors/6000.3.27f1/Editor/Unity.exe"

# Apply all project settings (idempotent)
"$UNITY" -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.ProjectBootstrap.Run -logFile -

# EditMode tests
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile -

# Android debug APK -> Builds/Android/RushAhead-debug.apk
"$UNITY" -batchmode -quit -projectPath . -buildTarget Android -executeMethod RushAhead.EditorTools.BuildScript.AndroidDebug -logFile -

# Windows dev build -> Builds/Windows/RushAhead.exe
"$UNITY" -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod RushAhead.EditorTools.BuildScript.WindowsDev -logFile -
```

## Screens

The whole game is one scene, `Assets/_Project/Scenes/Game.unity`, run by `GameController`
(`Scripts/UI/Game`). Its screens are built in code with UI Toolkit (`Scripts/UI/Screens`), styled by
`Assets/_Project/UI/Game.uss`, and moved between by `AppFlow`: title, main menu, host, join,
connecting, lobby, race, results, your animal and settings. During a race the HUD only draws; every
touch goes through `CrewInputRouter` and `TouchControls`, so steering with one thumb never stops the
other from pressing Burst or Brace.

The look is sleek and sporty: dark smoky glass panels with hairline edges, white text, one hot orange accent for
what matters, and condensed uppercase type in Barlow Condensed (Barlow for text to read), from Google Fonts under the
SIL Open Font License (`Assets/_Project/ThirdParty/Fonts`, with `OFL.txt`). Buttons are slanted parallelograms painted
by `Ui.Slant` from the `--fill`, `--edge` and `--slant` properties in `Game.uss`.

## Modes, items and the shop

- **Co-op** (one raft), **Team Race** (two rafts, Red against Blue) and **Elimination**: three or four rafts, AI in
  every spare seat, where the last raft through each knockout gate (red "LAST ONE OUT" banners) is out and the river
  runs faster for those left (`Sim/Rules/Knockout.cs`). Up to four rafts and eight people.
- **Watching and looking round**: once your raft is home or out, WATCH follows another raft still racing; the look stick
  (right of the HUD, or the right mouse button, or a pad's right stick) swings the camera round the raft and eases back.
- **Power-ups**: glowing "?" crates dealt with each layout give a raft an item, used with ITEM by anyone aboard: a
  turbo can, a shield bubble that saves one fall or capsize, and an air horn that shoves rafts, scatters geese and
  barrels, and sends Old Snapjaw diving (`Sim/Course/PowerUps.cs`).
- **Weather**, the host's choice in the lobby: Clear, Rain (with gusts across the river), Fog, Night (lanterns, lit
  gates, fireflies) or Random, dealt from the round's seed (`Sim/World/Weather.cs`, `Render/WeatherView.cs`).
- **Islands** on nine courses split the river into a narrow fast line (quicker water, a boost lane, something in the
  way) and a wide safe line (`Sim/Course/Island.cs`).
- **Old Snapjaw**, the giant crocodile of Snapjaw Swamp (course 13, 22 stars), chases the last raft down the river and
  lunges when it's close (`Sim/Course/BossCroc.cs`, `Render/BossView.cs`). A crew pushing steadily keeps ahead; a burst,
  a dodge or a horn gets you out of a lunge.
- **Photo finish**: when a Team Race's first two rafts (or an Elimination's last two) cross within 0.6 s, each device
  replays the run-in at a fifth of the speed from beside the line and freezes on a sepia photo with the margin, which
  then sits on the results (`UI/Game/GameController.PhotoFinish.cs`, `Render/ReplayBoat.cs`). Nothing extra is sent.
- **Coins and the shop**: results pay coins (10 a star, a Team Race win, an Elimination podium, a new best, escaping
  Snapjaw; `UI/Records/CoinRules.cs`), spent in the shop tabs of Your animal on 12 hats, 8 raft skins and 9 blower
  paints, all made in code (`Content/Cosmetics.cs`, `Render/CosmeticViews.cs`). A raft wears its captain's skin.

## Courses

The thirteen courses, in six places (creek, lake, rapids, glacier, canyon and swamp), are laid out in code
(`Assets/_Project/Editor/CourseLibrary.cs`): the river, checkpoints, rocks,
barrels and logs, currents and whirlpools, the river's flow, and a menu of obstacles each course may be dealt, with how
many. Every round deals a fresh layout from that menu (`Sim/Course/LayoutGenerator.cs`): only the seed goes over the
network, and every device deals the same layout from it. The dealer keeps obstacles clear of the start, every respawn
(with a run-up after it), the finish, whirlpools and rocks, spaces them out, never puts two brace-or-die ones back to
back, and only puts the big ones where the river is wide enough. There are 40 kinds: paddle wheels, crocodiles, logs
swinging under bridges and geysers (`Sim/Course/Hazards.cs`), plus rockslides, hippos, sluice gates, sweepers, water
cannons, rapids, weirs, bumper buoys, ferries, bears, falling trees, snapping turtles, piranha shoals, waterspouts,
electric eels, mudslides, ice floes, beaver dams, diving pelicans, net boats, a kraken, tidal bores, jump ramps, goose
gangs, chained mines, wrecking balls, storm clouds, paddle steamers, mill races, drawbridges, cargo cranes, log flumes,
calving glaciers, dynamite floats, leaping frogs and giant fans (`Sim/Course/Obstacles`). Every one has an entry in the
in-game Field Guide (`Content/ObstacleGuide.cs`; also `docs/FIELD_GUIDE.md`). Nearly all run on the race clock, so
nothing about them goes over the network; the geese are the exception, sent with the barrels and logs. Stars are the
lives a crew finishes with. Rafts run at about 8-10 m/s on calm water and 12-14 m/s down the rapids, faster still on
the last six courses (their water runs at 9-12 m/s), so a course takes 40-55 s; glowing chevron boost lanes, dealt into the straights
with the obstacles, throw a raft forward (`Sim/Course/BoostLane.cs`), and the water carries a raft round bends rather
than flinging it off the edge. The assets in `Assets/_Project/Content/Courses` are made from it once and never
overwritten, so edits in the Inspector stick. After changing a layout in code, put every course back to the code's
layout (keeping the assets, so nothing pointing at them breaks):

```bash
"$UNITY" -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.CourseLibrary.Rebuild -logFile -
```

## Art

Most of the world is built in code. The photographic parts (the sky, rock, ground, bark, planks, metal and sand
textures, and scanned rocks, barrels, a buoy, a crate and a fern) come from [Poly Haven](https://polyhaven.com) under
CC0, in `Assets/_Project/ThirdParty/PolyHaven` (see its `CREDITS.md`). `Editor/PolyHavenArt.cs` sets their import
options and makes the materials, sky and post-processing look from them when the content bootstrap runs; without the
files the game falls back to flat colours. Code-built surfaces use `Shaders/Triplanar.shader`, which projects a photo
texture from three sides, so they need no texture coordinates. Scanned rocks are sized from their outline at the
waterline, so the stone covers its whole round collider and nothing hits before it touches.

The land under each course is a Unity terrain shaped from the river when it loads (`Render/World/GorgeTerrain.cs`):
scree at the cliff feet, a valley floor and hills, painted with four photo ground layers. Its woods are code-built
trees (`Render/World/Flora.cs`) with leaf sprays stamped from a scanned leaf and needle sprays drawn in code; shrubs,
mossy rocks and boulders are Poly Haven scans cut down to phone size with Unity's own mesh LOD tools
(`Editor/NatureArt.cs`, `Editor/MeshCutter.cs`). The practice lagoon behind the menus stands on the same land
(`GorgeTerrain.BuildPond`) with a rocky shore. The animals and the crocodile, hippo, bear and turtle are sculpted in
code from smooth shapes melted together (`Render/Sculpt`), baked to meshes by the content bootstrap
(`Editor/SculptBootstrap.cs`), and wear `Shaders/Fur.shader`, a velvet fur tinted per animal. The raft's rubber, rope
and fabric are drawn in code (`Editor/RaftArt.cs`).

## Releasing

`Store/RELEASE.md` walks through publishing on Google Play: the package name, your upload key, the signed
release bundle (`BuildScript.AndroidRelease`; `RUSHAHEAD_VERSION` sets its version), and the Play Console steps.
`.github/workflows/release.yml` builds the APK and the App Bundle on GitHub's machines and publishes them as a GitHub
Release when a version tag is pushed (`git tag v1.1.0` then `git push origin v1.1.0`), or from the Actions tab with a
version. It signs with the upload key when the repository has the secrets `ANDROID_KEYSTORE_BASE64`,
`ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` and `ANDROID_KEYALIAS_PASS` (and the Unity ones the CI workflow uses);
without them the files are debug-signed and named NOT-FOR-PLAY. Until GameCI fixes Unity Personal activation on Unity
6000.3 (game-ci/unity-test-runner#336) it stops at that step. `Store/LISTING.md` has the store text
and draft answers for Play's forms, `Store/PRIVACY.md` the privacy policy, and `Store/Graphics` the icon, feature
graphic and screenshots (made by `Design/store_art.py` from a release build's captures).

## Sound

Every sound is synthesized in code (`Scripts/Render/Audio`): no audio files to ship or license. To listen
to them outside the game, export them as WAVs into `Logs/Sounds`:

```bash
"$UNITY" -batchmode -quit -projectPath . -executeMethod RushAhead.EditorTools.SoundExport.Export -logFile -
```

## Checking it without a phone

A player build walks every screen by itself and saves a screenshot of each:

```bash
Builds/Windows/RushAhead.exe -screen-fullscreen 0 -screen-width 1200 -screen-height 540 -rushahead-tour Logs/tour -rushahead-ai-all ace -rushahead-demo-speed 3
```

- `-rushahead-safe-area 70,0,70,0` fakes a phone's notch (left, top, right, bottom, in pixels).
- Two copies, one with `-rushahead-host 127.0.0.1` and one with `-rushahead-join 127.0.0.1`, each with
  its own `-rushahead-tour` folder: the host picks a course and starts, and the joiner follows it.
- `-rushahead-course 1` skips the menus and races straight away; `-rushahead-dev` shows the tuning sliders.
- `-rushahead-mode team|elimination` (with `-rushahead-rafts 3|4`), `-rushahead-weather clear|rain|fog|night|random`
  and `-rushahead-photo-any` (every Team Race and Elimination ends in a photo finish) set up a run for screenshots.
- `-rushahead-hitzones` draws what actually hits on the water: every collider's outline (red solid, cyan triggers,
  white rafts) and each obstacle's scripted reach in yellow, live, to check that hits land where things are drawn.
- With `-rushahead-demo <folder>`, `-rushahead-quality 1` (Low) to `3` (High) picks the graphics setting, and the log
  gives frame times (average and slowest 5%) at each screenshot.
- Run demo copies one at a time: several at once on one PC can corrupt DirectX 12's pipeline cache (then the game
  crashes on quitting until the cache in `%LOCALAPPDATA%/Temp/Rush Ahead` is cleared). For a host and a joiner,
  add `-force-d3d11` to both.
- The tour saves best times like any race. Settings > Reset best times clears them.
