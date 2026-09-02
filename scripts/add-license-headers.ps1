# BambooMintKey - Vietnamese Telex Input Method Editor for Windows
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Script thêm license header MIT vào các file mã nguồn trong dự án.
# Chạy từ thư mục gốc của repository.

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$HeaderCs = @"
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT

"@

$HeaderFs = @"
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT

"@

$HeaderXml = @"
<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

"@

$HeaderPs1 = @"
# BambooMintKey - Vietnamese Telex Input Method Editor for Windows
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT

"@

$Marker = "BambooMintKey - Vietnamese Telex Input Method Editor for Windows"

function Add-HeaderIfMissing {
    param(
        [string]$Path,
        [string]$Header
    )

    $content = Get-Content -Path $Path -Raw -Encoding UTF8
    if ($content -like "*$Marker*") {
        Write-Host "  [SKIP] $Path" -ForegroundColor DarkGray
        return $false
    }

    $newContent = $Header + $content
    Set-Content -Path $Path -Value $newContent -Encoding UTF8 -NoNewline
    Write-Host "  [ADDED] $Path" -ForegroundColor Green
    return $true
}

$root = $PSScriptRoot | Split-Path -Parent
Write-Host "Scanning source files under $root ..." -ForegroundColor Cyan

$files = @()
$files += Get-ChildItem -Path "$root/src" -Recurse -File -Include *.cs, *.fs, *.fsi, *.axaml, *.axaml.fs, *.csproj, *.fsproj, *.props, *.targets |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
$files += Get-ChildItem -Path "$root/tests" -Recurse -File -Include *.cs, *.fs, *.fsi, *.fsproj |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
$files += Get-ChildItem -Path "$root/scripts" -File -Include *.ps1

$added = 0
$skipped = 0

foreach ($file in $files) {
    $result = $false
    switch ($file.Extension.ToLowerInvariant()) {
        { $_ -in ".cs" } { $result = Add-HeaderIfMissing -Path $file.FullName -Header $HeaderCs }
        { $_ -in ".fs", ".fsi", ".axaml.fs" } { $result = Add-HeaderIfMissing -Path $file.FullName -Header $HeaderFs }
        { $_ -in ".csproj", ".fsproj", ".props", ".targets", ".axaml" } { $result = Add-HeaderIfMissing -Path $file.FullName -Header $HeaderXml }
        { $_ -in ".ps1" } { $result = Add-HeaderIfMissing -Path $file.FullName -Header $HeaderPs1 }
        default { Write-Host "  [IGNORE] $file" -ForegroundColor Yellow }
    }

    if ($result) {
        $added++
    } else {
        $skipped++
    }
}

Write-Host "`nDone. Added headers: $added, Skipped (already present): $skipped" -ForegroundColor Cyan
