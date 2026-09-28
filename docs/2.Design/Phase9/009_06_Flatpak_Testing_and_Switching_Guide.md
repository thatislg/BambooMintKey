<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Hướng Dẫn Kiểm Thử & Chuyển Đổi Qua Lại: Fcitx 5 Native vs Fcitx 5 Flatpak

**Mã tài liệu:** `009_Flatpak_Testing_and_Switching_Guide`  
**Giai đoạn:** Phase 9 — Hỗ trợ Steam Deck & Flathub Addon  
**Đối tượng:** Lập trình viên & Người kiểm thử bộ gõ trên Linux  

---

## 1. Bản Chất Kiến Trúc Giữa 2 Môi Trường

Trên Linux, bạn có thể chạy Fcitx 5 theo hai phương thức hoàn toàn độc lập:

| Đặc tính | Fcitx 5 Hệ Thống (Native APT/DEB) | Fcitx 5 Sandbox (Flatpak / Steam Deck) |
|---|---|---|
| **Mục đích** | Dành cho desktop truyền thống (Ubuntu, Linux Mint, Debian). | Dành cho hệ điều hành chỉ đọc như SteamOS (Steam Deck) hoặc Fedora Silverblue. |
| **Đường dẫn daemon** | `/usr/bin/fcitx5` | `flatpak run org.fcitx.Fcitx5` |
| **Vị trí Addon .so** | `/usr/lib/x86_64-linux-gnu/fcitx5/` | `~/.local/share/flatpak/runtime/org.fcitx.Fcitx5.Addon.BambooMintKey/.../lib/fcitx5/` |
| **Thư mục cấu hình** | `~/.config/fcitx5/` | `~/.var/app/org.fcitx.Fcitx5/config/fcitx5/` |
| **Cơ chế phân quyền** | Toàn quyền truy cập hệ thống host. | Bị cô lập trong sandbox container, giao tiếp qua Session D-Bus và Wayland sockets. |

---

## 2. Công Cụ Chuyển Đổi 1-Click (`switch_fcitx.sh`)

Dự án cung cấp sẵn script [`scripts/linux/switch_fcitx.sh`](../../scripts/linux/switch_fcitx.sh) để chuyển đổi an toàn và không gây xung đột socket D-Bus:

### 2.1. Kiểm tra trạng thái hiện tại
```bash
./scripts/linux/switch_fcitx.sh status
```
*In ra chế độ đang chạy (Native hay Flatpak), PID tiến trình, và Input Method đang kích hoạt.*

### 2.2. Chuyển sang Fcitx 5 Flatpak (Môi trường Steam Deck)
```bash
./scripts/linux/switch_fcitx.sh flatpak
```
*Script sẽ:*
1. Dừng tiến trình Fcitx 5 native.
2. Tự động đồng bộ các icon `org.fcitx.Fcitx5.fcitx_bamboomintkey.svg` vào cache icon của máy host.
3. Khởi chạy `flatpak run org.fcitx.Fcitx5 -d &`.

### 2.3. Chuyển về Fcitx 5 Hệ Thống (Môi trường Linux Mint gốc)
```bash
./scripts/linux/switch_fcitx.sh native
```
*Script sẽ:*
1. Dừng tiến trình Fcitx 5 Flatpak container.
2. Khởi chạy `/usr/bin/fcitx5 -d &` của hệ điều hành.

### 2.4. Mở cấu hình bộ gõ tương ứng
```bash
./scripts/linux/switch_fcitx.sh config
```
*Tự động phát hiện phiên bản đang chạy để mở đúng bảng `fcitx5-configtool`.*

---

## 3. Các Lệnh Thủ Công (Manual Commands)

Nếu không sử dụng script, bạn có thể gõ trực tiếp trong terminal:

### Chuyển sang Flatpak:
```bash
killall fcitx5 2>/dev/null || true
flatpak run org.fcitx.Fcitx5 -d &
```

### Chuyển về Native:
```bash
flatpak kill org.fcitx.Fcitx5 2>/dev/null || killall fcitx5 2>/dev/null || true
/usr/bin/fcitx5 -d &
```

---

## 4. Giải Thích Hiện Tượng "Một Số Ứng Dụng Không Nhận Tiếng Việt"

Khi chạy Fcitx 5 Flatpak, bạn có thể nhận thấy:
- **Gõ được tốt:** Terminal (Konsole, GNOME Terminal), Notepad / KWrite, LibreOffice.
- **Không gõ được hoặc chập chờn:** Trình duyệt (Chrome, Brave), Trình soạn thảo GPU (Zed Editor).

### Nguyên nhân kỹ thuật:
1. **Ứng dụng mở trước khi chuyển Fcitx 5:**
   - Khi Fcitx 5 daemon bị tắt và bật lại dưới Flatpak, các ứng dụng GUI phức tạp (Chrome, Zed) giữ socket kết nối cũ và **không tự động tái kết nối** (reconnect) với máy chủ input method mới.
   - **Cách xử lý:** Đóng ứng dụng đó và mở lại sau khi đã chạy `./scripts/linux/switch_fcitx.sh flatpak`.

2. **Cơ chế giao tiếp Input Method trên Wayland:**
   - Fcitx 5 Flatpak giao tiếp với ứng dụng ngoài host thông qua Session D-Bus và Wayland `zwp_text_input_v3`.
   - **Google Chrome / Chromium native:** Theo mặc định trên Wayland, Chrome không tự bật cờ Wayland IME. Cần khởi chạy Chrome với tham số:
     ```bash
     google-chrome --enable-wayland-ime &
     ```
     hoặc bật cờ `chrome://flags/#enable-wayland-ime` trong trình duyệt.
   - **Zed Editor:** Zed được viết bằng Rust (framework GPUI). Hiện tại trên Linux Wayland, GPUI yêu cầu cờ Wayland input method protocol đặc thù hoặc chạy qua XWayland.

3. **Tính chất thiết kế trên Steam Deck (SteamOS):**
   - Trên Steam Deck, hầu hết mọi ứng dụng người dùng cài đặt (Chrome, Discord, Steam Chat, VS Code) **đều được cài dưới dạng Flatpak** thông qua Discover Store.
   - Khi cả ứng dụng VÀ Fcitx 5 đều chạy trong không gian Flatpak, chúng dùng chung runtime Fcitx5 client module và giao tiếp trực tiếp 100% mượt mà không gặp rào cản phân quyền của host.
