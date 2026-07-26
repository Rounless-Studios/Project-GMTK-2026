#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
install_dir="${1:-$RUNNER_TEMP/butler}"
mkdir -p "$install_dir"
curl --fail --show-error --silent --location 'https://broth.itch.ovh/butler/linux-amd64/LATEST/archive/default' -o "$install_dir/butler.zip"
unzip -q -o "$install_dir/butler.zip" -d "$install_dir"
chmod +x "$install_dir/butler"
echo "$install_dir" >> "${GITHUB_PATH:?GITHUB_PATH is required}"
version="$($install_dir/butler version)"
note "$version"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then echo "- Butler: \`$version\`" >> "$GITHUB_STEP_SUMMARY"; fi
