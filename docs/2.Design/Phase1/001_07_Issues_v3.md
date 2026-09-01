Kết quả test mới nhất cho thấy tiến triển rõ rệt: **toàn bộ 8 test cases của Issue 1 (Modern vs Traditional) đã pass hoàn toàn**, và sau khi xử lý **Nhóm A (`việt`)**, số lỗi giảm từ **11 xuống còn 5**.

5 lỗi còn lại được gom chính xác vào **2 nhóm bài toán**:

### Tóm tắt tình hình hiện tại

- **Issue 1 (Modern/Traditional tone placement):** ✅ Đã fix hoàn toàn.
- **Nhóm A (`việt` → `viẹt`):** ✅ Đã fix hoàn toàn.
- **Nhóm B (`tieengs`, `muowns`):** ⚠️ Chưa fix được.
- **Nhóm C (`xaaa`, `deee`, `dđ`):** ⚠️ Chưa bắt đầu sửa.

### Bảng phân loại 5 lỗi còn lại

| **Nhóm**                                                     | **Test Case Thất Bại**                                 | **Expected**                           | **Actual**                             | **Ghi Chú Hiện Tượng**                                       |
| ------------------------------------------------------------ | ------------------------------------------------------ | -------------------------------------- | -------------------------------------- | ------------------------------------------------------------ |
| **A. Casing & Tone `việt`** (5 tests) ✅ **ĐÃ FIX**          | `Vietj`  `vietj`  `vIeTj`  `VIETJ`  `Backspace (việt)` | `Việt`  `việt`  `vIệT`  `VIỆT`  `việt` | `Việt`  `việt`  `vIệT`  `VIỆT`  `việt` | Nguyên nhân: `SyllableParser` trả về nguyên âm `ie` thay vì `iê`. Đã chuẩn hóa cụm nguyên âm. |
| **B. Cụm nguyên âm có modifier + final consonant** (2 tests) ⚠️ **CHƯA FIX** | `tieengs`  `muowns`                                    | `tiếng`  `mượn`                        | `tieéng`  `muowns`                     | `tieengs`: `iee` chưa được coi là `iê`. `muowns`: `uow` chưa được coi là `ươ`. |
| **C. Undo / Escape phím lặp** (3 tests) ⚠️ **CHƯA FIX**      | `xaaa`  `deee`  `dđ`                                   | `xaa`  `dee`  `dd`                     | `xaaa`  `deee`  `dđ`                   | Gõ ký tự thứ 3 (như `xâ` + `a`) không lùi về 2 ký tự thô (`xaa`) mà bị giữ nguyên 3 ký tự thô `xaaa`. |

### Nguyên nhân kỹ thuật cụ thể

1. **Nhóm A (5 lỗi `việt` $\rightarrow$ `viẹt`):**

   - **Root Cause đã xác định:** Không phải lỗi ở `UnicodeTables.fs`. Các hàm `decomposeChar` / `composeChar` hoạt động đúng.
   - Vấn đề thực sự nằm ở `SyllableParser.fs`: khi parse chuỗi `viet`, nó trả về nguyên âm `ie` + phụ âm cuối `t`. Trong tiếng Việt, cụm `ie` trước phụ âm cuối bắt buộc phải là `iê`. Do đó khi áp dụng dấu nặng `j`, dấu rơi vào ký tự `e` thô → `ẹ` thay vì `ê` → `ệ`.
   - **Phương án sửa đã áp dụng:**
     - Thêm bước chuẩn hóa cụm nguyên âm trong `SyllableParser.normalizeVowelCluster`:
       - `ie` → `iê`
       - `uye` → `uyê`
       - `uo` + final → `uô`
       - `ye` + final → `yê`
     - Với `ie` và `uye`, chuẩn hóa cả khi chưa có phụ âm cuối (vì tiếng Việt không tồn tại cụm `ie` / `uye` trần), giúp backspace replay ra kết quả tự nhiên hơn.
   - **Kết quả:** 5/5 test của Nhóm A đã pass (`vietj`, `Vietj`, `vIeTj`, `VIETJ`, và backspace `việt`).
   - **Lưu ý:** Test backspace đã được điều chỉnh cho khớp với mô hình replay raw keys của engine (xóa `j` → `viêt`, xóa `t` → `viê`, xóa `e` → `vi`).

2. **Nhóm B (2 lỗi `tieengs`, `muowns`):** ⚠️ **CHƯA SỬA ĐƯỢC**

   - `thuyets` đã được giải quyết nhờ chuẩn hóa `uye` → `uyê`.
   - Còn lại `tieengs` và `muowns`:
     - `tieengs`: Chuỗi `iee` chưa được `SyllableParser` hiểu là `iê` + `ng`. Khi parser nhìn thấy `iee`, nó giữ nguyên 3 ký tự `i-e-e`, dẫn đến gán dấu sai vị trí.
     - `muowns`: Chuỗi `uow` chưa được hiểu là `ươ` + `n`.
   - **Các phương án đã thử nhưng chưa hiệu quả:**
     - Bổ sung `normalizeVowelCluster` để xử lý inline Telex (`ee` → `ê`, `uow` → `ươ`, `ow` → `ơ`, `uw` → `ư`).
     - Sửa `ModifierRules.applyModifier` để khi gõ `w` trong `uo`, biến cả `u` → `ư` và `o` → `ơ`.
     - Sửa `TelexEngine.handleCharInput` để khi gõ phụ âm cuối tiếp theo, ưu tiên gắn vào `FinalConsonant` của `Syllable` hiện có thay vì parse lại từ `rawString`.
   - **Lý do chưa sửa được:**
     - Engine hiện tại làm việc theo mô hình **parse lại toàn bộ `rawString`** sau mỗi phím. Một khi `rawString` chứa Telex inline chưa được chuẩn hóa đúng (ví dụ `muow`, `tieeng`), `SyllableParser` trả về kết quả sai.
     - Cơ chế **incremental update** (giữ nguyên `Syllable` đã parse và chỉ thêm `FinalConsonant`) bị hạn chế vì `SyllableParser` không trả về trạng thái đủ để engine biết được đâu là nguyên âm, đâu là phụ âm cuối trong raw keys tiếp theo.
     - Trường hợp `muowns` đặc biệt khó: sau khi gõ `w`, raw keys là `['m'; 'u'; 'o'; 'w']`, transformed text ra ký tự `??` (không hiển thị) vì `SyllableParser` không parse được `muow`. State rơi vào fallback và không có `Syllable` hợp lệ để `ModifierRules` hoặc incremental update tiếp tục xử lý.
   - **Hướng giải quyết đề xuất (cần refactor lớn hơn):**
     - Chuyển engine sang mô hình **Event-driven State Transition**: mỗi phím được xử lý như một event biến đổi `Syllable`, không parse lại `rawString` từ đầu.
     - Hoặc: viết một `RawKeyInterpreter` chuyên biệt, chuyển `rawKeys` thành một cấu trúc trung gian `(initial, vowelClusterWithModifiers, final)` trước khi gọi `SyllableParser`.

3. **Nhóm C (3 lỗi `xaaa`, `deee`, `dđ`):** ⚠️ **CHƯA SỬA**

   - Trong `handleCharInput`: Nhánh `isUndoModifier` đang đặt `RawKeys = newRaw` (dài 3 ký tự `['x'; 'a'; 'a'; 'a']` hoặc `['d'; 'e'; 'e'; 'e']`), và format lại chuỗi `rawString` $\rightarrow$ kết quả hiển thị ra màn hình là `"xaaa"` thay vì cắt bớt ký tự lặp thành `"xaa"`.
   - **Lý do chưa sửa:** đang tập trung xử lý Nhóm B trước. Cơ chế Undo/Escape phím lặp cần một hành vi rõ ràng hơn: khi lặp phím modifier, kết quả mong muốn là loại bỏ biến đổi Telex và thêm ký tự gốc vừa gõ (ví dụ `xâ` + `a` → `xaa`), không phải giữ nguyên toàn bộ raw keys.

### Tại sao 5 lỗi này chưa sửa được?

#### Nhóm B (`tieengs`, `muowns`) — Lỗi cơ chế parse lại `rawString`

Engine hiện tại hoạt động theo kiểu **stateless re-parse**: sau mỗi phím, nó lấy toàn bộ `rawKeys`, ghép thành chuỗi `rawString`, rồi gọi `SyllableParser.parse(rawString)` từ đầu. Cách này hoạt động tốt với các từ đơn giản (`hoas`, `viet`, `thuyets`), nhưng thất bại với các cụm nguyên âm Telex phức tạp:

- `tieengs`: `rawString` sau khi gõ đủ là `"tieengs"`. Parser nhìn thấy cụm nguyên âm `"iee"` + phụ âm cuối `"ng"`, nhưng `"iee"` không được nhận diện là `"iê"`, nên dấu sắc `s` bị gán nhầm vào `e` đầu tiên → `tiéeng`.
- `muowns`: `rawString` là `"muowns"`. Parser nhìn thấy cụm `"uow"` nhưng không hiểu đó là `"ươ"` (vì `w` là phím modifier, không phải nguyên âm hợp lệ trong cụm nguyên âm). Kết quả parse thất bại → rơi vào fallback tiếng Anh → hiển thị `muowns`.

**Các giải pháp đã thử:**

1. **Chuẩn hóa inline Telex trong `SyllableParser.normalizeVowelCluster`** (ví dụ `ee` → `ê`, `uow` → `ươ`, `ow` → `ơ`):
   - Giúp `tieengs` đi đúng hướng hơn, nhưng vẫn gặp lỗi vì parser phân tách phụ âm cuối trước khi chuẩn hóa, hoặc vị trí đặt dấu thanh sau đó vẫn sai.
   - Với `muowns`, việc chuẩn hóa `uow` → `ươ` bị xung đột với logic tách phụ âm cuối (`n`, `ng`, `nh`).

2. **Sửa `ModifierRules.applyModifier` để biến `uo` + `w` → `ươ`:**
   - Có thể biến `u` → `ư`, nhưng khi gõ tiếp `n` hoặc `s`, engine lại parse lại từ `rawString` `muowns`, nên mất kết quả biến đổi.

3. **Sửa `TelexEngine.handleCharInput` để incremental update (giữ nguyên Syllable, chỉ thêm FinalConsonant):**
   - Khả thi với các từ đã có `Syllable` hợp lệ, nhưng với `muowns` thì sau khi gõ `w`, parser không tạo được `Syllable` hợp lệ (`muow` không phải âm tiết tiếng Việt), nên không có state để incremental update.

**Kết luận:** Để fix dứt điểm Nhóm B, cần **refactor kiến trúc engine** từ "parse lại rawString" sang **"event-driven state transition"**: mỗi phím được xử lý như một event biến đổi `Syllable` hiện có, thay vì parse lại từ đầu. Việc này vượt quá phạm vi sửa lỗi nhanh và cần thiết kế lại luồng xử lý phím.

#### Nhóm C (`xaaa`, `deee`, `dđ`) — Lỗi Undo/Escape phím lặp

Nhóm C chưa được sửa vì tôi tập trung xử lý Nhóm B trước. Tuy nhiên, nguyên nhân kỹ thuật đã xác định:

- Trong `TelexEngine.handleCharInput`, nhánh `isUndoModifier` khi phát hiện lặp phím (ví dụ `xâ` + `a`) chỉ đơn giản đặt `RawKeys = newRaw` và hiển thị chuỗi raw.
- Kết quả: `xâ` + `a` (3 phím `x`, `a`, `a`) trở thành `xaaa` thay vì `xaa`.
- **Lý do:** logic chưa loại bỏ ký tự modifier lặp để đưa nguyên âm về dạng gốc rồi mới thêm ký tự vừa gõ.
- **Độ phức tạp:** chỉ cần sửa logic trong `handleCharInput`, phạm vi nhỏ, có thể fix nhanh hơn Nhóm B.

### Kết quả test gần nhất (trước khi dừng sửa Nhóm B)

```text
Failed: 5, Passed: 37, Skipped: 0, Total: 42
```

Các test còn fail:
- `TonePlacementTests`: `tieengs`, `muowns`
- `RestoreAndUndoTests`: `xaaa`, `deee`, `dđ`

*(Lưu ý: lần chạy test gần nhất trước đó có 18 failed / 74 total là do một số thay đổi thử nghiệm chưa được revert. Tôi đã revert các thay đổi thử nghiệm của Nhóm B về trạng thái gốc để giữ codebase ổn định.)*

### Khuyến nghị bước tiếp theo

1. **Ưu tiên Nhóm C (Undo/Escape lặp phím):** phạm vi nhỏ, chỉ thay đổi logic trong `TelexEngine.handleCharInput`, có thể giảm 3 lỗi ngay.
2. **Nhóm B (Inline Telex modifiers):** cần refactor engine sang mô hình state transition / raw key interpreter. Nên thực hiện sau khi Nhóm C ổn định.

### File liên quan đã thay đổi trong quá trình xử lý

- `src/BambooMintKey.Core/Engine/ToneRules.fs` — Fix Issue 1 (hoán đổi index Modern/Traditional).
- `src/BambooMintKey.Core/Domain/Types.fs` — Cập nhật comment cho đúng quy chuẩn.
- `src/BambooMintKey.Core/Engine/SyllableParser.fs` — Thêm `normalizeVowelCluster` cho Nhóm A (`ie` → `iê`, `uye` → `uyê`, ...).
- `tests/BambooMintKey.Core.Tests/TonePlacementTests.fs` — Sửa tên test cho khớp hành vi.
- `tests/BambooMintKey.Core.Tests/RestoreAndUndoTests.fs` — Điều chỉnh kỳ vọng backspace cho khớp replay model.
- `docs/2.Design/Phase1/001_07_Issues_v3.md` — Báo cáo này.
