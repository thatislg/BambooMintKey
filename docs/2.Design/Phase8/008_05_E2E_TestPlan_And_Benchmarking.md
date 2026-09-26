<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 008_05 — Kế Hoạch Kiểm Thử Toàn Diện, Đo Kiểm Hiệu Năng & Tiêu Chí Nghiệm Thu

**Mã tài liệu:** `008_05_E2E_TestPlan_And_Benchmarking`  
**Giai đoạn:** Phase 8 — Tích hợp từ điển & sửa lỗi vặt  
**Thuộc module:** `BambooMintKey.Core` (Áp dụng độc lập cho cả Windows & Linux)  
**Trạng thái:** 📝 Đã hoàn thiện thiết kế chi tiết (Đã mở rộng ma trận kiểm thử toàn diện)  
**Tài liệu tham chiếu:** [008_01_InvestigationForDictionary.md](008_01_InvestigationForDictionary.md), [008_04_OnTheFly_PredictEngine_Design.md](008_04_OnTheFly_PredictEngine_Design.md), `003_PredictEngine_Progress.md`

---

## 1. Mục Tiêu & Phạm Vi Kiểm Thử

Tài liệu này đặc tả toàn bộ chiến lược kiểm thử chất lượng, ma trận ca kiểm thử chuyên sâu và phương pháp đo kiểm hiệu năng vi mô cho Phase 8:
1. Mở rộng ma trận kiểm thử lên **hơn 100 ca kiểm thử chuyên sâu** phân bổ đều trên 8 phân nhóm ngữ âm học, văn bản song ngữ và kỹ thuật hệ điều hành, đảm bảo không bỏ sót bất kỳ trường hợp biên nào.
2. Thẩm định tính ổn định tuyệt đối của bộ gõ trên cả hai nền tảng Windows (TSF) và Linux (Fcitx5) trên nhiều ứng dụng thực tế (trình duyệt, văn phòng, IDE lập trình, terminal).
3. Đo lường định lượng các chỉ số hiệu năng: Độ trễ xử lý từng phím bấm, thời gian khởi động, dung lượng RAM chiếm dụng, và xác nhận không có rò rỉ bộ nhớ sau thời gian dài gõ liên tục.
4. Đảm bảo tỷ lệ kiểm thử hồi quy đạt 100% đối với toàn bộ các tính năng đã phát triển từ Phase 1 đến Phase 7.

---

## 2. Vấn Đề Cần Giải Quyết & Phương Pháp Thực Hiện

### Vấn Đề 1: Kiểm thử hồi quy toàn diện các tính năng đã phát triển (Regression Testing)

- **Hiện trạng & Thách thức:**
  Bộ gõ đã tích hợp nhiều tính năng phức tạp từ Phase 1 đến Phase 7: Hoàn tác lặp phím dấu (Repeat-Key Undo), cơ chế bỏ dấu tự do (Free Tone Placement), tự động viết hoa chữ cái sau phím Đ, đồng bộ chế độ gõ V/E qua bộ nhớ chia sẻ hoặc D-Bus. Việc đưa thêm bộ thẩm định từ điển vào luồng xử lý chính của `TelexEngine` có nguy cơ làm xung đột hoặc vô hiệu hóa các quy tắc cũ nếu thứ tự ưu tiên không được kiểm soát chặt chẽ.
- **Đầu vào (Input):**
  - Toàn bộ bộ kiểm thử tự động của các Phase trước (từ Phase 1 đến Phase 7).
  - Tập hợp kịch bản gõ phím mẫu bao gồm cả văn bản tiếng Việt thuần túy, văn bản tiếng Anh, và văn bản kỹ thuật.
- **Đầu ra (Output):**
  - Báo cáo kết quả kiểm thử tự động đạt tỷ lệ thành công 100% (Zero Regressions).
  - Không có bất kỳ ca kiểm thử cũ nào bị thất bại sau khi tích hợp module từ điển mới.
- **Cách giải quyết:**
  - Thiết lập kịch bản chạy hồi quy tự động kiểm tra theo từng tầng:
    1. Tầng thuần ngữ âm: Đảm bảo các bảng mã Unicode NFC, bảng phân tích âm tiết `SyllableParser` vẫn hoạt động chính xác.
    2. Tầng quy tắc lặp phím: Kiểm tra việc lặp lại phím dấu (như gõ thêm `s` sau `má`, gõ thêm `f` sau `dà`) vẫn thực hiện chức năng hoàn tác về chữ cái tiếng Anh gốc như thiết kế ban đầu.
    3. Tầng bỏ dấu tự do: Kiểm tra việc gõ phím dấu ở cuối từ (như `hoacs` ra `hoác`, `phari` ra `phải`) vẫn hoạt động trơn tru kết hợp cùng bộ thẩm định âm tiết mới.
  - Mọi trường hợp sai lệch so với hành vi chuẩn đều phải được điều chỉnh trong mã nguồn Core trước khi chuyển sang các bước tiếp theo.

---

### Vấn Đề 2: Thiết lập Ma trận kiểm thử chuyên sâu toàn diện (8 Phân nhóm kiểm thử)

- **Hiện trạng & Thách thức:**
  Bộ gõ tiếng Việt không chỉ xử lý các từ đơn giản mà còn phải đối mặt với hàng trăm biến thể ngữ âm phức tạp, thói quen gõ tắt, văn bản song ngữ Anh - Việt, mã nguồn lập trình chứa tên biến CamelCase, và sự khác biệt về hiển thị giữa các hệ điều hành. Số lượng 15 ca kiểm thử trước đây chỉ mang tính chất minh họa ban đầu (Sanity Check). Để đạt tiêu chuẩn phát hành chuyên nghiệp, cần một hệ thống kiểm thử toàn diện bao quát mọi tình huống thực tế.
- **Đầu vào (Input):**
  - Hệ thống các chuỗi thao tác gõ phím liên tiếp đại diện cho toàn bộ các tình huống sử dụng trong đời sống và kỹ thuật.
- **Đầu ra (Output):**
  - Trạng thái chữ hiển thị tức thời tại từng phím bấm và kết quả chốt từ cuối cùng trùng khớp 100% với kỳ vọng ngữ âm học và trải nghiệm người dùng.
- **Cách giải quyết:**
  - Phân chia toàn bộ không gian kiểm thử thành 8 nhóm chuyên biệt với hơn 100 ca kiểm thử cụ thể:

#### Nhóm 1: Kiểm thử Biến đổi Modifier & Cụm Nguyên Âm Kép (20 ca kiểm thử)
*Mục tiêu:* Thẩm định tính chính xác của việc biến đổi dấu mũ, móc, trăng và các cụm nguyên âm biên.
- Ca kiểm thử sửa lỗi cụm `ua` đi với `w`: Gõ `v - u - a - w - f` phải ra `vừa` (tại phím `w` ra `vưa`, không bao giờ ra `vuằ`).
- Ca kiểm thử tương tự với các phụ âm đầu khác: `m - u - a - w` ra `mưa`, `c - h - u - a - w` ra `chưa`, `c - u - a - w - j` ra `cựa`, `d - u - a - w - x` ra `dữa`, `t - h - u - a - w` ra `thưa`.
- Ca kiểm thử cụm `uo` đi với `w`: `d - u - o - w - n - g` ra `đương`, `t - h - u - o - w - n - g` ra `thương`, `n - u - o - w - c - s` ra `nước`, `b - u - o - w - c - s` ra `bước`.
- Ca kiểm thử cụm `ia/ie` đi với modifier: `k - h - i - a - s` ra `khía`, `t - i - e - e - n - g - s` ra `tiếng`, `b - i - e - e - t - s` ra `biết`.
- Ca kiểm thử cụm `uye/uyê`: `c - h - u - y - e - e - n - j` ra `chuyện`, `k - h - u - y - e - e - s - n` ra `khuyến`, `t - h - u - y - e - e - n - f` ra `thuyền`.
- Ca kiểm thử tam trùng âm `ươu`, `iêu`, `oai`, `oay`: `r - u - o - w - u - j` ra `rượu`, `c - h - i - e - e - u - r` ra `chiều`, `n - g - o - a - i - f` ra `ngoài`, `x - o - a - y - s` ra `xoáy`.
- Ca kiểm thử hoàn tác lặp phím `w`: `d - r - a - w - w` ra `draw`, `v - i - e - w - w` ra `view`, `s - h - o - w - w` ra `show`.

#### Nhóm 2: Kiểm thử Vị trí Đặt Dấu Thanh & Ràng Buộc Âm Tắc (16 ca kiểm thử)
*Mục tiêu:* Thẩm định vị trí đặt dấu theo kiểu Mới (Modern) và Cổ điển (Traditional), cùng các ràng buộc âm tắc cuối.
- Cụm mở `oa, oe, uy` không có âm cuối theo kiểu Mới: `h - o - a - s` ra `hóa`, `x - o - e - f` ra `xòe`, `t - h - u - y - s` ra `thúy` (dấu ở nguyên âm thứ nhất).
- Cụm mở `oa, oe, uy` theo kiểu Cổ điển: `h - o - a - s` ra `hoá`, `x - o - e - f` ra `xoè`, `t - h - u - y - s` ra `thuý` (dấu ở nguyên âm thứ hai).
- Cụm khép có phụ âm cuối: `t - o - a - n - s` luôn ra `toán`, `h - o - a - n - g - f` ra `hoàng`, `q - u - y - e - e - t - s` ra `quyết` (bất kể kiểu dấu, luôn đặt ở nguyên âm thứ hai).
- Cụm có nguyên âm mang dấu phụ: `v - u - a - w - f` ra `vừa` (dấu trên `ư`), `t - h - u - e - e - s` ra `thuế` (dấu trên `ê`), `n - g - h - e - e - j` ra `nghệ`.
- Ràng buộc âm tắc cuối (`c, ch, p, t`): `t - h - a - c - s` ra `thác` (hợp lệ), `t - h - a - c - j` ra `thạc` (hợp lệ). Ngược lại, gõ `t - h - a - c - f`, `t - h - a - c - r`, `t - h - a - c - x` hệ thống từ chối áp dấu thanh vì âm tắc không đi với huyền, hỏi, ngã.

#### Nhóm 3: Kiểm thử On-The-Fly English Backtracking theo Đuôi Phụ Âm (24 ca kiểm thử)
*Mục tiêu:* Xác minh việc tự động hoàn tác về chuỗi tiếng Anh thô ngay trên phím gõ mà không bị gián đoạn.
- Đuôi `-st`: `p - o - s` hiển thị `pó`, gõ tiếp `t` lập tức biến thành `post`. Kiểm tra tương tự với: `fast`, `last`, `test`, `cost`, `list`, `best`, `must`, `just`, `dust`, `rest`.
- Đuôi `-rm`: `f - o - r` hiển thị `fỏ`, gõ tiếp `m` lập tức biến thành `form`. Kiểm tra với: `storm`, `warm`, `farm`, `norm`, `term`.
- Đuôi `-rt`: `s - t - a - r` hiển thị `stả`, gõ tiếp `t` lập tức biến thành `start`. Kiểm tra với: `part`, `smart`, `chart`, `short`, `sport`, `port`, `report`.
- Đuôi `-rd`: `w - o - r` hiển thị `wỏ`, gõ tiếp `d` lập tức biến thành `word`. Kiểm tra với: `card`, `hard`, `board`, `record`.
- Đuôi `-ct` và `-ft`: `f - a - c - t` ra `fact`, `e - f - f - e - c - t` ra `effect`, `l - e - f - t` ra `left`, `g - i - f - t` ra `gift`.
- Đuôi `-re` (nguyên âm câm): `c - o - r` hiển thị `cò`, gõ tiếp `e` lập tức biến thành `core`. Kiểm tra với: `more`, `care`, `share`, `fire`, `sure`, `store`, `before`.
- Từ khóa lập trình phổ biến: `class`, `case`, `break`, `switch`, `type`, `mode`, `node`, `code`, `true`, `false`, `null`, `void`, `return`.
- Dạng số nhiều tiếng Anh có đuôi `-s`: `files`, `lines`, `games`, `notes`, `times`, `rules`, `pages`, `types`, `cases`, `users`.

#### Nhóm 4: Kiểm thử Bỏ Dấu Tự Do (Free Tone Placement) kết hợp Thẩm Định (12 ca kiểm thử)
*Mục tiêu:* Thẩm định tính năng gõ phím dấu ở cuối từ hoặc giữa từ kết hợp với việc kiểm tra từ điển.
- Dấu gõ ở cuối từ: `h - o - a - c - s` ra `hoác`, `p - h - a - r - i` ra `phải`, `b - i - e - e - t - s` ra `biết`, `n - g - u - o - w - i - f` ra `người`.
- Dấu gõ trước nguyên âm cuối: `p - h - a - r` ghép tiếp `i` ra `phải`, `c - h - o - a` ghép `s` rồi ghép `t` ra `choát`.
- Lặp phím dấu trong cơ chế tự do để hủy dấu: `h - o - a - c - s - s` ra `hoacs`, `p - h - a - r - r - i` ra `phari`.

#### Nhóm 5: Kiểm thử Bảo Toàn Tính Chất Viết Hoa / Viết Thường (12 ca kiểm thử)
*Mục tiêu:* Đảm bảo kiểu chữ thường, chữ hoa đầu từ, toàn bộ chữ hoa và phong cách đặt tên biến được giữ nguyên vẹn.
- Chữ thường (Lowercase): `vừa`, `người`, `trường`, `post`, `form`.
- Viết hoa chữ đầu (TitleCase): `V - u - a - w - f` ra `Vừa`, `N - g - u - o - w - i - f` ra `Người`, `P - o - s - t` ra `Post`.
- Viết hoa toàn bộ (UPPERCASE): `V - U - A - W - F` ra `VỪA`, `N - G - U - O - W - I - F` ra `NGƯỜI`, `P - O - S - T` ra `POST`.
- Tên biến lập trình (CamelCase & PascalCase): `getPostList` (không bị biến `Post` thành tiếng Việt), `userName`, `parseSyllable`.

#### Nhóm 6: Tương Tác Biên Từ, Dấu Cách & Ký Tự Phân Tách (12 ca kiểm thử)
*Mục tiêu:* Thẩm định hành vi chốt từ khi gặp khoảng trắng hoặc dấu câu.
- Chốt từ bằng phím dấu cách (Space): Sau khi chốt từ, từ đệm được làm rỗng, từ tiếp theo bắt đầu chu kỳ gõ mới độc lập.
- Chốt từ bằng dấu câu: Kiểm tra dấu chấm, dấu phẩy, dấu chấm phẩy, dấu hai chấm, dấu gạch chéo, dấu ngoặc đơn. Từ phía trước được chốt nguyên vẹn, dấu câu được chèn ngay liền kề.
- Xóa lùi từng ký tự (Backspace): Nhấn Backspace khi từ chưa chốt sẽ xóa từng ký tự theo thứ tự ngược lại một cách tự nhiên.

#### Nhóm 7: Kiểm Thử Đa Nền Tảng Trên Các Ứng Dụng Thực Tế (12 ca kiểm thử)
*Mục tiêu:* Xác minh tính tương thích và độc lập của bộ gõ trên môi trường Windows và Linux.
- **Trên Linux (Fcitx5 Addon):**
  - Kiểm tra chức năng gõ trong môi trường Wayland và X11.
  - Kiểm tra tính ổn định trên các trình duyệt: Google Chrome, Mozilla Firefox.
  - Kiểm tra trên trình soạn thảo văn phòng: LibreOffice Writer.
  - Kiểm tra trên môi trường lập trình: Visual Studio Code, Terminal (GNOME Terminal, Kitty, Alacritty).
  - Xác nhận bộ đệm C-ABI hoạt động hoàn hảo, không rò rỉ bộ nhớ qua nhiều phiên gõ.
- **Trên Windows (TSF NativeBridge):**
  - Kiểm tra chức năng gõ trên Microsoft Word, Excel.
  - Kiểm tra trên Microsoft Edge, Google Chrome.
  - Kiểm tra trên Notepad, Notepad++, Visual Studio Code.
  - Xác nhận đường gạch chân soạn thảo hiển thị đúng chuẩn TSF, chuyển đổi focus giữa các ứng dụng không bị mất trạng thái.

#### Nhóm 8: Đo Kiểm Hiệu Năng & Ổn Định Bộ Nhớ (6 ca kiểm thử chuyên sâu)
*Mục tiêu:* Đo đạc các thông số kỹ thuật định lượng và kiểm tra độ bền hệ thống.
- Đo thời gian khởi động: Nạp từ điển nhúng vào `FrozenSet` phải hoàn thành trong thời gian dưới 5 mili-giây.
- Đo độ trễ xử lý từng phím: Thực hiện 100.000 lần gõ phím ngẫu nhiên, đo thời gian xử lý trung bình đạt dưới 0.1ms, phân vị P99 đạt dưới 0.5ms.
- Đo mức tiêu thụ RAM: Toàn bộ cấu trúc từ điển trong bộ nhớ RAM không chiếm dụng quá 3 Megabyte.
- Kiểm tra rò rỉ bộ nhớ dài hạn: Chạy kịch bản gõ văn bản liên tục trong 60 phút, theo dõi đường biểu diễn bộ nhớ phẳng, không tăng tiến tính theo thời gian.
- Kiểm tra tính tương thích C-ABI đa tiến trình: Khởi tạo và giải phóng đồng thời 50 phiên gõ độc lập, xác nhận bộ đếm phiên gõ cân bằng tuyệt đối.

---

### Vấn Đề 3: Phương pháp Đo lường Hiệu năng & Quy trình Nghiệm thu

- **Phương pháp đo đạc:**
  - Sử dụng đồng hồ đo thời gian vi mô có độ phân giải nanosecond tích hợp trong môi trường thực thi để ghi nhận chính xác thời gian bắt đầu và kết thúc của từng thao tác xử lý phím.
  - Sử dụng bộ giám sát hiệu năng bộ nhớ để theo dõi số lượng thế hệ thu gom rác (GC Generation 0, 1, 2) phát sinh trong suốt quá trình gõ phím.
- **Quy trình nghiệm thu (Definition of Done):**
  Phase 8 chỉ được ký duyệt hoàn thành khi và chỉ khi:
  1. Toàn bộ **100+ ca kiểm thử** phân bổ trong 8 nhóm trên đều đạt kết quả PASS 100%.
  2. Không có bất kỳ lỗi hồi quy nào đối với các tính năng từ Phase 1 đến Phase 7.
  3. Mọi chỉ số hiệu năng (độ trễ vi mô, bộ nhớ tĩnh, mức độ ổn định đa nền tảng) đều thỏa mãn các ngưỡng định lượng đã đề ra.
