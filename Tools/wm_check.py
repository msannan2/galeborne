#!/usr/bin/env python3
"""Watermark check for an APK (restored from recovery/Game Researcher/wm_check.py).
usage: wm_check.py <file.apk>
UnityPy opens the APK and reads BuildSettings from assets/bin/Data/data.unity3d.
Prints 1 (exit 0) if isNoWatermarkBuild is True and isTrial is False, else 0 (exit 1).
Also accepts a globalgamemanagers file, like the recovery copy did."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.realpath(__file__)))
from _unitypy import check

if len(sys.argv) != 2:
    sys.exit(__doc__)
sys.exit(check(sys.argv[1], "apk:" + sys.argv[1]))
