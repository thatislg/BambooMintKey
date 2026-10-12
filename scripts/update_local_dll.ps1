# BambooMintKey - Script cap nhat nhanh file DLL & Tray UI vua build vao Program Files (Yeu cau Admin)
param()

$SourceDll = "D:\Kojin\BambooMintKey\publish\win-x64\BambooMintKey.dll"
$SourceUiDir = "D:\Kojin\BambooMintKey\publish\ui"
$DestDir = "C:\Program Files\BambooMintKey"
$DestDll = Join-Path $DestDir "BambooMintKey.dll"
$DestUiExe = Join-Path $DestDir "BambooMintKey.UI.exe"

if (-not (Test-Path $SourceDll)) {
    Write-Host "[ERROR] Khong tim thay file DLL nguon tai $SourceDll" -ForegroundColor Red
    exit 1
}

# Kiem tra quyen Administrator, neu chua co thi tu dong prompt UAC
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Dang yeu cau quyen Administrator de ghi de vao Program Files..." -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -NoExit -File `"$PSCommandPath`""
    exit 0
}

Write-Host "1. Dung ctfmon va BambooMintKey.UI cu..." -ForegroundColor Yellow
Stop-Process -Name "BambooMintKey.UI" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "ctfmon" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

Write-Host "2. Cap nhat BambooMintKey.dll (TSF Engine)..." -ForegroundColor Yellow
$copied = $false

# Cach 1: Copy truc tiep
try {
    Copy-Item -Path $SourceDll -Destination $DestDll -Force -ErrorAction Stop
    $copied = $true
} catch {
    Write-Host "   (File dang duoc tien trinh he thong su dung, chuyen sang co che Rename-and-Replace...)" -ForegroundColor Yellow
}

# Cach 2: Rename file cu thanh .old va copy file moi
if (-not $copied) {
    $oldDll = Join-Path $DestDir ("BambooMintKey.dll.old." + [System.Guid]::NewGuid().ToString("N").Substring(0, 8))
    try {
        Move-Item -Path $DestDll -Destination $oldDll -Force -ErrorAction Stop
        Copy-Item -Path $SourceDll -Destination $DestDll -Force -ErrorAction Stop
        $copied = $true
        Get-ChildItem -Path $DestDir -Filter "BambooMintKey.dll.old.*" | ForEach-Object {
            try { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue } catch {}
        }
    } catch {
        Write-Host "   (Explorer.exe hoac Taskbar dang giu handle, khoi dong lai Explorer...)" -ForegroundColor Yellow
    }
}

# Cach 3: Neu Explorer.exe dang khoa, restart Explorer
if (-not $copied) {
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    try {
        Copy-Item -Path $SourceDll -Destination $DestDll -Force -ErrorAction Stop
        $copied = $true
    } catch {
        $oldDll = Join-Path $DestDir ("BambooMintKey.dll.old." + [System.Guid]::NewGuid().ToString("N").Substring(0, 8))
        Move-Item -Path $DestDll -Destination $oldDll -Force -ErrorAction SilentlyContinue
        Copy-Item -Path $SourceDll -Destination $DestDll -Force -ErrorAction Stop
        $copied = $true
    }
    Start-Process explorer.exe
    Start-Sleep -Milliseconds 800
}

if ($copied) {
    Write-Host "[SUCCESS] Da cap nhat BambooMintKey.dll!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Khong the ghi de BambooMintKey.dll." -ForegroundColor Red
}

Write-Host "3. Cap nhat BambooMintKey.UI (Tray Icon App)..." -ForegroundColor Yellow
if (Test-Path $SourceUiDir) {
    try {
        Get-ChildItem -Path $SourceUiDir | ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $DestDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Host "[SUCCESS] Da cap nhat cac file UI vao $DestDir!" -ForegroundColor Green
    } catch {
        Write-Host "[WARN] Khong the sao chep toan bo file UI: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "4. Khoi dong lai ctfmon..." -ForegroundColor Yellow
Start-Process ctfmon.exe
Start-Sleep -Milliseconds 500

Write-Host "5. Khoi dong BambooMintKey.UI (Tray Icon Mode)..." -ForegroundColor Yellow
if (Test-Path $DestUiExe) {
    Start-Process -FilePath $DestUiExe -ArgumentList "--tray" -WindowStyle Hidden
    Write-Host "[SUCCESS] BambooMintKey.UI dang chay o che do Tray Icon!" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== DA CAP NHAT XONG! ===" -ForegroundColor Cyan
Write-Host "1. Taskbar gio chi hien 'VIE: BambooMintKey' (khong con icon con tranh chap)." -ForegroundColor Green
Write-Host "2. Khay he thong (System Tray) se tu dong hien icon E/V khi ban chon BambooMintKey." -ForegroundColor Green
Write-Host "3. Khi chuyen sang Google Japanese Input (Win + Space), E/V tu an va Google se hien chu 'A' / 'A' binh thuong!" -ForegroundColor Green
Write-Host ""
Write-Host "Cua so nay se tu dong dong sau 5 giay..."
Start-Sleep -Seconds 5
