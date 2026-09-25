<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# BambooMintKey Linux (Fcitx5) Progress Tracking

**Cập nhật:** 2026-09-26  
**Giai đoạn:** Phase 7 — Chuẩn bị & Triển khai nền tảng Linux / Fcitx5  
**Trạng thái chung:** 🛠️ Hoàn thành Milestone 1 & Milestone 2, chuẩn bị triển khai Milestone 3 (M3: `BambooMintKey.UI.Linux`)  
**Tài liệu tham chiếu:**
- Điều tra khả thi: [007_01_InvestigationForLinux.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_01_InvestigationForLinux.md)
- Lộ trình tổng thể: [007_002_Roadmap.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_002_Roadmap.md)
- Thiết kế C-ABI: [007_03_CoreNative_CABI_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_03_CoreNative_CABI_Design.md)
- Thiết kế Fcitx5 Addon: [007_04_Fcitx5_Addon_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_04_Fcitx5_Addon_Design.md)
- Thiết kế UI Linux: [007_05_UILinux_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_05_UILinux_Design.md)
- Kế hoạch E2E Test: [007_06_E2E_TestPlan_and_Delivery.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_06_E2E_TestPlan_and_Delivery.md)

---

## 1. Tổng Quan Tiến Độ Các Milestone

| Milestone | Tên Hạng Mục | Trọng Số | Trạng Thái | Tiến Độ (%) | Ghi Chú |
|:---:|---|:---:|:---:|:---:|---|
| **M0** | **Tài Liệu Thiết Kế Kỹ Thuật & Test Matrix** | 15% | ✅ Hoàn thành | 100% | 4 tài liệu đặc tả: C-ABI, Addon & D-Bus, UI, E2E Test |
| **M1** | **`BambooMintKey.Core.Native` (C# NativeAOT)** | 25% | ✅ Hoàn thành | 100% | M1.1-M1.7 hoàn thành (full C-ABI + test matrix 9/9 PASS) |
| **M2** | **`BambooMintKey.Fcitx5` Addon (C++/D-Bus)** | 30% | ✅ Hoàn thành | 100% | M2.1-M2.8 hoàn thành (C++ Addon, D-Bus, file watcher, build & test) |
| **M3** | **`BambooMintKey.UI.Linux` (Avalonia F#)** | 18% | ⏳ Chuẩn bị triển khai | 0% | GUI cấu hình chuẩn XDG, D-Bus client, single instance |
| **M4** | **Kiểm Thử E2E & Đóng Gói (Delivery)** | 12% | ⏳ Chờ M3 | 0% | Test Wayland/X11, script cài đặt `install_linux.sh` |
| **Tổng** | **Toàn bộ Phase 7 (Linux / Fcitx5)** | **100%** | 🛠️ **Đang triển khai** | **70%** | |

---

## 2. Checklist Chi Tiết Từng Đầu Việc

### 🎯 Milestone 0: Hoàn Thiện Các Tài Liệu Thiết Kế Kỹ Thuật (Design Specs & Test Matrix)
> **Mục tiêu:** Xây dựng đầy đủ tài liệu đặc tả kiến trúc, hợp đồng giao tiếp (API contract), và danh mục các ca kiểm thử chi tiết trước khi tiến hành viết code.

- [x] **M0.1 — Thiết kế C-ABI & Quản Lý Đa Context ([007_03_CoreNative_CABI_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_03_CoreNative_CABI_Design.md))**
  - [x] Thiết kế cấu trúc struct `EngineContext` (lưu `WordState`, `EngineConfig`, buffers UTF-8).
  - [x] Quy định chuẩn calling convention `cdecl`, danh sách hàm C-ABI export.
  - [x] Hợp đồng sở hữu bộ nhớ (Memory Ownership): Caller không cần `free` chuỗi UTF-8 trả về.
  - [x] Lập ma trận kiểm thử Unit Test C-ABI (`TC-CABI-01` đến `TC-CABI-08`): Lifecycle, Telex, Backspace, WordBreak, English Restore, Context Isolation, Memory Stress Test.

- [x] **M0.2 — Thiết kế Fcitx5 Addon, D-Bus V/E & File Watcher ([007_04_Fcitx5_Addon_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_04_Fcitx5_Addon_Design.md))**
  - [x] Thiết kế kiến trúc `BambooMintKeyEngine` kế thừa `InputMethodEngine` và `BambooMintKeyState : InputContextProperty`.
  - [x] Thiết kế pipeline xử lý phím `keyEvent`, Preedit UI gạch chân và commit string.
  - [x] Đặc tả D-Bus Service `org.fcitx.Fcitx5.BambooMintKey`: Method `SetVietnameseMode(bool)`, `GetVietnameseMode() -> bool`, Signal `ModeChanged(bool)`.
  - [x] Thiết kế cơ chế File Watcher `inotify` theo dõi `$XDG_CONFIG_HOME/bamboomintkey/config.json`.
  - [x] Lập ma trận kiểm thử Fcitx5 (`TC-FCITX-01` đến `TC-FCITX-07`): Addon Load, Shortcut PassThrough, Inline Preedit, Commit, Focus Switch, Dynamic Config Reload, D-Bus V/E Sync.

- [x] **M0.3 — Thiết kế Avalonia UI Linux, XDG & D-Bus Client ([007_05_UILinux_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_05_UILinux_Design.md))**
  - [x] Đặc tả JSON schema cấu hình chuẩn FreeDesktop XDG.
  - [x] Cơ chế ghi file cấu hình Atomic (ghi file `.tmp` -> `rename`).
  - [x] Cơ chế Single Instance POSIX qua Unix Domain Socket (`$XDG_RUNTIME_DIR/bamboomintkey-ui.sock`) hoặc `flock`.
  - [x] Thiết kế D-Bus Client kết nối tới Addon để gửi lệnh toggle V/E và nhận signal `ModeChanged`.
  - [x] Thiết kế tab Gõ thử nghiệm tích hợp trực tiếp F# Core.
  - [x] Lập ma trận kiểm thử UI (`TC-UI-01` đến `TC-UI-06`): XDG Path, Atomic Save, Single Instance, Live Test Tab, D-Bus Signal Update, Desktop Launcher.

- [x] **M0.4 — Kế Hoạch Kiểm Thử E2E & Kịch Bản Cài Đặt ([007_06_E2E_TestPlan_and_Delivery.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_06_E2E_TestPlan_and_Delivery.md))**
  - [x] Ma trận kiểm thử tương thích môi trường hiển thị: Wayland vs X11.
  - [x] Ma trận ứng dụng mục tiêu: GTK (Firefox), Qt (Telegram), Chromium/Electron (Chrome, VS Code), Terminal (Alacritty, Kitty).
  - [x] Kịch bản ngữ pháp ngôn ngữ (`LNG-01` đến `LNG-08`).
  - [x] Kịch bản script tự động hóa `install_linux.sh` và `uninstall_linux.sh`.
  - [x] Lập ma trận kiểm thử E2E (`TC-E2E-01` đến `TC-E2E-07`).

---

### 🎯 Milestone 1: Xây Dựng `BambooMintKey.Core.Native` (C# NativeAOT C-ABI)
> **Mục tiêu:** Đóng gói lõi F# thành thư viện `libBambooMintKeyCore.so` xuất các hàm C-ABI chuẩn, hỗ trợ đa context độc lập cho từng `InputContext` của Fcitx5.

- [x] **M1.1 — Khởi tạo Project & Cấu hình NativeAOT**
  - [x] Tạo thư mục `src/BambooMintKey.Core.Native/`.
  - [x] Tạo `BambooMintKey.Core.Native.csproj` nhắm mục tiêu `net10.0`, `PublishAot=true`, `NativeLib=Shared`, `AllowUnsafeBlocks=true`.
  - [x] Thêm tham chiếu đến `src/BambooMintKey.Core/BambooMintKey.Core.fsproj`.
  - [x] Cấu hình cờ tối ưu hóa NativeAOT (StripSymbols, InvariantGlobalization nếu cần).
  - [x] Xác minh lệnh `dotnet publish -c Release -r linux-x64` sinh thành công file `.so` (export `bmk_version`).

- [x] **M1.2 — Thiết kế Đối Tượng Context (`EngineContext`)**
  - [x] Định nghĩa class `EngineContext` đại diện cho 1 phiên gõ độc lập.
  - [x] Lưu trữ trường `Types.WordState` của F# bên trong context.
  - [x] Lưu trữ trường `EngineConfig.EngineConfig` cấu hình gõ của context.
  - [x] Khởi tạo bộ đệm UTF-8 nội bộ (`PreeditBuffer`, `CommitBuffer`) để cấp phát chuỗi an toàn, không bị giải phóng ngoài ý muốn.
  - [x] Cơ chế Thread-Safety (lock nhẹ per-context).

- [x] **M1.3 — Triển khai C-ABI Vòng Đời Context (Lifecycle)**
  - [x] `bmk_context_create()`: Cấp phát mới một `EngineContext`, giữ alive qua GCHandle và trả về `IntPtr` handle.
  - [x] `bmk_context_free(IntPtr handle)`: Giải phóng buffer unmanaged và thu hồi GCHandle.
  - [x] `bmk_context_reset(IntPtr handle)`: Đặt lại `WordState` về rỗng và xóa buffer.

- [x] **M1.4 — Triển khai C-ABI Xử Lý Phím (Key Processing)**
  - [x] `bmk_process_key(IntPtr handle, uint unicodeChar)`: Nhận code point Unicode, gọi `TelexEngine.processKey`, trả về mã action (`0/1/2/3`).
  - [x] `bmk_process_backspace(IntPtr handle)`: Nhận phím Backspace, cập nhật âm tiết lùi hoặc hoàn tác.
  - [x] `bmk_process_wordbreak(IntPtr handle, uint breakChar)`: Nhận dấu cách/Enter/dấu câu để commit từ hiện tại.

- [x] **M1.5 — Triển khai C-ABI Trích Xuất Dữ Liệu UTF-8**
  - [x] `bmk_get_preedit_text(IntPtr handle)`: Trả về `byte*` trỏ tới chuỗi UTF-8 preedit (caller không `free`).
  - [x] `bmk_get_commit_text(IntPtr handle)`: Trả về `byte*` trỏ tới chuỗi UTF-8 commit đã chốt.
  - [x] `bmk_get_preedit_length(IntPtr handle)`: Trả về độ dài chuỗi preedit (số byte).

- [x] **M1.6 — Triển khai C-ABI Cấu Hình Runtime**
  - [x] `bmk_set_options(IntPtr handle, int isEnabled, int toneStyle, int autoRestore, int allowRepeatUndo, int allowLeadingW, int freeTone)`: Cập nhật nhanh các tùy chọn gõ.
  - [x] `bmk_load_config_json(IntPtr handle, byte* jsonUtf8)`: Nạp cấu hình từ chuỗi JSON XDG (0 thành công / -1 lỗi).

- [x] **M1.7 — Kiểm Thử Độc Lập Thư Viện C-ABI Theo Test Matrix `TC-CABI-01` -> `08`**
  - [x] Viết test runner console (script Python `ctypes`) gọi trực tiếp `BambooMintKeyCore.so`.
  - [x] Chạy và verify toàn bộ test case `TC-CABI-01` đến `TC-CABI-08` (đã build + chạy thành công 9/9 PASS).
  - [x] Kiểm tra rò rỉ bộ nhớ (Memory Leak) qua 10.000 lượt tạo/hủy context (đã xác minh 0 context tồn dư).

---

### 🎯 Milestone 2: Phát Triển `BambooMintKey.Fcitx5` Addon (C++/CMake/D-Bus)
> **Mục tiêu:** Xây dựng plugin Fcitx5 đón sự kiện bàn phím từ hệ điều hành, gắn kết với `libBambooMintKeyCore.so`, triển khai D-Bus service điều khiển V/E và file watcher.

- [x] **M2.1 — Cấu Trúc Dự Án CMake & Khai Báo Addon**
  - [x] Tạo thư mục `src/BambooMintKey.Fcitx5/`.
  - [x] Tạo `CMakeLists.txt` tìm kiếm các package Fcitx5 (`Fcitx5Core`, `Fcitx5Config`, `Fcitx5Utils`).
  - [x] Cấu hình link `BambooMintKeyCore.so` qua `target_link_libraries` và thiết lập `rpath`.
  - [x] Tạo `bamboomintkey-addon.conf.in` và `bamboomintkey.conf.in`.

- [x] **M2.2 — Quản Lý State Theo Context (`BambooMintKeyState`)**
  - [x] Định nghĩa class `BambooMintKeyState : public fcitx::InputContextProperty`.
  - [x] Constructor: gọi `bmk_context_create()` lưu `handle` riêng cho mỗi `InputContext`.
  - [x] Destructor: gọi `bmk_context_free(handle)` để giải phóng bộ nhớ context khi ứng dụng đóng.
  - [x] Triển khai phương thức `reset()`: gọi `bmk_context_reset(handle)` và xóa preedit panel.

- [x] **M2.3 — Triển Khai Engine Lõi (`BambooMintKeyEngine`)**
  - [x] Kế thừa `fcitx::InputMethodEngine` và `fcitx::AddonInstance`.
  - [x] Quản lý trạng thái V/E tập trung (Single-owner).
  - [x] Triển khai các hàm `activate()`, `deactivate()`, `reset()`.

- [x] **M2.4 — Xử Lý Luồng Sự Kiện Bàn Phím (`keyEvent`)**
  - [x] Bỏ qua sự kiện nhả phím (`keyEvent.isRelease()`).
  - [x] Kiểm tra các modifier hệ thống (`Ctrl`, `Alt`, `Super`): Cho qua (`PassThrough`).
  - [x] Phân loại phím sang `bmk_process_backspace`, `bmk_process_wordbreak` hoặc `bmk_process_key`.
  - [x] Cập nhật giao diện: `filterAndAccept()`, `setPreedit()`, `commitString()`.

- [x] **M2.5 — Định Dạng Hiển Thị Preedit UI**
  - [x] Thiết lập gạch chân (Underline format) cho vùng đang composition.
  - [x] Hỗ trợ chế độ client preedit và cập nhật vị trí con trỏ chuột.

- [x] **M2.6 — Triển Khai D-Bus Service Cho Trạng Thái V/E**
  - [x] Đăng ký D-Bus Service `org.fcitx.Fcitx5.BambooMintKey` trên Session Bus.
  - [x] Triển khai D-Bus Method `SetVietnameseMode(bool)`.
  - [x] Triển khai D-Bus Method `GetVietnameseMode() -> bool`.
  - [x] Triển khai D-Bus Signal `ModeChanged(bool)`.
  - [x] Khi phím tắt hoặc UI chuyển mode: cập nhật state nội bộ và phát signal `ModeChanged` tức thì.

- [x] **M2.7 — Đồng Bộ Cấu Hình Ít Đổi Qua File Watcher (`inotify`)**
  - [x] Theo dõi thư mục `$XDG_CONFIG_HOME/bamboomintkey/`.
  - [x] Bắt sự kiện `IN_CLOSE_WRITE` trên `config.json` để reload các cấu hình (toneStyle, charset, autoRestore...).

- [x] **M2.8 — Kiểm Thử Addon Theo Test Matrix `TC-FCITX-01` -> `07`**
  - [x] Kiểm tra hoạt động gõ, phím tắt, chuyển đổi focus, D-Bus sync và config reload.

---

### 🎯 Milestone 3: Xây Dựng `BambooMintKey.UI.Linux` (Avalonia Settings GUI)
> **Mục tiêu:** Cung cấp ứng dụng cài đặt giao diện Avalonia hiện đại trên Linux, hoàn toàn độc lập với code Windows, tích hợp D-Bus client và lưu cấu hình chuẩn XDG.

- [ ] **M3.1 — Khởi Tạo Project Avalonia Linux**
  - [ ] Tạo thư mục `src/BambooMintKey.UI.Linux/`.
  - [ ] Tạo `BambooMintKey.UI.Linux.fsproj` (Target `net10.0`, Avalonia 11.x).
  - [ ] Thêm tham chiếu đến `src/BambooMintKey.Core/BambooMintKey.Core.fsproj`.

- [ ] **M3.2 — Triển Khai Quản Lý Cấu Hình Chuẩn XDG (`SharedConfig.fs`)**
  - [ ] Lấy đường dẫn `$XDG_CONFIG_HOME` (fallback `~/.config`).
  - [ ] Cơ chế ghi file Atomic (ghi `.tmp` -> `rename`) vào `~/.config/bamboomintkey/config.json`.

- [ ] **M3.3 — Cơ Chế Single Instance Chuẩn POSIX**
  - [ ] Triển khai Unix Domain Socket (`$XDG_RUNTIME_DIR/bamboomintkey-ui.sock`) hoặc `flock`.
  - [ ] Đưa cửa sổ đã mở lên trước màn hình (`BringToFront`) khi nhận tín hiệu từ instance mới.

- [ ] **M3.4 — Tích Hợp D-Bus Client Cho Trạng Thái V/E**
  - [ ] Kết nối tới Session Bus dịch vụ `org.fcitx.Fcitx5.BambooMintKey`.
  - [ ] Khi người dùng toggle V/E trên UI: gọi D-Bus method `SetVietnameseMode`.
  - [ ] Lắng nghe signal `ModeChanged`: tự động cập nhật checkbox/icon trên UI tức thì khi người dùng bấm phím tắt bên ngoài.

- [ ] **M3.5 — Hoàn Thiện Các Tab Giao Diện Cài Đặt**
  - [ ] Tab Cơ bản, Nâng cao, Phím tắt, Macro.
  - [ ] Tab Gõ thử nghiệm trực tiếp kết nối với F# Core.

- [ ] **M3.6 — Tích Hợp Hệ Thống Desktop Linux**
  - [ ] Tạo tệp `bamboomintkey-settings.desktop` và icon SVG/PNG thương hiệu.

- [ ] **M3.7 — Kiểm Thử UI Theo Test Matrix `TC-UI-01` -> `06`**
  - [ ] Xác minh toàn bộ các ca kiểm thử XDG, atomic save, single instance, live test và D-Bus sync.

---

### 🎯 Milestone 4: Kiểm Thử E2E & Đóng Gói (Delivery)
> **Mục tiêu:** Xác minh hoạt động ổn định trên các môi trường hiển thị Linux và cung cấp công cụ cài đặt một chạm.

- [ ] **M4.1 — Kiểm Thử Gõ Thực Tế Trên Các Môi Trường Display Server**
  - [ ] Kiểm thử trên **Wayland** (GNOME Wayland, KDE Plasma Wayland).
  - [ ] Kiểm thử trên **X11** (Xorg tiêu chuẩn, XFCE, Cinnamon).

- [ ] **M4.2 — Kiểm Thử Trên Các Ứng Dụng Phổ Biến**
  - [ ] Web Browser: Chrome, Firefox, Edge.
  - [ ] IDE/Editor: VS Code, Rider, CLion, Sublime Text.
  - [ ] Terminal: GNOME Terminal, Alacritty, Kitty.
  - [ ] Office & Chat: LibreOffice, Telegram, Discord.

- [ ] **M4.3 — Kiểm Thử Ngữ Pháp Tiếng Việt Nâng Cao**
  - [ ] Kiểm thử từ ghép, bỏ dấu tự do, khôi phục từ tiếng Anh, xóa lùi Backspace.

- [ ] **M4.4 — Xây Dựng Script Cài Đặt Tự Động (`scripts/install_linux.sh`)**
  - [ ] Tự động build NativeAOT -> Fcitx5 addon -> UI Linux.
  - [ ] Cài đặt vào `~/.local/` và restart Fcitx5 daemon (`fcitx5 -r -d`).

- [ ] **M4.5 — Tạo Script Gỡ Cài Đặt (`scripts/uninstall_linux.sh`) & Đóng Gói Phân Phối**
  - [ ] Script dọn dẹp sạch sẽ tài nguyên đã cài đặt.
  - [ ] Tạo file nén portable `.tar.gz` hoặc kịch bản đóng gói `.deb`.

---

## 3. Nhật Ký Tiến Độ Triển Khai (Progress Log)

| Ngày | Milestone | Hạng Mục / Task | Nội Dung Thực Hiện | Trạng Thái |
|:---:|:---:|---|---|:---:|
| 2026-09-26 | Phase 7 | Khởi tạo tài liệu | Nghiên cứu khả thi [007_01](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_01_InvestigationForLinux.md), lập roadmap [007_002](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_002_Roadmap.md) và thiết lập checklist theo dõi tiến độ [002_LinuxProgressTracking.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/4.Progress/002_LinuxProgressTracking.md). | ✅ Hoàn thành |
| 2026-09-26 | Phase 7 | Cập nhật kiến trúc D-Bus | Cập nhật cơ chế đồng bộ 3 kênh (D-Bus cho V/E real-time, inotify cho config ít đổi, JSON cho persistence) và bổ sung Milestone 0 (Design Specs & Test Matrix). | ✅ Hoàn thành |
| 2026-09-26 | M0 | M0.1 - M0.4 | **Hoàn thành toàn bộ Milestone 0**: Soạn thảo 4 tài liệu thiết kế kỹ thuật chi tiết ([007_03](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_03_CoreNative_CABI_Design.md), [007_04](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_04_Fcitx5_Addon_Design.md), [007_05](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_05_UILinux_Design.md), [007_06](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_06_E2E_TestPlan_and_Delivery.md)) kèm đầy đủ ma trận kiểm thử và tiêu chuẩn Pass/Fail. | ✅ Hoàn thành |
| 2026-09-26 | M1 | M1.1 | Khởi tạo project `src/BambooMintKey.Core.Native` (C# NativeAOT): tạo `BambooMintKey.Core.Native.csproj` (`net10.0`, `PublishAot`, `NativeLib=Shared`, `AllowUnsafeBlocks`), tham chiếu F# Core, cờ `StripSymbols`/`InvariantGlobalization`, thêm `Exports.cs` (export `bmk_version`). Đã publish thành công `BambooMintKeyCore.so` và xác minh symbol `bmk_version@@V1.0`. | ✅ Hoàn thành |
| 2026-09-26 | M1 | M1.2 | Định nghĩa `EngineContext` (`src/BambooMintKey.Core.Native/EngineContext.cs`): chứa `Types.WordState` + `EngineConfig.EngineConfig`, hai bộ đệm UTF-8 cố định 256 byte (`PreeditBuffer`/`CommitBuffer` kèm length), lock nhẹ per-context, và `Reset()`. | ✅ Hoàn thành |
| 2026-09-26 | M1 | M1.3 - M1.6 | Hoàn thành toàn bộ giao diện C-ABI trong `Exports.cs` + `EngineContext.cs`: lifecycle (`bmk_context_create/free/reset` qua GCHandle), xử lý phím (`bmk_process_key/backspace/wordbreak`), trích xuất buffer UTF-8 (`bmk_get_preedit_text/commit_text/preedit_length`), cấu hình (`bmk_set_options` + `bmk_load_config_json` với JSON parser tối giản). Bộ đệm dùng `NativeMemory` để con trỏ `byte*` ổn định. | ✅ Hoàn thành |
| 2026-09-26 | M1 | M1.7 | Viết test runner `scripts/test-cabi.py` (Python ctypes) chạy TC-CABI-01 -> 08 + bonus config test. Đã build + chạy thành công **9/9 PASS** (kèm thêm `bmk_gc_collect` + `bmk_get_live_context_count` để đo rò rỉ context chính xác). | ✅ Hoàn thành |
| 2026-09-26 | M2 | M2.1 - M2.8 | Hoàn thành toàn bộ Milestone 2: Xây dựng addon Fcitx5 C++, CMake build, C-ABI bridge, context state, D-Bus service (V/E mode toggle + signals), inotify file watcher. Đã build, cài đặt addon vào Fcitx5 và kiểm thử thành công (TC-FCITX-01 -> 07). | ✅ Hoàn thành |

---

## 4. Quản Lý Bug / Vấn Đề Kỹ Thuật Phát Sinh (Linux Issues)

| Issue # | Mô Tả Vấn Đề | Module | Mức Độ | Trạng Thái | Giải Pháp / File Liên Quan |
|:---:|---|:---:|:---:|:---:|---|
| *Chưa có* | — | — | — | — | — |
