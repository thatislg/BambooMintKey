<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 021: Lỗi ký tự lạ và rác chữ khi bấm phím mũi tên dở dang & Làm rõ chính sách gạch chân Preedit trên macOS

**Mã tài liệu:** `021_MacOS_ArrowKey_GarbledPreedit_And_UnderlineClarification`

**Trạng thái:** ✅ Đã giải quyết & Đã làm rõ tài liệu

**Mức độ nghiêm trọng:** Trung bình (Medium) — Ảnh hưởng điều hướng con trỏ trong từ đang gõ.

**Nền tảng:** macOS (InputMethodKit). Đã từng gặp lỗi tương tự trên Linux (tham chiếu [Issue 015](file:///Users/lmo1720/Self-App/BambooMintKey/docs/3.Issue/015_Linux_Preedit_Commit_Order_And_ArrowKey.md)).

**Module liên quan:** 
- `src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift`
- `docs/4.Progress/007_MacOSProgressTracking.md`

**Ngày ghi nhận:** 10/10/2026

---

## 1. Mô tả hai vấn đề

### 1.1. Vấn đề 1: Thắc mắc về đường gạch chân Preedit & Simple Telex không có gạch chân ở một số app
- **Phản hồi từ người dùng:** Trong cài đặt không có tùy chọn nào bật/ẩn đường gạch chân của Preedit, trước đó thấy app nào cũng gạch chân, nhưng quan sát thấy bộ gõ Simple Telex của Apple ở một số ứng dụng (như Notes, Safari, TextEdit) lại **hoàn toàn không có gạch chân**.
- **Nguyên nhân kỹ thuật được tìm ra:**
  1. Trong cài đặt `BambooMintKey.UI.Mac`, không có checkbox bật/tắt gạch chân (checkbox `ChkPreedit` chỉ có trên Windows TSF).
  2. Trước đây trong code `BambooMintKeyController.swift`, chúng ta dùng thuộc tính tự tạo `stealthAttributes = [ .underlineStyle: 0, .underlineColor: NSColor.clear ]` mà không dùng API chuẩn của Apple. Do các text view của Apple không nhận diện được `NSMarkedClauseSegment`, chúng kích hoạt fallback vẽ gạch chân ở mọi ứng dụng.
  3. Trong khi đó, **Simple Telex** sử dụng API chuẩn của `IMKInputController`: `mark(forStyle: 0, at: range)` (`style 0: kTSMHiliteRawText`), trả về `[NSMarkedClauseSegment: 1]` (hoàn toàn không có thuộc tính `NSUnderline`).
  4. Các ứng dụng Apple bản địa (Safari, Notes, TextEdit, Pages...) khi nhận `NSMarkedClauseSegment: 1` sẽ **tự động không vẽ gạch chân**, giúp chữ hiển thị tự nhiên. Ngược lại, các ứng dụng Chromium/Electron (Chrome, Slack) tự render gạch chân riêng nên vẫn hiển thị.
  5. Đã chuyển `BambooMintKeyController.swift` sang sử dụng `self.mark(forStyle: 0, at: range)`, nhờ đó BambooMintKey hiện tại cũng **không có gạch chân trên các app Apple bản địa**, khớp 100% với hành vi của Simple Telex!

### 1.2. Vấn đề 2: Bấm phím mũi tên (Trái/Phải/Lên/Xuống) khi đang có Preedit sinh ký tự lạ và rác chữ
- **Hiện tượng:** Khi đang gõ một từ chưa bấm `Space` (vẫn còn preedit dở dang, ví dụ `củ`), nếu bấm các phím mũi tên điều hướng (Trái, Phải, Lên, Xuống):
  1. Xuất hiện một ký tự lạ khó xóa (ký tự unprintable).
  2. Bấm `Space` tiếp theo thì văn bản bị chèn kèm ký tự lạ đó và phát sinh rác chữ.
- **Quan sát so sánh:**
  - Trên Linux: Đã từng bị lỗi tương tự ([Issue 015](file:///Users/lmo1720/Self-App/BambooMintKey/docs/3.Issue/015_Linux_Preedit_Commit_Order_And_ArrowKey.md)), đã xử lý bằng cách commit preedit dở dang trước khi nhả phím cho ứng dụng.
  - Trên macOS với bộ gõ Simple Telex mặc định của Apple: Khi bấm mũi tên lùi lại, con trỏ di chuyển vào giữa các ký tự của từ mà không làm biến đổi chữ hay sinh ký tự lạ.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Vì sao phím mũi tên lọt qua bộ lọc của `handle(_:client:)`?
Trong `BambooMintKeyController.swift`, code ban đầu có đoạn:

```swift
// 5. Trích xuất mã Unicode của ký tự (dùng event.characters để giữ hoa/thường theo Shift).
guard let characters = event.characters,
      let scalar = characters.unicodeScalars.first else {
    // Phím không sinh ký tự in được (mũi tên, Home/End, F1-F12...):
    // chốt từ dở dang trước khi nhường phím, tránh mất chữ.
    flushPendingComposition(sender)
    return false
}
```

- **Giả định sai lầm:** Người viết code cho rằng các phím mũi tên (Left, Right, Down, Up) và phím chức năng (Escape, Home, End...) sẽ khiến `event.characters` bị `nil` hoặc rỗng, từ đó nhảy vào nhánh `else` để gọi `flushPendingComposition`.
- **Thực tế của Apple AppKit / Cocoa:**
  - Trên macOS, các phím điều hướng và phím chức năng **KHÔNG HỀ BỊ NIL**!
  - Cocoa gán cho chúng các ký tự đặc biệt thuộc dải Unicode Private Use Area (`NSFunctionKey` range: `0xF700...0xF8FF`):
    - `NSUpArrowFunctionKey`: `\u{F700}` (scalar `0xF700`)
    - `NSDownArrowFunctionKey`: `\u{F701}` (scalar `0xF701`)
    - `NSLeftArrowFunctionKey`: `\u{F702}` (scalar `0xF702`)
    - `NSRightArrowFunctionKey`: `\u{F703}` (scalar `0xF703`)
  - Vì vậy, `guard let characters` **luôn luôn thành công**!
  - Ký tự `\u{F702}` tiếp tục chạy xuống:
    - Bỏ qua `isWordBreak` (vì `0xF702 > 0x7F`).
    - Bỏ qua ASCII in được (`0x20 <= unicode <= 0x7E`).
    - Rơi thẳng xuống cuối hàm: `return false`.

### 2.2. Hậu quả khi `return false` mà không dọn dẹp Preedit
1. Hàm trả về `false` mà **chưa gọi `flushPendingComposition`**.
2. Preedit (chuỗi marked text) vẫn treo lơ lửng trong ô nhập liệu của ứng dụng và trong bộ đệm C-ABI.
3. Đồng thời, sự kiện phím (mang mã ký tự `0xF702`) được forward xuống ứng dụng.
4. Một số widget soạn thảo (Chromium omnibox, khung chat Electron, TextKit) khi đang có marked text mà lại nhận sự kiện không được consume sẽ chèn ký tự `\u{F702}` (ký tự unprintable ô vuông/hộp rác khó xóa) vào dòng văn bản hoặc làm vỡ chỉ mục marked text.
5. Khi người dùng bấm `Space` tiếp theo, bộ gõ vẫn giữ ngữ cảnh cũ và gọi lệnh commit, đè lên vị trí con trỏ bị lệch $\rightarrow$ sinh ra rác chữ và các ký tự dị thường.

---

## 3. Giải pháp đã triển khai

### 3.1. Nhận diện chuẩn xác toàn bộ phím điều hướng và phím chức năng
Bổ sung phương thức phân loại `isNavigationOrFunctionKey` trong `BambooMintKeyController.swift`:

```swift
/// Nhận diện các phím điều hướng và phím chức năng không in được:
/// - Mũi tên: Trái (123), Phải (124), Xuống (125), Lên (126)
/// - Điều hướng trang: Home (115), End (119), Page Up (116), Page Down (121)
/// - Hủy / Xóa phía trước: Escape (53), Forward Delete (117)
/// - Dải Cocoa Special Function Keys (0xF700...0xF8FF) bao gồm mũi tên và F1..F20
/// - Ký tự điều khiển ASCII (< 0x20 ngoại trừ Tab \t, LF \n, CR \r)
private func isNavigationOrFunctionKey(_ event: NSEvent) -> Bool {
    // Phím mũi tên (Left: 123, Right: 124, Down: 125, Up: 126)
    if event.keyCode >= 123 && event.keyCode <= 126 {
        return true
    }
    // Phím điều hướng & hệ thống khác
    if event.keyCode == 53 || event.keyCode == 115 || event.keyCode == 119 ||
       event.keyCode == 116 || event.keyCode == 121 || event.keyCode == 117 {
        return true
    }
    // Ký tự trong dải function key hoặc điều khiển của Cocoa
    if let chars = event.characters, let scalar = chars.unicodeScalars.first {
        let val = scalar.value
        if val >= 0xF700 && val <= 0xF8FF {
            return true
        }
        if val < 0x20 && val != 0x09 && val != 0x0A && val != 0x0D {
            return true
        }
    }
    return false
}
```

### 3.2. Chốt dứt khoát Preedit trước khi nhường phím
Trước khi xử lý ký tự gõ thông thường, kiểm tra `isNavigationOrFunctionKey`:

```swift
if isNavigationOrFunctionKey(event) {
    flushPendingComposition(sender)
    return false
}
```

Trong `flushPendingComposition`:
- Lấy `text = CABIBridge.preeditString(handle)`.
- Nếu `!text.isEmpty`: gọi `input.insertText(text, replacementRange: notFoundRange)` để chốt chuỗi hiện tại thành chữ thật, kết thúc trạng thái marked text.
- Nếu `text.isEmpty`: gọi `input.setMarkedText("", ...)` để đảm bảo xóa sạch mọi đánh dấu dư thừa.
- Gọi `CABIBridge.contextReset(handle)` để reset bộ đệm C-ABI về rỗng.
- Nhường phím mũi tên cho ứng dụng (`return false`).

Tương tự, ở cuối hàm `handle`, nếu có bất kỳ phím nào không được thụ lý, cũng gọi `flushPendingComposition(sender)` trước khi `return false` để tránh kẹt trạng thái.

### 3.3. Cập nhật tài liệu theo dõi tiến độ
- Cập nhật Nguyên tắc 3 trong [007_MacOSProgressTracking.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/007_MacOSProgressTracking.md): Làm rõ cơ chế Preedit hiển thị có đường gạch chân chuẩn Apple Text System, không hứa hẹn tính năng "Stealth Mode" không khả thi và không tạo setting thừa trong UI.
- Đánh dấu hoàn thành hạng mục **M6.2**.

---

## 4. Kết quả nghiệm thu

1. **Khi gõ từ dở dang rồi bấm mũi tên (Trái/Phải/Lên/Xuống):**
   - Từ đang gõ (ví dụ `củ`) lập tức được chốt thành văn bản chính thức.
   - Con trỏ di chuyển tự nhiên vào giữa các ký tự của từ (giữa `c` và `ủ`), tương tự như Simple Telex của Apple.
   - Không xuất hiện bất kỳ ký tự lạ unprintable nào.
2. **Khi bấm `Space` hoặc gõ tiếp sau khi di chuyển:**
   - Dấu cách được chèn bình thường, từ mới bắt đầu sạch sẽ.
   - Không còn tình trạng nhân bản chữ hay rác chữ.
