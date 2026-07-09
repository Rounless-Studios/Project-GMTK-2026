# GMTK 2026 Repository Collaboration

`main` is the integrated, potentially shippable state. Keep branches short-lived and merge back quickly.

## Branches

Use:

- `feat/<issue-or-short-id>-<slug>`
- `fix/<issue-or-short-id>-<slug>`
- `polish/<issue-or-short-id>-<slug>`
- `chore/<slug>`

No long-lived `develop` branch.

## Local Setup

After cloning:

```powershell
tools/setup-unity-yaml-merge.ps1
tools/validate-repo.ps1
```

On macOS/Linux:

```sh
./tools/setup-unity-yaml-merge.sh
./tools/validate-repo.sh
```

The setup script configures only this local repository's UnityYAMLMerge driver.

## Integration

Open a compact PR into `main` for normal work. Self-merge is acceptable when the change is tested and the release captain has not frozen integration. Prefer squash merge for short-lived work.

Emergency direct integration can happen only when repository permissions and release timing require it. Never force-push `main`.

## Unity Collision Rules

One person owns a high-conflict scene or prefab at a time. Prefer prefab/component boundaries over simultaneous edits to one monolithic scene.

Commit `Assets`, `Packages`, `ProjectSettings`, and required `.meta` files. Do not commit Unity generated folders, IDE caches, logs, or build outputs.

External art/audio credit tracking is mandatory at import time. Record source, license, author, and required credit before the asset is integrated.

During freeze, the release captain may reject integration even if the branch is technically correct.
