<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Chi Tiết: Công Cụ Kiểm Thử Dev Harness & Script Tự Động Đăng Ký TSF

**Mã tài liệu:** `002_05_DevHarness_and_RegistrationScript`

  

**Giai đoạn:** Phase 2 - Tích hợp Hệ Điều Hành (Windows TSF & NativeAOT)  

**Thuộc module:** `BambooMintKey.DevHarness` & `scripts/`

**Trạng thái:** ✅ Hoàn thành (Closed)

> **Lưu ý cập nhật theo code hiện tại:** Danh sách script đã thay đổi trong quá trình phát triển. Xem phần "Cập nhật theo triển khai thực tế" ở cuối tài liệu để biết danh sách script hiện tại.

## 1. Mục Tiêu Kỹ Thuật

- Xây dựng một ứng dụng Console độc lập (`BambooMintKey.DevHarness`) để kiểm thử trực tiếp file `BambooMintKey.dll` đã biên dịch NativeAOT:  
  - Nạp DLL bằng Win32 `LoadLibraryW` / `GetProcAddress`.
  - Gọi các hàm xuất C-ABI `DllGetClassObject`, tạo instance qua `IClassFactory`, và kiểm tra phản hồi của `ITfTextInputProcessorEx` mà không cần inject vào tiến trình Windows.  
  - Giả lập chuỗi sự kiện `OnTestKeyDown` / `OnKeyDown` để xác minh luồng thay thế văn bản của F# Engine qua C-ABI bridge.
- Xây dựng bộ kịch bản tự động hóa PowerShell:
  - `build-native.ps1`: Tự động biên dịch NativeAOT ra `BambooMintKey.dll` (x64).  
  - `register-tip.ps1`: Đăng ký DLL với COM & Windows TSF bằng quyền Administrator.  
  - `unregister-tip.ps1`: Gỡ đăng ký sạch sẽ khỏi Taskbar và Registry để phục vụ chu trình dev-rebuild liên tục.  

## 2. Kiến Trúc Kiểm Thử Với Dev Harness (Isolated Test Pipeline)

```bash
┌────────────────────────────────────────────────────────────────────────┐
│ BambooMintKey.DevHarness (Console Runner)                              │
│                                                                        │
│  1. Win32 Dynamic Load:                                                │
│     - LoadLibrary("BambooMintKey.dll")                                 │
│     - GetProcAddress("DllGetClassObject")                              │
│                                                                        │
│  2. COM Instantiation:                                                 │
│     - DllGetClassObject(CLSID_BambooMintKey, IID_IClassFactory)        │
│     - IClassFactory::CreateInstance(IID_ITfTextInputProcessorEx)       │
│                                                                        │
│  3. Mock TSF Environment:                                              │
│     - Tạo Mock ITfThreadMgr & Mock ITfContext                          │
│     - Gọi ITfTextInputProcessorEx::ActivateEx(pMockMgr, clientId, 0)   │
│                                                                        │
│  4. Synthetic Key Injection:                                            │
│     - Gửi chuỗi Virtual Keys ('v', 'i', 'e', 't', 'j', ' ')            │
│     - Kiểm tra kết quả hiển thị trên Mock Context Range                │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ In-Process Fast Invocation
┌──────────────────────────────────▼─────────────────────────────────────┐
│ BambooMintKey.dll (C# NativeAOT Bridge + F# Core Engine)               │
└────────────────────────────────────────────────────────────────────────┘
```

## 3. Cài Đặt Ứng Dụng Console Dev Harness

Tập trung tại file `src/BambooMintKey.DevHarness/Program.cs`:

C#

```c#
using System;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.DevHarness;

public unsafe class Program
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    private delegate int DllGetClassObjectDelegate(Guid* rclsid, Guid* riid, IntPtr* ppv);

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BambooMintKey NativeAOT Dev Harness ===");

        string dllPath = args.Length > 0 ? args[0] : "BambooMintKey.dll";
        Console.WriteLine($"[1] Nạp thư viện: {dllPath}");

        IntPtr hModule = LoadLibraryW(dllPath);
        if (hModule == IntPtr.Zero)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] Không thể nạp DLL. Mã lỗi Win32: {Marshal.GetLastWin32Error()}");
            Console.ResetColor();
            return 1;
        }

        try
        {
            // 1. Lấy con trỏ hàm DllGetClassObject
            IntPtr pGetClassObject = GetProcAddress(hModule, "DllGetClassObject");
            if (pGetClassObject == IntPtr.Zero)
            {
                Console.WriteLine("[FAIL] Không tìm thấy export 'DllGetClassObject'.");
                return 1;
            }

            var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(pGetClassObject);

            // 2. Yêu cầu IClassFactory
            Console.WriteLine("[2] Khởi tạo COM ClassFactory...");
            IntPtr pClassFactory = IntPtr.Zero;
            Guid clsid = Guids.TextServiceClsid;
            Guid iidFactory = Guids.IidIClassFactory;

            int hr = dllGetClassObject(&clsid, &iidFactory, &pClassFactory);
            if (hr != HResult.Ok || pClassFactory == IntPtr.Zero)
            {
                Console.WriteLine($"[FAIL] DllGetClassObject thất bại với HRESULT: 0x{hr:X8}");
                return 1;
            }
            Console.WriteLine("[OK] Lấy thành công con trỏ IClassFactory.");

            // 3. Tạo instance ITfTextInputProcessorEx
            Console.WriteLine("[3] Tạo thực thể BambooMintKeyTextService...");
            IntPtr pTextService = IntPtr.Zero;
            Guid iidProcessorEx = Guids.IidITfTextInputProcessorEx;

            var factoryVTable = *(ClassFactoryVTable**)pClassFactory;
            hr = factoryVTable->CreateInstance(pClassFactory, IntPtr.Zero, &iidProcessorEx, &pTextService);

            if (hr != HResult.Ok || pTextService == IntPtr.Zero)
            {
                Console.WriteLine($"[FAIL] CreateInstance thất bại với HRESULT: 0x{hr:X8}");
                return 1;
            }
            Console.WriteLine("[OK] Khởi tạo đối tượng TIP thành công.");

            // 4. Test QueryInterface ITfKeyEventSink
            Console.WriteLine("[4] Kiểm tra QueryInterface cho ITfKeyEventSink...");
            IntPtr pKeyEventSink = IntPtr.Zero;
            Guid iidKeySink = Guids.IidITfKeyEventSink;

            var serviceVTable = *(TfTextInputProcessorExVTable**)pTextService;
            hr = serviceVTable->QueryInterface(pTextService, &iidKeySink, &pKeyEventSink);

            if (hr == HResult.Ok && pKeyEventSink != IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASS] Interface ITfKeyEventSink phản hồi chuẩn xác.");
                Console.ResetColor();

                var keySinkVTable = *(TfKeyEventSinkVTable**)pKeyEventSink;
                keySinkVTable->Release(pKeyEventSink);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Không thể QueryInterface ITfKeyEventSink. HRESULT: 0x{hr:X8}");
                Console.ResetColor();
                return 1;
            }

            // 5. Giải phóng COM Pointers
            serviceVTable->Release(pTextService);
            factoryVTable->Release(pClassFactory);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== TOÀN BỘ C-ABI VTABLE VÀ INTERFACE EXPORT ĐÃ VƯỢT QUA TEST ===");
            Console.ResetColor();
            return 0;
        }
        finally
        {
            FreeLibrary(hModule);
        }
    }
}
```

## 4. Bộ Script Tự Động Hóa (Automation Scripts)

### 4.1. Kịch bản biên dịch NativeAOT: `scripts/build-native.ps1`

Tự động dọn dẹp, khôi phục phụ thuộc, và xuất bản file `BambooMintKey.dll` độc lập:  

PowerShell

```c#
# scripts/build-native.ps1
[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RootDir "src\BambooMintKey.NativeBridge\BambooMintKey.NativeBridge.csproj"
$OutputDir = Join-Path $RootDir "publish\$Runtime"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "  BambooMintKey NativeAOT Build: $Configuration ($Runtime)" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Dọn dẹp thư mục output cũ
if (Test-Path $OutputDir) {
    Write-Host "[1/3] Xóa thư mục output cũ..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}

# 2. Biên dịch NativeAOT DLL
Write-Host "[2/3] Bắt đầu xuất bản NativeAOT..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDir `
    /p:NativeLib=Shared `
    /p:PublishAot=true

# 3. Kiểm tra file đầu ra
$TargetDll = Join-Path $OutputDir "BambooMintKey.dll"
if (Test-Path $TargetDll) {
    $fileSize = (Get-Item $TargetDll).Length / 1MB
    Write-Host "[3/3] Xuất bản thành công!" -ForegroundColor Green
    Write-Host " -> File: $TargetDll" -ForegroundColor Green
    Write-Host " -> Dung lượng: $([Math]::Round($fileSize, 2)) MB" -ForegroundColor Green
} else {
    Write-Host "[LỖI] Không tìm thấy file BambooMintKey.dll sau khi build!" -ForegroundColor Red
    exit 1
}
```

### 4.2. Kịch bản đăng ký TIP với Windows: `scripts/register-tip.ps1`

Yêu cầu quyền Administrator để ghi Registry COM và đăng ký TSF Language Profile:  

PowerShell

```c#
# scripts/register-tip.ps1
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# Đảm bảo chạy dưới quyền Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[CẢNH BÁO] Kịch bản cần quyền Administrator để đăng ký COM/TSF Server." -ForegroundColor Red
    Write-Host "Đang yêu cầu nâng quyền (UAC)..." -ForegroundColor Yellow
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

$RootDir = Split-Path -Parent $PSScriptRoot
$DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"

if (-not (Test-Path $DllPath)) {
    Write-Host "[LỖI] Không tìm thấy $DllPath. Hãy chạy scripts/build-native.ps1 trước." -ForegroundColor Red
    exit 1
}

Write-Host "Đang đăng ký BambooMintKey vào Windows TSF..." -ForegroundColor Cyan
Write-Host "Đường dẫn DLL: $DllPath" -ForegroundColor Gray

# Gọi regsvr32 trong chế độ im lặng
$process = Start-Process regsvr32.exe -ArgumentList "/s `"$DllPath`"" -PassThru -Wait

if ($process.ExitCode -eq 0) {
    Write-Host "Đăng ký thành công!" -ForegroundColor Green
    Write-Host "Hãy kiểm tra thanh Language Bar (Win + Space) để chọn 'BambooMintKey Vietnamese Input'." -ForegroundColor Green
} else {
    Write-Host "[FAIL] regsvr32 thất bại với mã lỗi: $($process.ExitCode)" -ForegroundColor Red
    exit 1
}
```

### 4.3. Kịch bản gỡ đăng ký TIP: `scripts/unregister-tip.ps1`

Dọn sạch đăng ký để tránh khóa file DLL khi cần build lại:  

PowerShell

```c#
# scripts/unregister-tip.ps1
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

$RootDir = Split-Path -Parent $PSScriptRoot
$DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"

if (-not (Test-Path $DllPath)) {
    Write-Host "[CẢNH BÁO] Không tìm thấy $DllPath. Cố gắng hủy qua Registry nếu có..." -ForegroundColor Yellow
}

Write-Host "Đang gỡ đăng ký BambooMintKey khỏi Windows TSF..." -ForegroundColor Cyan

$process = Start-Process regsvr32.exe -ArgumentList "/u /s `"$DllPath`"" -PassThru -Wait

if ($process.ExitCode -eq 0) {
    Write-Host "Gỡ đăng ký thành công!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Gỡ đăng ký thất bại với mã lỗi: $($process.ExitCode)" -ForegroundColor Red
}
```

## 5. Sơ Đồ Cấu Trúc Toàn Bộ Dự Án (Sau Khi Hoàn Tất Phase 2)

```bash
BambooMintKey/
├── src/
│   ├── BambooMintKey.Core/                  # [F#] Pure Telex Engine & Rules
│   │   ├── Domain/
│   │   │   ├── Types.fs
│   │   │   ├── EngineConfig.fs
│   │   │   └── UnicodeTables.fs
│   │   └── Engine/
│   │       ├── SyllableParser.fs
│   │       ├── ModifierRules.fs
│   │       ├── ToneRules.fs
│   │       ├── WordBuffer.fs
│   │       └── TelexEngine.fs
│   │
│   ├── BambooMintKey.NativeBridge/          # [C# NativeAOT] In-Process COM / TSF Server
│   │   ├── BambooMintKey.NativeBridge.csproj
│   │   ├── Exports.cs                      # C-ABI Exports (DllGetClassObject,...)
│   │   ├── Common/
│   │   │   ├── Guids.cs
│   │   │   ├── Constants.cs
│   │   │   └── HResult.cs
│   │   ├── COM/
│   │   │   ├── ComServerState.cs
│   │   │   ├── TextServiceClassFactory.cs
│   │   │   └── ServerRegistrar.cs
│   │   ├── TSF/
│   │   │   ├── ITfTextInputProcessor.cs
│   │   │   ├── ITfThreadMgrEventSink.cs
│   │   │   ├── ITfKeyEventSink.cs
│   │   │   ├── ITfComposition.cs
│   │   │   ├── BambooMintKeyTextService.cs
│   │   │   ├── KeyEventSinkImpl.cs
│   │   │   ├── TextEditSession.cs
│   │   │   ├── CompositionManager.cs
│   │   │   ├── DisplayAttributeHelper.cs
│   │   │   └── BridgeStateManager.cs
│   │   └── Interop/
│   │       ├── NativeMethods.cs
│       ├── NativeCom.cs
│   │       ├── TsfRegistration.cs
│   │       └── KeyInputTranslator.cs
│   │
│   └── BambooMintKey.DevHarness/            # [C# Console] Mock Runner kiểm thử nhanh
│       ├── BambooMintKey.DevHarness.csproj
│       └── Program.cs
│
├── tests/
│   └── BambooMintKey.Core.Tests/            # [F# xUnit] 119 Core Unit Tests
│
├── scripts/
│   ├── build-native.ps1                     # Script publish NativeAOT DLL
│   ├── register-tip.ps1                     # Script đăng ký TSF TIP
│   └── unregister-tip.ps1                   # Script gỡ bỏ TIP
│
└── docs/
    └── 2.Design/
        ├── 002_00_Overview_Architecture.md
        ├── 002_01_COM_Registration_and_Exports.md
        ├── 002_02_TSF_TextInputProcessor_Lifecycle.md
        ├── 002_03_KeyEventSink_and_Core_Interop.md
        ├── 002_04_Composition_and_TextRange.md
        └── 002_05_DevHarness_and_RegistrationScript.md
```

Toàn bộ 5 tài liệu thiết kế kỹ thuật của **Phase 2 (Windows TSF & NativeAOT Integration)** đã được hoàn thiện đầy đủ.

---

## 5. Cập Nhật Theo Triển Khai Thực Tế

### 5.1. Danh Sách Script Hiện Tại

Trong quá trình phát triển, danh sách script đã được điều chỉnh:

| Script | Mục Đích | Quyền |
|--------|----------|-------|
| `scripts/build-native.ps1` | Publish NativeAOT DLL | User |
| `scripts/test-register.ps1` | Đăng ký COM + TSF, tạo registry HKLM | Administrator |
| `scripts/unregister-tip.ps1` | Gỡ đăng ký COM + TSF | Administrator |
| `scripts/enable-tip.ps1` | Kích hoạt TIP cho user hiện tại, thêm vào language list | User |
| `scripts/debug-cocreate.ps1` | Kiểm tra `CoCreateInstance` + `QueryInterface` từ PowerShell | User |
| `scripts/add-license-headers.ps1` | Thêm license header MIT vào source files | User |

Script `register-tip.ps1` đã được thay thế bằng `test-register.ps1` để phân biệt rõ chức năng test/đăng ký.

### 5.2. Quy Trình Đăng Ký Chuẩn

```powershell
# 1. Publish (User)
.\scripts\build-native.ps1

# 2. Đăng ký (Administrator)
pwsh -File scripts/test-register.ps1

# 3. Kích hoạt cho user hiện tại (User)
pwsh -File scripts/enable-tip.ps1

# 4. Restart ctfmon
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Process ctfmon
```

Xem thêm chi tiết tại [002_06_Closure.md](002_06_Closure.md).