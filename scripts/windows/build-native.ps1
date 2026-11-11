# scripts/build-native.ps1
[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$IncludeX86
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectPath = Join-Path $RootDir "src\BambooMintKey.NativeBridge\BambooMintKey.NativeBridge.csproj"

function Invoke-PublishNativeForRuntime {
    param (
        [string]$TargetRuntime
    )

    $OutputDir = Join-Path $RootDir "publish\$TargetRuntime"

    Write-Host "====================================================" -ForegroundColor Cyan
    Write-Host "  BambooMintKey NativeAOT Build: $Configuration ($TargetRuntime)" -ForegroundColor Cyan
    Write-Host "====================================================" -ForegroundColor Cyan

    # 1. Dọn dẹp thư mục output cũ (Dừng ctfmon & đổi tên file locked nếu cần)
    Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
    $FinalDllName = if ($TargetRuntime -eq "win-x86") { "BambooMintKey32.dll" } else { "BambooMintKey.dll" }
    $TargetDll = Join-Path $OutputDir $FinalDllName
    $RawDll = Join-Path $OutputDir "BambooMintKey.dll"
    foreach ($candidate in @($TargetDll, $RawDll) | Select-Object -Unique) {
        if (Test-Path $candidate) {
            Write-Host "[1/3] Giải phóng file lock và dọn dẹp thư mục output ($TargetRuntime)..." -ForegroundColor Yellow
            $regsvr = if ($TargetRuntime -eq "win-x86" -and (Test-Path "$env:SystemRoot\SysWOW64\regsvr32.exe")) {
                "$env:SystemRoot\SysWOW64\regsvr32.exe"
            } else {
                "regsvr32.exe"
            }
            Start-Process $regsvr -ArgumentList "/u /s `"$candidate`"" -Wait -ErrorAction SilentlyContinue
            try {
                Remove-Item $candidate -Force -ErrorAction Stop
            } catch {
                $rndOld = "$(Split-Path $candidate -Leaf).$([System.Guid]::NewGuid().ToString('N')).old"
                Rename-Item -Path $candidate -NewName $rndOld -Force -ErrorAction SilentlyContinue
            }
        }
    }

    # 2. Biên dịch NativeAOT DLL
    Write-Host "[2/3] Bắt đầu xuất bản NativeAOT ($TargetRuntime)..." -ForegroundColor Yellow
    dotnet publish $ProjectPath `
        -c $Configuration `
        -r $TargetRuntime `
        -p:NativeLib=Shared `
        -p:PublishAot=true `
        -p:IlcOptimizationPreference=Size `
        -p:OptimizationPreference=Size `
        -p:StackTraceSupport=false `
        -p:InvariantGlobalization=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:GenerateDocumentationFile=false `
        -o $OutputDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[LỖI] dotnet publish thất bại cho $TargetRuntime (exit code $LASTEXITCODE)!" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # 3. Dọn dẹp file không cần thiết
    Write-Host "[3/3] Dọn dẹp artifacts thừa ($TargetRuntime)..." -ForegroundColor Yellow
    Remove-Item -Path (Join-Path $OutputDir "*.pdb") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.xml") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.deps.json") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.exp") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.lib") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.ilk") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.map") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "createdump.exe") -Force -ErrorAction SilentlyContinue

    # 4. Đổi tên đầu ra riêng cho bản 32-bit (tránh trùng Filename khi đóng gói MSIX wapproj) và kiểm tra
    if ($TargetRuntime -eq "win-x86" -and (Test-Path $RawDll)) {
        Move-Item -Path $RawDll -Destination $TargetDll -Force
    }
    if (Test-Path $TargetDll) {
        $fileSize = (Get-Item $TargetDll).Length / 1MB
        Write-Host "[3/3] Xuất bản thành công ($TargetRuntime)!" -ForegroundColor Green
        Write-Host " -> File: $TargetDll" -ForegroundColor Green
        Write-Host " -> Dung lượng: $([Math]::Round($fileSize, 2)) MB" -ForegroundColor Green
    } else {
        Write-Host "[LỖI] Không tìm thấy file $FinalDllName ($TargetRuntime) sau khi build!" -ForegroundColor Red
        exit 1
    }
}

Invoke-PublishNativeForRuntime -TargetRuntime $Runtime

if ($IncludeX86 -and $Runtime -ne "win-x86") {
    Invoke-PublishNativeForRuntime -TargetRuntime "win-x86"
}