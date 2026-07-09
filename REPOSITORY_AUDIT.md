# Repository Audit

Date: 2026-07-09

Repository: `Rounless-Studios/Project-GMTK-2026`

Branch used for setup: `chore/pre-jam-repo-setup`

## State Before Changes

- Default branch: `main`
- Local branch before setup: `main`
- Remote: `origin` -> `https://github.com/Rounless-Studios/Project-GMTK-2026.git`
- Tracked files before setup: `LICENSE`, `README.md`
- Unity project present: yes, at repository root, but untracked
- Unity editor version: `6000.3.19f1`
- Render pipeline: URP, `com.unity.render-pipelines.universal` `17.3.0`
- `.gitignore`: absent
- `.gitattributes`: absent
- Git LFS configuration: Git LFS installed locally, no repository tracking rules
- GitHub workflows: absent
- Issue templates: absent
- Pull request template: absent
- CODEOWNERS: absent
- Editor configuration: absent
- Line-ending policy: absent; local `core.autocrlf` unset
- Unity serialization: already `m_SerializationMode: 2` in `ProjectSettings/EditorSettings.asset`
- Unity version-control mode: already `Visible Meta Files` in `ProjectSettings/VersionControlSettings.asset`
- Generated Unity directories present locally: `Library/`, `Logs/`, `UserSettings/`
- Generated Unity/build directories tracked: none
- Large binary files in ordinary Git history: none found
- Case-colliding paths: none found
- Merge conflict markers: none found

Reference points used for source-control choices:

- Official GitHub Unity `.gitignore`: https://github.com/github/gitignore/blob/main/Unity.gitignore
- Unity Smart Merge documentation: https://docs.unity3d.com/6000.4/Documentation/Manual/SmartMerge.html

## Changes Made

- Added Unity-focused `.gitignore`.
- Added `.gitattributes` with LF text normalization, UnityYAMLMerge attributes for serialized Unity YAML assets, and conservative Git LFS patterns.
- Added minimal `.editorconfig`.
- Added repository-local UnityYAMLMerge setup scripts:
  - `tools/setup-unity-yaml-merge.ps1`
  - `tools/setup-unity-yaml-merge.sh`
- Added repository validation scripts:
  - `tools/validate-repo.ps1`
  - `tools/validate-repo.sh`
- Added compact GitHub PR and issue templates.
- Added `COLLABORATION.md`.
- Added this audit document.
- Kept existing Unity text serialization and visible meta-file settings because they were already correct.

## GitHub Settings

Successfully configured:

- No repository-level GitHub settings were changed. See the table below for the exact blockers.

Could not configure with currently available tooling:

| Setting | Desired State | Status |
| --- | --- | --- |
| Merge policy | Prefer squash merge for short-lived branches | Not changed; `gh` is not installed and no usable local GitHub API token is available. |
| Delete merged branches | Enabled | Not changed; no exposed connector/API write path for this setting. |
| `main` force-push protection | Force pushes disabled | Not changed; branch protection reads/writes require authenticated API access not available through local CLI. |
| `main` deletion protection | Deletion disabled | Not changed; branch protection reads/writes require authenticated API access not available through local CLI. |
| Required approvals | No mandatory reviewer approval | Not changed; no branch-protection write path available. |
| Required CI gate | None | Not changed; no branch-protection write path available. |
| Jam labels | Minimal `type:*`, `area:*`, `P*`, `cut` labels | Not changed; connector does not expose label creation. |
| Milestone | `GMTK 2026 Submission` | Not changed; connector does not expose milestone creation. |
| Project board | `IDEA`, `NEXT`, `DOING`, `VERIFY`, `DONE`, `CUT` | Not changed; connector does not expose Projects configuration. |

Authentication notes:

- GitHub connector reports repository permissions including `admin`, `maintain`, `push`, `pull`, and `triage`.
- `gh` is not installed.
- Environment variables `GH_TOKEN`, `GITHUB_TOKEN`, `GITHUB_PAT`, and `GITHUB_OAUTH_TOKEN` were absent.
- Local Git credential lookup is unusable because the configured helper is `wincred`, but `credential-wincred` is not available in this environment.
- Public unauthenticated REST can read repo basics, labels, and milestones, but branch protection read returned `401 Unauthorized`.
- Public REST showed the default GitHub label set and no milestones.

## LFS Policy

LFS is used for source asset formats that are typically large binary files or not useful in text diffs:

- layered/source art: `*.psd`, `*.psb`, `*.kra`, `*.clip`
- 3D/source assets: `*.blend`, `*.fbx`, `*.obj`, `*.abc`, `*.mb`, `*.ma`, `*.max`, `*.c4d`
- audio/video: `*.wav`, `*.aif`, `*.aiff`, `*.flac`, `*.mp3`, `*.ogg`, `*.mp4`, `*.mov`, `*.webm`, `*.avi`, `*.mkv`
- large texture/source formats: `*.exr`, `*.hdr`, `*.tga`, `*.tif`, `*.tiff`
- `*.unitypackage`

Unity YAML files are not stored in LFS.

Small raster assets such as `*.png` and `*.jpg` are marked binary but are not automatically forced into LFS.

## Remaining Risks

- Repository-level GitHub protections and merge policy still need an authenticated settings API path or `gh`.
- The remote branch `chore/pre-jam-repo-setup` already existed at the initial commit before local setup work was pushed.
- This is a new Unity template project; high-conflict scene/prefab ownership discipline still depends on team behavior.
- Unity package choices were preserved as found. No dependency cleanup was performed.

## Verification

- `tools/setup-unity-yaml-merge.ps1` configured the local repository merge driver to use `C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data/Tools/UnityYAMLMerge.exe`.
- `tools/validate-repo.ps1` passed with exit code 0.
- `tools/validate-repo.sh` passed with exit code 0 under Windows Bash.
- Representative `.gitignore` checks passed for `Library/`, `Logs/`, `UserSettings/`, `Build/`, `Builds/`, `Temp/`, `Obj/`, `.vs/`, generated `.csproj`, generated `.slnx`, `Dist/`, `Release/`, and `WebBuild/`.
- `git lfs track` reports the intended `.gitattributes` LFS patterns.
- `git lfs ls-files` reports no current LFS files.
- Attribute checks confirmed `.unity` and `.asset` files use `merge=unityyamlmerge`; `*.psd`, `*.wav`, and `*.fbx` use LFS; `*.png` and `*.jpg` are binary but not forced into LFS.
- No tracked generated Unity or build directories were found.
- No case-colliding tracked paths were found.
- No unresolved merge markers were found.
- A targeted whitespace check for non-Unity generated files passed. A full `git diff --check` reports Unity-authored empty YAML fields as trailing whitespace, so `.editorconfig` intentionally disables trim-on-save for Unity YAML/meta/layout files.
- Unity batch-mode open/import with `6000.3.19f1` exited 0. The log showed transient licensing handshake/update messages that resolved to an initialized Unity Personal license, and no compiler/import errors were found.
- Unity batch-mode validation did not modify source-controlled files.
- Local fresh-clone verification from the committed setup branch passed: the clone contained `Assets`, `Packages`, `ProjectSettings`, `.gitignore`, and `.gitattributes`; generated directories were absent; setup and validation passed; the temporary clone was removed.
