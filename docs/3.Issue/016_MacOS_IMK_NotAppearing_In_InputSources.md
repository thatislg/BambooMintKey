<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 016: Bộ gõ macOS không xuất hiện trong Input Sources — IMKServer không đăng ký được connection

**Mã tài liệu:** `016_MacOS_IMK_NotAppearing_In_InputSources`

**Trạng thái:** ✅ Đã giải quyết (Resolved) — Đã xuất hiện trong Input Sources, gõ tiếng Việt ổn định, Menu Bar và icon chuẩn chuẩn macOS.

**Mức độ nghiêm trọng:** Chặn (blocker) — Đã khắc phục hoàn toàn.

**Nền tảng:** macOS (InputMethodKit). Đây là milestone M3 của Phase 10 (macOS).

**Module liên quan:** `src/BambooMintKey.Mac.IMK/` (`main.swift`, `BambooMintKeyController.swift`, `Info.plist`, `Resources/`), `scripts/macos/build_imk.sh`

**Ngày ghi nhận:** 09/10/2026 — **Ngày giải quyết:** 09/10/2026

---

## 1. Mô tả hiện tượng ban đầu

Sau khi biên dịch và cài bundle vào `~/Library/Input Methods/`, bộ gõ không xuất hiện trong danh sách Input Sources (System Settings → Keyboard → Text Input → Input Sources → Edit… → +). Ngoài ra khi chạy trực tiếp qua Terminal thì báo lỗi abort đăng ký `NSConnection`.

---

## 2. Nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Không xuất hiện trong Input Sources: Thiếu cấu hình chế độ nhập TIS trong `Info.plist`
- `Info.plist` ban đầu thiếu hoàn toàn khối `ComponentInputModeDict`, `tsInputModeListKey`, `tsVisibleInputModeOrderedArrayKey`, và `TISInputSourceID`.
- Hệ điều hành macOS Text Input Services (TIS) đọc cấu hình này để phân loại ngôn ngữ và nguồn nhập liệu. Khi thiếu các key trên, TIS xác định bundle có 0 chế độ nhập khả dụng nên bỏ qua hoàn toàn, không hiển thị trong GUI cài đặt bàn phím.

### 2.2. Lỗi crash `[_IMKServerLegacy _createConnection] could not register ...`
- Khi chạy thử trực tiếp nhị phân bằng tay trong Terminal, do đã có một tiến trình `BambooMintKey` trước đó đang chạy ngầm trong hệ thống chiếm giữ Mach port (`NSConnection`), tiến trình mới không thể đăng ký trùng tên cổng Mach dẫn đến `Abort trap: 6`.
- Tên kết nối trong `main.swift` được hardcode chuỗi thay vì đọc động thuộc tính `InputMethodConnectionName` từ `Info.plist`.

### 2.3. Thiếu icon và tệp bản địa hóa tên bộ gõ
- Thiếu khai báo `tsInputMethodIconFileKey` khiến hệ thống không nạp được icon hiển thị.
- Thiếu các thư mục `en.lproj/InfoPlist.strings` và `vi.lproj/InfoPlist.strings` khiến TIS hiển thị tên thô dạng `com.bamboomintkey.inputmethod.bamboomintkey.vietnamese`.

---

## 3. Các biện pháp khắc phục đã triển khai

1. **Chuẩn hóa `Info.plist`:**
   - Bổ sung `ComponentInputModeDict` với chế độ `com.bamboomintkey.inputmethod.BambooMintKey.Vietnamese`.
   - Khai báo `tsInputModeListKey`, `tsVisibleInputModeOrderedArrayKey`, `TISInputSourceID`.
   - Khai báo `tsInputMethodIconFileKey = BambooMintKey.tiff`.
   - Chuẩn hóa bundle identifier thành `com.bamboomintkey.inputmethod.BambooMintKey`.

2. **Cập nhật `main.swift`:**
   - Đọc động tên kết nối từ `Bundle.main.infoDictionary?["InputMethodConnectionName"]`.

3. **Thêm bản địa hóa hiển thị:**
   - Tạo `Resources/en.lproj/InfoPlist.strings` và `Resources/vi.lproj/InfoPlist.strings` đặt tên hiển thị thân thiện là `BambooMintKey`.

4. **Tạo Icon chuẩn Retina:**
   - Sinh file TIFF đa tầng độ phân giải (16x16 @ 72 DPI và 32x32 @ 144 DPI) qua `tiffutil -cathidpicheck`.

5. **Cập nhật script build tự động:**
   - `scripts/macos/build_imk.sh`: Tự động biên dịch Swift, liên kết dylib, sinh icon, copy bản địa hóa, ký ad-hoc và cài đặt/làm mới TIS.

---

## 4. Kết quả kiểm chứng

- ✅ `BambooMintKey` xuất hiện đầy đủ trong mục **Vietnamese** tại System Settings → Keyboard → Input Sources.
- ✅ Người dùng kích hoạt thành công bộ gõ vào thanh trạng thái macOS.
- ✅ Tên hiển thị trên menu là `BambooMintKey` (gọn gàng, không còn chuỗi bundle ID dài).
- ✅ Biểu tượng icon hiển thị sắc nét, chuẩn kích thước thanh menu.
- ✅ Gõ thử nghiệm tiếng Việt trong thực tế thành công.
