#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
platform="${1:-}"
build_dir="${2:-}"
zip_path="$(realpath -m "${3:-}")"
bash "$(dirname "$0")/validate-output.sh" "$platform" "$build_dir"
rm -f "$zip_path"
mkdir -p "$(dirname "$zip_path")"
(cd "$build_dir" && zip -q -r "$zip_path" . -x '*_BackUpThisFolder_ButDontShipItWithYourGame/*' '*_BurstDebugInformation_DoNotShip/*' '*.pdb' '*.debug')
unzip -t "$zip_path" >/dev/null || die "ZIP integrity check failed: $zip_path"
if [[ "$platform" == 'WebGL' ]]; then unzip -Z1 "$zip_path" | grep -Eq '^\./?index\.html$' || die 'WebGL ZIP lacks root index.html.'; fi
if [[ "$platform" == 'StandaloneWindows64' ]]; then unzip -Z1 "$zip_path" | grep -Eiq '^[^/]+\.exe$' || die 'Windows ZIP lacks a root executable.'; fi
note "Packaged $zip_path"
