#!/usr/bin/env bash
# COPY of /workspace/recovery/Game Researcher/build_apk.sh, kept here so a disk rollback cannot lose it.
# Tools/apk_go.sh calls Unity directly instead; note the final "aapt2 | head -1" can exit 141 (SIGPIPE) under pipefail.
# REWRITTEN: one Unity batch run per build; never overwrites; passes versionCode explicitly.
# usage: build_apk.sh <ver> <code> [--project DIR] [--out FILE] [--linux NAME]
set -euo pipefail
ver="$1"; code="$2"; shift 2
proj=/workspace/galeborne-unity; out="/workspace/Galeborne_v$ver.apk"; linux=""
while [ $# -gt 0 ]; do case "$1" in
  --project) proj="$2"; shift 2;; --out) out="$2"; shift 2;; --linux) linux="$2"; shift 2;;
  *) echo "unknown $1"; exit 1;; esac; done
[ -e "$out" ] && { echo "refusing: $out exists"; exit 1; }
UNITY="${UNITY:-$(ls -d /workspace/unitydl/*/Editor/Unity 2>/dev/null | head -1)}"
: "${DISPLAY:=:61}"; case "$DISPLAY" in :5|:97) echo "display $DISPLAY not allowed"; exit 1;; esac
if [ -n "$linux" ]; then method=Builder.BuildLinuxCli; extra=(-gbOut "$proj/Builds/$linux/Galeborne.x86_64")
else method=Builder.BuildAndroidCli; extra=(-gbOut "$out"); fi
DISPLAY=$DISPLAY "$UNITY" -batchmode -quit -projectPath "$proj" -executeMethod "$method" \
  -gbVersionCode "$code" -gbVersion "$ver" "${extra[@]}" -logFile "/workspace/build_$ver.log"
[ -z "$linux" ] && aapt2 dump badging "$out" | head -1
# Matches live Builder.cs: BuildAndroidCli / BuildLinuxCli, -gbVersion, -gbVersionCode, -gbOut.
