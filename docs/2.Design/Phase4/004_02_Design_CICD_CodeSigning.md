Dưới đây là nội dung chi tiết của file thứ hai: `docs/4.Packaging_Distribution/002_Design_CICD_CodeSigning.md`, tập trung vào **Hạng mục 4.2: Tự động hóa Pipeline CI/CD và Ký số (Code Signing & Windows SmartScreen)**.  

Markdown

# Tài liệu Thiết kế Kỹ thuật: 002_Design_CICD_CodeSigning.md

## 1. Tổng quan & Mục tiêu

Tài liệu này đặc tả quy trình tự động hóa tích hợp và phân phối liên tục (CI/CD) thông qua **GitHub Actions**, kết hợp cơ chế ký số bảo mật (**Code Signing**) cho dự án BambooMintKey.

Do đặc thù `BambooMintKey.NativeBridge.dll` là một In-process Text Service Framework (TSF) DLL được nạp trực tiếp vào không gian bộ nhớ của hầu hết các tiến trình hệ điều hành (kể cả các tiến trình nhạy cảm như `explorer.exe` hay `notepad.exe`), các tệp thực thi nếu không có chữ ký số hợp lệ sẽ bị cơ chế bảo vệ của Windows cảnh báo màu xanh (*Windows SmartScreen*) hoặc bị Windows Defender nhận diện nhầm là mối đe dọa (False-Positive).

---

## 2. Chiến lược Ký số (Code Signing Strategy)

Để đảm bảo tính toàn vẹn của chuỗi phần mềm và vượt qua các bộ lọc kiểm soát mã độc của hệ điều hành, toàn bộ các tệp nhị phân được ký theo một trình tự nghiêm ngặt trước khi đóng gói:

* **Thuật toán chữ ký:** Bắt buộc sử dụng hàm băm an toàn **SHA-256** (`/fd sha256`).
* **Máy chủ đóng dấu thời gian (RFC 3161 Timestamp Server):** Sử dụng các máy chủ chứng thực công khai tiêu chuẩn (ví dụ: `http://timestamp.digicert.com` hoặc `http://timestamp.sectigo.com` qua cờ `/tr http://timestamp.digicert.com /td sha256`). Điều này giúp chữ ký duy trì hiệu lực ngay cả khi chứng chỉ số hết hạn.
* **Thứ tự ký số:**
  1. Ký số tệp thư viện lõi: `BambooMintKey.NativeBridge.dll`.
  2. Ký số tệp giao diện cấu hình: `BambooMintKey.Config.exe`.
  3. Đóng gói bộ cài đặt bằng Inno Setup.
  4. Ký số tệp cài đặt cuối cùng: `BambooMintKey-Setup.exe`.

---

## 3. Kiến trúc Luồng CI/CD (GitHub Actions Pipeline)

Pipeline được kích hoạt tự động mỗi khi có một Git Tag mới bắt đầu bằng tiền tố phiên bản (`v*`), chạy trực tiếp trên môi trường ảo hóa `windows-latest`:

```text
[Tag Push: v1.0.0]
       │
       ▼
┌────────────────────────────────────────┐
│ 1. Checkout & Setup .NET SDK (8.0.x)   │
└────────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────┐
│ 2. Test Execution                      │
│    - Chạy toàn bộ Unit Test F# Core    │
│    - Kiểm tra tính toàn vẹn Phonotactic│
└────────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────┐
│ 3. Build & Publish Binaries            │
│    - NativeAOT: NativeBridge.dll       │
│    - Single-file: Config.exe           │
└────────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────┐
│ 4. Code Signing (SignTool.exe)         │
│    - Ký DLL và EXE qua Secrets         │
└────────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────┐
│ 5. Installer Compilation (Inno Setup)  │
│    - Đóng gói thành BambooMintKey-Setup│
│    - Ký số lại tệp Setup.exe           │
└────────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────┐
│ 6. Hash & Release Asset Generation     │
│    - Trích xuất mã băm SHA-256         │
│    - Đẩy tệp lên GitHub Release        │
└────────────────────────────────────────┘
```

## 4. Kịch bản Tự động hóa Pipeline (`.github/workflows/release.yml`)

Dưới đây là nội dung chi tiết kịch bản GitHub Actions phục vụ phát hành:

YAML

```
name: Release & Code Signing Pipeline

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build-and-sign:
    name: Build, Sign and Package
    runs-on: windows-latest

    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Run Core Test Suite
        run: |
          dotnet test tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj -c Release

      - name: Publish F# NativeBridge (NativeAOT)
        run: |
          dotnet publish src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.fsproj `
            -c Release `
            -r win-x64 `
            -o dist/native

      - name: Publish GUI Config Tool
        run: |
          dotnet publish src/BambooMintKey.Config/BambooMintKey.Config.csproj `
            -c Release `
            -r win-x64 `
            --self-contained false `
            -o dist/app

      - name: Decode Code Signing Certificate
        shell: pwsh
        env:
          CERT_BASE64: ${{ secrets.CODE_SIGN_CERT_BASE64 }}
        run: |
          if (-not [string]::IsNullOrEmpty($env:CERT_BASE64)) {
            [System.IO.File]::WriteAllBytes("cert.pfx", [System.Convert]::FromBase64String($env:CERT_BASE64))
          } else {
            Write-Host "Warning: No Code Signing Certificate found in secrets. Skipping signing."
          }

      - name: Sign Inner Binaries
        shell: pwsh
        env:
          CERT_PASS: ${{ secrets.CODE_SIGN_CERT_PASS }}
        run: |
          if (Test-Path "cert.pfx") {
            & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign `
              /f cert.pfx `
              /p $env:CERT_PASS `
              /tr [http://timestamp.digicert.com](http://timestamp.digicert.com) `
              /td sha256 `
              /fd sha256 `
              dist/native/BambooMintKey.NativeBridge.dll `
              dist/app/BambooMintKey.Config.exe
          }

      - name: Compile Inno Setup Installer
        uses: Minithra/action-inno-setup@v1
        with:
          filepath: setup.iss

      - name: Sign Final Installer
        shell: pwsh
        env:
          CERT_PASS: ${{ secrets.CODE_SIGN_CERT_PASS }}
        run: |
          if (Test-Path "cert.pfx") {
            & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign `
              /f cert.pfx `
              /p $env:CERT_PASS `
              /tr [http://timestamp.digicert.com](http://timestamp.digicert.com) `
              /td sha256 `
              /fd sha256 `
              bin/dist/BambooMintKey-Setup.exe
          }

      - name: Calculate Checksum (SHA-256)
        id: compute_hash
        shell: pwsh
        run: |
          $target = "bin/dist/BambooMintKey-Setup.exe"
          $hash = (Get-FileHash$target -Algorithm SHA256).Hash
          Set-Content -Path "bin/dist/BambooMintKey-Setup.exe.sha256" -Value $hash
          echo "INSTALLER_SHA256=$hash" >> $env:GITHUB_OUTPUT
          Write-Host "Generated SHA256: $hash"

      - name: Upload GitHub Release Assets
        uses: softprops/action-gh-release@v1
        with:
          files: |
            bin/dist/BambooMintKey-Setup.exe
            bin/dist/BambooMintKey-Setup.exe.sha256
          draft: false
          prerelease: false
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## 5. Danh mục Secrets Cần Thiết trên Repository

Để quy trình trên hoạt động tự động và bảo mật, các thông số sau cần được cấu hình trong mục **Settings > Secrets and variables > Actions**:

| **Tên biến Secret**     | **Mô tả dữ liệu**                                            |
| ----------------------- | ------------------------------------------------------------ |
| `CODE_SIGN_CERT_BASE64` | Chuỗi Base64 của tệp chứng chỉ ký số cá nhân/tổ chức (`.pfx`). |
| `CODE_SIGN_CERT_PASS`   | Mật khẩu bảo vệ tệp chứng chỉ `.pfx`.                        |
| `GITHUB_TOKEN`          | Token mặc định do GitHub Actions cấp phát để tải asset lên trang Releases. |

