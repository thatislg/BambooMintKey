<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Auto-capitalization after uppercase Đ

## Tóm tắt

Khi gõ chữ bắt đầu bằng `Đ` viết hoa và nguyên âm tiếp theo không giữ phím `Shift`, các chữ cái sau `Đ` vẫn bị tự động viết hoa.

## Thông tin lỗi

| Trường | Giá trị |
|--------|---------|
| **Chuỗi phím đã gõ** | `Ddi` + space |
| **Kết quả mong đợi** | `Đi` |
| **Kết quả thực tế** | `ĐI` |
| **Phạm vi** | Hầu hết các ứng dụng, cả hai kiểu gõ mới và cũ |
| **Cách sửa tạm thời** | Gõ `Ddi` + space + Backspace + `i` |

## Mô tả chi tiết

Vấn đề xảy ra khi các chữ `Đ` và `Ê` được viết hoa ở đầu từ. Dù người dùng không giữ phím `Shift` cho các phím sau đó, engine vẫn tự động chuyển các con chữ theo sau thành chữ hoa.

Ví dụ:

- Gõ `Ddi` + space → mong muốn ra `Đi`, nhưng thực tế ra `ĐI`.
- Tương tự có thể xảy ra với các từ bắt đầu bằng `Ê` như `Êm` → `ÊM`.
- ÊM -> bug 
- Ơm -> Ok
- ĂN -> bug 
- ÂN -> bug 
- Ương -> Ok
- Ung -> Ok
- ÔN -> bug 
- Om -> Ok
- Ơn -> Ok

## Nguyên nhân dự kiến

Engine xử lý trạng thái phím `Shift` chưa được reset sau khi tạo ra chữ cái viết hoa đầu tiên, khiến các phím tiếp theo trong cùng một từ cũng được coi là đang giữ `Shift`.

## Các bước tái hiện

1. Mở ứng dụng bất kỳ (Notepad, Word, v.v.).
2. Chuyển sang BambooMintKey (`Win + Space`).
3. Gõ `Ddi` rồi nhấn `Space`.
4. Quan sát kết quả: chữ `ĐI` xuất hiện thay vì `Đi`.

## Môi trường

- **Ứng dụng gặp lỗi:** Hầu hết ứng dụng
- **Bố cục bàn phím trong Windows:** Vietnamese Telex / US
- **Phiên bản BambooMintKey:** *(cần cập nhật)*
- **Phiên bản Windows:** *(cần cập nhật)*

## Ghi chú bổ sung

- Lỗi này có thể liên quan đến logic xử lý `Shift state` trong engine hoặc trong TSF integration layer.
- Cần kiểm tra xem việc gõ `Đ` bằng tổ hợp `DD` có tạo ra một "dead shift state" kéo dài đến hết từ hay không.
