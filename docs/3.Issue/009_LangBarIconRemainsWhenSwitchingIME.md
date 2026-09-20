<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue : Icon E/V của BambooMintKey vẫn hiển thị trên Taskbar khi chuyển sang bộ gõ khác (Google Japanese IME)

**Mã tài liệu:** `009_LangBarIconRemainsWhenSwitchingIME`

**Trạng thái:** 🔍 Đã xác định nguyên nhân gốc rễ — Chờ triển khai fix

**Mức độ nghiêm trọng:** Thấp / Trung bình (Giao diện UX) — Không gây crash hệ thống hay mất dữ liệu, nhưng gây hiểu nhầm cho người dùng về bộ gõ đang kích hoạt.

---

## 1. Mô tả hiện tượng

Người dùng thiết lập hệ thống sử dụng cùng lúc 3 bộ gõ / ngôn ngữ nhập liệu:
1. **Tiếng Anh** (English US / UK Keyboard)
2. **Tiếng Việt** (BambooMintKey)
3. **Tiếng Nhật** (Google Japanese Input / Mozc)

### Hiện tượng lỗi:
- Khi chuyển đổi từ **BambooMintKey** sang **Google Japanese Input** (thông qua phím tắt chuyển bộ gõ của Windows như `Win + Space` hoặc `Alt + Shift`):
  - Thay vì hiển thị icon chỉ thị trạng thái nhập liệu của Google IME (biểu tượng chữ Nhật `あ` / `A` kèm logo Google), Taskbar Windows lại **tiếp tục hiển thị biểu tượng icon E/V của BambooMintKey**.
  - Người dùng lầm tưởng BambooMintKey vẫn đang hoạt động hoặc bộ gõ chưa chuyển đổi thành công.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Vòng đời Text Service trong Windows TSF
Theo kiến trúc Windows Text Services Framework (TSF):
- Khi người dùng chọn kích hoạt một Text Service (IME), TSF gọi `ITfTextInputProcessorEx::ActivateEx` để khởi tạo.
- Khi người dùng chuyển sang bộ gõ khác hoặc hủy kích hoạt Text Service hiện tại, TSF gọi `ITfTextInputProcessor::Deactivate`.

### 2.2. Điểm lỗi trong mã nguồn BambooMintKey

#### Lỗi 1: `LangBarItemButton.Unregister()` bị bỏ quên hoàn toàn trong `DeactivateImpl`
Trong [BambooMintKeyTextService.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs#L439-L441):
```csharp
// Khi kích hoạt (ActivateExImpl):
// 5. Đăng ký Language Bar Item Button vào Taskbar
LangBarItemButton.Register(pThreadMgr, tfClientId);
```
Trong hàm `LangBarItemButton.Register`, nút bấm của BambooMintKey được đăng ký vào Windows Taskbar thông qua:
```csharp
mgrVTable->AddItem(_langBarMgr, ComInstance);
```
với thuộc tính `TfLbiStyleShownInTray` (buộc hiển thị trong khay ngôn ngữ của Taskbar).

Tuy nhiên, khi bộ gõ bị hủy kích hoạt trong [BambooMintKeyTextService.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs#L485-L533) (`DeactivateImpl`):
- Hệ thống đã gỡ `StateWatcher`, `CompartmentSink`, `KeyEventSink`, `ThreadMgrEventSink` và giải phóng `_pThreadMgr`.
- **Hoàn toàn KHÔNG gọi `LangBarItemButton.Unregister()`!**
- Hàm `LangBarItemButton.Unregister()` đã được định nghĩa sẵn trong [LangBarItemButton.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs#L859-L892) (gọi `ITfLangBarItemMgr::RemoveItem`), nhưng chưa từng được gọi ở bất kỳ đâu trong quá trình `Deactivate`.

Hậu quả: Đối tượng COM `LangBarItemButton` của BambooMintKey vẫn tồn tại trong danh sách quản lý của Windows Language Bar (`ITfLangBarItemMgr`), chiếm vị trí hiển thị trên khay Taskbar ngay cả khi người dùng đã chuyển sang Google Japanese Input hoặc bàn phím tiếng Anh.

#### Lỗi 2: `GetStatus` không kiểm tra trạng thái hoạt động của Text Service
Trong [LangBarItemButton.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs#L308-L314):
```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetStatus(IntPtr thisPtr, uint* pdwStatus)
{
    if (pdwStatus == null) return HResult.InvalidArgument;
    *pdwStatus = BridgeStateManager.IsVietnameseMode ? TsfLangBarFlags.TfLbiStatusBtnToggled : 0;
    return HResult.Ok;
}
```
- `GetStatus` không kiểm tra cờ `_isActivated`.
- Khi bộ gõ không active, nút bấm không trả về cờ `TsfLangBarFlags.TfLbiStatusHidden` (`0x00000001`), dẫn đến việc Windows tiếp tục coi nút này đang hiển thị bình thường.

---

## 3. Môi trường & Điều kiện tái hiện

- **Hệ điều hành:** Windows 10 / Windows 11.
- **Danh sách bộ gõ cài đặt:**
  1. English (United States)
  2. BambooMintKey (Vietnamese Telex)
  3. Google Japanese Input (Mozc)
- **Phiên bản BambooMintKey:** Các bản build hiện tại (NativeBridge TSF).

---

## 4. Các bước tái hiện lỗi (Reproduction Steps)

1. Cài đặt và cấu hình 3 bộ gõ: Tiếng Anh, BambooMintKey (Tiếng Việt), Google Japanese Input (Tiếng Nhật).
2. Mở một ứng dụng soạn thảo (ví dụ: Notepad, Chrome, hoặc VS Code).
3. Nhấn `Win + Space` chuyển sang **BambooMintKey**.
   - Quan sát: Icon **V** hoặc **E** xuất hiện trên khay hệ thống / Taskbar.
4. Nhấn `Win + Space` chuyển tiếp sang **Google Japanese Input**.
5. **Quan sát thực tế:**
   - Biểu tượng của Google Japanese Input (`あ` hoặc `A`) không xuất hiện hoặc bị che khuất / thay thế bởi icon **E/V** của BambooMintKey.
6. **Kết quả mong đợi:**
   - Icon **E/V** của BambooMintKey phải lập tức biến mất khỏi Taskbar khi BambooMintKey bị hủy kích hoạt (`Deactivate`).
   - Icon của Google Japanese Input (`あ`/`A`) phải hiển thị đúng chuẩn của Windows.

---

## 5. Phương án giải quyết (Proposed Fix)

### Bước 1: Gọi `LangBarItemButton.Unregister()` trong `DeactivateImpl`
Trong [BambooMintKeyTextService.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs):
```csharp
private static int DeactivateImpl(IntPtr thisPtr)
{
    var target = GetTarget(thisPtr);
    if (!target._isActivated) return HResult.Ok;

    // Gỡ nút Language Bar khỏi Taskbar trước khi giải phóng ThreadMgr
    LangBarItemButton.Unregister();

    // Dừng luồng lắng nghe cấu hình
    target.StopStateWatcher();
    ...
```

### Bước 2: Bổ sung xử lý trạng thái ẩn/hiện trong `LangBarItemButton`
- Cập nhật hàm `GetStatus` trong [LangBarItemButton.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs) để trả về `TsfLangBarFlags.TfLbiStatusHidden` khi bộ gõ đang ở trạng thái không kích hoạt.
- Khi chuyển đổi focus hoặc activate/deactivate, gửi notification `OnUpdate(TsfLangBarFlags.TfLbiStatus)` cho Windows Taskbar để đảm bảo Windows cập nhật giao diện ngay lập tức mà không cần đợi vòng render tiếp theo.

### Bước 3: Tham chiếu chuẩn triển khai của Google Mozc
- Google Mozc (`tip_lang_bar_menu.cc` và `tip_text_service.cc`) luôn quản lý nghiêm ngặt cặp hàm `AddItem` khi `ActivateEx` và `RemoveItem` khi `Deactivate`.
- Đảm bảo việc `RemoveItem` được gọi an toàn trên cùng luồng STA hoặc thông qua message proxy để tránh xung đột với Explorer shell.

---

## 6. Kế hoạch kiểm thử (Verification Checklist)

- [ ] Chuyển qua lại giữa 3 bộ gõ (English, BambooMintKey, Google Japanese IME) liên tục bằng `Win + Space`.
- [ ] Xác nhận icon E/V biến mất ngay khi rời khỏi BambooMintKey.
- [ ] Xác nhận icon của Google IME (`あ`/`A`) hiển thị chuẩn xác khi chuyển sang tiếng Nhật.
- [ ] Xác nhận icon của English Keyboard hiển thị chuẩn xác khi chuyển sang tiếng Anh.
- [ ] Xác nhận khi chuyển lại BambooMintKey, icon E/V tái xuất hiện mượt mà, không gặp lỗi com exception hay rò rỉ tài nguyên COM.
