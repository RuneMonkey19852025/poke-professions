#!/usr/bin/env bash
# Emberwild Vale launcher (standalone Vulkan build, windowed for Wayland stability)
#
# If launched from a shell that will be torn down (agent tool calls, CI, etc.),
# the game would be silently SIGKILLed with that shell's cgroup. When running
# outside a systemd unit, re-exec through systemd-run so the game gets its own
# user unit and outlives the caller.
DIR="$(cd "$(dirname "$0")" && pwd)"

if [ -z "$INVOCATION_ID" ] && command -v systemd-run >/dev/null 2>&1 && systemctl --user is-active >/dev/null 2>&1; then
  exec systemd-run --user --unit=emberwild-game --collect \
    --setenv="DISPLAY=${DISPLAY:-}" \
    --setenv="WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-wayland-0}" \
    --setenv="XDG_RUNTIME_DIR=${XDG_RUNTIME_DIR:-$XDG_RUNTIME_DIR}" \
    bash "$0" "$@"
fi

exec "$DIR/Builds/Linux/EmberwildVale.x86_64" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 "$@"
