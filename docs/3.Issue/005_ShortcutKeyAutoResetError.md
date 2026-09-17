<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 005 : Phím tắt chuyển V/E cần thêm phím Space mới có hiệu lực

**Mã tài liệu:** `005_ShortcutKeyAutoResetError`  
**Trạng thái:** 🔍 Đang điều tra  
**Mức độ nghiêm trọng:** Cao — ảnh hưởng trực tiếp trải nghiệm chuyển đổi chế độ gõ.  
**Liên quan:** `docs/2.Design/Phase3/003_00_Taskbar_LanguageBar.md`, `src/BambooMintKey.UI/MainWindow.axaml.fs`, `src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs`

---

## 1. Mô tả lỗi

Người dùng bấm phím tắt chuyển V/E (ví dụ `Ctrl + Shift` hoặc `Ctrl + Shift + Q`) nhưng trạng thái gõ thực tế **không thay đổi ngay**. Chỉ sau khi bấm thêm **một phím bất kỳ** (chữ, số, space, backspace, ...), bộ gõ mới thực sự kích hoạt mode mới.

Hiện tượng này xảy ra ở:
- Thanh search bar của Windows.
- Các editor có thể gõ được.
- Có thể xảy ra ở mọi ứng dụng sử dụng BambooMintKey.

**Quan trọng:** Không phải chỉ `Space` mới kích hoạt. Bất kỳ phím nào cũng làm mode mới có hiệu lực.

## 2. Log quan sát được

File log: `docs/3.Issue/BambooMintKey_Runtime.log` (2026-09-17).

### 2.1. Phím tắt đang được cấu hình

Log cho thấy `PreserveKey` được đăng ký với giá trị lạ:

```text
[17:48:32.402] [13148] PreserveKey (BambooMintKey Toggle (0x51+0x0006)) hr=0x00000000
```

Nghĩa là `vKey=0x51` (phím `Q`) với `modifiers=0x0006` (`Ctrl | Shift`).  
Tuy nhiên UI lại hiển thị phím tắt là `Ctrl + Shift`, không phải `Ctrl + Shift + Q`.  
Người dùng sau đó đã đặt lại phím tắt; vấn đề chính là **toggle không có hiệu lực ngay** mà cần thêm một phím bất kỳ.

### 2.2. Dấu hiệu nhấn Ctrl + Shift bị nhận diện sớm

```text
[17:48:32.402] [13148] OnTestKeyDown ENTER vk=17
[17:48:32.402] [13148] PreserveKey (BambooMintKey Toggle (0x51+0x0006)) hr=0x00000000
[17:48:32.403] [13148] LangBarItemButton.NotifyStateChanged ENTER ...
[17:48:32.405] [13148] TsfCompartmentHelper.SetConversionMode isVietnamese=False, hr=0x00000000
[17:48:32.406] [13148] OnTestKeyDown modifier pressed, skip
[17:48:32.454] [13148] OnTestKeyDown ENTER vk=16
[17:48:32.454] [13148] OnTestKeyDown modifier pressed, skip
[17:48:32.808] [13148] OnPreservedKey ENTER rguid=e58a4372-b147-49d6-8c45-76df53e65b01
[17:48:32.808] [13148] TsfCompartmentHelper.SetConversionMode isVietnamese=True, hr=0x00000000
```

Nhận xét:
- `vk=17` là **Ctrl**, không phải `Q` hay `Shift`.
- `OnPreservedKey` lại toggle ngược lên `True` sau đó.
- Có dấu hiệu toggle **2 lần** trong một lần nhấn phím tắt, dẫn đến kết quả cuối cùng không như mong muốn.

### 2.3. Kết quả sau khi bấm Space

```text
[17:48:33.585] [13148] OnTestKeyDown ENTER vk=32
[17:48:33.587] [13148] TsfCompartmentHelper.SetConversionMode isVietnamese=True, hr=0x00000000
[17:48:33.636] [13148] LangBarItemButton.GetIcon ENTER requested='V', IsVietnameseMode=True, thread=1
```

Chỉ đến khi bấm thêm một phím bất kỳ (ví dụ `Space`), mode mới được đồng bộ lại và icon chuyển sang `V`.

## 3. Giả thuyết nguyên nhân

| # | Giả thuyết | Xác suất | Cách xác nhận |
|---|---|---|---|
| 1 | **UI lưu `HotkeyVKey` / `HotkeyModifiers` không khớp với preset Ctrl+Shift.** UI hiển thị "Ctrl + Shift" nhưng lưu xuống `0x51+0x0006` (Ctrl+Shift+Q). | Cao | Kiểm tra `%APPDATA%\BambooMintKey\config.json` xem `hotkeyVKey` và `hotkeyModifiers` giá trị gì. |
| 2 | **KeyEventSinkImpl nhận diện sai phím chính.** Code cũ chấp nhận cả `VK_CONTROL` (0x11) làm phím tắt khi target là `VK_SHIFT` (0x10). | Cao | Đã thấy log `OnTestKeyDown vk=17` kích hoạt `PreserveKey`. |
| 3 | **Double-toggle giữa `OnKeyDown` và `OnPreservedKey`.** Cả hai đường đều gọi `GlobalVEState.ToggleVietnameseMode`, làm đảo 2 lần trong một nhịp. | Cao | Log cho thấy `SetConversionMode` False rồi True liên tiếp. |
| 4 | **Chuyển mode không kích hoạt composition/input đang dang dở.** Ngay cả khi global state đã đổi, engine/config trong process chưa được áp dụng cho lần gõ tiếp theo cho đến khi có key event mới. | Cao | Log thiếu dòng `BridgeStateManager.Config` cập nhật ngay sau toggle; chỉ khi có key event mới thì mode mới có hiệu lực. |
| 5 | **OnKeyUp flag trong PreservedKey.** UI lưu `0x0202` (Ctrl + OnKeyUp), khiến TSF chỉ gọi toggle khi nhả phím. Nhưng log hiện tại lại thấy `0x0006`, không có OnKeyUp. | Trung bình | Cần xác nhận giá trị thực tế trong shared memory và `config.json`. |
| 6 | **Resync từ shared memory ghi đè lên trạng thái mới toggle.** Các watcher nghe event và gọi `ResyncFromSharedMemory`, có thể gây race condition. | Trung bình | Quan sát thứ tự `ResyncFromSharedMemory` / `SetConversionMode` sau khi toggle. |

## 4. Môi trường

- **Ứng dụng gặp lỗi:** Thanh search Windows, editor chung.
- **Phiên bản BambooMintKey:** build sau commit `8e9fc96`.
- **Phím tắt mong muốn:** `Ctrl + Shift` (UI hiển thị).
- **Phím tắt thực tế đăng ký:** log cho thấy `0x51+0x0006` (Ctrl+Shift+Q).

## 5. Các bước tái hiện

1. Mở Settings GUI → Tab "Bàn phím & Phím tắt".
2. Chọn preset "Ctrl + Shift".
3. Mở Notepad hoặc Windows Search.
4. Bấm `Ctrl + Shift` một lần.
5. Quan sát icon V/E và thử gõ tiếng Việt — có thể vẫn ở mode cũ.
6. Bấm thêm một phím bất kỳ (ví dụ `Space` hoặc một chữ cái) — mode mới mới có hiệu lực.

## 6. Thông tin cần thu thập thêm

- Giá trị `hotkeyVKey` và `hotkeyModifiers` trong `%APPDATA%\BambooMintKey\config.json`.
- Giá trị `ToggleHotkey` trong file JSON.
- Log với `BAMBOOMINTKEY_DEBUG=1` khi chỉ bấm **một lần duy nhất** `Ctrl + Shift`, không bấm gì thêm trong 2 giây; sau đó bấm thêm một phím bất kỳ.
- Behavior trên các ứng dụng: Notepad, VS Code, Chrome, Word.

## 7. Action Items

- [ ] Xác nhận giá trị `hotkeyVKey` / `hotkeyModifiers` trong `config.json`.
- [ ] Kiểm tra UI preset "Ctrl + Shift" có lưu đúng `0x10+0x0002` hay không.
- [ ] Fix `KeyInputTranslator.IsToggleHotkeyPressed` để chỉ nhận đúng phím chính.
- [ ] Ngăn double-toggle giữa `OnKeyDown` và `OnPreservedKey`.
- [ ] Đảm bảo khi toggle xong, `BridgeStateManager.Config` được invalidate để lần gõ tiếp theo dùng mode mới ngay lập tức.
- [ ] Cân nhắc xóa `OnKeyUp` flag khi đăng ký PreservedKey để toggle xảy ra ngay khi nhấn.
- [ ] Test lại sau fix và thu thập log mới.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **Chuỗi phím đã nhấn** | Để lập trình viên tái hiện chính xác từng phím. |
| **Kết quả mong đợi** | Toggle V/E ngay sau khi nhấn `Ctrl + Shift`, không cần thêm Space. |
| **Kết quả thực tế** | Phải bấm thêm Space mới kích hoạt. |
| **Ứng dụng** | Một số lỗi chỉ xảy ra trong ứng dụng nhất định do tích hợp TSF. |
| **Phiên bản** | Giúp xác định lỗi đã được sửa ở bản mới chưa. |
