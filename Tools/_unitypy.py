"""Shared helper for wm.py / wm_check.py: import UnityPy, re-exec under a venv that has it if needed."""
import os, sys

VENVS = [os.environ.get("GR_UNITYPY_PY", ""), "/workspace/gr_venv/bin/python", "/tmp/wm_venv/bin/python"]

def load_unitypy():
    try:
        import UnityPy  # noqa: F401
        return UnityPy
    except ImportError:
        if os.environ.get("GR_WM_REEXEC"):
            sys.exit("UnityPy not importable; install it: python3 -m venv /workspace/gr_venv && /workspace/gr_venv/bin/pip install UnityPy")
        for py in VENVS:
            if py and os.access(py, os.X_OK):
                os.environ["GR_WM_REEXEC"] = "1"
                os.execv(py, [py] + sys.argv)
        sys.exit("UnityPy not found and no venv with it (tried %s). pip install UnityPy" % ", ".join(v for v in VENVS if v))

def check(path, label):
    """Prints '1 {...}' if isNoWatermarkBuild is True and isTrial is False, else '0 ...'. Returns exit code."""
    UnityPy = load_unitypy()
    if not os.path.exists(path):
        print(0, "missing:", path); return 2
    env = UnityPy.load(path)
    for o in env.objects:
        if o.type.name == "BuildSettings":
            t = o.read_typetree()
            ok = t.get("isNoWatermarkBuild") is True and t.get("isTrial") is False
            print(1 if ok else 0, {k: t.get(k) for k in ("isNoWatermarkBuild", "isTrial")}, label)
            return 0 if ok else 1
    print(0, "BuildSettings not found in", path); return 1
