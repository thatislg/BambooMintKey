<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 009_01 — Điều Tra Khả Năng Phân Phối Flatpak & Tương Thích Steam Deck (SteamOS)

**Mã tài liệu:** `009_01_Flatpak_Investigation`  
**Giai đoạn:** Phase 9 — Phân phối Flatpak & Tương thích Steam Deck  
**Thuộc module:** `BambooMintKey.Fcitx5`, `BambooMintKey.Core.Native`, `BambooMintKey.UI.Linux`, Flatpak Manifest  
**Trạng thái:** 🔍 Điều tra kỹ thuật (Investigation & Architecture Design)  

---

## 1. Bối cảnh & Bài toán người dùng (User Context)

### 1.1. Hiện trạng triển khai hiện tại
BambooMintKey đã hoàn thiện triển khai thành công trên môi trường Linux gốc (Native Install) thông qua:
- **Core Engine:** F# thuần (`BambooMintKey.Core`) biên dịch qua **.NET 10 NativeAOT** (`BambooMintKey.Core.Native`) thành thư viện C-ABI `BambooMintKeyCore.so`.
- **Fcitx5 Addon:** Module C++17 (`BambooMintKey.Fcitx5`) liên kết động với `BambooMintKeyCore.so` qua RPATH `$ORIGIN` và `SONAME`.
- **Giao diện Cài đặt:** Ứng dụng Avalonia F# (`BambooMintKey.UI.Linux`) giao tiếp qua D-Bus `org.fcitx.Fcitx5.BambooMintKey`.
- **Đóng gói phân phối:** Hoạt động ổn định trên **Ubuntu/Debian** (gói `.deb`), **Fedora/RHEL** (gói `.rpm`) và cài đặt trực tiếp vào hệ thống `/usr` thông qua script `install_linux.sh` (xem [Issue 010](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/3.Issue/010_LinuxPackaging_NotAvailable_Fix.md)).

### 1.2. Nhu cầu người dùng trên Steam Deck
Người dùng sở hữu thiết bị **Steam Deck** (chạy hệ điều hành **SteamOS 3.x**) mong muốn gõ tiếng Việt Telex bằng BambooMintKey trong chế độ **Desktop Mode** (để lướt web trên Firefox/Chrome, gõ văn bản, trò chuyện trên Discord, lập trình, v.v.).

Tuy nhiên, việc cài đặt trực tiếp vào hệ thống (`sudo ./scripts/linux/install_linux.sh`) gặp rào cản nghiêm trọng trên Steam Deck:
1. **Hệ thống tệp bất biến (Immutable Root Filesystem):** SteamOS sử dụng cơ chế phân vùng hệ thống A/B read-only (`steamos-readonly`). Mặc định người dùng không thể ghi vào `/usr`.
2. **Cập nhật hệ thống xóa sạch (Wipe on OS Update):** Dù người dùng có mở khóa bằng lệnh `steamos-readonly disable` để cài đặt bằng `pacman` hoặc script, mọi tệp trong `/usr/lib/` và `/usr/share/` sẽ bị xóa hoàn toàn mỗi khi SteamOS cập nhật OTA (Over-The-Air Update).
3. **Chuẩn mực của SteamOS:** Mọi ứng dụng đồ họa trên Steam Deck đều được khuyến nghị phân phối qua **Flatpak** từ **Flathub**, tích hợp sẵn qua trung tâm phần mềm **KDE Discover**.
4. **Hệ sinh thái Fcitx5 trên Steam Deck:** Người dùng Steam Deck hiện cài đặt bộ gõ thông qua ứng dụng Flatpak `org.fcitx.Fcitx5` trên Flathub. Do đó, người dùng mong muốn BambooMintKey xuất hiện dưới dạng một **Fcitx5 Addon Extension** cài đặt được qua Flatpak.

---

## 2. Kiến trúc Fcitx5 và Addon trong môi trường Flatpak

Qua khảo sát trực tiếp mã nguồn và quy chuẩn đóng gói Flatpak của Fcitx5 (`flathub/org.fcitx.Fcitx5` và `fcitx/flatpak-fcitx5`), cấu trúc hoạt động của addon trong Flatpak được xác định như sau:

### 2.1. Điểm mở rộng (Extension Point) của `org.fcitx.Fcitx5`
Trong tệp cấu hình Flatpak của `org.fcitx.Fcitx5`, Fcitx5 khai báo cơ chế mở rộng động:
```yaml
add-extensions:
  org.fcitx.Fcitx5.Addon:
    version: stable
    directory: addons
    subdirectories: true
    no-autodownload: true
    add-ld-path: lib
    autodelete: true
```

### 2.2. Cơ chế nạp Addon tự động của wrapper `fcitx5.sh`
Bên trong container Flatpak của Fcitx5, tệp thực thi `/app/bin/fcitx5` thực chất là một script shell (`fcitx5.sh`) khởi động trước binary thực sự `fcitx5-bin`:
```sh
#!/bin/sh

export FCITX_ADDON_DIRS=/app/lib/fcitx5
for dir in `ls /app/addons/`; do
    export FCITX_ADDON_DIRS=/app/addons/$dir/lib/fcitx5:$FCITX_ADDON_DIRS
    export XDG_DATA_DIRS=/app/addons/$dir/share:${XDG_DATA_DIRS}
    export PATH=/app/addons/$dir/bin:$PATH
done

for dir in `ls /app/addons/`; do
    if [ -d /app/addons/$dir/lib/libime ]; then
        export LIBIME_MODEL_DIRS=/app/addons/$dir/lib/libime:$LIBIME_MODEL_DIRS
    fi
done

exec fcitx5-bin "$@"
```

### 2.3. Quy chuẩn cấu trúc tệp của một Addon Extension
Khi một extension có App ID `org.fcitx.Fcitx5.Addon.<Name>` (ví dụ `org.fcitx.Fcitx5.Addon.BambooMintKey`) được cài đặt, Flatpak sẽ mount nội dung của nó vào đường dẫn `/app/addons/BambooMintKey`. Để Fcitx5 nhận diện được toàn bộ thành phần, addon phải đặt các tệp theo cấu trúc:

```
/app/addons/BambooMintKey/
├── lib/
│   └── fcitx5/
│       ├── libbamboomintkey.so       # Module C++ của Fcitx5
│       └── BambooMintKeyCore.so      # Shared library C-ABI NativeAOT
└── share/
    ├── fcitx5/
    │   ├── addon/
    │   │   └── bamboomintkey.conf    # Khai báo Addon metadata
    │   └── inputmethod/
    │       └── bamboomintkey.conf    # Khai báo Input Method hiển thị
    └── icons/
        └── hicolor/
            └── scalable/
                └── apps/
                    ├── fcitx_bamboomintkey.svg
                    ├── fcitx_bamboomintkey_e.svg
                    └── bamboomintkey.svg
```

**Nhận xét kỹ thuật:**
- Nhờ `add-ld-path: lib`, thư mục `/app/addons/BambooMintKey/lib` được tự động đưa vào `LD_LIBRARY_PATH`.
- Nhờ script `fcitx5.sh`, thư mục `/app/addons/BambooMintKey/lib/fcitx5` được tự động đưa vào biến môi trường `FCITX_ADDON_DIRS`, và `/app/addons/BambooMintKey/share` được đưa vào `XDG_DATA_DIRS`.
- Vì `libbamboomintkey.so` được biên dịch với `RPATH: $ORIGIN` và `SONAME: BambooMintKeyCore.so`, dynamic linker sẽ tìm và nạp ngay `BambooMintKeyCore.so` nằm cùng thư mục `lib/fcitx5/` mà không cần bất kỳ đường dẫn tuyệt đối nào!

---

## 3. Đánh giá tính sẵn sàng của mã nguồn BambooMintKey

Khảo sát chi tiết từng thành phần trong mã nguồn hiện tại của BambooMintKey:

### 3.1. Engine Core Native (`BambooMintKey.Core.Native`)
- **Ngôn ngữ & Công nghệ:** F# + C# NativeAOT (.NET 10).
- **Phụ thuộc nhị phân (Binary Dependencies):**
  Kiểm tra bằng `ldd publish/linux-x64/BambooMintKeyCore.so`:
  ```text
  linux-vdso.so.1
  libm.so.6 => /lib/x86_64-linux-gnu/libm.so.6
  libc.so.6 => /lib/x86_64-linux-gnu/libc.so.6
  /lib64/ld-linux-x86-64.so.2
  ```
  Thư viện **chỉ phụ thuộc vào libc và libm tiêu chuẩn của Linux**. Không đòi hỏi .NET Runtime, không đòi hỏi thư viện runtime của bên thứ ba.
- **Tính di động (Portability):** Cờ `<LinkerArg Include="-Wl,-soname,BambooMintKeyCore.so" />` trong [BambooMintKey.Core.Native.csproj](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj) đảm bảo header ELF độc lập hoàn toàn với máy build.
- **Đánh giá:** ✅ **Sẵn sàng 100%** cho việc đóng gói Flatpak.

### 3.2. Fcitx5 Addon C++ (`BambooMintKey.Fcitx5`)
- **Ngôn ngữ & Build System:** C++17, CMake 3.16+, sử dụng `GNUInstallDirs`.
- **Cấu hình Addon:** Tệp [bamboomintkey-addon.conf.in](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Fcitx5/bamboomintkey-addon.conf.in) khai báo `0=core` (không gán cứng phiên bản), đảm bảo tương thích hoàn hảo với phiên bản Fcitx5 trong Flatpak runtime (hiện là Fcitx5 5.1.x trên nền `org.kde.Platform//6.11`).
- **Liên kết động:** [CMakeLists.txt](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Fcitx5/CMakeLists.txt) đã thiết lập `IMPORTED_SONAME "BambooMintKeyCore.so"` và `INSTALL_RPATH "$ORIGIN"`.
- **Đánh giá:** ✅ **Sẵn sàng 95%**. Chỉ cần điều chỉnh logic đường dẫn file cấu hình (Config Path Fallback) để chạy tốt trong sandbox.

### 3.3. Cơ chế giao tiếp D-Bus IPC
- **Hiện trạng:** Addon xuất bản service `org.fcitx.Fcitx5.BambooMintKey` trên D-Bus Session Bus (các phương thức `GetVietnameseMode`, `SetVietnameseMode`, `ToggleVietnameseMode` và signal `ModeChanged`).
- **Khả năng tương thích Flatpak:**
  Kiểm tra `finish-args` của `org.fcitx.Fcitx5`:
  ```yaml
  finish-args:
    - --socket=session-bus
  ```
  Container Fcitx5 được cấp toàn quyền kết nối tới session-bus của user session. Do đó, các ứng dụng ngoài host (hoặc các container khác) hoàn toàn có thể gửi/nhận lệnh D-Bus tới BambooMintKey mà không gặp bất kỳ rào cản phân quyền nào.
- **Đánh giá:** ✅ **Sẵn sàng 100%**.

### 3.4. Vấn đề cô lập hệ thống tệp & File cấu hình (`config.json`)
- **Hiện trạng:** Trong [engine.cpp (dòng 335-345)](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Fcitx5/engine.cpp#L335-L345), hàm `configFilePath()` phân giải:
  `$XDG_CONFIG_HOME/bamboomintkey/config.json` hoặc `$HOME/.config/bamboomintkey/config.json`.
- **Vấn đề trong Flatpak Sandbox:**
  1. Flatpak `org.fcitx.Fcitx5` chỉ cấp quyền truy cập các thư mục config:
     ```yaml
     - --filesystem=xdg-config/kxkbrc:rw
     - --filesystem=xdg-config/fontconfig:ro
     - --filesystem=xdg-config/fcitx:create
     - --filesystem=xdg-config/ibus:create
     ```
  2. Nó **không có quyền truy cập** `xdg-config/bamboomintkey` của host!
  3. Vì vậy, bên trong sandbox, `$XDG_CONFIG_HOME` trỏ về:
     `~/.var/app/org.fcitx.Fcitx5/config/bamboomintkey/config.json`.
  4. Nếu người dùng mở giao diện Settings trên máy host (`bamboomintkey-ui`), ứng dụng UI sẽ ghi vào `~/.config/bamboomintkey/config.json` của host. Addon trong Flatpak sẽ **không đọc được** thay đổi này trừ khi:
     - Thêm override quyền filesystem: `flatpak override --user --filesystem=xdg-config/bamboomintkey org.fcitx.Fcitx5`.
     - *Hoặc* Addon hỗ trợ thêm đường dẫn fallback nằm trong thư mục Fcitx: `~/.config/fcitx/bamboomintkey/config.json` hoặc `~/.config/fcitx5/conf/bamboomintkey.json`.
     - *Hoặc* Đồng bộ cấu hình thời gian thực qua giao thức D-Bus IPC đã có sẵn.
- **Đánh giá:** ⚠️ **Cần giải pháp đồng bộ cấu hình.**

### 3.5. Giao diện Cài đặt (`BambooMintKey.UI.Linux`) trên Steam Deck
- **Hiện trạng:** Viết bằng Avalonia F# (.NET 10). Tệp phân phối hiện tại dạng framework-dependent, đòi hỏi cài đặt `dotnet-runtime-10.0` qua apt/dnf.
- **Rào cản trên Steam Deck:** SteamOS là hệ thống tối giản cho thiết bị chơi game cầm tay, **không cài sẵn .NET 10 Runtime**. Người dùng cũng không thể chạy `sudo pacman -S dotnet-runtime` vì sẽ bị xóa khi cập nhật OS.
- **Giải pháp:**
  Biên dịch `BambooMintKey.UI.Linux` dưới dạng **Self-Contained Single File** (hoặc NativeAOT):
  ```bash
  dotnet publish src/BambooMintKey.UI.Linux/BambooMintKey.UI.Linux.fsproj \
      -c Release -r linux-x64 \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -o publish/ui-steamdeck
  ```
  Kết quả sinh ra duy nhất một tệp thực thi độc lập `bamboomintkey-ui` dung lượng tối ưu, có thể copy vào `~/.local/bin/` và chạy ngay trên Steam Deck mà không cần cài thêm bất kỳ dependency nào.

---

## 4. Các giải pháp phân phối khả thi (Distribution Strategies)

Có 3 giải pháp phân phối BambooMintKey cho người dùng Steam Deck, từ triển khai nhanh tức thì đến chuẩn mực lâu dài:

| Tiêu chí | Giải pháp 1: Sideload Script (Cài đặt User-Space vào Flatpak) | Giải pháp 2: Local Flatpak Bundle (`.flatpak`) | Giải pháp 3: Phát hành chính thức lên Flathub |
|---|---|---|---|
| **Thời gian có thể dùng** | ⚡ **Có thể dùng ngay lập tức** | ⏱️ 1-2 ngày (CI/CD GitHub) | ⏳ 1-2 tuần (kiểm duyệt Flathub) |
| **Yêu cầu quyền root (`sudo`)** | ❌ Không cần (`steamos-readonly` giữ nguyên) | ❌ Không cần | ❌ Không cần |
| **Bền vững qua cập nhật SteamOS** | ✅ Bền vững (lưu trong `~/.var/app` và `~/.local`) | ✅ Bền vững (Flatpak quản lý) | ✅ Bền vững tuyệt đối |
| **Trải nghiệm người dùng** | Chạy 1 lệnh script trong terminal Konsole | Tải file `.flatpak` và double-click / chạy lệnh cài | Mở **KDE Discover**, gõ tìm kiếm và bấm **Install** |
| **Độ phức tạp bảo trì** | Thấp (chỉ cần script shell cài đặt) | Trung bình (cần `flatpak-builder`) | Cao (cần tuân thủ quy chuẩn bot build Flathub) |

---

### Giải pháp 1: Sideload Script cho Steam Deck (Khuyến nghị triển khai ngay)

Đây là giải pháp tốt nhất để đáp ứng ngay lập tức yêu cầu của người dùng mà không cần chờ đợi xét duyệt từ Flathub.

#### Cơ chế hoạt động:
Steam Deck user đã cài `org.fcitx.Fcitx5` qua Discover. Fcitx5 Flatpak lưu trữ dữ liệu cá nhân tại:
`~/.var/app/org.fcitx.Fcitx5/data/` (tương đương `$XDG_DATA_HOME` trong sandbox).

Script cài đặt `scripts/install_steamdeck.sh` sẽ thực hiện:
1. **Kiểm tra môi trường:** Đảm bảo `flatpak` và `org.fcitx.Fcitx5` đã được cài đặt trên Steam Deck. Nếu chưa có, script tự động chạy:
   ```bash
   flatpak install --user -y flathub org.fcitx.Fcitx5
   ```
2. **Cài đặt Addon vào không gian Flatpak User Data:**
   - Sao chép `libbamboomintkey.so` và `BambooMintKeyCore.so` vào `~/.var/app/org.fcitx.Fcitx5/data/fcitx5/lib/`.
   - Sao chép `bamboomintkey.conf` vào `~/.var/app/org.fcitx.Fcitx5/data/fcitx5/addon/`.
   - Sao chép input method metadata vào `~/.var/app/org.fcitx.Fcitx5/data/fcitx5/inputmethod/`.
   - Sao chép icon vào `~/.var/app/org.fcitx.Fcitx5/data/icons/hicolor/scalable/apps/`.
3. **Cấu hình Flatpak Override:**
   Sử dụng lệnh `flatpak override` để cấp quyền cho container Fcitx5 nạp addon và đọc config:
   ```bash
   # Bổ sung đường dẫn tìm kiếm addon
   flatpak override --user --env=FCITX_ADDON_DIRS="/var/data/fcitx5/lib:/app/lib/fcitx5" org.fcitx.Fcitx5
   # Cho phép Fcitx5 đọc file config chung với UI ngoài host
   flatpak override --user --filesystem=xdg-config/bamboomintkey:create org.fcitx.Fcitx5
   ```
4. **Cài đặt UI Settings dạng Self-Contained:**
   - Đặt nhị phân `bamboomintkey-ui` vào `~/.local/bin/`.
   - Cài đặt `bamboomintkey-settings.desktop` vào `~/.local/share/applications/`.
   - Cài đặt icon vào `~/.local/share/icons/hicolor/scalable/apps/`.
5. **Cấu hình môi trường SteamOS Desktop (Tự động thiết lập gõ tiếng Việt):**
   Ghi các biến môi trường vào `~/.config/plasma-workspace/env/input.sh`:
   ```bash
   export GTK_IM_MODULE=fcitx
   export QT_IM_MODULE=fcitx
   export XMODIFIERS=@im=fcitx
   export XIM=fcitx
   ```
   Khai báo tự khởi động Fcitx5 tại `~/.config/autostart/org.fcitx.Fcitx5.desktop`.
6. **Khởi động lại Fcitx5:**
   ```bash
   flatpak kill org.fcitx.Fcitx5 2>/dev/null || true
   flatpak run org.fcitx.Fcitx5 -d &
   ```

---

### Giải pháp 2: Xây dựng Flatpak Extension Manifest (`org.fcitx.Fcitx5.Addon.BambooMintKey`)

Đây là phương thức chuẩn của hệ sinh thái Flatpak, cho phép đóng gói thành tệp `.flatpak` bundle hoặc đưa lên Flathub.

#### Cấu trúc Manifest Flatpak (`org.fcitx.Fcitx5.Addon.BambooMintKey.yaml`):

```yaml
app-id: org.fcitx.Fcitx5.Addon.BambooMintKey
branch: stable
runtime: org.fcitx.Fcitx5
runtime-version: stable
sdk: org.kde.Sdk//6.11
sdk-extensions:
  - org.freedesktop.Sdk.Extension.dotnet10
build-extension: true
separate-locales: false

build-options:
  prefix: /app/addons/BambooMintKey
  prepend-pkg-config-path: /app/addons/BambooMintKey/lib/pkgconfig
  append-path: /usr/lib/sdk/dotnet10/bin
  no-debuginfo: true
  strip: true

cleanup:
  - /include
  - /lib/pkgconfig
  - "*.la"

modules:
  - name: bamboomintkey
    buildsystem: simple
    build-commands:
      # 1. Build BambooMintKeyCore.so (NativeAOT C-ABI)
      - dotnet publish src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj -c Release -r linux-x64 -o publish/linux-x64
      # 2. Build libbamboomintkey.so (Fcitx5 Addon C++)
      - cmake -B build -S src/BambooMintKey.Fcitx5 -DCMAKE_INSTALL_PREFIX=${FLATPAK_DEST} -DBAMBOOMINTKEY_CORE_SO=$PWD/publish/linux-x64/BambooMintKeyCore.so
      - cmake --build build
      - cmake --install build
    sources:
      - type: git
        url: https://github.com/thatislg/BambooMintKey.git
        tag: v1.1.0
        commit: e666af7a8367f08b3baaa2f8087ab6a152ca3145
```

#### Xử lý thách thức Offline Build trên Flathub:
Hệ thống build của Flathub không cho phép truy cập Internet trong quá trình compile (offline sandbox). Khi dùng .NET, `dotnet restore` mặc định cần tải package NuGet từ Internet.
**Giải pháp:**
1. **Cách A (Khuyến nghị cho Flathub):** Dùng công cụ `flatpak-dotnet-generator` của cộng đồng Flathub để tạo file `nuget-sources.json`, cache toàn bộ dependencies của F# Core thành các source file `archive` trong manifest.
2. **Cách B (Prebuilt Artifact Release):** CI/CD của BambooMintKey build sẵn bản release nhị phân `bamboomintkey-fcitx5-linux-x64.tar.gz`. Trong manifest Flatpak, module chỉ việc tải file tarball này và cài vào `${FLATPAK_DEST}`. Cách tiếp cận này được rất nhiều dự án lớn trên Flathub áp dụng thành công.

---

## 5. Kế hoạch triển khai kỹ thuật (Action Plan)

Để giải quyết trọn vẹn yêu cầu người dùng Steam Deck, kế hoạch hành động được chia thành các bước cụ thể:

```
┌────────────────────────────────────────────────────────┐
│ Bước 1: Chuẩn bị mã nguồn & Tính tương thích Sandbox    │
│  - Thêm Config Fallback trong engine.cpp               │
│  - Thêm chế độ build Self-Contained cho UI Linux       │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Bước 2: Tạo Bộ Cài Sideload Chuyên Dụng Steam Deck     │
│  - Viết scripts/install_steamdeck.sh                   │
│  - Viết scripts/uninstall_steamdeck.sh                 │
│  - Tích hợp vào CI/CD tạo artifact steamdeck.tar.gz    │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Bước 3: Đóng gói Flatpak Extension Chuẩn                │
│  - Tạo manifests/org.fcitx.Fcitx5.Addon.BambooMintKey  │
│  - Tạo script build-bundle cục bộ (.flatpak)           │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Bước 4: Kiểm thử trên môi trường SteamOS & Tài liệu hóa │
│  - Test trên Steam Deck / Arch Linux Flatpak Fcitx5    │
│  - Soạn tài liệu docs/STEAMDECK_GUIDE.md               │
└────────────────────────────────────────────────────────┘
```

### Chi tiết các công việc cần làm:

### Công việc 1: Hỗ trợ đường dẫn Config trong môi trường Sandbox ([engine.cpp](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Fcitx5/engine.cpp))
Cập nhật hàm `configFilePath()` trong addon C++ để kiểm tra theo thứ tự ưu tiên:
1. `~/.config/bamboomintkey/config.json` (nếu tồn tại hoặc có quyền ghi).
2. `~/.config/fcitx/bamboomintkey/config.json` (nằm trong thư mục mà Fcitx5 Flatpak mặc định có quyền `--filesystem=xdg-config/fcitx:create`).
3. `~/.var/app/org.fcitx.Fcitx5/config/bamboomintkey/config.json` (thư mục sandbox nội bộ).

### Công việc 2: Bổ sung cấu hình Self-Contained cho Settings UI
Trong [package_linux.sh](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/scripts/linux/package_linux.sh), bổ sung tùy chọn `--self-contained-ui` để xuất bản nhị phân độc lập `bamboomintkey-ui` chứa kèm .NET runtime thu gọn:
```bash
dotnet publish src/BambooMintKey.UI.Linux/BambooMintKey.UI.Linux.fsproj \
    -c Release -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o publish/ui-selfcontained
```

### Công việc 3: Xây dựng Script `install_steamdeck.sh`
Script tự động hóa hoàn toàn 100% cho người dùng Steam Deck:
- Kiểm tra & cài đặt `org.fcitx.Fcitx5` nếu chưa có.
- Cài đặt các tệp addon và icon vào thư mục user data của Flatpak Fcitx5.
- Đặt `bamboomintkey-ui` self-contained vào `~/.local/bin/`.
- Cấu hình tự động biến môi trường input trong KDE Plasma (`~/.config/plasma-workspace/env/input.sh`).
- Cấu hình Fcitx5 tự chạy khi vào Desktop Mode.
- Khởi động lại Fcitx5 và sẵn sàng sử dụng.

### Công việc 4: Tạo Manifest Flatpak & Hướng dẫn sử dụng
- Tạo tệp `manifests/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.yaml`.
- Viết tài liệu `docs/STEAMDECK_GUIDE.md` hướng dẫn từng bước rõ ràng cho cộng đồng game thủ/người dùng Steam Deck tại Việt Nam.

---

## 6. Kết luận & Khuyến nghị

1. **Khả thi kỹ thuật cao:** Kiến trúc của BambooMintKey hiện tại cực kỳ thuận lợi cho Flatpak và Steam Deck vì engine F# đã được biên dịch thành NativeAOT C-ABI độc lập (`BambooMintKeyCore.so`), không có ràng buộc phức tạp về .NET runtime ở phía Fcitx5 daemon.
2. **Chiến lược triển khai 2 giai đoạn:**
   - **Giai đoạn tức thì (Immediate):** Cung cấp script `install_steamdeck.sh` kèm artifact phát hành trong GitHub Releases. Người dùng Steam Deck chỉ cần tải về và chạy 1 lệnh là có thể gõ tiếng Việt ngay lập tức trong Desktop Mode mà không sợ bị mất khi SteamOS cập nhật.
   - **Giai đoạn chính quy (Standard):** Hoàn thiện Flatpak manifest và đưa lên Flathub để người dùng có thể cài đặt trực tiếp từ kho ứng dụng Discover của Steam Deck.
