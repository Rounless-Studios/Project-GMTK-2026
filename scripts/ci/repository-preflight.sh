#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
root="$(repo_root)"
cd "$root"

require_command git
require_command git-lfs
[[ -f ProjectSettings/ProjectVersion.txt ]] || die 'ProjectSettings/ProjectVersion.txt is missing.'
[[ -f Packages/manifest.json && -f Packages/packages-lock.json ]] || die 'Unity package manifest or lock file is missing.'
[[ -f ProjectSettings/EditorBuildSettings.asset ]] || die 'EditorBuildSettings.asset is missing.'
version="$(unity_version)"
[[ "$version" == '6000.3.19f1' ]] || die "Expected Unity 6000.3.19f1; repository requests '$version'. Never silently change this version."

git lfs pull
git lfs ls-files
bash "$root/scripts/ci/validate-lfs.sh" Assets

if [[ -n "$(git ls-files -i --exclude-standard -- Library Temp Logs Build Builds 2>/dev/null)" ]]; then
  die 'Generated Unity or build output is tracked by Git.'
fi

note "Repository preflight passed for Unity $version."
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo '### Repository preflight'
    echo "- Unity version: \`$version\`"
    echo '- Git LFS objects pulled and pointer scan passed.'
    echo '- Required Unity project files are present.'
  } >> "$GITHUB_STEP_SUMMARY"
fi
