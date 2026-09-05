**Phase 4: Packaging, Distribution & Deployment** (Đóng gói, Ký số và Phát hành) giải quyết bài toán cốt lõi: một bộ gõ TSF đòi hỏi quyền Admin để ghi Registry hệ thống (`HKLM`), đăng ký COM Server và cài đặt Language Profile, trong khi các kho ứng dụng hiện đại lại ưu tiên sự an toàn và cô lập.

Dưới đây là bức tranh toàn cảnh và các đầu việc cụ thể bạn cần làm trong Phase 4:

### 1. Đặc thù kỹ thuật của bộ gõ TSF khi phát hành

Khác với app thông thường, BambooMintKey bao gồm:

- **`BambooMintKey.NativeBridge.dll`**: DLL in-process được nạp thẳng vào mọi tiến trình Windows (`explorer.exe`, `notepad.exe`, các app sandbox UWP/AppContainer).
- **Đăng ký hệ thống**: Bắt buộc phải đăng ký COM Class trong `HKCR/HKLM` và đăng ký với TSF Category Manager (`ITfInputProcessorProfiles::Register`).

Vì vậy, cách tiếp cận phát hành chia làm **2 nhánh chính**:

1. **Nhánh Installer truyền thống (MSI/Inno Setup)**: Dành cho **Winget**, **Chocolatey**, và tải trực tiếp từ GitHub Releases.
2. **Nhánh Microsoft Store (MSIX / Unpackaged App)**: Đóng gói theo chính sách hiện đại của Microsoft.

### 2. Các đầu việc cụ thể trong Phase 4

#### Việc 1: Xây dựng Bộ cài đặt chuẩn (Installer Engineering)

Thay vì dùng script PowerShell tạm thời, bạn cần một bộ cài đóng gói chuyên nghiệp (khuyên dùng **Inno Setup** hoặc **WiX Toolset**):

- **Cài đặt file**: Copy DLL và binary GUI (`BambooMintKey.Config.exe`) vào `C:\Program Files\BambooMintKey\`.
- **Tự động đăng ký COM & TSF**:
  - Gọi `DllRegisterServer` của `NativeBridge.dll` (hoặc nhúng sẵn các khóa Registry `HKLM\Software\Classes\CLSID\...`).
  - Thực thi đăng ký Profile ID `0x042A` (Vietnamese) với `ITfInputProcessorProfiles`.
- **Gỡ cài đặt sạch sẽ (Clean Uninstall)**: Tự động gọi `DllUnregisterServer`, gỡ Language Profile để không để lại rác trên bàn phím hệ thống của người dùng.

#### Việc 2: Ký số (Code Signing & Windows SmartScreen)

Đây là rào cản lớn nhất khi phân phối phần mềm Windows:

- Nếu không ký số, Windows SmartScreen sẽ chặn màu xanh (*"Windows protected your PC"*), đồng thời trình diệt virus (Windows Defender) rất dễ bắt nhầm (false-positive) do bộ gõ có cơ chế bắt phím.
- **Giải pháp**:
  - Lấy chứng chỉ ký số **Code Signing Certificate** (tiêu chuẩn hoặc EV từ DigiCert, Sectigo...).
  - Tích hợp `signtool.exe` vào luồng build CI/CD (GitHub Actions) để ký cho cả file `.dll`, `.exe` và file installer `.exe`/`.msi`.

#### Việc 3: Phát hành lên WinGet & Chocolatey

Đây là 2 nền tảng dễ triển khai nhất sau khi đã có file installer hoàn chỉnh:

- **WinGet (Windows Package Manager - Chính chủ Microsoft)**:
  - Tạo file manifest YAML theo chuẩn (`bamboomintkey.yaml`, `bamboomintkey.installer.yaml`).
  - Khai báo kiểu installer (Inno/WiX/Nullsoft), mã hash SHA-256, URL download từ GitHub Releases.
  - Mở Pull Request lên repo chính thức `microsoft/winget-pkgs`. Sau khi bot tự động quét virus và kiểm tra cài đặt trong sandbox, package sẽ được duyệt.
  - Người dùng chỉ cần gõ: `winget install BambooMintKey`.

#### Việc 4: Phát hành lên Microsoft Store

Microsoft Store hiện tại cho phép 2 cơ chế:

1. **Phân phối ứng dụng Desktop truyền thống (Win32 / Unpackaged)**:
   - Microsoft Store hiện nay cho phép bạn submit trực tiếp bộ cài Win32 (`.exe` hoặc `.msi`) mà không ép buộc phải chạy trong container UWP.
   - Bạn chỉ cần tài khoản **Microsoft Partner Center** (phí đăng ký cá nhân ~19 USD một lần). Bạn khai báo URL tải installer, hệ thống Store sẽ đóng vai trò là kênh phân phối và cập nhật.
2. **Đóng gói dạng MSIX**:  
   - Nếu muốn tận dụng cơ chế cài đặt 1-click mượt mà của Store, bạn dùng **MSIX Packaging Tool**.
   - *Lưu ý kỹ thuật*: TSF TIP cần khai báo extension COM trong manifest của MSIX để hệ thống nhận diện mà không bị sandbox chặn quyền nạp DLL vào các tiến trình khác.

### 3. Tóm tắt Roadmap cho Phase 4

| **Bước** | **Hạng mục**       | **Công cụ / Deliverable**                                    |
| -------- | ------------------ | ------------------------------------------------------------ |
| **4.1**  | Đóng gói Installer | Script Inno Setup (`setup.iss`) tạo file `BambooMintKey-Setup.exe` (hỗ trợ Silent Install `/SILENT`) |
| **4.2**  | Pipeline CI/CD     | GitHub Actions tự động build NativeAOT, chạy Unit Test F# Core, đóng gói Installer và gắn hash SHA-256 |
| **4.3**  | Đăng ký WinGet     | Manifest PR vào `microsoft/winget-pkgs`                      |
| **4.4**  | Microsoft Store    | Khởi tạo ứng dụng trên Microsoft Partner Center, khai báo Desktop App hoặc gói MSIX |

