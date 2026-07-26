#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
root="$(repo_root)"
scan_path="${1:-Assets}"
validate_relative_path "$scan_path"
target="$root/$scan_path"
[[ -d "$target" ]] || die "LFS scan directory does not exist: $scan_path"

signature='version https://git-lfs.github.com/spec/v1'
found=0
while IFS= read -r -d '' file; do
  if head -c 200 "$file" 2>/dev/null | grep -Fq "$signature"; then
    printf 'Unresolved Git LFS pointer: %s\n' "${file#"$root/"}" >&2
    found=1
  fi
done < <(find "$target" -type f ! -name '*.meta' -print0)
(( found == 0 )) || die 'One or more Assets files are unresolved Git LFS pointers. Check LFS storage and checkout configuration.'
note "No unresolved Git LFS pointers found under $scan_path."
