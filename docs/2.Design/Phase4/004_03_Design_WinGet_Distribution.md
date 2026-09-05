Dưới đây là nội dung chi tiết của file thứ ba: `docs/4.Packaging_Distribution/003_Design_WinGet_Distribution.md`, tập trung vào **Hạng mục 4.3: Phát hành lên WinGet & Chocolatey**.  

Markdown

```
# Tài liệu Thiết kế Kỹ thuật: 003_Design_WinGet_Distribution.md

## 1. Tổng quan & Mục tiêu

Tài liệu này đặc tả cơ chế phân phối BambooMintKey thông qua **Windows Package Manager (`winget`)** – giải pháp cài đặt ứng dụng chính thức của Microsoft trên Windows 10 và 11.

Mục tiêu cốt lõi:
* Cho phép người dùng cài đặt, cập nhật và gỡ cài đặt BambooMintKey chỉ bằng một dòng lệnh duy nhất (`winget install BambooMintKey`).
* Chuẩn hóa cấu trúc manifest tuân thủ tiêu chuẩn của repository `microsoft/winget-pkgs`.
* Tự động hóa việc tạo và nộp Pull Request lên kho gói của Microsoft sau khi có bản Release mới trên GitHub.

---

## 2. Cấu trúc Thư mục Manifest Chuẩn

Trên kho lưu trữ `microsoft/winget-pkgs`, các gói phần mềm được tổ chức theo quy tắc phân cấp ba cấp độ dựa trên định danh gói (`PackageIdentifier`):

```text
manifests/
└── b/
    └── BambooMintKey/
        └── BambooMintKey/
            └── 1.0.0/
                ├── BambooMintKey.BambooMintKey.yaml
                ├── BambooMintKey.BambooMintKey.installer.yaml
                └── BambooMintKey.BambooMintKey.locale.en-US.yaml
```

- **Cấp 1 (`b/`):** Chữ cái đầu tiên viết thường của nhà phát triển hoặc tên sản phẩm.
- **Cấp 2 (`BambooMintKey/`):** Tên định danh nhà phát hành (`Publisher`).
- **Cấp 3 (`BambooMintKey/`):** Tên sản phẩm phần mềm (`PackageName`).
- **Cấp 4 (`1.0.0/`):** Thư mục phiên bản phát hành cụ thể (`PackageVersion`).

## 3. Đặc tả Nội dung Bộ Ba Tệp Manifest

### 3.1. File Định danh Phiên bản (`BambooMintKey.BambooMintKey.yaml`)



Xác định số hiệu phiên bản và thiết lập ngôn ngữ mặc định:

YAML

```
# yaml-language-server: $schema=[https://aka.ms/winget-manifest.version.1.6.0.schema.json](https://aka.ms/winget-manifest.version.1.6.0.schema.json)
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: 1.0.0
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
```

### 3.2. File Mô tả Ngôn ngữ & Metadata (`BambooMintKey.BambooMintKey.locale.en-US.yaml`)

Khai báo toàn bộ thông tin hiển thị, giấy phép, trang chủ và từ khóa tìm kiếm khi người dùng gõ `winget search bamboomintkey`:

YAML

```
# yaml-language-server: $schema=[https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json](https://aka.ms/winget-manifest.defaultLocale.1.6.0.schema.json)
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: 1.0.0
PackageLocale: en-US
Publisher: BambooMintKey Team
PublisherUrl: [https://github.com/Kojin/BambooMintKey](https://github.com/Kojin/BambooMintKey)
PublisherSupportUrl: [https://github.com/Kojin/BambooMintKey/issues](https://github.com/Kojin/BambooMintKey/issues)
PackageName: BambooMintKey
PackageUrl: [https://github.com/Kojin/BambooMintKey](https://github.com/Kojin/BambooMintKey)
License: MIT
LicenseUrl: [https://github.com/Kojin/BambooMintKey/blob/main/LICENSE](https://github.com/Kojin/BambooMintKey/blob/main/LICENSE)
Copyright: Copyright (c) 2026 BambooMintKey Team
ShortDescription: Modern Vietnamese Input Method Engine powered by F# NativeAOT and Text Services Framework.
Description: |
  BambooMintKey is an open-source, high-performance Vietnamese Input Method Engine (IME) 
  designed for Windows. It features an F# NativeAOT core implementing a formal 5-tuple 
  phonotactic model, native TSF integration, zero GC latency, and advanced English detection heuristics.
Moniker: bamboomintkey
Tags:
  - ime
  - vietnamese
  - tsf
  - telex
  - vni
  - input-method
ManifestType: defaultLocale
ManifestVersion: 1.6.0
```

### 3.3. File Cấu hình Bộ cài đặt (`BambooMintKey.BambooMintKey.installer.yaml`)



Khai báo đường dẫn tải tệp thực thi, mã băm bảo mật SHA-256, cờ cài đặt im lặng và mã đăng ký gỡ bỏ:

YAML

```
# yaml-language-server: $schema=[https://aka.ms/winget-manifest.installer.1.6.0.schema.json](https://aka.ms/winget-manifest.installer.1.6.0.schema.json)
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: 1.0.0
InstallerType: inno
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /NORESTART
  SilentWithProgress: /SILENT /NORESTART
UpgradeBehavior: install
Scope: machine
Installers:
  - Architecture: x64
    InstallerUrl: [https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe](https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe)
    InstallerSha256: 0000000000000000000000000000000000000000000000000000000000000000 # Thay thế bằng mã SHA-256 thực tế
AppsAndFeaturesEntries:
  - DisplayName: BambooMintKey
    ProductCode: '{D8A27E4B-4E3F-4A92-805F-294FCE314D01}_is1'
ManifestType: installer
ManifestVersion: 1.6.0
```

## 4. Quy trình Đăng ký & Tự động hóa Nộp Gói (Submission Pipeline)

### 4.1. Quy trình Thủ công & Kiểm thử Cục bộ (Validation)

Trước khi đưa vào luồng CI/CD, bộ manifest có thể được sinh và kiểm thử trực tiếp trên máy phát triển bằng công cụ chính thức của Microsoft:

PowerShell

```
# 1. Cài đặt công cụ wingetcreate
winget install Microsoft.WingetCreate

# 2. Tạo nhanh cấu trúc manifest từ URL Release
wingetcreate new [https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe](https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe)

# 3. Kiểm thử cài đặt từ file manifest cục bộ trong môi trường sandbox
winget test --manifests .\manifests\b\BambooMintKey\BambooMintKey\1.0.0\

# 4. Xác thực tính hợp lệ của schema YAML
winget validate .\manifests\b\BambooMintKey\BambooMintKey\1.0.0\
```

### 4.2. Tự động hóa qua GitHub Actions (`winget-releaser`)

Để không phải tạo PR thủ công mỗi khi ra phiên bản mới, ta tích hợp job tự động vào cuối file `.github/workflows/release.yml`:

YAML

```
  winget-submission:
    name: Submit Manifest to WinGet
    needs: build-and-sign
    runs-on: ubuntu-latest
    steps:
      - name: Submit to microsoft/winget-pkgs
        uses: vedantmgoyal2009/winget-releaser@v2
        with:
          identifier: BambooMintKey.BambooMintKey
          version: ${{ github.ref_name }}
          installer-url: [https://github.com/Kojin/BambooMintKey/releases/download/$](https://github.com/Kojin/BambooMintKey/releases/download/$){{ github.ref_name }}/BambooMintKey-Setup.exe
          token: ${{ secrets.WINGET_PAT_TOKEN }}
```

> **Ghi chú bảo mật:** `WINGET_PAT_TOKEN` là GitHub Personal Access Token (classic) có quyền `public_repo`, được cấp phát để bot có quyền fork repo `microsoft/winget-pkgs`, tạo nhánh mới và mở Pull Request tự động.

## 5. Tiêu chí Đánh giá của Bot Kiểm duyệt Microsoft

Khi Pull Request được gửi lên `microsoft/winget-pkgs`, hệ thống kiểm tra tự động của Microsoft sẽ tiến hành quét trong môi trường máy ảo cô lập:

1. **Kiểm tra mã độc:** Quét URL bộ cài qua hệ thống Windows Defender và VirusTotal.
2. **Kiểm tra cài đặt ngầm:** Chạy thử lệnh `/VERYSILENT /NORESTART` để đảm bảo bộ cài không hiển thị cửa sổ hộp thoại nào làm treo tiến trình.
3. **Đối chiếu ProductCode:** Sau khi cài đặt ngầm, kiểm tra khóa `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` xem có khớp chính xác chuỗi `{D8A27E4B-4E3F-4A92-805F-294FCE314D01}_is1` đã khai báo hay không.
4. **Kiểm tra gỡ cài đặt:** Chạy lệnh gỡ cài đặt ngầm và xác nhận không còn tệp dư thừa trong `Program Files`.

Sau khi toàn bộ các bài kiểm tra tự động đạt trạng thái xanh (Passed), Pull Request sẽ được merge tự động hoặc bởi người duyệt trong vòng 24–48 giờ.

  