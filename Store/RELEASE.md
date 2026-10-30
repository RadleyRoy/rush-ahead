# Releasing Rush Ahead on Google Play

The project builds a store-ready Android App Bundle. These are the steps only you can do (accounts, payment, keys,
forms), in order. Store text and form answers are in `LISTING.md`; the privacy policy is `PRIVACY.md`; graphics
are in `Graphics/`.

## 1. The package name

The game is `com.rushahead.game` (`ProjectBootstrap.BundleId`), chosen for good: once the first bundle is
uploaded, Google Play never lets it change.

## 2. Make your upload key (once)

Google signs the copy players download; you sign what you upload with your own *upload key*. Make it with the
`keytool` that came with Unity. It asks for a password and your name. Keep the file and the password somewhere
safe and backed up, and never put either in the project (`.gitignore` already ignores `*.keystore`).

```powershell
& "D:\Unity\Editors\6000.3.27f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore "$HOME\rushahead-upload.keystore" -alias upload -keyalg RSA -keysize 2048 -validity 10000
```

If you lose it, Google can reset it (Play Console > Setup > App signing), but it takes a few days.

## 3. Build the bundle

Tell the build where the key is, for this terminal only, then build:

```powershell
$env:RUSHAHEAD_KEYSTORE = "$HOME\rushahead-upload.keystore"
$env:RUSHAHEAD_KEY_ALIAS = "upload"
$env:RUSHAHEAD_KEYSTORE_PASS = Read-Host "Keystore password"
$env:RUSHAHEAD_KEY_PASS = Read-Host "Key password"
& "D:\Unity\Editors\6000.3.27f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\Projects\Rush Ahead" -buildTarget Android -executeMethod RushAhead.EditorTools.BuildScript.AndroidRelease -logFile -
```

It writes `Builds/Android/RushAhead-1.0.0-<code>.aab` (and a `.symbols.zip` beside it). The version name is
`BuildScript.Version`; the version code is the number of commits, so commit before each upload (Play refuses a code
it has seen). Without the key variables it still builds, but signs with Unity's debug key and names the file
`NOT-FOR-UPLOAD`.

## 4. Publish the privacy policy

Play needs a public web address for `PRIVACY.md`. Any of these work: GitHub Pages, a Google Sites page, or a page on
your own site. Put your contact email in it first (it says `[your contact email]`).

## 5. Play Console

1. Create a developer account at https://play.google.com/console (a one-off $25 fee, and an identity check that can
   take a few days).
2. **Create app**: name Rush Ahead, default language, Game, Free.
3. **App content**: privacy policy URL, ads (none), app access (no login), target audience, content rating
   questionnaire and Data safety, using `LISTING.md`.
4. **Main store listing**: the text from `LISTING.md`, the icon, feature graphic and screenshots from `Graphics/`.
5. **Testing > Internal testing**: create a release, upload the `.aab` (and the symbols zip under the bundle's
   *Native debug symbols*), add yourself and friends as testers, roll out, and install from the link on real phones.
6. **Closed testing**: personal developer accounts need a closed test with at least 12 testers who stay opted in
   for 14 days before Google allows production. Start it as early as you can.
7. **Production**: apply for access when the closed test is done, then promote the tested release.

## Before each release

- Play it on at least two real phones, together on Wi-Fi and online.
- Bump `BuildScript.Version` (players see it) and commit.
- Check the pre-launch report in the Play Console after uploading: it runs the game on Google's test phones.
