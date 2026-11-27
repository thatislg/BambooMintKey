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

# Khóa độc quyền toàn cục để tránh Visual Studio AppxBundle chạy song song 2 tiến trình ilc.exe đụng vào cùng thư mục obj/
$buildMutex = New-Object System.Threading.Mutex($false, "Local\BambooMintKey_BuildNative_Mutex")
$hasLock = $false
try {
    $hasLock = $buildMutex.WaitOne([TimeSpan]::FromMinutes(5))
} catch [System.Threading.AbandonedMutexException] {
    $hasLock = $true
}

if (-not $hasLock) {
    Write-Host "[LỖI] Hết thời gian chờ khóa build NativeAOT." -ForegroundColor Red
    exit 1
}

try {

function Invoke-PublishNativeForRuntime {
    param (
        [string]$TargetRuntime
    )

    $OutputDir = Join-Path $RootDir "publish\$TargetRuntime"
    $FinalDllName = if ($TargetRuntime -eq "win-x86") { "BambooMintKey32.dll" } else { "BambooMintKey.dll" }
    $TargetDll = Join-Path $OutputDir $FinalDllName

    # Nếu tiến trình song song vừa mới build xong bản DLL này trong vòng 25 giây qua -> bỏ qua để tránh build trùng
    if (Test-Path $TargetDll) {
        $age = ([DateTime]::UtcNow - (Get-Item $TargetDll).LastWriteTimeUtc).TotalSeconds
        if ($age -ge 0 -and $age -lt 25) {
            Write-Host "[INFO] $FinalDllName ($TargetRuntime) vừa được build cách đây $([Math]::Round($age, 1))s -> dùng lại bản mới nhất." -ForegroundColor Cyan
            return
        }
    }

    Write-Host "====================================================" -ForegroundColor Cyan
    Write-Host "  BambooMintKey NativeAOT Build: $Configuration ($TargetRuntime)" -ForegroundColor Cyan
    Write-Host "====================================================" -ForegroundColor Cyan

    # 1. Biên dịch NativeAOT DLL vào thư mục staging tạm để KHÔNG BAO GIỜ xóa mất DLL đích
    #    trong lúc đang biên dịch (tránh lỗi "Could not copy ... not found" khi Visual Studio build song song).
    $StagingDir = Join-Path $OutputDir "_staging"
    if (Test-Path $StagingDir) {
        Remove-Item -Path $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

    Write-Host "[1/3] Bắt đầu xuất bản NativeAOT ($TargetRuntime)..." -ForegroundColor Yellow
    dotnet publish $ProjectPath `
        -c $Configuration `
        -r $TargetRuntime `
        -p:NativeLib=Shared `
        -p:PublishAot=true `
        -p:IlcOptimizationPreference=Size `
        -p:OptimizationPreference=Size `
        -p:StackTraceSupport=false `
        -p:InvariantGlobalization=true `
        -o $StagingDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[LỖI] dotnet publish thất bại cho $TargetRuntime (exit code $LASTEXITCODE)!" -ForegroundColor Red
        Remove-Item -Path $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
        exit $LASTEXITCODE
    }

    $StagedRawDll = Join-Path $StagingDir "BambooMintKey.dll"
    if (-not (Test-Path $StagedRawDll)) {
        Write-Host "[LỖI] Không tìm thấy file BambooMintKey.dll trong staging ($TargetRuntime) sau khi build!" -ForegroundColor Red
        exit 1
    }

    # 2. Thay thế nguyên tử file DLL đích (giải phóng lock cũ nếu có)
    Write-Host "[2/3] Cập nhật file đích ($FinalDllName)..." -ForegroundColor Yellow
    Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
    if (Test-Path $TargetDll) {
        try {
            Copy-Item -Path $StagedRawDll -Destination $TargetDll -Force -ErrorAction Stop
        } catch {
            $rndOld = "$(Split-Path $TargetDll -Leaf).$([System.Guid]::NewGuid().ToString('N')).old"
            Rename-Item -Path $TargetDll -NewName $rndOld -Force -ErrorAction SilentlyContinue
            Copy-Item -Path $StagedRawDll -Destination $TargetDll -Force -ErrorAction Stop
        }
    } else {
        Copy-Item -Path $StagedRawDll -Destination $TargetDll -Force -ErrorAction Stop
    }

    # 3. Dọn dẹp thư mục staging & artifacts thừa
    Write-Host "[3/3] Dọn dẹp artifacts thừa ($TargetRuntime)..." -ForegroundColor Yellow
    Remove-Item -Path $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.old") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.pdb") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.xml") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.deps.json") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.exp") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.lib") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.ilk") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "*.map") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $OutputDir "createdump.exe") -Force -ErrorAction SilentlyContinue

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

} finally {
    if ($hasLock) {
        $buildMutex.ReleaseMutex()
    }
    $buildMutex.Dispose()
}