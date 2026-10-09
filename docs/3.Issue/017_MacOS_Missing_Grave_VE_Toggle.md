<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 017: Bộ gõ macOS chưa hỗ trợ phím ` (grave) để chuyển chế độ V/E

**Mã tài liệu:** `017_MacOS_Missing_Grave_VE_Toggle`

**Trạng thái:** ✅ Đã giải quyết — thêm nhánh xử lý phím ` (grave, keyCode 50) để chuyển V/E tức thì.

**Mức độ nghiêm trọng:** Trung bình (Medium) — Đã khắc phục.

**Nền tảng:** macOS (InputMethodKit). Milestone M5 của Phase 10 (macOS).

**Module liên quan:** `src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift` (hàm `handle`)

**Ngày ghi nhận:** 09/10/2026

---

## 1. Mô tả hiện tượng

Trên macOS, người dùng **không thể chuyển đổi chế độ gõ Việt/Anh (V/E) bằng phím `` ` ``** (grave, phím dưới Esc) như đã quen trên Windows và Linux. Hiện tại chỉ có 2 cách đổi chế độ:

1. Bấm vào icon StatusBar (chữ V/E) → chọn "Tiếng Việt (V)" / "Tiếng Anh (E)".
2. Bấm vào menu input source → chọn "Tiếng Việt (Telex)" / "Tiếng Anh (English)".

Không có phím tắt nhanh bằng bàn phím.

---

## 2. Đối chiếu với Windows và Linux

- **Windows (TSF):** phím `` ` `` (grave) chuyển V/E tức thì.
- **Linux (Fcitx5):** phím `` ` `` (grave) chuyển V/E — xem `src/BambooMintKey.Fcitx5/engine.cpp` dòng 176–185:
  ```cpp
  // Hotkey cứng: phím ` (grave, dưới Esc) đổi V/E — giống chuẩn Kata/Romaji.
  if (keyEvent.key().sym() == FcitxKey_grave && !isSystemModifier(keyEvent.key())) {
      toggleVietnameseMode();
      keyEvent.filterAndAccept();
      keyEvent.inputContext()->updateUserInterface(
          fcitx::UserInterfaceComponent::StatusArea, true);
      return;
  }
  ```
- **macOS (IMK):** **chưa có** nhánh xử lý phím `` ` `` trong `handle(_:client:)`.

---

## 3. Nguyên nhân gốc rễ

Trong `BambooMintKeyController.swift`, hàm `handle` chỉ xử lý: phím bổ trợ (Cmd/Ctrl), Backspace (keyCode 51), phím ngắt từ, và ký tự in được ASCII. Không có nhánh nhận diện **keyCode 50** (phím grave/backtick trên bàn phím Mac) để gọi chuyển đổi V/E.

---

## 4. Phương án xử lý (đề xuất)

Thêm nhánh bắt **keyCode 50** (grave) trước nhánh Backspace, khi không có phím bổ trợ Cmd/Ctrl:

1. Đảo `InputSettings.isVietnamese`.
2. Áp dụng lại tùy chọn vào context hiện tại (`InputSettings.apply(to: handle)`).
3. Phát `NSDistributedNotificationCenter` với `isVietnameseMode` mới để đồng bộ icon V/E trên StatusBar.
4. Trả về `true` (nuốt phím) — không chèn ký tự `` ` `` vào văn bản.

---

## 5. Môi trường tái hiện

- **Hệ điều hành:** macOS 27.0.1 (arm64, Apple Silicon).
- **Phiên bản BambooMintKey:** Phase 10 M5.
- **Bàn phím:** layout US (phím grave ở keyCode 50).

## 6. Các bước tái hiện

1. Kích hoạt BambooMintKey làm nguồn nhập.
2. Mở TextEdit.
3. Bấm phím `` ` `` (dưới Esc).
4. **Quan sát:** chế độ không đổi, icon StatusBar không đổi (trong khi trên Linux/Windows nó đổi ngay).

## 7. Action Items

- [x] Thêm nhánh xử lý keyCode 50 (grave) trong `handle` để toggle V/E.
- [x] Đồng bộ trạng thái mới qua `NSDistributedNotificationCenter` cho StatusBar.
- [x] Lưu trạng thái V/E vào `config.json` (`InputSettings.persistMode`) để không mất khi restart.
- [x] Kiểm chứng icon V/E đổi tức thì khi bấm ` — người dùng xác nhận hoạt động.
- [x] Test hồi quy: gõ tiếng Việt có dấu ở chế độ V, gõ tiếng Anh không dấu ở chế độ E đều đúng.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **KeyCode** | Xác định đúng phím grave trên macOS (keyCode 50) |
| **Đối chiếu đa nền tảng** | Đảm bảo nhất quán trải nghiệm Windows/Linux/macOS |
| **Đồng bộ icon** | Đảm bảo StatusBar phản ánh đúng trạng thái mới |
