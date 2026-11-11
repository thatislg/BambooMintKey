<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 028: BambooMintKey không kích hoạt được trong Zalo PC (32-bit WOW64) và tự reset về English

**Mã tài liệu:** `028_Zalo_TSF_NotInSwitcher_And_IdleResetToEnglish`  
**Trạng thái:** ✅ Đã đóng (Resolved — Đã xác minh hoạt động trên máy thực tế)  
**Mức độ nghiêm trọng:** Cao (Ảnh hưởng toàn bộ ứng dụng 32-bit WOW64 như Zalo PC + mất trạng thái bộ gõ toàn cục)  
**Ngày ghi nhận & hoàn tất:** 11/11/2026  
**Nền tảng ảnh hưởng:** Windows 10 / Windows 11 64-bit khi chạy ứng dụng 32-bit WOW64 (điển hình: **Zalo PC**)  
**Mô-đun liên quan:**
- `src/BambooMintKey.NativeBridge/COM/ServerRegistrar.cs` (đăng ký/gỡ đăng ký song song COM & TSF cho cả `x64` và `x86` WOW64)
- `src/BambooMintKey.NativeBridge/TSF/TsfCompartmentHelper.cs` (chuẩn hóa ABI struct `Variant` cho cả 32-bit và 64-bit)
- `src/BambooMintKey.NativeBridge/Interop/TsfRegistration.cs` & `NativeMethods.cs` (`LoadKeyboardLayoutW` + `SubstituteKeyboardLayout`)
- `src/BambooMintKey.UI/TsfBootstrapper.fs` (triển khai & kiểm tra `BambooMintKey.dll` + `BambooMintKey32.dll` cho gói MSIX)
- `scripts/windows/build-native.ps1`, `scripts/windows/sync-version.ps1`, `Directory.Build.props`, `BambooMintKey.Package/BambooMintKey.Package.wapproj`, `src/BambooMintKey.UI/BambooMintKey.UI.fsproj`

---

## 1. Mô tả hiện tượng

### 1.1. Triệu chứng 1 — Không hiện / không kích hoạt được trong Zalo PC
Khi focus vào ô nhập chat của **Zalo PC** trên Windows 64-bit và nhấn `Win + Space`:
- Ban đầu **BambooMintKey không xuất hiện** trong danh sách chuyển bộ gõ.
- Bộ gõ tiếng Nhật (**Microsoft Japanese IME**) vẫn xuất hiện và gõ bình thường trong Zalo.
- Các ứng dụng 64-bit khác (trình duyệt, Notepad, IDE, ...) vẫn hiện và gõ tiếng Việt bình thường.

### 1.2. Triệu chứng 2 — Tự động rơi về English sau một khoảng thời gian không gõ
Khi đang để bộ gõ ở BambooMintKey, sau một lúc không gõ (hoặc có tương tác nền), bộ gõ toàn hệ thống tự động rơi về **English (`ENG`)**. Muốn gõ tiếng Việt lại phải mở `Win + Space` chọn lại BambooMintKey.

---

## 2. Quá trình điều tra & Bằng chứng thực nghiệm

### 2.1. Kiểm tra cấu hình ngôn ngữ & TSF 64-bit
Kết quả `Get-WinUserLanguageList`:
```text
en-US: 0409:00000409
ja   : 0411:{03B5835F-F03C-411B-9CE2-AA23E1171E36}{A76C93D9-5523-4E90-AAFA-4DB112F9AC76}
vi   : 042A:{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}{C2F31A8E-92D0-4F81-9C3E-A52889211D44}
```
- Nhánh 64-bit `HKCR\CLSID\{B8A5A29D-...}\InprocServer32` trỏ đúng `C:\ProgramData\BambooMintKey\BambooMintKey.dll`, đầy đủ ACL (`Everyone`, `Users`, `ALL APPLICATION PACKAGES` `RX`) và đủ 6 TSF Categories.

### 2.2. Thử nghiệm bước 1 (`SubstituteKeyboardLayout`) — Manh mối quyết định
- Khi bổ sung `LoadKeyboardLayoutW("0000042A", ...)` + `ITfInputProcessorProfiles::SubstituteKeyboardLayout`, Zalo bắt đầu **cho phép chọn BambooMintKey** trong danh sách bộ gõ, nhưng **vẫn không gõ được tiếng Việt** và ngay khi di chuột/đổi focus thì thanh ngôn ngữ tự trả về **`ENG`**.
- Hiện tượng "chọn được profile nhưng không khởi tạo được phiên TSF trên tiến trình Zalo" dẫn thẳng đến kiểm tra kiến trúc nhị phân của `Zalo.exe` và nhánh Registry `WOW6432Node`.

### 2.3. Kiểm tra PE Header của Zalo PC & Registry `WOW6432Node`
Kiểm tra trực tiếp tệp thực thi Zalo PC đang chạy trên máy:
```text
C:/Users/.../AppData/Local/Programs/Zalo/Zalo-26.10.20/Zalo.exe:
PE32 executable for MS Windows 5.01 (GUI), Intel i386, 11 sections
```
Đối chiếu khóa COM 32-bit (`WOW6432Node`) giữa Microsoft Japanese IME và BambooMintKey:
```text
HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{03B5835F-F03C-411B-9CE2-AA23E1171E36}\InprocServer32
    (Default)      REG_SZ    C:\Windows\SysWOW64\IME\IMEJP\imjptip.dll
    ThreadingModel REG_SZ    Apartment

HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}
    ERROR: The system was unable to find the specified registry key or value.
```

---

## 3. Nguyên nhân gốc rễ (Root Cause)

### 3.1. Thiếu TSF In-Process COM Server 32-bit (`win-x86`) cho các ứng dụng WOW64
1. **Zalo PC trên Windows là ứng dụng 32-bit (`x86` / `PE32 Intel i386`)**, chạy dưới lớp giả lập WOW64.
2. **TSF hoạt động theo mô hình In-Process COM Server (`InprocServer32`)**: tiến trình đích phải trực tiếp nạp (`LoadLibrary`) DLL của bộ gõ có **cùng kiến trúc bit** vào không gian địa chỉ của chính nó. Một tiến trình 32-bit tuyệt đối không thể nạp DLL 64-bit và chỉ tra cứu COM tại `HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{...}`.
3. Từ trước đến nay, BambooMintKey chỉ biên dịch và đăng ký duy nhất bản **64-bit (`win-x64`)**. Sau khi dọn sạch các bàn phím/IME tiếng Việt mặc định bị trùng lặp (Issue 023/024), ngôn ngữ `vi` (`0x042A`) chỉ còn duy nhất TIP 64-bit của BambooMintKey:
   - Khi chưa có `SubstituteKeyboardLayout`: Windows CTF thấy `0x042A` không có bất kỳ TIP 32-bit hay HKL dự phòng nào cho tiến trình 32-bit → **ẩn luôn BambooMintKey khỏi `Win + Space` khi focus vào Zalo**.
   - Khi có `SubstituteKeyboardLayout`: Zalo thấy HKL dự phòng nên hiện trong danh sách, nhưng `CoCreateInstance` trong tiến trình 32-bit thất bại (`REGDB_E_CLASSNOTREG`) → không nạp được engine tiếng Việt và lập tức bị trả về `ENG` khi đổi focus.

### 3.2. Cơ chế gây lỗi "tự reset về English sau một lúc không gõ"
- Ở chế độ mặc định của Windows 10/11 (Global Input Method — dùng chung trạng thái bộ gõ giữa mọi cửa sổ), mỗi khi một tiến trình hoặc cửa sổ phụ 32-bit chạy nền (`Zalo.exe`, `ZaloCap.exe`, `ZaloCall.exe`, popup thông báo Zalo) nhận focus thoáng qua hoặc đồng bộ input context, Windows cố kích hoạt TIP hiện hành (`042A` BambooMintKey) trên thread 32-bit đó.
- Do kích hoạt thất bại, Windows tự động fallback bộ gõ hiện hành của toàn phiên làm việc về mục đầu tiên trong `HKCU\Keyboard Layout\Preload` (`1 = 00000409` — **English US**). Khi người dùng quay lại gõ trên ứng dụng 64-bit khác, bộ gõ toàn cục đã bị đổi về English.

---

## 4. Giải pháp kỹ thuật đã triển khai

### 4.1. Biên dịch song song NativeAOT Dual-Architecture (`win-x64` + `win-x86`)
SDK `.NET 10 NativeAOT` hỗ trợ biên dịch gốc cho `win-x86`, xuất ra DLL `PE32 (Intel i386)` với đầy đủ 4 điểm nhập COM C-ABI chuẩn (`DllGetClassObject`, `DllCanUnloadNow`, `DllRegisterServer`, `DllUnregisterServer`):
- Khai báo `<RuntimeIdentifiers>win-x64;win-x86</RuntimeIdentifiers>` trong `Directory.Build.props` để NuGet restore đồng thời cả hai target mà không ghi đè `project.assets.json` của nhau.
- Mở rộng `scripts/windows/build-native.ps1` (tham số `-IncludeX86`) để xuất hai tệp riêng tên nhằm tránh xung đột `%(Filename)` trong MSBuild DesktopBridge:
  - `publish\win-x64\BambooMintKey.dll` (64-bit)
  - `publish\win-x86\BambooMintKey32.dll` (32-bit)
- Đóng gói cả `BambooMintKey.dll` và `BambooMintKey32.dll` vào `BambooMintKey.Package.wapproj` và `BambooMintKey.UI.fsproj`.

### 4.2. Chuẩn hóa ABI 32/64-bit trong `NativeBridge`
- Trong `src/BambooMintKey.NativeBridge/TSF/TsfCompartmentHelper.cs`, chuyển struct `Variant` từ `[StructLayout(LayoutKind.Explicit, Size = 24)]` cố định sang `LayoutKind.Sequential` với 2 trường payload kiểu `IntPtr`. Kích thước struct tự động khớp đúng đặc tả `VARIANT` của Windows SDK trên từng kiến trúc: **16 bytes trên `x86`** và **24 bytes trên `x64`**.
- Giữ liên kết `LoadKeyboardLayoutW("0000042A", KLF_ACTIVATE | KLF_SUBSTITUTE_OK)` + `SubstituteKeyboardLayout` trong `TsfRegistration.cs`.

### 4.3. Triển khai & Đăng ký tự động với duy nhất 1 hộp thoại UAC
- **`TsfBootstrapper.fs` (UI / MSIX Bootstrapper)**:
  - Kiểm tra tính toàn vẹn tệp tin tại `C:\ProgramData\BambooMintKey\` và khóa `InprocServer32` trên **cả hai nhánh Registry** (`RegistryView.Registry64` cho `BambooMintKey.dll` và `RegistryView.Registry32` / `WOW6432Node` cho `BambooMintKey32.dll`).
  - Sao chép an toàn (hỗ trợ rename-and-replace khi đang bị tiến trình khác giữ lock) cả 2 DLL ra `C:\ProgramData\BambooMintKey\`.
- **`ServerRegistrar.cs` (`DllRegisterServer` / `DllUnregisterServer`)**:
  - Khi tiến trình `regsvr32.exe` 64-bit (đã được nâng quyền Admin) chạy `RegisterServer()`, nó tự động kiểm tra sự tồn tại của `BambooMintKey32.dll` cùng thư mục và gọi `%SystemRoot%\SysWOW64\regsvr32.exe /s BambooMintKey32.dll` trước. Tiến trình con kế thừa trực tiếp token Admin của tiến trình cha nên **người dùng vẫn chỉ bấm UAC đúng 1 lần duy nhất**.
  - Khi gỡ đăng ký (`UnregisterServer`), tiến trình 64-bit tự động gọi `SysWOW64\regsvr32.exe /u /s` và dọn sạch khóa `SOFTWARE\Classes\CLSID\{...}` ở `RegistryView.Registry32`.

### 4.4. Khắc phục lỗi phụ `MSB4184` khi đóng gói MSIX & đồng bộ Version
- Lỗi `MSB4184: The expression """.get_Chars(0)" cannot be evaluated` khi chạy MSBuild trên `BambooMintKey.Package.wapproj` xuất phát từ đoạn PowerShell inline trong target `SyncVersionFromAppxManifest` khiến MSBuild diễn giải nhầm biến `$($p[0])` của PowerShell thành biểu thức MSBuild.
- Đã tách toàn bộ logic đồng bộ phiên bản ra `scripts/windows/sync-version.ps1` (gọi qua `-File` trước `PrepareForBuild;ResolveProjectReferences`), đảm bảo mỗi khi đổi Version trong Visual Studio (`Package.appxmanifest`), toàn bộ `Directory.Build.props`, `app.manifest`, `BambooMintKey.UI.exe`, `BambooMintKey.dll` và `BambooMintKey32.dll` đều tự động cập nhật đồng bộ trong cùng một lần build.

---

## 5. Kết quả kiểm chứng

- [x] Unit test Core Engine (`BambooMintKey.Core.Tests`): **530/530 passed**.
- [x] Build MSIX (`BambooMintKey.Package.wapproj`, Release `x64` qua MSBuild 18 / VS 2026): xuất thành công `.msixbundle` chứa đồng thời `BambooMintKey.dll` (x64) và `BambooMintKey32.dll` (x86), không còn lỗi `MSB4184` hay cảnh báo nullness ở `FrozenDictionaryService.fs`.
- [x] Cài đặt thực tế và kiểm thử trực tiếp trên **Zalo PC (32-bit)**: chọn được BambooMintKey, giữ nguyên trạng thái bộ gõ khi đổi focus và **gõ tiếng Việt Telex ổn định trong ô chat Zalo**.

---

## 6. Tham chiếu
- `docs/3.Issue/023_Windows_LangBar_EV_Icon_Conflict_With_Other_IMEs.md` (xử lý `GetKeyboardLayout` khi chuyển bộ gõ)
- `docs/3.Issue/027_Windows_SearchBar_AppContainer_RepeatKeyUndo_And_Backspace_Bug.md` (tiền lệ tiến trình đặc biệt/AppContainer)
- `docs/2.Design/Phase2/002_01_COM_Registration_and_Exports.md` (đặc tả đăng ký COM/TSF)
