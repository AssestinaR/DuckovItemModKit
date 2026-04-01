param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string[]]$Extensions = @('.cs', '.md', '.txt', '.json', '.xml', '.props', '.targets', '.csproj', '.sln', '.config', '.yml', '.yaml'),
    [switch]$Recurse = $true,
    [switch]$WhatIf,
    [switch]$VerboseReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TextEncodingName {
    param([byte[]]$Bytes)

    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) { return 'utf8-bom' }
    if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) { return 'utf16-le' }
    if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) { return 'utf16-be' }

    try {
        $utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
        [void]$utf8Strict.GetString($Bytes)
        return 'utf8'
    }
    catch {
        return 'gb18030'
    }
}

function Decode-Text {
    param(
        [byte[]]$Bytes,
        [string]$EncodingName
    )

    switch ($EncodingName) {
        'utf8-bom' {
            $enc = New-Object System.Text.UTF8Encoding($true, $true)
            return $enc.GetString($Bytes, 3, $Bytes.Length - 3)
        }
        'utf8' {
            $enc = New-Object System.Text.UTF8Encoding($false, $true)
            return $enc.GetString($Bytes)
        }
        'utf16-le' {
            return [System.Text.Encoding]::Unicode.GetString($Bytes, 2, $Bytes.Length - 2)
        }
        'utf16-be' {
            return [System.Text.Encoding]::BigEndianUnicode.GetString($Bytes, 2, $Bytes.Length - 2)
        }
        'gb18030' {
            try {
                return [System.Text.Encoding]::GetEncoding(54936).GetString($Bytes)
            }
            catch {
                return [System.Text.Encoding]::GetEncoding(936).GetString($Bytes)
            }
        }
        default {
            throw "Unsupported encoding: $EncodingName"
        }
    }
}

function Convert-FileToUtf8Bom {
    param([System.IO.FileInfo]$File)

    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    if ($bytes.Length -eq 0) {
        return [pscustomobject]@{ Path = $File.FullName; SourceEncoding = 'empty'; Changed = $false; Skipped = $false }
    }

    if ($bytes -contains 0) {
        return [pscustomobject]@{ Path = $File.FullName; SourceEncoding = 'binary-like'; Changed = $false; Skipped = $true }
    }

    $sourceEncoding = Get-TextEncodingName -Bytes $bytes
    $text = Decode-Text -Bytes $bytes -EncodingName $sourceEncoding

    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    $newBytes = $utf8Bom.GetBytes($text)
    $preamble = $utf8Bom.GetPreamble()
    $finalBytes = New-Object byte[] ($preamble.Length + $newBytes.Length)
    [Array]::Copy($preamble, 0, $finalBytes, 0, $preamble.Length)
    [Array]::Copy($newBytes, 0, $finalBytes, $preamble.Length, $newBytes.Length)

    $changed = $true
    if ($bytes.Length -eq $finalBytes.Length) {
        $changed = -not [System.Linq.Enumerable]::SequenceEqual($bytes, $finalBytes)
    }

    if ($changed -and -not $WhatIf) {
        [System.IO.File]::WriteAllBytes($File.FullName, $finalBytes)
    }

    return [pscustomobject]@{
        Path = $File.FullName
        SourceEncoding = $sourceEncoding
        Changed = $changed
        Skipped = $false
    }
}

$rootPath = (Resolve-Path $Root).Path
$extSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($ext in $Extensions) {
    [void]$extSet.Add($ext)
}

$searchOption = if ($Recurse) { [System.IO.SearchOption]::AllDirectories } else { [System.IO.SearchOption]::TopDirectoryOnly }
$files = [System.IO.Directory]::EnumerateFiles($rootPath, '*', $searchOption) |
    Where-Object {
        $full = $_
        $extSet.Contains([System.IO.Path]::GetExtension($full)) -and
        $full -notmatch '\\bin\\|\\obj\\|\\.git\\|\\bak\\'
    } |
    ForEach-Object { Get-Item $_ }

$results = foreach ($file in $files) {
    Convert-FileToUtf8Bom -File $file
}

$changed = @($results | Where-Object { $_.Changed })
$skipped = @($results | Where-Object { $_.Skipped })

Write-Host ("Scanned: {0}" -f @($results).Count)
Write-Host ("Changed: {0}" -f $changed.Count)
Write-Host ("Skipped: {0}" -f $skipped.Count)

if ($VerboseReport) {
    $results |
        Sort-Object SourceEncoding, Path |
        Format-Table SourceEncoding, Changed, Path -AutoSize
}
