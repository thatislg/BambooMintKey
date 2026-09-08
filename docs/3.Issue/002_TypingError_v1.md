<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue Template: Báo lỗi gõ tiếng Việt

Sử dụng template này khi báo lỗi liên quan đến việc gõ tiếng Việt (Telex/VNI) trong BambooMintKey.

Ví dụ:

- **Chuỗi phím đã gõ:** `gooox`
- **Kết quả mong đợi:** `gõ`
- **Kết quả thực tế:** `goxxx`

---

## 1. Bảng mô tả lỗi gõ

Sử dụng bảng dưới đây để liệt kê các lỗi gặp phải. Mỗi dòng là một trường hợp lỗi riêng biệt.

| STT | Chuỗi phím đã gõ | Kết quả mong đợi | Kết quả thực tế | Ứng dụng/Chú thích | Cách sửa tạm thời |
|-----|------------------|------------------|-----------------|--------------------|-----|
| 1 | `phari` + space | `phải` | `phari` | Hầu hết các ứng dụng, cả 2 kiểu gõ mới và cũ. Các chữ có 2 âm tiết không có phụ âm cuối đều hay phải làm thế này. Tức là bắt buộc phải bỏ dấu ở cuối từ | Gõ theo trình tự: phair |
| 2 | `Ddi` + space | `Đi` | `Ddi` | Hầu hết các ứng dụng, cả 2 kiểu gõ mới và cũ | Gõ theo trình tự: `Dd` + space + Backspace + `i` |
| 3 | `Uwu` + space | `Ưu` | `Uwu` | Hầu hết các ứng dụng, cả 2 kiểu gõ mới và cũ | Gõ theo trình tự: `Uw` + space + Backspace + `u` |
| 4 | `Core` (cần gõ `Corre`) + space | `Core` | `Corre` | Hầu hết các ứng dụng, cả 2 kiểu gõ mới và cũ. Các từ tiếng Anh chứa `r` sau nguyên âm hay bị nhầm thành dấu hỏi. Để ra đúng `Core` phải gõ thừa 1 chữ `r`, lúc này engine hiểu `rr` → bỏ dấu hỏi, trả lại `r` thường | Gõ `Corre` rồi di chuyển tới chữ `r` dư và xoá đi 1 chữ |
|  |  |  |  |  |  |
|  |  |  |  |  |  |

## Ghi chú phân tích

- **Lỗi 1 (`phari` → `phải`):** Có khả năng liên quan đến tính năng **bỏ dấu tự do** (free tone placement) mà BambooMintKey hiện tại chưa hỗ trợ. Trong Telex chuẩn, dấu thanh phải được đặt đúng vị trí theo quy tắc; tuy nhiên nhiều bộ gõ khác cho phép người dùng gõ dấu ở cuối từ (`phair`) rồi engine tự chuyển dấu về đúng vị trí (`phải`). BambooMintKey hiện tại có vẻ chưa xử lý trường hợp này.

**Chú thích cột:**

- **STT:** Số thứ tự lỗi.
- **Chuỗi phím đã gõ:** Gõ đúng từng phím bạn đã nhấn, bao gồm cả phím dấu. Nếu cần gõ khác đi để đạt kết quả mong muốn, ghi chú trong ngoặc.
- **Kết quả mong đợi:** Theo quy tắc Telex/VNI, chuỗi phím trên lẽ ra phải ra chữ gì?
- **Kết quả thực tế:** BambooMintKey hiện tại đang trả về chuỗi gì?
- **Ứng dụng/Chú thích:** Ứng dụng gặp lỗi (VD: Notepad, Word, Chrome) hoặc ghi chú thêm.
- **Cách sửa tạm thời:** Cách gõ khác hoặc thao tác bổ sung để đạt được kết quả mong muốn.

## 2. Môi trường

- **Ứng dụng gặp lỗi:**
  <!-- VD: Notepad, Microsoft Word, Google Chrome, VS Code, v.v. -->
- **Bố cục bàn phím trong Windows:**
  <!-- VD: Vietnamese Telex, US, UK, v.v. -->
- **Phiên bản BambooMintKey:**
  <!-- VD: commit abc1234, build 0.1.0, v.v. -->
- **Phiên bản Windows:**
  <!-- VD: Windows 11 23H2, Windows 10 22H2, v.v. -->

## 3. Các bước tái hiện

1. Mở ứng dụng `...`
2. Chuyển sang BambooMintKey (`Win + Space`)
3. Gõ `...`
4. Quan sát kết quả

## 4. Thông tin thêm

- Lỗi xảy ra ở chế độ gõ tiếng Việt hay tiếng Anh?
- Có phím tắt hoặc tùy chọn đặc biệt nào đang bật không?
  <!-- VD: chế độ VNI, bảng mã Unicode/TCVN, macro, v.v. -->
- Lỗi có xảy ra ở mọi ứng dụng hay chỉ một ứng dụng cụ thể?
- Có thông báo lỗi, crash log, hoặc hành vi bất thường nào khác không?

---

## Lưu ý khi báo lỗi

| Trường | Tại sao cần |
|--------|-------------|
| **Chuỗi phím đã gõ** | Để lập trình viên có thể tái hiện chính xác từng phím bạn nhấn. |
| **Kết quả mong đợi** | Xác định hành vi đúng theo quy tắc Telex/VNI. |
| **Kết quả thực tế** | Xác định lỗi engine hoặc TSF đang trả về. |
| **Ứng dụng** | Một số lỗi chỉ xảy ra trong ứng dụng nhất định do tích hợp TSF. |
| **Phiên bản** | Giúp xác định lỗi đã được sửa ở bản mới chưa. |
