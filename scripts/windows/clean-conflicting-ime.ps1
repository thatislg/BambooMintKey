# scripts/windows/clean-conflicting-ime.ps1
# Don dep triet de tan du cua EVKey hoac bo go TSF khac gay xung dot crash voi BambooMintKey.
# Chay trong PowerShell voi quyen Administrator.
[CmdletBinding()]
param ()

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] Dang yeu cau quyen Administrator (UAC)..." -ForegroundColor Yellow
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   BAMBOOMINTKEY - DON DEP XUNG DOT TSF / EVKEY           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Dung tien trinh EVKey va ctfmon
Write-Host "`n[1/6] Dung cac tien trinh EVKey va ctfmon..." -ForegroundColor Yellow
$processNames = @("EVKey", "EVKey64", "EVKey32")
foreach ($p in $processNames) {
    Get-Process -Name $p -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  -> Dung tien trinh: $($_.Name) (PID: $($_.Id))" -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force
    }
}
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Xoa startup Run keys & Scheduled Tasks
Write-Host "`n[2/6] Don dep Registry Startup va Scheduled Tasks..." -ForegroundColor Yellow
$runPaths = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
)
foreach ($path in $runPaths) {
    if (Test-Path $path) {
        $props = Get-ItemProperty -Path $path
        foreach ($name in @("EVKey", "EVKey64", "EVKey32", "EVKey Vietnamese Keyboard")) {
            if ($props.$name) {
                Write-Host "  -> Xoa startup entry: $name tai $path" -ForegroundColor Green
                Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Get-ScheduledTask -TaskPath "\" -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -match "EVKey" } | ForEach-Object {
    Write-Host "  -> Xoa Scheduled Task: $($_.TaskName)" -ForegroundColor Green
    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false
}

# 3. Xoa Registry keys cau hinh EVKey
Write-Host "`n[3/6] Xoa cau hinh Registry EVKey..." -ForegroundColor Yellow
$evkeyRegPaths = @(
    "HKCU:\Software\EVKey",
    "HKLM:\SOFTWARE\EVKey",
    "HKLM:\SOFTWARE\WOW6432Node\EVKey"
)
foreach ($reg in $evkeyRegPaths) {
    if (Test-Path $reg) {
        Write-Host "  -> Xoa Registry key: $reg" -ForegroundColor Green
        Remove-Item -Path $reg -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 4. Quet va don dep TSF TIP mo coi
Write-Host "`n[4/6] Quet va don dep TSF TIP tan du cua EVKey..." -ForegroundColor Yellow
$tipRoots = @(
    "HKLM:\SOFTWARE\Microsoft\CTF\TIP",
    "HKCU:\SOFTWARE\Microsoft\CTF\TIP"
)
foreach ($root in $tipRoots) {
    if (Test-Path $root) {
        Get-ChildItem -Path $root -ErrorAction SilentlyContinue | ForEach-Object {
            $clsidKey = $_.PSPath
            $langProfiles = Join-Path $clsidKey "LanguageProfile"
            if (Test-Path $langProfiles) {
                Get-ChildItem -Path $langProfiles -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
                    $iconVal = (Get-ItemProperty -Path $_.PSPath -Name "IconFile" -ErrorAction SilentlyContinue).IconFile
                    if ($iconVal -and ($iconVal -match "EVKey")) {
                        Write-Host "  -> Xoa TSF Profile tan du tai: $($_.PSPath)" -ForegroundColor Red
                        Remove-Item -Path $clsidKey -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
            }
        }
    }
}

# 5. Don dep file/thu muc tan du
Write-Host "`n[5/6] Don dep file tan du tren o dia..." -ForegroundColor Yellow
$folderCandidates = @(
    "$env:LOCALAPPDATA\EVKey",
    "$env:APPDATA\EVKey",
    "$env:ProgramFiles\EVKey",
    "${env:ProgramFiles(x86)}\EVKey",
    "$env:ProgramData\EVKey",
    "$env:TEMP\EVKey*"
)
foreach ($folder in $folderCandidates) {
    if (Test-Path $folder) {
        Write-Host "  -> Xoa thu muc: $folder" -ForegroundColor Green
        Remove-Item -Path $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 6. Khoi dong lai ctfmon
Write-Host "`n[6/6] Khoi dong lai ctfmon.exe..." -ForegroundColor Yellow
Start-Process ctfmon.exe
Start-Sleep -Milliseconds 500

Write-Host "`n[HOAN TAT] Da don dep sach se tan du cua EVKey!" -ForegroundColor Green

