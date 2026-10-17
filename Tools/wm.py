#!/usr/bin/env python3
"""Watermark check B for a Linux build (replaces /tmp/wm.py).
usage: wm.py <Build>/Galeborne_Data/globalgamemanagers   (or the build folder)
Prints 1 (exit 0) if the build has no watermark: BuildSettings.isNoWatermarkBuild True, isTrial False.
Prints 0 (exit 1) otherwise."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.realpath(__file__)))
from _unitypy import check

if len(sys.argv) != 2:
    sys.exit(__doc__)
p = sys.argv[1]
if os.path.isdir(p):
    p = os.path.join(p, "Galeborne_Data", "globalgamemanagers") if not p.endswith("_Data") else os.path.join(p, "globalgamemanagers")
sys.exit(check(p, "linux:" + p))
