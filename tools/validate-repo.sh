#!/usr/bin/env bash
set -u

large_warning_mib="${LARGE_FILE_WARNING_MIB:-10}"
large_failure_mib="${LARGE_FILE_FAILURE_MIB:-100}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
failures=()
warnings=()

add_failure() { failures+=("$1"); }
add_warning() { warnings+=("$1"); }

has_lfs_attr() {
  git check-attr filter -- "$1" | grep -Eq 'filter:[[:space:]]*lfs$'
}

is_lfs_pointer() {
  git cat-file -p "$1" 2>/dev/null | head -n 1 | grep -Fxq 'version https://git-lfs.github.com/spec/v1'
}

cd "$repo_root" || exit 1
git rev-parse --show-toplevel >/dev/null || { printf 'Not inside a Git repository.\n' >&2; exit 1; }

if [ -f ProjectSettings/ProjectVersion.txt ]; then
  unity_version="$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' ProjectSettings/ProjectVersion.txt | head -n 1)"
  if [ -n "$unity_version" ]; then
    printf 'Unity editor version: %s\n' "$unity_version"
  else
    add_failure "ProjectSettings/ProjectVersion.txt exists but m_EditorVersion was not found."
  fi
else
  add_failure "ProjectSettings/ProjectVersion.txt is missing."
fi

if command -v git-lfs >/dev/null 2>&1; then
  printf 'Git LFS: %s\n' "$(git lfs version)"
else
  add_failure "Git LFS is not installed or not on PATH."
fi

mapfile -t tracked < <(git ls-files)
mapfile -t all_repo_files < <(git ls-files -co --exclude-standard)

for path in "${tracked[@]}"; do
  if [[ "$path" =~ ^(Library|Temp|Obj|Build|Builds|Logs|UserSettings|MemoryCaptures|Recordings|ServerData)(/|$) ]]; then
    add_failure "Tracked generated/build path: $path"
  fi
done

declare -A seen_case
for path in "${all_repo_files[@]}"; do
  lower="$(printf '%s' "$path" | tr '[:upper:]' '[:lower:]')"
  if [ "${seen_case[$lower]+set}" ]; then
    add_failure "Case-colliding paths: ${seen_case[$lower]} | $path"
  else
    seen_case[$lower]="$path"
  fi
done

if command -v rg >/dev/null 2>&1; then
  marker_output="$(rg -n --hidden -g '!.git' -g '!Library/**' -g '!Logs/**' -g '!UserSettings/**' -e '^(<<<<<<<|=======|>>>>>>>)' || true)"
  if [ -n "$marker_output" ]; then
    while IFS= read -r line; do
      add_failure "Merge marker: $line"
    done <<< "$marker_output"
  fi
else
  add_warning "ripgrep is not installed; merge-marker scan was skipped."
fi

warning_bytes=$((large_warning_mib * 1024 * 1024))
failure_bytes=$((large_failure_mib * 1024 * 1024))
for path in "${all_repo_files[@]}"; do
  [ -f "$path" ] || continue
  size="$(wc -c < "$path" | tr -d ' ')"
  if ! has_lfs_attr "$path"; then
    if [ "$size" -ge "$failure_bytes" ]; then
      add_failure "File is >= ${large_failure_mib}MiB and not covered by LFS attributes: $path ($size bytes)"
    elif [ "$size" -ge "$warning_bytes" ]; then
      add_warning "File is >= ${large_warning_mib}MiB and not covered by LFS attributes: $path ($size bytes)"
    fi
  fi
done

for path in "${tracked[@]}"; do
  has_lfs_attr "$path" || continue
  index_line="$(git ls-files -s -- "$path")"
  sha="$(printf '%s\n' "$index_line" | awk '{print $2}')"
  [ -n "$sha" ] || continue
  if ! is_lfs_pointer "$sha"; then
    add_failure "LFS-attributed file is stored as a normal Git blob: $path"
  fi
done

if [ -d Assets ]; then
  while IFS= read -r path; do
    [ "$path" = "Assets" ] && continue
    [[ "$path" == *.meta ]] && continue
    if git check-ignore -q -- "$path"; then
      continue
    fi
    if [ ! -f "$path.meta" ]; then
      add_failure "Missing Unity meta file for Assets item: $path"
    fi
  done < <(find Assets -mindepth 1 -print | sed 's#^\./##')

  while IFS= read -r meta; do
    target="${meta%.meta}"
    if [ ! -e "$target" ]; then
      add_failure "Orphan Unity meta file: $meta"
    fi
  done < <(find Assets -type f -name '*.meta' -print | sed 's#^\./##')
fi

driver="$(git config --local --get merge.unityyamlmerge.driver || true)"
if [ -z "$driver" ]; then
  add_warning "Local UnityYAMLMerge merge driver is not configured. Run tools/setup-unity-yaml-merge.sh."
else
  printf 'UnityYAMLMerge driver: %s\n' "$driver"
fi

if [ "${#warnings[@]}" -gt 0 ]; then
  printf '\nWARNINGS:\n'
  for warning in "${warnings[@]}"; do
    printf '  - %s\n' "$warning"
  done
fi

if [ "${#failures[@]}" -gt 0 ]; then
  printf '\nFAILURES:\n'
  for failure in "${failures[@]}"; do
    printf '  - %s\n' "$failure"
  done
  exit 1
fi

printf '\nRepository validation passed.\n'
