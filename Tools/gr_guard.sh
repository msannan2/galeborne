#!/usr/bin/env bash
# gr_guard.sh - shared "one Unity editor or one Galeborne game at a time" lock for the box.
#
# Two ways to use it:
#   1. Run a command under the lock (preferred):
#        Tools/gr_guard.sh <cmd> [args...]
#      Waits for the global flock on /tmp/gr_unity.lock, then waits while any Unity editor /
#      Galeborne player that this guard doesn't own is running, then runs <cmd> and exits with
#      its exit code. The lock is released when <cmd> exits.
#   2. Source it (the old /tmp/gr_guard.sh convention, used by gr_apk_go.sh):
#        source /tmp/gr_guard.sh
#        while busy; do sleep 30; done      # old style: only waits for foreign processes
#        gr_acquire                         # new: take the flock on fd 9 and wait until not busy
#        ... run Unity with 9>&- so children don't inherit the lock ...
#        gr_release
#
# Nested use is safe: the command run by mode 1 gets GR_GUARD_HELD=1, so a gr_acquire inside it
# (e.g. apk_go.sh run via gr_guard.sh) doesn't wait on its own lock.
# It never kills anything. Env: GR_GUARD_LOCK (default /tmp/gr_unity.lock),
# GR_GUARD_POLL seconds between busy checks (default 30), GR_GUARD_QUIET=1 for less output.

GR_GUARD_LOCK="${GR_GUARD_LOCK:-/tmp/gr_unity.lock}"
GR_GUARD_POLL="${GR_GUARD_POLL:-30}"
GR_GUARD_OWNER="${GR_GUARD_OWNER:-$$}"

_gr_log() { [ "${GR_GUARD_QUIET:-0}" = 1 ] || echo "gr_guard $(date '+%T %Z'): $*" >&2; }

# pids that are "ours": the owner, its descendants and this shell's ancestors.
_gr_own_pids() {
  local p=$BASHPID
  while [ -n "$p" ] && [ "$p" -gt 1 ]; do
    echo "$p"; p=$(ps -o ppid= -p "$p" 2>/dev/null | tr -d ' ')
  done
  # descendants of the owner (breadth-first over ppid table)
  ps -eo pid=,ppid= | awk -v root="$GR_GUARD_OWNER" '
    { kids[$2] = kids[$2] " " $1 }
    END { q[1] = root; n = 1; i = 1
          while (i <= n) { c = q[i++]; print c; m = split(kids[c], a, " ")
                           for (j = 1; j <= m; j++) q[++n] = a[j] } }'
}

# Lists foreign Unity / Galeborne processes as "pid comm". Matches on the process name
# (comm), not the command line, so a shell or grep that merely mentions "Unity" never counts.
# Unity.Licensing.Client is ignored: it can linger for minutes after an editor quits.
gr_busy_list() {
  local own; own=" $(_gr_own_pids | tr '\n' ' ') "
  ps -eo pid=,comm= | while read -r pid comm; do
    case "$comm" in
      Unity|Unity-*|UnityShaderComp*|UnityPackageMan*|bee_backend|Galeborne*) ;;
      *) continue ;;
    esac
    case "$own" in *" $pid "*) continue ;; esac
    echo "$pid $comm"
  done
}

# Old API: true (0) while a foreign Unity/Galeborne process is running.
busy() { [ -n "$(gr_busy_list)" ]; }

# Take the global lock on fd 9 (waiting), then wait until nothing foreign is running.
gr_acquire() {
  if [ "${GR_GUARD_HELD:-}" = 1 ]; then _gr_log "lock already held by a parent gr_guard"; return 0; fi
  exec 9>>"$GR_GUARD_LOCK" || return 1
  chmod 666 "$GR_GUARD_LOCK" 2>/dev/null || true
  if ! flock -n 9; then
    _gr_log "waiting for $GR_GUARD_LOCK (held by another build/game)"
    flock 9 || return 1
  fi
  local l
  while l=$(gr_busy_list) && [ -n "$l" ]; do
    _gr_log "lock held, but Unity/game busy: $(echo "$l" | tr '\n' ' ')- waiting ${GR_GUARD_POLL}s"
    sleep "$GR_GUARD_POLL"
  done
  _gr_log "acquired $GR_GUARD_LOCK (pid $$)"
}

gr_release() { [ "${GR_GUARD_HELD:-}" = 1 ] && return 0; flock -u 9 2>/dev/null; exec 9>&- 2>/dev/null; _gr_log "released $GR_GUARD_LOCK"; }

# Executed (not sourced): run the given command under the lock.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  if [ $# -eq 0 ] || [ "$1" = -h ] || [ "$1" = --help ]; then
    sed -n '2,20p' "$0"; exit 2
  fi
  gr_acquire || { echo "gr_guard: could not lock $GR_GUARD_LOCK" >&2; exit 1; }
  # 9>&- : the command (and anything it leaves behind) must not inherit the lock fd.
  GR_GUARD_HELD=1 "$@" 9>&-
  rc=$?
  gr_release
  exit $rc
fi
