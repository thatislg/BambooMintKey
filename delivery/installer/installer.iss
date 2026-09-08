#define MyAppName "BambooMintKey"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BambooMintKey Team"
#define MyAppURL "[https://github.com/thatislg/BambooMintKey](https://github.com/thatislg/BambooMintKey)"
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
Source: "..\..\publish\ui\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "Avalonia.FreeDesktop.dll,Avalonia.FreeDesktop.AtSpi.dll,Avalonia.Vulkan.dll,Avalonia.X11.dll,Tmds.DBus.Protocol.dll,*.pdb,*.xml,*.dbg,*.exp,*.lib"
; 3. Biểu tượng ứng dụng
Source: "..\..\src\media\bamboomintkey.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\bamboomintkey.ico"

[Run]
; Kích hoạt DllRegisterServer để đưa TIP vào hệ thống TSF
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\BambooMintKey.dll"""; StatusMsg: "Đang đăng ký Text Services Framework Profile..."; Flags: runhidden
; Mở ứng dụng cấu hình sau khi cài đặt
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kích hoạt DllUnregisterServer để dọn sạch TSF Profile trước khi xóa tệp tin
Filename: "{sys}\regsvr32.exe"; Parameters: "/u /s ""{app}\BambooMintKey.dll"""; StatusMsg: "Đang gỡ đăng ký Text Services Framework Profile..."; Flags: runhidden

[Code]
function IsCtfmonRunning: Boolean;
var
  ResultCode: Integer;
begin
  // tasklist trả về 0 nếu tìm thấy process, 1 nếu không
  Result := (Exec(ExpandConstant('{sys}\tasklist.exe'), '/fi "IMAGENAME eq ctfmon.exe" /fo csv /nh', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0));
end;

procedure StopCtfmon;
var
  ResultCode: Integer;
begin
  if IsCtfmonRunning then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im ctfmon.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure StartCtfmon;
var
  ResultCode: Integer;
  CtfmonPath: String;
begin
  CtfmonPath := ExpandConstant('{sys}\ctfmon.exe');
  if FileExists(CtfmonPath) then
  begin
    if not Exec(CtfmonPath, '', '', SW_HIDE, ewNoWait, ResultCode) then
    begin
      Log('Không thể khởi động lại CTF Loader; bộ gõ sẽ có hiệu lực sau khi đăng nhập lại Windows.');
    end;
  end else
  begin
    Log('Không tìm thấy ctfmon.exe; bỏ qua việc khởi động lại CTF Loader.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Tắt CTF Loader trước khi copy DLL, tránh bị lock và tránh bắt buộc restart Windows
    StopCtfmon;
  end;
  if CurStep = ssPostInstall then
  begin
    // Khởi động lại CTF Loader sau khi đăng ký TSF để bộ gõ có hiệu lực ngay
    StartCtfmon;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Tắt CTF Loader trước khi gỡ cài đặt để DLL có thể unregister và xóa sạch
    StopCtfmon;
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    // Khởi động lại CTF Loader sau khi gỡ để cập nhật Language Bar
    StartCtfmon;
  end;
end;
