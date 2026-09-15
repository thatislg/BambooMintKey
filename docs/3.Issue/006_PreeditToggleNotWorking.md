<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue : Lỗi tính năng Preedit không hoạt động sau khi thay đổi cài đặt

**Mã tài liệu:** `006_PreeditToggleNotWorking`

**Trạng thái:** 🔍 Đang điều tra

**Liên quan:** `docs/2.Design/Phase5/005_05_DisplayAttributeProvider.md`

---

## 1. Mô tả lỗi

Người dùng bật/tắt tùy chọn **"Bật tính năng hiển thị gạch chân khi soạn thảo (Preedit)"** trong UI cấu hình, nhưng hiệu ứng gạch chân trên các ứng dụng (VS Code, Chrome, Edge, Word...) không thay đổi theo.

Cụ thể:
- Khi **bật Preedit**: vẫn không thấy đường gạch chân nét đứt.
- Khi **tắt Preedit**: vẫn có thể hiển thị gạch chân (fallback của ứng dụng) hoặc ngược lại.

## 2. Giả thuyết nguyên nhân

| # | Giả thuyết | Xác suất | Cách xác nhận |
|---|---|---|---|
| 1 | Người dùng chưa cài lại `BambooMintKey.dll` mới sau khi xóa phần mềm cũ. Windows vẫn load DLL cũ hoặc registry TSF trỏ đến đường dẫn đã mất. | Cao | Kiểm tra process đang gõ có load đúng DLL mới không; chạy installer mới. |
| 2 | TSF/Chromium cache display attribute theo GUID duy nhất. Khi toggle Preedit, ứng dụng không gọi lại `GetAttributeInfo`. | Cao | Bật log `BAMBOOMINTKEY_DEBUG=1`, kiểm tra có dòng `GetAttributeInfo` mới không. |
| 3 | Shared memory offset 21 (`EnablePreedit`) không được cập nhật từ UI. | Trung bình | Kiểm tra `config.json` và shared memory bằng PowerShell. |
| 4 | `ITfCategoryMgr::RegisterGUID` thất bại, atom = 0, nên `SetValue` không có hiệu lực. | Trung bình | Kiểm tra log `RegisterGUID`. |
| 5 | Ứng dụng target tự vẽ underline của riêng mình, không phụ thuộc TSF display attribute. | Thấp | Test trên nhiều ứng dụng khác nhau. |

## 3. Môi trường

- **Ứng dụng gặp lỗi:** VS Code, Chrome, Edge, Microsoft Word (cần xác nhận thêm)
- **Bố cục bàn phím trong Windows:** (cần xác nhận)
- **Phiên bản BambooMintKey:** build sau commit `fa1a2e2`
- **Phiên bản Windows:** (cần xác nhận)

## 4. Các bước tái hiện

1. Cài đặt BambooMintKey.
2. Mở UI cấu hình → Tab "Tùy chọn gõ".
3. Toggle checkbox "Bật tính năng hiển thị gạch chân khi soạn thảo (Preedit)".
4. Mở VS Code / Chrome.
5. Gõ một từ tiếng Việt chưa commit (ví dụ `đang`).
6. Quan sát có/không đường gạch chân nét đứt bên dưới.

## 5. Thông tin thêm

- Lỗi xảy ra ở cả chế độ V và E?
- Toggle Preedit có lưu vào `config.json` không?
- Có cần restart ứng dụng sau khi toggle không?
- Log `%TEMP%\BambooMintKey_Runtime.log` có gì khi `BAMBOOMINTKEY_DEBUG=1`?

## 6. Action Items

- [ ] Xác nhận người dùng đã cài lại build mới nhất.
- [ ] Bật log và thu thập `BambooMintKey_Runtime.log`.
- [ ] Kiểm tra `EnablePreedit` trong shared memory (offset 21).
- [ ] Kiểm tra atom của 2 GUID Stealth/Preedit.
- [ ] Xác nhận behavior trên VS Code, Chrome, Edge, Word, Notepad++.
- [ ] Triển khai fix 2-GUID nếu chưa có (đã implement trong code, cần verify).
- [ ] Cập nhật tài liệu `005_05_DisplayAttributeProvider.md` nếu cần.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **Chuỗi phím đã gõ** | Để lập trình viên tái hiện chính xác. |
| **Kết quả mong đợi** | Xác định hành vi đúng theo thiết kế. |
| **Kết quả thực tế** | Xác định lỗi TSF/DisplayAttribute đang trả về. |
| **Ứng dụng** | Một số lỗi chỉ xảy ra trong ứng dụng nhất định do tích hợp TSF. |
| **Phiên bản** | Giúp xác định lỗi đã được sửa ở bản mới chưa. |
