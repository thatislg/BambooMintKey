<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Walkthrough 005: Hoàn Thiện 100% Cơ Chế Chuyển Đổi V/E Theo Chuẩn Google Japanese Input (Mozc / TSF Native) & Khóa Phím Tắt Cố Định Dưới Esc

**Mã tài liệu:** `005_Walkthrough`  
**Ngày cập nhật:** 2026-09-17  
**Liên quan:** 
- `docs/3.Issue/005_ShortcutKeyAutoResetError.md` (Issue báo lỗi ban đầu)
- `docs/3.Issue/005_01_Mozc_TSF_Mode_Activation.md` (Tài liệu thiết kế kiến trúc chuẩn Mozc TSF)

---

## 1. Mục tiêu đã hoàn thành

1. **Khóa cứng phím tắt chuyển đổi V/E cố định theo vị trí phím vật lý dưới Esc:**
   - **Bàn phím tiếng Anh (US):** Phím `` ` `` / `~` (`VK_OEM_3` = `0xC0`) bấm trực tiếp, tổ hợp `Alt + ~` (chuẩn Google Japanese Input trên bàn phím US) và `Ctrl + ~`. (Vẫn bảo toàn thao tác `Shift + ~` để gõ ký tự dấu ngã `~` khi soạn thảo văn bản).
   - **Bàn phím tiếng Nhật (JIS):** Phím 半角/全角 / 漢字 (`VK_KANJI` = `0x19`, `VK_OEM_AUTO` = `0xF3`, `VK_OEM_ENLW` = `0xF4`) với cờ `TsfModFlags.IgnoreAllModifier` (`0x0400`).
2. **Khóa hoàn toàn ở tầng UI và Cấu hình (Zero Auto-Reset):**
   - Không cho phép người dùng thay đổi hoặc gán phím tắt tùy biến nữa.
   - Bảng điều khiển cài đặt hiển thị thông tin phím tắt cố định rõ ràng, loại bỏ toàn bộ các nút bấm và sự kiện ghi nhận phím tắt gây lỗi tự reset.
   - Các hàm lưu trữ cấu hình `SharedConfig.fs` và `SharedMemoryManager.cs` luôn ghi nhận preset phím tắt mặc định cố định.
3. **Trị dứt điểm lỗi "hoạt động không kiểm soát được bật tắt V/":**
   - **Phát hiện từ Runtime Log:** Khi chuyển cửa sổ hoặc một ứng dụng mất focus (`focus=0`), Windows TSF tự chuyển compartment `OpenClose` của thread đó về `0` (False). Trước đây `OnCompartmentChanged` đọc `isOpen = false` và ngay lập tức ghi đè `SharedMemoryManager.IsVietnameseMode` thành `False`, đồng thời kích hoạt vòng lặp gọi đệ quy (echo cascade).
   - **Cách xử lý:** 
     + Thêm cờ `[ThreadStatic] public static bool IsInternalCompartmentSync;` để chặn 100% các tín hiệu echo nội bộ.
     + Thêm cờ `_hasFocus = (pdimFocus != IntPtr.Zero);`. Khi thread mất focus, `OnCompartmentChanged` sẽ bỏ qua, không bao giờ ghi đè làm tắt tiếng Việt toàn cục.

---

## 2. Chi tiết các sửa đổi cốt lõi

### 2.1. Tầng TSF Native & PreservedKey
- **[KeyEventSinkHelper.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkHelper.cs)**:
  - Cố định hàm `GetActiveToggleKeys()` luôn trả về danh sách các phím chuẩn: `VK_OEM_3` (0, Alt, Ctrl), `VK_KANJI`, `VK_OEM_AUTO`, `VK_OEM_ENLW`.
  - Cập nhật `AllPossibleKeys` bao gồm toàn bộ các phím này để giải phóng sạch sẽ khi Deactivate.
- **[KeyEventSinkImpl.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs)**:
  - Loại bỏ hoàn toàn kiểm tra phím tắt thủ công trong `OnTestKeyDown` và `OnKeyDown`.
  - Điểm chuyển đổi duy nhất là callback `OnPreservedKey` được Windows TSF gọi trực tiếp.
- **[KeyInputTranslator.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Interop/KeyInputTranslator.cs)**:
  - Loại bỏ hàm quét phím tắt kiểu Unikey `IsToggleHotkeyPressed` và `IsKeyDown`.
- **[Guids.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/Guids.cs)**:
  - Sửa `IidITfCompartmentEventSink` thành `743ABD5F-F26D-48DF-8CC5-238492419B64` (chuẩn SDK `msctf.h`).

### 2.2. Xử lý Vòng lặp Echo & Focus Churn
- **[BambooMintKeyTextService.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs)**:
  - Khai báo `[ThreadStatic] public static bool IsInternalCompartmentSync;` và biến cờ `_hasFocus`.
  - Trong `OnCompartmentChanged`: Bỏ qua nếu `IsInternalCompartmentSync == true` hoặc `!_hasFocus`.
  - Trong `OnSetFocus`: Cập nhật `_hasFocus = (pdimFocus != IntPtr.Zero)`. Chỉ đồng bộ trạng thái vào thread compartment khi thực sự có focus, và bọc bằng `IsInternalCompartmentSync`.
  - Trong `StartStateWatcher`: Bọc việc cập nhật compartment bằng `IsInternalCompartmentSync`.
- **[GlobalVEState.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/GlobalVEState.cs)**:
  - Bọc khối cập nhật `TsfCompartmentHelper.SetOpenClose` và `SetConversionMode` bằng `IsInternalCompartmentSync` để ngắt đệ quy toàn cục.

### 2.3. Khóa Cố Định trên Cấu hình & Giao diện (UI)
- **[SharedMemoryManager.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs)**:
  - Cố định mặc định trong RAM và file JSON: `hotkeyVKey = 192 (0xC0)`, `hotkeyModifiers = 1 (Alt)`.
- **[SharedConfig.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/SharedConfig.fs)**:
  - Cố định `ToggleHotkey = 0uy`, `HotkeyVKey = 0xC0u`, `HotkeyModifiers = 0x0001u`, `HotkeyDisplay = "`/ ~ / 半角/全角 (JP) | Alt + ~"` khi nạp và lưu cấu hình.
- **[MainWindow.axaml](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/MainWindow.axaml)**:
  - Thay thế toàn bộ khung gán phím tắt tùy biến và các nút chip chọn nhanh bằng card thông tin cố định:
    `"MẶC ĐỊNH CỐ ĐỊNH: Phím dưới Esc (` / ~ / 半角/全角 / 漢字)"`.
- **[MainWindow.axaml.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/MainWindow.axaml.fs)**:
  - Gỡ bỏ các trường điều khiển gán phím tắt, gỡ bỏ `SetHotkey` và các listener sự kiện `KeyDown` / `KeyUp` gán phím.

---

## 3. Kết quả nghiệm thu & Kiểm thử

1. **Kiểm thử tự động (Unit Tests):**
   - `BambooMintKey.Core.Tests`: **321/321 tests PASSED**.
   - `BambooMintKey.NativeBridge.Tests`: **5/5 tests PASSED**.
2. **Biên dịch & Đóng gói:**
   - Solution biên dịch thành công 100% với 0 lỗi.
   - Bộ cài đặt mới đã được đóng gói:
     `D:\Kojin\BambooMintKey\bin\dist\BambooMintKey-Setup.exe` (12.38 MB).
3. **Quản lý phiên bản:**
   - Toàn bộ thay đổi đã được commit và push lên nhánh `main` của repository `thatislg/BambooMintKey`.
