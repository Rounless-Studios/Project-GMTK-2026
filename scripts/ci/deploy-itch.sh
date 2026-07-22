#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
directory="${1:-}"; channel="${2:-}"; version="${3:-}"
[[ -d "$directory" ]] || die "Deployment directory is missing: $directory"
[[ "$channel" =~ ^(windows|html5)(-staging)?$ ]] || die "Invalid itch channel: $channel"
[[ -n "$version" ]] || die 'Deployment version is empty.'
[[ -n "${BUTLER_API_KEY:-}" ]] || die 'BUTLER_API_KEY is not configured in the selected GitHub environment.'
[[ "${ITCH_PROJECT:-}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9][A-Za-z0-9._-]*$ ]] || die 'ITCH_PROJECT must match username/project-slug.'
require_command butler
butler version
note "Deploying $directory to ${ITCH_PROJECT}:$channel as $version (source ${SOURCE_SHA:-unknown})."
butler push "$directory" "${ITCH_PROJECT}:$channel" --userversion "$version"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  printf -- '- itch destination: `%s:%s`\n- Version: `%s`\n- Source: `%s`\n- Run: `%s/actions/runs/%s`\n' "$ITCH_PROJECT" "$channel" "$version" "${SOURCE_SHA:-unknown}" "${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:-}" "${GITHUB_RUN_ID:-}" >> "$GITHUB_STEP_SUMMARY"
fi
