<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 023: Xung đột hiển thị biểu tượng E/V của BambooMintKey trên Taskbar khi chuyển đổi qua lại giữa các Input Method (Google Japanese Input, US Keyboard) và xử lý khởi động lại máy

**Mã tài liệu:** `023_Windows_LangBar_EV_Icon_Conflict_With_Other_IMEs`  
**Trạng thái:** 🛠️ Đang triển khai & Sửa đổi mã nguồn  
**Mức độ nghiêm trọng:** Trung bình (Ảnh hưởng trải nghiệm người dùng đa ngôn ngữ, gây xung đột hiển thị giao diện khay hệ thống)  
**Ngày ghi nhận:** 12/10/2026  
**Nền tảng ảnh hưởng:** Windows 10 / Windows 11 (Text Services Framework — TSF)  
**Mô-đun liên quan:**
- `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs`
- `src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs`
- `src/BambooMintKey.NativeBridge/Interop/TsfLangBarTypes.cs`

---

## 1. Mô tả hiện tượng & Bảng so sánh lỗi

### 1.1. Bối cảnh
Người dùng làm việc đa ngôn ngữ thường cài đặt song song nhiều bộ gõ trên hệ điều hành:
1. **Tiếng Việt:** BambooMintKey (TSF TIP)
2. **Tiếng Nhật:** Google Japanese Input (hoặc Microsoft Japanese IME)
3. **Tiếng Anh:** US English Keyboard

### 1.2. Hiện tượng lỗi
- Khi người dùng đang chọn **BambooMintKey**, biểu tượng **V** (hoặc **E**) màu xanh chữ trắng xuất hiện hợp lệ trên khay Taskbar của Windows.
- Khi người dùng nhấn phím chuyển bộ gõ (`Win + Space` hoặc `Alt + Shift`) để chuyển sang **Google Japanese Input**:
  - Biểu tượng của Google Japanese Input (chữ `あ` hoặc `A`) xuất hiện, **nhưng biểu tượng E/V của BambooMintKey không biến mất**.
  - Biểu tượng E/V tiếp tục tồn tại song song, chiếm chỗ trên Taskbar và gây cảm giác như BambooMintKey vẫn đang can thiệp vào tiến trình gõ.
  - Tương tự khi chuyển sang **US Keyboard**, biểu tượng E/V vẫn tồn tại lì lợm.
- **Yêu cầu nghiệp vụ cốt lõi:**
  1. Biểu tượng **E/V chỉ được phép xuất hiện khi Input Method hiện tại là BambooMintKey**.
  2. Ngay khi chuyển sang Input Method khác, biểu tượng E/V **phải lập tức biến mất** khỏi Taskbar.
  3. **Cover trường hợp tắt máy bật lại (Reboot / Sign-in):**
     - Nếu lúc bật máy, bàn phím mặc định đang là BambooMintKey $\rightarrow$ Bắt buộc phải có biểu tượng E/V hiển thị.
     - Nếu lúc bật máy, bàn phím mặc định là bộ gõ khác (Google Japanese Input hoặc US Keyboard) $\rightarrow$ Tuyệt đối không được hiển thị biểu tượng E/V.

---

## 2. Đối chiếu kinh nghiệm giải quyết trên macOS

Trên macOS (InputMethodKit — tham chiếu `docs/4.Progress/009_MacOS_M5_StatusBar_And_VESync_Report.md`), vấn đề tương tự đã được giải quyết triệt để thông qua mô hình:

```swift
// src/BambooMintKey.Mac.StatusBar/StatusBarController.swift
private func isBambooMintKeyActive() -> Bool {
    guard let source = TISCopyCurrentKeyboardInputSource()?.takeRetainedValue() else { return false }
    guard let raw = TISGetInputSourceProperty(source, kTISPropertyInputSourceID) else { return false }
    let id = Unmanaged<CFTypeRef>.fromOpaque(raw).takeUnretainedValue() as! CFString
    return (id as String).hasPrefix("com.bamboomintkey.inputmethod")
}

private func updateVisibility() {
    statusItem.isVisible = isBambooMintKeyActive()
}
```

* **Cơ chế:** Lắng nghe thông báo hệ thống `TISNotifySelectedKeyboardInputSourceChanged` và `didActivateApplicationNotification`.
* **Khởi động:** Ngay trong hàm khởi tạo `init()`, gọi `updateVisibility()` để kiểm tra xem Input Source lúc mở máy có phải là BambooMintKey không. Nếu không phải thì ẩn hoàn toàn.

---

## 3. Phân tích nguyên nhân gốc rễ trên Windows (TSF)

Khác với macOS sử dụng daemon `NSStatusItem`, trên Windows, biểu tượng trạng thái của bộ gõ được quản lý bởi **Windows Text Services Framework (TSF)** thông qua đối tượng giao tiếp COM `ITfLangBarItemButton` và bộ quản lý `ITfLangBarItemMgr`.

Qua rà soát mã nguồn trong thư mục `src/BambooMintKey.NativeBridge/TSF/`, phát hiện 2 nguyên nhân cốt lõi:

### 3.1. Nguyên nhân 1: `DeactivateImpl` bị quên hoàn toàn lệnh gỡ `LangBarItemButton.Unregister()`
Trong [BambooMintKeyTextService.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs#L439-L441):
- Khi kích hoạt (`ActivateExImpl`), bộ gõ đăng ký nút vào Taskbar:
  ```csharp
  LangBarItemButton.Register(pThreadMgr, tfClientId);
  ```
  Hàm này gọi `mgrVTable->AddItem(_langBarMgr, ComInstance)`.
- Tuy nhiên, trong hàm hủy kích hoạt [DeactivateImpl](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs#L485-L533):
  ```csharp
  private static int DeactivateImpl(IntPtr thisPtr)
  {
      var target = GetTarget(thisPtr);
      if (!target._isActivated) return HResult.Ok;

      target.StopStateWatcher();
      // Unadvise CompartmentEventSink...
      // Unadvise KeyEventSink...
      // Unadvise ThreadMgrEventSink...
      // TerminateActiveComposition...
      // Release pThreadMgr...
      // HOÀN TOÀN KHÔNG CÓ LangBarItemButton.Unregister()!
  }
  ```
- **Hậu quả:** Mặc dù bộ gõ đã bị Deactivate khi người dùng chuyển sang Google Japanese Input, đối tượng COM `LangBarItemButton` của BambooMintKey **vẫn nằm nguyên trong danh sách quản lý của Windows Language Bar (`ITfLangBarItemMgr`)**. Windows Taskbar không nhận được lệnh gỡ bỏ nên tiếp tục vẽ nút E/V trên khay hệ thống!

### 3.2. Nguyên nhân 2: `GetStatus` không trả về `TfLbiStatusHidden` khi bộ gõ không hoạt động
Trong [LangBarItemButton.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs#L308-L314):
```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetStatus(IntPtr thisPtr, uint* pdwStatus)
{
    if (pdwStatus == null) return HResult.InvalidArgument;
    *pdwStatus = BridgeStateManager.IsVietnameseMode ? TsfLangBarFlags.TfLbiStatusBtnToggled : 0;
    return HResult.Ok;
}
```
- Hàm `GetStatus` không kiểm tra trạng thái hoạt động của Text Service.
- Kể cả khi chưa đăng ký (`_langBarMgr == IntPtr.Zero`) hoặc đã bị deactive, hàm vẫn không gắn cờ `TsfLangBarFlags.TfLbiStatusHidden` (`0x00000001`). Do đó nếu Windows Taskbar gửi truy vấn thăm dò trạng thái, nút luôn báo là "đang hiển thị bình thường".

### 3.3. Cơ chế vòng đời khi khởi động lại máy (Reboot / Sign-in)
- Khi Windows khởi động, tiến trình `ctfmon.exe` nạp hạ tầng TSF.
- TSF đọc thiết lập ngôn ngữ mặc định của người dùng:
  - **Trường hợp A (Khởi động vào BambooMintKey):** TSF gọi `ActivateEx` trên `BambooMintKeyTextService` của ứng dụng tiền cảnh $\rightarrow$ `LangBarItemButton.Register` được thực thi $\rightarrow$ Biểu tượng E/V xuất hiện.
  - **Trường hợp B (Khởi động vào Input Method khác):** TSF **chỉ gọi `ActivateEx` trên bộ gõ đó (ví dụ Google Japanese Input)** và KHÔNG gọi `ActivateEx` trên BambooMintKey. Nếu nút E/V không tự ý đăng ký ngoài luồng và `GetStatus` mặc định trả về `TfLbiStatusHidden`, biểu tượng E/V sẽ hoàn toàn vô hình, đúng 100% yêu cầu.

---

## 4. Phương án giải quyết chi tiết

### 4.1. Bước 1: Quản lý cờ đăng ký và trả về `TfLbiStatusHidden` trong `LangBarItemButton`
Trong [LangBarItemButton.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs):
1. Thêm biến trạng thái `private static bool _isRegistered = false;`.
2. Khi `Register` thành công: gán `_isRegistered = true;`.
3. Khi `Unregister`: gán `_isRegistered = false;`.
4. Trong hàm `GetStatus`:
   ```csharp
   if (!_isRegistered || _pThreadMgr == IntPtr.Zero)
   {
       *pdwStatus = TsfLangBarFlags.TfLbiStatusHidden;
       return HResult.Ok;
   }
   ```

### 4.2. Bước 2: Gọi `LangBarItemButton.Unregister()` trong `DeactivateImpl`
Trong [BambooMintKeyTextService.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs):
Ngay đầu hàm `DeactivateImpl`, trước khi giải phóng `_pThreadMgr`, gọi lệnh gỡ bỏ nút:
```csharp
private static int DeactivateImpl(IntPtr thisPtr)
{
    var target = GetTarget(thisPtr);
    if (!target._isActivated) return HResult.Ok;

    // 0. Gỡ nút Language Bar khỏi Taskbar ngay khi chuyển sang Input Method khác
    LangBarItemButton.Unregister();

    target.StopStateWatcher();
    ...
```

---

## 5. Kế hoạch kiểm thử & Tiêu chuẩn nghiệm thu

1. **Test Case 1: Chuyển đổi linh hoạt giữa các bộ gõ (`Win + Space`)**
   - Đang ở BambooMintKey $\rightarrow$ Icon E/V hiển thị.
   - Bấm `Win + Space` chuyển sang Google Japanese Input $\rightarrow$ Icon E/V biến mất tức thì, chỉ hiển thị icon `あ`/`A` của Google IME.
   - Bấm `Win + Space` chuyển sang English US $\rightarrow$ Icon E/V không hiển thị, Taskbar hiển thị chuẩn ENG.
   - Bấm `Win + Space` quay lại BambooMintKey $\rightarrow$ Icon E/V tái xuất hiện mượt mà.

2. **Test Case 2: Kiểm thử kịch bản Khởi động máy (Reboot / Sign-in)**
   - Đặt Input Method mặc định là Google Japanese Input $\rightarrow$ Khởi động lại máy $\rightarrow$ Kiểm tra Taskbar: Không có icon E/V của BambooMintKey.
   - Đặt Input Method mặc định là BambooMintKey $\rightarrow$ Khởi động lại máy $\rightarrow$ Kiểm tra Taskbar: Icon E/V hiển thị ngay lập tức.
