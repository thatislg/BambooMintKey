# scripts/update-winget-manifest.ps1
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$InstallerUrl,

    [Parameter(Mandatory = $true)]
    [string]$InstallerSha256,

    [string]$ManifestDir = "manifests\b\BambooMintKey\BambooMintKey",
    [string]$DefaultLocale = "en-US"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$VersionDir = Join-Path $RootDir "$ManifestDir\$Version"

Write-Host "Updating WinGet manifest for version $Version"
Write-Host " -> $VersionDir"

if (-not (Test-Path $VersionDir)) {
    New-Item -ItemType Directory -Path $VersionDir -Force | Out-Null
}

$versionTemplate = @'
# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.9.0.schema.json
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: {{VERSION}}
DefaultLocale: {{DEFAULT_LOCALE}}
ManifestType: version
ManifestVersion: 1.9.0
'@

$localeTemplate = @'
# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.9.0.schema.json
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: {{VERSION}}
PackageLocale: {{DEFAULT_LOCALE}}
Publisher: BambooMintKey Team
PublisherUrl: https://github.com/Kojin/BambooMintKey
PublisherSupportUrl: https://github.com/Kojin/BambooMintKey/issues
PackageName: BambooMintKey
PackageUrl: https://github.com/Kojin/BambooMintKey
License: MIT
LicenseUrl: https://github.com/Kojin/BambooMintKey/blob/main/LICENSE
Copyright: Copyright (c) 2026 BambooMintKey Team
ShortDescription: Modern Vietnamese Input Method Engine powered by F# NativeAOT and Text Services Framework.
Description: |
  BambooMintKey is an open-source, high-performance Vietnamese Input Method Engine (IME)
  designed for Windows. It features an F# NativeAOT core implementing a formal 5-tuple
  phonotactic model, native TSF integration, zero GC latency, and advanced English detection heuristics.
Moniker: bamboomintkey
Tags:
  - ime
  - vietnamese
  - tsf
  - telex
  - vni
  - input-method
ManifestType: defaultLocale
ManifestVersion: 1.9.0
'@

$installerTemplate = @'
# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.9.0.schema.json
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: {{VERSION}}
MinimumOSVersion: 10.0.19041.0
InstallerType: inno
Scope: machine
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /NORESTART
  SilentWithProgress: /SILENT /NORESTART
  Upgrade: /NORESTART
UpgradeBehavior: install
ElevationRequirement: elevationRequired
AppsAndFeaturesEntries:
  - DisplayName: BambooMintKey
    ProductCode: '{D8A27E4B-4E3F-4A92-805F-294FCE314D01}_is1'
Installers:
  - Architecture: x64
    InstallerUrl: {{URL}}
    InstallerSha256: {{SHA256}}
ManifestType: installer
ManifestVersion: 1.9.0
'@

$placeholders = @{
    VERSION = $Version
    URL = $InstallerUrl
    SHA256 = $InstallerSha256
    DEFAULT_LOCALE = $DefaultLocale
}

function Expand-Placeholders($template, $placeholders) {
    $result = $template
    foreach ($key in $placeholders.Keys) {
        $result = $result -replace "\{\{$key\}\}", $placeholders[$key]
    }
    return $result
}

$versionYaml = Expand-Placeholders $versionTemplate $placeholders
$localeYaml = Expand-Placeholders $localeTemplate $placeholders
$installerYaml = Expand-Placeholders $installerTemplate $placeholders

$versionYaml | Out-File -FilePath (Join-Path $VersionDir "BambooMintKey.BambooMintKey.yaml") -Encoding UTF8NoBOM
$localeYaml | Out-File -FilePath (Join-Path $VersionDir "BambooMintKey.BambooMintKey.locale.$DefaultLocale.yaml") -Encoding UTF8NoBOM
$installerYaml | Out-File -FilePath (Join-Path $VersionDir "BambooMintKey.BambooMintKey.installer.yaml") -Encoding UTF8NoBOM

Write-Host "Manifest files written to $VersionDir"

$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    Write-Host "Validating manifest with winget..."
    & winget validate $VersionDir
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "winget validate returned non-zero exit code."
    }
} else {
    Write-Host "winget not found in PATH. Skipping local validation."
}
