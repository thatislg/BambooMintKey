# 002_06 — Vấn đề đã biết trong giai đoạn 2 (TSF/COM Registration)

> Ghi chú kỹ thuật tổng hợp các lỗi, nguyên nhân và trạng thái xử lý khi triển khai COM Text Input Processor (TIP) trên Windows TSF.

---

## 1. Tổng quan trạng thái dự án

| Hạng mục | Trạng thái | Ghi chú |
|----------|-----------|---------|
| Build toàn solution (`BambooMintKey.slnx`) | ✅ OK | `dotnet build -c Release` thành công. |
| Publish NativeAOT Shared DLL | ✅ OK | Sau khi giải phóng file bị khóa, `publish/win-x64/BambooMintKey.dll` được tạo lại. |
| DevHarness test nội bộ | ✅ OK | Kiểm tra `DllGetClassObject`, `CreateInstance`, `QueryInterface` đều chuẩn. |
| `DllRegisterServer` | ✅ OK | Trả về `0x00000000`, registry `CLSID` và `HKLM\SOFTWARE\Microsoft\CTF\TIP` được tạo. |
| `CoCreateInstance` cho CLSID của BambooMintKey | ✅ OK | PowerShell `GetTypeFromCLSID` tạo instance thành công; `QueryInterface` cho `IUnknown`, `ITfTextInputProcessor`, `ITfTextInputProcessorEx`, `ITfThreadMgrEventSink`, `ITfKeyEventSink` đều trả về `S_OK`. |
| Hiển thị trong Windows input methods | ❌ Chưa OK | Không xuất hiện trong `Get-WinUserLanguageList` của Vietnamese. |
| Thêm từ Settings | ⚠️ Bị tự xóa | Có thể chọn trong Settings, nhưng sau khi đóng tab thì TIP bị loại bỏ. |
| `HKCU\SOFTWARE\Microsoft\CTF\TIP\{CLSID}...` | ❌ Chưa tạo | Script `enable-tip.ps1` chạy báo thành công nhưng registry key không tồn tại. |
| TSF runtime (`ctfmon`, `msctf`) | ✅ OK | `ctfmon.exe` chạy bình thường; `sfc /scannow` và `DISM` không phát hiện lỗi. |

---

## 2. Các vấn đề cụ thể đã gặp

### 2.1. File `BambooMintKey.dll` bị khóa khi publish

**Biểu hiện:**

```text
error MSB3027: Could not copy ... to publish\win-x64\BambooMintKey.dll
The file is locked by: "Windows Explorer (11040), Application Frame Host (7860),
Podman Desktop (20520), Remote Desktop (21964), Microsoft Edge (22928),
Zed (1352), Opera Internet Browser (15984)"
```

**Nguyên nhân:** DLL tại `publish/win-x64/BambooMintKey.dll` được một loạt ứng dụng Windows Desktop giữ handle (có thể do file nằm trong thư mục bị Windows Search/Explorer index, hoặc do một số ứng dụng load DLL thử khi test đăng ký trước đó).

**Cách xử lý tạm thời:**

```bash
mv publish/win-x64/BambooMintKey.dll publish/win-x64/BambooMintKey.dll.locked
```

Sau đó publish lại thành công.

> ⚠️ Đây **không phải** giải pháp lâu dài. Cần một cơ chế publish an toàn hơn hoặc tránh các ứng dụng giữ handle DLL.

---

### 2.2. GUID interface TSF trong `Guids.cs` không khớp Windows SDK

**Biểu hiện:** CoCreateInstance thành công nhưng `QueryInterface` cho `ITfTextInputProcessorEx` có thể trả về `E_NOINTERFACE` (`0x80004002`) nếu dùng GUID sai.

**Sai sót đã sửa:**

| Interface | GUID cũ (sai) | GUID đúng (Windows SDK `msctf.idl`) |
|-----------|--------------|-------------------------------------|
| `ITfTextInputProcessorEx` | `AABEC164-429C-4234-A75D-4E90B01D77D1` | `6E4E2102-F9CD-433D-B496-303CE03A6507` |
| `ITfTextInputProcessor` | `AA80E7D5-2021-11D2-93E0-0060B067B86E` | `AA80E7F7-2021-11D2-93E0-0060B067B86E` |
| `ITfThreadMgrEventSink` | `30B573D0-CCFA-11D2-9A86-00AA006EFD5E` | `AA80E80E-2021-11D2-93E0-0060B067B86E` |
| `ITfEditSession` | `AA80E7FD-2021-11D2-93E0-0060B067B86E` | `AA80E803-2021-11D2-93E0-0060B067B86E` |

> `ITfKeyEventSink` may mắn đã đúng ngay từ đầu (`AA80E7F5-2021-11D2-93E0-0060B067B86E`).

**Bài học:** Tất cả GUID TSF phải tra cứu từ Windows SDK (`C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\msctf.idl`) chứ không nên copy từ tài liệu thứ cấp.

---

### 2.3. `ITfInputProcessorProfiles` / `ITfCategoryMgr` COM class không có sẵn

**Biểu hiện:** `CoCreateInstance` với CLSID:

- `{33C53824-660F-457B-8B3E-5F4A9D87AC47}` (`ITfInputProcessorProfiles`)
- `{A4B54FC0-ACAA-49FB-BB87-4EB0260080F6}` (`ITfCategoryMgr`)

trả về `0x80040154` (`REGDB_E_CLASSNOTREG`).

**Nguyên nhân:** Máy Windows 10 Home Core của người dùng không đăng ký sẵn các COM class này trong Registry. Điều này khác thường vì chúng là phần của `msctf.dll`.

**Giải pháp:** Đã chuyển sang **registry fallback** — ghi trực tiếp vào:

- `HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}`
- `HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\LanguageProfile\0x0000042A\{ProfileGuid}`
- `HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\{CategoryGuid}`

`DllRegisterServer` vẫn trả về `S_OK`.

---

### 2.4. Sai đường dẫn Category trong registry

**Biểu hiện:** `Category` bị ghi thành:

```
HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\Category\{CAT_GUID}
```

thay vì:

```
HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\{CAT_GUID}
```

**Nguyên nhân:** Lỗi nối chuỗi trong `TsfRegistration.RegisterCategoriesRegistry()`.

**Đã sửa:** Loại bỏ thư mục `Category` trung gian thừa.

---

### 2.5. `Set-WinUserLanguageList` không giữ BambooMintKey

**Biểu hiện:**

```powershell
$vi.InputMethodTips.Add("042A:{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}{C2F31A8E-92D0-4F81-9C3E-A52889211D44}")
Set-WinUserLanguageList -LanguageList $list -Force
```

Chạy không lỗi, nhưng khi kiểm tra lại danh sách thì TIP không xuất hiện.

**Nguyên nhân dự đoán:**

1. **Windows tự động loại bỏ TIP không hợp lệ.** Khi `Set-WinUserLanguageList` được gọi, Windows kiểm tra TIP mới. Nếu TIP không vượt qua validation (ví dụ: thiếu `DisplayAttributeProvider`, `ITfKeyEventSink` chưa đủ hoàn chỉnh, hoặc DLL báo lỗi khi `Activate`), nó sẽ bị xóa ngầm.
2. **Thiếu `HKCU` CTF profile enable.** Mặc dù `enable-tip.ps1` gọi `New-Item`/`Set-ItemProperty`, registry `HKCU\SOFTWARE\Microsoft\CTF\TIP\{CLSID}...` vẫn **không tồn tại** sau khi chạy. Lý do chưa rõ — có thể do UAC virtualization, PowerShell session context, hoặc Windows bảo vệ registry TSF.
3. **Máy Windows Home Core có thể thiếu một số language capability cần thiết**, dù đã thêm `Language.Basic~~~vi-VN~0.0.1.0` và các capability khác.

---

### 2.6. HKCU registry profile không được tạo

**Script đã chạy:** `scripts/enable-tip.ps1`

**Kết quả mong đợi:**

```
HKCU\SOFTWARE\Microsoft\CTF\TIP\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}\LanguageProfile\0x0000042A\{C2F31A8E-92D0-4F81-9C3E-A52889211D44}
    (Default)    : BambooMintKey Vietnamese Input
    Enable       : 1
    IconFile     : D:\Kojin\BambooMintKey\publish\win-x64\BambooMintKey.dll
    IconIndex    : 0
```

**Kết quả thực tế:** Key không tồn tại (`reg query` trả về `ERROR: The system was unable to find the specified registry key or value`).

**Đã thử:**

- Script chạy dưới quyền user thường.
- Script `debug-enable-tip.ps1` thêm log chi tiết cũng cho kết quả tương tự.
- `Set-WinUserLanguageList` báo thành công nhưng không giữ TIP.

**Hướng điều tra tiếp:**

- Kiểm tra xem Windows có redirect HKCU ghi sang `HKU\.DEFAULT` hoặc virtual store không.
- Kiểm tra Event Viewer `Application` log xem có lỗi TSF/CTF khi thêm TIP.
- Thử ghi HKCU profile trực tiếp bằng `reg.exe` hoặc Win32 API thay vì `New-Item`.
- Xem xét thêm `ITfDisplayAttributeProvider` và hoàn thiện `ITfKeyEventSink` để TIP qua validation.

---

## 3. Các thay đổi code đã thực hiện

1. **`src/BambooMintKey.NativeBridge/Common/Guids.cs`** — Cập nhật GUID đúng theo Windows SDK `msctf.idl`.
2. **`src/BambooMintKey.NativeBridge/Interop/TsfRegistration.cs`** — Sửa đường dẫn `Category` registry, bỏ `Category\Category` thừa.
3. **`scripts/debug-enable-tip.ps1`** — Script debug mới để kiểm tra chi tiết `New-Item` / `Set-ItemProperty`.
4. **`scripts/debug-cocreate.ps1`** — Script test `CoCreateInstance` + `QueryInterface` từ PowerShell.

---

## 4. Hướng giải quyết tiếp theo đề xuất

### 4.1. Đối với việc TIP không xuất hiện / bị xóa

Cần kiểm tra các điểm sau:

- [ ] Hoàn thiện `ITfKeyEventSink` — đặc biệt `OnTestKeyDown`, `OnTestKeyUp`, `OnKeyDown`, `OnKeyUp`, `OnPreservedKey`.
- [ ] Thêm `ITfDisplayAttributeProvider` nếu cần (để TSF hiển thị composition dưới gạch chân).
- [ ] Kiểm tra `ITfTextInputProcessorEx::ActivateEx` trả về gì khi TSF thực sự tải DLL.
- [ ] Thêm logging (ví dụ: ghi file log trong `%TEMP%`) trong `ActivateEx` / `Deactivate` để xác nhận DLL có được load.
- [ ] Kiểm tra Event Viewer log khi chọn TIP trong Settings.

### 4.2. Đối với HKCU profile không tạo

- [ ] Thử ghi trực tiếp bằng `reg.exe` với quyền user.
- [ ] Kiểm tra xem `Set-WinUserLanguageList` có thực sự cần `HKCU\SOFTWARE\Microsoft\CTF\TIP` hay chỉ cần `HKCU\Control Panel\International\User Profile`.
- [ ] Xem xét sử dụng `ITfInputProcessorProfileMgr` (nếu có sẵn) thay vì `Set-WinUserLanguageList`.

### 4.3. Cải thiện quy trình publish

- [ ] Đóng tất cả ứng dụng giữ handle DLL trước khi publish.
- [ ] Cân nhắc thêm bước stop `ctfmon` / dịch vụ liên quan trước publish.
- [ ] Có thể publish ra thư mục khác rồi copy đè khi đảm bảo an toàn.

---

## 5. Lệnh kiểm tra nhanh

```powershell
# 1. Kiểm tra build
 dotnet build BambooMintKey.slnx -c Release

# 2. Publish NativeAOT Shared DLL
 dotnet publish src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj -c Release -r win-x64 --self-contained -o publish/win-x64 -p:NativeLib=Shared -p:PublishAot=true

# 3. Đăng ký (Admin)
 pwsh -File scripts/test-register.ps1

# 4. Enable cho user hiện tại (KHÔNG Admin)
 pwsh -File scripts/enable-tip.ps1

# 5. Restart ctfmon
 Stop-Process -Name ctfmon -Force; Start-Process ctfmon

# 6. Kiểm tra danh sách
 (Get-WinUserLanguageList | Where-Object { $_.LanguageTag -eq 'vi' }).InputMethodTips

# 7. Kiểm tra registry HKCU profile
 reg query "HKCU\SOFTWARE\Microsoft\CTF\TIP\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}\LanguageProfile\0x0000042A\{C2F31A8E-92D0-4F81-9C3E-A52889211D44}"

# 8. Kiểm tra COM instance + QueryInterface
 pwsh -File scripts/debug-cocreate.ps1
```

---

## 6. Kết luận

NativeAOT COM server đã **build và đăng ký thành công** ở mức HKLM; `DllRegisterServer`, `CoCreateInstance`, `QueryInterface` đều hoạt động. Tuy nhiên, TIP vẫn **chưa xuất hiện** trong danh sách input methods của user và bị Windows tự động loại bỏ khi thêm thủ công. Nguyên nhân chính đang nghiêng về: (a) thiếu/hỏng HKCU CTF profile enable, và (b) TIP chưa đủ hoàn chỉnh để vượt qua validation của `Set-WinUserLanguageList` / Windows Settings.

Cần tiếp tục hoàn thiện các interface TSF (`ITfKeyEventSink`, `ITfDisplayAttributeProvider`) và điều tra lý do HKCU registry không được ghi.
