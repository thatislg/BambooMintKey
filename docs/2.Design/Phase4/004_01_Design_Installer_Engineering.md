# Tài liệu Thiết kế Kỹ thuật: 004_01_Design_Installer_Engineering.md

## 1. Tổng quan & Mục tiêu

Tài liệu này đặc tả cơ chế đóng gói bộ cài đặt chuẩn của BambooMintKey bằng công cụ **Inno Setup**, tạo ra tệp thực thi `BambooMintKey-Setup.exe`. 

Khác với các ứng dụng máy tính thông thường, BambooMintKey là một Text Service hoạt động dựa trên hạ tầng **Text Services Framework (TSF)** của Windows. Do đó, bộ cài đặt phải đảm bảo khả năng can thiệp mức hệ thống, đăng ký an toàn các thành phần COM In-process, thiết lập hồ sơ ngôn ngữ (Language Profile), đồng thời hỗ trợ chế độ cài đặt im lặng (Silent Install) cho các kho quản lý gói tự động.

---

## 2. Yêu cầu Hệ thống & Đặc quyền

* **Quyền hạn cài đặt:** Bắt buộc yêu cầu quyền Quản trị viên (`PrivilegesRequired=admin`) để ghi dữ liệu vào vùng khóa máy `HKLM` và thư mục hệ thống.
* **Thư mục cài đặt mặc định:** `{autopf}\BambooMintKey` (thông thường là `C:\Program Files\BambooMintKey`).
* **Kiến trúc mục tiêu:** Hệ điều hành Windows x64 (hỗ trợ `x64compatible`).
* **Tiêu chuẩn tự động hóa:** Hỗ trợ đầy đủ cờ `/VERYSILENT` và `/NORESTART` để phục vụ quy trình cài đặt ngầm qua WinGet.

---

## 3. Ma trận Tệp tin Phân phối (Distribution Manifest)

Bộ cài đặt chịu trách nhiệm phân phối và đồng bộ các tệp tin sau vào thư mục đích `{app}`:

| Tên tệp tin                      | Nguồn đóng gói                                  | Mục đích sử dụng                                             |
| :------------------------------- | :----------------------------------------------- | :----------------------------------------------------------- |
| `BambooMintKey.dll`              | `publish\win-x64\BambooMintKey.dll`             | DLL TSF In-process (NativeAOT), được nạp trực tiếp vào các tiến trình Windows. |
| `BambooMintKey.UI.exe`           | `publish\ui\BambooMintKey.UI.exe`               | Giao diện người dùng cấu hình tùy chọn bộ gõ.                |
| `bamboomintkey.ico`              | `src\media\bamboomintkey.ico`                  | Biểu tượng ứng dụng trên Start Menu, System Tray và cửa sổ cài đặt. |

---

## 4. Vòng đời Đăng ký TSF & COM (Registration Lifecycle)

Để bộ gõ xuất hiện trên thanh ngôn ngữ (Language Bar) và hoạt động trong các ứng dụng con, bộ cài đặt phải thực thi tuần tự quy trình đăng ký 2 lớp:

### 4.1. Quy trình Cài đặt (Installation Hook)
Trong bước `[Run]`, sau khi sao chép toàn bộ tệp tin, trình cài đặt gọi công cụ hệ thống `regsvr32.exe`:
* Lệnh thực thi: `regsvr32.exe /s "{app}\BambooMintKey.dll"`
* Cơ chế xử lý bên trong `DllRegisterServer`:
  1. Ghi thông tin COM Class ID (`CLSID_BambooMintKeyTextService`) vào khóa `HKCR\CLSID`.
  2. Khởi tạo đối tượng TSF Category Manager (`ITfCategoryMgr`), đăng ký CLSID vào danh mục `GUID_TFCAT_TIP_KEYBOARD`.
  3. Khởi tạo `ITfInputProcessorProfiles`, đăng ký hồ sơ nhập liệu:
     * **LangID:** `0x042A` (Vietnamese).
     * **Profile GUID:** `GUID_BambooMintKeyProfile`.
     * **Mô tả hiển thị:** `"BambooMintKey Vietnamese Input Engine"`.

### 4.2. Quy trình Gỡ cài đặt Sạch sẽ (Clean Uninstallation)
Khi người dùng gỡ phần mềm qua Windows Settings hoặc Control Panel, mục `[UninstallRun]` phải được kích hoạt trước khi xóa tệp tin khỏi đĩa cứng:
* Lệnh thực thi: `regsvr32.exe /u /s "{app}\BambooMintKey.dll"`
* Cơ chế xử lý bên trong `DllUnregisterServer`:
  1. Gọi `ITfInputProcessorProfiles::Unregister` để thu hồi Language Profile `0x042A`.
  2. Xóa liên kết CLSID khỏi `GUID_TFCAT_TIP_KEYBOARD` qua `ITfCategoryMgr::UnregisterCategory`.
  3. Xóa các khóa Registry trong `HKCR\CLSID`.
  4. Đảm bảo khay bàn phím hệ thống không còn mục rác (ghost input indicator).

### 4.3. Tránh restart Windows khi cài đặt / gỡ cài đặt

Vì `BambooMintKey.dll` là một COM In-process server, tiến trình `ctfmon.exe` (CTF Loader của Windows) có thể đã nạp DLL cũ. Nếu thay thế file khi DLL đang bị lock, Inno Setup sẽ yêu cầu restart máy. Để tránh điều này:

* Trước khi cài đặt (`ssInstall`): tắt `ctfmon.exe` bằng `taskkill /f /im ctfmon.exe` trong `[Code]\CurStepChanged`.
* Sau khi đăng ký TSF (`[Run]`): khởi động lại `ctfmon.exe` để bộ gõ có hiệu lực ngay lập tức.
* Trước khi gỡ cài đặt (`usUninstall`): tắt `ctfmon.exe` trong `[Code]\CurUninstallStepChanged` để DLL có thể unregister và xóa sạch.
* Thiết lập `RestartIfNeededByRun=no` trong `[Setup]`.

---

## 5. Kịch bản Đóng gói Inno Setup (`installer.iss`)

Biểu tượng `bamboomintkey.ico` được tạo từ `src/media/rendered_v_64x64.png` (PNG 64×64) với các kích thước 16×16, 24×24, 32×32, 48×48 và 64×64, phục vụ cho cửa sổ cài đặt, uninstaller, Start Menu và System Tray.

Dưới đây là định nghĩa kịch bản đầy đủ dùng để biên dịch bộ cài đặt:

```iss
#define MyAppName "BambooMintKey"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BambooMintKey Team"
#define MyAppURL "[https://github.com/Kojin/BambooMintKey](https://github.com/Kojin/BambooMintKey)"
#define MyAppExeName "BambooMintKey.UI.exe"

[Setup]
; GUID định danh duy nhất cho bộ cài đặt trong Windows Registry
AppId={{D8A27E4B-4E3F-4A92-805F-294FCE314D01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; Icon cho cửa sổ cài đặt, uninstaller và Installed Apps
SetupIconFile=..\..\src\media\bamboomintkey.ico
UninstallDisplayIcon={app}\bamboomintkey.ico
OutputDir=..\..\bin\dist
OutputBaseFilename=BambooMintKey-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; Không bắt buộc restart Windows; chúng ta tự quản lý CTF Loader
RestartIfNeededByRun=no

[Files]
; 1. Lõi NativeAOT Engine & TSF COM Server
Source: "..\..\publish\win-x64\BambooMintKey.dll"; DestDir: "{app}"; Flags: ignoreversion restartreplace uninsrestartdelete
; 2. Ứng dụng cấu hình GUI + toàn bộ dependencies publish (bỏ các thư viện cross-platform không dùng trên Windows)
Source: "..\..\publish\ui\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "Avalonia.FreeDesktop.dll,Avalonia.FreeDesktop.AtSpi.dll,Avalonia.Vulkan.dll,Avalonia.X11.dll,Tmds.DBus.Protocol.dll"
; 3. Biểu tượng ứng dụng
Source: "..\..\src\media\bamboomintkey.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\bamboomintkey.ico"

[Run]
; Kích hoạt DllRegisterServer để đưa TIP vào hệ thống TSF
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\BambooMintKey.dll"""; StatusMsg: "Đang đăng ký Text Services Framework Profile..."; Flags: runhidden
; Khởi động lại CTF Loader để bộ gõ có hiệu lực ngay mà không cần restart máy
Filename: "{sys}\ctfmon.exe"; Description: "Kích hoạt bộ gõ ngay"; Flags: nowait postinstall skipifsilent runhidden
; Mở ứng dụng cấu hình sau khi cài đặt
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kích hoạt DllUnregisterServer để dọn sạch TSF Profile trước khi xóa tệp tin
Filename: "{sys}\regsvr32.exe"; Parameters: "/u /s ""{app}\BambooMintKey.dll"""; Flags: runhidden
; Khởi động lại CTF Loader để icon ghost biến mất nhanh hơn
Filename: "{sys}\ctfmon.exe"; Flags: runhidden

[Code]
procedure StopCtfmon;
var
  ResultCode: Integer;
begin
  // Dùng /fi để taskkill không trả về lỗi khi ctfmon.exe chưa chạy
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /fi ""IMAGENAME eq ctfmon.exe""', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Tắt CTF Loader trước khi copy DLL, tránh bị lock và tránh bắt buộc restart Windows
    StopCtfmon;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Tắt CTF Loader trước khi gỡ cài đặt để DLL có thể xóa sạch
    StopCtfmon;
  end;
end;
```

## 6. Script tự động hóa Build (`scripts/build-installer.ps1`)

Để không phải gõ tay nhiều lệnh, script PowerShell sau đây thực hiện toàn bộ quy trình: build NativeAOT bridge, publish GUI, rồi gọi Inno Setup Compiler (`ISCC.exe`).

```powershell
# scripts/build-installer.ps1
[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$UiOutputDir = Join-Path $RootDir "publish\ui"
$InstallerScript = Join-Path $RootDir "delivery\installer\installer.iss"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "  BambooMintKey Installer Build" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Build NativeAOT TSF COM DLL (output: publish\win-x64\BambooMintKey.dll)
$BuildNativeScript = Join-Path $RootDir "scripts\build-native.ps1"
Write-Host "[1/3] Building NativeAOT TSF bridge..." -ForegroundColor Yellow
& $BuildNativeScript -Configuration $Configuration -Runtime $Runtime

# 2. Publish Avalonia UI app (output: publish\ui\)
$UiProject = Join-Path $RootDir "src\BambooMintKey.UI\BambooMintKey.UI.fsproj"
Write-Host "[2/3] Publishing configuration GUI..." -ForegroundColor Yellow
if (Test-Path $UiOutputDir) {
    Remove-Item -Path $UiOutputDir -Recurse -Force
}
dotnet publish $UiProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $UiOutputDir

# 3. Compile Inno Setup installer
Write-Host "[3/3] Compiling installer with Inno Setup..." -ForegroundColor Yellow
$Iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
if (-not $Iscc) {
    $IsccFallback = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $IsccFallback) {
        $Iscc = $IsccFallback
    } else {
        Write-Host "[ERROR] Inno Setup compiler (ISCC.exe) not found." -ForegroundColor Red
        Write-Host "        Please install Inno Setup 6 or add ISCC.exe to PATH." -ForegroundColor Red
        exit 1
    }
}

& $Iscc $InstallerScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Installer compilation failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

$OutputExe = Join-Path $RootDir "bin\dist\BambooMintKey-Setup.exe"
if (Test-Path $OutputExe) {
    $size = (Get-Item $OutputExe).Length / 1MB
    Write-Host "----------------------------------------------------" -ForegroundColor Green
    Write-Host "  Installer built successfully!" -ForegroundColor Green
    Write-Host "  $OutputExe ($([Math]::Round($size, 2)) MB)" -ForegroundColor Green
    Write-Host "----------------------------------------------------" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Installer output not found at $OutputExe" -ForegroundColor Red
    exit 1
}

```

Cách chạy:

```powershell
.\scripts\build-installer.ps1
```

Sau khi chạy xong, tệp cài đặt nằm tại `bin\dist\BambooMintKey-Setup.exe`.

### Ghi chú về gỡ cài đặt

Inno Setup **tự động** tạo trình gỡ cài đặt (uninstaller) dựa trên mục `[UninstallRun]` trong script. Khi người dùng gỡ phần mềm qua Windows Settings / Control Panel, uninstaller sẽ chạy `regsvr32.exe /u /s` để dọn dẹp TSF profile trước khi xóa các tệp tin trong `{app}`. Không cần viết thêm công cụ gỡ riêng.