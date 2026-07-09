param(
    [int]$LargeFileWarningMiB = 10,
    [int]$LargeFileFailureMiB = 100
)

$ErrorActionPreference = "Stop"
$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$message) { $script:failures.Add($message) | Out-Null }
function Add-Warning([string]$message) { $script:warnings.Add($message) | Out-Null }

function Get-GitLines([string[]]$arguments) {
    $output = & git @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($arguments -join ' ') failed"
    }
    return @($output)
}

function Test-LfsAttribute([string]$path) {
    $attr = git check-attr filter -- $path
    return ($attr -match "filter:\s*lfs$")
}

function Test-GitLfsPointer([string]$objectSha) {
    $content = git cat-file -p $objectSha 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $false
    }
    return (($content | Select-Object -First 1) -eq "version https://git-lfs.github.com/spec/v1")
}

Push-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))
try {
    git rev-parse --show-toplevel | Out-Null

    $projectVersionPath = "ProjectSettings/ProjectVersion.txt"
    if (Test-Path -LiteralPath $projectVersionPath) {
        $versionLine = Select-String -LiteralPath $projectVersionPath -Pattern "m_EditorVersion:\s*(.+)$" | Select-Object -First 1
        if ($versionLine) {
            Write-Host "Unity editor version: $($versionLine.Matches[0].Groups[1].Value.Trim())"
        }
        else {
            Add-Failure "ProjectSettings/ProjectVersion.txt exists but m_EditorVersion was not found."
        }
    }
    else {
        Add-Failure "ProjectSettings/ProjectVersion.txt is missing."
    }

    if (Get-Command git-lfs -ErrorAction SilentlyContinue) {
        $lfsVersion = git lfs version
        Write-Host "Git LFS: $lfsVersion"
    }
    else {
        Add-Failure "Git LFS is not installed or not on PATH."
    }

    $tracked = Get-GitLines @("ls-files")
    $allRepoFiles = Get-GitLines @("ls-files", "-co", "--exclude-standard")

    $generatedPattern = "^(Library|Temp|Obj|Build|Builds|Logs|UserSettings|MemoryCaptures|Recordings|ServerData)(/|$)"
    $trackedGenerated = $tracked | Where-Object { $_ -match $generatedPattern }
    foreach ($path in $trackedGenerated) {
        Add-Failure "Tracked generated/build path: $path"
    }

    $caseGroups = $allRepoFiles | Group-Object { $_.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 }
    foreach ($group in $caseGroups) {
        Add-Failure "Case-colliding paths: $($group.Group -join ' | ')"
    }

    $markerOutput = @()
    if (Get-Command rg -ErrorAction SilentlyContinue) {
        $markerOutput = rg -n --hidden -g "!.git" -g "!Library/**" -g "!Logs/**" -g "!UserSettings/**" -e "^(<<<<<<<|=======|>>>>>>>)" 2>$null
        if ($LASTEXITCODE -gt 1) {
            Add-Warning "Could not run rg merge-marker scan."
            $markerOutput = @()
        }
    }
    else {
        $textExtensions = "\.(cs|asmdef|asmref|shader|compute|cginc|hlsl|glsl|inputactions|json|xml|uxml|uss|md|txt|yml|yaml|unity|prefab|asset|mat|anim|controller|overrideController|meta)$"
        foreach ($path in $allRepoFiles | Where-Object { $_ -match $textExtensions }) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $matches = Select-String -LiteralPath $path -Pattern "^(<<<<<<<|=======|>>>>>>>)" -ErrorAction SilentlyContinue
                $markerOutput += $matches | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line)" }
            }
        }
    }
    foreach ($line in $markerOutput) {
        Add-Failure "Merge marker: $line"
    }

    $warningBytes = [int64]$LargeFileWarningMiB * 1024 * 1024
    $failureBytes = [int64]$LargeFileFailureMiB * 1024 * 1024
    foreach ($path in $allRepoFiles) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            continue
        }

        $item = Get-Item -LiteralPath $path
        $usesLfs = Test-LfsAttribute $path
        if ($item.Length -ge $failureBytes -and -not $usesLfs) {
            Add-Failure "File is >= ${LargeFileFailureMiB}MiB and not covered by LFS attributes: $path ($($item.Length) bytes)"
        }
        elseif ($item.Length -ge $warningBytes -and -not $usesLfs) {
            Add-Warning "File is >= ${LargeFileWarningMiB}MiB and not covered by LFS attributes: $path ($($item.Length) bytes)"
        }
    }

    foreach ($path in $tracked) {
        if (-not (Test-LfsAttribute $path)) {
            continue
        }

        $indexLine = git ls-files -s -- $path
        if ($LASTEXITCODE -ne 0 -or -not $indexLine) {
            continue
        }

        $parts = $indexLine -split "\s+"
        if ($parts.Count -ge 2 -and -not (Test-GitLfsPointer $parts[1])) {
            Add-Failure "LFS-attributed file is stored as a normal Git blob: $path"
        }
    }

    if (Test-Path -LiteralPath "Assets" -PathType Container) {
        $assetItems = Get-ChildItem -LiteralPath "Assets" -Force -Recurse | Where-Object {
            $_.Name -ne ".DS_Store" -and
            $_.FullName -notmatch "\\Library(\\|$)" -and
            $_.Name -notlike "*.meta"
        }

        foreach ($item in $assetItems) {
            $relative = Resolve-Path -LiteralPath $item.FullName -Relative
            $relative = $relative.TrimStart(".\").Replace("\", "/")
            git check-ignore -q -- $relative
            if ($LASTEXITCODE -eq 0) {
                continue
            }

            $metaPath = "$($item.FullName).meta"
            if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
                Add-Failure "Missing Unity meta file for Assets item: $relative"
            }
        }

        $metaFiles = Get-ChildItem -LiteralPath "Assets" -Force -Recurse -File -Filter "*.meta"
        foreach ($meta in $metaFiles) {
            $target = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
            if (-not (Test-Path -LiteralPath $target)) {
                $relative = Resolve-Path -LiteralPath $meta.FullName -Relative
                Add-Failure "Orphan Unity meta file: $($relative.TrimStart('.\').Replace('\', '/'))"
            }
        }
    }

    $driver = git config --local --get merge.unityyamlmerge.driver
    if ($LASTEXITCODE -ne 0 -or -not $driver) {
        Add-Warning "Local UnityYAMLMerge merge driver is not configured. Run tools/setup-unity-yaml-merge.ps1."
    }
    else {
        Write-Host "UnityYAMLMerge driver: $driver"
        $toolMatch = [regex]::Match($driver, '^''([^'']+)''|^"([^"]+)"|^(.+?)\s+merge\s')
        $toolPath = if ($toolMatch.Groups[1].Success) {
            $toolMatch.Groups[1].Value
        }
        elseif ($toolMatch.Groups[2].Success) {
            $toolMatch.Groups[2].Value
        }
        else {
            $toolMatch.Groups[3].Value
        }
        $toolPath = $toolPath.Replace("/", "\")
        if (-not (Test-Path -LiteralPath $toolPath -PathType Leaf)) {
            Add-Warning "Configured UnityYAMLMerge executable does not exist: $toolPath"
        }
    }

    if ($warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "WARNINGS:"
        foreach ($warning in $warnings) {
            Write-Host "  - $warning"
        }
    }

    if ($failures.Count -gt 0) {
        Write-Host ""
        Write-Host "FAILURES:"
        foreach ($failure in $failures) {
            Write-Host "  - $failure"
        }
        exit 1
    }

    Write-Host ""
    Write-Host "Repository validation passed."
    exit 0
}
finally {
    Pop-Location
}
