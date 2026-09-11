<#
.SYNOPSIS
    Builds the standalone Unity player, without a hard-coded editor path.

.DESCRIPTION
    Finds the editor binary for the version in ProjectSettings/ProjectVersion.txt, looking in
    the Hub and standalone-installer locations for the current OS, and falls back to the
    newest installed version if that exact one is missing. Set UNITY_EDITOR to override.

    The Unity editor is x64 on Windows (it runs emulated on an ARM64 host) and ships native
    Apple Silicon builds on macOS; nothing here depends on the host architecture.
#>
[CmdletBinding()]
param(
    [string]$Output = 'Build/Windows/Champ.exe',

    # Launch the player once it is built.
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'engines/Champ.Unity'

$version = (Select-String -Path (Join-Path $project 'ProjectSettings/ProjectVersion.txt') `
    -Pattern '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()

function Get-EditorCandidates([string]$v) {
    if ($IsMacOS) {
        return @("/Applications/Unity/Hub/Editor/$v/Unity.app/Contents/MacOS/Unity")
    }
    if ($IsLinux) {
        return @("$HOME/Unity/Hub/Editor/$v/Editor/Unity")
    }
    return @(
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$v\Editor\Unity.exe"),
        (Join-Path $env:ProgramFiles "Unity $v\Editor\Unity.exe")
    )
}

$editor = $null
if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) {
    $editor = $env:UNITY_EDITOR
}
else {
    $editor = Get-EditorCandidates $version | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $editor) {
        # Exact version not installed -- take the newest one that is.
        $editor = Get-EditorCandidates '*' |
            ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } |
            Sort-Object FullName |
            Select-Object -Last 1 -ExpandProperty FullName
    }
}

if (-not $editor) {
    throw "No Unity $version editor found. Install it from the Hub, or point UNITY_EDITOR at the binary."
}

$buildLog = Join-Path $project 'Logs\BuildPlayer.log'
"Building the Unity player with $editor -- batchmode, takes a few minutes..."
"Unity log: $buildLog"

# -logFile <path> rather than `-`: Unity's stdout is a pipe under a run configuration, and a
# log file is readable either way. Start-Process -Wait because PowerShell does not block on a
# GUI app (Unity.exe is one, batchmode or not) when its own stdout is not a console.
$unity = Start-Process -FilePath $editor -PassThru -Wait -ArgumentList @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', $project,
    '-executeMethod', 'Champ.Unity.Editor.BuildScript.BuildWindowsCli',
    '-buildOutput', $Output,
    '-logFile', $buildLog
)

"Unity exited with $($unity.ExitCode)"
if ($unity.ExitCode -ne 0) {
    if (Test-Path $buildLog) { Get-Content $buildLog -Tail 30 }
    exit $unity.ExitCode
}

if ($Run) {
    # Unity resolves -buildOutput against the project folder.
    $player = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $project $Output }

    # Wait on the player rather than `& $player`: PowerShell does not block on a GUI app, so
    # the script would exit the moment the window opened. A run configuration that starts this
    # script would then count the run as over and kill the window along with the terminal.
    "Build done. Launching $player"
    $proc = Start-Process -FilePath $player -WorkingDirectory (Split-Path -Parent $player) -PassThru
    $proc.WaitForExit()
    exit $proc.ExitCode
}
