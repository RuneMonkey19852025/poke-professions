#!/usr/bin/env bash
# Emberwild Vale launcher (standalone Vulkan build, windowed for Wayland stability)
#
# IMPORTANT: the game must be started in the FOREGROUND (blocking) via
# systemd-run. It must never be a child of a short-lived shell — when that
# shell is reaped, its cgroup is torn down and the game is silently
# SIGKILLed after boot. A foreground systemd-run hands ownership to the
# user's systemd manager, which fully detaches it from the caller.
#
# Usage:
#   ./run-game.sh                 # foreground launch (script blocks; Ctrl-C stops it)
#   systemctl --user stop emberwild-game   # stop a running instance
#   systemctl --user status emberwild-game # check on it
DIR="$(cd "$(dirname "$0")" && pwd)"

# ALSA must be pointed at a null device or the player hangs on startup exactly
# like the editor does: FMOD calls snd_pcm_prepare() on the PipeWire "default"
# PCM while initialising the AudioManager, that call never returns on this
# machine, and the game freezes with no window and a log that stops dead after
# "Vulkan PSO: Cache data successfully loaded". See Tools/alsa-null.conf.
#
# Cost: the game runs silent — but it already does, since this host's only
# PipeWire sink is a Dummy Output. Nothing outside the game process is touched.
ALSA_CONF="$DIR/Tools/alsa-null.conf"

if [ ! -f "$ALSA_CONF" ]; then
  echo "error: missing $ALSA_CONF — without it the game will hang during audio init." >&2
  exit 1
fi

if [ "${EMBERWILD_LAUNCHED:-}" = "1" ]; then
  export ALSA_CONFIG_PATH="$ALSA_CONF"
  exec "$DIR/Builds/Linux/EmberwildVale.x86_64" \
    -screen-fullscreen 0 -screen-width 1600 -screen-height 900 "$@"
fi

if command -v systemd-run >/dev/null 2>&1 \
   && systemctl --user is-system-running >/dev/null 2>&1; then
  # systemd-run would fail with "unit already exists", and a second copy of the
  # game would fight over the save file and the Vulkan PSO cache
  if systemctl --user is-active --quiet emberwild-game 2>/dev/null; then
    echo "Emberwild Vale is already running — stop it with: systemctl --user stop emberwild-game"
    exit 0
  fi

  # units start in a different working dir and with a fresh environment — always
  # pass an absolute script path, and every variable the game needs
  SCRIPT="$(realpath "$0")"
  exec systemd-run --user --unit=emberwild-game \
    --setenv="DISPLAY=${DISPLAY:-:0}" \
    --setenv="WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-wayland-0}" \
    --setenv="XDG_RUNTIME_DIR=${XDG_RUNTIME_DIR:-/run/user/$(id -u)}" \
    --setenv="ALSA_CONFIG_PATH=$ALSA_CONF" \
    env EMBERWILD_LAUNCHED=1 bash "$SCRIPT" "$@"
fi

echo "error: systemd user session unavailable; refusing to run the game as a" >&2
echo "child of this shell (it would be killed when the shell exits)." >&2
echo "Start it via: systemd-run --user --unit=emberwild-game $0" >&2
exit 1
