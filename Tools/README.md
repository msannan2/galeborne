# Tools/ - build helpers (Game Researcher)

Kept in the repo so a box disk rollback can't wipe them. Installed copies live in
`/usr/local/lib/galeborne-tools/` (outside /workspace and /tmp). `/tmp/gr_guard.sh` and `/tmp/wm.py` are
symlinks to them, so teammates' existing calls keep working. To reinstall from any clone after a rollback:

    sudo install -d /usr/local/lib/galeborne-tools
    sudo install -m 755 Tools/{gr_guard.sh,wm.py,wm_check.py,apk_go.sh,_unitypy.py} /usr/local/lib/galeborne-tools/
    ln -sfn /usr/local/lib/galeborne-tools/gr_guard.sh /tmp/gr_guard.sh
    ln -sfn /usr/local/lib/galeborne-tools/wm.py /tmp/wm.py

(The installed `apk_go.sh` still builds from `/workspace/galeborne-repo2`; override with `GR_REPO=`.)

| File | What it does |
|---|---|
| `gr_guard.sh` | Global "one Unity editor or Galeborne game at a time" lock (`flock` on `/tmp/gr_unity.lock`). `gr_guard.sh <cmd...>` waits for the lock, then waits while any Unity/Galeborne process it doesn't own is running, then runs `<cmd>` and returns its exit code. It can also be sourced (old convention): `source /tmp/gr_guard.sh; while busy; do sleep 30; done`, or `gr_acquire` ... `gr_release`. It never kills anything. |
| `apk_go.sh` | `apk_go.sh [--dry-run] [--out FILE] <commit> <ver> <code>`: fresh worktree `/workspace/gr_wt_apk2` from `/workspace/galeborne-repo2`, `git lfs pull`, refuses on LFS pointers or an existing output (default `/workspace/Galeborne_v<ver>_mq.apk`), builds `Builder.BuildAndroidCli` under the lock with `timeout 20m` on its own Xvfb (:61, else :98), then prints aapt2 versionCode/versionName, `apksigner verify`, size, sha256, watermark A (Unity entitlements audit log) and B (`wm_check.py`). Fails under 35 MB (no art). A failed APK becomes `<out>.rejected`. Removes the worktree afterwards; Library is cached in `/workspace/gr_apk_library_cache`. Unity rc 141 = SIGPIPE, checked manually. |
| `wm.py` | Watermark check B for a Linux build: `wm.py <Build>/Galeborne_Data/globalgamemanagers` (or the build folder). Prints `1` and exits 0 when there's no watermark. |
| `wm_check.py` | The same check for an APK (reads BuildSettings in `data.unity3d`): `wm_check.py <file.apk>`. |
| `_unitypy.py` | Shared helper. It uses UnityPy, and re-runs itself under `/workspace/gr_venv` or `/tmp/wm_venv` when the system python lacks UnityPy (`pip install UnityPy`). |
| `build_apk.sh` | A copy of the recovery `build_apk.sh` (single Unity run; `<ver> <code> [--project] [--out] [--linux]`), kept for reference. |

Requirements: `git-lfs` installed system-wide (`sudo apt-get install git-lfs && git lfs install`).
Unity 6000.3.26f1 is at `/home/box/Unity/Hub/Editor/6000.3.26f1`. aapt2 and apksigner come from its SDK `build-tools/36.0.0`.
Signing uses the Unity debug keystore `~/.android/debug.keystore` (Builder sets `useCustomKeystore = false`).
