# VR De-Addiction POC

A procedurally generated TASMAC-style Indian bar for alcohol cue-exposure
therapy, running standalone on a Meta Quest 3S at a locked 72 fps.

Read `PROJECT_STATE.md` first — it holds the constraints, the file map, and the
lessons that were learned the hard way. `SESSION_LOG.md` and `TODAY_AUG20.md`
are the narrative history.

---

## Restoring this project from scratch

The local working copy was deleted on 21 Aug 2026 after this repo was verified
as a byte-for-byte complete copy. Everything below is what it takes to get from
this repo back to an APK on a headset.

### 1. Clone — mind the host alias

```sh
git clone git@github-darshan:darshanscodesoftwares/VR-DeAddiction.git
```

**Not** `git@github.com:...`. This Mac has two GitHub accounts. The default SSH
key authenticates as `MASIHATHABASUM-SCODE`, so the plain github.com URL fails.
The `github-darshan` alias in `~/.ssh/config` points at the dedicated key
`~/.ssh/id_ed25519_darshanscode`, which is the one registered on
`darshanscodesoftwares`.

If `~/.ssh/config` is ever lost, recreate:

```
Host github-darshan
  HostName github.com
  User git
  IdentityFile ~/.ssh/id_ed25519_darshanscode
  IdentitiesOnly yes
```

Verify with `ssh -T git@github-darshan` — it must greet you as
`darshanscodesoftwares`.

### 2. Unity — the version is not negotiable

**Unity 6000.5.8f1**, with the **Android Build Support** module *and* its two
children, **OpenJDK** and **Android SDK & NDK Tools**.

Opening this project on any other Unity version silently upgrades it. There is
no second copy to fall back on, so do not let the Hub tempt you into a newer
release.

Install via Unity Hub → Installs → Install Editor → Archive. Roughly 14 GB.

### 3. Free up disk BEFORE opening it

The first open rebuilds `Library/` from scratch — about **5.5 GB** — and an
IL2CPP build needs several GB more on top. A build has already failed once on
this machine with `No space left on device`, and it reported itself only as
`Build Failed: 1 error(s)` with no compile error, which is very misleading.

**Have ~10 GB free before you start.** Check with `df -h`.

### 4. First open

Expect a long import: every asset is reprocessed and every package
re-downloaded. This is normal, not a fault.

Then run the one-time player/quality setup, which lives outside the build:

```sh
Unity -batchmode -quit -nographics -projectPath . \
  -executeMethod QuestProjectSetup.Configure
```

This writes MSAA, pixel-light count and the shadow settings into
`ProjectSettings/QualitySettings.asset`. `BuildAPK` does **not** call it.

### 5. Rebuild the scene, then build

The scene is generated. Objects are destroyed and recreated every time, so hand
edits do not survive.

```sh
# Unity must be CLOSED for both of these
Unity -batchmode -quit -nographics -projectPath . \
  -executeMethod HeadlessTasks.RebuildScene

# Run the APK build DETACHED -- tool timeouts kill it mid-build
nohup Unity -batchmode -quit -nographics -projectPath . \
  -executeMethod HeadlessTasks.BuildAPK -logFile build.log &
```

**Verify by the log line `[Headless] APK built`, never by an APK existing on
disk.** A stale file from an earlier run has twice been mistaken for success.
Equally, `Build Failed` is not the same string as `APK built` — do not grep for
a prefix that matches both.

### 6. Install to the headset

```sh
ADB=/Applications/Unity/Hub/Editor/6000.5.8f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB devices                     # if empty: $ADB kill-server && $ADB start-server
$ADB install -r Builds/VRDeAddiction.apk
$ADB shell am start -n com.scodevr.vrdeaddiction/com.unity3d.player.UnityPlayerGameActivity

# confirm it is really the new build, from the DEVICE, not from your disk
$ADB shell dumpsys package com.scodevr.vrdeaddiction | grep lastUpdateTime
```

`adb` ships inside Unity's Android module. Delete Unity and you lose `adb` too.

### 7. Blender assets (only if regenerating them)

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python BlenderAssets/scripts/build_all.py
```

Blender 5.2.0 LTS. The exported FBX files are committed, so this is only needed
if the assets themselves change.

---

## The last known-good build

`Builds/VRDeAddiction.apk`, built 2026-09-05 from commit `615a963` (53 MB,
sha256 `556c1836fd21…`). Sideload it with step 6; nothing needs rebuilding.

Check the hash before trusting a copy. An older `VRDeAddiction-2026-08-20-final.apk`
is still floating around in `~/Downloads/` and in chat history: despite the name it
predates the brick walls, the corrugated doors, the ceiling lamps and fans, the
exterior lights, all the yard planting, the 4K sky and the uneven ground. It is
34 MB against 53 MB, which is the quickest way to tell them apart.

Builds are DEBUG-SIGNED with a key that lives on the machine that built them, not
in this repo. A build made on a different machine therefore will not install over
one made here -- Android rejects the signature change. Uninstall the app from the
headset first, then install. Nothing else is affected.

## What is deliberately not in this repo

`Library/`, `Builds/`, `Logs/`, `UserSettings/`, Gradle and Ninja artifacts, and
Unity crash-recovery copies. All of it is regenerated from `Assets/`,
`Packages/` and `ProjectSettings/` — which is why this repo is ~8 MB and the
working folder was 6 GB.
