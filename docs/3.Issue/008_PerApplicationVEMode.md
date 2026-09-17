<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue : Trạng thái V/E không đồng nhất giữa các ứng dụng

**Mã tài liệu:** `008_PerApplicationVEMode`

**Trạng thái:** 🛠️ Đã implement fix — chờ build/test xác nhận

**Mức độ nghiêm trọng:** Cao — người dùng mong muốn global mode nhưng Windows TSF/ứng dụng lại nhớ mode per-app.

---

## 1. Mô tả lỗi / yêu cầu

Người dùng báo cáo BambooMintKey **chưa có tính năng share kiểu gõ (V/E) cho tất cả các ứng dụng**, hoặc **đặt giống nhau toàn bộ cho các ứng dụng khác nhau**.

Tuy nhiên, trong thực tế lại quan sát thấy:
- Ứng dụng A đang ở chế độ **V** (gõ tiếng Việt).
- Chuyển sang ứng dụng B, bộ gõ lại ở chế độ **E** (tiếng Anh).
- Quay lại ứng dụng A, bộ gõ vẫn nhớ A đang ở **V**.

Điều này cho thấy có cơ chế **per-application mode** đang hoạt động, có thể do Windows TSF hoặc ứng dụng tự lưu trạng thái.

## 2. Mong đợi của người dùng

Người dùng muốn một trong hai hành vi:

| Tùy chọn | Mô tả |
|---|---|
| **A. Global Mode (khuyến nghị mặc định)** | Khi đổi V/E ở bất kỳ đâu, tất cả ứng dụng đều chuyển theo. Không có khái niệm "mode riêng cho app". |
| **B. Per-Application Mode** | Mỗi ứng dụng nhớ mode riêng. Tùy chọn này có thể một số người thích nhưng phức tạp hơn. |

Yêu cầu hiện tại: **triển khai Option A làm mặc định**, và có thể cho phép người dùng chọn B trong tương lai.

## 3. Nguyên nhân kỹ thuật

### 3.1. TSF Thread / Document Compartment

Windows TSF có nhiều loại compartment:
- **Thread compartment**: `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` trong thread manager.
- **Document compartment**: có thể khác nhau giữa các document trong cùng thread.

Nếu BambooMintKey đang dùng **thread compartment**, mỗi process/thread có thể có giá trị riêng. Windows cũng có thể nhớ giá trị per-thread khi focus chuyển.

### 3.2. Ứng dụng tự lưu mode

Một số ứng dụng (đặc biệt Microsoft Office, Edge, Chrome) có thể lưu lại input mode của chính mình và set lại compartment khi nhận focus.

### 3.3. `ITfThreadMgrEventSink.OnSetFocus`

Khi focus chuyển sang document/app khác, TSF gọi `OnSetFocus`. Hiện tại BambooMintKey có thể chưa xử lý event này để **ép buộc đồng bộ global state** vào compartment mới.

### 3.4. Shared Memory là global, nhưng Compartment là local

`SharedMemoryManager` lưu global state, nhưng mỗi process phải tự đồng bộ compartment local của mình. Nếu khi focus chuyển, process mới không resync, nó sẽ dùng compartment cũ của chính thread đó.

## 4. Cách kiểm chứng

### 4.1. Bật log và quan sát

```powershell
[Environment]::SetEnvironmentVariable('BAMBOOMINTKEY_DEBUG', '1', 'User')
```

Sau đó:
1. Mở Notepad, chuyển sang V, gõ vài từ tiếng Việt.
2. Mở Word, chuyển sang E, gõ vài từ tiếng Anh.
3. Quay lại Notepad.
4. Mở `%TEMP%\BambooMintKey_Runtime.log`, tìm:
   - `OnSetFocus` có được gọi không.
   - `ResyncFromSharedMemory` có được gọi khi focus chuyển không.
   - `SetConversionMode` được gọi với giá trị gì.

### 4.2. Kiểm tra registry TSF per-thread

Mở regedit, tìm:
```
HKEY_CURRENT_USER\Software\Microsoft\CTF\Thread\...
HKEY_CURRENT_USER\Software\Microsoft\CTF\Assemblies\...
```
Xem có dữ liệu nào lưu mode per-thread không.

### 4.3. Test với các ứng dụng khác nhau

| Ứng dụng | Hành vi quan sát | Ghi chú |
|---|---|---|
| Notepad | | |
| Notepad++ | | |
| VS Code | | |
| Chrome/Edge | | |
| Microsoft Word | | |
| Excel | | |

## 5. Phương án fix

### 5.1. Ép global mode trong `OnSetFocus` (Đã implement)

Mỗi khi focus chuyển sang document mới, bộ gõ phải:
1. Đọc `GlobalVEState.IsVietnameseMode`.
2. Gọi `TsfCompartmentHelper.SetConversionMode(pThreadMgr, clientId, globalState)`.
3. Gọi `LangBarItemButton.NotifyStateChanged()`.

Code đã sửa trong `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs`:

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnSetFocus(IntPtr thisPtr, IntPtr pdimFocus, IntPtr pdimPrevFocus)
{
    var rootPtr = thisPtr - sizeof(IntPtr);
    var target = GetTarget(rootPtr);

    // Khi chuyển sang ô nhập liệu khác -> Chốt từ đang gõ dở và làm sạch State
    CompositionManager.EndComposition();
    BridgeStateManager.ResetState();

    // Ép buộc đồng bộ global V/E state vào process/thread hiện tại.
    if (target._pThreadMgr != IntPtr.Zero)
    {
        GlobalVEState.ResyncFromSharedMemory(target._pThreadMgr, target._clientId);
    }

    return HResult.Ok;
}
```

### 5.2. Tắt per-thread persistence của TSF

Có thể cần cấu hình registry hoặc gọi TSF API để không cho Windows lưu mode per-thread/document. Tuy nhiên, điều này có thể không được TSF hỗ trợ trực tiếp.

### 5.3. Theo dõi compartment change từ bên ngoài

Đăng ký `ITfCompartmentEventSink` để khi Windows/ứng dụng tự đổi compartment, bộ gõ đồng bộ ngược về shared memory và notify các process khác.

## 6. Câu hỏi cần trả lời

1. Người dùng thực sự muốn **global mode** hay **per-app mode**?
2. Hành vi hiện tại là do TSF lưu per-thread hay do ứng dụng (Office/Chrome) tự lưu?
3. Có cần tùy chọn trong UI để chuyển giữa global và per-app không?

## 7. Action Items

- [x] Thu thập log từ máy người dùng khi chuyển focus giữa các ứng dụng.
- [x] Xác định `OnSetFocus` có được gọi và resync có xảy ra không.
- [x] Triển khai `OnSetFocus` resync global state.
- [ ] Build installer mới và test chuyển focus giữa Notepad / Word / Chrome.
- [ ] Điều tra khả năng tắt TSF per-thread mode persistence nếu fix trên chưa đủ.
- [ ] Cân nhắc thêm tùy chọn "Global V/E mode" trong UI nếu cần hỗ trợ per-app mode sau này.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **Các ứng dụng test** | Xác định ứng dụng nào có behavior khác biệt. |
| **Log `BambooMintKey_Runtime.log`** | Thấy rõ `OnSetFocus` và `SetConversionMode` flow. |
| **Windows version** | Windows 10/11 xử lý TSF compartments khác nhau. |
| **Office version** | Office có cơ chế lưu input mode riêng. |
