<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Invalid tone from wrong Telex order `suawr`

## Tóm tắt

Người dùng gõ sai trình tự Telex (`suawr` thay vì `suwar`), kết quả là một chữ không tồn tại trong tiếng Việt (`suẳ`). Cần cơ chế tự động sửa hoặc gợi ý dấu thông minh hơn.

## Thông tin lỗi

| Trường | Giá trị |
|--------|---------|
| **Chuỗi phím đã gõ** | `suawr` + space |
| **Kết quả mong đợi** | `sửa` |
| **Kết quả thực tế** | `suẳ` |
| **Phạm vi** | Hầu hết các ứng dụng, cả hai kiểu gõ mới và cũ |
| **Cách sửa tạm thời** | Gõ đúng trình tự `suwar` |

## Mô tả chi tiết

Trong Telex chuẩn, dấu thanh phải được gõ sau nguyên âm cần đánh dấu. Trong trường hợp này:

- Gõ `suwar`: `w` tạo chữ `ư`, `r` tạo dấu hỏi trên `a` → ra `sửa` ✅
- Gõ `suawr`: `a` trước, sau đó `w` tạo `ư`, `r` đánh dấu hỏi vào vị trí không đúng → ra `suẳ` ❌

Tuy `suawr` là lỗi của người dùng, nhưng `suẳ` không phải là từ tiếng Việt hợp lệ. Vì vậy, engine có thể cải thiện trải nghiệm bằng cách:

1. **Tự động chuyển dấu về đúng vị trí** (smart tone placement) khi phát hiện chuỗi không hợp lệ.
2. **Tạo cụm từ thay thế / auto-correct**: khi gõ `suawr` thì tự động sửa thành `sửa`.

## Nguyên nhân dự kiến

- Engine chưa hỗ trợ tính năng **bỏ dấu tự do** (free tone placement) hoặc **auto-correct** cho các chuỗi Telex sai thứ tự.
- Thiếu từ điển kiểm tra tồn tại của từ tiếng Việt để gợi ý/sửa lỗi.

## Các bước tái hiện

1. Mở ứng dụng bất kỳ (Notepad, Word, v.v.).
2. Chuyển sang BambooMintKey (`Win + Space`).
3. Gõ `suawr` rồi nhấn `Space`.
4. Quan sát kết quả: chữ `suẳ` xuất hiện thay vì `sửa`.

## Môi trường

- **Ứng dụng gặp lỗi:** Hầu hết ứng dụng
- **Bố cục bàn phím trong Windows:** Vietnamese Telex
- **Phiên bản BambooMintKey:** *(cần cập nhật)*
- **Phiên bản Windows:** *(cần cập nhật)*

## Ghi chú bổ sung

- Cần cân nhắc giữa hai hướng tiếp cận:
  - **Strict Telex**: giữ đúng quy tắc, người dùng phải tự gõ đúng (`suwar`).
  - **Smart correction**: tự động sửa các chuỗi sai thứ tự thành từ đúng gần nhất.
- Nếu làm smart correction, cần đảm bảo không làm sai lệch các từ tiếng Anh hoặc các từ viết tắt có chứa các ký tự Telex.
