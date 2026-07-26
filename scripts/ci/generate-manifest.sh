#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
platform="${1:-}"; build_dir="${2:-}"; package="${3:-}"; output="${4:-}"
[[ -d "$build_dir" && -f "$package" ]] || die 'Build directory or package is missing for manifest generation.'
require_command jq
sha="$(sha256sum "$package" | awk '{print $1}')"
uncompressed="$(du -sb "$build_dir" | awk '{print $1}')"
packaged="$(stat -c '%s' "$package")"
jq -n \
  --arg repository "${GITHUB_REPOSITORY:-local}" --arg product "${PRODUCT_NAME:-Project-GMTK-2026}" \
  --arg unityVersion "$(unity_version)" --arg platform "$platform" --arg releaseVersion "${RELEASE_VERSION:?RELEASE_VERSION required}" \
  --arg releaseType "${RELEASE_TYPE:-staging}" --arg ref "${SOURCE_REF:-${GITHUB_REF:-local}}" \
  --arg commit "${SOURCE_SHA:-$(git rev-parse HEAD)}" --arg shortCommit "${SOURCE_SHORT_SHA:-$(git rev-parse --short=8 HEAD)}" \
  --arg runId "${GITHUB_RUN_ID:-local}" --arg runAttempt "${GITHUB_RUN_ATTEMPT:-1}" \
  --arg buildTime "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --argjson developmentBuild "${DEVELOPMENT_BUILD:-false}" \
  --arg artifactFilename "$(basename "$package")" --argjson uncompressedSize "$uncompressed" --argjson packagedSize "$packaged" --arg checksum "$sha" \
  '{schemaVersion:1,project:$repository,productName:$product,unityVersion:$unityVersion,targetPlatform:$platform,releaseVersion:$releaseVersion,releaseType:$releaseType,sourceRef:$ref,commitSha:$commit,shortCommitSha:$shortCommit,githubRunId:$runId,githubRunAttempt:$runAttempt,buildTimeUtc:$buildTime,developmentBuild:$developmentBuild,artifactFilename:$artifactFilename,uncompressedSizeBytes:$uncompressedSize,packagedSizeBytes:$packagedSize,sha256:$checksum}' > "$output"
jq empty "$output"
note "Generated $output"
