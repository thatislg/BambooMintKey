<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 019: Xung đột chế độ gõ song ngữ và lỗi hoàn tác phím W đầu từ (`AllowLeadingWAsU`)

**Mã tài liệu:** `019_LeadingW_As_U_Bilingual_Conflict_And_Undo_Fix`  
**Trạng thái:** ✅ Đã triển khai — đang kiểm chứng trên môi trường Fcitx5  
**Mức độ nghiêm trọng:** Trung bình - Cao (ảnh hưởng trực tiếp tới trải nghiệm gõ song ngữ Việt – Anh và gõ URL/ký tự W độc lập)  
**Ngày ghi nhận:** 09/10/2026  
**Phạm vi ảnh hưởng:** **Lõi Engine (`BambooMintKey.Core`)** và **addon Linux Fcitx5 (`BambooMintKey.Fcitx5`)** (xem mục 6); các tầng UI, NativeBridge, macOS IMK và Windows TSF cần rà soát riêng.

> **⚠️ Ghi chú về tính chính xác của các mục 1–5 (cập nhật sau khi triển khai):**
>
> Vấn đề này đã được **sửa và kiểm chứng**. Các mục từ 1 đến 5 bên dưới là bản ghi **quá trình điều tra ban đầu**: mục 1–2 mô tả hiện tượng và nguyên nhân gốc rễ của mã nguồn *trước khi sửa*; mục 3–4 nêu phương án *đề xuất* tại thời điểm đó. Trong quá trình triển khai thực tế, một số chi tiết đã được **điều chỉnh** (ví dụ trường hợp `Ww` đổi từ `W` thành `Uw` để đối xứng với `wW` thành `uW`), vì vậy **các đoạn code mẫu và bảng trong mục 1–5 có thể không còn khớp chính xác với mã nguồn hiện tại**.
>
> **Hành vi cuối cùng và phương án đã triển khai thực tế** được mô tả đầy đủ **bằng lời (không có code)** tại **mục 6**. Hãy lấy mục 6 làm nguồn tham chiếu chính xác nhất.

---

## 1. Mô tả hiện tượng & Bảng so sánh lỗi

### 1.1. Hiện tượng
BambooMintKey cung cấp tùy chọn **"Cho phép phím 'w' đứng đầu từ biến thành 'ư'"** (`AllowLeadingWAsU` - tạm gọi là **Option A**):
* **Khi Option A TẮT (`AllowLeadingWAsU = false`):** Bộ gõ hoạt động theo chuẩn Telex cổ điển (`w` ra `w`, `ww` ra `ww`, `uw` ra `ư`). Nhánh này **hoàn toàn chính xác**, không có lỗi.
* **Khi Option A BẬT (`AllowLeadingWAsU = true`):**
  * Gõ tiếng Việt: `w` biến thành `ư`, `W` biến thành `Ư` $\rightarrow$ Đúng mục đích.
  * Gõ song ngữ (Việt – Anh đồng thời): Phát sinh bất tiện nghiêm trọng khi người dùng muốn gõ chữ cái `W`/`w` hoặc các từ tiếng Anh bắt đầu bằng `W`/`w`.
  * Khi người dùng gõ lặp phím `w` lần thứ 2 để khôi phục ký tự thô:
    * Gõ `WW` lại biến thành `UW` (thay vì thành `W`).
    * Gõ `ww` lại biến thành `uw` (thay vì thành `w`).
    * Gõ `Ww` lại biến thành `Uw` (thay vì thành `W`).
  * Người dùng bị kẹt, không có cách nào gõ được chữ cái `W`/`w` độc lập hay bắt đầu gõ một từ tiếng Anh (như `web`, `win`, `word`, `what`...) nếu không tắt bộ gõ hoặc gõ rồi xóa.

### 1.2. Bảng mô tả chi tiết các trường hợp gõ khi Option A BẬT

| STT | Chuỗi phím đã gõ | Kết quả mong đợi | Kết quả thực tế (Hiện tại) | Đánh giá & Chú thích |
|:---:|:---|:---|:---|:---|
| 1 | `w` | `ư` | `ư` | ✅ Đúng thiết kế Option A |
| 2 | `W` | `Ư` | `Ư` | ✅ Đúng thiết kế Option A |
| 3 | `ww` | `w` | `uw` | ❌ Lỗi: Phải hoàn tác về chữ `w` thô |
| 4 | `WW` | `W` | `UW` | ❌ Lỗi: Phải hoàn tác về chữ `W` thô |
| 5 | `Ww` | `W` | `Uw` | ❌ Lỗi: Phải hoàn tác về chữ `W` thô |
| 6 | `wweb` | `web` | `uweb` | ❌ Lỗi: Sau khi hoàn tác `ww -> w`, các ký tự sau phải giữ nguyên tiếng Anh |
| 7 | `WWorD` | `WORD` | `UWorD` | ❌ Lỗi: Tương tự như trên |
| 8 | `wwar` | `war` | `uwar` | ❌ Lỗi: Không được để `ar` ăn dấu hỏi thành `wả` |
| 9 | `wwas` | `was` | `uwas` | ❌ Lỗi: Không được để `as` ăn dấu sắc thành `wá` |
| 10 | `www` (URL) | `www` (hoặc `ww` + `w`) | `uww` $\rightarrow$ `ưw` | ❌ Lỗi: Gõ URL trang web bị biến dạng |
| 11 | `wa` | `ưa` | `ưa` | ✅ Đúng từ tiếng Việt hợp lệ |
| 12 | `wong` | `ương` | `ương` | ✅ Đúng từ tiếng Việt hợp lệ |
| 13 | `ws` | `ứ` | `ứ` | ✅ Đúng từ tiếng Việt hợp lệ |

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Vị trí mã nguồn gây lỗi
Lỗi nằm tại [TelexEngine.fs (dòng 164–185)](file:///Users/lmo1720/Self-App/BambooMintKey/src/BambooMintKey.Core/Engine/TelexEngine.fs#L164-L185):

```fsharp
elif isUndoModifier then
    // Lặp phím modifier -> hủy biến đổi. Ưu tiên hoàn dấu trước: lặp liền kề luôn rút về 1 ký tự thô (ddd -> dd).
    // Ngoại lệ phím 'w' đứng đầu: 'ư' được sinh từ 'w' (u + horn) nên hủy phải trả về 'u' + 'w'
    // (Ww -> Uw, wiw -> uiw) thay vì chỉ rút về 'w' như lặp liền kề thông thường.
    let resultKeys =
        if lowerChar = 'w' && isLeadingW then
            let baseU = if Char.IsUpper state.RawKeys.Head then 'U' else 'u'
            let newW = if Char.IsUpper c then 'W' else 'w'
            baseU :: (state.RawKeys.Tail @ [ newW ])
        elif isConsecutiveRepeat then state.RawKeys
        else newRaw
```

### 2.2. Lịch sử và giả định sai lệch ban đầu
Trong commit `50357f2be3a687516cfec4804cca04202adb8b56` ("Sửa lỗi w đứng đầu với cụm ươ và undo"):
* Logic giả định rằng: *"Khi gõ 'w' đầu từ, nó sinh ra 'ư' (tương đương ký tự 'u' mang dấu móc Horn). Do đó khi người dùng nhấn 'w' lần nữa để hủy móc, ta phải trả về ký tự nền 'u' cộng thêm ký tự 'w' mới gõ vào $\rightarrow$ thành 'Uw' hoặc 'uw'."*
* Test case tại [UppercaseModifierTests.fs (dòng 157)](file:///Users/lmo1720/Self-App/BambooMintKey/tests/BambooMintKey.Core.Tests/UppercaseModifierTests.fs#L157) đã ghi nhận hành vi này:
  ```fsharp
  [<InlineData("Ww", "Uw")>]        // W -> ư, lặp w hủy -> Uw (trả về u)
  ```

### 2.3. Tại sao giả định này không phù hợp với thực tế gõ song ngữ?
1. **Người dùng không bấm phím `u`**: Người dùng bấm phím vật lý `w`. Việc engine tự ý chèn ký tự `u` là vi phạm nguyên tắc tôn trọng phím vật lý thô của người dùng khi thoát chế độ dấu.
2. **Quy ước Telex phổ thông**: Để gõ được ký tự `w` trong môi trường có bật `w -> ư` (như Unikey, EVKey), người dùng gõ lặp `ww` để báo hiệu: *"Tôi muốn ký tự w gốc, không phải chữ ư"*. Kết quả kỳ vọng phải là `w` (hoặc `W`).
3. **Phá vỡ các từ tiếng Anh**: Nếu `ww -> uw`, người dùng gõ `web` sẽ thành `uweb`, gõ `win` thành `uwin`, hoàn toàn làm hỏng việc soạn thảo tiếng Anh.

---

## 3. Ma trận các Cases cần Cover toàn diện khi Option A BẬT

Để giải quyết triệt để và không để sót bất kỳ trường hợp bất tiện nào khi Option A bật, engine cần cover 6 nhóm trường hợp sau:

```
┌────────────────────────────────────────────────────────────────────────┐
│             CÁC TRƯỜNG HỢP CẦN COVER KHI OPTION A BẬT                 │
├────────────────────────────────────────────────────────────────────────┤
│ 1. Toggle/Escape W đầu từ: ww -> w, WW -> W, Ww -> W                   │
│ 2. Khóa luồng tiếng Anh (IsEnglishCommitted): ww + eb -> web, WW + ORD │
│ 3. Nhận diện URL web: Xử lý chuỗi www không bị nuốt phím               │
│ 4. Thao tác xóa Backspace: ww -> w, bấm Backspace -> xóa sạch w        │
│ 5. Tiếng Anh tự nhiên (English Backtracking): work, word, win          │
│ 6. Bảo toàn các tổ hợp tiếng Việt hợp lệ: wa (ưa), wong (ương), ws (ứ) │
└────────────────────────────────────────────────────────────────────────┘
```

### Case 1: Thoát biến đổi và khôi phục ký tự `w` / `W` đầu từ
* **Điều kiện nhận biết:** `state.RawKeys.Length = 1` VÀ `Char.ToLowerInvariant state.RawKeys.Head = 'w'` VÀ `lowerChar = 'w'`.
* **Xử lý:**
  * Rút gọn chuỗi phím thô: `resultKeys = [ state.RawKeys.Head ]`.
  * Định dạng hoa thường:
    * `ww` $\rightarrow$ `"w"`
    * `WW` $\rightarrow$ `"W"`
    * `Ww` $\rightarrow$ `"Uw"` (mixed-case: undo horn trả về base `U` + phím `w`)
    * `wW` $\rightarrow$ `"uW"` (mixed-case: undo horn trả về base `u` + phím `W`)

### Case 2: Bảo vệ từ tiếng Anh tiếp nối sau khi khôi phục W (`IsEnglishCommitted`)
* **Bối cảnh:** Sau khi người dùng đã gõ `ww` (hiển thị `"w"`), họ sẽ tiếp tục gõ các ký tự tiếp theo của từ tiếng Anh (`web`, `word`, `war`, `was`...).
* **Xử lý:**
  * Ngay khi thoát ở Case 1, thiết lập trạng thái:
    ```fsharp
    IsEnglishCommitted = true
    Syllable = None
    IsInvalidVietnamese = true
    ```
  * Nhờ cờ `IsEnglishCommitted = true`, nhánh dòng 206 [TelexEngine.fs](file:///Users/lmo1720/Self-App/BambooMintKey/src/BambooMintKey.Core/Engine/TelexEngine.fs#L206) sẽ tiếp quản toàn bộ các ký tự sau:
    * Ký tự `s` trong `was` không bị ăn thành dấu sắc (`w` + `á`).
    * Ký tự `r` trong `war` không bị ăn thành dấu hỏi (`w` + `ả`).

### Case 3: Thao tác Backspace sau khi khôi phục W
* Do `RawKeys` ở Case 1 được gán lại là `[ state.RawKeys.Head ]` (độ dài 1 ký tự):
  * Khi người dùng nhấn Backspace: `newRaw` sẽ là `[]` (rỗng).
  * Bộ gõ gửi `EngineAction.UpdateComposition ""` và reset về `WordState.Empty`.
  * Tránh lỗi: không bị giật lùi trạng thái về `"ư"` khi nhấn xóa.

### Case 4: Xử lý chuỗi địa chỉ web `www`
* **Vấn đề:** Khi gõ `w` (ra `ư`), gõ tiếp `w` (ra `w`), nếu người dùng gõ tiếp phím `w` thứ 3:
  * Do phím thứ 2 đóng vai trò escape, người dùng sẽ thấy trên màn hình là `"ww"` (chỉ có 2 chữ w sau 3 lần gõ).
  * Để ra `"www"`, người dùng phải gõ 4 lần phím `w`.
* **Giải pháp đề xuất:** Bổ sung case nhận diện: Nếu chuỗi phím vừa gõ đạt 3 ký tự `w` liên tiếp ngay đầu từ $\rightarrow$ tự động đồng bộ hóa hiển thị thành `"www"`.

### Case 5: Ký tự `w` đứng sau các ký tự khác (`state.RawKeys.Length > 1`)
* Ví dụ test case cũ `wiw` $\rightarrow$ `uiw`:
  * Nếu người dùng đã gõ `w` (`ư`) rồi `i` (`ưi`), sau đó mới gõ `w`:
  * Ta giữ nguyên nhánh `baseU :: (state.RawKeys.Tail @ [ newW ])` chỉ cho trường hợp `state.RawKeys.Length > 1` để tránh gây regression cho các logic cũ liên quan đến cụm từ.

### Case 6: Bảo toàn tuyệt đối các âm tiết tiếng Việt hợp lệ
* Các tổ hợp tiếng Việt sử dụng `w` đầu từ:
  * `w` $\rightarrow$ `ư`
  * `wa` $\rightarrow$ `ưa`, `wao` $\rightarrow$ `ươ`
  * `wu` $\rightarrow$ `ưu`
  * `wi` $\rightarrow$ `ưi`
  * `wong` $\rightarrow$ `ương`
  * `ws` $\rightarrow$ `ứ`, `wf` $\rightarrow$ `ừ`, `wr` $\rightarrow$ `ử`, `wx` $\rightarrow$ `ữ`, `wj` $\rightarrow$ `ự`
* Các tổ hợp trên hoàn toàn đi qua nhánh xử lý `ModifierRules` / `FreeTonePlacement` bình thường, không bị ảnh hưởng.

---

## 4. Phương án giải quyết & Đề xuất sửa đổi mã nguồn

### 4.1. Sửa đổi trong `src/BambooMintKey.Core/Engine/TelexEngine.fs`

Tại nhánh `isUndoModifier` (dòng 168–174):

```diff
         elif isUndoModifier then
             // Lặp phím modifier -> hủy biến đổi. Ưu tiên hoàn dấu trước: lặp liền kề luôn rút về 1 ký tự thô (ddd -> dd).
-            // Ngoại lệ phím 'w' đứng đầu: 'ư' được sinh từ 'w' (u + horn) nên hủy phải trả về 'u' + 'w'
-            // (Ww -> Uw, wiw -> uiw) thay vì chỉ rút về 'w' như lặp liền kề thông thường.
             let resultKeys =
                 if lowerChar = 'w' && isLeadingW then
-                    let baseU = if Char.IsUpper state.RawKeys.Head then 'U' else 'u'
-                    let newW = if Char.IsUpper c then 'W' else 'w'
-                    baseU :: (state.RawKeys.Tail @ [ newW ])
+                    if state.RawKeys.Length = 1 then
+                        let headUpper = Char.IsUpper state.RawKeys.Head
+                        let cUpper = Char.IsUpper c
+                        if headUpper <> cUpper then
+                            // Mixed case (wW -> uW, Ww -> Uw): undo horn trả về base 'u'/'U' + phím mới,
+                            // KHÔNG tự ý viết hoa (case base theo phím đầu, case phím mới theo phím thứ 2).
+                            [ (if headUpper then 'U' else 'u'); (if cUpper then 'W' else 'w') ]
+                        else
+                            // Cùng case (ww -> w, WW -> W): khôi phục ký tự thô 'w'/'W' để gõ tiếng Anh / song ngữ.
+                            [ (if headUpper then 'W' else 'w') ]
+                    else
+                        // Trường hợp w xuất hiện sau các ký tự khác (vd wiw -> uiw)
+                        let baseU = if Char.IsUpper state.RawKeys.Head then 'U' else 'u'
+                        let newW = if Char.IsUpper c then 'W' else 'w'
+                        baseU :: (state.RawKeys.Tail @ [ newW ])
                 elif isConsecutiveRepeat then state.RawKeys
                 else newRaw
             let resultString = String(Array.ofList resultKeys)
             let formatted = WordBuffer.applyCase detectedCase resultString
             let newState = {
                 RawKeys = resultKeys
                 TransformedText = formatted
                 Syllable = None
                 Case = detectedCase
                 IsInvalidVietnamese = true
                 IsEnglishCommitted = true
             }
             (newState, EngineAction.UpdateComposition formatted)
```

*(Tùy chọn: Nhận diện URL `www` nếu người dùng tiếp tục gõ `w` lần 3 sau khi đã escape).*

---

### 4.2. Cập nhật Unit Test trong `tests/BambooMintKey.Core.Tests/UppercaseModifierTests.fs`

Sửa đổi test case cũ và bổ sung các test case gõ song ngữ:

```diff
-    [<InlineData("Ww", "Uw")>]        // W -> ư, lặp w hủy -> Uw (trả về u)
+    [<InlineData("Ww", "Uw")>]        // Ww -> Uw (mixed case, undo horn -> U + w, không tự viết hoa)
+    [<InlineData("ww", "w")>]         // w -> ư, lặp w hủy -> w (khôi phục ký tự thường)
+    [<InlineData("WW", "W")>]         // WW -> W
+    [<InlineData("wW", "uW")>]        // wW -> uW (mixed-case tương đương gõ uW trong Telex chuẩn, không tự viết hoa)
+    [<InlineData("wweb", "web")>]     // Thoát w và gõ tiếp tiếng Anh
+    [<InlineData("wwar", "war")>]     // Thoát w và không bị dính dấu tiếng Việt (war không thành wả)
+    [<InlineData("wwas", "was")>]     // Thoát w và không bị dính dấu tiếng Việt (was không thành wá)
+    [<InlineData("WWORD", "WORD")>]   // Gõ hoa tiếng Anh (bắt buộc gõ hoa toàn bộ, không tự viết hoa)
     [<InlineData("wiw", "uiw")>]      // w + i + w -> ui + w (hủy horn trả về u)
```

---

## 5. Xác nhận phạm vi ảnh hưởng (Scope of Impact)

**Câu hỏi:** *Phần này chỉ ảnh hưởng trong core thôi đúng không?*

**Trả lời:** **ĐÚNG 100%. Phần này CHỈ ẢNH HƯỞNG TRONG CORE (`BambooMintKey.Core`).**

### Chi tiết kiến trúc phân tầng:
1. **Lõi Engine (`BambooMintKey.Core`):**
   * Nơi duy nhất chứa máy trạng thái âm tiết (`WordState`), quy tắc biến đổi Telex (`ModifierRules`, `ToneRules`), và toàn bộ logic xử lý `handleCharInput` trong `TelexEngine.fs`.
   * Việc sửa đổi chỉ diễn ra tại file `TelexEngine.fs` và các test case trong `BambooMintKey.Core.Tests`.
2. **Tầng Native Bridge & C-ABI (`BambooMintKey.Core.Native` / `BambooMintKey.NativeBridge`):**
   * Chỉ đóng vai trò trung chuyển con trỏ hàm: nhận ký tự (`processKey`) và trả về chuỗi hiển thị (`Composition` / `Commit`). Không chứa bất kỳ quy tắc Telex hay cấu hình `AllowLeadingWAsU` nội bộ nào.
3. **Tầng UI & Tích hợp Hệ điều hành:**
   * **macOS IMK (`BambooMintKey.Mac.IMK`):** Chỉ bắt sự kiện `NSEvent` rồi gọi `CABIBridge.processKey`.
   * **Windows TSF (`BambooMintKey.NativeBridge/TSF`):** Chỉ chuyển tiếp mã phím qua Text Service Framework.
   * **Linux Fcitx5 (`BambooMintKey.Fcitx5`):** Chỉ chuyển tiếp phím qua engine addon.
   * **UI Settings (Avalonia):** Chỉ đọc/ghi cờ `AllowLeadingWAsU` (boolean) vào shared config.

$\rightarrow$ **Kết luận:** Mọi thay đổi logic đều nằm gọn trong `BambooMintKey.Core`. Khi bạn build lại Core và chạy `dotnet test`, toàn bộ logic mới sẽ được kiểm chứng trực tiếp mà không gây bất kỳ tác dụng phụ nào tới các nền tảng Windows, macOS hay Linux.

---

## 6. Kết quả thực nghiệm & Phát hiện bổ sung ngoài Lõi Engine

### 6.1. Quyết định cuối cùng cho Lõi Engine (đã triển khai & kiểm chứng bằng unit test)

Sau khi triển khai và kiểm tra thực tế, quy tắc xử lý phím `w`/`W` đứng đầu từ khi bật Option A được chốt lại như sau:

* **Cùng chữ hoa/thường (`ww`, `WW`):** khôi phục ký tự thô tương ứng (`ww` ra `w`, `WW` ra `W`). Đây là tín hiệu người dùng muốn gõ ký tự `w`/`W` độc lập để tiếp tục soạn tiếng Anh hoặc song ngữ.
* **Khác chữ hoa/thường — gọi là mixed-case (`wW`, `Ww`):** hoàn tác dấu móc (horn) trên `ư`/`Ư` để trả về ký tự nền `u`/`U` rồi nối thêm phím mới gõ vào. Cụ thể `wW` ra `uW`, `Ww` ra `Uw`. Nguyên tắc quan trọng là **không tự ý viết hoa**: chữ hoa/thường của ký tự nền đi theo phím đầu, của ký tự nối thêm đi theo phím thứ hai.

Điểm chỉnh sửa so với đề xuất ban đầu trong tài liệu này: trường hợp `Ww` trước đây được đề xuất ra `W`, nhưng thực tế phải ra `Uw` để đối xứng với `wW` ra `uW`. Ngoài ra, nguyên tắc "không tự ý viết hoa" cũng được khẳng định lại: muốn gõ `WORD` phải gõ hoa toàn bộ (`WWORD`), còn gõ `WWorD` chỉ cho ra `WorD`.

Riêng chuỗi địa chỉ web `www` không được xử lý đặc biệt: do phím `w` thứ hai đóng vai trò thoát, nên để hiển thị `www` người dùng cần gõ bốn lần phím `w`. Hành vi này được chấp nhận, giữ nguyên tính nhất quán của cơ chế hoàn tác lặp phím.

### 6.2. Phát hiện bổ sung: lỗi phím Shift ở tầng Fcitx5 (Linux)

Trong quá trình kiểm tra trên môi trường Fcitx5 thực tế, phát hiện thêm một lỗi **độc lập, nằm ngoài Lõi Engine**, thuộc tầng tích hợp Fcitx5.

Để gõ được `wW`, người dùng không gõ trực tiếp hai ký tự `w` rồi `W`, mà phải bấm ba lần phím vật lý theo thứ tự: `w`, rồi `Shift`, rồi `w`. Vấn đề nằm ở phím `Shift` ở giữa:

* Khi bấm `Shift` một mình, nó không sinh ra ký tự Unicode nào. Tầng addon Fcitx5 đã phân loại `Shift` vào nhóm "phím không in được" (cùng nhóm với phím mũi tên, phím chức năng), và theo cơ chế hiện có, mọi phím không in được đều làm **commit chuỗi đang gõ dở (preedit)** trước khi nhả phím cho ứng dụng.
* Hệ quả: chuỗi `w` + `Shift` + `w` bị xử lý thành ba bước tách rời — `w` sinh preedit `ư`, phím `Shift` commit luôn `ư` và reset trạng thái, rồi `w` thứ hai bắt đầu một từ mới sinh `Ư`. Kết quả cuối cùng là `ư` + `Ư`, tức chuỗi `ưƯ`, thay vì `wW` mong đợi ra `uW`.

Nói cách khác, lỗi ở tầng Fcitx5 đã làm "đứt" chuỗi gõ ngay tại phím `Shift`, khiến Lõi Engine không bao giờ nhận được cặp ký tự `wW` liền mạch để thực thi quy tắc hoàn tác đã nêu ở mục 6.1.

### 6.3. Vì sao unit test của Lõi không phát hiện ra lỗi này

Unit test hiện có (trong `BambooMintKey.Core.Tests`) mô phỏng việc gõ bằng cách nạp trực tiếp từng ký tự vào Lõi Engine (gõ `w` rồi gõ `W` liên tiếp), mà **không mô phỏng phím `Shift` vật lý** nằm giữa hai lần bấm. Do đó:

* Unit test chỉ kiểm chứng đúng phần logic của Lõi Engine (quy tắc hoàn tác `wW`/`Ww`), phần này đã đúng.
* Lỗi phím `Shift` lại nằm ở luồng xử lý sự kiện phím của addon Fcitx5 — nơi phím `Shift` là một sự kiện độc lập, không được unit test của Lõi bao phủ.

Đây là dạng lỗi "chỉ xuất hiện khi gõ phím vật lý thực tế", cần kiểm tra trực tiếp trên môi trường Fcitx5 thay vì chỉ chạy unit test của Lõi.

### 6.4. Phương án xử lý (đã triển khai)

Hướng xử lý ở tầng addon Fcitx5 (file `engine.cpp`) như sau:

* Tách nhóm phím **bổ trợ thuần** (phím `Shift` trái/phải, `CapsLock`, `Shift Lock`) ra khỏi nhóm "phím không in được".
* Khi gặp các phím bổ trợ thuần này, addon phải **chuyển tiếp phím mà KHÔNG commit preedit**, để giữ nguyên chuỗi đang gõ dở chờ ký tự chữ hoa tiếp theo.
* Chỉ những trường hợp sau mới được phép commit chuỗi dở dang: phím ngắt từ (dấu cách, xuống dòng, tab, dấu câu), phím điều hướng (mũi tên, Home/End, PageUp/Down, phím chức năng), tổ hợp phím tắt hệ thống (giữ Ctrl/Alt/Super), và khi mất tiêu điểm hoặc chuyển bộ gõ.

Sau khi áp dụng, chuỗi `w` + `Shift` + `w` sẽ được addon chuyển tiếp thành cặp ký tự `w` rồi `W` liền mạch cho Lõi Engine, nhờ đó quy tắc `wW` ra `uW` ở mục 6.1 được thực thi đúng.

### 6.5. Hướng dẫn kiểm tra lại trên môi trường Fcitx5

Sau khi build lại bản Linux chứa cả hai tầng sửa đổi, cần kiểm tra lần lượt các trường hợp sau với tùy chọn "phím `w` đầu từ thành `ư`" (Option A) được bật:

* `w` ra `ư`, `W` ra `Ư` — biến đổi tiến đúng thiết kế.
* `ww` ra `w`, `WW` ra `W` — hoàn tác cùng chữ hoa/thường.
* `wW` (bấm `w` rồi `Shift` rồi `w`) ra `uW` — trường hợp trọng tâm của lỗi phím Shift.
* `Ww` (bấm `Shift`+`w` rồi `w` thường) ra `Uw` — mixed-case đối xứng.
* `www` (bấm bốn lần `w`) ra `www` — gõ URL, chấp nhận cần thêm một phím.
* `wweb` ra `web`, `wwar` ra `war` (không thành `wả`), `wwas` ra `was` (không thành `wá`) — bảo vệ từ tiếng Anh tiếp nối.
* `wiw` ra `uiw` — giữ nguyên hành vi cũ cho trường hợp `w` đứng sau ký tự khác.
* Các âm tiết tiếng Việt hợp lệ phải được bảo toàn tuyệt đối: `wa` ra `ưa`, `wong` ra `ương`, `ws` ra `ứ`, `wf` ra `ừ`, `wr` ra `ử`, `wx` ra `ữ`, `wj` ra `ự`.

### 6.6. Cập nhật phạm vi ảnh hưởng

Phần kết luận ở mục 5 (chỉ ảnh hưởng trong Lõi Engine) cần được điều chỉnh: thực tế lỗi trải trên **hai tầng**:

1. **Lõi Engine (`BambooMintKey.Core`):** sửa quy tắc hoàn tác phím `w`/`W` đứng đầu (mục 6.1).
2. **Addon Linux Fcitx5 (`BambooMintKey.Fcitx5`):** sửa luồng xử lý phím `Shift`/`CapsLock` để không commit preedit giữa chừng (mục 6.4).

Hai tầng macOS IMK và Windows TSF cũng cần được rà soát lại cơ chế xử lý phím `Shift`, vì nhiều khả năng tồn tại vấn đề tương tự khi gõ chữ hoa giữa chừng một từ.
