# scripts/enable-tip.ps1
# Enable BambooMintKey input tip cho user hien tai (KHONG chay Admin).
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$RootDir = Split-Path -Parent $PSScriptRoot
$DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"

$Clsid = "{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}"
$ProfileGuid = "{C2F31A8E-92D0-4F81-9C3E-A52889211D44}"
$LangId = "0x0000042A"

Write-Host "Dang ghi Registry HKCU Profile & SortOrder cho BambooMintKey..." -ForegroundColor Cyan

# 1. Ghi HKCU CTF TIP Profile qua reg.exe (tranh loi Set-ItemProperty (Default) tu PowerShell)
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid" /ve /d "BambooMintKey Vietnamese Input" /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /ve /d "BambooMintKey Vietnamese Input" /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /v "Enable" /t REG_DWORD /d 1 /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /v "IconFile" /t REG_SZ /d "$DllPath" /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /v "IconIndex" /t REG_DWORD /d 0 /f | Out-Null

# 2. Ghi SortOrder AssemblyItem cho Vietnamese (0x0000042a) qua reg.exe
reg add "HKCU\Software\Microsoft\CTF\SortOrder\AssemblyItem\0x0000042a\00000000" /v "CLSID" /t REG_SZ /d "$Clsid" /f | Out-Null
reg add "HKCU\Software\Microsoft\CTF\SortOrder\AssemblyItem\0x0000042a\00000000" /v "Profile" /t REG_SZ /d "$ProfileGuid" /f | Out-Null

# 3. Add vao WinUserLanguageList (backup)
try {
    $tip = "042A:$Clsid$ProfileGuid"
    $list = Get-WinUserLanguageList
    $vi = $list | Where-Object { $_.LanguageTag -eq 'vi' }
    if ($vi -and -not ($vi.InputMethodTips -contains $tip)) {
        $vi.InputMethodTips.Add($tip)
        Set-WinUserLanguageList -LanguageList $list -Force -ErrorAction SilentlyContinue
    }
} catch {
    Write-Host "[CANH BAO] Khong the cap nhat LanguageList qua Set-WinUserLanguageList, da ghi Registry SortOrder." -ForegroundColor Yellow
}

# 3.1 Ghi Category tuong thich Windows 10/11 Input Indicator (khong can Desktop Language Bar)
$categories = @(
    "{34745C63-B2F0-4784-8B67-5E12C8701A31}", # GUID_TFCAT_TIP_KEYBOARD
    "{35E7A704-438C-4235-96BC-4A6361C31595}", # GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER
    "{13A016DF-560B-46CD-947A-4C3AF1E0E35D}", # GUID_TFCAT_TIPCAP_IMMERSIVESUPPORT
    "{25504FB4-7BAB-4BC1-9C69-CF81890F0EF5}", # GUID_TFCAT_TIPCAP_SYSTRAYSUPPORT
    "{CCF05DD7-4A87-11D7-A6E2-00065B84435C}", # GUID_TFCAT_TIPCAP_INPUTMODECOMPARTMENT
    "{49D2F9CF-1F5E-11D7-A6D3-00065B84435C}"  # GUID_TFCAT_TIPCAP_UIELEMENTENABLED
)
foreach ($cat in $categories) {
    reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\Category\Category\$cat\$Clsid" /ve /f | Out-Null
    reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\Category\Item\$Clsid\$cat" /ve /f | Out-Null
}

# 4. Restart ctfmon va explorer de nhan profile moi va nap DLL moi
Write-Host "[OK] Da ghi Registry HKCU Profile & SortOrder thanh cong." -ForegroundColor Green
Write-Host "Dang khoi dong lai ctfmon.exe va explorer.exe..." -ForegroundColor Yellow
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Process ctfmon
Start-Sleep -Milliseconds 500
Write-Host "[OK] Hoan tat kich hoat TIP cho user hien tai." -ForegroundColor Green

