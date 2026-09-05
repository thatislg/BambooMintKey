# Tài liệu Thiết kế Kỹ thuật: 004_03_Design_WinGet_Distribution.md

## 1. Tổng quan & Mục tiêu

Tài liệu này đặc tả cơ chế phân phối BambooMintKey thông qua **Windows Package Manager (`winget`)** – giải pháp cài đặt ứng dụng chính thức của Microsoft trên Windows 10 và Windows 11.

Mục tiêu cốt lõi:
* Cho phép người dùng cài đặt, cập nhật và gỡ bỏ BambooMintKey chỉ bằng một dòng lệnh: `winget install BambooMintKey.BambooMintKey`.
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

## 3. Đặc tả Nội dung Bộ Ba Tệp Manifest (v1.9.0)

### 3.1. File Định danh Phiên bản (`BambooMintKey.BambooMintKey.yaml`)

```yaml
# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.9.0.schema.json
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: 1.0.0
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.9.0
```

### 3.2. File Mô tả Ngôn ngữ & Metadata (`BambooMintKey.BambooMintKey.locale.en-US.yaml`)

```yaml
# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.9.0.schema.json
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: 1.0.0
PackageLocale: en-US
Publisher: BambooMintKey Team
PublisherUrl: https://github.com/Kojin/BambooMintKey
PublisherSupportUrl: https://github.com/Kojin/BambooMintKey/issues
PackageName: BambooMintKey
PackageUrl: https://github.com/Kojin/BambooMintKey
License: MIT
LicenseUrl: https://github.com/Kojin/BambooMintKey/blob/main/LICENSE
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
ManifestVersion: 1.9.0
```

### 3.3. File Cấu hình Bộ cài đặt (`BambooMintKey.BambooMintKey.installer.yaml`)

```yaml
# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.9.0.schema.json
PackageIdentifier: BambooMintKey.BambooMintKey
PackageVersion: 1.0.0
MinimumOSVersion: 10.0.19041.0
InstallerType: inno
Scope: machine
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /NORESTART
  SilentWithProgress: /SILENT /NORESTART
  Upgrade: /NORESTART
UpgradeBehavior: install
ElevationRequirement: elevationRequired
AppsAndFeaturesEntries:
  - DisplayName: BambooMintKey
    ProductCode: '{D8A27E4B-4E3F-4A92-805F-294FCE314D01}_is1'
Installers:
  - Architecture: x64
    InstallerUrl: https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe
    InstallerSha256: 0000000000000000000000000000000000000000000000000000000000000000
ManifestType: installer
ManifestVersion: 1.9.0
```

## 4. Quy trình Đăng ký & Tự động hóa Nộp Gói (Submission Pipeline)

### 4.1. Quy trình Thủ công & Kiểm thử Cục bộ (Validation)

Trước khi đưa vào luồng CI/CD, bộ manifest có thể được sinh và kiểm thử trực tiếp trên máy phát triển bằng công cụ chính thức của Microsoft:

```powershell
# 1. Cài đặt công cụ wingetcreate
winget install Microsoft.WingetCreate

# 2. Tạo nhanh cấu trúc manifest từ URL Release (thay v1.0.0 và SHA256 bằng giá trị thực tế)
wingetcreate new `
  https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe

# 3. Kiểm thử cài đặt từ file manifest cục bộ
winget install --manifest .\manifests\b\BambooMintKey\BambooMintKey\1.0.0\

# 4. Xác thực tính hợp lệ của schema YAML
winget validate .\manifests\b\BambooMintKey\BambooMintKey\1.0.0\
```

Tệp manifest mẫu cũng được lưu trong repo tại `manifests/b/BambooMintKey/BambooMintKey/1.0.0/` để dễ chỉnh sửa và kiểm thử.

### 4.2. Tự động hóa qua GitHub Actions (`komac`)

Để không phải tạo PR thủ công mỗi khi ra phiên bản mới, ta tích hợp job tự động vào file `.github/workflows/release.yml`:

```yaml
  winget-submission:
    name: Submit Manifest to WinGet
    needs: build-and-sign
    runs-on: ubuntu-latest
    steps:
      - name: Install Komac
        run: |
          python -m pip install --upgrade pip
          pip install komac

      - name: Update and Submit WinGet Manifest
        env:
          WINGET_TOKEN: ${{ secrets.WINGET_PAT_TOKEN }}
        run: |
          version="${GITHUB_REF_NAME#v}"
          url="https://github.com/${{ github.repository }}/releases/download/${{ github.ref_name }}/BambooMintKey-Setup.exe"
          echo "Updating WinGet manifest for version $version from $url"
          komac update BambooMintKey.BambooMintKey --version "$version" --urls "$url" --token "$WINGET_TOKEN" --submit
```

> **Ghi chú bảo mật:** `WINGET_PAT_TOKEN` là GitHub Personal Access Token (classic) có quyền `public_repo`, được cấp phát để bot có quyền fork repo `microsoft/winget-pkgs`, tạo nhánh mới và mở Pull Request tự động.

Workflow cũng cần quyền `pull-requests: write` trong mục `permissions`.

## 5. Tiêu chí Đánh giá của Bot Kiểm duyệt Microsoft

Khi Pull Request được gửi lên `microsoft/winget-pkgs`, hệ thống kiểm tra tự động của Microsoft sẽ tiến hành quét trong môi trường máy ảo cô lập:

1. **Kiểm tra mã độc:** Quét URL bộ cài qua hệ thống Windows Defender và VirusTotal.
2. **Kiểm tra cài đặt ngầm:** Chạy thử lệnh `/VERYSILENT /NORESTART` để đảm bảo bộ cài không hiển thị cửa sổ hộp thoại nào làm treo tiến trình.
3. **Đối chiếu ProductCode:** Sau khi cài đặt ngầm, kiểm tra khóa `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` xem có khớp chính xác chuỗi `{D8A27E4B-4E3F-4A92-805F-294FCE314D01}_is1` đã khai báo hay không.
4. **Kiểm tra gỡ cài đặt:** Chạy lệnh gỡ cài đặt ngầm (`unins000.exe /SILENT`) và xác nhận không còn tệp dư thừa trong `Program Files`.

> **Lưu ý:** Nếu installer chưa được ký số, bot của Microsoft có thể đánh dấu SmartScreen warning và yêu cầu reviewer xem xét thủ công. Để PR được merge nhanh, nên sử dụng chứng chỉ ký số hợp lệ.

## 6. Hướng dẫn Kiểm thử Trước khi Submit

Trước khi chạy workflow thật, T nên kiểm thử cục bộ:

```powershell
# 1. Build installer local
.\scripts\build-installer.ps1

# 2. Lấy SHA-256 của installer
$hash = (Get-FileHash bin\dist\BambooMintKey-Setup.exe -Algorithm SHA256).Hash
Write-Host $hash

# 3. Giả lập update manifest (thay URL bằng URL release thật khi có)
.\scripts\update-winget-manifest.ps1 `
  -Version "1.0.0" `
  -InstallerUrl "https://github.com/Kojin/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe" `
  -InstallerSha256 $hash

# 4. Validate manifest
winget validate manifests\b\BambooMintKey\BambooMintKey\1.0.0\

# 5. Test cài đặt từ manifest cục bộ
winget install --manifest manifests\b\BambooMintKey\BambooMintKey\1.0.0\
```

Nếu validate và install đều OK, T có thể push tag để workflow tự động chạy.

  