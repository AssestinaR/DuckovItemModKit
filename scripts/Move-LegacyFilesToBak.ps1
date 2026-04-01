param(
    [string]$ProjectRoot = '',
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,
    [string]$BakRoot = 'bak/legacy',
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptRoot = if ([string]::IsNullOrEmpty($PSScriptRoot)) { Split-Path -Parent $MyInvocation.MyCommand.Path } else { $PSScriptRoot }
    $ProjectRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
}

$root = (Resolve-Path $ProjectRoot).Path
$manifest = (Resolve-Path $ManifestPath).Path
$bakBase = Join-Path $root ($BakRoot -replace '/', '\\')

if (-not (Test-Path $bakBase) -and -not $WhatIf) {
    New-Item -ItemType Directory -Path $bakBase -Force | Out-Null
}

$lines = Get-Content -Path $manifest |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith('#') }

foreach ($relativePath in $lines) {
    $normalizedRelativePath = $relativePath -replace '/', '\\'
    $sourcePath = Join-Path $root $normalizedRelativePath

    if (-not (Test-Path $sourcePath)) {
        Write-Warning "Missing: $relativePath"
        continue
    }

    $targetPath = Join-Path $bakBase $normalizedRelativePath
    $targetDir = Split-Path -Parent $targetPath

    Write-Host "MOVE $relativePath -> $($targetPath.Substring($root.Length).TrimStart('\\'))"

    if ($WhatIf) {
        continue
    }

    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    Move-Item -Path $sourcePath -Destination $targetPath -Force
}

Write-Host 'Done.'
