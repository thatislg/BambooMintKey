Bộ test mở rộng (119 tests) đã phát hiện thêm các trường hợp biên của toàn bộ hệ thống. 35 lỗi thất bại này được gom chính xác vào **6 nhóm lỗi kỹ thuật cốt lõi**.

### Bảng tổng hợp phân loại 35 lỗi kiểm thử

| **Nhóm**                                      | **Số lượng** | **Test Case Tiêu Biểu**                                      | **Giá trị thực tế (Actual)**                                 | **Kỳ vọng (Expected)**                                       | **Vị trí nghi vấn**                  |
| --------------------------------------------- | ------------ | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------ |
| **1. Cụm Modifier `ươ` (`w`, `uw`, `ow`)**    | 8 tests      | `uwow`, `uow`, `huowng`, `tuwowr`, `muowns`, `ruowuj`, `muwas`, `nguwaf` | `uwow`, `uơ`, `huowng`, `tuwowr`, `muowns`, `ruowuj`, `muwas`, `nguwaf` | `ươ`, `ươ`, `hương`, `tưở`, `mượn`, `rượu`, `mứa`, `ngừa`    | `ModifierRules.fs`                   |
| **2. Disabled Engine Pass-Through**           | 3 tests      | `hoas`, `vietj`, `truwowngf` (khi `IsEnabled = false`)       | `""` (Rỗng)                                                  | `hoas`, `vietj`, `truwowngf`                                 | `TelexEngine.fs`                     |
| **3. Casing & Vowel Cluster (`ee` ->  `ê`)**  | 9 tests      | `vieetj`, `VIEETJ`, `vIeeTj`, `bieens`, `tieengs`, `buoonf`, `giets`, `quocs`, `Backspace` | `vieẹt`, `VIEẸT`, `vIeẹT`, `bieén`, `tieéng`, `buoòn`, `giét`, `quóc`, `vieet` | `việt`, `VIỆT`, `vIệT`, `biến`, `tiếng`, `buồn`, `giết`, `quốc`, `viết` | `ModifierRules.fs` / `WordBuffer.fs` |
| **4. Triphthongs / Tam nguyên âm**            | 5 tests      | `khuays`, `muoix`, `tieur`, `yeus`, `thuyesft`               | `khuáy`, `muõi`, `tiẻu`, `yéu`, `thuyesft`                   | `khuấy`, `muỗi`, `tiểu`, `yếu`, `thuyết`                     | `ToneRules.fs`                       |
| **5. Repeat Key Undo / Restore**              | 4 tests      | `luxx`, `mass`, `dajj`, `toff`                               | `lux`, `mas`, `daj`, `tof`                                   | `luxx`, `mass`, `dajj`, `toff`                               | `TelexEngine.fs`                     |
| **6. Parser Contracts & Syllable Validation** | 3 tests      | `k`, `thuyet`, `nghieng`                                     | `k` (chấp nhận), `uyê`, `iê`                                 | `k` (phải reject `None`), `uye`, `ie`                        | `SyllableParser.fs`                  |

### Chi tiết kỹ thuật từng nhóm lỗi

**Nhóm 1: Biến đổi phức hợp `ươ`, `ưa` và phím `w`**

- **Hiện tượng:** Gõ các biến thể như `uwow`, `uow`, `huowng`, `tuwowr` đều bị văng về chuỗi thô không dấu.
- **Nguyên nhân:**
  - Bộ quy tắc `ModifierRules.fs` chưa bao quát hết các cách gõ `ươ` trong tiếng Việt:
    - `u` + `w` ->  `ư`
    - `ư` + `o` + `w` ->  `ươ`
    - `u` + `o` + `w` ->  `ươ`
    - `u` + `w` + `o` + `w` ->  `ươ`
    - `w` sau `a` ->  `ă` (`muwas` ->  `mứa` do `w` biến `u` thành `ư` trước khi gán sắc).

**Nhóm 2: Xử lý EngineAction khi tắt bộ gõ (`IsEnabled = false`)**

- **Hiện tượng:** Khi tắt Engine, test mong muốn text được đưa ra dưới dạng pass-through nguyên vẹn, nhưng nhận được chuỗi rỗng `""`.
- **Nguyên nhân:**
  - `processKey` khi gặp `config.IsEnabled = false` đang trả về `(WordState.Empty, EngineAction.PassThrough)`.
  - Bộ test Runner của bạn đang đọc chuỗi hiển thị từ action `UpdateComposition` hoặc `state.TransformedText`. Khi engine nhả `PassThrough`, buffer bị xóa trắng thay vì giữ nguyên ký tự thô vừa nhấn.

**Nhóm 3: Nhận diện `ee` ->  `ê`, `oo` ->  `ô` và bảo toàn Casing**

- **Hiện tượng:** Chuỗi `vieetj` ra `vieẹt` (chữ `e` đầu bị giữ lại, chữ `e` sau thành `ẹ`), `bieens` ra `bieén`.
- **Nguyên nhân:**
  - Khi gõ `e` thứ hai trong `viee`, hàm modifier biến đổi ký tự `e` thứ hai nhưng không rút gọn 2 chữ `ee` thành 1 chữ `ê`, làm nucleus trở thành `"ieê"` hoặc `"iee"` ->  gán tone vào vị trí sai.
  - Tương tự với `giets` và `quocs`: phụ âm `gi` và `qu` cần phân tách rõ phụ âm đầu và nguyên âm đệm để không biến `i` trong `gi` hay `u` trong `qu` thành tâm điểm dấu.

**Nhóm 4: Đặt dấu thanh trên tam nguyên âm (Triphthongs)**

- **Hiện tượng:** `khuays` ra `khuáy` (dấu trên `a` thay vì `â`/`y`), `tieur` ra `tiẻu` (dấu trên `e` thay vì `ê`), `yeus` ra `yéu` (dấu trên `e` thay vì `ê`).
- **Nguyên nhân:**
  - Hàm `getTargetVowelIndex` đối với các cụm 3 nguyên âm (`uay`, `ieu`, `yeu`, `uoi`, `oai`) cần ưu tiên đặt dấu lên **nguyên âm có dấu mũ/móc** (`ê`, `ơ`, `â`) hoặc nguyên âm ở chính giữa nếu không có mũ.

**Nhóm 5: Khôi phục chuỗi khi Undo phím lặp dấu (`luxx` ->  `luxx`)**

- **Hiện tượng:** Gõ `lux` (đã có dấu ngã) rồi gõ tiếp `x` thì test mong muốn khôi phục chuỗi thô `luxx` (4 ký tự), nhưng code lại cắt mất chữ `x` vừa gõ thành `lux` (3 ký tự).
- **Nguyên nhân:**
  - Logic Undo dấu thanh (`isUndoTone`) đang gỡ bỏ dấu thanh và nhả lại chuỗi không dấu nhưng lại vô tình nuốt mất phím `x` cuối cùng thay vì append đủ `c` vào chuỗi raw.

**Nhóm 6: Hợp đồng đầu ra của SyllableParser (Parser Contract)**

- **Hiện tượng:**
  - `k` đơn lẻ bị parse thành công (`Some`) trong khi test yêu cầu phải reject (`None`) vì từ chưa có nguyên âm hợp lệ.
  - `thuyet` và `nghieng`: Test parser yêu cầu trường `VowelNucleus` phải trả về chuỗi nguyên âm thuần túy trước biến đổi (`"uye"`, `"ie"`), trong khi parser lại trả về dạng dựng sẵn (`"uyê"`, `"iê"`).