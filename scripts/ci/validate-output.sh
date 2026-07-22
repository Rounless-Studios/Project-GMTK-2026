#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
platform="${1:-}"
directory="${2:-}"
[[ "$platform" == 'StandaloneWindows64' || "$platform" == 'WebGL' ]] || die 'Platform must be StandaloneWindows64 or WebGL.'
[[ -n "$directory" && -d "$directory" ]] || die "Build directory is missing: $directory"

if find "$directory" -type d \( -name '*_BackUpThisFolder_ButDontShipItWithYourGame' -o -name '*_BurstDebugInformation_DoNotShip' \) -print -quit | grep -q .; then
  die 'Build contains a Unity backup/debug folder that must not ship.'
fi

if [[ "$platform" == 'StandaloneWindows64' ]]; then
  mapfile -t executables < <(find "$directory" -maxdepth 1 -type f -iname '*.exe')
  (( ${#executables[@]} == 1 )) || die "Expected exactly one Windows executable at the build root; found ${#executables[@]}."
  [[ -s "${executables[0]}" ]] || die 'Windows executable is empty.'
  stem="$(basename "${executables[0]}" .exe)"
  [[ -d "$directory/${stem}_Data" ]] || die "Missing corresponding ${stem}_Data directory."
  find "$directory" -maxdepth 1 -type f \( -iname 'UnityPlayer.dll' -o -iname 'GameAssembly.dll' \) -print -quit | grep -q . || die 'Expected Unity runtime DLL is missing.'
else
  [[ -s "$directory/index.html" ]] || die 'WebGL index.html is missing or empty.'
  [[ -d "$directory/Build" ]] || die 'WebGL Build directory is missing.'
  find "$directory/Build" -type f -iname '*.loader.js' -print -quit | grep -q . || die 'Unity WebGL loader is missing.'
  find "$directory/Build" -type f \( -iname '*.wasm' -o -iname '*.wasm.gz' -o -iname '*.wasm.br' \) -print -quit | grep -q . || die 'Unity WebAssembly output is missing.'
  find "$directory/Build" -type f \( -iname '*.data' -o -iname '*.data.gz' -o -iname '*.data.br' \) -print -quit | grep -q . || die 'Unity WebGL data output is missing.'
  if grep -RIEq '([A-Za-z]:\\\\|/Users/|/home/runner/|/github/workspace/)' "$directory/index.html" "$directory/Build" --include='*.js' --include='*.html'; then
    die 'WebGL output contains an obvious absolute local filesystem path.'
  fi
  python3 "$(dirname "$0")/validate-webgl.py" "$directory"
fi
note "$platform output validation passed: $directory"
