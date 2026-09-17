<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Walkthrough 005: Hoàn Thiện 100% Cơ Chế Chuyển Đổi V/E Theo Chuẩn Google Japanese Input (Mozc / TSF Native)

**Mã tài liệu:** `005_Walkthrough`  
**Ngày thực hiện:** 2026-09-17  
**Liên quan:** 
- `docs/3.Issue/005_ShortcutKeyAutoResetError.md` (Issue báo lỗi)
- `docs/3.Issue/005_01_Mozc_TSF_Mode_Activation.md` (Tài liệu giải pháp chi tiết)

---

## 1. Nguyên tắc kiến trúc (100% Mozc / TSF Native - Tuyệt đối không dùng cơ chế cũ của Unikey)
Đúng theo triết lý của **Google Japanese Input (Mozc)** trên Windows:
1. **Không quét phím thô (Zero raw key scanning)**:
   - Xóa bỏ hoàn toàn `IsToggleHotkeyPressed`, `IsKeyDown`, và các P/Invoke `GetKeyState` / `GetAsyncKeyState` dùng để đánh chặn phím tắt.
   - `OnTestKeyDown` và `OnKeyDown` khi ở chế độ English (`!IsVietnameseMode`) lập tức cho phím đi qua (`*pfEaten = 0; return HResult.Ok;`), không nuốt phím và không can thiệp.
2. **Kích hoạt độc quyền qua Windows TSF PreservedKey**:
   - Phím tắt được ủy quyền hoàn toàn cho `ITfKeystrokeMgr::PreserveKey`.
   - Khi người dùng nhấn phím tắt, Windows TSF trực tiếp gọi callback chuẩn `ITfKeyEventSink::OnPreservedKey`.
   - `OnPreservedKey` gọi chuyển đổi chế độ qua `GlobalVEState.ToggleVietnameseMode`.
3. **Phản ứng sự kiện hai chiều (Reactive TSF Compartment)**:
   - Sử dụng đúng IID của `ITfCompartmentEventSink` (`743ABD5F-F26D-48DF-8CC5-238492419B64`).
   - Mọi thay đổi về Open/Close và ConversionMode đều phát sự kiện `OnChange` qua TSF Compartment để đồng bộ toàn hệ thống.

---

## 2. Chi tiết các sửa đổi cốt lõi

1. **[Guids.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/Guids.cs)**:
   - Sửa `IidITfCompartmentEventSink` thành `743ABD5F-F26D-48DF-8CC5-238492419B64` (chuẩn SDK `msctf.h`), giúp `AdviseSink` thành công với `HR=0x00000000`.
2. **[KeyEventSinkImpl.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs)**:
   - Loại bỏ hoàn toàn các lệnh kiểm tra phím tắt thủ công trong `OnTestKeyDown` và `OnKeyDown`.
   - Điểm chuyển đổi duy nhất là `OnPreservedKey`.
3. **[KeyInputTranslator.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Interop/KeyInputTranslator.cs)**:
   - Loại bỏ hàm quét phím tắt kiểu Unikey `IsToggleHotkeyPressed` và `IsKeyDown`.
4. **[KeyEventSinkHelper.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkHelper.cs)**:
   - Đăng ký PreservedKey chuẩn xác vào TSF Keystroke Manager cho từng loại phím tắt (`Ctrl + Shift`, `Alt + Z`, `Ctrl + Space`, hoặc phím tự chọn).
5. **[BambooMintKeyTextService.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs)**:
   - Trong `OnSetFocus`: Cấp phát trạng thái hiện tại vào thread compartment của cửa sổ mới, loại bỏ hoàn toàn lỗi bị reset về `false`.
   - Trong `StateWatcher`: Nhận sự kiện broadcast từ shared memory để đồng bộ thread compartment và icon.
6. **[LangBarItemButton.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs)**:
   - Click chuột trái và menu lệnh đều chuyển đổi trạng thái đồng bộ toàn hệ thống qua `GlobalVEState`.
7. **[SharedMemoryManager.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs)**:
   - Sanitize giá trị phím tắt khi khởi tạo, triệt tiêu hoàn toàn dữ liệu rác cũ trong RAM (`0x51`).

---

## 3. Kết quả nghiệm thu
- **Unit Tests**: 321/321 Core Tests + 5/5 NativeBridge Tests Passed.
- **Biên dịch & Đóng gói**: Đã tạo file cài đặt mới `bin/dist/BambooMintKey-Setup.exe` (12.37 MB).
