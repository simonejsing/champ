<#
.SYNOPSIS
    Builds or runs Champ.Stride on any Windows host architecture.

.DESCRIPTION
    On x64 this is a plain `dotnet build` / `dotnet run`.

    On ARM64 the project builds win-x64 (Stride's shader compiler needs spirv-cross.dll,
    which has no win-arm64 build -- see README), so it needs an x64 SDK first on PATH:
    Stride's AssetCompiler shells out to a nested `dotnet`, and an ARM64 one refuses to
    load the win-x64 build. That SDK is discovered here rather than hard-coded in
    .vscode/, so one task definition works on either architecture.
#>
[CmdletBinding()]
param(
    [ValidateSet('build', 'run')]
    [string]$Action = 'run',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'engines\Champ.Stride\Champ.Stride.csproj'

$x64Root = $null
if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') {
    # Program Files\dotnet\x64 is where the official x64 installer puts .NET on an ARM64
    # host, and the one location an x64 app host probes without being told. The others are
    # the usual side-by-side script installs.
    $candidates = @(
        (Join-Path $env:ProgramFiles 'dotnet\x64'),
        (Join-Path $HOME 'dotnet-x64'),
        'C:\dotnet-x64'
    )
    $x64Root = $candidates | Where-Object { Test-Path (Join-Path $_ 'dotnet.exe') } | Select-Object -First 1

    if (-not $x64Root) {
        throw "No x64 .NET SDK found (looked in: $($candidates -join ', ')). " +
              "Stride builds win-x64 on ARM64 hosts and needs one; see the README."
    }

    $env:PATH = "$x64Root;$env:PATH"
    $env:DOTNET_ROOT = $x64Root
    $env:DOTNET_ROOT_X64 = $x64Root
}

if ($Action -eq 'build') {
    & dotnet build $project @Rest
}
else {
    & dotnet run --project $project @Rest
}

exit $LASTEXITCODE
