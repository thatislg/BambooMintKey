<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Hướng Dẫn Khởi Đầu & Phát Triển BambooMintKey (Getting Started)

Tài liệu này cung cấp hướng dẫn toàn diện cho lập trình viên để thiết lập môi trường, hiểu rõ cấu trúc dự án, build mã nguồn, chạy kiểm thử và đóng gói bộ gõ **BambooMintKey** trên cả **Windows** (TSF) và **Linux** (Fcitx5 / SteamOS Flatpak).

---

## 1. Yêu Cầu Môi Trường (Prerequisites)

### 1.1. Môi trường Windows

- **Hệ điều hành:** Windows 10/11 (x64)
- **.NET SDK:** Phiên bản 10.0 trở lên
- **PowerShell:** PowerShell 7+ (`pwsh`)
- **Visual Studio Build Tools:** C++ Desktop Development (để hỗ trợ NativeAOT compilation và liên kết C runtime)
- **Inno Setup (tùy chọn):** Phiên bản 6.x để đóng gói bộ cài đặt `Setup.exe`

### 1.2. Môi trường Linux (Ubuntu / Debian / Linux Mint / Fedora / SteamOS)

- **.NET SDK:** Phiên bản 10.0 trở lên
- **Trình biên dịch C/C++:** `g++` (hỗ trợ C++17) hoặc `clang`
- **Công cụ NativeAOT:** `clang`, `lld` (bắt buộc để publish .NET NativeAOT ra `.so`)
- **CMake:** Phiên bản 3.16 trở lên
- **Thư viện Fcitx5 Development:**
  - *Ubuntu / Debian / Linux Mint:*
    ```bash
    sudo apt update
    sudo apt install -y fcitx5 fcitx5-frontend-all libfcitx5core-dev libfcitx5config-dev libfcitx5utils-dev cmake g++ clang lld
    ```
  - *Fedora / RHEL:*
    ```bash
    sudo dnf install -y fcitx5 fcitx5-devel fcitx5-qt fcitx5-gtk2 fcitx5-gtk3 cmake gcc-c++ clang lld rpm-build
    ```
- **Flatpak & Flatpak Builder (tùy chọn cho Steam Deck / Flathub):**
  ```bash
  sudo apt install -y flatpak flatpak-builder  # hoặc: sudo dnf install -y flatpak flatpak-builder
  ```

---

## 2. Cấu Trúc Dự Án & Phân Vùng Mã Nguồn

BambooMintKey sử dụng kiến trúc phân tầng, phân tách rạch ròi giữa thuật toán ngôn ngữ thuần chức năng và các tầng cầu nối hệ thống riêng biệt cho từng nền tảng:

```
BambooMintKey/
├── src/
│   ├── BambooMintKey.Core/         # Lõi F# thuần chức năng: Telex engine, phân tích âm tiết, bảng mã Unicode
│   ├── BambooMintKey.Shared/       # Thư viện dùng chung (cấu hình, hằng số, kiểu dữ liệu chung)
│   ├── BambooMintKey.NativeBridge/ # C# NativeAOT: In-Process COM Server TIP cho Windows TSF (BambooMintKey.dll)
│   ├── BambooMintKey.UI/           # F# Avalonia Desktop: Bảng điều khiển Settings cho Windows
│   ├── BambooMintKey.Core.Native/  # C# NativeAOT: C-ABI wrapper (bmk_*) xuất khẩu BambooMintKeyCore.so cho Linux
│   ├── BambooMintKey.Fcitx5/       # C++ Fcitx5 Addon: libbamboomintkey.so, D-Bus service, icon V/E động
│   ├── BambooMintKey.UI.Linux/     # F# Avalonia Desktop: Bảng điều khiển Settings độc lập cho Linux
│   └── BambooMintKey.DevHarness/   # Console harness kiểm thử TSF COM nội bộ
├── tests/
│   ├── BambooMintKey.Core.Tests/   # Unit tests F# cho lõi Telex (119+ test cases)
│   └── BambooMintKey.Fcitx5.Tests/ # Unit tests C++ cho Fcitx5 addon
├── scripts/
│   ├── windows/                    # Script PowerShell phát triển, đăng ký TIP và build installer Windows
│   ├── linux/                      # Script Bash cài đặt, gỡ bỏ, đóng gói DEB/RPM/Flatpak cho Linux
│   ├── tools/                      # Script công cụ (sinh từ điển MIT, tải corpus Wikipedia, sinh icon)
│   └── tests/                      # Kịch bản kiểm thử tự động (kiểm tra C-ABI qua Python ctypes)
├── manifests/
│   ├── flatpak/                    # Manifest Flatpak cho Flathub & Steam Deck (YAML, Metainfo XML)
│   └── l/LMO-LAB/BambooMintKey/    # Manifest phân phối qua WinGet (Windows Package Manager)
├── delivery/                       # Thư mục chứa gói thành phẩm (installer, DEB, RPM, Flatpak)
└── docs/                           # Tài liệu kiến trúc, hướng dẫn build và phân tích kỹ thuật
```

---

## 3. Cấu Trúc Thư Mục Scripts (`scripts/`)

Toàn bộ script hỗ trợ đã được tái cấu trúc thành 4 thư mục chuyên biệt:

| Thư mục | File | Chức năng |
|---|---|---|
| **`scripts/windows/`** | `build-native.ps1` | Publish thư viện NativeAOT `BambooMintKey.dll` |
| | `test-register.ps1` | Đăng ký COM Server và TSF Language Profile vào Windows Registry |
| | `enable-tip.ps1` | Kích hoạt TIP cho người dùng hiện tại và khởi động lại `ctfmon` |
| | `unregister-tip.ps1` | Hủy đăng ký TIP sạch sẽ khỏi Windows Registry |
| | `build-installer.ps1` | Tự động hóa build và đóng gói Inno Setup `BambooMintKey-Setup.exe` |
| | `update-winget-manifest.ps1` | Cập nhật checksum SHA256 và phiên bản cho WinGet manifest |
| | `debug-cocreate.ps1` | Kiểm tra gọi `CoCreateInstance` CLSID trên Windows |
| | `check-dll.ps1` | Kiểm tra PE headers, exports và dependencies của DLL |
| **`scripts/linux/`** | `install_linux.sh` | Build toàn diện và cài đặt addon + core + UI vào `/usr` |
| | `uninstall_linux.sh` | Gỡ bỏ sạch sẽ bộ gõ khỏi hệ thống (`/usr` và `~/.local`) |
| | `install-ui-linux.sh` | Build và cài đặt riêng giao diện Avalonia Settings (`bamboomintkey-ui`) |
| | `package_linux.sh` | Đóng gói bộ cài đặt `.deb`, `.rpm`, `.tar.gz` vào `delivery/linux/` |
| | `package_flatpak.sh` | Đóng gói Flatpak extension cho Flathub / Steam Deck vào `delivery/flatpak/` |
| | `bamboomintkey.spec` | RPM Spec file cho Fedora / RHEL / openSUSE |
| **`scripts/tools/`** | `generate_mit_dict.py` | Tạo tập từ điển tiếng Việt cấp phép MIT từ nhiều nguồn |
| | `fetch_vi_wikipedia.py` | Tải và phân tích XML dump Wikipedia tiếng Việt để trích xuất từ vựng |
| | `generate-icon.py` | Tạo icon đa kích thước (`.ico`, `.svg`) cho Taskbar và System Tray |
| | `add-license-headers.ps1` | Tự động chèn header bản quyền MIT vào toàn bộ source file |
| **`scripts/tests/`** | `test-cabi.py` | Bộ test tự động 9 kịch bản C-ABI qua Python `ctypes` với `BambooMintKeyCore.so` |

---

## 4. Hướng Dẫn Phát Triển trên Windows

### Bước 1: Build toàn bộ Solution

```powershell
dotnet build BambooMintKey.slnx -c Release
```

### Bước 2: Publish NativeAOT DLL (In-Process TIP)

Chạy script chuyên dụng:
```powershell
pwsh -File scripts/windows/build-native.ps1
```

Hoặc dùng lệnh dotnet trực tiếp:
```powershell
dotnet publish src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj `
  -c Release -r win-x64 --self-contained `
  -o publish/win-x64 `
  -p:NativeLib=Shared -p:PublishAot=true
```

### Bước 3: Đăng ký và Kích hoạt bộ gõ

Mở PowerShell với quyền **Administrator**:
```powershell
# 1. Đăng ký COM Server và TSF Category
pwsh -File scripts/windows/test-register.ps1

# 2. Kích hoạt TIP cho người dùng hiện tại
pwsh -File scripts/windows/enable-tip.ps1
```

### Bước 4: Chạy giao diện Cài đặt (Settings GUI)

```powershell
dotnet run --project src/BambooMintKey.UI/BambooMintKey.UI.fsproj
```

---

## 5. Hướng Dẫn Phát Triển trên Linux

### Cách 1: Build & Cài đặt nhanh bằng 1 lệnh

```bash
# Build và cài đặt toàn bộ addon, thư viện Core và giao diện UI vào hệ thống
./scripts/linux/install_linux.sh

# Khởi động lại Fcitx5 để nạp bộ gõ mới
fcitx5 -r
```

### Cách 2: Quy trình Build thủ công từng bước

#### 1. Biên dịch NativeAOT C-ABI (`BambooMintKeyCore.so`)

```bash
dotnet publish src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj \
  -c Release -r linux-x64 -o publish/linux-x64
```

Kiểm tra các export `bmk_*`:
```bash
nm -D publish/linux-x64/BambooMintKeyCore.so | grep bmk
```

#### 2. Biên dịch Addon Fcitx5 (`libbamboomintkey.so`)

```bash
cmake -B build -S src/BambooMintKey.Fcitx5 \
  -DCMAKE_INSTALL_PREFIX=/usr \
  -DBAMBOOMINTKEY_CORE_SO=$PWD/publish/linux-x64/BambooMintKeyCore.so

cmake --build build
sudo cmake --install build
```

#### 3. Cài đặt Giao diện Avalonia Settings (`BambooMintKey.UI.Linux`)

```bash
./scripts/linux/install-ui-linux.sh
```

### Cách 3: Đóng gói phân phối Linux

- **Đóng gói DEB, RPM, Tarball:**
  ```bash
  ./scripts/linux/package_linux.sh
  # Kết quả xuất ra: delivery/linux/
  ```
- **Đóng gói Flatpak Addon (cho Steam Deck / Flathub):**
  ```bash
  ./scripts/linux/package_flatpak.sh
  # Kiểm thử cài đặt ngay vào session người dùng:
  ./scripts/linux/package_flatpak.sh --install
  # Kết quả xuất ra: delivery/flatpak/
  ```

---

## 6. Chạy Kiểm Thử Tự Động (Automated Testing)

### 6.1. Unit Tests Lõi F# (Core Tests)

Chạy bộ unit test kiểm tra toàn bộ ngữ pháp Telex, phân tích âm tiết và các tùy chọn:
```bash
dotnet test tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj
```

### 6.2. Kiểm thử C-ABI qua Python (`scripts/tests/test-cabi.py`)

Kiểm thử 9 kịch bản giao tiếp C-ABI giữa thư viện gốc `BambooMintKeyCore.so` và host:
```bash
python3 scripts/tests/test-cabi.py publish/linux-x64/BambooMintKeyCore.so
```
Kết quả mong đợi: `9 passed, 0 failed`.

---

## 7. Tài Liệu Tham Khảo Liên Quan

- [README.md](../README.md): Tổng quan dự án, tính năng và hướng dẫn người dùng cuối.
- [BUILD_LINUX.md](BUILD_LINUX.md): Hướng dẫn chi tiết biên dịch, khắc phục sự cố trên các distro Linux.
- [SYSTEM_ARCHITECTURE.md](SYSTEM_ARCHITECTURE.md): Kiến trúc hệ thống toàn diện (Windows TSF, Linux Fcitx5, SteamOS Flatpak, Shared Memory, D-Bus).
- [manifests/flatpak/README.md](../manifests/flatpak/README.md): Hướng dẫn chi tiết đóng gói Flatpak extension cho Flathub.
- [docs/2.Design/Phase9/](2.Design/Phase9/): Thiết kế giải pháp Flathub và điều tra tính tương thích Steam Deck.
