#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
directory="${1:-}"
log_file="${2:-webgl-server.log}"
[[ -d "$directory" ]] || die "WebGL directory is missing: $directory"
require_command python3
require_command curl
port="$((18000 + RANDOM % 10000))"
python3 -m http.server "$port" --bind 127.0.0.1 --directory "$directory" >"$log_file" 2>&1 &
server_pid=$!
cleanup() { kill "$server_pid" 2>/dev/null || true; wait "$server_pid" 2>/dev/null || true; }
trap cleanup EXIT
for _ in {1..20}; do curl --fail --silent "http://127.0.0.1:$port/index.html" >/dev/null && break; sleep 0.5; done
curl --fail --show-error --silent "http://127.0.0.1:$port/index.html" >/dev/null
while IFS= read -r file; do
  rel="${file#"$directory/"}"
  curl --fail --show-error --silent "http://127.0.0.1:$port/$rel" >/dev/null
done < <(find "$directory/Build" -maxdepth 1 -type f \( -iname '*.loader.js' -o -iname '*.wasm*' -o -iname '*.data*' \) | head -n 4)
note 'WebGL structural/loading smoke validation passed (HTTP 200 for index and key resources).'
