<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# BambooMintKey

**Bộ gõ tiếng Việt Telex cho Windows, tích hợp sâu vào Text Services Framework (TSF).**

BambooMintKey là một Text Input Processor (TIP) viết bằng .NET 10 + NativeAOT, chạy như một In-Process COM Server bên trong tiến trình ứng dụng đích. Dự án kết hợp lõi xử lý ngôn ngữ thuần chức năng (F#) với lớp cầu nối hệ thống Windows (C# NativeAOT) để mang lại trải nghiệm gõ tiếng Việt nhẹ, nhanh và tương thích rộng.

---

## Tính Năng

- **Gõ Telex tiếng Việt chuẩn** (`aa` → `â`, `dd` → `đ`, `as` → `á`, v.v.).
- **Tích hợp Windows TSF**: hiển thị trên Language Bar, chuyển đổi bằng `Win + Space`.
- **In-Process NativeAOT**: toàn bộ bộ gõ được biên dịch thành một DLL C gốc duy nhất (`BambooMintKey.dll`).
- **Xử lý phím mức hệ thống** qua `ITfKeyEventSink`, không dựa vào giả lập phím.
- **Lõi F# thuần chức năng**: engine Telex, parser vần, bảng Unicode được viết bằng F# và liên kết tĩnh với C# NativeBridge.

---

## Yêu Cầu

| Thành phần | Phiên bản |
|------------|-----------|
| Windows | Windows 10/11 (64-bit) |
| .NET SDK | 10.0 hoặc mới hơn |
| Công cụ build | `dotnet` CLI |
| Windows SDK | Được khuyến nghị để phát triển TSF |

---

## Bắt Đầu Nhanh

### 1. Build

```powershell
# Build toàn bộ solution
dotnet build BambooMintKey.slnx -c Release

# Publish NativeAOT DLL để chạy trên Windows
dotnet publish src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj `
  -c Release -r win-x64 --self-contained `
  -o publish/win-x64 `
  -p:NativeLib=Shared -p:PublishAot=true
```

### 2. Đăng Ký Bộ Gõ (Administrator)

```powershell
pwsh -File scripts/test-register.ps1
```

Script sẽ:
- Đăng ký CLSID COM In-Process Server.
- Tạo TSF Language Profile cho tiếng Việt (`0x042A`).
- Đăng ký Category `GUID_TFCAT_TIP_KEYBOARD`.

### 3. Kích Hoạt Cho User Hiện Tại

```powershell
pwsh -File scripts/enable-tip.ps1
```

Sau đó restart `ctfmon`:

```powershell
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Process ctfmon
```

### 4. Kiểm Tra

```powershell
(Get-WinUserLanguageList | Where-Object { $_.LanguageTag -eq 'vi' }).InputMethodTips
```

Bạn sẽ thấy BambooMintKey trong danh sách input method của Vietnamese.

---

## Kiến Trúc

```
┌─────────────────────────────────────────────────────────────────────┐
| Ứng dụng đích (Notepad, Word, Chrome, ...)                          |
|                                                                     |
|  ┌──────────────────────────────────────────────────────────────┐  |
|  | Windows TSF Subsystem (msctf.dll)                            |  |
|  └───────────────────────┬──────────────────────────────────────┘  |
|                         │ COM Interface Calls                       |
|  ┌──────────────────────▼──────────────────────────────────────┐  |
|  | [C# NativeAOT] BambooMintKey.NativeBridge (BambooMintKey.dll)|  |
|  |  - COM Exports: DllGetClassObject, DllRegisterServer, ...     |  |
|  |  - TSF Interfaces: ITfTextInputProcessorEx, ITfKeyEventSink   |  |
|  |  - State Bridge & Composition Manager                          |  |
|  └───────────────────────┬──────────────────────────────────────┘  |
|                         │ In-Memory Fast Call                       |
|  ┌──────────────────────▼──────────────────────────────────────┐  |
|  | [F#] BambooMintKey.Core                                       |  |
|  |  - Telex Engine, Syllable Parser, Unicode Tables             |  |
|  └───────────────────────────────────────────────────────────────┘  |
└─────────────────────────────────────────────────────────────────────┘
```

Chi tiết kiến trúc và thiết kế xem tại [`docs/2.Design/Phase2/`](docs/2.Design/Phase2/).

---

## Cấu Trúc Thư Mục

| Thư mục | Mô tả |
|---------|-------|
| `src/BambooMintKey.Core` | Lõi F#: engine Telex, parser, domain types. |
| `src/BambooMintKey.NativeBridge` | C# NativeAOT: COM server, TSF interfaces, bridge sang F#. |
| `src/BambooMintKey.Shared` | Thư viện dùng chung giữa Core và NativeBridge. |
| `src/BambooMintKey.UI` | Giao diện Avalonia (nếu có) cho cấu hình bộ gõ. |
| `src/BambooMintKey.DevHarness` | Console harness kiểm thử COM/TSF nội bộ. |
| `tests/BambooMintKey.Core.Tests` | Unit tests cho Telex engine. |
| `scripts/` | PowerShell scripts đăng ký, gỡ đăng ký, enable TIP. |
| `docs/` | Tài liệu thiết kế và hướng dẫn. |

---

## Giấy Phép

Dự án được phát hành dưới [MIT License](LICENSE).

Copyright (c) 2026 Dương Gia Long and LMO contributors
