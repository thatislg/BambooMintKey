<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Hướng Dẫn Cài Đặt & Build BambooMintKey trên Linux (Fcitx5)

Tài liệu hướng dẫn cài đặt và build bộ gõ BambooMintKey trên Linux (Fcitx5), hỗ trợ **Ubuntu/Debian** và **Fedora**. Cách nhanh nhất là dùng script 1 lệnh; các mục bên dưới mô tả chi tiết từng bước build thủ công.

> Các đường dẫn trong tài liệu dùng ký hiệu chung: `~` = thư mục home của người dùng, `$HOME` tương đương, `$XDG_CONFIG_HOME` = thư mục config XDG (mặc định `~/.config`).

> **⚡ Cài đặt nhanh bằng 1 lệnh:**
> ```bash
> cd /đường/dẫn/tới/BambooMintKey
> ./scripts/linux/install_linux.sh      # build + cài toàn bộ vào /usr (cần sudo)
> ./scripts/linux/uninstall_linux.sh    # gỡ sạch
> ```
> Cài vào hệ thống `/usr` (phù hợp apt/rpm cho Ubuntu/Debian & Fedora). Các mục bên dưới mô tả chi tiết từng bước nếu bạn muốn build thủ công.

---

## 1. Yêu cầu hệ thống

| Thành phần | Phiên bản tối thiểu | Ghi chú |
|---|---|---|
| Fcitx5 | 5.1.x | Input method framework |
| .NET SDK | 10.0 | Build `Core.Native` (NativeAOT) |
| CMake | 3.16+ | Build addon C++ |
| g++ (GCC) | C++17 | Compiler C++ |
| clang + lld | — | Bắt buộc cho NativeAOT |

**Ubuntu / Debian / Linux Mint:**

```bash
sudo apt update
sudo apt install -y \
    fcitx5 fcitx5-frontend-all \
    libfcitx5core-dev \
    libfcitx5config-dev \
    libfcitx5utils-dev \
    cmake g++ clang lld
```

**Fedora / RHEL:**

```bash
sudo dnf install -y \
    fcitx5 fcitx5-devel fcitx5-qt fcitx5-gtk2 fcitx5-gtk3 \
    cmake gcc-c++ clang lld
```

> `fcitx5-frontend-all` (Ubuntu) và `fcitx5-qt`/`fcitx5-gtk*` (Fedora) là các IM module để ứng dụng GTK/Qt kết nối được với Fcitx5 — bắt buộc để gõ tiếng Việt trong app.

---

## 2. Build `BambooMintKey.Core.Native` (thư viện C-ABI `.so`)

Bước này dùng .NET NativeAOT để đóng gói engine F# thành thư viện gốc `BambooMintKeyCore.so` (giao diện C-ABI `bmk_*`).

```bash
cd /đường/dẫn/tới/BambooMintKey

dotnet publish src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj \
  -c Release -r linux-x64 -o publish/linux-x64
```

Kết quả: `publish/linux-x64/BambooMintKeyCore.so`.

> Xác minh nhanh:
> ```bash
> nm -D publish/linux-x64/BambooMintKeyCore.so | grep bmk
> ```

---

## 3. Build addon Fcitx5 (`libbamboomintkey.so`)

```bash
cd /đường/dẫn/tới/BambooMintKey

cmake -B build -S src/BambooMintKey.Fcitx5 \
  -DCMAKE_INSTALL_PREFIX=/usr \
  -DBAMBOOMINTKEY_CORE_SO=$PWD/publish/linux-x64/BambooMintKeyCore.so

cmake --build build
```

Kết quả: `build/libbamboomintkey.so`.

---

## 4. Cài đặt

```bash
sudo cmake --install build
```

Việc cài đặt sẽ đặt các file vào đúng vị trí chuẩn của Fcitx5:

| File | Đường dẫn Ubuntu / Debian | Đường dẫn Fedora / RHEL | Ghi chú |
|---|---|---|---|
| `libbamboomintkey.so` | `/usr/lib/<multiarch>/fcitx5/` | `/usr/lib64/fcitx5/` | Addon C++ của Fcitx5 |
| `BambooMintKeyCore.so` | `/usr/lib/<multiarch>/fcitx5/` | `/usr/lib64/fcitx5/` | Đặt cạnh addon để `$ORIGIN` rpath tự tìm |
| `bamboomintkey.conf` (addon) | `/usr/share/fcitx5/addon/` | `/usr/share/fcitx5/addon/` | Metadata khai báo addon |
| `bamboomintkey.conf` (input method) | `/usr/share/fcitx5/inputmethod/` | `/usr/share/fcitx5/inputmethod/` | Hiển thị trong danh sách bộ gõ |
| `fcitx_bamboomintkey*.svg` (icon V/E) | `/usr/share/icons/hicolor/scalable/apps/` | `/usr/share/icons/hicolor/scalable/apps/` | Icon trạng thái bộ gõ |

> Nếu muốn cài vào thư mục user (không cần sudo), đổi `-DCMAKE_INSTALL_PREFIX=~/.local` và đặt file thủ công vào `~/.local/share/fcitx5/` + `~/.local/lib/fcitx5/`.

---

## 5. Build & cài đặt giao diện Cài đặt (`BambooMintKey.UI.Linux`)

Giao diện cài đặt là ứng dụng Avalonia (F# / .NET 10), tách biệt khỏi addon Fcitx5. Dùng script cài đặt tự động:

```bash
cd /đường/dẫn/tới/BambooMintKey

# Cài launcher vào /usr/local/bin (cần sudo) — khuyến nghị để Fcitx5 addon gọi được
./scripts/linux/install-ui-linux.sh

# Hoặc cài user (không cần sudo), launcher vào ~/.local/bin
./scripts/linux/install-ui-linux.sh --user
```

Script thực hiện:

1. `dotnet publish` ứng dụng ra `publish/ui-linux/`.
2. Tạo launcher `bamboomintkey-ui` trên PATH (trỏ tới apphost đã publish).
3. Cài desktop entry `bamboomintkey-settings.desktop` vào `/usr/share/applications/` (cài user: `~/.local/share/applications/`).
4. Cài icon `bamboomintkey.svg` vào `/usr/share/icons/hicolor/scalable/apps/` (cài user: `~/.local/share/icons/hicolor/scalable/apps/`).
5. Làm mới cache icon / desktop database.

**Mở giao diện cài đặt bằng các cách:**

- Menu ứng dụng → tìm "BambooMintKey Settings".
- Lệnh terminal: `bamboomintkey-ui`.
- Menu chuột phải Fcitx5 (mục "Cài đặt...") khi BambooMintKey đang là bộ gõ đang chọn.

---

## 6. Đóng gói phân phối (.deb, .rpm, .tar.gz)

Để đóng gói BambooMintKey thành các gói phân phối sẵn sàng cài đặt trên nhiều máy khác nhau mà không cần build từ mã nguồn:

```bash
cd /đường/dẫn/tới/BambooMintKey
./scripts/linux/package_linux.sh
```

Kết quả đóng gói nằm tại thư mục `delivery/linux/`:
- **Gói `.deb`:** `delivery/linux/bamboomintkey_<ver>_<arch>.deb` (cho Ubuntu, Debian, Linux Mint, Pop!_OS)
- **Gói `.rpm`:** `delivery/linux/rpmbuild/RPMS/<arch>/bamboomintkey-<ver>-1.<dist>.<arch>.rpm` (cho Fedora, RHEL, openSUSE)
- **Gói `.tar.gz`:** `delivery/linux/bamboomintkey_<ver>_linux.tar.gz` (dùng giải nén thủ công vào `/`)

### Các điểm kỹ thuật quan trọng trong quy trình đóng gói:
* **Tính độc lập thư viện (Portable DT_NEEDED):** `BambooMintKey.Core.Native.csproj` được gán `-Wl,-soname,BambooMintKeyCore.so` và CMake khai báo `IMPORTED_SONAME "BambooMintKeyCore.so"`. Nhờ đó, file `libbamboomintkey.so` chỉ ghi nhận tên file thay vì đường dẫn tuyệt đối của máy host build, cho phép nạp thư viện trơn tru tại mọi máy đích thông qua `RPATH: $ORIGIN`. *(Xem chi tiết tại [docs/3.Issue/010_LinuxPackaging_NotAvailable_Fix.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/3.Issue/010_LinuxPackaging_NotAvailable_Fix.md))*.
* **Chuẩn FHS Fedora 64-bit (`/usr/lib64`):** Khi gọi `rpmbuild`, script tự động áp dụng `--define "_lib lib64" --define "_libdir /usr/lib64"`, đảm bảo các file `.so` và ứng dụng UI được cài chính xác vào `/usr/lib64/fcitx5/` và launcher tại `/usr/bin/bamboomintkey-ui`.
* **Tương thích Fcitx5 Core:** Metadata addon khai báo `0=core` (không gán cứng số phiên bản) giúp bộ gõ tương thích rộng rãi trên mọi phiên bản Fcitx5 của các bản phân phối Linux.

---

## 7. Đóng gói Flatpak Extension (cho Steam Deck & Flathub)

Trên SteamOS (Steam Deck), phân vùng hệ thống `/usr` ở chế độ chỉ đọc (read-only) và bộ gõ Fcitx5 được cài đặt dưới dạng Flatpak (`org.fcitx.Fcitx5`). BambooMintKey cung cấp cơ chế đóng gói thành **Fcitx5 Addon Extension** độc lập:

```bash
cd /đường/dẫn/tới/BambooMintKey

# Đóng gói bundle Flatpak xuất ra delivery/flatpak/
./scripts/linux/package_flatpak.sh

# Hoặc vừa build vừa cài đặt trực tiếp vào session Fcitx5 Flatpak hiện tại để test:
./scripts/linux/package_flatpak.sh --install
```

Kết quả đóng gói: `delivery/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.flatpak`.

Cấu hình Flatpak được duy trì độc lập tại:
- `manifests/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.yaml`: Manifest xây dựng addon extension trên runtime KDE Platform.
- `manifests/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.metainfo.xml`: AppStream metadata mô tả bộ gõ.
- `manifests/flatpak/flathub.json`: Cấu hình Flathub build.
- Xem chi tiết tại [manifests/flatpak/README.md](../manifests/flatpak/README.md) và [docs/2.Design/Phase9/009_02_Flathub_Independent_Architecture.md](2.Design/Phase9/009_02_Flathub_Independent_Architecture.md).

---

## 8. Kích hoạt Fcitx5 & thêm bộ gõ

### 8.1. Đặt Fcitx5 làm bộ gõ hệ thống

Để ứng dụng (GTK/Qt) dùng Fcitx5, cần đặt biến môi trường IM module. Thêm vào `~/.profile` (hoặc `~/.bash_profile`):

```bash
export GTK_IM_MODULE=fcitx
export QT_IM_MODULE=fcitx
export XMODIFIERS=@im=fcitx
```

Hoặc dùng công cụ có sẵn:
- **Ubuntu/Debian**: `im-config -n fcitx5` rồi đăng xuất/đăng nhập lại.
- **KDE Plasma**: System Settings → Input Devices → Virtual Keyboard → chọn **Fcitx 5**.

> Lưu ý: giá trị module là `fcitx` (không phải `fcitx5`) để tương thích ngược với các app.

### 8.1.1. Ngoại lệ Steam (app 32-bit)

**Steam** (client 32-bit) dùng CEF/GTK cho ô chat/tìm kiếm, nên `GTK_IM_MODULE=fcitx` sẽ khiến nó cố nạp `libfcitx5gclient.so` (64-bit) và **không gõ được tiếng Việt**. Nếu bạn dùng Steam, hãy đổi `GTK_IM_MODULE` thành `xim` (đi qua giao thức XIM thuần túy, không cần thư viện client):

```bash
# Đổi GTK_IM_MODULE=fcitx -> xim (giữ QT_IM_MODULE=fcitx vì app Qt là 64-bit)
sed -i 's/GTK_IM_MODULE=fcitx/GTK_IM_MODULE=xim/' ~/.config/environment.d/bamboomintkey.conf
sudo sed -i 's/GTK_IM_MODULE=fcitx/GTK_IM_MODULE=xim/' /etc/environment
# rồi đăng xuất / đăng nhập lại
```

Hoặc tự động hóa bằng script: `./scripts/linux/setup_ime_compat.sh`.

> `GTK_IM_MODULE=xim` vẫn gõ tiếng Việt bình thường cho mọi app GTK (Firefox, gedit...); chỉ thiếu một số tính năng nâng cao (surrounding text) so với `fcitx` — chấp nhận được cho bộ gõ Telex. Chi tiết: [Issue 011](3.Issue/011_Flatpak_Incompatibility_Chrome_Opera_Zed_Steam.md).

### 8.2. Nạp addon & thêm bộ gõ

```bash
# Restart Fcitx5 để nạp addon mới
fcitx5 -r
```

Sau đó mở **Fcitx5 Configuration** → tab **Input Method**:

1. Tìm **BambooMintKey** (mục tiếng Việt, `LangCode=vi`) trong danh sách bên phải.
2. Thêm vào nhóm input method hiện tại của bạn.

> Nếu icon chưa hiện, chạy `gtk-update-icon-cache /usr/share/icons/hicolor`.

---

## 9. Kiểm tra

| Việc cần test | Thao tác | Kết quả mong đợi |
|---|---|---|
| Gõ Telex | Gõ `duowngf` rồi `space` | Hiện **"đường "** |
| Chuyển V/E | Bấm phím `` ` `` (dưới Esc) | Đổi V ↔ E (gõ tiếng Việt ↔ tiếng Anh) |
| Icon V/E | Quan sát thanh trạng thái | Icon đổi V ↔ E |
| D-Bus | `dbus-send --session --print-reply --dest=org.fcitx.Fcitx5.BambooMintKey /org/fcitx/Fcitx5/BambooMintKey org.fcitx.Fcitx5.BambooMintKey1.GetVietnameseMode` | Trả `boolean true/false` |
| Kiểm thử C-ABI | `python3 scripts/tests/test-cabi.py publish/linux-x64/BambooMintKeyCore.so` | `9 passed, 0 failed` |

---

## 10. Cấu hình

File cấu hình đặt tại `~/.config/bamboomintkey/config.json` (theo chuẩn XDG).

Các trường chính:

| Trường | Ý nghĩa | Giá trị |
|---|---|---|
| `isVietnameseMode` | Trạng thái V/E mặc định | `true` / `false` |
| `toneStyle` | Kiểu đặt dấu | `0` = mới (`hòa`), `1` = cũ (`hoà`) |
| `autoRestoreEnglishWords` | Tự khôi phục từ tiếng Anh | `true` / `false` |
| `allowRepeatKeyUndo` | Gõ lặp phím để undo | `true` / `false` |
| `allowLeadingWAsU` | `w` đầu từ thành `ư` | `true` / `false` |
| `allowFreeTonePlacement` | Bỏ dấu tự do | `true` / `false` |

Thay đổi file này sẽ được addon tự nạp lại qua cơ chế `inotify` (không cần restart Fcitx5).

---

## 11. Gỡ cài đặt

Cách đơn giản nhất:

```bash
./scripts/linux/uninstall_linux.sh
```

Script tự dọn cả `/usr` (bao gồm `/usr/lib64` trên Fedora) lẫn `~/.local` rồi restart Fcitx5. Nếu muốn gỡ thủ công:

```bash
sudo rm -rf /usr/lib/*/fcitx5/libbamboomintkey.so \
            /usr/lib/*/fcitx5/BambooMintKeyCore.so \
            /usr/lib64/fcitx5/libbamboomintkey.so \
            /usr/lib64/fcitx5/BambooMintKeyCore.so \
            /usr/lib64/bamboomintkey \
            /usr/lib/bamboomintkey \
            /usr/share/fcitx5/addon/bamboomintkey.conf \
            /usr/share/fcitx5/inputmethod/bamboomintkey.conf \
            /usr/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey*.svg \
            /usr/share/icons/hicolor/scalable/apps/bamboomintkey.svg \
            /usr/share/applications/bamboomintkey-settings.desktop \
            /usr/bin/bamboomintkey-ui \
            /usr/local/bin/bamboomintkey-ui

# Gỡ bản cài đặt user (nếu có)
rm -f ~/.local/share/applications/bamboomintkey-settings.desktop \
      ~/.local/share/icons/hicolor/scalable/apps/bamboomintkey.svg \
      ~/.local/bin/bamboomintkey-ui
```

---

## 12. Khắc phục sự cố thường gặp

| Vấn đề | Nguyên nhân / cách xử lý |
|---|---|
| Bộ gõ hiện trong danh sách nhưng ở trạng thái "Not Available" | Do không nạp được thư viện `BambooMintKeyCore.so` hoặc thiếu dependency. Chạy `killall fcitx5 && fcitx5 -v` để xem log trực tiếp, hoặc `ldd -r /usr/lib*/fcitx5/libbamboomintkey.so`. Vấn đề này đã được khắc phục triệt để từ bản v1.1.0+ bằng `SONAME` và `$ORIGIN` (xem [Issue 010](3.Issue/010_LinuxPackaging_NotAvailable_Fix.md)). |
| Không chọn được input method nào | Có addon trùng lặp cài ở nhiều nơi (`/usr` và `/usr/local`) → dọn sạch file addon cũ, chỉ giữ 1 bản |
| Không thấy addon sau khi cài | Chưa restart: chạy `fcitx5 -r`; hoặc sai prefix (`/usr/local` thay vì `/usr`) |
| Icon không hiện | Chạy `gtk-update-icon-cache /usr/share/icons/hicolor` |
| Gõ không ra tiếng Việt | Đang ở chế độ E → bấm `` ` `` để chuyển V |
| Không gõ được tiếng Việt trong Steam | Steam (32-bit) không nạp được `libfcitx5gclient.so` (64-bit) → đổi `GTK_IM_MODULE=fcitx` thành `xim` (xem mục 8.1.1), rồi đăng xuất/đăng nhập lại |
| Một số app (vd Zed) vẫn gạch chân preedit | Zed tự vẽ gạch chân composition, bỏ qua cờ `NoFlag` — giới hạn phía app |

---

## 13. Kiến trúc tổng quan

```
Ứng dụng (GTK/Qt/Zed...)
        │ Fcitx5 (keyEvent, preedit, commit)
        ▼
libbamboomintkey.so (addon C++)
        │ C-ABI (bmk_*)
        ▼
BambooMintKeyCore.so (F# engine, NativeAOT)
```

Xem thêm:
- Kiến trúc hệ thống toàn diện: `docs/SYSTEM_ARCHITECTURE.md`
- Thiết kế C-ABI: `docs/2.Design/Phase7/007_03_CoreNative_CABI_Design.md`
- Thiết kế addon: `docs/2.Design/Phase7/007_04_Fcitx5_Addon_Design.md`
- Thiết kế Flatpak độc lập: `docs/2.Design/Phase9/009_02_Flathub_Independent_Architecture.md`
- Theo dõi tiến độ: `docs/4.Progress/002_LinuxProgressTracking.md`
