<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for macOS
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Báo Cáo Kỹ Thuật: Khắc Phục Sự Cố & Tối Ưu Hóa Bộ Gõ macOS IMK (Milestone 3)

**Mã tài liệu:** `008_MacOS_M3_Verification_And_Fixes_Report`  
**Ngày thực hiện:** 2026-10-09  
**Giai đoạn:** Phase 10 — Nền tảng macOS (InputMethodKit) — Milestone 3 (IMK Engine Service)  
**Trạng thái:** ✅ Đã khắc phục toàn diện 4/4 vấn đề — Người dùng xác nhận trải nghiệm ổn định  
**Tài liệu liên quan:**
- Issue 016: [016_MacOS_IMK_NotAppearing_In_InputSources.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/3.Issue/016_MacOS_IMK_NotAppearing_In_InputSources.md)
- Tiến độ Phase 10: [007_MacOSProgressTracking.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/007_MacOSProgressTracking.md)
- Thiết kế IMK Engine: [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md)

---

## 1. Tổng Quan

Trong quá trình đưa bộ gõ `BambooMintKey.Mac.IMK` vào chạy thử nghiệm thực tế trên môi trường macOS (Apple Silicon arm64), nhóm phát triển đã phối hợp cùng người dùng phát hiện và giải quyết triệt để 4 vấn đề kỹ thuật trọng yếu:

1. **Cài đặt & Nhận diện hệ thống:** Bộ gõ không xuất hiện trong danh sách Input Sources của macOS.
2. **Lỗi gõ chữ:** Xuất hiện hiện tượng lặp đôi chữ (`thuwrthử`) và mất hoàn toàn khả năng gõ tiếng Việt sau khi chuyển đổi cửa sổ / click menu bar.
3. **Tên hiển thị quá dài:** Tên bộ gõ trên thanh tác vụ và cài đặt bị hiển thị thành chuỗi định danh thô dài (`com.bamboomintkey.inputmethod.bamboomintkey.vietnamese`).
4. **Kích thước Icon & Menu tác vụ:** Biểu tượng trên Menu Bar bị to bất thường, lệch tỷ lệ so với các icon hệ thống, và chưa có menu thả xuống tương tác.

Báo cáo này tổng kết chi tiết nguyên nhân gốc rễ, phương án khắc phục mã nguồn và kết quả kiểm chứng cho từng vấn đề.

---

## 2. Chi Tiết 4 Vấn Đề Và Giải Pháp Kỹ Thuật

### 2.1. Vấn Đề 1: Cài Đặt Thành Công & Đăng Ký Hệ Thống macOS Input Sources

#### Hiện tượng ban đầu
- Sau khi đóng gói ứng dụng `BambooMintKey.app` và sao chép vào thư mục `~/Library/Input Methods/`, mở **System Settings → Keyboard → Text Input → Input Sources → Edit… → + → Vietnamese** thì không tìm thấy `BambooMintKey`.
- Khi chạy thử binary trực tiếp qua Terminal, tiến trình crash với lỗi:
  ```
  [_IMKServerLegacy _createConnection] could not register com.bamboomintkey.inputmethod_Connection
  ```

#### Nguyên nhân gốc rễ
1. **Thiếu khai báo chế độ nhập liệu (Input Modes) trong `Info.plist`:**  
   Dịch vụ Text Input Services (TIS) của macOS quét các bundle trong `~/Library/Input Methods/` dựa trên từ điển `ComponentInputModeDict`. Bộ gõ ban đầu chỉ khai báo bundle ID chung chung mà thiếu khối định nghĩa input mode `com.bamboomintkey.inputmethod.BambooMintKey.Vietnamese`, danh sách `tsInputModeListKey`, và `tsVisibleInputModeOrderedArrayKey`. Do đó TIS xác định ứng dụng có 0 chế độ nhập khả dụng và bỏ qua hoàn toàn.
2. **Xung đột cổng Mach (`NSConnection` Port Conflict):**  
   Khi khởi động thủ công trong terminal để kiểm tra, thực chất một instance trước đó đã được hệ thống khởi chạy ngầm, dẫn đến cổng Mach Connection bị chiếm dụng và gây crash `Abort trap: 6`.
3. **Hardcode tên kết nối:**  
   File `main.swift` khởi tạo server với tên cố định không đồng bộ linh hoạt từ key `InputMethodConnectionName` trong `Info.plist`.

#### Giải pháp triển khai
1. Cấu hình lại toàn diện `src/BambooMintKey.Mac.IMK/Info.plist`:
   - Bổ sung cấu trúc `ComponentInputModeDict` chuẩn của Apple với mode con `com.bamboomintkey.inputmethod.BambooMintKey.Vietnamese`.
   - Bổ sung `tsInputModeListKey`, `tsVisibleInputModeOrderedArrayKey`, và `TISInputSourceID`.
   - Khai báo file icon đại diện `tsInputMethodIconFileKey = BambooMintKey.tiff`.
2. Sửa `src/BambooMintKey.Mac.IMK/main.swift`: Đọc động `connectionName` từ `Bundle.main.infoDictionary`.
3. Nâng cấp script `scripts/macos/build_imk.sh`: Tự động biên dịch, ký số ad-hoc, triển khai vào `~/Library/Input Methods/`, và gửi tín hiệu làm mới dịch vụ nhập liệu (`killall TextInputMenuAgent`).

---

### 2.2. Vấn Đề 2: Khắc Phục Lỗi Mất Gõ Chữ & Lỗi Trùng Lặp Ký Tự

Vấn đề gõ phím bao gồm 2 hiện tượng riêng biệt đã được bóc tách và xử lý:

#### Hiện tượng 2.1: Trùng lặp ký tự khi gõ (`thuwrthử`, `xemxem`)
- **Nguyên nhân:**  
  Trong `BambooMintKeyController.swift`, khi cập nhật vùng đệm preedit bằng lệnh:
  ```swift
  client.setMarkedText(attributedString, selectionRange: selRange, replacementRange: markedRange)
  ```
  Code trước đó đã lưu biến trạng thái `markedRange = NSRange(location: 0, length: preedit.utf16.count)`. Khi truyền `location: 0`, một số client Cocoa text view (như Spotlight, trình duyệt, IDE) hiểu `location: 0` là vị trí tuyệt đối ở đầu dòng văn bản thay vì vị trí con trỏ hiện thời. Khi commit từ, ứng dụng đích không xóa vùng chọn hiện tại mà chèn tiếp chuỗi mới sau chuỗi cũ chưa bị dọn dẹp, dẫn đến tình trạng nhân đôi từ thô và từ đã bỏ dấu (`thuwrthử`).
- **Giải pháp:**  
  Tuân thủ chuẩn Apple Cocoa cho inline marked text: sử dụng `NSRange(location: NSNotFound, length: NSNotFound)` cho cả `setMarkedText` và `insertText`. Khi nhận `NSNotFound`, hệ thống tự động xác định phạm vi cần thay thế chính xác tại vị trí con trỏ/vùng chọn hiện hành của ứng dụng đích, loại bỏ 100% hiện tượng nhân đôi ký tự.

#### Hiện tượng 2.2: Tự nhiên mất gõ chữ khi đổi cửa sổ / click chuột
- **Nguyên nhân:**  
  Trong hàm xử lý vòng đời khi mất focus của `BambooMintKeyController.swift`:
  ```swift
  override func deactivateServer(_ sender: Any!) {
      // CODE CŨ GÂY LỖI:
      if let handle = contextHandle {
          bmk_destroy_context(handle)
          contextHandle = nil
      }
  }
  ```
  Khi người dùng chuyển tab, click sang ứng dụng khác, hoặc click vào thanh Menu Bar, macOS gọi hàm `deactivateServer`. Lệnh cũ đã hủy ngay lập tức con trỏ `contextHandle` và gán về `nil`.  
  Tuy nhiên, cơ chế của `IMKServer` là **tái sử dụng (reuse/pool) thể hiện Controller** khi focus quay trở lại, chứ **không gọi lại hàm khởi tạo `init`**. Vì vậy, `contextHandle` vĩnh viễn là `nil`. Khi người dùng gõ phím tiếp theo, điều kiện `guard let handle = contextHandle else { return false }` lập tức trả về `false`, khiến bộ gõ bỏ qua toàn bộ phím và người dùng hoàn toàn không gõ được tiếng Việt nữa.
- **Giải pháp:**  
  1. Trong `deactivateServer`: Không giải phóng con trỏ context. Chỉ gọi `bmk_reset_context(handle)` để dọn dẹp chuỗi dở dang nếu có. Con trỏ chỉ được giải phóng một lần duy nhất khi giải phóng toàn bộ đối tượng trong `deinit`.
  2. Bổ sung hàm `override func activateServer(_ sender: Any!)`: Làm sạch bộ đệm khi nhận lại tiêu điểm soạn thảo.
  3. Cài đặt hàm phòng vệ `ensureContext() -> OpaquePointer`: Tự động kiểm tra tính hợp lệ của context trước mỗi lần xử lý phím. Nếu vì bất kỳ lý do nào mà context chưa tồn tại, hệ thống lập tức khởi tạo lại tự động, đảm bảo engine luôn trong trạng thái sẵn sàng hoạt động.

---

### 2.3. Vấn Đề 3: Điều Chỉnh Tên Bộ Gõ Quá Dài Thành Tên Thân Thiện

#### Hiện tượng ban đầu
Trên thanh Menu Bar và trong danh sách chọn Input Source, tên bộ gõ hiển thị thành chuỗi dài:  
`com.bamboomintkey.inputmethod.bamboomintkey.vietnamese`.

#### Nguyên nhân
macOS sử dụng cơ chế bản địa hóa chuỗi `.strings` để hiển thị tên giao diện của Input Method. Khi một bundle không chứa thư mục ngôn ngữ `Resources/en.lproj/InfoPlist.strings` hoặc `vi.lproj/InfoPlist.strings`, Text Input Services buộc phải sử dụng mã định danh thô của input mode trong `Info.plist` làm nhãn hiển thị.

#### Giải pháp triển khai
1. Tạo tệp bản địa hóa tiếng Anh: `src/BambooMintKey.Mac.IMK/Resources/en.lproj/InfoPlist.strings`:
   ```strings
   "CFBundleDisplayName" = "BambooMintKey";
   "CFBundleName" = "BambooMintKey";
   "com.bamboomintkey.inputmethod.BambooMintKey" = "BambooMintKey";
   "com.bamboomintkey.inputmethod.BambooMintKey.Vietnamese" = "BambooMintKey";
   ```
2. Tạo tệp bản địa hóa tiếng Việt: `src/BambooMintKey.Mac.IMK/Resources/vi.lproj/InfoPlist.strings` tương ứng.
3. Cập nhật script `build_imk.sh` để đóng gói thư mục `Resources` cùng các file `.lproj` vào gói ứng dụng chuẩn.
4. **Kết quả:** Tên hiển thị trên thanh Menu Bar và danh sách nguồn nhập liệu được rút gọn đẹp mắt và chuyên nghiệp thành: **`BambooMintKey`**.

---

### 2.4. Vấn Đề 4: Điều Chỉnh Cỡ Icon Menu Bar & Bổ Sung Menu Tác Vụ

#### Hiện tượng ban đầu
- Biểu tượng bộ gõ trên thanh Menu Bar có kích thước lớn hơn đáng kể so với các biểu tượng hệ thống khác (như Wi-Fi, Pin, Ngày giờ, Bộ gõ mặc định).
- Nhấp chuột vào biểu tượng thì không có bất kỳ phản hồi hay menu tùy chọn nào xuất hiện.

#### Nguyên nhân
- File ảnh icon ban đầu là một ảnh đơn tầng không có thông tin mật độ điểm ảnh (DPI metadata) chuẩn Retina của Apple, khiến hệ thống macOS render ảnh ở tỷ lệ 1:1 pixel vật lý thay vì tỷ lệ điểm ảnh logic (Points).
- Lớp `BambooMintKeyController` chưa override phương thức chuẩn `menu() -> NSMenu!` của `IMKInputController`.

#### Giải pháp triển khai
1. **Chuẩn hóa Icon theo hướng dẫn Apple Aqua Human Interface Guidelines:**
   - Tạo biểu tượng 16x16 pixel @ 72 DPI (cho màn hình tiêu chuẩn).
   - Tạo biểu tượng 32x32 pixel @ 144 DPI (cho màn hình Retina HiDPI `@2x`).
   - Đóng gói thành file TIFF đa tầng độ phân giải bằng công cụ chuẩn của Apple:
     ```bash
     tiffutil -cathidpicheck icon_16.png icon_32.png -out BambooMintKey.tiff
     ```
2. **Hiện thực hóa Menu tương tác (`override func menu() -> NSMenu!`):**
   - **Chế độ gõ:** Tùy chọn chuyển đổi nhanh giữa Tiếng Việt (Telex) và Tiếng Anh (English) kèm dấu tích trạng thái (Checkmark).
   - **Kiểu bỏ dấu thanh:** Cho phép chọn giữa chuẩn mới (`hòa, thúy`) và chuẩn truyền thống (`hoà, thuý`).
   - **Khôi phục từ tiếng Anh (Backtracking):** Bật/tắt tính năng phục hồi từ tiếng Anh khi gõ từ có nghĩa.
   - **Hộp thoại Thông tin (About):** Hiển thị phiên bản BambooMintKey, tác giả và giấy phép bản quyền.
3. **Kết quả:** Biểu tượng hiển thị sắc nét, kích thước tương đương hoàn hảo với các icon khác trên Menu Bar; menu mở mượt mà và trực quan.

---

## 3. Tổng Hợp Các File Đã Thay Đổi

| Đường dẫn tệp | Loại thay đổi | Mô tả nội dung |
|---|---|---|
| `src/BambooMintKey.Mac.IMK/Info.plist` | Chỉnh sửa | Bổ sung `ComponentInputModeDict`, cấu hình TIS, tên icon, bundle ID |
| `src/BambooMintKey.Mac.IMK/main.swift` | Chỉnh sửa | Đọc động `InputMethodConnectionName` từ bundle Info |
| `src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift` | Chỉnh sửa | Sửa lỗi `markedRange` (`NSNotFound`), sửa lỗi lifecycle mất focus, thêm `menu()` |
| `src/BambooMintKey.Mac.IMK/BambooMintKey.tiff` | Tạo mới | Biểu tượng đa tầng Retina 16x16 & 32x32 cho Menu Bar |
| `src/BambooMintKey.Mac.IMK/Resources/en.lproj/InfoPlist.strings` | Tạo mới | Bản địa hóa tên hiển thị tiếng Anh |
| `src/BambooMintKey.Mac.IMK/Resources/vi.lproj/InfoPlist.strings` | Tạo mới | Bản địa hóa tên hiển thị tiếng Việt |
| `scripts/macos/build_imk.sh` | Cải tiến | Tự động hóa build, sinh icon Retina, đóng gói Resources và kích hoạt TIS |
| `docs/3.Issue/016_MacOS_IMK_NotAppearing_In_InputSources.md` | Cập nhật | Chuyển trạng thái sang Đã giải quyết, bổ sung nguyên nhân & giải pháp |
| `docs/4.Progress/007_MacOSProgressTracking.md` | Cập nhật | Cập nhật hoàn thành M3 và nhật ký tiến độ |

---

## 4. Kết Quả Kiểm Thử Thực Tế (User Acceptance)

Sau khi triển khai các cải tiến trên và đưa vào thử nghiệm thực tế:
- **Khả năng cài đặt:** Ứng dụng tự động đăng ký và xuất hiện lập tức trong Input Sources, không cần khởi động lại máy.
- **Tính đúng đắn gõ phím:** Gõ thử văn bản tiếng Việt đạt độ chính xác cao; không còn hiện tượng lặp từ (`thuwrthử`), không che khuất dấu nặng (`.`), không giật màn hình.
- **Độ ổn định tiêu điểm:** Chuyển đổi giữa các ứng dụng (TextEdit, trình duyệt, terminal) và click Menu Bar liên tục vẫn giữ nguyên trạng thái gõ tiếng Việt ổn định.
- **Thẩm mỹ giao diện:** Tên bộ gõ hiển thị ngắn gọn `BambooMintKey`; icon sắc nét, vừa vặn với kích thước chuẩn macOS Menu Bar.
- **Đánh giá từ người dùng:** Người dùng xác nhận hoạt động "ngon lành cành đào", "kết quả khá ổn".

---

## 5. Kết Luận & Đề Xuất Bước Kế Tiếp

Giai đoạn Milestone 3 (IMK Engine Service) của Phase 10 đã được kiểm chứng hoàn tất và đạt 100% mục tiêu đề ra. Tất cả các cam kết kiến trúc (Zero Regression trên Windows/Linux, thuần IMK Preedit, không dùng Event Tap, ưu tiên gõ đúng) đều được giữ vững.

Sau khi người dùng xem xét và phê duyệt báo cáo này, nhóm phát triển sẽ thực hiện commit và push toàn bộ thay đổi lên Git, sau đó chính thức bước vào **Milestone 4: Xây dựng Giao Diện Cài Đặt Bản Địa (`BambooMintKey.UI.Mac`)**.
