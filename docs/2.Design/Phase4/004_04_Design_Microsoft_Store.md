Dưới đây là nội dung chi tiết của file cuối cùng trong Phase 4: `docs/4.Packaging_Distribution/004_Design_Microsoft_Store.md`, tập trung vào **Hạng mục 4.4: Phát hành lên Microsoft Store**.  

Markdown

```
# Tài liệu Thiết kế Kỹ thuật: 004_Design_Microsoft_Store.md

## 1. Tổng quan & Thách thức Kiến trúc

Tài liệu này đặc tả phương thức phát hành BambooMintKey lên **Microsoft Store** – kênh phân phối chính thức của Windows với hàng trăm triệu người dùng.

### Thách thức Kỹ thuật của TSF Input Method trên Store:
* Các ứng dụng Store truyền thống (UWP/AppContainer thuần) bị cách ly trong môi trường sandbox nghiêm ngặt, cấm ghi khóa máy `HKLM` và cấm nạp mã nhị phân vào tiến trình khác.
* Ngược lại, bộ gõ tiếng Việt dựa trên Text Services Framework (TSF) bắt buộc phải nạp DLL (`BambooMintKey.NativeBridge.dll`) dưới dạng In-process Server vào mọi tiến trình Windows (`explorer.exe`, trình duyệt, ứng dụng văn phòng).

Để giải quyết mâu thuẫn này, tài liệu thiết kế hai phương án phân phối phù hợp với chính sách mở của Microsoft Store:
1. **Phương án 1 (Ưu tiên): Phân phối dạng Ứng dụng Desktop Truyền thống (Win32 / Unpackaged App)**.
2. **Phương án 2 (Mở rộng): Đóng gói dạng MSIX với quyền Full Trust và COM Server Extension**.

---

## 2. Phương án 1: Win32 Desktop App (Unpackaged) - Chiến lược Ưu tiên

Chính sách hiện đại của Microsoft Store cho phép nộp trực tiếp các bộ cài Win32 tiêu chuẩn (`.exe` hoặc `.msi`) mà không ép buộc phải đóng gói vào container hay sửa đổi mã nguồn.

### 2.1. Cơ chế Hoạt động
* **Mô hình:** Store đóng vai trò là danh mục khám phá (Discovery), quản lý bản quyền, đánh giá (Reviews) và là kênh phân phối metadata.
* **Kênh tải tệp:** Microsoft Store trỏ trực tiếp đến URL của tệp cài đặt `BambooMintKey-Setup.exe` được host trên GitHub Releases của dự án.
* **Quá trình cài đặt:** Ứng dụng Store Client trên máy người dùng sẽ tải bộ cài về và kích hoạt cài đặt ngầm:
  ```text
  BambooMintKey-Setup.exe /VERYSILENT /NORESTART
```

- **Cập nhật:** Bộ cài đặt tự quản lý việc nâng cấp hoặc thông qua thông báo từ ứng dụng cấu hình (`BambooMintKey.Config.exe`).

### 2.2. Quy trình Nộp Ứng dụng trên Microsoft Partner Center

1. **Tài khoản nhà phát triển:** Đăng ký tài khoản cá nhân hoặc tổ chức tại [Microsoft Partner Center](https://www.google.com/search?q=https://partner.microsoft.com/dashboard).  
2. **Đặt trước tên ứng dụng (Reserve App Name):** Đăng ký tên `BambooMintKey`.
3. **Khai báo loại gói sản phẩm:**
   - Chọn loại hình: **Win32 App / Traditional Desktop Application**.  
   - Cung cấp URL tải bộ cài đặt: Link trực tiếp tới `BambooMintKey-Setup.exe` tại GitHub Release mới nhất.  
   - Khai báo tham số cài đặt im lặng (Silent Install Parameters): `/VERYSILENT /NORESTART`.
   - Khai báo tham số gỡ cài đặt (Silent Uninstall Parameters): `/VERYSILENT /NORESTART`.
4. **Khai báo quyền hạn hệ thống:**
   - Nêu rõ lý do ứng dụng yêu cầu quyền Administrator: *"Cần quyền ghi khóa Registry HKLM để đăng ký Text Services Framework (TSF) TIP và COM In-process Server cho bộ gõ tiếng Việt"*.  

## 3. Phương án 2: Đóng gói MSIX (Dành cho Cài đặt 1-Click)

Nếu dự án muốn tận dụng trải nghiệm cài đặt mượt mà của gói MSIX (tự động cập nhật qua hạ tầng Store, cài đặt không hiện cửa sổ UAC của installer), kiến trúc cần sử dụng mô hình **Desktop Bridge (Centennial) kết hợp COM Server Extension**.  

### 3.1. Cấu trúc Gói Phân phối MSIX Layout

Plaintext

```
PackageRoot/
├── AppxManifest.xml             # Khai báo quyền Full Trust và COM Server
├── BambooMintKey.Config.exe      # Ứng dụng cấu hình GUI (Entry point)
├── BambooMintKey.NativeBridge.dll # TSF TIP DLL (In-process COM)
├── Assets/                      # Bộ logo/biểu tượng theo chuẩn Store
│   ├── Square44x44Logo.png
│   ├── Square150x150Logo.png
│   └── StoreLogo.png
└── resources.pri                # Tệp tài nguyên biên dịch (MakePri.exe)
```

### 3.2. Cấu hình Manifest Đặc biệt (`AppxManifest.xml`)

Để Windows cho phép DLL được nạp ngoài tiến trình của gói ứng dụng, ta phải khai báo extension `windows.comServer`:  

XML

```
<?xml version="1.0" encoding="utf-8"?>
<Package IgnorableNamespaces="uap rescap com" xmlns="[http://schemas.microsoft.com/appx/manifest/foundation/windows10](http://schemas.microsoft.com/appx/manifest/foundation/windows10)" xmlns:com="[http://schemas.microsoft.com/appx/manifest/com/windows10](http://schemas.microsoft.com/appx/manifest/com/windows10)" xmlns:rescap="[http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities](http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities)" xmlns:uap="[http://schemas.microsoft.com/appx/manifest/uap/windows10](http://schemas.microsoft.com/appx/manifest/uap/windows10)">

  <Identity Name="BambooMintKeyTeam.BambooMintKey" ProcessorArchitecture="x64" Publisher="CN=YOUR-PUBLISHER-ID" Version="1.0.0.0"/>

  <Properties>
    <DisplayName>BambooMintKey</DisplayName>
    <PublisherDisplayName>BambooMintKey Team</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily MaxVersionTested="10.0.22621.0" MinVersion="10.0.19041.0" Name="Windows.Desktop"/>
  </Dependencies>

  <Capabilities>
    <!-- Bắt buộc để thực thi mã máy ngoài Sandbox -->
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>

  <Applications>
    <Application EntryPoint="Windows.FullTrustApplication" Executable="BambooMintKey.Config.exe" Id="BambooMintKey">
      <uap:VisualElements DisplayName="BambooMintKey"
                          Description="Modern Vietnamese Input Method Engine"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png"
                          BackgroundColor="transparent" />
      <Extensions>
        <!-- Khai báo In-process COM Server để TSF nhận diện TIP DLL -->
        <com:Extension Category="windows.comServer">
          <com:ComServer>
            <com:InProcessServer Path="BambooMintKey.NativeBridge.dll" ImplementationType="Native">
              <com:Class Id="D8A27E4B-4E3F-4A92-805F-294FCE314D01" 
                         ThreadingModel="Both" 
                         DisplayName="BambooMintKey Text Service" />
            </com:InProcessServer>
          </com:ComServer>
        </com:Extension>
      </Extensions>
    </Application>
  </Applications>
</Package>
```

### 3.3. Lệnh Đóng gói và Ký số Kiểm thử Cục bộ (PowerShell)

PowerShell

```
# 1. Đóng gói thư mục thành tệp .msix
MakeAppx.exe pack /d .\PackageRoot /p .\BambooMintKey_1.0.0.0_x64.msix

# 2. Ký số tệp gói bằng chứng chỉ thử nghiệm nội bộ
SignTool.exe sign /fd SHA256 /a /f .\DevCert.pfx /p "SecretPassword" .\BambooMintKey_1.0.0.0_x64.msix

# 3. Cài đặt thử nghiệm lên máy cá nhân
Add-AppxPackage .\BambooMintKey_1.0.0.0_x64.msix
```

## 4. Bảng So sánh & Quyết định Triển khai

| **Tiêu chí**           | **Phương án 1: Win32 Unpackaged (Khuyên dùng)**             | **Phương án 2: Đóng gói MSIX**                        |
| ---------------------- | ----------------------------------------------------------- | ----------------------------------------------------- |
| **Độ tương thích TSF** | Tuyệt đối 100% (hoạt động giống UniKey, EVKey).             | Phụ thuộc vào cơ chế ánh xạ COM ảo của Windows 10/11. |
| **Cài đặt ngầm**       | Thực thi qua cờ `/VERYSILENT` của Inno Setup.               | 1-Click native từ Store không hiện hộp thoại.         |
| **Chi phí bảo trì**    | Rất thấp (dùng chung file setup với WinGet/GitHub Release). | Cao hơn (phải duy trì thêm Manifest và build layout). |
| **Kiểm duyệt Store**   | Dễ dàng (Microsoft chỉ quét link installer).                | Chặt chẽ hơn do yêu cầu quyền `runFullTrust`.         |

## 5. Kết luận & Lộ trình Thực hiện Phase 4

- **Ngắn hạn (v1.0.0):** Triển khai toàn diện **Phương án 1 (Win32 Unpackaged)** cùng lúc với việc submit lên **WinGet**. Điều này giúp tiết kiệm tối đa thời gian phát hành và giảm thiểu rủi ro kỹ thuật liên quan đến sandbox của Windows.
- **Dài hạn (v1.x):** Khi cấu trúc TSF COM đã ổn định hoàn toàn, sẽ nghiên cứu đóng gói MSIX Sparse Package để tối ưu hóa trải nghiệm người dùng cuối trên Microsoft Store.

