<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue : Lỗi trạng thái V/E không đồng bộ giữa icon và kiểu gõ thực tế

**Mã tài liệu:** `004_EV_IconError`

**Trạng thái:** 🔍 Đang điều tra / Đã có thiết kế fix

**Design fix:** `docs/2.Design/Phase5/005_06_Global_VE_State_Synchronization.md`

---

## 1. Mô tả lỗi

Thanh taskbar / system tray / language bar hiển thị icon **V** (Vietnamese) hoặc **E** (English) để báo trạng thái gõ tiếng Việt/tiếng Anh của BambooMintKey.

Tuy nhiên, đôi khi xảy ra tình trạng **icon không khớp với kiểu gõ thực tế**:

| Icon hiển thị | Kết quả khi gõ | Mô tả |
|---|---|---|
| **E** | Vẫn gõ được tiếng Việt (`tôi` → `tôi`) | Icon báo E nhưng engine đang ở chế độ V. |
| **V** | Vẫn gõ tiếng Anh thuần (`tôi` → `tôi`) | Icon báo V nhưng engine đang ở chế độ E. |
| **V/E** | Bấm phím tắt hoặc click icon không đổi được trạng thái | Trạng thái bị "kẹt" ở một giá trị. |

> **Yêu cầu bắt buộc sau khi sửa:** Icon E/V phải nhất quán 100% với khả năng gõ tiếng Việt. Nếu icon là V, người dùng phải gõ được tiếng Việt. Nếu icon là E, engine phải trả về ký tự thô.

Lỗi **không xảy ra 100%**, thường xuất hiện sau một thời gian sử dụng, hoặc khi chuyển đổi giữa các ứng dụng, hoặc sau khi bật/tắt máy tính từ sleep/hibernate.

## 2. Giả thuyết nguyên nhân

| # | Giả thuyết | Xác suất | Giải thích |
|---|---|---|---|
| 1 | **Shared Memory / Compartment không đồng bộ** | Cao | Trạng thái V/E được lưu ở nhiều nơi: `SharedMemoryManager` (byte offset 0), TSF Input Mode Compartment (`GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`), và biến nội bộ của icon helper. Nếu một trong các giá trị này bị lệch, icon và engine sẽ khác nhau. |
| 2 | **Icon helper không nhận sự kiện cập nhật** | Cao | Khi trạng thái V/E thay đổi (qua phím tắt, click icon, hoặc từ ứng dụng khác), icon helper có thể không được notify, hoặc callback update icon bị bỏ qua do lỗi COM refcount / GCHandle. |
| 3 | **Phím tắt chuyển V/E xung đột với TSF preserved key** | Trung bình | Nếu `OnPreservedKey` và `OnKeyDown` cùng xử lý phím tắt, có thể toggle 2 lần hoặc không toggle, dẫn đến trạng thái hiển thị sai. |
| 4 | **Multiple TSF server instances load cùng lúc** | Trung bình | Mỗi process target (VS Code, Chrome, Word...) load một instance `BambooMintKeyTextService.dll` riêng. Nếu đồng bộ shared memory chậm hoặc event broadcast mất, các process có thể thấy trạng thái khác nhau. |
| 5 | **Icon cache của Windows / Explorer** | Thấp | Windows taskbar có thể cache icon bitmap; nhưng thường sẽ tự refresh khi `NotifyStateChanged` được gọi. |

## 3. Môi trường

- **Ứng dụng gặp lỗi:** Thanh taskbar / system tray icon; ảnh hưởng đến mọi ứng dụng đang gõ.
- **Bố cục bàn phím trong Windows:** (cần xác nhận)
- **Phiên bản BambooMintKey:** build sau commit `fa1a2e2`
- **Phiên bản Windows:** (cần xác nhận)

## 4. Các bước tái hiện

1. Khởi động BambooMintKey.
2. Mở Notepad, chuyển sang BambooMintKey.
3. Gõ `tooi` + space → kết quả `tôi` (chế độ V, icon hiển thị **V**).
4. Nhấn phím tắt chuyển E (mặc định `Ctrl+Shift` hoặc tùy chỉnh).
5. Quan sát icon chuyển sang **E**.
6. Gõ `tooi` + space → kết quả mong đợi: `tooi` (chế độ E).
7. **Lỗi:** icon là **E** nhưng kết quả vẫn là `tôi`, hoặc ngược lại.

Các tình huống cần thử:
- Chuyển V/E bằng **phím tắt**.
- Chuyển V/E bằng **click icon trên taskbar/language bar**.
- Chuyển V/E từ **UI cấu hình**.
- Để máy sleep/hibernate rồi wake up, thử lại.
- Chuyển qua lại giữa nhiều ứng dụng (Notepad → Chrome → VS Code).

## 5. Phương án debug kiểm chứng nguyên nhân

### 5.1. Bật log toàn hệ thống

Set environment variable:

```powershell
[Environment]::SetEnvironmentVariable('BAMBOOMINTKEY_DEBUG', '1', 'User')
```

Sau đó **đăng nhập lại Windows** (hoặc restart `explorer.exe`) để log bắt đầu ghi.

Log file: `%TEMP%\BambooMintKey_Runtime.log`

Tìm các dòng quan trọng:
- `StateWatcher: Sự kiện cấu hình thay đổi đã được kích hoạt!`
- `SetConversionMode: mode=...`
- `OnClick: ...`
- `OnPreservedKey: ...`
- `ToggleVietnameseMode: ...`
- `UpdateIcon: ...`

### 5.2. Kiểm tra Shared Memory offset 0

Mở PowerShell:

```powershell
$map = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('BambooMintKey_SharedConfig_v1')
$view = $map.CreateViewAccessor(0, 64)
$bytes = New-Object byte[] 64
$view.ReadArray(0, $bytes, 0, 64)
Write-Host "IsVietnameseMode (offset 0) = $($bytes[0])"
$view.Dispose()
$map.Dispose()
```

- `1` = Vietnamese mode (V)
- `0` = English mode (E)

So sánh giá trị này với icon đang hiển thị. Nếu khác nhau → lỗi ở icon helper. Nếu giống nhau nhưng gõ vẫn sai → lỗi ở engine đọc sai shared memory.

### 5.3. Kiểm tra TSF Input Mode Compartment

TSF cung cấp compartment `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` để ứng dụng và taskbar biết trạng thái V/E. Có thể kiểm tra bằng COM API hoặc log.

Trong log tìm:
- `TsfCompartmentHelper.SetConversionMode(...)` — xem mode được set là gì.
- `TsfCompartmentHelper.GetConversionMode(...)` — xem mode đọc lại là gì.

Nếu `Set` ≠ `Get` → compartment bị override bởi TSF hoặc ứng dụng khác.

### 5.4. Kiểm tra icon helper notify path

Tìm trong log:
- `LangBarItemButton.NotifyStateChanged` — có được gọi không?
- `GetIconBitmap` / `UpdateIcon` — có tạo bitmap mới không?
- Nếu `NotifyStateChanged` không được gọi khi toggle → lỗi ở `StateWatcher` hoặc `SharedMemoryManager.SignalStateChanged`.

### 5.5. Kiểm tra nhiều process load DLL

Mở PowerShell với quyền admin:

```powershell
Get-Process | Where-Object { $_.Modules.ModuleName -contains 'BambooMintKey.dll' } | Select-Object Name, Id, Path
```

Kiểm tra xem các process (`Code.exe`, `chrome.exe`, `explorer.exe`, `ctfmon.exe`) có load cùng một đường dẫn DLL không. Nếu khác nhau → có thể cài đặt cũ/mới lẫn lộn.

### 5.6. Tạm thời hardcode log vào các điểm quan trọng

Nếu log hiện tại chưa đủ, thêm log vào các hàm sau (để debug):
- `SharedMemoryManager.IsVietnameseMode` getter/setter.
- `TsfCompartmentHelper.SetConversionMode`.
- `LangBarItemButton.NotifyStateChanged`.
- `LangBarItemButton.GetIconBitmap`.
- `KeyEventSinkImpl.OnPreservedKey`.
- `KeyEventSinkImpl.OnKeyDown` (khi nhận diện phím tắt V/E).
- `BambooMintKeyTextService.ActivateEx` / `Deactivate`.

### 5.7. Dùng Process Monitor (ProcMon)

Nếu nghi ngờ registry bị ghi đè:
1. Mở **Process Monitor** (Sysinternals).
2. Filter: `Process Name contains BambooMintKey` OR `Path contains SOFTWARE\Microsoft\CTF`.
3. Toggle V/E và quan sát các write/read vào registry/compartment.

## 6. Các điểm code cần kiểm tra

| File | Hàm/Class | Mục đích kiểm tra |
|---|---|---|
| `src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs` | `IsVietnameseMode` | Getter/setter có đọc/ghi đúng byte 0 không? |
| `src/BambooMintKey.NativeBridge/TSF/TsfCompartmentHelper.cs` | `SetConversionMode`, `GetConversionMode` | Compartment TSF có được cập nhật đúng không? |
| `src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs` | `NotifyStateChanged`, `GetIconBitmap` | Icon có được vẽ lại khi trạng thái đổi không? |
| `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs` | `StartStateWatcher` | Watcher có lắng nghe event shared memory không? |
| `src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs` | `OnPreservedKey`, `OnKeyDown` | Phím tắt V/E có xử lý đúng, không double-toggle không? |
| `src/BambooMintKey.UI/MainWindow.axaml.fs` | Hotkey recording / toggle | UI có ghi nhận phím tắt đúng vào shared memory không? |

## 7. Action Items

- [ ] Thu thập log từ máy bị lỗi với `BAMBOOMINTKEY_DEBUG=1`.
- [ ] Kiểm tra shared memory byte 0 khi icon và kết quả gõ không khớp.
- [ ] Kiểm tra compartment TSF có đồng bộ với shared memory không.
- [ ] Xác nhận `NotifyStateChanged` được gọi sau mỗi lần toggle.
- [ ] Xác nhận không có double-toggle trong `OnPreservedKey` + `OnKeyDown`.
- [ ] Test trên nhiều ứng dụng (Notepad, Word, Chrome, VS Code, Edge).
- [ ] Test sau sleep/hibernate.
- [ ] Triển khai fix đồng bộ: single source of truth cho V/E state.

## 8. Phương án fix chính thức

Xem chi tiết tại `docs/2.Design/Phase5/005_06_Global_VE_State_Synchronization.md`.

Tóm tắt:
1. Tạo `GlobalVEState` làm API trung tâm duy nhất để đọc/ghi trạng thái V/E.
2. Shared memory offset 0 là single source of truth.
3. `ToggleVietnameseMode` dùng `Interlocked.CompareExchange` để tránh race condition.
4. Khi có tranh chấp, abort toggle và resync icon/compartment theo state thực tế.
5. Tất cả code path (click icon, menu, phím tắt, UI config) đều đi qua `GlobalVEState`.
6. `BridgeStateManager.Config` luôn đọc state mới nhất, không cache stale.
7. `StateWatcher` resync compartment khi nhận event từ process khác.

## 9. Action Items

- [ ] Triển khai `GlobalVEState.cs`.
- [ ] Sửa `SharedMemoryManager.ToggleVietnameseMode` thành atomic.
- [ ] Cập nhật `BridgeStateManager` dùng `GlobalVEState`.
- [ ] Cập nhật `LangBarItemButton.OnClick` / `ExecuteMenuCommand`.
- [ ] Cập nhật `KeyEventSinkImpl.OnKeyDown` / `OnPreservedKey`.
- [ ] Cập nhật `BambooMintKeyTextService.StateWatcher` để resync compartment.
- [ ] Kiểm tra UI config gọi đúng API.
- [ ] Test race condition và xác nhận icon luôn nhất quán với khả năng gõ.

## 10. Thông tin thêm

- Lỗi có xảy ra sau khi bấm phím tắt nhanh nhiều lần liên tiếp không?
- Lỗi có xảy ra khi dùng phím tắt tùy chỉnh (ví dụ `Alt+Z`) không?
- Lỗi có xảy ra khi chuyển đổi bằng click icon trên language bar không?

---

## Lưu ý khi báo lỗi

| Trường | Tại sao cần |
|--------|-------------|
| **Chuỗi phím đã gõ** | Để lập trình viên tái hiện chính xác. |
| **Icon hiển thị vs kết quả gõ** | Xác định lỗi là ở UI icon hay engine state. |
| **Cách chuyển V/E** | Phím tắt / click icon / UI config có thể có code path khác nhau. |
| **Ứng dụng** | Một số lỗi chỉ xảy ra trong ứng dụng nhất định do tích hợp TSF. |
| **Phiên bản Windows** | Các phiên bản Windows xử lý TSF/Language Bar khác nhau. |
| **Log `BambooMintKey_Runtime.log`** | Dữ liệu quan trọng nhất để debug. |
