<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue : Lỗi hiển thị ICON EV



---

## 1. Bảng mô tả lỗi gõ

- Sau 1 thời gian đặt shortcutkey thì nó lại tự động reset về Ctrl + Shift thay vì tổ hợp phím đã đặt. 

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
