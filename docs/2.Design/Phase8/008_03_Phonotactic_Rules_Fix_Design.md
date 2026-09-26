<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 008_03 — Thiết Kế Sửa Đổi Quy Tắc Ngữ Âm & Đặt Dấu Biên Trong Core Engine

**Mã tài liệu:** `008_03_Phonotactic_Rules_Fix_Design`  
**Giai đoạn:** Phase 8 — Tích hợp từ điển & sửa lỗi vặt  
**Thuộc module:** `BambooMintKey.Core` (Dùng chung độc lập cho cả Windows & Linux)  
**Trạng thái:** 📝 Đã hoàn thiện thiết kế chi tiết  
**Tài liệu tham chiếu:** [008_01_InvestigationForDictionary.md](008_01_InvestigationForDictionary.md), `ToneRules.fs`, `ModifierRules.fs`

---

## 1. Mục Tiêu & Phạm Vi Thiết Kế

Tài liệu này đặc tả các cải tiến và sửa lỗi triệt để trong tầng quy tắc ngữ âm học của bộ gõ BambooMintKey, bao gồm:
1. Sửa dứt điểm lỗi thứ tự ưu tiên biến đổi ký tự phụ (Modifier) khi người dùng gõ phím `w` sau các cụm nguyên âm kép, đặc biệt là trường hợp gõ `vuawf` biến thành `vuằ` thay vì `vừa`.
2. Chuẩn hóa quy tắc biến đổi mở rộng cho các cụm nhị trùng âm và tam trùng âm đi kèm phím `w`, đồng thời bảo toàn tính năng lặp phím để hủy biến đổi (Repeat-Key Undo).
3. Chuẩn hóa thuật toán xác định vị trí đặt dấu thanh trên các cụm nguyên âm biên, đảm bảo sự nhất quán tuyệt đối giữa hai trường phái đặt dấu kiểu mới (Modern) và kiểu truyền thống (Traditional).
4. Thực thi toàn bộ các sửa đổi này ở độ phức tạp hằng số $O(1)$ ngay tại tầng quy tắc âm vị, không gây bất kỳ độ trễ nào cho pipeline gõ phím.

---

## 2. Vấn Đề Cần Giải Quyết & Phương Pháp Thực Hiện

### Vấn Đề 1: Lỗi biến đổi sai cụm nguyên âm `ua` khi gặp phím `w` (`vuawf` biến thành `vuằ`)

- **Hiện trạng & Thách thức:**
  Trong thực tế gõ tiếng Việt kiểu Telex, khi người dùng muốn gõ các từ như `vừa`, `mưa`, `chưa`, `cưa`, `dưa`, thói quen phổ biến là gõ phụ âm đầu, tiếp đến hai phím nguyên âm `u`, `a`, sau đó nhấn phím `w` để thêm dấu móc cho nguyên âm, và cuối cùng nhấn phím dấu thanh (ví dụ: `v - u - a - w - f`).
  Tuy nhiên, trong logic hiện tại của module xử lý modifier, khi kiểm tra phím `w`, quy tắc ưu tiên kiểm tra ký tự `a` đứng trước kiểm tra ký tự `u`. Do đó, khi thấy chuỗi chứa ký tự `a`, hệ thống lập tức biến đổi `a` thành `ă` (dấu trăng), biến cụm `ua` thành `uă`. Khi người dùng gõ tiếp phím dấu huyền `f`, hệ thống ghép dấu lên `ă` và tạo ra từ dị dạng `vuằ`. Trong tiếng Việt, cụm `uă` hầu như không tồn tại độc lập ở âm tiết mở, trong khi cụm `ưa` là một âm tiết cực kỳ phổ biến.
- **Đầu vào (Input):**
  - Trạng thái âm tiết hiện tại có phần hạt nhân nguyên âm chứa chuỗi ký tự `ua` (hoặc các biến thể viết hoa `Ua`, `UA`).
  - Ký tự phím vừa được nhấn là phím `w` (hoặc `W`).
- **Đầu ra (Output):**
  - Cấu trúc âm tiết mới với phần hạt nhân nguyên âm được biến đổi chính xác thành `ưa` (hoặc `Ưa`, `ƯA`), bảo toàn phụ âm đầu và các thuộc tính viết hoa/thường.
- **Cách giải quyết:**
  - Thay đổi thứ tự thẩm định quy tắc biến đổi modifier theo nguyên tắc: **Ưu tiên nhận diện cụm nguyên âm dài hơn nguyên âm đơn lẻ**.
  - Trước khi duyệt từng nguyên âm đơn (như kiểm tra riêng lẻ `a` hay riêng lẻ `u`), hệ thống thực hiện kiểm tra xem hạt nhân nguyên âm có chứa cụm ký tự `ua` hay không:
    - Nếu hạt nhân nguyên âm có chứa cụm `ua` và chưa từng chứa ký tự `ưa`: Tiến hành thay thế toàn bộ cụm `ua` thành `ưa`.
    - Thuật toán này bảo toàn tính chất viết hoa/thường: Nếu chữ cái đầu là chữ hoa thì kết quả là `Ưa`, nếu toàn bộ viết hoa thì kết quả là `ƯA`, nếu viết thường thì kết quả là `ưa`.
  - Nhờ việc ưu tiên biến đổi cụm `ua` thành `ưa`, khi người dùng nhấn tiếp phím dấu thanh `f` (thanh Huyền), quy tắc đặt dấu thanh sẽ đặt dấu trực tiếp lên nguyên âm mang dấu móc `ừ`, tạo ra kết quả hoàn hảo là `vừa`.

---

### Vấn Đề 2: Mở rộng biến đổi cụm nguyên âm và cơ chế hoàn tác lặp phím `w`

- **Hiện trạng & Thách thức:**
  Ngoài cụm `ua`, tiếng Việt còn có các cụm nguyên âm kép có chứa `u` và `o` đi cùng phím `w`. Nếu không quy định chặt chẽ, việc nhấn phím `w` có thể gây xung đột giữa việc thêm dấu móc (horn) và việc hủy dấu (undo).
- **Đầu vào (Input):**
  - Trạng thái âm tiết chứa các cụm nguyên âm: `uo`, `uô`, `ưo`, `uơ`, hoặc nguyên âm đơn `u`, `o`.
  - Ký tự vừa nhấn là phím `w`.
  - Cờ cấu hình cho phép lặp phím để hủy dấu (AllowRepeatKeyUndo).
- **Đầu ra (Output):**
  - Âm tiết với cụm nguyên âm được móc đôi đồng bộ (ví dụ: `uo` biến thành `ươ`), hoặc trạng thái khôi phục nguyên vẹn chuỗi phím thô ban đầu nếu phát hiện hành động cố tình lặp phím `w`.
- **Cách giải quyết:**
  - Thiết lập danh mục ưu tiên biến đổi theo thứ tự từ phức tạp đến đơn giản khi nhận phím `w`:
    1. **Biến đổi cặp đôi `uo` thành `ươ`:** Nếu hạt nhân nguyên âm chứa bất kỳ dạng nào của cặp đôi này (`uo`, `uô`, `ưo`, `uơ`), phím `w` sẽ chuyển đổi đồng bộ cả hai nguyên âm thành `ươ` (ví dụ: gõ `duowng` biến thành `đương`).
    2. **Biến đổi cụm `ua` thành `ưa`:** Thực hiện như đã mô tả ở Vấn đề 1.
    3. **Biến đổi nguyên âm đơn `o` thành `ơ`:** Áp dụng khi âm tiết chỉ có nguyên âm `o` đơn độc và chưa mang dấu mũ hay móc.
    4. **Biến đổi nguyên âm đơn `u` thành `ư`:** Áp dụng khi âm tiết chỉ có nguyên âm `u` đơn độc và chưa mang dấu móc.
  - **Quy tắc hoàn tác lặp phím `w` (Undo):**
    - Nếu âm tiết hiện tại đã ở trạng thái mang nguyên âm có móc (chẳng hạn đã có `ư` hoặc `ơ`) và người dùng tiếp tục nhấn thêm một phím `w` nữa ngay liền kề:
    - Hệ thống phát hiện đây là hành vi lặp phím có chủ đích để lấy lại ký tự `w` gốc (ví dụ gõ từ tiếng Anh như `draw`, `show`, `view`).
    - Hệ thống hủy bỏ toàn bộ biến đổi modifier, hoàn trả âm tiết về chuỗi ký tự thô nguyên bản kèm theo ký tự `w` vừa gõ.

---

### Vấn Đề 3: Chuẩn hóa thuật toán xác định vị trí đặt dấu thanh trên các cụm nguyên âm biên

- **Hiện trạng & Thách thức:**
  Vị trí đặt dấu thanh trong chữ Quốc ngữ có sự phân hóa giữa hai trường phái: Kiểu mới (Modern - dấu đặt ở nguyên âm âm chính theo chuẩn ngữ âm hiện đại) và Kiểu cũ (Traditional - dấu đặt thiên về nguyên âm phía sau theo mỹ tự học truyền thống). Sự nhầm lẫn vị trí đặt dấu thường xảy ra ở các cụm nguyên âm mở như `oa`, `oe`, `uy` hoặc các cụm chứa nguyên âm mang dấu móc như `ưa`, `ươ`.
- **Đầu vào (Input):**
  - Chuỗi ký tự hạt nhân nguyên âm đã được loại bỏ dấu thanh cũ (ví dụ: `oa`, `oe`, `uy`, `ưa`, `ươ`, `uê`, `iê`).
  - Sự tồn tại của phụ âm cuối (cờ logic biểu thị âm tiết đóng hay âm tiết mở).
  - Kiểu đặt dấu cấu hình trong hệ thống: Hiện đại (Modern) hay Cổ điển (Traditional).
- **Đầu ra (Output):**
  - Chỉ số nguyên âm (vị trí ký tự bắt đầu từ 0) bên trong chuỗi hạt nhân nguyên âm sẽ được áp dấu thanh lên đó.
- **Cách giải quyết:**
  - Thuật toán xác định vị trí đặt dấu được thực hiện theo cấu trúc phân cấp nghiêm ngặt như sau:
    1. **Trường hợp hạt nhân chỉ có 1 nguyên âm đơn:** Vị trí dấu thanh luôn là chỉ số 0.
    2. **Trường hợp hạt nhân có 2 nguyên âm:**
       - **Nếu âm tiết có phụ âm cuối (âm tiết khép):** Dấu thanh bắt buộc luôn đặt ở nguyên âm thứ hai (chỉ số 1). Ví dụ: `hoàn` (dấu trên `a`), `tiến` (dấu trên `ê`), `thương` (dấu trên `ơ`).
       - **Nếu âm tiết không có phụ âm cuối (âm tiết mở):**
         - Xét cụm đặc thù `oa`, `oe`, `uy`: Nếu cấu hình là Kiểu mới (Modern), dấu thanh đặt ở nguyên âm thứ nhất (chỉ số 0, ví dụ: `hóa`, `xòe`, `thúy`). Nếu cấu hình là Kiểu cổ điển (Traditional), dấu thanh đặt ở nguyên âm thứ hai (chỉ số 1, ví dụ: `hoá`, `xoè`, `thuý`).
         - Xét các cụm có nguyên âm mang dấu phụ (ê, ơ, ư, â, ă): Nguyên âm nào mang dấu phụ thì nguyên âm đó là âm chính và được nhận dấu thanh. Ví dụ trong cụm `ưa` thì `ư` là âm chính (chỉ số 0), dấu thanh đặt trên `ư` tạo thành `vừa`, `cửa`, `mứa`. Trong cụm `uê` thì `ê` là âm chính (chỉ số 1), dấu thanh đặt trên `ê` tạo thành `thuế`, `huệ`.
    3. **Trường hợp hạt nhân có 3 nguyên âm (tam trùng âm):**
       - Nếu chuỗi nguyên âm chứa cụm `ươ` (như trong `ươu`, `ươi`), dấu thanh luôn được ưu tiên đặt trực tiếp lên nguyên âm `ơ`.
       - Trong các cụm tam trùng âm khác (như `oai`, `uay`, `iêu`, `uyê`), tìm kiếm nguyên âm mang dấu phụ (ê, ơ, â, ă) để đặt dấu. Nếu không có nguyên âm nào mang dấu phụ, nguyên âm đứng ở giữa (chỉ số 1) sẽ nhận dấu thanh.
  - **Ràng buộc tương thích âm tắc cuối:**
    - Sau khi tính được vị trí nguyên âm, hệ thống kiểm tra phụ âm cuối của âm tiết.
    - Nếu phụ âm cuối là một trong các âm tắc vô thanh (c, ch, p, t), hệ thống kiểm tra tính hợp lệ của thanh điệu: Chỉ chấp nhận thanh Sắc hoặc thanh Nặng. Nếu người dùng nhập thanh Huyền, Hỏi, Ngã, hệ thống từ chối áp dấu thanh lên âm tiết để bảo vệ tính đúng đắn ngữ âm học tiếng Việt.
