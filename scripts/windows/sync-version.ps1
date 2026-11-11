# scripts/windows/sync-version.ps1
# Đồng bộ version từ Package.appxmanifest sang Directory.Build.props.
# Gọi từ target SyncVersionFromAppxManifest trong BambooMintKey.Package.wapproj.
[CmdletBinding()]
param ()

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ManifestPath = Join-Path $RootDir "BambooMintKey.Package\Package.appxmanifest"
$PropsPath = Join-Path $RootDir "Directory.Build.props"
$UiAppManifestPath = Join-Path $RootDir "src\BambooMintKey.UI\app.manifest"

if (-not (Test-Path $ManifestPath)) {
    Write-Host "[sync-version] Không tìm thấy $ManifestPath - bỏ qua." -ForegroundColor Yellow
    exit 0
}
if (-not (Test-Path $PropsPath)) {
    Write-Host "[sync-version] Không tìm thấy $PropsPath - bỏ qua." -ForegroundColor Yellow
    exit 0
}

[xml]$manifest = Get-Content $ManifestPath
$version = $manifest.Package.Identity.Version
if ([string]::IsNullOrEmpty($version)) {
    Write-Host "[sync-version] Không đọc được Version từ appxmanifest - bỏ qua." -ForegroundColor Yellow
    exit 0
}

# Version dạng a.b.c.d (vd: 1.2.2.0). $v3 = a.b.c dùng cho VersionPrefix/Version/InformationalVersion.
$parts = $version.Split('.')
if ($parts.Count -lt 3) {
    Write-Host "[sync-version] Version '$version' không hợp lệ (cần ít nhất a.b.c) - bỏ qua." -ForegroundColor Yellow
    exit 0
}
$v3 = "$($parts[0]).$($parts[1]).$($parts[2])"

$content = [System.IO.File]::ReadAllText($PropsPath)
$content = [System.Text.RegularExpressions.Regex]::Replace($content, '<VersionPrefix>.*?</VersionPrefix>', "<VersionPrefix>$v3</VersionPrefix>")
$content = [System.Text.RegularExpressions.Regex]::Replace($content, '<Version>.*?</Version>', "<Version>$v3</Version>")
$content = [System.Text.RegularExpressions.Regex]::Replace($content, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$version</AssemblyVersion>")
$content = [System.Text.RegularExpressions.Regex]::Replace($content, '<FileVersion>.*?</FileVersion>', "<FileVersion>$version</FileVersion>")
$content = [System.Text.RegularExpressions.Regex]::Replace($content, '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>$v3</InformationalVersion>")

[System.IO.File]::WriteAllText($PropsPath, $content, (New-Object System.Text.UTF8Encoding($false)))

if (Test-Path $UiAppManifestPath) {
    $appManifest = [System.IO.File]::ReadAllText($UiAppManifestPath)
    $appManifest = [System.Text.RegularExpressions.Regex]::Replace(
        $appManifest,
        '(<assemblyIdentity\s+version=")[^"]*(")',
        "`${1}$version`${2}")
    [System.IO.File]::WriteAllText($UiAppManifestPath, $appManifest, (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host "[sync-version] Đã đồng bộ version $version -> Directory.Build.props & app.manifest" -ForegroundColor Green
