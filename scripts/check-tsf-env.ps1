# scripts/check-tsf-env.ps1
# Kiểm tra môi trường Windows TSF trước khi đăng ký BambooMintKey.
[CmdletBinding()]
param ()

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$checks = @(
    @{ Name = "msctf.dll";        Path = "C:\Windows\System32\msctf.dll"; Kind = "File" },
    @{ Name = "ITfInputProcessorProfiles CLSID"; Path = "HKLM:\SOFTWARE\Classes\CLSID\{33C53824-660F-457B-8B3E-5F4A9D87AC47}"; Kind = "Registry" },
    @{ Name = "ITfCategoryMgr CLSID";            Path = "HKLM:\SOFTWARE\Classes\CLSID\{A4B54FC0-ACAA-49FB-BB87-4EB0260080F6}"; Kind = "Registry" },
    @{ Name = "TabletInputService";               Path = "TabletInputService"; Kind = "Service" }
)

$allOk = $true
foreach ($c in $checks) {
    switch ($c.Kind) {
        "File" {
            $ok = Test-Path $c.Path
            Write-Host "[$($ok ? 'OK' : 'FAIL')] $($c.Name): $($c.Path)" -ForegroundColor ($ok ? 'Green' : 'Red')
        }
        "Registry" {
            $ok = Test-Path $c.Path
            Write-Host "[$($ok ? 'OK' : 'FAIL')] $($c.Name): $($c.Path)" -ForegroundColor ($ok ? 'Green' : 'Red')
        }
        "Service" {
            $svc = Get-Service -Name $c.Path -ErrorAction SilentlyContinue
            $ok = $svc -ne $null
            $status = if ($ok) { $svc.Status } else { "NOT FOUND" }
            Write-Host "[$($ok ? 'OK' : 'FAIL')] $($c.Name): $status" -ForegroundColor ($ok ? 'Green' : 'Red')
        }
    }
    if (-not $ok) { $allOk = $false }
}

if (-not $allOk) {
    Write-Host "" 
    Write-Host "Moi truong TSF chua san sang. Cach khac phuc:" -ForegroundColor Yellow
    Write-Host "1. Vao Settings -> Time & Language -> Language & Region" -ForegroundColor Yellow
    Write-Host "2. Them Vietnamese input method (Microsoft Bopomofo / Microsoft Pinyin / Bat ky bo go nao)." -ForegroundColor Yellow
    Write-Host "3. Dam bao Windows khong phai ban N/KN, hoac cai Media Feature Pack." -ForegroundColor Yellow
    Write-Host "4. Chay trong PowerShell admin: Start-Service TabletInputService" -ForegroundColor Yellow
}
