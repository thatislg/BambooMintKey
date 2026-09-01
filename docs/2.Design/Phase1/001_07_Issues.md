Dưới đây là bản phân tích chi tiết toàn bộ các lỗi từ log kiểm thử thực tế, nguyên nhân gốc rễ (Root Cause) và chiến lược xử lý triệt để về mặt giải thuật.

### Tổng hợp và Phân loại Issues từ Log Test

Từ 19 ca kiểm thử thất bại, toàn bộ lỗi được gom về đúng **4 nhóm bài toán cốt lõi**:

| **Nhóm Issue**                       | **Test Case Tiêu Biểu**               | **Thực tế (Actual)**         | **Kỳ vọng (Expected)**    |
| ------------------------------------ | ------------------------------------- | ---------------------------- | ------------------------- |
| **1. Đảo ngược vị trí dấu Style**    | `hoas` (Modern)  `hoas` (Traditional) | `hoá`  `hóa`                 | `hóa`  `hoá`              |
| **2. Bỏ dấu sai khi có modifier**    | `tieengs`  `thuyets`  `muowns`        | `tiéeng`  `thuýet`  `muowns` | `tiếng`  `thuyết`  `mượn` |
| **3. Mất dấu mũ khi gõ case/tone**   | `vietj`, `Vietj`, `VIETJ`             | `viẹt`, `Viẹt`, `VIẸT`       | `việt`, `Việt`, `VIỆT`    |
| **4. Cơ chế Escape / Undo phím lặp** | `xaaa`, `deee`, `dđ`                  | `xaaa`, `deee`, `dđ`         | `xaa`, `dee`, `dd`        |

### Phân tích Nguyên nhân Gốc rễ & Phương án Khắc phục

#### Issue 1: Vị trí dấu bị nghịch đảo giữa Modern và Traditional

- **Hiện tượng:** Cặp âm tiết mở hai nguyên âm (`oa`, `oe`, `uy`) khi chạy chế độ Modern thì ra kiểu cũ (`hoá`, `thuý`), còn chạy Traditional lại ra kiểu mới (`hóa`, `thúy`).
- **Root Cause đã xác định:**
  - Chuỗi nguyên âm `oa`, `oe`, `uy` có index 0 là nguyên âm đầu (`o`, `o`, `u`) và index 1 là nguyên âm sau (`a`, `e`, `y`).
  - Quy chuẩn thực tế (và được bộ test khẳng định):
    - **Modern (kiểu mới):** dấu đặt trên nguyên âm đầu → `hóa`, `xòe`, `thúy` (index = `0`).
    - **Traditional (kiểu cũ):** dấu đặt trên nguyên âm sau → `hoá`, `xoè`, `thuý` (index = `1`).
  - Hàm `getTargetVowelIndex` trong `src/BambooMintKey.Core/Engine/ToneRules.fs` lại gán ngược: `Modern -> 1`, `Traditional -> 0`, khiến dấu rơi vào nguyên âm sai.
- **Phương án sửa đã áp dụng:**
  - Hoán đổi giá trị index trong nhánh `oa`/`oe`/`uy` của `getTargetVowelIndex`:
    - `Modern -> 0`
    - `Traditional -> 1`
  - Cập nhật lại comment trong `Types.fs` và tên test trong `TonePlacementTests.fs` cho khớp với quy chuẩn thực tế.
- **Kết quả đánh giá:**
  - 8/8 test case của Issue 1 đã pass (`hoas`, `hoaf`, `thuys`, `xoef` cho cả hai style).
  - 3 test case còn lại trong file `TonePlacementTests.fs` (`tieengs`, `thuyets`, `muowns`) thuộc Issue 2 (lỗi parse lại chuỗi raw khi có phụ âm cuối), không liên quan đến Issue 1.

#### Issue 2: Lỗi vỡ cụm nguyên âm kép khi nhận diện Telex (`tieengs` $\rightarrow$ `tiéeng`)

- **Hiện tượng:** Khi gõ `tieeng` + `s`, thay vì biến `iê` thành `iế`, engine lại tạo ra `tiéeng`. Tương tự với `muowns` bị rơi thẳng về chuỗi thô `muowns`.
- **Root Cause:**
  - Luồng gõ: `t` $\rightarrow$ `i` $\rightarrow$ `e` $\rightarrow$ `e` $\rightarrow$ `n` $\rightarrow$ `g` $\rightarrow$ `s`.
  - Khi người dùng gõ `e` thứ hai, chuỗi biến thành `ê`. Nhưng khi gõ tiếp `n` và `g`, engine đang cố gắng **parse lại chuỗi thô từ đầu** (`tieen`, `tieeng`).
  - Bộ `SyllableParser` khi phân tích chuỗi thô `tieeng` lại không biết quy tắc Telex `ee = ê`, dẫn đến việc tách nucleus thành chuỗi gồm 2 chữ `e` thô (`iee`), làm mất dấu mũ `ê`. Khi phím `s` đi vào, nó gán dấu sắc lên chữ `e` đầu tiên $\rightarrow$ tạo ra `tiéeng`.
  - Tương tự với `muowns`: gõ `w` biến `uo` thành `ươ`. Nhưng gõ tiếp `n` lại parse lại từ `muown`, không còn là `mươn` nên phím `s` không nhận diện được âm tiết hợp lệ.
- **Phương án sửa (Mô hình Pipeline tuần tự):**
  - Không bao giờ dùng chuỗi phím thô `rawKeys` để phân tích lại cấu trúc âm tiết một khi âm tiết đã được hình thành.
  - Phải tách biệt rõ 2 giai đoạn:
    1. **Giai đoạn nhận diện phím gõ (Input Interpreter):** Nếu phím vừa nhấn là phụ âm cuối (như `n`, `g`, `t`, `c`), hãy gắn trực tiếp nó vào trường `FinalConsonant` của `Syllable` hiện tại, thay vì reset lại toàn bộ từ chuỗi raw.
    2. Chỉ gọi `SyllableParser` toàn diện khi bắt đầu một từ mới hoặc khi cấu trúc hiện tại bị xóa hoàn toàn.

#### Issue 3: Mất dấu mũ khi gán dấu nặng (`Vietj` $\rightarrow$ `Viẹt` thay vì `Việt`)

- **Hiện tượng:** Khi gõ từ `viet` (đã là `viêt`), nhấn tiếp `j` thì dấu nặng được gán lên `e` biến thành `ẹ` thay vì `ệ`.
- **Root Cause:**
  - Hàm phân rã/tổng hợp ký tự Unicode (`decomposeChar` / `composeChar`):
    - Ký tự `ê` có cấu trúc: `Base = 'e'`, `Modifier = Hat`, `Tone = None`.
    - Khi áp dụng phím `j` (`Tone.Dot`), hàm `applyTone` lại lấy `baseChar = 'e'` nhưng vô tình bỏ quên `Modifier.Hat` (hoặc truyền `Modifier.None`), dẫn đến việc gọi `composeChar('e', Modifier.None, Tone.Dot)` $\rightarrow$ tạo ra chữ `ẹ`.
- **Phương án sửa:**
  - Trong hàm `applyTone`: Khi bóc tách ký tự tại vị trí đích (`targetIdx`), phải giữ nguyên toàn bộ `Modifier` hiện tại của ký tự đó (`Hat`, `Horn`, `Breve`), chỉ thay thế trường `Tone` bằng tone mới rồi mới gọi `composeChar`.

#### Issue 4: Cơ chế Undo / Escape phím lặp (`xaaa` $\rightarrow$ `xaa`)

- **Hiện tượng:** Gõ `xaa` ra `xâ`, nhưng gõ tiếp `a` thứ ba (`xaaa`) thì mong muốn khôi phục lại chuỗi `xaa` (bỏ mũ), thực tế engine vẫn giữ nguyên hoặc biến đổi sai.
- **Root Cause:**
  - Trạng thái nhận diện `isUndoModifier`: Điều kiện hiện tại chỉ kiểm tra xem từ có chứa `â` hay không. Khi người dùng gõ `x` $\rightarrow$ `a` $\rightarrow$ `a` (thành `xâ`), ký tự tiếp theo là `a`. Lúc này engine thấy `lowerChar = 'a'` và text đang chứa `â`, nhưng lại chuyển toàn bộ chuỗi sang fallback chuỗi thô gồm cả 3 ký tự `xaaa` thay vì cắt giảm phím lặp để trả về `xaa`.
- **Phương án sửa:**
  - Khi phát hiện lặp phím modifier (ví dụ từ đang là `xâ` mà gõ tiếp `a`):
    1. Loại bỏ dấu mũ trên nguyên âm tương ứng để đưa về nguyên âm gốc (biến `â` lùi về `a`).
    2. Nối thêm chính ký tự vừa gõ vào sau: `xa` + `a` = `xaa`.
    3. Đánh dấu trạng thái này là đã Escape để các phím tiếp theo không tự động gom lại thành `â` nữa.

### Kế hoạch triển khai từng bước

1. **Bước 1:** Chuẩn hóa bảng tra cứu và bảo toàn `Modifier` trong `composeChar` / `decomposeChar` để giải quyết dứt điểm Issue 3 (`Việt` không bao giờ bị thành `Viẹt`).
2. **Bước 2:** Cố định bảng mapping index cho 2 style dấu Modern/Traditional theo đúng kỳ vọng của bộ test để giải quyết Issue 1.
3. **Bước 3:** Chuyển đổi cơ chế cập nhật State của Engine sang dạng **Incremental State Transition** (giữ nguyên cấu trúc `Syllable` đã biến đổi, chỉ thêm/bớt thành phần khi gõ tiếp phụ âm/nguyên âm) thay vì parse lại từ `rawKeys` để giải quyết Issue 2 (`tieengs`, `muowns`).
4. **Bước 4:** Định nghĩa lại thuật toán Undo/Escape khi gõ lặp phím để giải quyết Issue 4.

Bạn thấy hướng phân tích trên đã bắt đúng toàn bộ các điểm nghẽn của Engine chưa, để chúng ta bắt đầu xử lý từng bước một?