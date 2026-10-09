<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 018: Chuyển E/V không có hiệu lực ngay & gõ loạn ký tự khi đổi ứng dụng trên macOS

**Mã tài liệu:** `018_MacOS_VE_NoEffect_And_Garbled_On_AppSwitch`

**Trạng thái:** ✅ Đã giải quyết — Bug A (V/E) và Bug B (nhân đôi ký tự) đã khắc phục, người dùng xác nhận gõ đúng.

**Mức độ nghiêm trọng:** Nghiêm trọng (High) — Đã khắc phục.

**Nền tảng:** macOS (InputMethodKit). Ghi nhận trên cả Windows/Linux với biểu hiện tương đương.

**Module liên quan:** `src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift` (vòng đời context, `handle`, `deactivateServer`, `InputSettings`)

**Ngày ghi nhận:** 09/10/2026

---

## 1. Mô tả hiện tượng

Hai lỗi riêng biệt, đều quan sát được trên macOS:

### 1.1. Bug A — Chuyển E/V không có hiệu lực ngay tại vị trí gõ hiện tại

- Người dùng chuyển chế độ từ V sang E (qua StatusBar hoặc menu IMK), nhưng **vẫn gõ được tiếng Việt** nếu giữ nguyên ô nhập liệu hiện tại.
- Chỉ khi chuyển sang ô/ứng dụng khác thì trạng thái mới mới "có vẻ" được áp dụng.

### 1.2. Bug B — Gõ loạn ký tự khi đổi ứng dụng

- Khi chuyển focus sang ứng dụng khác rồi gõ, chuỗi ra bị **nhân đôi / lẫn lộn**, ví dụ:
  ```
  ox ieens neuneu sudunsudun bobo gogo thithi lailai dangdang bibi loiloi lonlon meme jj roiroi cofncofn
  ```
- Mỗi ký tự/syllable bị lặp đôi (bobo, gogo, thithi, lailai, dangdang…) — dấu hiệu preedit/commit bị xử lý 2 lần, hoặc trạng thái `WordState` bị lẫn giữa các phiên.

### 1.3. Hành vi kỳ vọng (đúng chuẩn IME)

Người dùng nêu rõ kỳ vọng đúng chuẩn:
1. **Nhớ kiểu gõ (V/E) thống nhất xuyên suốt các ứng dụng** — đổi V/E một lần thì áp dụng toàn cục.
2. **Cắt trạng thái khi con trỏ chuột được sử dụng** (click chuột → commit/reset preedit).
3. **Cắt preedit khi bấm mũi tên lên/xuống/trái/phải, Space** — commit preedit trước khi nhường phím.

---

## 2. Nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Bug A — V/E không áp dụng lại vào context hiện tại

Trong `BambooMintKeyController.swift`:

- `InputSettings.apply(to:)` gọi `CABIBridge.setOptions(...)` để nạp cấu hình (gồm `isEnabled`) vào **context C-ABI hiện tại**.
- Hàm này **chỉ được gọi trong `ensureContext()`** — tức chỉ khi **tạo context mới**:
  ```swift
  private func ensureContext() -> UnsafeMutableRawPointer? {
      if let handle = contextHandle { return handle }   // context cũ -> KHÔNG apply lại
      let handle = CABIBridge.contextCreate()
      if let h = handle { InputSettings.apply(to: h) }  // chỉ apply cho context MỚI
      ...
  }
  ```
- Khi người dùng đổi V/E, `InputSettings.isVietnamese` thay đổi nhưng **context hiện tại vẫn giữ `isEnabled = 1` cũ** → gõ tiếp vẫn ra tiếng Việt.
- `registerModeObserver` (nhận thông báo từ StatusBar) cũng **chỉ cập nhật biến `isVietnamese`** mà không gọi `apply` lại vào context đang sống:
  ```swift
  { notification in
      ...
      isVietnamese = enabled   // chỉ set biến, không apply vào handle hiện tại
  }
  ```

### 2.2. Bug B — Trạng thái preedit/WordState không được commit/reset đúng khi đổi focus

- `deactivateServer` có commit preedit dở dang và `contextReset`, nhưng khả năng:
  1. `IMKServer` **tái sử dụng controller** giữa các ứng dụng, khiến `contextHandle`/`WordState` của app cũ bị dùng lại cho app mới mà không reset sạch.
  2. Thứ tự commit/reset và `replacementRange = NSNotFound` có thể khiến app mới nhận preedit cũ + phím mới → sinh ra chuỗi nhân đôi.
- Cần xác minh bằng log runtime (xem mục 6).

---

## 3. Ghi chú đa nền tảng

Người dùng xác nhận **Linux và Windows cũng có biểu hiện tương tự**, cho thấy có thể tồn tại một lớp vấn đề chung ở cơ chế:
- Duy trì trạng thái gõ thống nhất xuyên ứng dụng.
- Commit/reset preedit khi đổi focus / click chuột / phím điều hướng.

Tham khảo Issue 015 (Linux — preedit commit order & phím mũi tên) vì có cùng bản chất "preedit dở dang bị app xử lý sai khi đổi ngữ cảnh".

---

## 4. Môi trường tái hiện

- **Hệ điều hành:** macOS 27.0.1 (arm64).
- **Phiên bản BambooMintKey:** Phase 10 M5.
- **Ứng dụng:** TextEdit, các app khác (chuyển đổi qua lại).

## 5. Các bước tái hiện

**Bug A:**
1. Kích hoạt BambooMintKey.
2. Mở TextEdit, gõ `tieengs` → ra `tiếng` (đang chế độ V).
3. Chuyển sang chế độ E (StatusBar hoặc menu IMK).
4. Gõ `tieengs` tiếp → **vẫn ra `tiếng`** (kỳ vọng: `tieengs` — chế độ E).

**Bug B:**
1. Mở TextEdit, gõ một từ (preedit đang soạn dở).
2. Click sang ứng dụng khác.
3. Gõ tiếp → chuỗi ra bị nhân đôi/lẫn lộn.

## 6. Action Items

- [x] Sửa Bug A: gọi `InputSettings.apply(to:)` vào context hiện tại ở đầu `handle` mỗi lần xử lý phím.
- [x] Sửa Bug B: ở chế độ E, nhường toàn bộ phím cho ứng dụng (không gọi engine) — loại bỏ lỗi nhân đôi `testtest` do engine tích lũy `RawKeys` rồi commit lại khi space.
- [x] Gọi `contextReset` trong `activateServer` để làm sạch trạng thái cũ khi controller tái sử dụng giữa các app.
- [x] Kiểm chứng thực tế: gõ tiếng Việt có dấu (V) và tiếng Anh không dấu (E) đều đúng, người dùng xác nhận.
- [ ] Kiểm chứng lại trên Windows/Linux để xác định phạm vi chung của lỗi.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **V/E không áp dụng ngay** | Xác định context hiện tại không được `apply` lại cấu hình mới |
| **Chuỗi nhân đôi** | Xác định preedit/commit bị xử lý 2 lần khi đổi focus |
| **Đa nền tảng** | Khoanh vùng lỗi chung ở Core hoặc wrapper |
