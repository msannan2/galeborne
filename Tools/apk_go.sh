#!/usr/bin/env bash
# apk_go.sh [--dry-run] [--out FILE] <commit> <ver> <code>
# Builds a debug-signed Galeborne APK from <commit> in a fresh git worktree and verifies it.
#   - worktree /workspace/gr_wt_apk2 from /workspace/galeborne-repo2, git lfs pull, refuses on LFS pointers
#   - refuses if the output (default /workspace/Galeborne_v<ver>_mq.apk) already exists
#   - builds under Tools/gr_guard.sh (global one-Unity lock), timeout 20m, own Xvfb on :61 or :98
#   - Builder.BuildAndroidCli with -gbVersion/-gbVersionCode/-gbOut (Unity debug keystore signing)
#   - prints aapt2 versionCode/versionName, apksigner verify, size, sha256, watermark checks A and B
#   - fails if the APK is under 35 MB (no-art guard); a failed APK is renamed to <out>.rejected
#   - removes the worktree afterwards (Library is kept in /workspace/gr_apk_library_cache for speed)
# --verify FILE <ver> <code> only runs the checks on an existing APK (never renames it); watermark A
#   then looks at the 30 minutes before the file's mtime.
# --dry-run does everything up to the Unity build (worktree, LFS check, tools, lock check) and stops.
set -euo pipefail
TOOLS=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
REPO=${GR_REPO:-/workspace/galeborne-repo2}
WT=${GR_WT:-/workspace/gr_wt_apk2}
LIBCACHE=/workspace/gr_apk_library_cache
E=/home/box/Unity/Hub/Editor/6000.3.26f1/Editor
UNITY=$E/Unity
AP=$E/Data/PlaybackEngines/AndroidPlayer
BT=$AP/SDK/build-tools/36.0.0
AUDIT=$HOME/.config/unity3d/Unity/Unity.Entitlements.Audit.log
MIN_BYTES=35000000

dry=0; out=""; verify=""
while [ $# -gt 0 ]; do case "$1" in
  --dry-run) dry=1; shift;;
  --out) out="$2"; shift 2;;
  --verify) verify="$2"; shift 2;;
  -h|--help) sed -n '2,13p' "$0"; exit 0;;
  --) shift; break;;
  -*) echo "unknown option $1"; exit 2;;
  *) break;; esac; done
if [ -n "$verify" ]; then [ $# -eq 2 ] && set -- verify "$1" "$2"; fi
[ $# -eq 3 ] || { sed -n '2,3p' "$0"; exit 2; }
commit=$1; ver=$2; code=$3
[[ $code =~ ^[0-9]+$ ]] || { echo "versionCode must be a number: $code"; exit 2; }
out=${out:-/workspace/Galeborne_v${ver}_mq.apk}
log=/workspace/build_apk_gr_${ver}.log
export JAVA_HOME=$AP/OpenJDK
export PATH=$BT:$AP/OpenJDK/bin:$PATH
ts() { date '+%F %T %Z'; }

if [ -z "$verify" ]; then
[ -e "$out" ] && { echo "refusing: $out exists"; exit 1; }
[ -e "$out.rejected" ] && { echo "refusing: $out.rejected exists (look at it, then move it away)"; exit 1; }
for t in "$UNITY" "$BT/aapt2" "$BT/apksigner" "$JAVA_HOME/bin/java"; do [ -x "$t" ] || { echo "missing tool: $t"; exit 1; }; done
git lfs version >/dev/null 2>&1 || { echo "git-lfs not installed (sudo apt-get install git-lfs && git lfs install)"; exit 1; }
[ -d "$REPO/.git" ] || { echo "no repo at $REPO (git clone https://github.com/msannan2/galeborne.git $REPO)"; exit 1; }

XPID=""; MADE_WT=0
cleanup() {
  if [ -n "$XPID" ]; then kill "$XPID" 2>/dev/null || true; fi   # only our own Xvfb
  if [ "$MADE_WT" = 1 ] && [ -d "$WT" ]; then
    if [ -d "$WT/Library" ]; then rm -rf "$LIBCACHE.old"; [ -d "$LIBCACHE" ] && mv "$LIBCACHE" "$LIBCACHE.old"; mv "$WT/Library" "$LIBCACHE" && rm -rf "$LIBCACHE.old"; fi
    git -C "$REPO" worktree remove --force "$WT" && echo "worktree $WT removed" || echo "WARNING: could not remove worktree $WT"
    git -C "$REPO" worktree prune
  fi
}
trap cleanup EXIT

fi
if [ -n "$verify" ]; then
  out=$verify; [ -f "$out" ] || { echo "no file $out"; exit 1; }
  m=$(stat -c %Y "$out"); T0=$(date -u -d "@$((m-1800))" '+%F %T'); T1=$(date -u -d "@$((m+60))" '+%F %T')
  echo "verify-only $out (audit window $T0 .. $T1 UTC)"
else
# --- worktree at the commit ---
if [ -e "$WT" ]; then
  if git -C "$REPO" worktree list --porcelain | grep -qx "worktree $WT"; then
    echo "removing stale worktree $WT (ours)"; git -C "$REPO" worktree remove --force "$WT"
  else echo "refusing: $WT exists and is not a worktree of $REPO"; exit 1; fi
fi
git -C "$REPO" fetch -q origin
sha=$(git -C "$REPO" rev-parse --verify -q "$commit^{commit}") || { echo "unknown commit $commit (pushed to GitHub?)"; exit 1; }
git -C "$REPO" worktree add -q --detach "$WT" "$sha"; MADE_WT=1
cd "$WT"
git lfs pull
pointers=$(git lfs ls-files | awk '$2 == "-"' | wc -l)
rgp=$(rg -l -uu --max-count 1 -g '!Library' '^version https://git-lfs.github.com/spec' Assets 2>/dev/null | wc -l || true)
if [ "$pointers" != 0 ] || [ "$rgp" != 0 ]; then
  echo "refusing: LFS pointer files remain (git lfs: $pointers, scan: $rgp)"; git lfs ls-files | awk '$2 == "-"' | head; exit 1; fi
echo "HEAD $(git rev-parse --short HEAD) $(git log -1 --format=%s | cut -c1-100)"
echo "LFS ok: $(git lfs ls-files | wc -l) files, 0 pointers"
[ -d "$LIBCACHE" ] && { mv "$LIBCACHE" "$WT/Library"; echo "using cached Library"; }

D=""; for d in 61 98; do [ -e /tmp/.X11-unix/X$d ] || [ -e /tmp/.X$d-lock ] || { D=$d; break; }; done
[ -n "$D" ] || { echo "displays :61 and :98 both taken"; exit 1; }
cmd=("$UNITY" -batchmode -quit -projectPath "$WT" -executeMethod Builder.BuildAndroidCli
     -gbVersion "$ver" -gbVersionCode "$code" -gbOut "$out" -logFile "$log")
if [ $dry = 1 ]; then
  # shellcheck source=Tools/gr_guard.sh
  GR_GUARD_QUIET=1 source "$TOOLS/gr_guard.sh"
  if flock -n "$GR_GUARD_LOCK" true; then echo "lock $GR_GUARD_LOCK: free"; else echo "lock $GR_GUARD_LOCK: held (a real run would wait)"; fi
  bl=$(gr_busy_list); echo "foreign Unity/game processes: ${bl:-none}"
  echo "aapt2: $("$BT/aapt2" version 2>&1)"; echo "apksigner: $("$BT/apksigner" --version 2>&1)"
  echo "DRY RUN: would run on :$D under gr_guard + timeout 20m:"; printf ' %q' "${cmd[@]}"; echo
  echo "DRY RUN: output $out; stopping before the build"; exit 0
fi

# --- build under the global lock ---
# shellcheck source=Tools/gr_guard.sh
source "$TOOLS/gr_guard.sh"
gr_acquire
Xvfb ":$D" -screen 0 1280x720x24 >"/tmp/gr_xvfb$D.log" 2>&1 9>&- & XPID=$!
sleep 2
T0=$(date -u '+%F %T'); t0=$(date +%s); echo "build start $(ts) on :$D, log $log"
rc=0; DISPLAY=":$D" timeout 20m "${cmd[@]}" 9>&- || rc=$?
T1=$(date -u '+%F %T'); echo "build end $(ts) ($(( $(date +%s) - t0 ))s) unity rc=$rc"
kill "$XPID" 2>/dev/null || true; XPID=""
gr_release
case $rc in
  0) ;;
  141) echo "rc=141 is SIGPIPE: verifying the APK manually";;
  124) echo "BUILD TIMED OUT (20m)"; exit 1;;
  *) echo "BUILD FAILED rc=$rc"; rg -N 'error CS|GB CLI|Error building|Build Finished' "$log" | tail -5 || true; exit 1;;
esac
[ -f "$out" ] || { echo "no APK at $out"; rg -N 'error CS|GB CLI|Error building' "$log" | tail -5 || true; exit 1; }
fi

# --- verify ---
fail=0
badge=$("$BT/aapt2" dump badging "$out" 2>/dev/null | sed -n 1p || true)
vc=$(grep -o "versionCode='[0-9]*'" <<<"$badge" | tr -dc 0-9); vn=$(grep -o "versionName='[^']*'" <<<"$badge" | cut -d"'" -f2)
echo "aapt2: versionCode=$vc versionName=$vn"
[ "$vc" = "$code" ] || { echo "FAIL: versionCode $vc != $code"; fail=1; }
[ "$vn" = "$ver" ] || { echo "FAIL: versionName $vn != $ver"; fail=1; }
if sv=$("$BT/apksigner" verify --print-certs "$out" 2>&1); then
  echo "apksigner: verified; $(grep -m1 'certificate DN' <<<"$sv")"; grep -m1 'certificate SHA-256' <<<"$sv" || true
else echo "FAIL: apksigner verify: $sv"; fail=1; fi
size=$(stat -c%s "$out"); echo "size $size bytes"
[ "$size" -ge $MIN_BYTES ] || { echo "FAIL: APK under 35 MB - art probably missing"; fail=1; }
echo "sha256 $(sha256sum "$out" | cut -d' ' -f1)"
# A: entitlements granted during this build (audit log timestamps are UTC)
win=$(awk -v a="$T0" -v b="$T1" 'substr($0,1,19) >= a && substr($0,1,19) <= b' "$AUDIT" 2>/dev/null || true)
okA=1
for ent in com.unity.editor.headless com.unity.editor.platforms.android com.unity.editor.watermarks.disable; do
  g=$(grep -F "[$ent]" <<<"$win" | tail -1 | grep -o 'granted: [A-Za-z]*' || true)
  echo "watermark A: $ent ${g:-not requested}"; [ "$g" = "granted: True" ] || okA=0
done
grep -E 'watermarks\.(trial|edu|prototyping)\], Result: \[granted: True' <<<"$win" >/dev/null && { echo "watermark A: a watermark entitlement was granted"; okA=0; }
if [ $okA = 1 ]; then echo "watermark A: PASS"; else echo "FAIL: watermark A"; fail=1; fi
echo -n "watermark B: "; python3 "$TOOLS/wm_check.py" "$out" || { echo "FAIL: watermark B"; fail=1; }
if [ $fail = 1 ] && [ -n "$verify" ]; then echo "VERIFY FAILED (file left untouched)"; exit 1; fi
if [ $fail = 1 ]; then mv "$out" "$out.rejected"; echo "APK REJECTED -> $out.rejected"; exit 1; fi
echo "OK $out"
