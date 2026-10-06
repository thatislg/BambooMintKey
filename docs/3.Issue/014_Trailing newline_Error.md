Dưới đây là bản tổng hợp (Overview) chi tiết về bản chất vấn đề và các phương án xử lý từ ngắn hạn đến triệt để.

---

# TỔNG QUAN VẤN ĐỀ (OVERVIEW)

### 1. Hiện tượng
- Khi đặt tên file/thư mục trên cả **Windows (dùng TSF)** và **Linux (dùng Fcitx)**, tên file ngầm chứa ký tự xuống dòng (`\n` hoặc `\r\n`).
- Bằng mắt thường trên GUI không thấy (do file manager render tên file trên 1 dòng), nhưng kiểm tra bằng Terminal, IDE (VS Code, JetBrains...) hoặc script thì thấy chuỗi bị ngắt dòng.
- Ví dụ: Khi tạo 1 file mới, đặt tên file xong thì space để kết thúc chuỗi preedit và enter để tạo, nhưng thay vì kết thúc việc tạo thì nó lại ghi thành tenfile\n và không kết thúc được việc tạo file. 

### 2. Bản chất kỹ thuật
Vì lỗi xảy ra trên **cả 2 nền tảng khác nhau**, nguyên nhân không nằm ở API của OS mà nằm ở **Core Engine** hoặc **tầng giao tiếp (Wrapper)** giữa Core và OS:

1. **Bộ gõ dùng `Space` làm tín hiệu kết thúc pre-edit:** 
   - Khi gõ văn bản thông thường, người dùng ấn `Space` để hoàn tất từ.
   - Nhưng khi **đổi tên file**, người dùng có phản xạ gõ xong là bấm **`Enter` ngay lập tức** để vừa chốt từ, vừa xác nhận tên.
2. **Xung đột tín hiệu:** 
   - Do Core chỉ đợi `Space`, khi phím `Enter` ập đến, Core rơi vào trạng thái bối rối: hoặc tự gom luôn mã `\n` của phím Enter vào buffer kết quả, hoặc trả kết quả ra ngoài nhưng **quên "nuốt" (eat/consume)** phím Enter.
   - Hậu quả: File Manager nhận một chuỗi có kèm mã `0x0A` (`\n`) hoặc nhận text xong thì ăn tiếp một sự kiện phím Enter thô, biến nó thành ký tự xuống dòng trong tên file.

---

# CÁC PHƯƠNG ÁN XỬ LÝ

---

### PHƯƠNG ÁN 1: Sanitize (Lọc sạch) chuỗi ở tầng Wrapper (Giải pháp nhanh / Hotfix)
> **Mục tiêu:** Chặn đứng lỗi ngay lập tức mà không cần sửa sâu vào Core Engine.

Dù Core Engine có trả về bất cứ thứ gì, Wrapper của TSF và Fcitx sẽ chịu trách nhiệm "dọn rác" trước khi commit vào OS.

* **Fcitx (C++):**
  ```cpp
  // Trước khi gọi: ic->commitString(str);
  void sanitize(std::string &str) {
      while (!str.empty() && (str.back() == '\n' || str.back() == '\r')) {
          str.pop_back();
      }
  }
  ```
* **TSF (C++ / COM):**
  Làm sạch chuỗi `wstring` hoặc buffer UTF-16 trước khi gọi `pEditSession` hoặc `SetText()` / `InsertTextAtSelection()`.

* **Đánh giá:**
  *  Rất nhanh, an toàn, không lo vỡ logic cũ.
  *  Chỉ là giải quyết phần ngọn, không sửa được triệt để vấn đề luồng xử lý phím.

---

### PHƯƠNG ÁN 2: Bổ sung `Enter` làm tín hiệu Commit ngang hàng với `Space` (Khuyên dùng)
> **Mục tiêu:** Đưa trải nghiệm gõ (UX) về đúng chuẩn quốc tế của các bộ gõ CJKV (Trung/Nhật/Hàn/Việt).

Quy tắc chuẩn của IME: **Nếu đang có chuỗi Pre-edit, phím Enter dùng để "chốt chữ", KHÔNG dùng để "xuống dòng/xác nhận form".**

* **Luồng xử lý mới:**
  ```text
  Có phím nhấn vào (Key Event)
         │
         ├── Phím là SPACE ──> Chốt Pre-edit + Thêm khoảng trắng (như cũ)
         │
         └── Phím là ENTER:
                 ├── Đang CÓ Pre-edit:
                 │     ├── 1. Lấy chuỗi text thuần túy (KHÔNG kèm \n)
                 │     ├── 2. Commit chuỗi vào hệ thống
                 │     ├── 3. Xóa sạch Pre-edit
                 │     └── 4. ĐÁNH DẤU PHÍM ĐÃ BỊ NUỐT (Consumed / Handled = TRUE)
                 │
                 └── KHÔNG CÓ Pre-edit:
                       └── Nhả phím Enter cho OS tự xử lý (Handled = FALSE)
  ```

* **Thao tác người dùng khi đặt tên file sẽ là:**
  - Bấm Enter **lần 1**: Chốt chuỗi tiếng Việt (Pre-edit biến mất, tên file hiện đầy đủ không có `\n`).
  - Bấm Enter **lần 2**: OS nhận lệnh Enter thực sự -> Lưu tên file và đóng ô Rename.
* **Đánh giá:**
  *  Chuẩn UX, giải quyết triệt để vấn đề cho mọi loại ứng dụng (File Explorer, Terminal, Search Bar, Game).
  * ⚠️ Cần tinh chỉnh lại State Machine của Core Engine để quản lý trạng thái `is_composing`.

---

### PHƯƠNG ÁN 3: Kiểm tra cơ chế Buffer và Ký tự kết thúc chuỗi (Null-Terminator) trong Core
> **Mục tiêu:** Loại trừ khả năng lỗi bộ nhớ (Memory Corruption).

Nếu bạn đã dùng chuột click ra ngoài hoặc dùng `Space` mà chuỗi **vẫn bị dính `\n`**, lỗi chắc chắn nằm ở code C/C++ xử lý chuỗi:

1. **Kiểm tra các hàm đọc:** Nếu bộ gõ của bạn nạp từ điển/luật gõ từ file hoặc parse input bằng các hàm như `fgets()`, `getline()`, ký tự `\n` ở cuối dòng có thể đã bị nạp ngầm vào bảng tra (Lookup Table).
2. **Kiểm tra độ dài chuỗi (String length):**
   - Đảm bảo khi gửi chuỗi từ Core -> Wrapper, tham số `length` là số lượng ký tự thực tế, **không cộng dôi** byte kết thúc chuỗi `\0`.
   - Nếu bạn tính nhầm `length = strlen(str) + 1`, một số API của hệ thống sẽ đọc byte rác đó và chuyển thành `\n`.

---

### Khuyến nghị lộ trình thực hiện:
1. **Bước 1 (Áp dụng ngay Phương án 1):** Viết hàm `sanitize` cắt bỏ toàn bộ `\r`, `\n` ở cuối chuỗi ngay tại cổng ra của TSF và Fcitx. Test lại việc tạo folder để xác nhận bug biến mất.
2. **Bước 2 (Áp dụng Phương án 2):** Cập nhật lại Core Engine để hỗ trợ biến cố phím **Enter khi đang Composing** (chốt chữ và nuốt phím). Đây là cách làm đúng đắn và lâu dài nhất cho một bộ gõ đa nền tảng.
