<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 008_02 — Thiết Kế Dữ Liệu Từ Điển Âm Tiết Tiếng Việt Chuẩn MIT & Ngữ Liệu Mở

**Mã tài liệu:** `008_02_MIT_Dictionary_And_Corpus_Design`  
**Giai đoạn:** Phase 8 — Tích hợp từ điển & sửa lỗi vặt  
**Thuộc module:** `BambooMintKey.Core` (Dùng chung độc lập cho cả Windows & Linux)  
**Trạng thái:** 📝 Đã hoàn thiện thiết kế chi tiết  
**Tài liệu tham chiếu:** [008_01_InvestigationForDictionary.md](008_01_InvestigationForDictionary.md), `THIRD-PARTY-NOTICES.md`

---

## 1. Mục Tiêu & Phạm Vi Thiết Kế

Tài liệu này đặc tả toàn diện giải pháp tự chủ nguồn dữ liệu từ điển cho bộ gõ BambooMintKey, nhằm mục đích:
1. Xây dựng tập dữ liệu ~7.800 âm tiết tiếng Việt hợp lệ và danh mục 20.000 từ tiếng Anh thông dụng đạt chuẩn bản quyền **MIT / CC0 100%**.
2. Loại bỏ hoàn toàn sự lệ thuộc vào các file dữ liệu mang giấy phép GPL mượn từ dự án IBus-Bamboo, bảo vệ tính toàn vẹn của giấy phép MIT cho toàn bộ dự án BambooMintKey.
3. Thiết lập cơ chế đóng gói dữ liệu dạng tài nguyên nhúng (Embedded Resource) để bộ gõ hoạt động độc lập tuyệt đối trên cả Windows và Linux mà không phụ thuộc vào đường dẫn tập tin trên ổ đĩa.

---

## 2. Vấn Đề Cần Giải Quyết & Phương Pháp Thực Hiện

### Vấn Đề 1: Tự sinh ma trận âm tiết tiếng Việt bằng quy tắc ngữ âm học

- **Hiện trạng & Thách thức:**
  Tiếng Việt là ngôn ngữ đơn lập, mọi âm tiết đều tuân theo cấu trúc ngữ âm chặt chẽ gồm bốn thành phần: Phụ âm đầu, Âm đệm/Nguyên âm chính, Phụ âm cuối, và Thanh điệu. Nếu chỉ thu thập từ ngữ một cách thủ công sẽ dễ bỏ sót các biến thể âm tiết hợp lệ hoặc gom phải từ rác.
- **Đầu vào (Input):**
  - Danh mục 27 phụ âm đầu chuẩn của tiếng Việt: b, c, ch, d, đ, g, gh, gi, h, k, kh, l, m, n, ng, ngh, nh, p, ph, qu, r, s, t, th, tr, v, x và trường hợp âm tiết không có phụ âm đầu (phụ âm rỗng).
  - Danh mục các cụm nguyên âm đơn, nhị trùng âm và tam trùng âm hợp lệ: a, ă, â, e, ê, i, o, ô, ơ, u, ư, y, ia, iê, oa, oă, oe, oi, ôi, ơi, oo, ua, uâ, uo, uô, uê, ui, uy, uơ, ưa, ươ, ưu, ye, yê, oai, oay, oao, oeo, uai, uay, uoi, uôi, ươi, ươu, uya, uye, uyê, uyu, ieu, iêu, yeu, yêu.
  - Danh mục 8 phụ âm cuối hợp lệ: c, ch, m, n, ng, nh, p, t và trường hợp không có phụ âm cuối.
  - Hệ thống 6 thanh điệu: Ngang (không dấu), Sắc, Huyền, Hỏi, Ngã, Nặng.
- **Đầu ra (Output):**
  - Một tập hợp danh sách các chuỗi âm tiết thô được sinh ra từ việc kết hợp có kiểm soát ngữ âm.
- **Cách giải quyết:**
  - Thực hiện thuật toán tích Descartes giữa bốn tập hợp thành phần ngữ âm.
  - Áp dụng các bộ lọc ràng buộc chính tả bắt buộc của chữ Quốc ngữ:
    1. **Ràng buộc âm tắc cuối:** Các âm tiết kết thúc bằng phụ âm tắc vô thanh (c, ch, p, t) bắt buộc chỉ được kết hợp với hai thanh điệu là thanh Sắc hoặc thanh Nặng. Mọi tổ hợp của âm tắc cuối với thanh Ngang, Huyền, Hỏi, Ngã đều bị loại bỏ ngay lập tức.
    2. **Ràng buộc tương thích giữa phụ âm đầu và nguyên âm:** Phụ âm k, gh, ngh bắt buộc phải đứng trước các nguyên âm dòng trước (e, ê, i, y, iê, ia). Ngược lại, phụ âm c, g, ng không được đứng trước e, ê, i. Phụ âm qu luôn đi kèm với âm đệm u.
    3. **Ràng buộc nguyên âm ngắn:** Các nguyên âm ngắn như ă, â bắt buộc phải có phụ âm cuối đi kèm (ví dụ: "băn", "bất"), không tồn tại âm tiết mở chỉ có riêng ă hoặc â đứng cuối từ.
    4. **Ràng buộc bán nguyên âm cuối:** Bán nguyên âm y chỉ đi sau a, â, u, uâ, oai, uay; bán nguyên âm i đi sau các nguyên âm khác.

---

### Vấn Đề 2: Lọc âm tiết thực tế qua kho ngữ liệu mở Wikipedia tiếng Việt (Corpus Extraction)

- **Hiện trạng & Thách thức:**
  Ma trận ngữ âm thuần túy có thể sinh ra một số âm tiết về mặt lý thuyết ngữ âm thì ghép được nhưng trong thực tế đời sống tiếng Việt không ai sử dụng (từ trống ngữ nghĩa). Cần một bộ lọc ngữ liệu thực tế để giữ lại tập hợp âm tiết chuẩn xác nhất.
- **Đầu vào (Input):**
  - Tập hợp âm tiết thô sinh ra từ Vấn đề 1.
  - Bản sao lưu cơ sở dữ liệu mở của Wikipedia tiếng Việt (Wikipedia Database Dump dạng text mở, phát hành theo giấy phép CC-BY-SA và CC0).
- **Đầu ra (Output):**
  - Tệp danh mục chuẩn `dicts/vietnamese-syllables-mit.dict` chứa chính xác ~7.800 âm tiết tiếng Việt thông dụng và chuẩn mực, mỗi âm tiết trên một dòng, chuẩn hóa theo định dạng Unicode NFC, chữ thường.
- **Cách giải quyết:**
  - Dùng tiến trình xử lý văn bản đọc toàn bộ các bài viết từ kho ngữ liệu Wikipedia tiếng Việt.
  - Phân tách văn bản thành các từ đơn, loại bỏ các ký tự dấu câu, số và ký tự đặc biệt.
  - Xây dựng bảng tần số xuất hiện của từng âm tiết trong kho ngữ liệu.
  - Đối chiếu danh sách âm tiết từ Vấn đề 1 với bảng tần số:
    - Nếu một âm tiết xuất hiện trong kho ngữ liệu thực tế với tần số xuất hiện lớn hơn hoặc bằng ngưỡng tối thiểu quy định, âm tiết đó được xác thực là từ có nghĩa và được đưa vào tập từ điển chính thức.
    - Bổ sung các âm tiết cổ, từ láy đặc biệt hoặc địa danh truyền thống của Việt Nam được ghi nhận trong từ điển chính thống.
  - Sắp xếp danh sách theo thứ tự bảng chữ cái tiếng Việt và lưu trữ ở dạng tệp văn bản thuần UTF-8 không có byte order mark (BOM).

---

### Vấn Đề 3: Chuẩn hóa danh mục 20.000 từ tiếng Anh thông dụng

- **Hiện trạng & Thách thức:**
  Danh sách từ tiếng Anh phục vụ tính năng bảo vệ và tự động hoàn tác (English Protection & Backtracking) hiện đang bị hardcode khoảng 200 từ trong mã nguồn, dẫn đến việc nhiều từ tiếng Anh thông dụng khi gõ bị biến dạng thành âm tiết tiếng Việt ngoài ý muốn.
- **Đầu vào (Input):**
  - Danh mục 20.000 từ tiếng Anh thông dụng nhất thế giới (dựa trên tần suất Google Books N-gram Corpus công khai).
- **Đầu ra (Output):**
  - Tệp danh mục chuẩn `dicts/english-20k.dict` chứa 20.000 từ tiếng Anh thông dụng, chữ thường, không chứa từ rác hay ký tự lạ.
- **Cách giải quyết:**
  - Lọc bỏ toàn bộ các từ đơn lẻ có độ dài 1 ký tự (trừ các đại từ và mạo từ hợp lệ như a, i).
  - Loại bỏ các từ chứa ký tự phi La-tinh, chữ số hoặc dấu nối.
  - Chuẩn hóa toàn bộ về chữ thường Unicode NFC.
  - Khử trùng lặp và sắp xếp theo thứ tự bảng chữ cái.

---

### Vấn Đề 4: Đóng gói tài nguyên dạng nhúng (Embedded Resource) độc lập hệ điều hành

- **Hiện trạng & Thách thức:**
  Nếu lưu trữ từ điển dưới dạng các tệp tin rời rạc trên ổ cứng (`%AppData%` trên Windows hay `/usr/share/` trên Linux), ứng dụng sẽ đối mặt với các nguy cơ: Người dùng vô tình xóa tệp, lỗi phân quyền đọc tệp, đường dẫn cài đặt khác biệt giữa các bản phân phối Linux (Debian, Arch, Fedora) dẫn tới lỗi không tìm thấy từ điển khi chạy.
- **Đầu vào (Input):**
  - Hai tệp dữ liệu đã được chuẩn hóa: `vietnamese-syllables-mit.dict` (kích thước khoảng 60KB) và `english-20k.dict` (kích thước khoảng 150KB).
- **Đầu ra (Output):**
  - Thư viện nhị phân `BambooMintKey.Core.dll` (và phiên bản chia sẻ `BambooMintKeyCore.so`) chứa sẵn toàn bộ dữ liệu từ điển bên trong manifest của assembly.
  - Khởi động bộ gõ không cần đọc bất kỳ tệp tin rời nào từ ổ đĩa.
- **Cách giải quyết:**
  - Cấu hình chỉ thị tài nguyên nhúng trong tệp mô tả dự án của `BambooMintKey.Core`. Khi trình biên dịch đóng gói thư viện, nội dung hai tệp văn bản này sẽ được nén tĩnh và đặt trực tiếp vào phân vùng dữ liệu của tệp nhị phân.
  - Khi chương trình khởi động, tầng logic dùng luồng đọc dữ liệu nội bộ của assembly để giải nén dữ liệu từ bộ nhớ vào cấu trúc bảng băm tĩnh (FrozenSet).
  - Tổng kích thước bổ sung vào file nhị phân sau khi nén chỉ khoảng 80KB đến 100KB, hoàn toàn tối ưu và miễn nhiễm 100% với lỗi đường dẫn tập tin trên cả Windows và Linux.

---

## 3. Khẳng Định Bản Quyền & Tính Pháp Lý

1. **Tính chất dữ liệu ngôn ngữ:**
   Danh sách âm tiết tiếng Việt đơn lẻ và danh mục từ tiếng Anh thông dụng thuần túy là tập hợp các đơn vị từ vựng tồn tại khách quan trong ngôn ngữ tự nhiên. Chúng thuộc nhóm dữ kiện thực tế của nhân loại, không phải là tác phẩm phái sinh hay sản phẩm sáng tạo văn học nghệ thuật được bảo hộ bản quyền độc quyền.
2. **Tuyên bố giấy phép:**
   Bộ dữ liệu sau khi được trích xuất, làm sạch và đóng gói trong dự án BambooMintKey được phát hành chính thức theo giấy phép **MIT License** và nhãn bản quyền công cộng **Creative Commons Zero (CC0)**. Điều này cho phép dự án phân phối tự do, nhúng trực tiếp vào các sản phẩm thương mại hoặc mã nguồn mở khác mà không gặp bất kỳ ràng buộc lây nhiễm bản quyền nào từ GPL.
