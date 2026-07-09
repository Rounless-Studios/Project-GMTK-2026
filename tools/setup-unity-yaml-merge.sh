#!/usr/bin/env bash
set -u

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version_file="$repo_root/ProjectSettings/ProjectVersion.txt"

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

[ -f "$version_file" ] || fail "ProjectSettings/ProjectVersion.txt was not found."

unity_version="$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' "$version_file" | head -n 1)"
[ -n "$unity_version" ] || fail "Could not read m_EditorVersion from ProjectVersion.txt."

find_tool() {
  if [ "${UNITY_YAML_MERGE:-}" ] && [ -x "$UNITY_YAML_MERGE" ]; then
    printf '%s\n' "$UNITY_YAML_MERGE"
    return 0
  fi

  if [ "${UNITY_EXE:-}" ]; then
    editor_dir="$(dirname "$UNITY_EXE")"
    candidate="$editor_dir/Data/Tools/UnityYAMLMerge"
    [ -x "$candidate" ] && { printf '%s\n' "$candidate"; return 0; }
  fi

  for candidate in \
    "/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/Tools/UnityYAMLMerge" \
    "$HOME/Unity/Hub/Editor/$unity_version/Editor/Data/Tools/UnityYAMLMerge" \
    "$HOME/Unity/Hub/Editor/$unity_version/Editor/Data/Tools/UnityYAMLMerge.exe" \
    "/opt/unityhub/editor/$unity_version/Editor/Data/Tools/UnityYAMLMerge" \
    "/opt/unity/Editor/Data/Tools/UnityYAMLMerge"
  do
    if [ -x "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

tool="$(find_tool)" || fail "UnityYAMLMerge was not found for Unity $unity_version. Set UNITY_YAML_MERGE or UNITY_EXE."

cd "$repo_root" || fail "Could not enter repository root."
git rev-parse --show-toplevel >/dev/null || fail "Not inside a Git repository."

git config --local merge.unityyamlmerge.name "Unity Smart Merge (UnityYAMLMerge)"
git config --local merge.unityyamlmerge.driver "'$tool' merge -p --force %O %B %A %A"
git config --local merge.unityyamlmerge.recursive binary
git config --local mergetool.unityyamlmerge.trustExitCode false
git config --local mergetool.unityyamlmerge.cmd "'$tool' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""

printf 'Configured local UnityYAMLMerge driver:\n  %s\n' "$tool"
printf 'Verify with:\n  git config --local --get merge.unityyamlmerge.driver\n'
