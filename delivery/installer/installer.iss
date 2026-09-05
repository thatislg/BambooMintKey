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
; 2. Ứng dụng cấu hình GUI + toàn bộ dependencies publish
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