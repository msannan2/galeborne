# Galeborne

Galeborne is a single-player trading and seafaring game for Android, built in Unity and headed for the Google Play Store. You captain a dhow between ports, buy low and sell high, handle events at sea (squalls, customs, pirates, rumours) and fight ship-to-ship and duel battles.

## Download

Every release has a signed APK attached. Get the newest one from the [Releases page](https://github.com/msannan2/galeborne/releases/latest) and install it on an Android phone (you'll need to allow installs from unknown sources).

## Project layout

- `Assets/Scripts/`: game code. `Game.cs` is the entry point, `GameCombat.cs` and `CombatCore.cs` run the fights, `GameVoyageEvents.cs` handles sea events, `GameSave.cs` handles saving and `GameQa.cs` runs the automated QA pass.
- `Assets/Editor/Builder.cs`: headless build entry points for Android and Linux (`Builder.BuildAndroidReleaseTo`).
- `Assets/Resources/`: data and art loaded at runtime, such as the events JSON, portraits and models.
- `Tools/balance-checks/`, `Tools/combat-sim/`: offline balance scripts and the combat simulator.
- `design/`: design notes and copy.
- `CREDITS.txt`: third-party assets and their licences (Kenney and KayKit, CC0).

## Building

- Unity editor version 6000.3.26f1 (see `ProjectSettings/ProjectVersion.txt`), with Android Build Support.
- The package id is `com.sannan.galeborne`.
- Builds run headless through the methods in `Assets/Editor/Builder.cs` using `-batchmode -executeMethod Builder.<Method>`. Builds use only the files committed to this repo.

## Debug flags

The game reads some command-line flags for testing. For example, `-gbforceevent <id>` forces a particular sea event. You can find the rest by searching the scripts for `-gb`.

## Releases

`main` always matches the most recent signed-off build. Each release gets a tag (`v0.6.1`, `v0.6.2`, `v0.6.3` and so on) with its APK and sha256 attached.
