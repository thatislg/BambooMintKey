<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Kỹ Thuật: Bộ Cài Win32 & Phát Hành Microsoft Store

**Mã tài liệu:** `006_00_Store_Packaging`  
**Đường dẫn:** `docs/2.Design/Phase6/006_00_MSIX_Store_Packaging.md`  
**Giai đoạn:** Phase 6 — Phân Phối & Microsoft Store  
**Trạng thái:** 🎯 Đã phê duyệt (Win32 Store App Model)  

---

## 1. Bối Cảnh & Quyết Định Kiến Trúc

### 1.1. Bối cảnh
BambooMintKey là bộ gõ tiếng Việt dựa trên kiến trúc **Text Services Framework (TSF)**, đóng vai trò là một **In-process COM Server DLL (`BambooMintKey.dll`)** chạy sâu trong tiến trình của các ứng dụng nhập liệu (Chrome, Word, Terminal...).

Để phân phối thuận tiện tới người dùng cuối, dự án hướng tới việc xuất hiện trên **Microsoft Store**.

### 1.2. Quyết định kiến trúc: Bãi bỏ hoàn toàn MSIX, chọn Win32 Store App
Ban đầu, nhóm phát triển đã khảo sát mô hình đóng gói MSIX (Desktop Bridge). Tuy nhiên, sau quá trình thử nghiệm thực tế, mô hình MSIX **bị hủy bỏ** vì các lý do kỹ thuật sau:
1. **Thiếu hỗ trợ TSF từ MSIX:** `AppxManifest.xml` không có schema cho TSF TIP và Language Profiles. Cài MSIX không thể kích hoạt `regsvr32` vào hệ thống.
2. **Không kiểm thử được trên Local:** Gói MSIX khi cài đặt chỉ mở được UI cấu hình, hoàn toàn không gõ được tiếng Việt trên máy local.
3. **Rủi ro khi gỡ cài đặt (Dangling Registry):** Nếu dùng script ngoài để đăng ký TSF cho file trong `WindowsApps`, khi người dùng gỡ cài đặt gói MSIX, Windows xóa folder nhưng không dọn registry TSF, gây crash toàn bộ các ứng dụng gõ phím khác trên máy.

**Lựa chọn chính thức:** **Microsoft Store Win32 Desktop App (`.exe`)**.
Từ năm 2021, Microsoft Store cho phép nộp trực tiếp các bộ cài Win32 truyền thống (`.exe` / `.msi`) mà không cần đóng gói MSIX. Người dùng tải trên Store nhưng máy tính thực thi cài đặt silent từ bộ cài Inno Setup chuẩn.

---

## 2. Kiến Trúc Bộ Cài Win32 (Inno Setup)

Bộ cài được quản lý qua kịch bản Inno Setup: `delivery/installer/installer.iss` và script tự động hóa `scripts/build-installer.ps1`.

### 2.1. Vòng đời cài đặt (Installation Lifecycle)
```
[User / Store Silent Run]
       │
       ▼
[1. Dừng CTF Loader] ──► [2. Copy BambooMintKey.dll & UI] ──► [3. regsvr32 BambooMintKey.dll] ──► [4. Khởi động lại ctfmon]
```

1. **Trước khi cài:** Tự động phát hiện và tạm dừng `ctfmon.exe` để giải phóng lock trên `BambooMintKey.dll` cũ (không cần restart máy).
2. **Triển khai file:** Copy file vào `C:\Program Files\BambooMintKey\`.
3. **Đăng ký hệ thống:** Gọi `regsvr32.exe /s "{app}\BambooMintKey.dll"` để đăng ký COM CLSID và TSF TIP Category/Language Profile.
4. **Sau khi cài:** Tự động kích hoạt lại `ctfmon.exe` để thanh ngôn ngữ nhận diện bộ gõ tức thì.

### 2.2. Vòng đời gỡ cài đặt (Uninstallation Lifecycle)
1. Tạm dừng `ctfmon.exe`.
2. Gọi `regsvr32.exe /u /s "{app}\BambooMintKey.dll"` để dọn sạch TSF Categories và COM CLSID khỏi Windows Registry.
3. Dọn sạch khóa Auto-start `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BambooMintKey`.
4. Xóa sạch thư mục cài đặt và khởi động lại `ctfmon.exe`.

---

## 3. Chuẩn Hóa Quản Lý Phiên Bản (Version Governance)

Quản lý tập trung tại `Directory.Build.props`:
```xml
<PropertyGroup>
  <VersionPrefix>1.0.1</VersionPrefix>
  <Version>1.0.1</Version>
</PropertyGroup>
```

- **Inno Setup:** Nhận tham số `/DMyAppVersion=1.0.1` từ `build-installer.ps1`.
- **UI:** Đọc động qua `FileVersionInfo.GetVersionInfo(Environment.ProcessPath)`.
- **WinGet:** Tự động cập nhật qua `scripts/update-winget-manifest.ps1`.

---

## 4. Quy Trình Nộp Microsoft Partner Center (Store Submission)

### 4.1. Thông số khai báo trên Partner Center
1. **Product Type:** Chọn **"EXE or MSI"**.
2. **Download URL:** Điền đường dẫn tải trực tiếp từ GitHub Releases:
   `https://github.com/thatislg/BambooMintKey/releases/download/v<version>/BambooMintKey-Setup.exe`
3. **Silent Install Arguments:**
   ```text
   /VERYSILENT /NORESTART /SP- /SUPPRESSMSGBOXES
   ```
4. **Silent Uninstall Arguments:**
   ```text
   /VERYSILENT /NORESTART
   ```
5. **Install Elevation:** Requires Administrator (vì bộ gõ TSF cần ghi `HKLM`).

### 4.2. Store Listing Information
- **Tên hiển thị:** BambooMintKey — Bộ gõ tiếng Việt Telex
- **Danh mục:** Utilities & Tools / Productivity
- **Độ tuổi:** All Ages
- **Privacy Policy:** `https://github.com/thatislg/BambooMintKey`
- **Tài sản truyền thông:** Icon 512x512, 1024x1024 và 2-3 ảnh chụp màn hình giao diện / thanh taskbar.

---

## 5. Pipeline Tự Động Hóa (Scripts)

Chỉ cần một lệnh duy nhất:
```powershell
.\scripts\build-installer.ps1
```
Output: `bin\dist\BambooMintKey-Setup.exe` (sẵn sàng phân phối GitHub, WinGet và Microsoft Store).
