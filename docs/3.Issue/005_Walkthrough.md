<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Walkthrough 005: Hoàn Thiện 100% Cơ Chế Chuyển Đổi V/E Theo Chuẩn Google Japanese Input (Mozc / TSF Native) & Khóa Phím Tắt Cố Định Dưới Esc

**Mã tài liệu:** `005_Walkthrough`  
**Ngày cập nhật:** 2026-09-17  
**Liên quan:** 
- `docs/3.Issue/005_ShortcutKeyAutoResetError.md` (Issue báo lỗi ban đầu)
- `docs/3.Issue/005_01_Mozc_TSF_Mode_Activation.md` (Tài liệu thiết kế kiến trúc chuẩn Mozc TSF)

---

## 1. Mục tiêu đã hoàn thành

1. **Khóa cứng phím tắt chuyển đổi V/E cố định theo vị trí phím vật lý dưới Esc:**
   - **Bàn phím tiếng Anh (US):** Phím `` ` `` / `~` (`VK_OEM_3` = `0xC0`) bấm trực tiếp, tổ hợp `Alt + ~` (chuẩn Google Japanese Input trên bàn phím US) và `Ctrl + ~`. (Vẫn bảo toàn thao tác `Shift + ~` để gõ ký tự dấu ngã `~` khi soạn thảo văn bản).
   - **Bàn phím tiếng Nhật (JIS):** Phím 半角/全角 / 漢字 (`VK_KANJI` = `0x19`, `VK_OEM_AUTO` = `0xF3`, `VK_OEM_ENLW` = `0xF4`) với cờ `TsfModFlags.IgnoreAllModifier` (`0x0400`).
2. **Khóa hoàn toàn ở tầng UI và Cấu hình (Zero Auto-Reset):**
   - Không cho phép người dùng thay đổi hoặc gán phím tắt tùy biến nữa.
   - Bảng điều khiển cài đặt hiển thị thông tin phím tắt cố định rõ ràng, loại bỏ toàn bộ các nút bấm và sự kiện ghi nhận phím tắt gây lỗi tự reset.
   - Các hàm lưu trữ cấu hình `SharedConfig.fs` và `SharedMemoryManager.cs` luôn ghi nhận preset phím tắt mặc định cố định.
3. **Trị dứt điểm lỗi "hoạt động không kiểm soát được bật tắt V/":**
   - **Phát hiện từ Runtime Log:** Khi chuyển cửa sổ hoặc một ứng dụng mất focus (`focus=0`), Windows TSF tự chuyển compartment `OpenClose` của thread đó về `0` (False). Trước đây `OnCompartmentChanged` đọc `isOpen = false` và ngay lập tức ghi đè `SharedMemoryManager.IsVietnameseMode` thành `False`, đồng thời kích hoạt vòng lặp gọi đệ quy (echo cascade).
   - **Cách xử lý:** 
     + Thêm cờ `[ThreadStatic] public static bool IsInternalCompartmentSync;` để chặn 100% các tín hiệu echo nội bộ.
     + Thêm cờ `_hasFocus = (pdimFocus != IntPtr.Zero);`. Khi thread mất focus, `OnCompartmentChanged` sẽ bỏ qua, không bao giờ ghi đè làm tắt tiếng Việt toàn cục.

---

## 2. Chi tiết các sửa đổi cốt lõi

### 2.1. Tầng TSF Native & PreservedKey
- **[KeyEventSinkHelper.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkHelper.cs)**:
  - Cố định hàm `GetActiveToggleKeys()` luôn trả về danh sách các phím chuẩn: `VK_OEM_3` (0, Alt, Ctrl), `VK_KANJI`, `VK_OEM_AUTO`, `VK_OEM_ENLW`.
  - Cập nhật `AllPossibleKeys` bao gồm toàn bộ các phím này để giải phóng sạch sẽ khi Deactivate.
- **[KeyEventSinkImpl.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs)**:
  - Loại bỏ hoàn toàn kiểm tra phím tắt thủ công trong `OnTestKeyDown` và `OnKeyDown`.
  - Điểm chuyển đổi duy nhất là callback `OnPreservedKey` được Windows TSF gọi trực tiếp.
- **[KeyInputTranslator.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Interop/KeyInputTranslator.cs)**:
  - Loại bỏ hàm quét phím tắt kiểu Unikey `IsToggleHotkeyPressed` và `IsKeyDown`.
- **[Guids.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/Guids.cs)**:
  - Sửa `IidITfCompartmentEventSink` thành `743ABD5F-F26D-48DF-8CC5-238492419B64` (chuẩn SDK `msctf.h`).

### 2.2. Xử lý Vòng lặp Echo & Focus Churn
- **[BambooMintKeyTextService.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs)**:
  - Khai báo `[ThreadStatic] public static bool IsInternalCompartmentSync;` và biến cờ `_hasFocus`.
  - Trong `OnCompartmentChanged`: Bỏ qua nếu `IsInternalCompartmentSync == true` hoặc `!_hasFocus`.
  - Trong `OnSetFocus`: Cập nhật `_hasFocus = (pdimFocus != IntPtr.Zero)`. Chỉ đồng bộ trạng thái vào thread compartment khi thực sự có focus, và bọc bằng `IsInternalCompartmentSync`.
  - Trong `StartStateWatcher`: Bọc việc cập nhật compartment bằng `IsInternalCompartmentSync`.
- **[GlobalVEState.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/GlobalVEState.cs)**:
  - Bọc khối cập nhật `TsfCompartmentHelper.SetOpenClose` và `SetConversionMode` bằng `IsInternalCompartmentSync` để ngắt đệ quy toàn cục.

### 2.3. Khóa Cố Định trên Cấu hình & Giao diện (UI)
- **[SharedMemoryManager.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs)**:
  - Cố định mặc định trong RAM và file JSON: `hotkeyVKey = 192 (0xC0)`, `hotkeyModifiers = 1 (Alt)`.
- **[SharedConfig.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/SharedConfig.fs)**:
  - Cố định `ToggleHotkey = 0uy`, `HotkeyVKey = 0xC0u`, `HotkeyModifiers = 0x0001u`, `HotkeyDisplay = "`/ ~ / 半角/全角 (JP) | Alt + ~"` khi nạp và lưu cấu hình.
- **[MainWindow.axaml](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/MainWindow.axaml)**:
  - Thay thế toàn bộ khung gán phím tắt tùy biến và các nút chip chọn nhanh bằng card thông tin cố định:
    `"MẶC ĐỊNH CỐ ĐỊNH: Phím dưới Esc (` / ~ / 半角/全角 / 漢字)"`.
- **[MainWindow.axaml.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/MainWindow.axaml.fs)**:
  - Gỡ bỏ các trường điều khiển gán phím tắt, gỡ bỏ `SetHotkey` và các listener sự kiện `KeyDown` / `KeyUp` gán phím.

---

## 3. Kết quả nghiệm thu & Kiểm thử

1. **Kiểm thử tự động (Unit Tests):**
   - `BambooMintKey.Core.Tests`: **321/321 tests PASSED**.
   - `BambooMintKey.NativeBridge.Tests`: **5/5 tests PASSED**.
2. **Biên dịch & Đóng gói:**
   - Solution biên dịch thành công 100% với 0 lỗi.
   - Bộ cài đặt mới đã được đóng gói:
     `D:\Kojin\BambooMintKey\bin\dist\BambooMintKey-Setup.exe` (12.38 MB).
3. **Quản lý phiên bản:**
   - Toàn bộ thay đổi đã được commit và push lên nhánh `main` của repository `thatislg/BambooMintKey`.

---

## 4. Bổ sung: Khắc phục lỗi hiển thị phản ánh Icon Taskbar E/V (2026-09-18)

- **Hiện tượng:** Phím tắt chuyển đổi E/V rất mượt mà và không giắt phím, nhưng icon E/V trên Taskbar không cập nhật theo.
- **Nguyên nhân cốt lõi qua Runtime Log (`BambooMintKey_Runtime.log`):**
  1. **Apartment Thread Mismatch (STA vs Worker Thread):** `ITfLangBarItemSink::OnUpdate` là interface COM đơn luồng (STA) thuộc về UI Thread của `explorer.exe` (Thread 6). Khi gọi từ luồng nền (Worker Thread 3/7), Windows TSF âm thầm hủy thông báo vẽ lại. Khi gọi trên UI Thread 6, Explorer gọi ngay `GetIcon` sau 54ms.
  2. **Cấu hình `dwStyle` và cờ trạng thái:** Cần cấu hình nút chuẩn Google Mozc TSF (`tip_lang_bar_menu.cc`): `TfLbiStyleBtnButton | TfLbiStyleShownInTray`, `GetStatus` trả về `TfLbiStatusBtnToggled` khi ở chế độ Tiếng Việt, và gửi cờ `TfLbiStatus` kèm trong `OnUpdate`.
- **Giải pháp áp dụng:**
  - **[LangBarItemButton.cs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs)**:
    + Thêm cơ chế Marshalling thông điệp qua Win32 Message-Only Window (`HWND_MESSAGE`).
    + Khi luồng nền phát hiện thay đổi trạng thái, `PostMessageW(hMsgWnd, WM_STATE_CHANGED, 0, 0)` sang UI Thread.
    + `MsgWndProc` trên UI Thread tiếp nhận và gọi `_pLangBarSink->OnUpdate()` đúng căn hộ STA COM.

---

## 5. Sự cố nghiêm trọng: Crash `msctf.dll` (`0xc000041d`) trên `SearchHost.exe` & `explorer.exe` (Màn hình đen) và Cách khắc phục triệt để

### 5.1. Triệu chứng sự cố
Sau khi cài đặt phiên bản thử nghiệm hỗ trợ cập nhật Global Compartment, khi người dùng chọn IME BambooMintKey:
- `SearchHost.exe` (CortanaUI / Windows Search) và `explorer.exe` lập tức bị sập, thanh Taskbar biến mất, màn hình máy tính chuyển sang màu đen.
- **Windows Event Log ghi nhận:**
  ```
  Faulting application name: SearchHost.exe, version: 2607.28006.200.0, time stamp: 0x6a6d0c36
  Faulting module name: msctf.dll, version: 10.0.26100.9278, time stamp: 0x328c1816
  Exception code: 0xc000041d (STATUS_FATAL_USER_CALLBACK_EXCEPTION)
  Fault offset: 0x0000000000028ebb
  Faulting application path: C:\WINDOWS\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe
  Faulting module path: C:\WINDOWS\System32\msctf.dll
  Faulting package-relative application ID: CortanaUI
  ```

### 5.2. Điều tra nguyên nhân gốc rễ (Root Cause)
1. **Lệch VTable Slot COM trong `TsfCompartmentHelper.cs` (Thủ phạm gây Access Violation):**
   - Trong nỗ lực gọi `ITfThreadMgr::GetGlobalCompartment`, một cấu trúc `TfThreadMgrVTable` tự chế đã được cài đặt, gán `GetGlobalCompartment` vào **Slot 11**.
   - Đối chiếu trực tiếp với file định nghĩa chính thức của Windows 11 SDK (`C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\msctf.h:1119-1196`):
     - Slot 0..2: `IUnknown` (`QueryInterface`, `AddRef`, `Release`)
     - Slot 3..9: Các method context & focus
     - Slot 10: `IsThreadFocus`
     - **Slot 11: `GetFunctionProvider(REFCLSID clsid, ITfFunctionProvider **ppFuncProv)`** (KHÔNG PHẢI `GetGlobalCompartment`!)
     - Slot 12: `EnumFunctionProviders`
     - **Slot 13: `GetGlobalCompartment`**
   - **Hậu quả chết người:** Khi gọi slot 11, chương trình thực chất đã gọi vào `GetFunctionProvider` và truyền vào một con trỏ stack tùy ý thay vì một con trỏ GUID `REFCLSID`. `msctf.dll` cố giải tham chiếu con trỏ rác này gây ra **Access Violation** tại `msctf.dll+0x28ebb`, làm sập bất kỳ tiến trình nào kích hoạt IME (`SearchHost.exe`, `explorer.exe`).
2. **Vi phạm bảo mật Sandbox AppContainer của UWP:**
   - Ban đầu, `LangBarItemButton.Register()` gọi `EnsureMsgWndCreated()` tạo cửa sổ Win32 `HWND_MESSAGE` trên mọi tiến trình nạp DLL IME.
   - Các ứng dụng hiện đại của Windows 10/11 như `SearchHost.exe`, Start Menu, Settings chạy trong môi trường bảo mật AppContainer cô lập, nghiêm cấm việc tùy tiện tạo cửa sổ Win32, gây xung đột và crash.

### 5.3. Giải pháp triệt để & Kiến trúc an toàn 100%
1. **Gỡ bỏ hoàn toàn VTable slot tự chế và `GetGlobalCompartment`:**
   - Xóa bỏ toàn bộ `TfThreadMgrVTable`, `SetGlobalOpenClose` và `SetGlobalConversionMode`.
   - Trở lại đúng 100% triết lý thiết kế của Google Japanese Input (Mozc): Mozc không bao giờ gọi `GetGlobalCompartment` trên ThreadMgr. Việc thao tác compartment được thực hiện an toàn qua `QueryInterface(IID_ITfCompartmentMgr)` trên chính `pThreadMgr` và `pContext`.
2. **Cô lập tạo Message-Only Window Win32:**
   - Chuyển lệnh gọi `EnsureMsgWndCreated()` từ `Register()` sang hàm `AdviseSink()`.
   - **Đặc tính kỹ thuật:** `AdviseSink()` chỉ duy nhất được gọi bởi `explorer.exe` (khi Taskbar gắn kết Sink theo dõi Language Bar). Tất cả các tiến trình khác (Edge, Chrome, Word, đặc biệt là `SearchHost.exe` trong AppContainer) **hoàn toàn không bao giờ tạo window Win32 này**.
   - Thêm điều kiện kiểm tra sớm (early exit) trong `NotifyStateChanged()`: nếu `_pLangBarSink == IntPtr.Zero` thì lập tức thoát, không thực hiện bất kỳ lệnh Win32 nào.
3. **Bảo vệ ranh giới NativeAOT:**
   - Bọc toàn bộ phần thân `MsgWndProc` bằng khối `try-catch (Exception ex)` để đảm bảo không một ngoại lệ C# nào có thể thoát ra môi trường unmanaged của Windows (tránh mã lỗi `0xc000041d`).

### 5.4. Kết quả nghiệm thu thực tế
- **Độ ổn định:** Hoàn toàn chấm dứt tình trạng crash `msctf.dll`, `SearchHost.exe`, `explorer.exe`. Hệ điều hành hoạt động trơn tru 100%.
- **Trải nghiệm gõ & chuyển chế độ:** Phím tắt cố định dưới nút Esc (`~` trên US, Hankaku/Zenkaku trên JP) phản hồi tức thì, không độ trễ, không kẹt phím.
- **Biểu tượng Taskbar:** Nhận diện và cập nhật biểu tượng E/V tức thì sau mỗi lần nhấn phím tắt.
- **Kiểm thử tự động:** Toàn bộ 326 unit tests (`BambooMintKey.Core.Tests` và `BambooMintKey.NativeBridge.Tests`) vượt qua thành công 100%.
- **Bản phân phối:** File cài đặt đã được đóng gói thành công: `D:\Kojin\BambooMintKey\bin\dist\BambooMintKey-Setup.exe` (12.37 MB).


