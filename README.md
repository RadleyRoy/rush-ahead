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
- The tour saves best times like any race. Settings > Reset best times clears them.
