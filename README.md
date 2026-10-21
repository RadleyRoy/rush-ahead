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
```
