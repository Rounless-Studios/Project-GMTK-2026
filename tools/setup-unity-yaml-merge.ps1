param(
    [string]$UnityYamlMergePath,
    [string]$UnityEditorPath
)

$ErrorActionPreference = "Stop"

function Get-ProjectUnityVersion {
    $versionFile = Join-Path $PSScriptRoot "..\ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "ProjectSettings/ProjectVersion.txt was not found."
    }

    $match = Select-String -LiteralPath $versionFile -Pattern "m_EditorVersion:\s*(.+)$" | Select-Object -First 1
    if (-not $match) {
        throw "Could not read m_EditorVersion from ProjectVersion.txt."
    }

    return $match.Matches[0].Groups[1].Value.Trim()
}

function Resolve-UnityYamlMerge {
    param(
        [string]$RequestedTool,
        [string]$RequestedEditor,
        [string]$UnityVersion
    )

    $candidates = New-Object System.Collections.Generic.List[string]

    if ($RequestedTool) {
        $candidates.Add($RequestedTool)
    }

    if ($env:UNITY_YAML_MERGE) {
        $candidates.Add($env:UNITY_YAML_MERGE)
    }

    if ($RequestedEditor) {
        $editorDir = Split-Path -Parent $RequestedEditor
        $candidates.Add((Join-Path $editorDir "Data\Tools\UnityYAMLMerge.exe"))
    }

    if ($env:UNITY_EXE) {
        $editorDir = Split-Path -Parent $env:UNITY_EXE
        $candidates.Add((Join-Path $editorDir "Data\Tools\UnityYAMLMerge.exe"))
    }

    $candidates.Add("C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Data\Tools\UnityYAMLMerge.exe")
    $candidates.Add("C:\Program Files\Unity\Editor\Data\Tools\UnityYAMLMerge.exe")

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "UnityYAMLMerge was not found for Unity $UnityVersion. Set UNITY_YAML_MERGE or pass -UnityYamlMergePath."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    git rev-parse --show-toplevel | Out-Null

    $unityVersion = Get-ProjectUnityVersion
    $tool = Resolve-UnityYamlMerge -RequestedTool $UnityYamlMergePath -RequestedEditor $UnityEditorPath -UnityVersion $unityVersion
    $gitToolPath = $tool.Replace("\", "/")
    $driver = "'$gitToolPath' merge -p --force %O %B %A %A"
    $mergetool = "'$gitToolPath' merge -p `"`$BASE`" `"`$REMOTE`" `"`$LOCAL`" `"`$MERGED`""

    git config --local merge.unityyamlmerge.name "Unity Smart Merge (UnityYAMLMerge)"
    git config --local merge.unityyamlmerge.driver $driver
    git config --local merge.unityyamlmerge.recursive binary
    git config --local mergetool.unityyamlmerge.trustExitCode false
    git config --local mergetool.unityyamlmerge.cmd $mergetool

    Write-Host "Configured local UnityYAMLMerge driver:"
    Write-Host "  $tool"
    Write-Host "Verify with:"
    Write-Host "  git config --local --get merge.unityyamlmerge.driver"
}
finally {
    Pop-Location
}
