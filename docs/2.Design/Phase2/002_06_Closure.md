<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 002_06 — Tổng Kết & Đóng Giai Đoạn 2 (TSF/COM Registration Closure)

> Tài liệu tổng kết trạng thái cuối cùng của Phase 2: các vấn đề đã gặp, cách khắc phục, và kết quả đạt được khi triển khai COM Text Input Processor (TIP) trên Windows TSF.

---

## 1. Trạng Thái Cuối Cùng

| Hạng mục | Trạng thái | Ghi chú |
|----------|-----------|---------|
| Build toàn solution (`BambooMintKey.slnx`) | ✅ OK | `dotnet build -c Release` thành công. |
| Publish NativeAOT Shared DLL | ✅ OK | `publish/win-x64/BambooMintKey.dll` được tạo. Nếu bị khóa, cần giải phóng handle trước khi publish. |
| DevHarness test nội bộ | ✅ OK | `DllGetClassObject`, `CreateInstance`, `QueryInterface` hoạt động chuẩn. |
| `DllRegisterServer` | ✅ OK | Trả về `0x00000000`; registry `CLSID` và `HKLM\SOFTWARE\Microsoft\CTF\TIP` được tạo đầy đủ. |
| `CoCreateInstance` cho CLSID BambooMintKey | ✅ OK | `IUnknown`, `ITfTextInputProcessor`, `ITfTextInputProcessorEx`, `ITfThreadMgrEventSink`, `ITfKeyEventSink` đều `QueryInterface` thành công. |
| Hiển thị trong Windows input methods | ✅ OK | BambooMintKey xuất hiện trong `Get-WinUserLanguageList` cho Vietnamese. |
| Thêm từ Settings | ✅ OK | Có thể thêm & giữ lại trong Settings. |
| `HKCU\SOFTWARE\Microsoft\CTF\TIP\{CLSID}...` | ✅ OK | Profile key được tạo khi user kích hoạt TIP. |
| TSF runtime (`ctfmon`, `msctf`) | ✅ OK | `ctfmon.exe` chạy bình thường. |

**Kết luận Phase 2:** BambooMintKey đã đăng ký thành công như một TSF Text Input Processor trên Windows, sẵn sàng cho giai đoạn tiếp theo (tinh chỉnh engine gõ, UI cấu hình, packaging).

---

## 2. Các Vấn Đề Đã Gặp & Cách Khắc Phục

### 2.1. GUID Interface TSF Không Khớp Windows SDK

**Biểu hiện:** `QueryInterface` cho một số interface TSF trả về `E_NOINTERFACE` (`0x80004002`).

**Nguyên nhân:** Một số GUID trong `Guids.cs` được lấy từ tài liệu thứ cấp không chính xác.

**Khắc phục:** Tra cứu trực tiếp từ Windows SDK `C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\msctf.idl` và cập nhật:

| Interface | GUID đúng |
|-----------|-----------|
| `ITfTextInputProcessorEx` | `6E4E2102-F9CD-433D-B496-303CE03A6507` |
| `ITfTextInputProcessor` | `AA80E7F7-2021-11D2-93E0-0060B067B86E` |
| `ITfThreadMgrEventSink` | `AA80E80E-2021-11D2-93E0-0060B067B86E` |
| `ITfEditSession` | `AA80E803-2021-11D2-93E0-0060B067B86E` |

### 2.2. Cấu Trúc Registry TSF Không Đúng Chuẩn

**Biểu hiện:** Windows không nhận diện TIP; thêm trong Settings bị tự động xóa.

**Nguyên nhân:** Registry Language Profile thiếu `Description` / `Display Description`, và cấu trúc Category chưa khớp chuẩn TSF.

**Khắc phục:** Cập nhật `TsfRegistration.cs` để ghi đầy đủ:

```
HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\LanguageProfile\0x0000042A\{ProfileGuid}
    (Default)            : BambooMintKey Vietnamese Input
    Description          : BambooMintKey Vietnamese Input
    Display Description  : BambooMintKey Vietnamese Input
    Enable               : 1
    IconFile             : D:\...\BambooMintKey.dll
    IconIndex            : 0

HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\Category\{34745C63-B2F0-4784-8B67-5E12C8701A31}
HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\Category\{35E7A704-438C-4235-96BC-4A6361C31595}
HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\Category\Item\{CLSID}
```

### 2.3. `ITfInputProcessorProfiles` / `ITfCategoryMgr` COM Class Không Có Sẵn

**Biểu hiện:** `CoCreateInstance` trả về `0x80040154` (`REGDB_E_CLASSNOTREG`).

**Nguyên nhân:** Máy Windows 10 Home Core thiếu registration cho các COM class TSF manager.

**Khắc phục:** Triển khai **registry fallback** trong `TsfRegistration.cs` — ghi trực tiếp vào `HKLM\SOFTWARE\Microsoft\CTF\TIP` thay vì buộc phải qua COM API.

### 2.4. File `BambooMintKey.dll` Bị Khóa Khi Publish

**Biểu hiện:** `dotnet publish` lỗi `MSB3027` vì file bị nhiều process giữ handle.

**Nguyên nhân:** Windows Explorer / ứng dụng desktop giữ handle DLL.

**Khắc phục tạm thời:**

```bash
mv publish/win-x64/BambooMintKey.dll publish/win-x64/BambooMintKey.dll.locked
```

Sau đó publish lại. Cần giải pháp dài hạn hơn trong Phase 3 (ví dụ: publish sang thư mục staging, dừng `ctfmon` trước publish).

### 2.5. HKCU CTF Profile Không Được Tạo

**Biểu hiện:** `enable-tip.ps1` chạy thành công nhưng key `HKCU\SOFTWARE\Microsoft\CTF\TIP\{CLSID}...` không tồn tại; TIP không xuất hiện trong language list.

**Nguyên nhân:** `Set-WinUserLanguageList` tự động loại bỏ TIP không hợp lệ. Khi registry profile chưa đúng chuẩn, Windows validation từ chối và không tạo HKCU key.

**Khắc phục:** Sau khi sửa registry profile và category theo chuẩn, `Set-WinUserLanguageList` giữ lại TIP và HKCU key được tạo tự động.

---

## 3. Quy Trình Đăng Ký & Kích Hoạt Chuẩn

### 3.1. Build & Publish

```powershell
# Build toàn solution
dotnet build BambooMintKey.slnx -c Release

# Publish NativeAOT Shared DLL
dotnet publish src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj `
  -c Release -r win-x64 --self-contained `
  -o publish/win-x64 `
  -p:NativeLib=Shared -p:PublishAot=true
```

### 3.2. Đăng Ký (Administrator)

```powershell
pwsh -File scripts/test-register.ps1
```

Script thực hiện:
1. Gọi `DllRegisterServer` từ `BambooMintKey.dll`.
2. Tạo COM CLSID trong `HKLM\SOFTWARE\Classes\CLSID\{CLSID}`.
3. Tạo TSF TIP profile trong `HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}`.
4. Đăng ký category bàn phím và display attribute.

### 3.3. Kích Hoạt Cho User Hiện Tại (Non-Admin)

```powershell
pwsh -File scripts/enable-tip.ps1
```

Script thực hiện:
1. Tạo/ cập nhật `HKCU\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\LanguageProfile\0x0000042A\{ProfileGuid}`.
2. Thêm BambooMintKey vào Vietnamese input methods qua `Set-WinUserLanguageList`.

### 3.4. Restart TSF Runtime

```powershell
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Process ctfmon
```

### 3.5. Kiểm Tra

```powershell
(Get-WinUserLanguageList | Where-Object { $_.LanguageTag -eq 'vi' }).InputMethodTips
```

Kết quả mong đợi chứa:

```text
042A:{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}{C2F31A8E-92D0-4F81-9C3E-A52889211D44}
```

---

## 4. Bài Học Kỹ Thuật

1. **GUID TSF phải lấy từ Windows SDK `msctf.idl`**, không nên tin tưởng tài liệu thứ cấp.
2. **Registry profile cần đầy đủ `Description` và `Display Description`** để Windows Settings hiển thị tên và giữ lại TIP.
3. **Cấu trúc Category chuẩn là `Category\Category\{guid}` và `Category\Item\{CLSID}`**.
4. **Khi COM API TSF không khả dụng, registry fallback là bắt buộc** trên một số bản Windows Home.
5. **HKCU CTF profile được tạo như một hệ quả của validation thành công**, không phải do ghi trực tiếp đơn thuần.
6. **NativeAOT `Marshal.GetFunctionPointerForDelegate` không đảm bảo trỏ vào DLL publish**; cần dùng địa chỉ hàm `[UnmanagedCallersOnly]` export.

---

## 5. Tài Liệu Liên Quan

- [002_00_Overview_Architecture.md](002_00_Overview_Architecture.md) — tổng quan Phase 2.
- [002_01_COM_Registration_and_Exports.md](002_01_COM_Registration_and_Exports.md) — chi tiết COM registration.
- [002_02_TSF_TextInputProcessor_Lifecycle.md](002_02_TSF_TextInputProcessor_Lifecycle.md) — vòng đời TIP.
- [002_03_KeyEventSink_and_Core_Interop.md](002_03_KeyEventSink_and_Core_Interop.md) — xử lý phím.
- [002_04_Composition_and_TextRange.md](002_04_Composition_and_TextRange.md) — composition.
- [002_05_DevHarness_and_RegistrationScript.md](002_05_DevHarness_and_RegistrationScript.md) — script và harness.

---

## 6. Kết Luận

Giai đoạn 2 đã hoàn thành: BambooMintKey đăng ký thành công trên Windows TSF, hiển thị trong Language Bar, và sẵn sàng để chuyển sang Phase 3 (hoàn thiện engine gõ thực tế, UI cấu hình, installer/packaging).
