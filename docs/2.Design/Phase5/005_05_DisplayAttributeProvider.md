<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Chi Tiết: TSF DisplayAttributeProvider - Cơ Chế Ẩn Gạch Chân Preedit Vô Hình (Stealth Seamless Composition)

**Mã tài liệu:** `005_05`  
**Tài liệu:** `docs/2.Design/Phase5/005_05_DisplayAttributeProvider.md`  
**Giai đoạn:** Phase 5 - Cải Tiến Trải Nghiệm Gõ  
**Trạng thái:** ✅ Đã triển khai (với 2 GUID tách biệt: Stealth / Preedit)  
**Mục tiêu:** Loại bỏ hoàn toàn đường gạch chân / vệt chấm chấm preedit trên VS Code, Chrome, Edge, MS Word, v.v., mang lại trải nghiệm gõ tiếng Việt tự nhiên, sạch bóng y hệt Notepad++.

---

## 1. Hiện Trạng & Bài Toán Kỹ Thuật (Problem Statement)

### 1.1. Sự khác biệt giữa Notepad++ và VS Code / Chromium

Người dùng nhận thấy sự khác biệt về hiển thị khi gõ tiếng Việt trên các ứng dụng khác nhau:

- **Trên Notepad++:** Khi gõ từ (ví dụ `đang`), các ký tự hiển thị trơn tru, chữ đen nền trắng bình thường, **không có bất kỳ đường gạch chân hay vệt chấm chấm nào bên dưới**. Trải nghiệm rất sạch sẽ và thích mắt.
- **Trên VS Code (và các ứng dụng nền Chromium / Electron như Chrome, Edge, Cursor, Slack... cũng như Microsoft Word):** Khi đang gõ một từ chưa bấm phím cách (chưa `EndComposition`), **dưới từ luôn xuất hiện một đường gạch chân (nét đứt hoặc nét liền)**. Chỉ khi bấm phím cách hoặc dấu câu, đường gạch chân mới biến mất.

### 1.2. Cội rễ kỹ thuật

1. **Notepad++** sử dụng engine **Scintilla**. Scintilla không tự động vẽ style gạch chân cho TSF composition trừ khi bộ gõ chỉ định thuộc tính hiển thị (Display Attribute) bắt buộc phải vẽ.
2. **VS Code / Chromium / Word** tuân thủ nghiêm ngặt tiêu chuẩn IME quốc tế:
   - Khi BambooMintKey gọi `StartComposition`, Chromium đánh dấu vùng văn bản này là **Preedit String**.
   - Chromium truy vấn thuộc tính hiển thị qua property `GUID_PROP_ATTRIBUTE` (`{34B45670-7526-11D2-A147-00105A2799B5}`).
   - Khi BambooMintKey không cung cấp hoặc gán thuộc tính hiển thị với `TF_LS_NONE`, Chromium sẽ dùng cơ chế Fallback mặc định: tự vẽ đường gạch chân (dotted underline).

---

## 2. Mục Tiêu Thiết Kế (Design Objectives)

1. **Trải nghiệm gõ "vô hình" (Stealth Composition):** Triệt tiêu hoàn toàn đường gạch chân preedit trên VS Code, Chrome, Edge, Word, mang lại giao diện văn bản mượt mà, thuần khiết như Notepad++.
2. **Bảo tồn 100% ưu điểm của TSF:** Vẫn duy trì phiên `ITfComposition` in-line hoàn chỉnh (hỗ trợ xóa lùi Backspace không giật, hỗ trợ Free Tone Placement, không xung đột với IntelliSense hay lịch sử Undo `Ctrl+Z`).
3. **Tuân thủ chuẩn mực Microsoft TSF SDK:** Triển khai đầy đủ các COM interface chính thống:
   - `ITfDisplayAttributeProvider` (IID: `{FEEA5376-7086-455A-B054-0098AE450A55}`)
   - `ITfDisplayAttributeInfo` (IID: `{70528852-F6D2-43DE-8E5B-8F763CE537F1}`)
   - `IEnumTfDisplayAttributeInfo` (IID: `{7CE316AA-4934-453B-B538-0FB79966B150}`)
4. **Không ảnh hưởng lõi F# Core Engine:** Phạm vi thay đổi chỉ nằm ở tầng `BambooMintKey.NativeBridge` (C# NativeAOT).

---

## 3. Kiến Trúc & Luồng Hoạt Động (Architecture & Data Flow)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Ứng Dụng (VS Code / Word)                        │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ 1. Khi BambooMintKey gọi SetText()
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│        ITfContext.GetProperty(GUID_PROP_DISPLAYATTRIBUTE, &pProp)       │
│        pProp.SetValue(ec, pRange, varAtom)                             │
│        (DisplayAttributeHelper gán DisplayAttribute atom vào dải gõ)   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ 2. Ứng dụng hỏi Windows CategoryMgr
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│   CategoryMgr tìm Provider đăng ký trong GUID_TFCAT_DISPLAYATTRIBUTE   │
│   -> Gọi QueryInterface(IID_ITfDisplayAttributeProvider)               │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ 3. BambooMintKeyTextService trả về Provider
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│   BambooMintKeyDisplayAttributeInfo.GetAttributeInfo(&da)              │
│   - da.lsStyle = TF_LS_NONE (Không gạch chân)                          │
│   - da.crLine.type = TF_CT_NONE (Không màu đường kẻ)                   │
│   - da.crText.type = TF_CT_NONE (Không đổi màu chữ)                    │
│   - da.crBk.type = TF_CT_NONE (Không đổi màu nền)                      │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ 4. Kết quả
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│  VS Code / Chromium / Word nhận chỉ thị: KHÔNG VẼ GẠCH CHÂN!            │
│  -> Từ hiển thị trơn tru, sạch sẽ 100% như Notepad++!                  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Đặc Tả Chi Tiết Các Thành Phần Cần Cài Đặt (Detailed Specifications)

### 4.1. Khai báo GUIDs chuẩn trong `Guids.cs`

Trong `src/BambooMintKey.NativeBridge/Common/Guids.cs`:

```csharp
// Category GUIDs
public static readonly Guid GuidTfCategoryDisplayAttributeProvider = new("04862A28-7F03-4D6B-89EB-B738D56D1F9C");
public static readonly Guid GuidTfCategoryDisplayAttribute = new("0046D38B-49FD-4A23-8B39-44F48A6F00F4");

// Display Attribute Interface GUIDs (Đã sửa lại đúng chuẩn Microsoft TSF SDK)
public static readonly Guid IidITfDisplayAttributeProvider = new("fee47777-163c-4769-996a-6e9c50ad8f54");
public static readonly Guid IidITfDisplayAttributeInfo = new("70528852-2f26-4aea-8c96-215150578932");
public static readonly Guid IidIEnumTfDisplayAttributeInfo = new("7cef04d7-cb75-4e80-a7ab-5f5bc7d332de");

// GUID định danh Display Attribute riêng của BambooMintKey
public static readonly Guid GuidDisplayAttributeInput = new("5C02B94A-32EA-4B4B-A77A-92F2E4A9811C");

// GUID Preedit - hiển thị gạch chân nét đứt; tách biệt để tránh cache của TSF/Chromium
public static readonly Guid GuidDisplayAttributeInputPreedit = new("7A2F9E1B-5D43-4C88-A3B6-1E8D5F2C9A04");
```

---

### 4.2. Cấu trúc dữ liệu TSF Display Attribute (`TsfDisplayAttributeTypes.cs`)

Định nghĩa struct và enum theo chuẩn Windows SDK `msctf.h`:

```csharp
public enum TfDaColorType
{
    TfCtNone = 0,
    TfCtSysColor = 1,
    TfCtColorRef = 2
}

[StructLayout(LayoutKind.Sequential)]
public struct TfDaColor
{
    public TfDaColorType Type;
    public int IndexOrColorRef;
}

public enum TfDaLineStyle
{
    TfLsNone = 0,
    TfLsSolid = 1,
    TfLsDot = 2,
    TfLsDash = 3,
    TfLsSquiggle = 4
}

public enum TfDaAttrInfo
{
    TfAttrInput = 0,
    TfAttrTargetConverted = 1,
    TfAttrConverted = 2,
    TfAttrTargetNotConverted = 3,
    TfAttrInputError = 4,
    TfAttrFixedConverted = 5,
    TfAttrOther = -1
}

[StructLayout(LayoutKind.Sequential)]
public struct TfDisplayAttribute
{
    public TfDaColor CrText;
    public TfDaColor CrBk;
    public TfDaLineStyle LsStyle;
    public int FBoldLine;
    public TfDaColor CrLine;
    public TfDaAttrInfo BAttr;
}
```

---

Bây giờ, khi người dùng bật/tắt Preedit, atom được gán vào `pRange` sẽ khác nhau, buộc TSF/Chromium phải query lại display attribute thay vì dùng cache cũ.

> **Lưu ý:** Tài liệu này giả định `DisplayAttributeHelper` chỉ dùng 1 GUID `GuidDisplayAttributeInput` và đổi style động trong `GetAttributeInfo`. Tuy nhiên, trên thực tế nhiều ứng dụng cache display attribute theo GUID, khiến toggle Preedit không có hiệu lực ngay. Triển khai thực tế đã được cập nhật sang **2 GUID riêng biệt** (xem mục 4.1, 4.3, 4.4).

### 4.3. Lớp `DisplayAttributeInfoImpl.cs` (Triển khai `ITfDisplayAttributeInfo`)

Cung cấp **2 instance** với 2 GUID riêng biệt:

- **Stealth** (`GuidDisplayAttributeInput`):  
  - `LsStyle = TfDaLineStyle.TfLsNone` (Không đường kẻ).  
  - `CrLine.Type = TfDaColorType.TfCtNone`.  
  - `CrText.Type = TfDaColorType.TfCtNone` (Giữ nguyên màu chữ của trình soạn thảo).  
  - `CrBk.Type = TfDaColorType.TfCtNone` (Giữ nguyên màu nền).

- **Preedit** (`GuidDisplayAttributeInputPreedit`):  
  - `LsStyle = TfDaLineStyle.TfLsDot` (Gạch chân nét đứt chuẩn TSF).  
  - Các màu `TF_CT_NONE` để không đổi màu chữ/nền.

> **Quan trọng:** Không trả về `TfLsDot` / `TfLsNone` động từ một GUID duy nhất. Nhiều ứng dụng (đặc biệt Chromium) cache display attribute theo GUID, nên nếu dùng 1 GUID, việc toggle Preedit trong khi app đang chạy có thể không có hiệu lực cho đến khi khởi động lại ứng dụng. Dùng 2 GUID giải quyết vấn đề này.

---

### 4.4. Lớp `EnumDisplayAttributeInfoImpl.cs` (Triển khai `IEnumTfDisplayAttributeInfo`)

Cung cấp danh sách **2 thuộc tính**: `GuidDisplayAttributeInput` (Stealth) và `GuidDisplayAttributeInputPreedit` (Preedit).

---

### 4.5. Tích hợp `ITfDisplayAttributeProvider` vào `BambooMintKeyTextService.cs`

Trong `BambooMintKeyTextService`:

- Bổ sung vtable con trỏ `ITfDisplayAttributeProvider` vào `NativeLayout`.
- Xử lý `QueryInterface`: khi ứng dụng hỏi `IidITfDisplayAttributeProvider`, trả về con trỏ interface này.
- Cài đặt 2 phương thức:
  - `EnumDisplayAttributeInfo(IEnumTfDisplayAttributeInfo** ppEnum)` $\rightarrow$ Trả về instance `EnumDisplayAttributeInfoImpl` liệt kê cả Stealth và Preedit.
  - `GetDisplayAttributeInfo(Guid* pguid, ITfDisplayAttributeInfo** ppInfo)` $\rightarrow$ Trả về instance tương ứng:
    - `GuidDisplayAttributeInput` → Stealth.
    - `GuidDisplayAttributeInputPreedit` → Preedit.

---

### 4.6. Cập nhật `DisplayAttributeHelper.cs`

1. Khởi tạo và đăng ký **2 GUID** (`GuidDisplayAttributeInput` và `GuidDisplayAttributeInputPreedit`) với Windows `ITfCategoryMgr` để nhận được 2 mã số nguyên nhận diện duy nhất (`TfGuidAtom`).
2. Trong `ApplyCompositionAttribute`, đọc `SharedMemoryManager.EnablePreedit` để chọn atom hiện tại:
   - `EnablePreedit == false` → gán atom của Stealth (`TF_LS_NONE`).
   - `EnablePreedit == true`  → gán atom của Preedit (`TF_LS_DOT`).
3. Khi `TextEditSession` cập nhật văn bản composition (`PerformUpdateText`):
   ```csharp
   public static void ApplyCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
   {
       // Lấy ITfProperty của GUID_PROP_DISPLAYATTRIBUTE
       // Gán variant VT_I4 với giá trị atom của GuidDisplayAttributeInput vào dải pRange
   }
   ```
3. Khi `PerformCommitText` kết thúc từ:
   - Xóa bỏ thuộc tính hiển thị bằng `pProp->Clear(ec, pRange)`.

---

### 4.7. Đăng ký Category ID trong `ServerRegistrar.cs` / `TsfRegistration.cs`

1. Đăng ký TextService vào `GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER`.
2. Đăng ký cả 2 thuộc tính `GuidDisplayAttributeInput` (Stealth) và `GuidDisplayAttributeInputPreedit` (Preedit) vào `GUID_TFCAT_DISPLAYATTRIBUTE` gắn với `CLSID_BambooMintKeyTextService`.

---

### 4.8. Tùy Chọn Cấu Hình Preedit Trên Giao Diện UI & Đồng Bộ Shared Memory

1. **Vị trí trên giao diện:** Tab 2 ("Tùy chọn gõ") - Card "Tính năng thông minh":
   - Tùy chọn: `Bật tính năng hiển thị gạch chân khi soạn thảo (Preedit)` (mặc định: `false`).
2. **Đồng bộ liên tiến trình (Cross-Process Shared Memory):**
   - Lưu tại byte offset `21` trong Shared Memory `Local\BambooMintKey_SharedConfig_v1`.
   - Lưu bền vững trong `config.json`: `"enablePreedit": false | true`.
3. **Phản ứng động:** Không đổi style trong một GUID duy nhất. Thay vào đó:
   - Khi `EnablePreedit == false`: `ApplyCompositionAttribute` gán atom của `GuidDisplayAttributeInput` (Stealth / `TF_LS_NONE`).
   - Khi `EnablePreedit == true`: `ApplyCompositionAttribute` gán atom của `GuidDisplayAttributeInputPreedit` (Preedit / `TF_LS_DOT`).

---

### 4.9. Khắc Phục Chuẩn Thứ Tự VTable của `ITfCategoryMgr` Theo Windows SDK `msctf.h`

Để gọi hàm `RegisterGUID` chính xác, struct `TfCategoryMgrVTable` được chuẩn hóa 100% theo `msctf.h`:
- Slot 11: `RegisterGUIDDWORD`
- Slot 12: `UnregisterGUIDDWORD`
- Slot 13: `GetGUIDDWORD`
- Slot 14: `RegisterGUID(This, rguid, pguidatom)`
- Slot 15: `GetGUID`
- Slot 16: `IsEqualTfGuidAtom`

Nhờ đó, atom của `GuidDisplayAttributeInput` được đăng ký thành công và `GUID_PROP_DISPLAYATTRIBUTE` được áp dụng chính xác cho mọi ứng dụng TSF (VS Code, Word, Chrome,...).

---

## 6. Hướng Dẫn Điều Tra Khi Preedit Không Hoạt Động

Nếu người dùng đã bật Preedit trong UI nhưng vẫn không thấy gạch chân (hoặc luôn thấy gạch chân dù đã tắt), kiểm tra theo thứ tự sau.

### 6.1. Đảm bảo đã cài lại bản build mới

`BambooMintKey.dll` là TSF Text Service chạy bên trong các tiến trình ứng dụng. Nếu chỉ mở UI mới mà không cài lại DLL mới, các ứng dụng (VS Code, Chrome, Word, ...) vẫn đang load **DLL cũ**.

Cách kiểm tra:
- Mở Task Manager → Details, tìm các process đang gõ (ví dụ `Code.exe`, `chrome.exe`, `msedge.exe`, `WINWORD.exe`).
- Kiểm tra xem process đó có load `BambooMintKey.dll` từ folder cài đặt mới hay không (dùng Process Explorer hoặc `tasklist /m BambooMintKey.dll`).
- Nếu chưa, chạy lại `BambooMintKey-Setup.exe` để `regsvr32` đăng ký DLL mới. Nếu cần, restart ứng dụng sau khi cài.

> **Lưu ý quan trọng:** Nếu bạn đã xóa phần mềm cũ mà chưa cài lại, Windows vẫn có thể còn registry TSF trỏ đến đường dẫn DLL cũ (hoặc đường dẫn đã mất). Điều này khiến bộ gõ không hoạt động hoặc hoạt động sai. **Bắt buộc phải chạy installer mới** để registry được cập nhật đúng.

### 6.2. Kiểm tra `EnablePreedit` đã đến `SharedMemoryManager`

Mở PowerShell:
```powershell
$map = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('BambooMintKey_SharedConfig_v1')
$view = $map.CreateViewAccessor(0, 64)
$bytes = New-Object byte[] 64
$view.ReadArray(0, $bytes, 0, 64)
Write-Host "EnablePreedit (offset 21) = $($bytes[21])"
```

- `0` = Stealth (ẩn gạch chân).
- `1` = Preedit (hiện gạch chân).

Nếu byte này không đổi khi toggle checkbox, lỗi nằm ở UI/SharedConfig, không phải TSF.

### 6.3. Bật log runtime và đọc `BambooMintKey_Runtime.log`

Trước khi mở ứng dụng cần test, set environment variable:
```powershell
$env:BAMBOOMINTKEY_DEBUG='1'
# Rồi mới mở VS Code / Chrome / Word từ PowerShell này
```

Hoặc set toàn hệ thống tạm thời:
```powershell
[Environment]::SetEnvironmentVariable('BAMBOOMINTKEY_DEBUG', '1', 'User')
```

Log ghi vào `%TEMP%\BambooMintKey_Runtime.log`. Tìm các dòng:
- `ApplyCompositionAttribute SetValue HR=..., atom=...`
- `GetAttributeInfo: IsPreedit=..., LsStyle=...`
- `ITfDisplayAttributeProvider.GetDisplayAttributeInfo called: ...`
- `ITfDisplayAttributeProvider.EnumDisplayAttributeInfo called`

Nếu không thấy dòng `GetDisplayAttributeInfo` hoặc `EnumDisplayAttributeInfo` khi gõ, Windows TSF chưa nhận ra BambooMintKey là Display Attribute Provider. Kiểm tra:
- Category `GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER` đã được đăng ký trong `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\CTF\TIP\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}\Category`.
- Hoặc dùng `ctfmon.exe` / đăng nhập lại để TSF reload.

### 6.4. Kiểm tra atom của 2 GUID

Nếu log hiện atom = 0 hoặc `RegisterGUID` thất bại, `TfCategoryMgrVTable` có thể bị sai slot. Đảm bảo slot `RegisterGUID` là slot 14 (0-based index 14) trong `TfCategoryMgrVTable` theo `msctf.h`.

### 6.5. Cache của Chromium / VS Code / TSF

Một số ứng dụng cache display attribute per-process. Nếu đã từng gõ trong một cửa sổ trước khi đổi setting, cửa sổ đó có thể không cập nhật ngay. Với fix 2-GUID, lần gõ tiếp theo sẽ gán atom khác nên app buộc phải query lại. Nếu vẫn không đổi, thử **khởi động lại ứng dụng** hoặc **đăng nhập lại Windows**.

### 6.6. Các lỗi thường gặp khác

1. **`ApplyCompositionAttribute` không được gọi**
   - Kiểm tra log có dòng `PerformUpdateText ec=..., text=...` không.
   - Nếu không có, composition không được tạo. Kiểm tra `CompositionManager.StartComposition` và `ITfKeyEventSink` có advise thành công hay không.

2. **`SetValue` trả về lỗi**
   - Log sẽ hiện `ApplyCompositionAttribute SetValue HR=0xXXXXXXXX, atom=...`.
   - Nếu HR là `0x80070057` (`E_INVALIDARG`), atom có thể không hợp lệ hoặc `pRange` đã bị giải phóng.
   - Nếu HR là `0x80004001` (`E_NOTIMPL`), context không hỗ trợ property.

3. **`ITfDisplayAttributeProvider` không bao giờ được gọi**
   - Đảm bảo `BambooMintKeyTextService.QueryInterface` trả về pointer đúng cho `IidITfDisplayAttributeProvider`.
   - Đảm bảo `SupportedCategories` trong `TsfRegistration.cs` chứa `GuidTfCategoryDisplayAttributeProvider`.
   - Kiểm tra registry: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\Category\{04862A28-7F03-4D6B-89EB-B738D56D1F9C}`.

4. **Gạch chân vẫn hiện dù Stealth đang active**
   - Một số ứng dụng vẽ underline của riêng chúng khi detect IME composition (ví dụ: một số trang web tự render preedit). Không thể kiểm soát từ TSF.
   - Nhưng với VS Code / Chromium chuẩn, `TF_LS_NONE` phải triệt tiêu underline.

### 6.7. Checklist tóm tắt

| Triệu chứng | Nguyên nhân có thể | Cách xác nhận / fix |
|---|---|---|
| Toggle Preedit không đổi gì | Chưa cài lại DLL mới | Chạy installer mới, restart ứng dụng |
| byte[21] không đổi | UI/SharedConfig chưa ghi shared memory | Kiểm tra `config.json` và shared memory |
| Không thấy log `GetDisplayAttributeInfo` | TSF chưa nhận provider | Kiểm tra registry category, restart ctfmon |
| `RegisterGUID` fail / atom=0 | Sai vtable slot | Chuẩn hóa `TfCategoryMgrVTable` theo `msctf.h` |
| Gạch chân đổi chậm | Cache của Chromium | Khởi động lại ứng dụng / đăng nhập lại |
| `SetValue` fail | Range/atom invalid | Kiểm tra log HR |
| Chỉ ứng dụng này bị, app khác không | App vẽ underline riêng | Test trên VS Code/Chrome để loại trừ |

---

## 7. Kế Hoạch Triển Khai (Implementation Steps)

1. **Bước 1:** Bổ sung GUIDs trong `Guids.cs` và struct definitions trong `TsfDisplayAttributeTypes.cs`.
2. **Bước 2:** Cài đặt COM vtables cho `DisplayAttributeInfoImpl.cs` và `EnumDisplayAttributeInfoImpl.cs`.
3. **Bước 3:** Cập nhật `BambooMintKeyTextService.cs` hỗ trợ `ITfDisplayAttributeProvider` trong `QueryInterface`.
4. **Bước 4:** Hoàn thiện `DisplayAttributeHelper.cs` gán thuộc tính hiển thị vào `pRange`.
5. **Bước 5:** Chuẩn hóa thứ tự VTable `ITfCategoryMgr` và đăng ký Category trong `TsfRegistration.cs`.
6. **Bước 6:** Bổ sung trường `EnablePreedit` vào `SharedMemoryManager.cs`, `SharedConfig.fs`, `MainWindow.axaml` và `MainWindow.axaml.fs`.
7. **Bước 7:** Cập nhật `installer.iss` với `CloseApplications=yes`, `RestartApplications=yes`, và dọn key `HKCU\...\Run` khi uninstall.
8. **Bước 8:** Biên dịch NativeAOT DLL, build bộ cài đặt mới `BambooMintKey-Setup.exe` và kiểm thử toàn diện.
9. **Bước 9:** Nếu Preedit không hoạt động trên máy người dùng, chạy checklist điều tra ở mục 6.

