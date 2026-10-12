<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 023: Xung đột hiển thị biểu tượng E/V của BambooMintKey trên Taskbar khi chuyển đổi qua lại giữa các Input Method (Google Japanese Input, US Keyboard) và xử lý khởi động lại máy

**Mã tài liệu:** `023_Windows_LangBar_EV_Icon_Conflict_With_Other_IMEs`  
**Trạng thái:** ✅ Đã khắc phục triệt để qua `TfLbiStyleHiddenStatusControl` & `GetKeyboardLayout`  
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

### 3.2. Nguyên nhân 2: Tranh chấp slot GUID_LBI_INPUTMODE giữa nhiều tiến trình (Multi-Process Conflict)

- BambooMintKey là một In-Process COM Server được nạp vào **tất cả các ứng dụng có trường nhập liệu** (IDE, Browser, Notepad, Explorer,...). Mỗi tiến trình sở hữu một địa chỉ bộ nhớ `ComInstance` riêng biệt.
- Khi gọi `ITfLangBarItemMgr::AddItem()`, Windows TSF chỉ chấp nhận **duy nhất 1 item** đại diện cho `GUID_LBI_INPUTMODE`.
- Tiến trình đầu tiên được khởi động (ví dụ IDE) gọi `AddItem()` thành công và chiếm slot Taskbar. Các tiến trình khác (Notepad, Browser) khi gọi `AddItem()` bị trả về mã lỗi `0x80004005` (`E_FAIL` - duplicate registration).
- Khi người dùng gõ trên Notepad/Browser và bấm `Win + Space` chuyển sang Google Japanese Input:
  - Notepad/Browser kích hoạt `DeactivateImpl` $\rightarrow$ gọi `Unregister()` trên con trỏ của mình (vốn chưa bao giờ được đăng ký thành công).
  - Trong khi đó, tiến trình IDE đang chạy nền **vẫn giữ nguyên con trỏ cũ và không bao giờ nhận lệnh Deactivate**!
  - **Hệ quả:** Icon E/V của tiến trình nền nằm lì cố định vĩnh viễn trên Taskbar, cướp slot hiển thị và chặn không cho Google Japanese Input hiển thị icon `あ` / `A` của nó.

### 3.3. Nguyên nhân 3: Thiếu cờ điều khiển ẩn hiện động trong Windows TSF

- Trong `TF_LANGBARITEMINFO.dwStyle`, trước đây thiếu cờ `TF_LBI_STYLE_HIDDENSTATUSCONTROL` (`0x00000001`).
- Theo tài liệu Windows SDK `ctfutb.h`: Nếu không có cờ này, Windows Taskbar sẽ **bỏ qua hoàn toàn** trạng thái ẩn `TF_LBI_STATUS_HIDDEN` (`0x00000001`) trả về từ `GetStatus`.
- Đồng thời hàm `GetStatus` trước đây không kiểm tra cờ `_isAdded`, luôn báo trạng thái `0` (không ẩn) cho Windows.

---

## 4. Phương án giải quyết triệt để

### 4.1. Bổ sung cờ `TfLbiStyleHiddenStatusControl` trong `GetInfo`
Theo chuẩn tài liệu Windows TSF SDK `ctfutb.h`:
```csharp
pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnButton |
                 TsfLangBarFlags.TfLbiStyleShownInTray |
                 TsfLangBarFlags.TfLbiStyleHiddenStatusControl;
```

### 4.2. Quản lý trạng thái `GetStatus`
Khi nút chưa được thêm hoặc không thuộc tiến trình đang hoạt động (`!_isAdded`), `GetStatus` lập tức trả về `TF_LBI_STATUS_HIDDEN` (`0x00000001`):
```csharp
private static int GetStatus(IntPtr thisPtr, uint* pdwStatus)
{
    if (pdwStatus == null) return HResult.InvalidArgument;
    if (!_isAdded)
    {
        *pdwStatus = TsfLangBarFlags.TfLbiStatusHidden;
        return HResult.Ok;
    }
    *pdwStatus = BridgeStateManager.IsVietnameseMode ? TsfLangBarFlags.TfLbiStatusBtnToggled : 0;
    return HResult.Ok;
}
```

### 4.3. Quản lý vòng đời động theo Tiêu điểm nhập liệu (`OnSetFocus`)
Để triệt tiêu hoàn toàn việc tiến trình nền giữ slot khay hệ thống của tiến trình tiền cảnh:
1. **Khi cửa sổ nhận focus (`OnSetFocus` với `pdimFocus != IntPtr.Zero`) và `_isActivated == true`:**
   Tiến trình tiền cảnh gọi `LangBarItemButton.Register()`, đưa icon E/V lên Taskbar.
2. **Khi cửa sổ mất focus (`OnSetFocus` với `pdimFocus == IntPtr.Zero`) hoặc bị Deactivate:**
   Tiến trình lập tức gọi `LangBarItemButton.Unregister()` (`ITfLangBarItemMgr::RemoveItem()`), giải phóng hoàn toàn slot Taskbar.
3. Nhờ cơ chế này, khi người dùng chuyển sang Google Japanese Input hoặc bộ gõ khác, slot được giải phóng ngay lập tức, nhường toàn quyền hiển thị cho Google Japanese Input (`あ` / `A`) và Microsoft Japanese IME mà không còn bất kỳ xung đột nào.

---

## 5. Kế hoạch kiểm thử & Kết quả nghiệm thu

1. **Bộ Unit Test tự động ([LangBarItemLifecycleTests.cs](file:///d:/Kojin/BambooMintKey/tests/BambooMintKey.NativeBridge.Tests/LangBarItemLifecycleTests.cs)):**
   - `LangBarItemButton_GetInfo_HasCorrectStyleAndGuid`: Khẳng định `dwStyle` có đủ 3 cờ `BtnButton`, `ShownInTray`, `HiddenStatusControl`. 👉 **PASSED**
   - `LangBarItemButton_GetStatus_ReflectsVietnameseMode`: Khẳng định trả về `StatusHidden` khi unregistered, `BtnToggled` khi tiếng Việt, `0` khi tiếng Anh. 👉 **PASSED**
   - `LangBarItemButton_RegisterAndUnregister_AddsAndRemovesFromLanguageBar`: Kiểm tra thêm/gỡ thực tế với COM `msctf.dll`. 👉 **PASSED**
   - `TextService_ActivateAndDeactivate_LifecycleManagesLangBarItem`: Kiểm tra vòng đời `ActivateEx` / `Deactivate`. 👉 **PASSED**
   - `VietnameseTyping_PreservedAcrossActivateDeactivateCycles`: Khẳng định sau nhiều chu kỳ chuyển đổi gõ `tiengs` vẫn ra `"tiếng"`, trạng thái gõ không bị mất. 👉 **PASSED**
   - Tổng cộng: **473/473 tests toàn solution PASSED 100%**.

2. **Kiểm tra COM NativeAOT ([BambooMintKey.DevHarness](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.DevHarness/Program.cs)):**
   - Kiểm tra C-ABI VTable, `ITfLangBarItemButton`, `ITfSource`, GDI stress test 500 lần (0 leak), 200 thread parallel test. 👉 **PASSED 100%**.

3. **Kiểm nghiệm thực tế trên hệ điều hành Windows 11 (Máy người dùng):**
   - Chuyển `BambooMintKey` $\leftrightarrow$ `Google Japanese Input`: Biểu tượng E/V biến mất lập tức khi sang Google Japanese Input, icon `あ` / `A` của Google hiển thị trơn tru; khi chuyển lại BambooMintKey thì icon E/V hiện lại ngay lập tức.
   - Chuyển `BambooMintKey` $\leftrightarrow$ `Microsoft Japanese IME`: Hoạt động chuẩn xác.
   - Chuyển `BambooMintKey` $\leftrightarrow$ `US Keyboard`: Hoạt động chuẩn xác.
   - Khả năng gõ tiếng Việt hoàn toàn bảo toàn, không bị mất trạng thái.
   - 👉 **Xác nhận người dùng: Đã hoạt động chuẩn xác!**
