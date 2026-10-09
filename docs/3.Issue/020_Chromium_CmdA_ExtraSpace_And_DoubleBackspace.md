<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 020: Lỗi bôi đen dư khoảng trắng (`Cmd+A`/`Ctrl+A`) và phải bấm Backspace 2 lần trên hệ ứng dụng Chromium (Antigravity Chat, Google Sheets, Chrome)

**Mã tài liệu:** `020_Chromium_CmdA_ExtraSpace_And_DoubleBackspace`  
**Trạng thái:** 🟡 Đang điều tra / Đề xuất giải pháp toàn diện đa nền tảng  
**Mức độ nghiêm trọng:** Trung bình - Cao (ảnh hưởng trực tiếp đến trải nghiệm thao tác phím tắt, bôi đen và xóa văn bản trên mọi ứng dụng nhân Chromium/Electron)  
**Ngày ghi nhận:** 09/10/2026  
**Nền tảng ảnh hưởng:** Cả 3 hệ điều hành: **macOS (InputMethodKit)**, **Windows (TSF)**, **Linux (Fcitx5)**  
**Ứng dụng ghi nhận:** Antigravity AI Chat (Electron/Monaco Editor), Google Sheets (trên trình duyệt Chrome), VS Code, Chrome/Edge textareas.

---

## 1. Mô tả hiện tượng & Bảng tái hiện lỗi

### 1.1. Hiện tượng
Khi người dùng nhập liệu tiếng Việt trong các ô nhập văn bản thuộc hệ Chromium (khung chat AI của Antigravity IDE, ô tính Excel trên Google Sheets, hoặc các form soạn thảo web trên Chrome):
1. **Dư khoảng trắng khi chọn tất cả:** Người dùng gõ một từ tiếng Việt (ví dụ `chào`). Ngay sau đó bấm tổ hợp `Cmd + A` (trên macOS) hoặc `Ctrl + A` (trên Windows/Linux) để chọn toàn bộ văn bản. Vùng được bôi đen lại bị **dư thêm 1 khoảng trắng ở cuối** (`chào ` thay vì `chào`).
2. **Phải bấm Backspace 2 lần mới xóa được:** Khi toàn bộ chuỗi đang được bôi đen, người dùng bấm phím Backspace (Delete) để xóa. Thay vì xóa sạch toàn bộ nội dung trong 1 lần bấm như quy chuẩn soạn thảo thông thường:
   - **Lần bấm 1:** Chỉ xóa dấu cách thừa (hoặc vùng bôi đen bị biến mất/deselect), chữ `chào` vẫn còn trơ lại trên màn hình.
   - **Lần bấm 2:** Người dùng phải bấm thêm một lần nữa thì chữ `chào` mới thực sự bị xóa.

### 1.2. Bảng so sánh các bước tái hiện

| Bước | Thao tác người dùng | Kết quả thực tế (Hiện tại) | Kết quả kỳ vọng (Chuẩn UX) | Đánh giá |
|:---:|:---|:---|:---|:---|
| 1 | Gõ `c-h-a-o-f` | Hiển thị `chào` (Preedit / Composition) | Hiển thị `chào` | ✅ Bình thường |
| 2 | Nhấn `Cmd + A` (Mac) hoặc `Ctrl + A` (Win/Linux) | Bôi đen vùng: `"chào "` (thừa 1 space) | Bôi đen vùng: `"chào"` (chính xác) | ❌ Lỗi dư ký tự đệm |
| 3 | Nhấn `Backspace` **lần 1** | Xóa dấu cách hoặc mất bôi đen; còn lại `"chào"` | Xóa sạch toàn bộ văn bản trong ô | ❌ Lỗi không xóa selection |
| 4 | Nhấn `Backspace` **lần 2** | Xóa chữ `"chào"`, ô trở về rỗng | *(Đáng lẽ đã xong từ bước 3)* | ❌ Bị dư thừa thao tác |

---

## 2. Phân tích bản chất kỹ thuật (Technical Deep Dive)

Hiện tượng này bắt nguồn từ **sự xung đột giữa hai tầng**:
1. **Mô hình DOM / Rendering Engine của Chromium (Blink):** Trong các ứng dụng web (`contenteditable`, Monaco Editor trong Electron, Google Sheets Canvas editor).
2. **Cơ chế quản lý vòng đời Composition & chặn phím của Bộ gõ (IME):** Trên các tầng giao tiếp OS (macOS IMK, Windows TSF, Linux Fcitx5).

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        CƠ CHẾ XUNG ĐỘT TRONG HỆ CHROMIUM                               │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. Người dùng gõ "chào":                                                               │
│    IME duy trì Preedit/Composition: [chào]                                             │
│    Chromium DOM chèn node đệm (Cursor Placeholder/Trailing Space) để giữ con trỏ chuột │
│                                                                                        │
│ 2. Người dùng bấm Cmd+A / Ctrl+A:                                                      │
│    IME: Flush/Commit [chào] -> Nhả phím cho Chromium                                   │
│    Chromium: Nhận lệnh SelectAll -> Bôi đen cả [chào] LẪN node đệm -> "chào "          │
│                                                                                        │
│ 3. Người dùng bấm Backspace:                                                           │
│    - Lần 1: Chromium xóa node đệm / collapse selection HOẶC IME nuốt phím Backspace    │
│             -> Chữ "chào" vẫn còn nguyên!                                              │
│    - Lần 2: Con trỏ đứng sau "chào" -> Lệnh deleteBackward thông thường mới chạy        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.1. Tại sao vùng chọn lại dư ra 1 space?
* **Node đệm con trỏ (DOM Caret Placeholder):** Trong Chromium/Blink, để con trỏ soạn thảo có thể đứng nhấp nháy hợp lệ ở cuối một node văn bản vừa được nhập qua IME, trình duyệt/editor (đặc biệt là Google Sheets và Monaco Editor) tự động duy trì một khoảng trắng đệm (`&nbsp;`, zero-width space hoặc `<br>` placeholder).
* **Đua lệnh giữa `insertText` và `SelectAll`:** Khi gặp `Cmd+A` / `Ctrl+A`, bộ gõ buộc phải gọi lệnh commit dở dang (`insertText` / `commitString`) trước khi nhả phím tắt cho ứng dụng. Lệnh commit vừa hoàn tất thì lệnh `SelectAll` của Chromium ập đến trong cùng một chu kỳ sự kiện $\rightarrow$ Chromium bôi đen toàn bộ container DOM, bao gồm cả node chữ lẫn node đệm khoảng trắng.

### 2.2. Tại sao phải bấm Backspace 2 lần mới xóa được?
Có 2 nguyên nhân độc lập nhưng cùng dẫn đến kết quả này:
1. **Hành vi xử lý Selection có chứa node đệm của Blink:** Khi vùng chọn chứa cả văn bản và node đệm, phím Backspace đầu tiên được trình duyệt hoặc JavaScript của ứng dụng (Google Sheets / Monaco) ưu tiên xử lý là: xóa node đệm / đưa con trỏ về trạng thái bình thường (collapse selection). Chỉ khi con trỏ đã về trạng thái bình thường ở lần bấm thứ 2, lệnh xóa ký tự văn bản mới được thực thi.
2. **Bộ gõ can thiệp và nuốt phím Backspace khi không nhận diện được Selection:** Trên cả 3 nền tảng (macOS, Windows, Linux), bộ gõ đều có xu hướng chặn phím Backspace để phục vụ việc xóa lùi trong từ tiếng Việt. Nếu bộ gõ không kiểm tra xem ứng dụng **có đang có vùng chọn (Selection)** hay không, bộ gõ sẽ xử lý Backspace theo logic nội bộ của mình ở lần 1 (hoặc nuốt phím), đến lần 2 khi trạng thái nội bộ đã rỗng thì mới nhả phím cho ứng dụng.

---

## 3. Phân tích hiện trạng mã nguồn trên từng nền tảng

### 3.1. Trên macOS (`BambooMintKey.Mac.IMK`)
Trong file [BambooMintKeyController.swift](file:///Users/lmo1720/Self-App/BambooMintKey/src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift):
* **Điểm 1 (Dòng 94):** `notFoundRange` đang bị định nghĩa sai quy chuẩn Apple:
  ```swift
  private let notFoundRange = NSRange(location: NSNotFound, length: NSNotFound)
  ```
  `length: NSNotFound` tương đương `Int.max`. Khi Chromium nhận `length` này trong `insertText(text, replacementRange:)`, một số phiên bản Chromium tính toán sai phạm vi thay thế. Chuẩn của Apple là `NSMakeRange(NSNotFound, 0)`.
* **Điểm 2 (Dòng 163–167):** Xử lý Backspace hoàn toàn không kiểm tra xem ứng dụng có đang bôi đen hay không:
  ```swift
  if event.keyCode == 51 {
      let action = CABIBridge.processBackspace(handle)
      return handleAction(action, client: sender)
  }
  ```
  Nếu người dùng vừa `Cmd + A` xong, `input.selectedRange().length > 0`. Bộ gõ vẫn gọi `processBackspace(handle)`. Nếu `handle` còn vướng trạng thái preedit, bộ gõ sẽ nuốt phím Backspace để xóa preedit của mình thay vì để ứng dụng xóa vùng chọn.

---

### 3.2. Trên Windows (`BambooMintKey.NativeBridge/TSF`)
Trong file [KeyEventSinkImpl.cs](file:///Users/lmo1720/Self-App/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs):
* **Điểm 1 (Dòng 92–96 và 136–139):** Bỏ qua tổ hợp phím tắt hệ thống nhưng **không kết thúc Composition**:
  ```csharp
  if (KeyInputTranslator.IsModifierModifierPressed())
  {
      return HResult.Ok;
  }
  ```
  Khi người dùng nhấn `Ctrl + A`, hàm kiểm tra thấy phím `Ctrl` đang đè nên lập tức `return HResult.Ok`. **Hậu quả:** Phiên `CompositionManager.HasActiveComposition()` vẫn đang mở lơ lửng trong TSF!
* **Điểm 2 (Dòng 100–105 và 157–167):** Khi người dùng nhấn Backspace:
  ```csharp
  if (vkCode == KeyInputTranslator.VkBack && CompositionManager.HasActiveComposition())
  {
      *pfEaten = 1;
      return HResult.Ok;
  }
  ```
  Vì composition vẫn chưa bị đóng sau `Ctrl + A`, khi người dùng bấm Backspace để xóa đoạn vừa chọn: **Bộ gõ TSF phát hiện composition đang mở nên nuốt luôn phím Backspace (`*pfEaten = 1`)!**
  Nó gọi `BridgeStateManager.ProcessBackspace()` trên từ `chào`, làm biến đổi từ thành `chà` thay vì xóa sạch vùng chọn `Ctrl + A`. Đến lần Backspace tiếp theo (hoặc sau khi composition rỗng) thì phím mới được nhả cho Chromium.

---

### 3.3. Trên Linux (`BambooMintKey.Fcitx5`)
Trong file [engine.cpp](file:///Users/lmo1720/Self-App/BambooMintKey/src/BambooMintKey.Fcitx5/engine.cpp):
* **Dòng 198–201:** Fcitx5 có gọi `flushPendingComposition(ic, state)` khi phát hiện `isSystemModifier`.
* Tuy nhiên, trong cơ chế giao tiếp Wayland (`wayland-text-input-v3`) hoặc X11 với Chromium, lệnh `ic->commitString()` và `keyEvent` phím `Ctrl+A` gửi lệch nhịp (async IPC) có thể khiến Chromium nhận lệnh `SelectAll` khi composition chưa kịp unmark hoàn toàn trên DOM, sinh ra trailing space tương tự.

---

## 4. Phương án giải quyết toàn diện

Chúng ta chia làm hai cấp độ giải quyết:
1. **Cấp độ 1 (Nguyên tắc chung cho cả 3 nền tảng):** Xử lý dứt điểm từ tầng Wrapper của Bộ gõ.
2. **Cấp độ 2 (Giải pháp chi tiết cho từng nền tảng & ứng dụng):** Vá trực tiếp vào mã nguồn của macOS, Windows và Linux.

---

### 4.1. Nguyên tắc thiết kế chung (Unified Principles)

Mọi nền tảng (macOS, Windows, Linux) phải tuân thủ 2 nguyên tắc bất di bất dịch:

1. **Nguyên tắc "Clean Flush on System Modifiers":**
   Khi người dùng nhấn bất kỳ tổ hợp phím tắt có chứa `Command` (Mac), `Control` (Win/Linux) hoặc `Alt` (đặc biệt là `Cmd+A` / `Ctrl+A` / `Ctrl+C` / `Ctrl+V`):
   * Phải **kết thúc dứt điểm (End/Commit/Clear)** phiên composition hiện tại.
   * Đặt lại bộ đệm về rỗng trước khi nhường phím tắt cho ứng dụng.
   * Tuyệt đối không để composition tiếp tục sống sau khi phím tắt đã thực thi.
2. **Nguyên tắc "Selection Priority on Backspace":**
   Khi phím Backspace được nhấn:
   * Nếu ứng dụng đích **đang có vùng chọn văn bản (Selection length > 0)**:
   * Bộ gõ **TUYỆT ĐỐI KHÔNG CAN THIỆP**, không nuốt phím. Phải nhả phím Backspace ngay lập tức (`PassThrough` / `pfEaten = 0` / `return false`) để ứng dụng tự xóa sạch vùng chọn trong 1 lần nhấn duy nhất.

---

### 4.2. Giải pháp chi tiết cho từng nền tảng

#### 🍏 A. Trên macOS (`src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift`)

1. **Chuẩn hóa `notFoundRange`:**
   ```swift
   // Sửa dòng 94:
   private let notFoundRange = NSRange(location: NSNotFound, length: 0)
   ```

2. **Kiểm tra vùng chọn trước khi xử lý Backspace (Dòng 163):**
   ```swift
   // 4. Backspace (keyCode 51 = delete/backspace trên bàn phím Mac).
   if event.keyCode == 51 {
       // Nếu ứng dụng đang có vùng bôi đen (ví dụ vừa Cmd + A xong):
       // Nhường quyền ngay để ứng dụng tự xóa selection trong 1 lần bấm.
       if let input = getTextInput(sender), input.selectedRange().length > 0 {
           CABIBridge.contextReset(handle)
           return false
       }

       let action = CABIBridge.processBackspace(handle)
       return handleAction(action, client: sender)
   }
   ```

3. **Tối ưu hóa `flushPendingComposition` (Dòng 277):**
   Đảm bảo xóa sạch Marked Text trước khi chèn, tránh để lại node đệm trong DOM của Chromium:
   ```swift
   private func flushPendingComposition(_ client: Any?) {
       guard let handle = contextHandle,
             let input = getTextInput(client) else { return }

       let text = CABIBridge.preeditString(handle)
       if !text.isEmpty {
           input.insertText(text, replacementRange: notFoundRange)
       } else {
           input.setMarkedText("", selectionRange: NSRange(location: 0, length: 0), replacementRange: notFoundRange)
       }
       CABIBridge.contextReset(handle)
   }
   ```

---

#### 🪟 B. Trên Windows (`src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs`)

1. **Đóng dứt điểm Composition khi gặp phím bổ trợ (Ctrl/Alt/Win) tại dòng 92 & 136:**
   ```csharp
   if (KeyInputTranslator.IsModifierModifierPressed())
   {
       // Đang có tổ hợp phím tắt (Ctrl+A, Ctrl+C...):
       // Phải chốt từ dở dang và giải phóng phiên composition ngay lập tức!
       if (CompositionManager.HasActiveComposition())
       {
           CompositionManager.EndComposition();
           BridgeStateManager.ResetState();
       }
       return HResult.Ok;
   }
   ```

2. **Kiểm tra vùng chọn trong TSF trước khi nuốt Backspace tại dòng 101 & 157:**
   * Tận dụng `TsfSelectionHelper.GetSelectionRange(pic, ec)`:
   * Nếu selection range đang có độ dài $> 0$ (không bị collapsed):
     * Không gán `*pfEaten = 1`.
     * Để phím Backspace đi thẳng vào ứng dụng để xóa vùng chọn.

---

#### 🐧 C. Trên Linux (`src/BambooMintKey.Fcitx5/engine.cpp`)

1. **Đảm bảo đồng bộ Preedit và Input Context khi gặp phím tắt (Dòng 198):**
   ```cpp
   if (isSystemModifier(keyEvent.key())) {
       flushPendingComposition(ic, state);
       ic->updatePreedit();
       return;
   }
   ```

2. **Xử lý Backspace khi có Selection:**
   * Trong Fcitx5, nếu ứng dụng hỗ trợ surrounding text và có vùng chọn (`selection`), tránh gọi `handleAction` mà nhả phím cho ứng dụng.

---

### 4.3. Biện pháp hỗ trợ riêng cho ứng dụng Web / Google Sheets / Chrome

Trong một số trường hợp cực đoan của Google Sheets (nơi Google Sheets tự bắt sự kiện JavaScript `keydown` và can thiệp vào con trỏ):
* **Tính năng "Khắc phục lỗi gợi ý trình duyệt" (tương tự EVKey):**
  * EVKey sở dĩ xử lý mượt mà trên Google Sheets và Chrome URL bar là vì EVKey nhận diện process name (`chrome.exe`, `msedge.exe`, `brave.exe`) và chuyển sang chế độ **Direct Injection / Clipboard paste** khi cần xóa hoặc chốt từ.
  * Trong tương lai, BambooMintKey có thể bổ sung tùy chọn: **"Tối ưu hóa tương thích Chromium / Google Sheets"** để áp dụng cơ chế commit dứt điểm ngay khi phát hiện ứng dụng thuộc họ Chromium.

---

## 5. Kế hoạch triển khai & Kiểm thử (Action Plan)

1. **Thực hiện bản vá trên macOS trước:**
   * Chỉnh sửa [BambooMintKeyController.swift](file:///Users/lmo1720/Self-App/BambooMintKey/src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift) theo mục 4.2.A.
   * Build lại component macOS IMK và deploy vào `/Library/Input Methods`.
   * Kiểm tra trực tiếp trên:
     1. Khung chat AI của Antigravity IDE.
     2. Ô tính Google Sheets trên Google Chrome.
     3. Ứng dụng TextEdit chuẩn của macOS.
2. **Áp dụng bản vá cho Windows TSF:**
   * Chỉnh sửa `KeyEventSinkImpl.cs` theo mục 4.2.B.
   * Kiểm thử trên Chrome và Excel cho Windows.
3. **Áp dụng cho Linux Fcitx5:**
   * Chỉnh sửa `engine.cpp` theo mục 4.2.C.
