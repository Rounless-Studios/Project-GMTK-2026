#!/usr/bin/env bash
set -Eeuo pipefail

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
note() { printf '%s\n' "$*"; }
require_command() { command -v "$1" >/dev/null 2>&1 || die "Required command not found: $1"; }

repo_root() {
  git rev-parse --show-toplevel 2>/dev/null || die 'Run this script inside the repository.'
}

unity_version() {
  sed -n 's/^m_EditorVersion: //p' "$(repo_root)/ProjectSettings/ProjectVersion.txt" | head -n1
}

validate_relative_path() {
  local value="$1"
  [[ -n "$value" ]] || die 'Path must not be empty.'
  [[ "$value" != /* && "$value" != *'..'* && "$value" != *\\* ]] || die "Unsafe relative path: $value"
}
