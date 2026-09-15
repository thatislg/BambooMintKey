<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Tính Năng: Bộ Cài MSIX & Đưa Lên Microsoft Store

**Mã tài liệu:** `006_00_MSIX_Store_Packaging`

**Tài liệu:** `docs/2.Design/Phase6/006_00_MSIX_Store_Packaging.md`

**Giai đoạn:** Phase 6 - Phân Phối & Store

**Trạng thái:** 📝 Đang thiết kế (Draft Outline)

**Chế độ triển khai:** Chỉ thiết kế quy trình, **chưa được phép viết code**.

---

## 1. Tóm tắt vấn đề

BambooMintKey hiện phân phối qua bộ cài Inno Setup (`BambooMintKey-Setup.exe`) trên GitHub Release. Để:

- Tiếp cận người dùng rộng hơn,
- Được quảng bá trên Microsoft Store,
- Tận dụng cơ chế cập nhật tự động qua Store,
- Tăng độ tin cậy (signed bởi Microsoft Store),

cần tạo thêm **gói MSIX** và upload lên **Microsoft Store**.

> **Thách thức chính:** TSF Text Service (`BambooMintKey.dll`) yêu cầu đăng ký COM/Registry toàn máy (`HKEY_LOCAL_MACHINE`), trong khi MSIX là container sandbox và bị giới hạn ghi registry / cài đặt toàn hệ thống. Cần thiết kế pipeline để MSIX vẫn có thể đăng ký TSF hợp lệ.

---

## 2. Mục tiêu kỹ thuật

1. **Tạo được gói MSIX (`*.msix` / `*.msixbundle`)** từ cùng artifact publish (NativeAOT DLL + UI).
2. **Hỗ trợ cả 2 kênh phân phối song song:**
   - GitHub Release với Inno Setup (như hiện tại).
   - Microsoft Store với MSIX.
3. **TSF COM Server vẫn hoạt động đúng** sau khi cài từ MSIX (đăng ký COM/Category/registry cần thiết).
4. **Tự động hóa toàn bộ quy trình:** build → sign → package → upload, có thể chạy từ GitHub Actions.
5. **Quản lý phiên bản đồng nhất:** version trong MSIX manifest khớp với version của installer `.exe`.

---

## 3. Phạm vi (In Scope / Out of Scope)

### 3.1. In Scope

- Đóng gói MSIX cho BambooMintKey.UI và BambooMintKey.dll.
- Manifest `AppxManifest.xml` với capability thích hợp.
- Đăng ký TSF COM Server trong MSIX context.
- Script/Pipeline build MSIX (local + CI).
- Chuẩn bị tài khoản Microsoft Store & Partner Center.
- Quy trình upload & publish MSIX.

### 3.2. Out of Scope (Phase 6+)

- Thay thế hoàn toàn Inno Setup (vẫn giữ song song).
- Auto-update tùy chỉnh bên ngoài Store.
- Phân phối qua winget (có thể là phase khác).

---

## 4. Kiến trúc & Luồng Hoạt Động

### 4.1. Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          GitHub Actions / Local Build                     │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐  │
│  │ Build DLL   │ → │ Publish UI  │ → │ Pack MSIX   │ → │ Sign MSIX   │  │
│  │ NativeAOT   │   │ Avalonia    │   │ + Manifest  │   │ Store cert  │  │
│  └─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
                    ┌────────────────────────────┐
                    │  Partner Center Portal      │
                    │  - Tạo submission mới       │
                    │  - Upload MSIX / MSIX bundle  │
                    │  - Điền metadata, screenshot  │
                    │  - Submit for certification   │
                    └────────────────────────────┘
                                   │
                                   ▼
                    ┌────────────────────────────┐
                    │  Microsoft Store             │
                    │  - Certification             │
                    │  - Public / Private release  │
                    └────────────────────────────┘
```

### 4.2. Các thành phần trong MSIX package

| Thành phần | Vai trò |
|---|---|
| `BambooMintKey.dll` | TSF Text Service COM Server (NativeAOT) |
| `BambooMintKey.UI.exe` | Ứng dụng cấu hình GUI |
| `bamboomintkey.ico` | Icon ứng dụng |
| `AppxManifest.xml` | Metadata, identity, capabilities, COM extension |
| `PackageLayout.xml` *(tùy chọn)* | Định nghĩa cấu trúc package cho MakeAppx |

---

## 5. Thách thức kỹ thuật chính

### 5.1. TSF COM Server + MSIX Sandbox

- MSIX không cho phép app ghi `HKEY_LOCAL_MACHINE`.
- COM Server cần được đăng ký qua **Sparse Package** hoặc **MSIX với registry virtualization**.
- Hoặc dùng **Packaged COM Extension** trong `AppxManifest.xml` để khai báo COM class thay vì ghi registry trực tiếp.

### 5.2. Đăng ký TSF Categories

- `ITfCategoryMgr::RegisterCategory` ghi vào `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\CTF\TIP`.
- Có thể cần **Sparse Package** hoặc ** Centennial / Full Trust** để gọi đăng ký này lúc first-run.
- Hoặc dùng **MSIX with custom install action** (không khuyến khích, bị giới hạn).

### 5.3. Khởi động cùng Windows

- Tùy chọn `StartWithWindows` ghi `HKEY_CURRENT_USER\...\Run`.
- MSIX cho phép ghi `HKCU` qua virtualization, nên phần này có thể vẫn hoạt động nếu UI chạy trong full trust.

### 5.4. NativeAOT DLL bị load vào process khác

- `BambooMintKey.dll` sẽ được `ctfmon.exe` / `explorer.exe` / các ứng dụng target load.
- MSIX cần đặt DLL ở vị trí accessible (trong package folder hoặc sparse package path).

---

## 6. Các phương án triển khai (Options)

### 6.1. Option A: MSIX Sparse Package (Khuyến nghị)

- Tạo **Sparse Package** với external location trỏ đến folder cài đặt hiện tại.
- Cho phép ứng dụng "packaged" mà vẫn có quyền full trust + registry toàn máy.
- Phù hợp với TSF COM server cần đăng ký `HKEY_LOCAL_MACHINE`.

**Ưu điểm:**
- Giữ được quyền admin/full trust cho TSF.
- Có thể cài từ Store hoặc sideload.

**Nhược điểm:**
- Phức tạp hơn MSIX thuần.
- Cần khai báo `TrustLevel=mediumIL`, `Windows.Storage.Pickers`.

### 6.2. Option B: Packaged COM trong AppxManifest.xml

- Khai báo COM class, surrogate, và TSF registration trực tiếp trong manifest.
- Không cần ghi registry thủ công.

**Ưu điểm:**
- "MSIX-native", sạch sẽ.

**Nhược điểm:**
- TSF categories phức tạp có thể không hỗ trợ đầy đủ qua manifest.
- Cần nghiên cứu sâu `com:Extension` / `com:ExeServer` / `com:SurrogateServer`.

### 6.3. Option C: Hybrid — MSIX chỉ chứa UI, DLL vẫn cài qua custom action

- MSIX chỉ đóng gói UI app.
- `BambooMintKey.dll` được đăng ký riêng bởi một MSIX extension hoặc companion installer.

**Ưu điểm:**
- Tách biệt UI và TSF server.

**Nhược điểm:**
- Không phải 1-click install từ Store.
- Vi phạm ý tưởng phân phối qua MSIX.

---

## 7. Draft quy trình build MSIX

### 7.1. Bước 1: Chuẩn bị artifact

```powershell
# 1. Build NativeAOT DLL (publish/win-x64/BambooMintKey.dll)
.\scripts\build-native.ps1

# 2. Publish Avalonia UI (publish/ui/)
.\scripts\build-installer.ps1   # hoặc chỉ publish UI
```

### 7.2. Bước 2: Tạo thư mục staging cho MSIX

```
msix-staging/
├── BambooMintKey.dll
├── BambooMintKey.UI.exe
├── bamboomintkey.ico
├── Assets/
│   ├── StoreLogo.png        (50x50)
│   ├── Square150x150Logo.png
│   ├── Square44x44Logo.png
│   └── Wide310x150Logo.png
└── AppxManifest.xml
```

### 7.3. Bước 3: Viết `AppxManifest.xml`

- Identity: `BambooMintKeyTeam.BambooMintKey` (hoặc tên đã đăng ký trong Partner Center).
- Publisher: CN khớp với certificate Store.
- Version: `1.0.0.0` (4 phần).
- TargetDeviceFamily: `Windows.Desktop` min 10.0.19041.0.
- Capabilities: `runFullTrust` (cần thiết cho TSF COM server).
- Extensions:
  - `windows.comServer` (khai báo CLSID).
  - `windows.immersiveShell` / TSF extension (nếu có).

### 7.4. Bước 4: Tạo MSIX package

```powershell
# Dùng MakeAppx.exe từ Windows SDK
MakeAppx.exe pack /d msix-staging /p BambooMintKey.msix
```

Hoặc tạo bundle cho nhiều kiến trúc:
```powershell
MakeAppx.exe bundle /d msix-staging /p BambooMintKey.msixbundle
```

### 7.5. Bước 5: Sign MSIX

- **Local test:** dùng self-signed certificate.
- **Store publish:** upload unsigned MSIX, Store sẽ sign.
- **CI:** dùng certificate từ Azure Key Vault / GitHub Secrets.

```powershell
# Sign local với test cert
signtool.exe sign /fd SHA256 /a /f test-cert.pfx /p password BambooMintKey.msix
```

### 7.6. Bước 6: Validate

```powershell
# Kiểm tra package trước khi upload
MakeAppx.exe validate /p BambooMintKey.msix
```

---

## 8. Quy trình publish lên Microsoft Store

### 8.1. Chuẩn bị tài khoản

1. Đăng ký [Microsoft Partner Center](https://partner.microsoft.com/dashboard).
2. Trả phí Developer Account (hiện khoảng $19 cá nhân / $99 công ty).
3. Đặt tên app (`BambooMintKey`) và reserve app name.

### 8.2. Tạo app submission

1. Vào Partner Center → Products & Services → Create new app.
2. Chọn app type: **MSIX or PWA app**.
3. Điền:
   - Name
   - Pricing: Free
   - Markets: Vietnam + Global
   - Age rating: Suitable for all ages
   - Category: Productivity / Utilities & Tools

### 8.3. Upload package

1. Vào tab **Packages**.
2. Upload `BambooMintKey.msix` hoặc `BambooMintKey.msixbundle`.
3. Chọn device family: Desktop only.

### 8.4. Store listing

1. Description (EN + VI).
2. Screenshots:
   - 1366x768 desktop screenshot của UI.
   - 16:9 recommended.
3. Store logos (từ `Assets/`).
4. Support URL, Privacy policy URL.

### 8.5. Submit for certification

1. Review tất cả thông tin.
2. Click **Submit to the Store**.
3. Chờ certification (thường vài giờ đến vài ngày).

---

## 9. CI/CD Integration (GitHub Actions)

### 9.1. Workflow outline

```yaml
name: Build & Publish MSIX

on:
  push:
    tags:
      - 'v*'

jobs:
  build-msix:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build NativeAOT DLL
        run: .\scripts\build-native.ps1

      - name: Publish UI
        run: dotnet publish src\BambooMintKey.UI\BambooMintKey.UI.fsproj -c Release -r win-x64 -o publish\ui

      - name: Stage MSIX
        run: .\scripts\stage-msix.ps1

      - name: Pack MSIX
        run: MakeAppx.exe pack /d msix-staging /p BambooMintKey.msix

      - name: Sign MSIX (test)
        run: signtool.exe sign /fd SHA256 /a /f ${{ secrets.TEST_CERT }} /p ${{ secrets.TEST_CERT_PASSWORD }} BambooMintKey.msix

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: BambooMintKey.msix
          path: BambooMintKey.msix

      - name: Publish to Store (optional)
        # Dùng Partner Center API hoặc Microsoft Store Publish GitHub Action
        run: .\scripts\publish-store.ps1
```

### 9.2. Secrets cần thiết

- `TEST_CERT_PFX`: Base64 của test certificate.
- `TEST_CERT_PASSWORD`: Mật khẩu cert.
- `PARTNER_CENTER_CLIENT_ID`, `CLIENT_SECRET`, `TENANT_ID`: Dùng cho Store submission API.
- `STORE_APP_ID`: ID của app trong Partner Center.

---

## 10. Câu hỏi cần giải đáp trước khi triển khai

1. **Tên nhà phát hành (Publisher) trong Store là gì?** Cần reserve sớm để lấy CN cho manifest.
2. **Có dùng Sparse Package không?** Đây là quyết định kiến trúc quan trọng nhất cho TSF.
3. **Có cần ghi `HKEY_LOCAL_MACHINE` không, hay có thể dùng Packaged COM?**
4. **Có cần hỗ trợ cài song song Inno Setup + MSIX không?** (ví dụ người dùng chuyển từ `.exe` sang Store).
5. **Phiên bản MSIX có cần migration config cũ từ `%APPDATA%\BambooMintKey\config.json` không?**

---

## 11. Kế hoạch hành động (Action Items)

| # | Công việc | Owner | Ưu tiên |
|---|---|---|---|
| 1 | Quyết định kiến trúc MSIX (Sparse Package vs Packaged COM) | PM/Dev | Cao |
| 2 | Reserve tên app trên Partner Center | PM | Cao |
| 3 | Tạo `AppxManifest.xml` mẫu | Dev | Cao |
| 4 | Viết `scripts/stage-msix.ps1` | Dev | Cao |
| 5 | Viết `scripts/build-msix.ps1` | Dev | Cao |
| 6 | Tạo assets (logo, screenshot) cho Store | Designer | Trung bình |
| 7 | Test sideload MSIX trên VM sạch | QA | Cao |
| 8 | Test TSF hoạt động sau khi cài từ MSIX | QA | Cao |
| 9 | Tích hợp GitHub Actions build MSIX | DevOps | Trung bình |
| 10 | Submit lên Microsoft Store | PM | Thấp (sau khi test xong) |

---

## 12. Tài liệu liên quan

- `docs/2.Design/Phase2/002_01_COM_Registration_and_Exports.md` — đăng ký COM/TSF.
- `docs/2.Design/Phase5/005_05_DisplayAttributeProvider.md` — Display Attribute Provider.
- `delivery/installer/installer.iss` — bộ cài Inno Setup hiện tại.
- `scripts/build-installer.ps1` — build pipeline hiện tại.
- Microsoft Docs: [Package a desktop app from source code using MSIX](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-packaging-dot-net)
- Microsoft Docs: [Sparse Packages](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
- Microsoft Docs: [Microsoft Store Publish API](https://learn.microsoft.com/en-us/windows/apps/develop/publish-using-apis)
