<#
.SYNOPSIS
    Thu thập số liệu Benchmark bộ gõ tiếng Việt (Menu chọn số, xác nhận từng nấc, lặp vòng)
.DESCRIPTION
    1. Hiển thị menu đánh số để chọn bộ gõ từ file cấu hình ngoài (ime_list.yaml / ime_list.json).
    2. Chọn tốc độ kiểm thử (từng nấc 60ms, 45ms, 30ms hoặc cả 3 nấc).
    3. Luôn dừng lại hỏi xác nhận chuẩn bị trước khi bắn phím, KHÔNG BAO GIỜ tự ý nhảy bài đo.
    4. Ghi kết quả vào CSV và hỏi tiếp tục thử bộ gõ khác hay dừng lại.
.PARAMETER ConfigFile
    File cấu hình danh sách bộ gõ (Mặc định: 'ime_list.yaml')
.PARAMETER WordCount
    Số lượng từ kiểm thử trong bài (Mặc định: 500 từ)
.PARAMETER CasesFile
    Tệp test case chuẩn (Mặc định: 'passage_500.cases')
.PARAMETER App
    Ứng dụng mục tiêu (Mặc định: 'notepad')
.PARAMETER OutputCsv
    Đường dẫn file CSV tổng hợp kết quả (Mặc định: 'docs/6.Benchmark/results/benchmark_all.csv')
.EXAMPLE
    .\scripts\benchmark\collect_benchmark.ps1
    .\scripts\benchmark\collect_benchmark.ps1 -WordCount 100
#>

param(
    [string]$ConfigFile = "ime_list.yaml",

    [int]$WordCount = 500,

    [string]$CasesFile = "passage_500.cases",

    [string]$App = "notepad",

    [string]$OutputCsv = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path "$scriptDir\..\.."
$runnerScript = Join-Path $scriptDir "tier2_sendinput_runner.ps1"

if ([string]::IsNullOrWhiteSpace($OutputCsv)) {
    $resultsDir = Join-Path $repoRoot "docs\6.Benchmark\results"
    if (-not (Test-Path $resultsDir)) {
        New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
    }
    $OutputCsv = Join-Path $resultsDir "benchmark_all.csv"
}

# Tính toán số lượng từ tối đa có trong tệp kịch bản
$casesPath = if (Test-Path $CasesFile) { 
    Resolve-Path $CasesFile 
} else { 
    Join-Path $repoRoot "docs\6.Benchmark\cases\$CasesFile" 
}

$totalAvailableWords = 0
if (Test-Path $casesPath) {
    $caseLines = Get-Content $casesPath -Encoding UTF8 | Where-Object { 
        $t = $_.Trim(); $t -ne "" -and -not $t.StartsWith("#") 
    }
    $totalAvailableWords = $caseLines.Count
}
if ($totalAvailableWords -eq 0) { $totalAvailableWords = 618 }
if ($WordCount -gt $totalAvailableWords) { $WordCount = $totalAvailableWords }

# 1. Tìm và nạp file cấu hình danh sách bộ gõ (YAML hoặc JSON)
$resolvedConfig = if (Test-Path $ConfigFile) { 
    Resolve-Path $ConfigFile 
} else { 
    $p = Join-Path $scriptDir $ConfigFile
    if (Test-Path $p) { $p } else { "" }
}

if (-not $resolvedConfig -or -not (Test-Path $resolvedConfig)) {
    $jsonFallback = Join-Path $scriptDir "ime_list.json"
    if (Test-Path $jsonFallback) { $resolvedConfig = $jsonFallback }
}

$catalog = @()

if ($resolvedConfig -and (Test-Path $resolvedConfig)) {
    $ext = [System.IO.Path]::GetExtension($resolvedConfig).ToLowerInvariant()
    if ($ext -eq ".json") {
        try {
            $jsonItems = Get-Content $resolvedConfig -Raw -Encoding UTF8 | ConvertFrom-Json
            $idx = 1
            foreach ($it in $jsonItems) {
                $itemId = if ($null -ne $it.id) { [int]$it.id } else { $idx }
                $catalog += [PSCustomObject]@{ Id = $itemId; Name = $it.name; Desc = $it.desc }
                $idx++
            }
        } catch {
            Write-Warning "Không đọc được file cấu hình JSON: $_"
        }
    } elseif ($ext -eq ".yaml" -or $ext -eq ".yml") {
        try {
            $idx = 1
            $curr = $null
            foreach ($line in (Get-Content $resolvedConfig -Encoding UTF8)) {
                $t = $line.Trim()
                if ($t -match '^-\s*id:\s*(\d+)') {
                    if ($curr) { $catalog += $curr }
                    $curr = [PSCustomObject]@{ Id = [int]$matches[1]; Name = ""; Desc = "" }
                    $idx = [int]$matches[1] + 1
                } elseif ($t -match '^-\s*name:\s*(.*)') {
                    if ($curr) { $catalog += $curr }
                    $curr = [PSCustomObject]@{ Id = $idx; Name = $matches[1].Trim('"'' '); Desc = "" }
                    $idx++
                } elseif ($t -match '^id:\s*(\d+)' -and $curr) {
                    $curr.Id = [int]$matches[1]
                } elseif ($t -match '^name:\s*(.*)' -and $curr) {
                    $curr.Name = $matches[1].Trim('"'' ')
                } elseif ($t -match '^desc:\s*(.*)' -and $curr) {
                    $curr.Desc = $matches[1].Trim('"'' ')
                }
            }
            if ($curr) { $catalog += $curr }
        } catch {
            Write-Warning "Không đọc được file cấu hình YAML: $_"
        }
    }
}

if ($catalog.Count -eq 0) {
    $catalog = @(
        [PSCustomObject]@{ Id = 1; Name = "BambooMintKey"; Desc = "TSF Native (Mã nguồn mở hiện đại)" },
        [PSCustomObject]@{ Id = 2; Name = "UniKey";        Desc = "Win32 Hook (Phổ biến nhất)" },
        [PSCustomObject]@{ Id = 3; Name = "EVKey";         Desc = "Win32 Hook (Tối ưu game & ứng dụng)" },
        [PSCustomObject]@{ Id = 4; Name = "OpenKey";       Desc = "Win32 Hook (Mã nguồn mở đa nền tảng)" },
        [PSCustomObject]@{ Id = 5; Name = "Windows Built-in Telex"; Desc = "TSF Native (Tích hợp sẵn Windows 10/11)" }
    )
}

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "     VIETNAMESE IME BENCHMARK - BỘ THU THẬP DỮ LIỆU ĐA TỐC ĐỘ            " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "File danh sách bộ gõ   : $(if ($resolvedConfig) { $resolvedConfig } else { 'Mặc định trong mã nguồn' })"
Write-Host "Số từ kiểm thử mỗi nấc : $WordCount từ (File: $CasesFile)"
Write-Host "Ứng dụng kiểm thử      : $App"
Write-Host "File kết quả CSV       : $OutputCsv"
Write-Host ""

$keepRunning = $true

while ($keepRunning) {
    $customId = $catalog.Count + 1

    # 1. Hiển thị Menu chọn bộ gõ bằng số
    Write-Host "==========================================================================" -ForegroundColor Cyan
    Write-Host "                   DANH SÁCH BỘ GÕ - CHỌN BẰNG SỐ                         " -ForegroundColor Cyan
    Write-Host "==========================================================================" -ForegroundColor Cyan
    foreach ($item in $catalog) {
        Write-Host ("  [{0}] {1,-24} - {2}" -f $item.Id, $item.Name, $item.Desc)
    }
    Write-Host ("  [{0}] Bộ gõ khác...            - Nhập tên tùy ý" -f $customId)
    Write-Host "  [0] Thoát chương trình"
    Write-Host ""

    $choice = Read-Host (" 👉 Bạn muốn thử bộ gõ số mấy? Nhập số [0-{0}]" -f $customId)
    $choice = $choice.Trim()

    if ($choice -eq "0" -or $choice.ToLowerInvariant() -eq "q") {
        Write-Host " -> Bạn đã chọn kết thúc chương trình." -ForegroundColor Yellow
        break
    }

    $chosenIME = ""
    $matched = $catalog | Where-Object { "$($_.Id)" -eq $choice }
    if ($matched) {
        $chosenIME = $matched.Name
    }
    elseif ($choice -eq "$customId") {
        $chosenIME = Read-Host " Nhập tên bộ gõ bạn muốn thử"
        if ([string]::IsNullOrWhiteSpace($chosenIME)) {
            Write-Host " [!] Tên bộ gõ không được để trống. Quay lại menu." -ForegroundColor Red
            continue
        }
    }
    else {
        Write-Host (" [!] Lựa chọn không hợp lệ. Vui lòng chọn số từ 0 đến {0}." -f $customId) -ForegroundColor Red
        Start-Sleep -Seconds 1
        continue
    }

    # 2. Chọn số lượng từ kiểm thử cho bộ gõ này
    Write-Host ""
    Write-Host "--------------------------------------------------------------------------" -ForegroundColor Cyan
    Write-Host " CHỌN SỐ LƯỢNG TỪ KIỂM THỬ CHO BỘ GÕ '$chosenIME':" -ForegroundColor Cyan
    Write-Host "--------------------------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  Tệp kịch bản '$CasesFile' có tối đa: $totalAvailableWords từ."
    Write-Host "  (Nhập số từ từ 1 đến $totalAvailableWords, hoặc nhập 0 / [Enter] để lấy TOÀN BỘ $totalAvailableWords từ)"
    Write-Host ""
    $wordsInput = Read-Host (" 👉 Nhập số lượng từ muốn kiểm thử [0-{0}] (Mặc định: {1})" -f $totalAvailableWords, $WordCount)
    $wordsInput = if ($wordsInput) { $wordsInput.Trim() } else { "" }

    $targetWordCount = $WordCount
    if ($wordsInput -eq "0" -or $wordsInput.ToLowerInvariant() -eq "max" -or $wordsInput.ToLowerInvariant() -eq "all") {
        $targetWordCount = $totalAvailableWords
    } elseif ($wordsInput -match '^\d+$') {
        $parsedNum = [int]$wordsInput
        if ($parsedNum -eq 0) {
            $targetWordCount = $totalAvailableWords
        } else {
            $targetWordCount = [math]::Max(1, [math]::Min($parsedNum, $totalAvailableWords))
        }
    }
    Write-Host "  -> Đã chọn kiểm thử: $targetWordCount / $totalAvailableWords từ." -ForegroundColor Green

    # 3. Chọn tốc độ kiểm thử cho bộ gõ này
    Write-Host ""
    Write-Host "--------------------------------------------------------------------------" -ForegroundColor Cyan
    Write-Host " CHỌN CHẾ ĐỘ / TỐC ĐỘ KIỂM THỬ CHO BỘ GÕ '$chosenIME':" -ForegroundColor Cyan
    Write-Host "--------------------------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  [1] Nhóm 1: Tốc độ thông thường (60ms -> 45ms -> 30ms liên hoàn, tự động chạy & lưu) [Mặc định]"
    Write-Host "  [2] Nhóm 2: Chịu tải dồn phím / Stress Test (20ms -> 15ms -> 10ms -> 7ms -> 5ms liên hoàn)"
    Write-Host "  [3] Cả 2 nhóm: Toàn dải tốc độ (60 -> 45 -> 30 -> 20 -> 15 -> 10 -> 7 -> 5 ms)"
    Write-Host "  [4] Chạy riêng nấc 5 ms/phím (~200 phím/giây - Chịu tải dồn phím cực hạn)"
    Write-Host "  [5] Chạy riêng nấc 30 ms/phím (~33 phím/giây - Tốc ký / Mốc chuẩn Baseline)"
    Write-Host "  [6] Chạy riêng nấc 60 ms/phím (~17 phím/giây - Tốc độ bình thường)"
    Write-Host "  [7] Tự nhập tốc độ tùy ý (ms/phím)..."
    Write-Host ""
    $speedChoice = Read-Host " 👉 Chọn chế độ tốc độ [1-7] (Mặc định: 1)"
    $speedChoice = if ($speedChoice) { $speedChoice.Trim() } else { "1" }

    $targetSpeeds = @(60, 45, 30)
    $autoBatch = $true

    switch ($speedChoice) {
        "1" { $targetSpeeds = @(60, 45, 30); $autoBatch = $true }
        "2" { $targetSpeeds = @(20, 15, 10, 7, 5); $autoBatch = $true }
        "3" { $targetSpeeds = @(60, 45, 30, 20, 15, 10, 7, 5); $autoBatch = $true }
        "4" { $targetSpeeds = @(5); $autoBatch = $false }
        "5" { $targetSpeeds = @(30); $autoBatch = $false }
        "6" { $targetSpeeds = @(60); $autoBatch = $false }
        "7" {
            $customSpd = Read-Host " Nhập tốc độ mong muốn (ms/phím, ví dụ: 5, 8, 25)"
            $customTrim = if ($customSpd) { $customSpd.Trim() } else { "" }
            if ($customTrim -match '^\d+$' -and [int]$customTrim -gt 0) {
                $targetSpeeds = @([int]$customTrim)
            } else {
                $targetSpeeds = @(30)
            }
            $autoBatch = $false
        }
        Default { $targetSpeeds = @(60, 45, 30); $autoBatch = $true }
    }

    # 4. Vòng lặp hỏi: "Bạn đã chuẩn bị xong chưa? Xong rồi mới chạy"
    $isReady = $false
    while (-not $isReady) {
        Write-Host ""
        Write-Host "--------------------------------------------------------------------------" -ForegroundColor Yellow
        Write-Host " CHUẨN BỊ ĐO CHO BỘ GÕ: $chosenIME" -ForegroundColor Yellow
        Write-Host "--------------------------------------------------------------------------" -ForegroundColor Yellow
        Write-Host " HƯỚNG DẪN CÀI ĐẶT & THIẾT LẬP:" -ForegroundColor Cyan
        Write-Host "   1. Tải/cài đặt và khởi chạy bộ gõ: [$chosenIME]"
        Write-Host "   2. Bật chế độ gõ TELEX, bảng mã UNICODE"
        $batchTag = if ($autoBatch) { "(Chạy liên hoàn tự động)" } else { "" }
        Write-Host "   4. Chuỗi nấc tốc độ sẽ đo: $( $targetSpeeds -join 'ms -> ' )ms $batchTag"
        Write-Host ""
        $readyAns = Read-Host " 👉 Bạn đã chuẩn bị xong bộ gõ [$chosenIME] chưa? ([Y] Đã xong / [N] Chưa xong / [H] Quay lại menu)"
        $trimmed = if ($readyAns) { $readyAns.Trim().ToLowerInvariant() } else { "" }

        if ($trimmed -eq "h" -or $trimmed -eq "cancel") {
            Write-Host " -> Đã hủy bộ gõ [$chosenIME], quay lại menu chọn." -ForegroundColor Gray
            break
        }
        elseif ($trimmed -eq "y" -or $trimmed -eq "yes" -or $trimmed -eq "xong" -or $trimmed -eq "ok" -or $trimmed -eq "") {
            Write-Host " ✅ Đã xác nhận sẵn sàng cho [$chosenIME]!" -ForegroundColor Green
            Write-Host " Bắt đầu bài test sau 3 giây..." -ForegroundColor Magenta
            for ($cd = 3; $cd -gt 0; $cd--) {
                Write-Host "   $cd..." -ForegroundColor Magenta
                Start-Sleep -Seconds 1
            }
            $isReady = $true
        }
        else {
            Write-Host ""
            Write-Host " ⏳ Chưa xong. Bạn cứ thong thả cài đặt/bật bộ gõ [$chosenIME] nhé." -ForegroundColor Cyan
            Write-Host "    Khi nào xong, quay lại đây nhấn Enter hoặc gõ 'y'." -ForegroundColor Cyan
            Start-Sleep -Seconds 1
        }
    }

    # Nếu người dùng hủy quay lại menu
    if (-not $isReady) {
        continue
    }

    # 5. Thực thi từng nấc tốc độ
    $sIdx = 0
    foreach ($spd in $targetSpeeds) {
        $rate = [math]::Round(1000.0 / $spd, 1)

        # Nếu không phải autoBatch và là nấc thứ 2 trở đi, DỪNG LẠI hỏi người dùng
        if (-not $autoBatch -and $sIdx -gt 0) {
            Write-Host ""
            Write-Host "--------------------------------------------------------------------------" -ForegroundColor Yellow
            Write-Host " CHUẨN BỊ NẤC TIẾP THEO: $spd ms/phím (~$rate phím/giây)" -ForegroundColor Yellow
            Write-Host "--------------------------------------------------------------------------" -ForegroundColor Yellow
            $stepAns = Read-Host " 👉 Bạn có muốn chạy tiếp nấc $spd ms này không? ([Y] Chạy tiếp / [S] Dừng ở đây)"
            $stepTrim = if ($stepAns) { $stepAns.Trim().ToLowerInvariant() } else { "" }
            if ($stepTrim -eq "s" -or $stepTrim -eq "stop" -or $stepTrim -eq "dung") {
                Write-Host " -> Đã dừng các nấc tốc độ tiếp theo của [$chosenIME]." -ForegroundColor Gray
                break
            }
            Write-Host " Bắt đầu nấc $spd ms sau 2 giây..." -ForegroundColor Magenta
            Start-Sleep -Seconds 2
        } elseif ($autoBatch -and $sIdx -gt 0) {
            Write-Host ""
            Write-Host " [LIÊN HOÀN TỰ ĐỘNG] Chuyển tiếp sang nấc $spd ms/phím (~$rate phím/giây)..." -ForegroundColor Yellow
            Start-Sleep -Milliseconds 1200
        }

        Write-Host ""
        Write-Host "   >>> Đang đo: $chosenIME | Tốc độ: $spd ms/phím (~$rate phím/giây)..." -ForegroundColor Cyan

        # Bắn phím qua Win32 SendInput và lưu kết quả vào CSV
        & $runnerScript -App $App -IME $chosenIME -DelayMs $spd -WordCount $targetWordCount -CasesFile $CasesFile

        Start-Sleep -Milliseconds 600
        $sIdx++
    }

    Write-Host ""
    Write-Host "==========================================================================" -ForegroundColor Green
    Write-Host " [XONG] Đã hoàn tất đo và lưu số liệu cho [$chosenIME] vào CSV!" -ForegroundColor Green
    Write-Host " File CSV: $OutputCsv" -ForegroundColor Green
    Write-Host "==========================================================================" -ForegroundColor Green
    Write-Host ""

    # 5. Hỏi người dùng có muốn test tiếp bộ gõ khác hay dừng lại
    $continueAns = Read-Host " 👉 Bạn có muốn thử tiếp BỘ GÕ KHÁC không? ([Y] Tiếp tục / [N] Dừng lại)"
    $continueTrimmed = if ($continueAns) { $continueAns.Trim().ToLowerInvariant() } else { "" }

    if ($continueTrimmed -eq "n" -or $continueTrimmed -eq "no" -or $continueTrimmed -eq "dung" -or $continueTrimmed -eq "q") {
        Write-Host ""
        Write-Host "-> Kết thúc chuỗi kiểm thử." -ForegroundColor Yellow
        $keepRunning = $false
    } else {
        Write-Host ""
        Write-Host "⏳ Hãy thong thả tắt bộ gõ cũ hoặc chuẩn bị bộ gõ mới." -ForegroundColor Cyan
        Write-Host "   Khi nào bạn sẵn sàng chọn bộ gõ tiếp theo, nhấn [Enter] để mở Menu..." -ForegroundColor Cyan
        $null = Read-Host
    }
}

Write-Host ""
Write-Host "==========================================================================" -ForegroundColor Green
Write-Host "                TOÀN BỘ SỐ LIỆU ĐÃ ĐƯỢC LƯU VÀO CSV                       " -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Green
Write-Host "File kết quả: $OutputCsv" -ForegroundColor Cyan
Write-Host "Bạn có thể mở trực tiếp file CSV để xem số liệu, lọc và vẽ biểu đồ."
Write-Host ""
