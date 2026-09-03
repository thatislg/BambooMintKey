<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 002_06 — Vấn Đề Kỹ Thuật: TSF Gõ Tiếng Việt Không Hiển Thị Text (ĐÃ GIẢI QUYẾT)

> Tài liệu ghi nhận lỗi và kết quả điều tra, khắc phục triệt để vấn đề: BambooMintKey đã đăng ký TSF thành công, bắt phím được, nhưng văn bản tiếng Việt không xuất hiện trong ứng dụng đích (Notepad, v.v.).
> **Trạng thái hiện tại:** ✅ **ĐÃ GIẢI QUYẾT HOÀN TOÀN** — Bộ gõ đã xuất hiện trong danh sách, bắt phím chuẩn, tạo phiên composition và hiển thị văn bản tiếng Việt có dấu chính xác vào ứng dụng.

---

## 1. Tóm Tắt Trạng Thái Sau Khắc Phục

| Hạng mục | Trạng thái trước | Trạng thái hiện tại | Ghi chú giải pháp |
|----------|------------------|---------------------|-------------------|
| Build & publish NativeAOT Shared DLL | ✅ OK | ✅ OK | `publish/win-x64/BambooMintKey.dll` 3.8 MB. |
| COM registration (`DllRegisterServer`) | ✅ OK | ✅ OK | Đăng ký CLSID, Profile, Categories đầy đủ. |
| Hiển thị trong language list | ✅ OK | ✅ OK | Hiển thị đúng `042A:{CLSID}{ProfileGuid}`. |
| Thêm/bỏ trong Settings & HKCU | ⚠️ Bất ổn | ✅ Ổn định | Đã đồng bộ qua `enable-tip.ps1` và `ctfmon.exe`. |
| Key event sink được advise | ✅ OK | ✅ OK | `AdviseKeyEventSink` thành công (`cookie=1`). |
| ThreadMgrEventSink advise | ❌ cookie=0 | ✅ OK | Đã sửa `IidITfSource` và cơ chế QueryInterface. |
| `OnKeyDown` nhận phím | ✅ OK | ✅ OK | Bắt đúng Virtual Key và chuyển đổi Telex engine. |
| Edit session callback chạy | ✅ OK | ✅ OK | `ExecuteSession` chạy đồng bộ `TF_ES_SYNC`. |
| `StartComposition` | ❌ FAIL (`hr=0x80004005`) | ✅ **SUCCESS** | Đã sửa đúng chuẩn `IID_ITfContextComposition` & `IID_ITfCompositionSink`. |
| Văn bản xuất hiện trong Notepad | ❌ Không hiện | ✅ **HIỂN THỊ CHUẨN** | Sửa đúng vtable `ITfRange::SetText` và `ITfContext::GetSelection`. |
| Phím Space (Ngắt từ) | ❌ Bị nuốt/bỏ qua | ✅ **HOẠT ĐỘNG CHUẨN** | Khi có composition, Space gọi `ProcessWordBreak` và commit từ thành công. |

---

## 2. Mô Tả Chi Tiết Hiện Tượng Ban Đầu

### 2.1. Registry & Language List
- `HKLM\SOFTWARE\Microsoft\CTF\TIP\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}` tồn tại, có tên `BambooMintKey Vietnamese Input`.
- Khi gõ phím, `OnKeyDown` và F# Core Engine xử lý đúng logic Telex (ví dụ gõ `T`, `o`, `o`, `i` -> `Tôi`), tuy nhiên văn bản không hiện ra trên Notepad.

### 2.2. Runtime Log Trước Khi Sửa
```text
[11:25:29.405] OnKeyDown ProcessKey char=T, text=T
[11:25:29.405] RequestEdit: action=UpdateText, text=T, pContext=...
[11:25:29.405] ExecuteSession ec=..., action=UpdateText, text=T
[11:25:29.405] PerformUpdateText ec=..., text=T
[11:25:29.405] StartComposition result=False
[11:25:29.405] RequestEditSession HR=0x00000000, hrSession=0x80004005
```
- `StartComposition` luôn trả về `false`.
- `PerformUpdateText` trả về `E_FAIL` (`0x80004005`), khiến Edit Session báo lỗi và không chèn được ký tự nào vào document.

---

## 3. Nguyên Nhân Gốc (Root Causes)

Sau khi đối chiếu chi tiết từng byte vtable và IID với bộ tệp định nghĩa chính thức của Windows SDK 10 (`msctf.h` và `msctf.idl`), đã phát hiện **7 lỗi cốt lõi**:

### 3.1. `IID_ITfContextComposition` sai GUID hoàn toàn
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/CompositionManager.cs`
- **Trước đó:** `D40C8A3B-DA93-4B21-9E58-53E7135B47F0` (GUID bị gõ sai/hallucinated).
- **Windows SDK chuẩn:** `uuid(D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B)`.
- **Hậu quả:** `QueryInterface(ITfContextComposition)` trên `pContext` luôn trả về `E_NOINTERFACE (0x80004002)`. `StartComposition` lập tức return `false`.

### 3.2. `IID_ITfCompositionSink` sai GUID
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/CompositionSinkImpl.cs`
- **Trước đó:** `3D61BF11-ACFF-428F-A89F-9E59C70C1E1F`.
- **Windows SDK chuẩn:** `uuid(A781718C-579A-4B15-A280-32B8577ACC5E)`.
- **Hậu quả:** TSF callback truy vấn sink bằng IID chuẩn sẽ bị từ chối với `E_NOINTERFACE`.

### 3.3. `TfCompositionVTable` thiếu method (Lệch VTable)
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/ITfComposition.cs`
- **Trước đó:** Vtable chỉ có `GetRange` (Slot 3) và `EndComposition` (Slot 4).
- **Windows SDK chuẩn:**
  - Slot 3: `GetRange`
  - Slot 4: `ShiftStart`
  - Slot 5: `ShiftEnd`
  - Slot 6: `EndComposition`
- **Hậu quả:** Khi gọi `EndComposition` ở Slot 4, thực tế đang gọi vào `ShiftStart` với đối số sai, làm hỏng state của composition.

### 3.4. `TfRangeVTable` lệch toàn bộ Slot từ Slot 6 trở đi
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/ITfComposition.cs`
- **Trước đó:** Đặt `Collapse` ở Slot 6, `ShiftStart` ở Slot 7...
- **Windows SDK chuẩn:** `GetEmbedded` ở Slot 6, `InsertEmbedded` ở Slot 7... và `Collapse` nằm ở tận **Slot 15**.
- **Hậu quả:** Khi `TsfSelectionHelper.SetSelectionToEnd` gọi `Collapse`, nó thực tế gọi vào `GetEmbedded`, gây sai lệch hành vi con trỏ hoặc làm crash tiến trình ngầm.

### 3.5. `TfContextVTable` sai tham số `GetSelection` và sai Slot `GetProperty`
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/TsfSelectionHelper.cs`
- **Lỗi 1 (Signature):** `GetSelection` trong SDK có 5 tham số sau `this`:
  `HRESULT GetSelection(TfEditCookie ec, ULONG ulIndex, ULONG ulCount, TF_SELECTION *pSelection, ULONG *pcFetched);`
  Code cũ thiếu tham số `ulCount`, khiến con trỏ `selection` bị truyền vào `ulCount`, gây đọc rác trên stack.
- **Lỗi 2 (Slot order):** `GetProperty` nằm ở Slot 12 trong SDK, nhưng code cũ đặt ở Slot 11 (trùng vào vị trí của `GetStatus`).

### 3.6. `TsfEventSinkHelper.AdviseSink` sai IID và sai đối số QueryInterface
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/TsfEventSinkHelper.cs`
- **Lỗi:** Code dùng `IidITfSource` sai (`...446F-8BC6-0B0B6E49E0C0` thay vì `...446F-8FD6-E6A8D82459F7`) và truyền nhầm con trỏ IID của Sink vào hàm `QueryInterface` của `pSource`.
- **Hậu quả:** `Advise ThreadMgrEventSink` luôn trả về cookie = 0.

### 3.7. Lỗi khóa file ghi log (`DebugLog`)
- **Vị trí:** `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs`
- **Lỗi:** Mở lại `FileStream(FileAccess.Write)` mà không bật cờ `FileShare.ReadWrite`, dẫn tới khi có nhiều tiến trình cùng nạp TIP (ctfmon, Notepad, Settings), file log bị xung đột khóa, làm treo hoặc mất log trong COM callbacks.

---

## 4. Các Thay Đổi Đã Thực Hiện

### 4.1. Bổ sung IID chuẩn trong `Guids.cs`
```csharp
public static readonly Guid IidITfContextComposition = new("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B");
public static readonly Guid IidITfComposition = new("20168D64-5A8F-4A5A-B7BD-CFA29F4D0FD9");
public static readonly Guid IidITfCompositionSink = new("A781718C-579A-4B15-A280-32B8577ACC5E");
public static readonly Guid IidITfSource = new("4EA48A35-60AE-446F-8FD6-E6A8D82459F7");
```

### 4.2. Khắc phục VTable `ITfComposition` và `ITfRange`
- Thêm `ShiftStart` và `ShiftEnd` vào `TfCompositionVTable`, đưa `EndComposition` về đúng Slot 6.
- Chuẩn hóa toàn bộ 25 slots của `TfRangeVTable` theo đúng `msctf.h`.

### 4.3. Sửa `TfContextVTable` và lời gọi `GetSelection`
- Bổ sung `ulCount` cho `GetSelection`.
- Sắp xếp chuẩn các slots (`GetProperty` ở Slot 12, `GetDocumentMgr` ở Slot 16).
- Sửa hàm gọi: `contextVTable->GetSelection(pContext, ec, 0, 1, &selection, &fetched)`.

### 4.4. Sửa `TsfEventSinkHelper.cs`
- Truy vấn `Guids.IidITfSource` trước khi gọi `sourceVTable->AdviseSink`.

### 4.5. Nâng cấp `DebugLog`
- Chuyển sang sử dụng `FileShare.ReadWrite` và cơ chế đồng bộ thread-safe an toàn cho đa tiến trình.

---

## 5. Xác Minh & Kết Quả Thực Tế

1. **Unit Tests F# Core:**
   - Đã chạy `dotnet test tests/BambooMintKey.Core.Tests`.
   - Kết quả: **119/119 test cases Passed (100%)**.
2. **Biên dịch NativeAOT:**
   - Chạy `build-native.ps1` thành công, tạo `publish/win-x64/BambooMintKey.dll` (3.8 MB).
   - Quá trình build diễn ra trực tiếp, không gặp lỗi lock file.
3. **Thử nghiệm gõ thực tế trên Windows:**
   - Chọn bộ gõ **BambooMintKey Vietnamese Input** qua `Win + Space`.
   - Gõ kiểm tra: `Tôi đang gõ thử tiếng Việt`.
   - **Kết quả:** Ký tự hiển thị tức thì, đúng dấu thanh, phím Space ngắt từ chuẩn xác, không bị dính chữ hay mất ký tự.

---

## 6. Trạng Thái Cuối Cùng

> **PHASE 2 CHÍNH THỨC HOÀN THÀNH.**  
> Vấn đề kỹ thuật 002_06 đã được xử lý triệt để. Hệ thống TSF Native Bridge đã kết nối hoàn hảo với F# Pure Telex Engine và hoạt động ổn định trên các ứng dụng đích. Sẵn sàng chuyển giao sang Phase 3 (UI / Settings / Display Attribute Provider).
