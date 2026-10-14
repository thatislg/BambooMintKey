ơ

# Hướng Dẫn Chạy Benchmark Bộ Gõ Đa Nền Tảng (Windows, Linux, macOS)

Tài liệu này cung cấp hướng dẫn từng bước (Step-by-step) để chạy đầy đủ cả **Tầng 1 (Engine & Corpus)** và **Tầng 2 (Real App Integration)** trên cả 3 hệ điều hành: **Windows**, **Linux**, và **macOS**.

---

## 1. Tổng Quan Kiến Trúc Kiểm Thử

```
┌────────────────────────────────────────────────────────────────────────┐
│             HỆ THỐNG KIỂM THỬ ĐA NỀN TẢNG (3 HỆ ĐIỀU HÀNH)             │
├───────────────────────────────────┬────────────────────────────────────┤
│   TẦNG 1: ENGINE & CORPUS (F#)    │   TẦNG 2: REAL APP (SEND INPUT)    │
│   (Chạy in-memory, đa nền tảng)   │   (Bắn phím OS vào app thật)       │
├───────────────────────────────────┼────────────────────────────────────┤
│ • Windows: dotnet test            │ • Windows: SendInput (PowerShell)  │
│ • Linux:   dotnet test            │ • Linux:   xdotool / ydotool (Py)  │
│ • macOS:   dotnet test            │ • macOS:   Quartz / AppleScript    │
└───────────────────────────────────┴────────────────────────────────────┘
```

---

## 2. Hướng Dẫn Tầng 1: Engine Golden Test & Corpus 10 Nấc

> [!NOTE]
> Do lõi engine F# (`BambooMintKey.Core`) là .NET 10 thuần hàm (Pure Functional), các bài test Tầng 1 chạy **giống hệt nhau 100%** trên Windows, Linux và macOS mà không cần cài đặt thêm bất kỳ thư viện UI hay hook hệ thống nào.

### 2.1. Yêu cầu tiên quyết

- Đã cài đặt [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

### 2.2. Câu lệnh thực thi (Dùng chung cho cả 3 OS)

Mở Terminal (Linux/macOS) hoặc PowerShell (Windows) tại thư mục gốc repository:

#### Bài test 1: Bộ Golden Test Suite (84 test cases đại diện cho 5 nhóm cốt lõi)

```bash
dotnet test tests/BambooMintKey.Core.Tests --filter "GoldenBenchmarkTests" --logger "console;verbosity=detailed"
```

*Kết quả xuất ra bảng ma trận % đúng theo từng nhóm: `tone_style`, `shortcuts`, `undo_recovery`, `english`, `corpus`.*

#### Bài test 2: Bộ Stress-Test Corpus 10 nấc (1.000 $\rightarrow$ 10.000 từ vựng)

```bash
dotnet test tests/BambooMintKey.Core.Tests --filter "CorpusTierBenchmarkTests" --logger "console;verbosity=detailed"
```

*Kết quả xuất ra bảng đo đạc hiệu năng: % chính xác, thời gian (ms), độ trễ (µs/từ), throughput (từ/giây) và RAM delta qua 10 nấc.*

---

## 3. Hướng Dẫn Tầng 2: Kiểm Thử Trên Ứng Dụng Thật (Real App)

Tầng 2 là kiểm thử hộp đen (Black-box), cho phép đo **bất kỳ bộ gõ nào** (BambooMintKey, UniKey, EVKey, OpenKey, Fcitx5-Bamboo, IBus-Bamboo...).

---

### 3.1. Hướng Dẫn Trên Windows 🪟

#### Công cụ sử dụng:

Script PowerShell tích hợp Win32 `SendInput` API:
[scripts/benchmark/tier2_sendinput_runner.ps1](file:///d:/Kojin/BambooMintKey/scripts/benchmark/tier2_sendinput_runner.ps1)

#### Các bước thực hiện:

1. Bật bộ gõ bạn muốn kiểm thử (ví dụ: BambooMintKey, UniKey, EVKey) ở chế độ **Telex** và bảng mã **Unicode**.
2. Mở PowerShell với quyền User bình thường:

```powershell
# Chạy thử 1.000 từ trên Notepad (tốc độ 25 phím/giây)
.\scripts\benchmark\tier2_sendinput_runner.ps1 -App notepad -WordCount 1000 -DelayMs 40

# Chạy thử 1.000 từ trên VS Code (tốc độ 20 phím/giây)
.\scripts\benchmark\tier2_sendinput_runner.ps1 -App vscode -WordCount 1000 -DelayMs 50
```

3. Giữ nguyên bàn phím và chuột trong quá trình chạy. Runner sẽ tự động mở app, bắn phím, copy text kết quả và in bảng báo cáo độ đúng %, tốc độ WPM, và danh sách các từ bị nuốt/đảo ký tự nếu có.

---

### 3.2. Hướng Dẫn Trên Linux 🐧 (Ubuntu, Fedora, Arch...)

#### Công cụ sử dụng:

Script Python đa năng hỗ trợ cả X11 và Wayland:
[scripts/benchmark/tier2_linux_runner.py](file:///d:/Kojin/BambooMintKey/scripts/benchmark/tier2_linux_runner.py)

#### Cài đặt gói hỗ trợ bắn phím:

- **Nếu dùng X11 (Ubuntu mặc định phiên bản cũ, Mint, XFCE):**
  ```bash
  sudo apt install xdotool xclip python3     # Ubuntu/Debian
  sudo dnf install xdotool xclip python3     # Fedora
  ```
- **Nếu dùng Wayland (Ubuntu 22.04+, Fedora Workstation):**
  ```bash
  sudo apt install ydotool wl-clipboard python3   # Ubuntu/Debian
  sudo dnf install ydotool wl-clipboard python3   # Fedora
  ```

#### Các bước thực hiện:

1. Bật bộ gõ Linux (Fcitx5 với Bamboo/Unikey hoặc BambooMintKey Linux).
2. Chạy lệnh benchmark trong Terminal:

```bash
# Thử 1.000 từ trên trình soạn thảo Gedit
python3 scripts/benchmark/tier2_linux_runner.py --app gedit --words 1000 --delay 0.04

# Thử 1.000 từ trên VS Code
python3 scripts/benchmark/tier2_linux_runner.py --app vscode --words 1000 --delay 0.05
```

---

### 3.3. Hướng Dẫn Trên macOS 🍎 (Sonoma, Sequoia, Apple Silicon & Intel)

#### Công cụ sử dụng:

Script Python tích hợp AppleScript & Quartz Event Tap:
[scripts/benchmark/tier2_macos_runner.py](file:///d:/Kojin/BambooMintKey/scripts/benchmark/tier2_macos_runner.py)

#### Cấp quyền Accessibility (Bắt buộc trên macOS):

Khi chạy lần đầu, macOS sẽ yêu cầu quyền gửi sự kiện bàn phím:

1. Vào **System Settings** $\rightarrow$ **Privacy & Security** $\rightarrow$ **Accessibility**.
2. Bật toggle cho **Terminal** (hoặc iTerm2 / VS Code).

#### Các bước thực hiện:

1. Bật bộ gõ tiếng Việt trên macOS (Bộ gõ mặc định macOS Telex, BambooMintKey macOS, EVKey macOS, OpenKey...).
2. Mở Terminal và chạy lệnh:

```bash
# Thử 1.000 từ trên TextEdit
python3 scripts/benchmark/tier2_macos_runner.py --app textedit --words 1000 --delay 0.04

# Thử 1.000 từ trên Visual Studio Code
python3 scripts/benchmark/tier2_macos_runner.py --app vscode --words 1000 --delay 0.05
```

---

## 4. Cách Sử Dụng Benchmark Này Cho Các Bộ Gõ Khác

Để kiểm tra và so sánh khách quan với các bộ gõ khác trong cộng đồng:

1. **Với Tầng 2:**
   - Không cần thay đổi mã nguồn! Chỉ cần chuyển bộ gõ active trên hệ điều hành sang **UniKey**, **EVKey**, **OpenKey**, **IBus-Bamboo**... và chạy lệnh runner Tầng 2 tương ứng ở trên.
   - Kết quả xuất ra sẽ phản ánh chính xác độ tương thích và tỷ lệ nuốt phím của bộ gõ đó trên cùng một môi trường máy.
2. **Với Tầng 1:**
   - Sử dụng chung file dữ liệu [docs/6.Benchmark/cases/tier1_golden.cases](file:///d:/Kojin/BambooMintKey/docs/6.Benchmark/cases/tier1_golden.cases).
   - Tác giả các bộ gõ khác chỉ cần viết 1 hàm bọc nhỏ (Wrapper) gọi hàm `ProcessKey` của họ để so sánh độ đúng chính tả in-memory.
