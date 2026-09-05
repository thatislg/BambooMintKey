# Khung Nghiên Cứu Kiến Trúc Lõi Bộ Gõ (F# Core Engine)

### Vùng 1: Cơ sở Ngữ âm học & Tập Luật Chính tả Tiếng Việt

Tập trung vào mô hình hóa toán học các quy tắc tiếng Việt để phục vụ bộ máy phân tích cú pháp.

- **Phạm vi nghiên cứu:**
  - **Mô hình hóa âm tiết:** Phân rã âm tiết thành bộ 5 thành tố $(O, M, N, C, T)$ tương ứng với Onset, Medial, Nucleus, Coda, Tone.
  - **Tập luật kết hợp ngữ âm (Phonotactics Rules):** Ma trận ràng buộc giữa các thành phần (nguyên âm nào đi được với âm đệm nào, phụ âm cuối nào giới hạn thanh điệu nào).
  - **Thuật toán đặt dấu thanh (Tone Placement):** So sánh và cài đặt luật đặt dấu kiểu mới (*hòa, thúy*) và kiểu cũ (*hoà, thuý*); xác định trọng âm âm tiết khi có nguyên âm đôi/ba.
  - **Xử lý biến âm & chính tả:** Luật tương thích chữ viết (`k/c/q`, `g/gh`, `ng/ngh`, `i/y`).
  - **Chuẩn mã hóa ký tự:** Phân biệt và chuyển đổi giữa Unicode Composite (Tổ hợp - NFD), Unicode Precomposed (Dựng sẵn - NFC), và mã di sản (TCVN3).

### Vùng 2: Cấu trúc Dữ liệu & Lý thuyết Tính toán (Parsing & State Machine)

Xây dựng mô hình máy trạng thái và cấu trúc lưu trữ tối ưu hóa bộ nhớ cho Functional Programming.

- **Phạm vi nghiên cứu:**
  - **Deterministic Finite Automaton (DFA):** Thiết kế đồ thị chuyển trạng thái hữu hạn cho luồng gõ (Idle $\rightarrow$ Buffering $\rightarrow$ Composing $\rightarrow$ Terminal/Invalid).
  - **Biến đổi trạng thái hàm thuần túy (Pure Functional State Transition):** Mô hình hóa hàm biến đổi `State -> Input -> State * Action`, không side-effect.
  - **Cấu trúc dữ liệu lưu trữ từ điển âm tiết:** So sánh Trie, Radix Tree, và Hash Set bất biến (`FSharp.Collections.Map/Set`) về chi phí bộ nhớ và tốc độ truy vấn $O(k)$.
  - **Thuật toán nhận diện ranh giới từ (Word Boundary Detection):** Xác định điểm bắt đầu/kết thúc từ qua tập ký tự ngắt (delimiters) và ngữ cảnh xung quanh.
  - **Cơ chế Backtracking & Rollback:** Chiến lược hoàn tác trạng thái (undo buffer) khi phát hiện chuỗi phím vi phạm luật âm tiết tiếng Việt.

### Vùng 3: Bóc tách Kiểu gõ & Quản lý Phím chức năng

Xử lý ánh xạ từ phím bấm vật lý sang các toán tử biến đổi dấu và phím tắt.

- **Phạm vi nghiên cứu:**
  - **Đặc tả toán tử biến đổi:** Tách biệt bộ logic gõ thành 2 nhóm lệnh: thêm/sửa thanh điệu (`AddTone`) và biến đổi thân chữ (`TransformGlyph` như mũ, móc, gạch ngang).
  - **Bảng ánh xạ kiểu gõ (Input Method Mappings):** Cấu hình hóa luật gõ cho Telex, VNI, và Simple Telex thành các declarative data contracts.
  - **Luật thoát dấu & lặp phím (Key Escaping / Double Key):** Quy tắc gõ 2 lần để trả về phím gốc (ví dụ: `ss` $\rightarrow$ `s`, `aa` $\rightarrow$ `a`).
  - **Cơ chế Macro / Gõ tắt:** Thuật toán khớp tiền tố/hậu tố nhanh cho bảng từ viết tắt mà không làm chậm mạch gõ thông thường.

### Vùng 4: Thuật toán Nhận diện Tiếng Anh & Chống Nhận diện Nhầm (False-Positive)

Giải quyết bài toán trải nghiệm thực tế khi người dùng gõ xen kẽ tiếng Việt và tiếng Anh/Code.

- **Phạm vi nghiên cứu:**
  - **Nhận diện tiền tố/hậu tố ngoại lai:** Phát hiện các phụ âm ghép không tồn tại trong tiếng Việt (`str`, `br`, `pr`, `fl`, `sh`, `ch` đứng đầu/cuối).
  - **Chiến lược Auto-Restore:** Tự động hoàn trả ký tự thô (ví dụ: gõ `code` không bị đổi thành `cođe`, `string` không bị nuốt chữ `s`).
  - **Chế độ gõ lướt (Fast Typing / Free Tone):** Bỏ dấu tự do ở bất kỳ vị trí nào trong từ mà vẫn định vị đúng nguyên âm chính để đặt dấu.
  - **Bộ lọc ứng dụng đặc thù:** Xử lý trường hợp người dùng gõ URL, email, path, hoặc mã lập trình (code variable names như `camelCase`, `snake_case`).

### Vùng 5: C-ABI Export & Giao tiếp Native (Interoperability Contract)

Đặc tả chuẩn nhị phân để F# Core NativeAOT có thể giao tiếp với C# (Windows TSF) và C++ (Linux Fcitx5).

- **Phạm vi nghiên cứu:**
  - **Thiết kế C-ABI Signature:** Định nghĩa các hàm `extern "C"` với tham số kiểu nguyên thủy (`int`, `uint32_t`, `char*`, con trỏ void).
  - **Quản lý vòng đời bộ nhớ (Memory Ownership):** Tránh cấp phát động qua biên giới ABI; kỹ thuật caller-allocated buffer để không gây memory leak hay đụng độ Garbage Collector.
  - **Mã hóa hành động (Action Codes):** Định dạng trả về để Native Bridge hiểu cần làm gì (`CONSUME`, `PASSTHROUGH`, `COMMIT_STRING`, `REPLACE_SURROUNDING_TEXT`).
  - **Xử lý NativeAOT Compilation Constraints:** Các hạn chế về reflection, dynamic loading khi biên dịch F# ra thư viện tĩnh/động (`.dll` / `.so`).

