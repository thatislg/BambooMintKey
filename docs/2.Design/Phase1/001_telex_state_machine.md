Dưới đây là khung dàn ý (**Outline**) cho tài liệu **`001_telex_state_machine.md`**, tập trung toàn bộ vào việc thiết kế lõi xử lý F# cho kiểu gõ Telex.

# [Outline] 001_telex_state_machine.md: Thiết kế Lõi F# Telex State Machine

### 1. Mục tiêu và Phạm vi (Goals & Non-Goals)

- **Mục tiêu:**
  - Xây dựng một Engine biến đổi ký tự thuần túy (Pure Functional State Machine) bằng F#.
  - Đạt tính độc lập tuyệt đối (Zero OS Dependencies), không tham chiếu Windows API hay TSF Interface để chạy nhanh và dễ viết Unit Test.
  - Xử lý chính xác 100% các biến đổi Telex, vị trí dấu thanh (kiểu mới/cũ), hoàn tác (Undo/Escape), và phục hồi từ tiếng Anh.
- **Phi mục tiêu (Non-Goals):**
  - Không xử lý I/O bàn phím thực tế (do C# NativeBridge đảm nhiệm).  
  - Không xử lý lưu trữ cấu hình file/registry (do UI/IPC đảm nhiệm).  

### 2. Mô hình Domain Types trong F# (`Domain/Types.fs`)

Đặc tả toàn bộ cấu trúc dữ liệu bằng Discriminated Unions và Records:

- **Key Input Representation:**
  - Phân loại phím nhấn: Ký tự chữ cái (`a-z`, `A-Z`), phím xóa (`Backspace`), phím ngắt từ (`Space`, `Enter`, `Punctuation`), hoặc phím không hợp lệ.  
- **Grammar Components:**
  - `Tone`: `None | Acute (sắc) | Grave (huyền) | Hook (hỏi) | Tilde (ngã) | Dot (nặng)`.  
  - `Modifier`: `None | Hat (â, ê, ô) | Horn (ơ, ư) | Breve (ă) | DBar (đ)`.
  - `Case`: `Lower | Upper | TitleCase` (Bảo toàn trạng thái viết hoa/thường khi biến đổi).
- **Word State Model:**
  - Cấu trúc `WordState` gồm 4 thành phần ngữ pháp: `InitialConsonant`, `VowelNucleus`, `FinalConsonant`, `Tone`.  
  - Bộ đệm chuỗi thô (`RawKeys: char list`) và ngăn xếp lịch sử (`History: WordState list`) để phục vụ hoàn tác.  
- **Engine Output Actions:**
  - `UpdateText of string`: Yêu cầu TSF cập nhật từ mới vào composition.  
  - `CommitText of string`: Yêu cầu chốt từ khi gặp ngắt từ.  
  - `PassThrough`: Nhả phím không xử lý (cho phép app xử lý tự nhiên).  

### 3. Bảng tra cứu tĩnh & Phân loại ký tự (`Domain/UnicodeTables.fs`)

- **Phân loại tập ký tự:**
  - Tập nguyên âm đơn và nguyên âm ghép (`oa, oe, uy, ie, uo,...`).  
  - Tập phụ âm đầu hợp lệ (`b, c, d, đ, g, gh, h, k, kh, l, m, n, ng, ngh, nh, p, ph, q, r, s, t, th, tr, v, x`).
  - Tập phụ âm cuối hợp lệ (`c, ch, m, n, ng, nh, p, t`).
- **Bảng ánh xạ biến đổi Unicode (NFC Direct Mapping):**
  - Ánh xạ nguyên âm gốc + Modifier $\rightarrow$ Ký tự Unicode có mũ/móc (`a` + `Hat` $\rightarrow$ `â`).  
  - Ánh xạ nguyên âm + Tone $\rightarrow$ Ký tự Unicode có dấu thanh (`a` + `Acute` $\rightarrow$ `á`).  

### 4. Thuật toán phân tích ngữ pháp & Đặt dấu thanh động (`Engine/`)

- **Bộ tách âm tiết (Syllable Parser):**
  - Tách chuỗi phím hiện tại thành: Phụ âm đầu + Cụm nguyên âm + Phụ âm cuối.
- **Quy tắc xác định nguyên âm chính mang dấu thanh (`ToneRules.fs`):**
  - *Trường hợp có phụ âm cuối:* Dấu luôn đặt trên nguyên âm chính đi liền trước phụ âm cuối (ví dụ: `hoàn`, `thuyết`, `mượn`).  
  - *Trường hợp không có phụ âm cuối:*
    - Chuẩn mới (Modern style): Đặt trên nguyên âm thứ hai trong cụm `oa, oe, uy` (ví dụ: `hòa`, `hóa`, `thúy`).  
    - Chuẩn cũ (Traditional style): Đặt trên nguyên âm đầu (ví dụ: `hoà`, `hoá`, `thuý`).  
- **Quy tắc phím biến đổi Telex (`ModifierRules.fs`):**
  - `aa -> â`, `aw -> ă`, `ee -> ê`, `oo -> ô`, `ow -> ơ`, `uw -> ư`, `dd -> đ`.  
  - Xử lý phím tắt nhanh: `w` đứng đầu từ $\rightarrow$ `w` (hoặc `ư` tùy cấu hình), `w` sau nguyên âm $\rightarrow$ biến đổi móc (`u` + `w` $\rightarrow$ `ư`, `o` + `w` $\rightarrow$ `ơ`).

### 5. Cơ chế Chuyển trạng thái, Hoàn tác & Fallback (`Engine/TelexEngine.fs`)

- **Hàm chuyển trạng thái thuần túy (Pure Transition Signature):**
  - Chữ ký hàm: `val processKey : WordState -> KeyInput -> EngineConfig -> (WordState * EngineAction)`
- **Logic Hoàn tác (Undo / Escape):**
  - *Lặp phím dấu/mũ:* Đang có `â`, gõ tiếp `a` $\rightarrow$ khôi phục về `aa`; đang có `má`, gõ tiếp `s` $\rightarrow$ khôi phục về `mas`.  
  - *Phím Backspace:* Pop trạng thái trước đó từ `History` stack.  
- **Cơ chế Fallback cho từ tiếng Anh / Sai âm tiết:**
  - Nhận diện cấu trúc phụ âm sai (ví dụ: `str`, `cl`, `pr`, `code`, `start`).  
  - Tự động trả về toàn bộ chuỗi thô (`RawKeys`) khi vi phạm quy tắc cấu trúc tiếng Việt.  

### 6. Danh mục Test Cases Kiểm thử (`BambooMintKey.Core.Tests`)

Quy hoạch hơn 200 test case chia theo các nhóm kịch bản:

1. **Simple Telex:** Gõ đơn giản phụ âm + nguyên âm + dấu (`as -> á`, `ddo -> đô`).  
2. **Dynamic Tone Placement:** So sánh chuẩn dấu mới vs cũ (`hoa + s -> hóa` vs `hoá`).  
3. **Compound Vowels:** Các từ có 3 nguyên âm (`khuỷu`, `ngoằn`, `ngoèo`).
4. **Key Repeat / Undo:** Gõ lặp phím để khôi phục (`ss`, `aa`, `ww`, `dd`).  
5. **Mixed Typing / English Words:** Gõ từ tiếng Anh không bị dính dấu sai (`code`, `system`, `filter`).  
6. **Case Preservation:** Giữ đúng trạng thái viết hoa (`VIET`, `Viet`, `vIeT`).


----

Hoàn thiện trọn vẹn Phase 1 theo outline, chúng ta sẽ thiết kế tuần tự **5 phần kỹ thuật cốt lõi**:

- **Phần 1: Hệ thống Domain Types (`Domain/Types.fs`)** Định nghĩa toàn bộ các kiểu dữ liệu nền tảng bằng F# (Discriminated Unions & Records): `Tone`, `Modifier`, `KeyInput`, `WordState`, `EngineAction`.  
- **Phần 2: Bảng tra cứu tĩnh & Phân loại ký tự (`Domain/UnicodeTables.fs`)** Xây dựng bảng mã Unicode NFC chuẩn UTF-16, phân loại tập nguyên âm, phụ âm ghép đầu/cuối và bảng ánh xạ biến đổi dấu.  
- **Phần 3: Thuật toán Ngữ pháp & Đặt dấu thanh (`Engine/ToneRules.fs`, `ModifierRules.fs`)** Thiết kế bộ phân rã âm tiết (Syllable Parser), quy tắc biến đổi mũ/móc/ngang Telex (`aa`, `aw`, `dd`), và logic đặt dấu thanh động (chuẩn mới `hòa/hóa` vs chuẩn cũ `hoà/hoá`).  
- **Phần 4: State Transition, Hoàn tác & Fallback tiếng Anh (`Engine/TelexEngine.fs`)** Thiết kế hàm chuyển trạng thái thuần túy `processKey`, cơ chế undo khi lặp phím (`ss`, `aa`), khôi phục từ khi nhấn Backspace và thuật toán nhận diện để bỏ qua từ tiếng Anh.  
- **Phần 5: Ma trận Test Cases kiểm thử (`BambooMintKey.Core.Tests`)** Quy hoạch hơn 200 kịch bản test chi tiết (từ đơn, từ ghép 3 nguyên âm, giữ nguyên hoa/thường, gõ phím nhanh) để làm tiêu chuẩn nghiệm thu (DoD).  

