<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 026: Xung đột Text Services Framework (TSF) giữa BambooMintKey và các bộ gõ tiếng Việt khác (EVKey) do tàn dư thư viện và Registry sau khi gỡ không sạch

**Mã tài liệu:** `026_EVKey_TSF_Conflict_And_Clean_Uninstall_Guide`  
**Trạng thái:** ✅ Đã xác định nguyên nhân & Có bộ script dọn dẹp triệt để  
**Mức độ nghiêm trọng:** Cao (Gây crash tiến trình ứng dụng như Word, Excel, Chrome, Notepad hoặc treo hệ thống gõ)  
**Ngày ghi nhận:** 03/11/2026  
**Nền tảng ảnh hưởng:** Windows 10 / Windows 11 (64-bit)  
**Mô-đun liên quan:**
- Windows Text Services Framework (`msctf.dll`, `ctfmon.exe`)
- `src/BambooMintKey.NativeBridge` (In-Process COM Server TIP)
- Registry hệ thống: `HKLM/HKCU\SOFTWARE\Microsoft\CTF\TIP`, `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

---

## 1. Mô tả hiện tượng & Bối cảnh phát sinh

### 1.1. Bối cảnh
Người dùng trước đó đã cài đặt và sử dụng các phần mềm gõ tiếng Việt khác (tiêu biểu là **EVKey**, hoặc Unikey có chế độ tương thích nâng cao). Khi chuyển sang sử dụng **BambooMintKey**, người dùng tiến hành gỡ bỏ bộ gõ cũ bằng cách xóa thư mục chương trình hoặc chạy gỡ cài đặt thông thường.

### 1.2. Hiện tượng lỗi
- Sau khi cài đặt và kích hoạt BambooMintKey, khi mở các ứng dụng soạn thảo hoặc gõ văn bản (Notepad, Microsoft Word, trình duyệt Chrome/Edge, Discord, game,...), ứng dụng lập tức bị **crash (đột ngột biến mất)** hoặc **treo cứng (freeze)**.
- Phân tích Windows Event Viewer (`Application Error`) thường ghi nhận lỗi nạp mô-đun:
  - **Exception Code:** `0xc0000005` (Access Violation).
  - **Faulting Module Name:** `msctf.dll`, `EVKey64.dll` (hoặc `BambooMintKey.dll` khi tương tác với pipeline TSF bị corrupt).
- Hiện tượng chỉ chấm dứt hoàn toàn khi tiến hành gỡ bỏ sạch sẽ toàn bộ tàn dư của bộ gõ cũ (tiến trình chạy ngầm, DLL injection, hook và các khóa Registry TSF liên quan).

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Cơ chế hoạt động của EVKey và can thiệp TSF / Windows Hook
1. **Chế độ gõ ứng dụng Modern/Metro (UWP) và Hook DLL:**
   - Để gõ được tiếng Việt trên các ứng dụng Universal Windows Platform (UWP/Metro) và các cửa sổ chạy quyền Admin, EVKey sử dụng các thư viện hook cấp thấp (`EVKey64.dll`, `EVKey32.dll`) và đăng ký tiêm (inject) vào không gian địa chỉ của các tiến trình Windows hoặc đăng ký cơ chế hỗ trợ TSF.
2. **Khởi động cùng Windows (Run Key) & Chạy quyền Administrator:**
   - EVKey thường ghi khóa tự khởi động trong `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` hoặc tạo Scheduled Task chạy với đặc quyền cao nhất (Highest Privileges).
3. **Gỡ cài đặt không sạch (Dirty Uninstall):**
   - Người dùng thường chỉ tắt EVKey trên thanh taskbar rồi xóa trực tiếp thư mục `EVKey`, hoặc uninstaller không dọn sạch các hook/DLL đang bị khóa (in-use) bởi `explorer.exe` hay `ctfmon.exe`.
   - Kết quả: Các khóa Registry đăng ký TIP, CLSID COM, hoặc đường dẫn hook vẫn còn nằm trong Registry hệ điều hành trỏ tới những file DLL không còn tồn tại trên ổ cứng.

### 2.2. Xung đột với kiến trúc In-Process COM TIP của BambooMintKey
- **BambooMintKey** là một Text Input Processor (TIP) hiện đại, hoạt động như một **In-Process COM Server** thông qua `BambooMintKey.dll` (NativeAOT), được `msctf.dll` nạp trực tiếp vào không gian bộ nhớ của từng tiến trình ứng dụng.
- Khi một ứng dụng khởi chạy và kích hoạt pipeline nhập liệu TSF:
  1. `msctf.dll` duyệt qua danh sách các TIP và Provider được đăng ký trong hệ thống (`HKLM/HKCU\Software\Microsoft\CTF\TIP`).
  2. Nếu tàn dư của EVKey vẫn còn đăng ký hoặc hook DLL bị gỡ dở dang, `msctf.dll` sẽ cố gắng gọi hoặc dispatch sự kiện phím qua các con trỏ hàm không hợp lệ (trỏ vào vùng nhớ rác hoặc DLL đã bị xóa).
  3. Khi `BambooMintKey.dll` cùng nhận sự kiện từ `ITfKeyEventSink`, sự tranh chấp tài nguyên (hook collision) hoặc lỗi bộ nhớ từ tàn dư của EVKey sẽ kích hoạt lỗi `Access Violation (0xC0000005)`, khiến toàn bộ tiến trình ứng dụng đích bị đổ vỡ (crash).

---

## 3. Phương án xử lý: Script PowerShell gỡ sạch triệt để (Khuyến nghị)

Script PowerShell đã được lưu sẵn trong repository tại: [`scripts/windows/clean-conflicting-ime.ps1`](file:///D:/Kojin/BambooMintKey/scripts/windows/clean-conflicting-ime.ps1).

Người dùng hoặc lập trình viên có thể chạy trực tiếp bằng lệnh:
```powershell
pwsh -File scripts/windows/clean-conflicting-ime.ps1
# Hoặc trên Windows PowerShell mặc định:
powershell -ExecutionPolicy Bypass -File scripts\windows\clean-conflicting-ime.ps1
```

Dưới đây là mã nguồn chi tiết của bộ script dọn dẹp:

### Script PowerShell dọn dẹp (`scripts/windows/clean-conflicting-ime.ps1`)

> **Yêu cầu:** Chạy script này trong PowerShell với quyền **Administrator**.

```powershell
<#
.SYNOPSIS
    Dọn dẹp triệt để tàn dư tiến trình, thư viện DLL và Registry của EVKey / TSF xung đột.
.DESCRIPTION
    Script thực hiện:
    1. Dừng các tiến trình EVKey và ctfmon.
    2. Gỡ bỏ Startup Run keys và Scheduled Tasks của EVKey.
    3. Dọn dẹp tàn dư Registry trong Software\EVKey.
    4. Quét và loại bỏ các TSF TIPs bị mồ côi (Orphaned TIPs trỏ tới file không tồn tại).
    5. Xóa các file/thư mục tàn dư trong AppData, Temp, ProgramFiles.
    6. Khởi động lại ctfmon để nạp lại TSF trong sạch cho BambooMintKey.
#>

# Yêu cầu quyền Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] Đang yêu cầu quyền Administrator..." -ForegroundColor Yellow
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   BAMBOOMINTKEY - BỘ CÔNG CỤ DỌN DẸP XUNG ĐỘT TSF/EVKEY   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Dừng tiến trình EVKey và các tiến trình liên quan
Write-Host "`n[Bước 1/6] Dừng các tiến trình EVKey và tạm dừng ctfmon..." -ForegroundColor Yellow
$processNames = @("EVKey", "EVKey64", "EVKey32")
foreach ($p in $processNames) {
    Get-Process -Name $p -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "  -> Đang dừng tiến trình: $($_.Name) (PID: $($_.Id))" -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force
    }
}

# Tạm dừng ctfmon để mở khóa các DLL TSF
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Xóa mục khởi động trong Registry và Task Scheduler
Write-Host "`n[Bước 2/6] Dọn dẹp Registry Startup (Run) và Task Scheduler..." -ForegroundColor Yellow
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
                Write-Host "  -> Xóa startup entry: $name tại $path" -ForegroundColor Green
                Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# Xóa Scheduled Tasks nếu có
Get-ScheduledTask -TaskPath "\" -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -match "EVKey" } | ForEach-Object {
    Write-Host "  -> Xóa Scheduled Task: $($_.TaskName)" -ForegroundColor Green
    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false
}

# 3. Dọn dẹp các Registry keys của EVKey
Write-Host "`n[Bước 3/6] Xóa cấu hình Registry của EVKey..." -ForegroundColor Yellow
$evkeyRegPaths = @(
    "HKCU:\Software\EVKey",
    "HKLM:\SOFTWARE\EVKey",
    "HKLM:\SOFTWARE\WOW6432Node\EVKey"
)
foreach ($reg in $evkeyRegPaths) {
    if (Test-Path $reg) {
        Write-Host "  -> Đang xóa Registry key: $reg" -ForegroundColor Green
        Remove-Item -Path $reg -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 4. Quét và dọn dẹp các TIP mồ côi (Orphaned TIPs) trong TSF
Write-Host "`n[Bước 4/6] Quét và dọn dẹp các TSF TIP bị mồ côi..." -ForegroundColor Yellow
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
                        Write-Host "  -> Phát hiện TSF Profile tàn dư của EVKey tại: $($_.PSPath)" -ForegroundColor Red
                        Remove-Item -Path $clsidKey -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
            }
        }
    }
}

# 5. Dọn dẹp thư mục và file tàn dư trên ổ đĩa
Write-Host "`n[Bước 5/6] Dọn dẹp file tàn dư trên ổ đĩa..." -ForegroundColor Yellow
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
        Write-Host "  -> Đang xóa tàn dư thư mục: $folder" -ForegroundColor Green
        Remove-Item -Path $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 6. Khởi động lại ctfmon để nạp sạch TSF
Write-Host "`n[Bước 6/6] Khởi động lại dịch vụ Text Services Framework (ctfmon.exe)..." -ForegroundColor Yellow
Start-Process ctfmon.exe
Start-Sleep -Milliseconds 500

Write-Host "`n[HOÀN TẤT] Quá trình dọn dẹp sạch tàn dư đã thành công!" -ForegroundColor Green
Write-Host "Bây giờ BambooMintKey có thể hoạt động ổn định mà không lo xung đột TSF." -ForegroundColor Cyan
```

---

## 4. Phương án gỡ sạch thủ công (Manual Clean Checklist)

Nếu không sử dụng script PowerShell, người dùng có thể thực hiện tuần tự theo các bước kiểm tra và gỡ bỏ dưới đây:

### Bước 1: Tắt toàn bộ tính năng can thiệp hệ thống của EVKey trước khi xóa
Nếu EVKey vẫn còn mở được:
1. Mở bảng điều khiển EVKey.
2. Bỏ chọn **"Khởi động cùng Windows"**.
3. Bỏ chọn **"Chạy với quyền Administrator"**.
4. Chuyển sang tab **Hệ thống** / **Nâng cao** $\rightarrow$ Tắt tùy chọn **"Hỗ trợ gõ trên ứng dụng Metro / Modern"** (tùy chọn này chịu trách nhiệm nạp DLL hook/TSF).
5. Nhấn **Kết thúc** để thoát hoàn toàn EVKey.

### Bước 2: Kiểm tra Task Manager
1. Nhấn `Ctrl + Shift + Esc` mở **Task Manager**.
2. Tìm trong tab **Processes** và **Details**, đảm bảo không còn tiến trình nào có tên `EVKey.exe`, `EVKey64.exe`, hoặc `EVKey32.exe`. Nếu còn, bấm chuột phải chọn **End Process Tree**.

### Bước 3: Dọn dẹp trong Registry Editor (`regedit.exe`)
1. Nhấn `Win + R`, gõ `regedit` và nhấn `Enter`.
2. Truy cập vào các đường dẫn sau và xóa các mục liên quan đến EVKey:
   - `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` $\rightarrow$ Xóa chuỗi `EVKey`.
   - `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` $\rightarrow$ Xóa chuỗi `EVKey` (nếu có).
   - `HKEY_CURRENT_USER\Software\EVKey` $\rightarrow$ Xóa toàn bộ key này.
   - `HKEY_LOCAL_MACHINE\SOFTWARE\EVKey` $\rightarrow$ Xóa toàn bộ key này (nếu có).
3. Kiểm tra mục TSF TIP:
   - `HKEY_CURRENT_USER\SOFTWARE\Microsoft\CTF\TIP`
   - `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\CTF\TIP`
   - Tìm kiếm (Ctrl + F) từ khóa `EVKey`. Nếu tìm thấy key nào chứa giá trị hoặc đường dẫn tới `EVKey*.dll`, hãy xóa key CLSID đó đi.

### Bước 4: Xóa thư mục cài đặt & AppData
1. Xóa thư mục chứa chương trình EVKey mà bạn đã giải nén/cài đặt.
2. Nhấn `Win + R`, gõ `%LocalAppData%` $\rightarrow$ Xóa thư mục `EVKey` (nếu có).
3. Nhấn `Win + R`, gõ `%AppData%` $\rightarrow$ Xóa thư mục `EVKey` (nếu có).

### Bước 5: Kiểm tra cài đặt ngôn ngữ Windows (Windows Language Settings)
1. Mở **Settings** (`Win + I`) $\rightarrow$ **Time & Language** $\rightarrow$ **Language & region**.
2. Tại mục **Tiếng Việt (Vietnamese)**, chọn dấu ba chấm `...` $\rightarrow$ **Language options**.
3. Kiểm tra danh sách bàn phím (**Installed keyboards**):
   - Đảm bảo chỉ còn lại **BambooMintKey Vietnamese Input** (và bàn phím mặc định của Windows nếu muốn).
   - Nếu thấy bàn phím lạ hoặc bàn phím liên quan đến EVKey, nhấn vào đó và chọn **Remove**.

### Bước 6: Khởi động lại hệ điều hành hoặc ctfmon
- Mở PowerShell hoặc Command Prompt và chạy:
  ```powershell
  Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
  Start-Process ctfmon
  ```
- Hoặc khởi động lại máy tính (Restart PC) để bảo đảm mọi handle DLL được giải phóng hoàn toàn khỏi các tiến trình hệ thống.

---

## 5. Đề xuất cải tiến cho BambooMintKey trong tương lai

1. **Thêm công cụ tự chẩn đoán trong `BambooMintKey.UI`:**
   - Trong màn hình Settings hoặc About của BambooMintKey, có thể bổ sung một nút bấm **"Chẩn đoán & Dọn dẹp xung đột bộ gõ"**. Nút này sẽ gọi lệnh quét Registry TSF và cảnh báo người dùng nếu phát hiện các TIP mồ côi hoặc hook của EVKey/Unikey còn sót lại.
2. **Kiểm tra trong `scripts/windows/test-register.ps1`:**
   - Trong script đăng ký dành cho lập trình viên, có thể bổ sung một hàm `Check-ConflictingImeTips` để cảnh báo lập trình viên trước khi tiến hành test TIP.
