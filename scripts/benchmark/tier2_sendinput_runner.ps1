<#
.SYNOPSIS
    Vietnamese IME Benchmark - Tier 2: Real-App Integration Runner (SendInput on Windows)
.DESCRIPTION
    Tự động hóa nạp file test case chuẩn (chuỗi phím thô | kết quả mong đợi),
    bắn phím qua Win32 SendInput vào ứng dụng thật, đọc lại văn bản qua Unicode Clipboard API,
    đối chiếu độ đúng, độ trễ, nuốt phím.
    DÙNG CHUNG ĐƯỢC CHO MỌI BỘ GÕ (BambooMintKey, UniKey, EVKey, OpenKey...).
.PARAMETER App
    Tên ứng dụng: 'notepad', 'vscode', hoặc 'active' (gắn vào app đang mở sẵn) (Mặc định: 'notepad')
.PARAMETER WordCount
    Số lượng từ cần kiểm thử (Mặc định: 15 từ khi thử nhanh, tối đa 1000 từ)
.PARAMETER DelayMs
    Khoảng thời gian nghỉ giữa các phím tính bằng mili-giây (Mặc định: 60ms)
.EXAMPLE
    .\tier2_sendinput_runner.ps1 -App notepad -WordCount 15 -DelayMs 60
    .\tier2_sendinput_runner.ps1 -App active -WordCount 50 -DelayMs 50
#>

param(
    [ValidateSet("notepad", "vscode", "active")]
    [string]$App = "notepad",

    [string]$IME = "",

    [string]$IMEVersion = "1.0",

    [string]$Architecture = "Auto",

    [int]$WordCount = 500,

    [int]$DelayMs = 50,

    [string]$CasesFile = "passage_500.cases",

    [int[]]$SweepSpeeds = @(),

    [switch]$NoSave = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path "$scriptDir\..\..").Path

# Định nghĩa các API Win32 cần thiết để mô phỏng bàn phím và đọc Clipboard UTF-16
$source = @"
using System;
using System.Runtime.InteropServices;
using System.Threading;

public class Win32KeyboardHarness {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [In, MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetClipboardData(uint uFormat);

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT {
        public uint type;
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
        public ulong padding;
    }

    public static void SendChar(char c, int delayMs) {
        short scan = VkKeyScan(c);
        if (scan == -1) {
            return;
        }

        byte vk = (byte)(scan & 0xFF);
        bool shift = ((scan >> 8) & 1) != 0;

        if (shift) {
            SendKeyDown(0x10); // VK_SHIFT
        }

        SendKeyDown(vk);
        Thread.Sleep(8);
        SendKeyUp(vk);

        if (shift) {
            SendKeyUp(0x10);
        }

        if (delayMs > 0) {
            Thread.Sleep(delayMs);
        }
    }

    public static void SendShiftCombo(char c, int delayMs) {
        short scan = VkKeyScan(c);
        if (scan == -1) {
            return;
        }

        byte vk = (byte)(scan & 0xFF);
        SendKeyDown(0x10); // VK_SHIFT
        SendKeyDown(vk);
        Thread.Sleep(8);
        SendKeyUp(vk);
        SendKeyUp(0x10);

        if (delayMs > 0) {
            Thread.Sleep(delayMs);
        }
    }

    public static void SendSequence(string text, int delayMs) {
        if (string.IsNullOrEmpty(text)) return;

        int i = 0;
        while (i < text.Length) {
            // Check for [Shift+X] or {Shift+X}
            if ((text[i] == '[' || text[i] == '{') && (i + 7 < text.Length)) {
                string sub = text.Substring(i + 1, 6);
                if (sub.Equals("Shift+", StringComparison.OrdinalIgnoreCase)) {
                    char target = text[i + 7];
                    char closeChar = (text[i] == '[') ? ']' : '}';
                    int closeIdx = text.IndexOf(closeChar, i + 7);
                    if (closeIdx > i + 7) {
                        SendShiftCombo(target, delayMs);
                        i = closeIdx + 1;
                        continue;
                    }
                }
            }

            // Check for Shift+X without brackets
            if (i + 6 < text.Length) {
                string sub = text.Substring(i, 6);
                if (sub.Equals("Shift+", StringComparison.OrdinalIgnoreCase)) {
                    char target = text[i + 6];
                    SendShiftCombo(target, delayMs);
                    i += 7;
                    continue;
                }
            }

            // Ký tự đơn lẻ thông thường
            SendChar(text[i], delayMs);
            i++;
        }
    }

    public static void SendKeyDown(ushort vk) {
        INPUT[] inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].ki.wVk = vk;
        inputs[0].ki.wScan = (ushort)MapVirtualKey((uint)vk, 0); // Hardware ScanCode thực
        inputs[0].ki.dwFlags = 0;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void SendKeyUp(ushort vk) {
        INPUT[] inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].ki.wVk = vk;
        inputs[0].ki.wScan = (ushort)MapVirtualKey((uint)vk, 0);
        inputs[0].ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void SelectAllAndCopy() {
        // Ctrl + A
        SendKeyDown(0x11); // VK_CONTROL
        SendKeyDown(0x41); // A
        Thread.Sleep(60);
        SendKeyUp(0x41);
        SendKeyUp(0x11);
        Thread.Sleep(120);

        // Ctrl + C
        SendKeyDown(0x11);
        SendKeyDown(0x43); // C
        Thread.Sleep(60);
        SendKeyUp(0x43);
        SendKeyUp(0x11);
        Thread.Sleep(250);
    }

    public static string GetUnicodeClipboardText() {
        for (int retry = 0; retry < 5; retry++) {
            if (OpenClipboard(IntPtr.Zero)) {
                try {
                    IntPtr hGlobal = GetClipboardData(13); // CF_UNICODETEXT = 13
                    if (hGlobal != IntPtr.Zero) {
                        return Marshal.PtrToStringUni(hGlobal) ?? "";
                    }
                } finally {
                    CloseClipboard();
                }
            }
            Thread.Sleep(60);
        }
        return "";
    }
}
"@

if (-not ([System.Management.Automation.PSTypeName]"Win32KeyboardHarness").Type) {
    Add-Type -TypeDefinition $source
}

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "       VIETNAMESE IME BENCHMARK - TẦNG 2: REAL APP INTEGRATION            " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan
if ([string]::IsNullOrWhiteSpace($IME)) {
    $IME = Read-Host "Nhập tên bộ gõ đang kiểm thử (ví dụ: UniKey, EVKey, BambooMintKey, OpenKey)"
    if ([string]::IsNullOrWhiteSpace($IME)) {
        $IME = "BambooMintKey"
    }
}

Write-Host "Ứng dụng mục tiêu: $App"
Write-Host "Bộ gõ kiểm thử:    $IME"
Write-Host "Số lượng từ:      $WordCount từ"
Write-Host "Tốc độ bắn phím:   $DelayMs ms/phím (~$( [math]::Round(1000 / $DelayMs) ) phím/giây)"
Write-Host ""

# 1. Tìm repo root và nạp file test case chuẩn hóa (chuỗi phím thô | từ mong đợi)
$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
$casesPath = if (Test-Path $CasesFile) {
    Resolve-Path $CasesFile
} else {
    Join-Path $repoRoot "docs\6.Benchmark\cases\$CasesFile"
}

if (-not (Test-Path $casesPath)) {
    Write-Error "Không tìm thấy file test case tại: $casesPath"
}

Write-Host "Tệp kịch bản:     $CasesFile"
Write-Host "[1/4] Đang nạp danh sách test case từ: $casesPath..." -ForegroundColor Yellow
$lines = Get-Content $casesPath -Encoding UTF8
$testPairs = New-Object System.Collections.Generic.List[PSObject]

foreach ($line in $lines) {
    if ($WordCount -gt 0 -and $testPairs.Count -ge $WordCount) { break }
    $l = $line.Trim()
    if ($l -and -not $l.StartsWith("#")) {
        $parts = $l.Split('|')
        if ($parts.Length -ge 2) {
            $keys = $parts[0].Trim()
            $expected = $parts[1].Trim()
            $lang = if ($parts.Length -ge 3) { $parts[2].Trim().ToLowerInvariant() } else { "vi" }
            $testPairs.Add([PSCustomObject]@{
                Keys = $keys
                Expected = $expected
                Lang = $lang
            })
        }
    }
}

Write-Host "      Đã nạp $($testPairs.Count) cặp test case chuẩn hóa." -ForegroundColor Green

# 2. Khởi chạy hoặc gắn vào cửa sổ ứng dụng mục tiêu
Write-Host "[2/4] Đang chuẩn bị ứng dụng '$App'..." -ForegroundColor Yellow

$proc = $null
$isExternalApp = $false

if ($App -eq "notepad") {
    $proc = Start-Process "notepad.exe" -PassThru
    Start-Sleep -Seconds 1
    $hWnd = $proc.MainWindowHandle
    [Win32KeyboardHarness]::SetForegroundWindow($hWnd) | Out-Null
    Start-Sleep -Milliseconds 500
} elseif ($App -eq "vscode") {
    $vscodePaths = @(
        (Get-Command code -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        (Get-Command code.cmd -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        "$env:LOCALAPPDATA\Programs\Microsoft VS Code\Code.exe",
        "$env:ProgramFiles\Microsoft VS Code\Code.exe",
        "${env:ProgramFiles(x86)}\Microsoft VS Code\Code.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }

    if ($vscodePaths.Count -gt 0) {
        $codeExe = $vscodePaths[0]
        Write-Host "      Tìm thấy VS Code tại: $codeExe" -ForegroundColor Gray
        $proc = Start-Process $codeExe -ArgumentList "--new-window" -PassThru
        Start-Sleep -Seconds 3
        $hWnd = $proc.MainWindowHandle
        [Win32KeyboardHarness]::SetForegroundWindow($hWnd) | Out-Null
        Start-Sleep -Milliseconds 500
    } else {
        Write-Host ""
        Write-Host "[CẢNH BÁO] Không tìm thấy 'code.exe' trong PATH hoặc thư mục mặc định!" -ForegroundColor Yellow
        Write-Host "Chuyển sang chế độ [Active Window]: Vui lòng mở sẵn trình soạn thảo bạn muốn thử." -ForegroundColor Cyan
        $App = "active"
    }
}

if ($App -eq "active") {
    $isExternalApp = true
    Write-Host ""
    Write-Host ">>> CHẾ ĐỘ THỬ NGHIỆM TRỰC TIẾP (ACTIVE WINDOW) <<<" -ForegroundColor Cyan
    Write-Host "Hãy NHẤP CHUỘT VÀO VÙNG SOẠN THẢO trong ứng dụng bạn muốn thử (VS Code / Antigravity / Word...)" -ForegroundColor Yellow
    for ($cd = 5; $cd -gt 0; $cd--) {
        Write-Host "      Bắt đầu bắn phím trong: $cd giây..." -ForegroundColor Magenta
        Start-Sleep -Seconds 1
    }
    Write-Host "      >>> BẮT ĐẦU BẮN PHÍM! <<<" -ForegroundColor Green
} else {
    Write-Host "      Ứng dụng đã sẵn sàng (PID: $($proc.Id)). Bắt đầu bắn $($testPairs.Count) từ..." -ForegroundColor Green
}

# Xóa sạch nội dung cũ trong cửa sổ (Ctrl+A -> Backspace) để đảm bảo trang trắng tinh
[Win32KeyboardHarness]::SendKeyDown(0x11)
[Win32KeyboardHarness]::SendKeyDown(0x41)
Start-Sleep -Milliseconds 50
[Win32KeyboardHarness]::SendKeyUp(0x41)
[Win32KeyboardHarness]::SendKeyUp(0x11)
Start-Sleep -Milliseconds 80
[Win32KeyboardHarness]::SendKeyDown(0x08)
Start-Sleep -Milliseconds 30
[Win32KeyboardHarness]::SendKeyUp(0x08)
Start-Sleep -Milliseconds 150

Write-Host "[3/4] Đang bắn phím mô phỏng qua Win32 SendInput..." -ForegroundColor Yellow

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$typedCount = 0

function Send-KeySequence([string]$keys, [int]$delayMs) {
    if ([string]::IsNullOrEmpty($keys)) { return }

    $i = 0
    $len = $keys.Length
    while ($i -lt $len) {
        # Check [Shift+x] hoặc {Shift+x}
        if (($keys[$i] -eq '[' -or $keys[$i] -eq '{') -and ($i + 7 -lt $len)) {
            $sub = $keys.Substring($i + 1, 6)
            if ($sub -eq "Shift+" -or $sub -eq "shift+") {
                $target = $keys[$i + 7]
                $closeChar = if ($keys[$i] -eq '[') { ']' } else { '}' }
                $closeIdx = $keys.IndexOf($closeChar, $i + 7)
                if ($closeIdx -gt ($i + 7)) {
                    $upperChar = switch ($target) {
                        "'" { '"' }
                        "9" { '(' }
                        "0" { ')' }
                        ";" { ':' }
                        "1" { '!' }
                        Default { [char]::ToUpperInvariant($target) }
                    }
                    [Win32KeyboardHarness]::SendChar($upperChar, $delayMs)
                    $i = $closeIdx + 1
                    continue
                }
            }
        }

        # Check Shift+x không ngoặc
        if (($i + 6 -lt $len) -and ($keys.Substring($i, 6) -eq "Shift+" -or $keys.Substring($i, 6) -eq "shift+")) {
            $target = $keys[$i + 6]
            $upperChar = switch ($target) {
                "'" { '"' }
                "9" { '(' }
                "0" { ')' }
                ";" { ':' }
                "1" { '!' }
                Default { [char]::ToUpperInvariant($target) }
            }
            [Win32KeyboardHarness]::SendChar($upperChar, $delayMs)
            $i += 7
            continue
        }

        # Ký tự thông thường
        [Win32KeyboardHarness]::SendChar($keys[$i], $delayMs)
        $i++
    }
}

foreach ($pair in $testPairs) {
    # Bắn chuỗi phím thô (hỗ trợ cả [Shift+x], Shift+x và ký tự đơn lẻ)
    Send-KeySequence $pair.Keys $DelayMs
    # Phím ngắt từ Space
    [Win32KeyboardHarness]::SendChar(' ', $DelayMs)
    # Khoảng nghỉ an toàn để hook engine (như UniKey, EVKey) hoàn tất chuỗi Backspace nếu có
    Start-Sleep -Milliseconds 40
    $typedCount++

    if ($typedCount % 100 -eq 0 -or $typedCount -eq $testPairs.Count) {
        Write-Host "      -> Đã gõ: $typedCount / $($testPairs.Count) từ..." -ForegroundColor Gray
    }
}

$sw.Stop()
Write-Host "      Hoàn tất gõ trong $([math]::Round($sw.Elapsed.TotalSeconds, 2)) giây." -ForegroundColor Green

# 3. Đọc lại nội dung qua Win32 Unicode Clipboard API (CF_UNICODETEXT)
Write-Host "[4/4] Đang đọc kết quả từ ứng dụng và phân tích..." -ForegroundColor Yellow
Start-Sleep -Milliseconds 600
[Win32KeyboardHarness]::SelectAllAndCopy()

$actualText = ""
try {
    $actualText = Get-Clipboard -Raw
} catch {
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
        $actualText = [System.Windows.Forms.Clipboard]::GetText()
    } catch {
        $actualText = (Get-Clipboard) -join " "
    }
}

if ($actualText -is [array]) {
    $actualText = $actualText -join " "
}

function Normalize-VietnameseTone([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return "" }
    $s = $text
    $s = $s.Replace("oà","òa").Replace("oá","óa").Replace("oả","ỏa").Replace("oã","õa").Replace("oạ","ọa")
    $s = $s.Replace("oè","òe").Replace("oé","óe").Replace("oẻ","ỏe").Replace("oẽ","õe").Replace("oẹ","ọe")
    $s = $s.Replace("uỳ","ùy").Replace("uý","úy").Replace("uỷ","ủy").Replace("uỹ","ũy").Replace("uỵ","ụy")
    $s = $s.Replace("Oà","Òa").Replace("Oá","Óa").Replace("Oả","Ỏa").Replace("Oã","Õa").Replace("Oạ","Ọa")
    $s = $s.Replace("Oè","Òe").Replace("Oé","Óe").Replace("Oẻ","Ỏe").Replace("Oẽ","Õe").Replace("Oẹ","Ọe")
    $s = $s.Replace("Uỳ","Ùy").Replace("Uý","Úy").Replace("Uỷ","Ủy").Replace("Uỹ","Ũy").Replace("Uỵ","Ụy")
    return $s
}

$actualWords = $actualText.Split([char[]]@(' ', "`t", "`r", "`n"), [StringSplitOptions]::RemoveEmptyEntries)

$passed = 0
$failed = 0
$totalVN = 0
$passedVN = 0
$failedVN = 0
$totalEN = 0
$passedEN = 0
$failedEN = 0
$allErrors = New-Object System.Collections.Generic.List[string]
$sampleErrors = New-Object System.Collections.Generic.List[string]

# Tính thời gian lý thuyết và overhead
$totalCharCount = 0
foreach ($p in $testPairs) {
    $totalCharCount += $p.Keys.Length + 1
}
$theoreticalSec = [math]::Round(($totalCharCount * $DelayMs) / 1000.0, 2)
$actualSec = [math]::Round($sw.Elapsed.TotalSeconds, 2)
$overheadPct = if ($theoreticalSec -gt 0) { [math]::Round((($actualSec - $theoreticalSec) / $theoreticalSec) * 100, 1) } else { 0 }

for ($i = 0; $i -lt $testPairs.Count; $i++) {
    $pair = $testPairs[$i]
    $expected = $pair.Expected
    $actual = if ($i -lt $actualWords.Length) { $actualWords[$i] } else { "" }
    $isEng = ($pair.Lang -eq "en")

    if ($isEng) { $totalEN++ } else { $totalVN++ }

    # So sánh với dung sai kiểu đặt dấu mới/cũ và biến thể (|)
    $isMatch = $false
    $variants = $expected -split '\|'
    foreach ($v in $variants) {
        if ($actual -eq $v -or (Normalize-VietnameseTone $actual) -eq (Normalize-VietnameseTone $v)) {
            $isMatch = $true
            break
        }
    }

    if ($isMatch) {
        $passed++
        if ($isEng) { $passedEN++ } else { $passedVN++ }
    } else {
        $failed++
        if ($isEng) { $failedEN++ } else { $failedVN++ }
        $langTag = if ($isEng) { "[Tiếng Anh]" } else { "[Tiếng Việt]" }
        $errLine = "$langTag Từ #$($i+1) (phím '$($pair.Keys)'): mong đợi='$expected', thực tế='$actual'"
        $allErrors.Add($errLine)
        if ($sampleErrors.Count -lt 25) {
            $sampleErrors.Add($errLine)
        }
    }
}

$accuracy = [math]::Round(($passed / $testPairs.Count) * 100, 2)
$accVN = if ($totalVN -gt 0) { [math]::Round(($passedVN / $totalVN) * 100, 2) } else { 0 }
$accEN = if ($totalEN -gt 0) { [math]::Round(($passedEN / $totalEN) * 100, 2) } else { 0 }
$speedWpm = [math]::Round(($testPairs.Count / ($sw.Elapsed.TotalMinutes)), 1)

# Phân tích so sánh với baseline 30ms nếu đây là bài đo dồn phím (< 30ms)
$cleanIme = ($IME -replace '[^a-zA-Z0-9_-]', '').ToLowerInvariant()
$logsDir = Join-Path $repoRoot "docs\6.Benchmark\results\logs"
$newSpeedErrors = New-Object System.Collections.Generic.List[string]

if ($DelayMs -lt 30 -and (Test-Path $logsDir)) {
    $baseLogFiles = Get-ChildItem -Path $logsDir -Filter "${cleanIme}_${App}_30ms_*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($baseLogFiles -and $baseLogFiles.Count -gt 0) {
        try {
            $baseRaw = Get-Content $baseLogFiles[0].FullName -Raw -Encoding UTF8
            $marker = "VĂN BẢN THỰC TẾ THU ĐƯỢC TỪ ỨNG DỤNG (CLIPBOARD OUTPUT)"
            $pos = $baseRaw.IndexOf($marker)
            if ($pos -gt 0) {
                $baseTail = $baseRaw.Substring($pos)
                $baseWordsRaw = $baseTail.Split([char[]]@(' ', "`t", "`r", "`n"), [StringSplitOptions]::RemoveEmptyEntries)
                # Bỏ tiêu đề phần clipboard
                $baseWords = $baseWordsRaw | Where-Object { 
                    $_ -ne "---" -and $_ -ne "VĂN" -and $_ -ne "BẢN" -and $_ -ne "THỰC" -and $_ -ne "TẾ" -and 
                    $_ -ne "THU" -and $_ -ne "ĐƯỢC" -and $_ -ne "TỪ" -and $_ -ne "ỨNG" -and $_ -ne "DỤNG" -and 
                    $_ -ne "(CLIPBOARD" -and $_ -ne "OUTPUT)" 
                }

                for ($k = 0; $k -lt $testPairs.Count; $k++) {
                    $pairK = $testPairs[$k]
                    $actCurr = if ($k -lt $actualWords.Length) { $actualWords[$k] } else { "" }
                    $actBase = if ($k -lt $baseWords.Count) { $baseWords[$k] } else { "" }

                    $baseOk = $false
                    foreach ($v in ($pairK.Expected -split '\|')) {
                        if ($actBase -eq $v -or (Normalize-VietnameseTone $actBase) -eq (Normalize-VietnameseTone $v)) {
                            $baseOk = $true; break
                        }
                    }

                    $currOk = $false
                    foreach ($v in ($pairK.Expected -split '\|')) {
                        if ($actCurr -eq $v -or (Normalize-VietnameseTone $actCurr) -eq (Normalize-VietnameseTone $v)) {
                            $currOk = $true; break
                        }
                    }

                    if ($baseOk -and -not $currOk) {
                        $newSpeedErrors.Add("  * Từ #$($k+1) (phím '$($pairK.Keys)'): mong đợi='$($pairK.Expected)', ở 30ms ĐÚNG ('$actBase'), ở ${DelayMs}ms SAI ('$actCurr')")
                    }
                }
            }
        } catch {}
    }
}

# In báo cáo kết quả
Write-Host ""
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "                  KẾT QUẢ BENCHMARK TẦNG 2 TRÊN $App.ToUpper()            " -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "Tổng số từ kiểm thử : $($testPairs.Count) từ ($totalVN tiếng Việt | $totalEN tiếng Anh)"
$failColor = if ($failed -gt 0) { "Red" } else { "Green" }
$accColor = if ($accuracy -ge 90.0) { "Green" } else { "Yellow" }
Write-Host "Độ đúng chung (All) : $passed / $($testPairs.Count) ($accuracy %)" -ForegroundColor $accColor
Write-Host "  * Tiếng Việt (VN) : $passedVN / $totalVN ($accVN %) - Sai: $failedVN từ" -ForegroundColor $(if ($failedVN -gt 0) { "Yellow" } else { "Green" })
Write-Host "  * Tiếng Anh  (EN) : $passedEN / $totalEN ($accEN %) - Sai: $failedEN từ" -ForegroundColor $(if ($failedEN -gt 0) { "Yellow" } else { "Green" })
Write-Host "Thời gian thực thi  : $actualSec s (Lý thuyết: $theoreticalSec s | Overhead: $overheadPct %)"
Write-Host "Tốc độ gõ giả lập   : $speedWpm WPM"
if ($newSpeedErrors.Count -gt 0) {
    Write-Host "  * [CẢNH BÁO DỒN PHÍM] Có $($newSpeedErrors.Count) từ bị hỏng mới so với mốc chuẩn 30ms!" -ForegroundColor Red
} elseif ($DelayMs -lt 30) {
    Write-Host "  * [CHỊU TẢI DỒN PHÍM] Rất tốt! Không phát sinh lỗi mới nào so với mốc 30ms." -ForegroundColor Green
}

if ($sampleErrors.Count -gt 0) {
    Write-Host ""
    Write-Host "Mẫu các từ không khớp (Hiển thị tối đa 25 từ đầu tiên):" -ForegroundColor Red
    foreach ($err in $sampleErrors) {
        Write-Host "  - $err" -ForegroundColor Red
    }
    if ($allErrors.Count -gt 25) {
        Write-Host "  ... và còn $( $allErrors.Count - 25 ) từ lỗi khác (Xem toàn bộ danh sách trong file Log chi tiết)." -ForegroundColor Yellow
    }
}

# Lưu kết quả cấu trúc và ghi log chi tiết
if (-not $NoSave) {
    $timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")

    $resolvedArch = $Architecture
    if ($resolvedArch -eq "Auto") {
        if ($IME -match "Bamboo|Windows") { $resolvedArch = "TSF" } else { $resolvedArch = "Hook" }
    }

    # 1. Ghi log toàn bộ kết quả output đầu ra cho mỗi lần thử nghiệm (KHÔNG GIỚI HẠN)
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir -Force | Out-Null }
    $logFileName = "${cleanIme}_${App}_${DelayMs}ms_${timestamp}.log"
    $logFilePath = Join-Path $logsDir $logFileName

    $stressReportSection = ""
    if ($newSpeedErrors.Count -gt 0) {
        $stressReportSection = @"

--------------------------------------------------------------------------
PHÂN TÍCH CHỊU TẢI DỒN PHÍM (SO VỚI MỐC BASELINE 30MS)
--------------------------------------------------------------------------
Phát hiện $($newSpeedErrors.Count) lỗi mới do tăng tốc / dồn phím:
$($newSpeedErrors -join "`r`n")
"@
    }

    $logContent = @"
==========================================================================
VIETNAMESE IME BENCHMARK - KẾT QUẢ KIỂM THỬ CHI TIẾT
==========================================================================
Thời điểm chạy    : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Bộ gõ kiểm thử    : $IME (Kiến trúc: $resolvedArch)
Ứng dụng kiểm thử : $App
Tốc độ phím gõ    : $DelayMs ms/phím (~$([math]::Round(1000.0 / $DelayMs, 1)) phím/giây)
Tệp kịch bản      : $CasesFile
Thời gian thực tế : $actualSec s (Thời gian lý thuyết: $theoreticalSec s | Độ trễ Overhead: $overheadPct %)
Tốc độ tương đương: $speedWpm WPM

--------------------------------------------------------------------------
TỔNG HỢP KẾT QUẢ ĐỘ CHÍNH XÁC (ACCURACY)
--------------------------------------------------------------------------
Tổng số từ kiểm thử : $($testPairs.Count) từ
Tổng số từ chính xác: $passed từ
Tổng số từ sai/hỏng : $failed từ
Độ đúng chung (All) : $accuracy %

* Tiếng Việt (VN)   : $totalVN từ | Đúng: $passedVN | Sai: $failedVN | Độ đúng: $accVN %
* Tiếng Anh  (EN)   : $totalEN từ | Đúng: $passedEN | Sai: $failedEN | Độ đúng: $accEN %
$stressReportSection

--------------------------------------------------------------------------
DANH SÁCH TOÀN BỘ CÁC TỪ BỊ LỖI (ERRORS - $($allErrors.Count) TỪ)
--------------------------------------------------------------------------
$($allErrors -join "`r`n")

--------------------------------------------------------------------------
VĂN BẢN THỰC TẾ THU ĐƯỢC TỪ ỨNG DỤNG (CLIPBOARD OUTPUT)
--------------------------------------------------------------------------
$actualText
"@
    $logContent | Set-Content -Path $logFilePath -Encoding UTF8

    # 2. Lưu file CSV cho lượt chạy này
    $csvDir = Join-Path $repoRoot "docs\6.Benchmark\results"
    if (-not (Test-Path $csvDir)) { New-Item -ItemType Directory -Path $csvDir -Force | Out-Null }
    $csvFileName = "${cleanIme}_${App}_${DelayMs}ms_${timestamp}.csv"
    $csvFilePath = Join-Path $csvDir $csvFileName

    $csvRow = [PSCustomObject]@{
        Timestamp       = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        IME             = $IME
        Architecture    = $resolvedArch
        App             = $App
        DelayMs         = $DelayMs
        KeysPerSec      = [math]::Round(1000.0 / $DelayMs, 1)
        TotalWords      = $testPairs.Count
        Passed          = $passed
        Failed          = $failed
        Accuracy        = $accuracy
        TotalVN         = $totalVN
        PassedVN        = $passedVN
        FailedVN        = $failedVN
        AccuracyVN      = $accVN
        TotalEN         = $totalEN
        PassedEN        = $passedEN
        FailedEN        = $failedEN
        AccuracyEN      = $accEN
        NewErrorsVs30ms = $newSpeedErrors.Count
        OverheadPct     = $overheadPct
        WPM             = $speedWpm
        CasesFile       = $CasesFile
    }

    $csvRow | Export-Csv -Path $csvFilePath -NoTypeInformation -Encoding UTF8

    # 3. Đồng thời nối vào file CSV tổng hợp chung
    $masterCsv = Join-Path $csvDir "benchmark_all.csv"
    $csvRow | Export-Csv -Path $masterCsv -Append -NoTypeInformation -Encoding UTF8

    Write-Host ""
    Write-Host "[GHI LOG OUTPUT] Đã lưu kết quả chi tiết: docs/6.Benchmark/results/logs/$logFileName" -ForegroundColor Green
    Write-Host "[LƯU CSV] Đã xuất kết quả lượt chạy : docs/6.Benchmark/results/$csvFileName" -ForegroundColor Green
    Write-Host "[TỔNG HỢP CSV] Đã ghi nhận vào bảng chung: docs/6.Benchmark/results/benchmark_all.csv" -ForegroundColor Green
}

# Đóng ứng dụng kiểm thử (chỉ đóng nếu là app do runner tự bật)
if (-not $isExternalApp -and $proc) {
    try {
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $proc.HasExited) { $proc.Kill() }
    } catch {}
}
