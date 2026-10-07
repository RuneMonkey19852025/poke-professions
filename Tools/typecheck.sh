#!/usr/bin/env bash
# Offline typecheck: compiles all game scripts (excluding the Editor folder,
# which only runs inside Unity) against the real Unity managed DLLs using the
# Roslyn compiler bundled with the editor. Exits non-zero on compile errors.
set -euo pipefail

U="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.6.3f1}"
PROJ="$(cd "$(dirname "$0")/.." && pwd)"
M="$U/Editor/Data/Managed/UnityEngine"
NS="$U/Editor/Data/NetStandard/ref/2.1.0"
SA="$PROJ/Library/ScriptAssemblies"   # UnityEngine.UI.dll lives here after first editor import
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

[ -d "$M" ] || { echo "Unity editor not found at $U (set UNITY_EDITOR=...)"; exit 2; }

find "$PROJ/Assets/Scripts" -name "*.cs" ! -path "*Editor*" > "$OUT/srcs.txt"

REFS=(-r:"$NS/netstandard.dll")
for d in "$M"/*.dll; do REFS+=(-r:"$d"); done
REFS+=(-r:"$U/Editor/Data/Managed/UnityEditor.dll")
if [ -f "$SA/UnityEngine.UI.dll" ]; then REFS+=(-r:"$SA/UnityEngine.UI.dll"); fi

"$U/Editor/Data/NetCoreRuntime/dotnet" \
  "$U/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" \
  -nologo -noconfig -target:library \
  -nowarn:CS0169,CS0649,CS0414 \
  -out:"$OUT/game.dll" \
  "${REFS[@]}" \
  @"$OUT/srcs.txt"
