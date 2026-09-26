<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Hướng Dẫn Build BambooMintKey trên Linux (Fcitx5)

Tài liệu hướng dẫn build và cài đặt bộ gõ BambooMintKey trên Linux, sử dụng Fcitx5 làm input method framework.

> Các đường dẫn trong tài liệu dùng ký hiệu chung: `~` = thư mục home của người dùng, `$HOME` tương đương, `$XDG_CONFIG_HOME` = thư mục config XDG (mặc định `~/.config`).

---

## 1. Yêu cầu hệ thống

| Thành phần | Phiên bản tối thiểu | Ghi chú |
|---|---|---|
| Fcitx5 | 5.1.x | Input method framework |
| .NET SDK | 10.0 | Build `Core.Native` (NativeAOT) |
| CMake | 3.16+ | Build addon C++ |
| g++ (GCC) | C++17 | Compiler C++ |
| clang + lld | — | Bắt buộc cho NativeAOT |

**Cài dependencies (Ubuntu/Debian/Linux Mint):**

```bash
sudo apt update
sudo apt install -y \
    fcitx5 \
    libfcitx5core-dev \
    libfcitx5config-dev \
    libfcitx5utils-dev \
    cmake g++ clang lld
```

> Fedora/RHEL dùng `dnf install fcitx5-devel clang lld` (tên gói có thể khác).

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

| File | Đường dẫn |
|---|---|
| `libbamboomintkey.so` | `/usr/lib/<multiarch>/fcitx5/` |
| `BambooMintKeyCore.so` | `/usr/lib/<multiarch>/fcitx5/` (cạnh addon, cho rpath `$ORIGIN`) |
| `bamboomintkey.conf` (metadata addon) | `/usr/share/fcitx5/addon/` |
| `bamboomintkey.conf` (input method) | `/usr/share/fcitx5/inputmethod/` |
| `fcitx_bamboomintkey*.svg` (icon V/E) | `/usr/share/icons/hicolor/scalable/apps/` |

> Nếu muốn cài vào thư mục user (không cần sudo), đổi `-DCMAKE_INSTALL_PREFIX=~/.local` và đặt file thủ công vào `~/.local/share/fcitx5/` + `~/.local/lib/fcitx5/`.

---

## 5. Kích hoạt bộ gõ

```bash
# Restart Fcitx5 để nạp addon mới
fcitx5 -r
```

Sau đó mở **Fcitx5 Configuration** → tab **Input Method**:

1. Tìm **BambooMintKey** (mục tiếng Việt, `LangCode=vi`) trong danh sách bên phải.
2. Thêm vào nhóm input method hiện tại của bạn.

> Nếu icon chưa hiện, chạy `gtk-update-icon-cache /usr/share/icons/hicolor` (hoặc `~/.local/share/icons/hicolor` nếu cài user).

---

## 6. Kiểm tra

| Việc cần test | Thao tác | Kết quả mong đợi |
|---|---|---|
| Gõ Telex | Gõ `duowngf` rồi `space` | Hiện **"đường "** |
| Chuyển V/E | Bấm phím `` ` `` (dưới Esc) | Đổi V ↔ E (gõ tiếng Việt ↔ tiếng Anh) |
| Icon V/E | Quan sát thanh trạng thái | Icon đổi V ↔ E |
| D-Bus | `dbus-send --session --print-reply --dest=org.fcitx.Fcitx5.BambooMintKey /org/fcitx/Fcitx5/BambooMintKey org.fcitx.Fcitx5.BambooMintKey1.GetVietnameseMode` | Trả `boolean true/false` |

---

## 7. Cấu hình

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

## 8. Gỡ cài đặt

```bash
sudo rm -f /usr/lib/*/fcitx5/libbamboomintkey.so \
           /usr/lib/*/fcitx5/BambooMintKeyCore.so \
           /usr/share/fcitx5/addon/bamboomintkey.conf \
           /usr/share/fcitx5/inputmethod/bamboomintkey.conf \
           /usr/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey*.svg
```

---

## 9. Khắc phục sự cố thường gặp

| Vấn đề | Nguyên nhân / cách xử lý |
|---|---|
| Không chọn được input method nào | Có addon trùng lặp cài ở nhiều nơi (`/usr` và `/usr/local`) → dọn sạch file addon cũ, chỉ giữ 1 bản |
| Không thấy addon sau khi cài | Chưa restart: chạy `fcitx5 -r`; hoặc sai prefix (`/usr/local` thay vì `/usr`) |
| Icon không hiện | Chạy `gtk-update-icon-cache` |
| Gõ không ra tiếng Việt | Đang ở chế độ E → bấm `` ` `` để chuyển V |
| Một số app (vd Zed) vẫn gạch chân preedit | Zed tự vẽ gạch chân composition, bỏ qua cờ `NoFlag` — giới hạn phía app |

---

## 10. Kiến trúc tổng quan

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
- Thiết kế C-ABI: `docs/2.Design/Phase7/007_03_CoreNative_CABI_Design.md`
- Thiết kế addon: `docs/2.Design/Phase7/007_04_Fcitx5_Addon_Design.md`
- Theo dõi tiến độ: `docs/4.Progress/002_LinuxProgressTracking.md`
