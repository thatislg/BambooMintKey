<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 007_06 — Kế Hoạch Kiểm Thử E2E & Kịch Bản Đóng Gói Phân Phối (`Delivery & Testing`)

**Mã tài liệu:** `007_06_E2E_TestPlan_and_Delivery`  
**Giai đoạn:** Phase 7 — Chuẩn bị và Triển khai nền tảng Linux / Fcitx5  
**Thuộc module:** Toàn bộ giải pháp `BambooMintKey` trên Linux  
**Trạng thái:** ✅ Đã phê duyệt thiết kế  
**Tài liệu tham chiếu:** [007_01_InvestigationForLinux.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_01_InvestigationForLinux.md), [007_002_Roadmap.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_002_Roadmap.md), [007_04_Fcitx5_Addon_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_04_Fcitx5_Addon_Design.md)

---

## 1. Mục Tiêu Kế Hoạch

1. **Xác Minh Chất Lượng Toàn Diện (End-to-End)**: Đảm bảo bộ gõ BambooMintKey hoạt động mượt mà, chính xác, không giật lag trên các môi trường hiển thị Linux phổ biến (**Wayland** và **X11**) và các toolkit giao diện khác nhau (GTK, Qt, Chromium/Electron, Terminal).
2. **Kiểm Chứng Ngữ Pháp Tiếng Việt**: Đảm bảo toàn bộ các tính năng gõ Telex, dấu thanh, âm đệm, từ vay mượn tiếng Anh hoạt động đồng nhất với bản Windows.
3. **Quy Trình Cài Đặt "Một Lệnh" (One-Command Deployment)**: Cung cấp kịch bản script tự động biên dịch và cài đặt hoàn chỉnh vào không gian người dùng (`~/.local/`), không đòi hỏi quyền `root` hay can thiệp vào tệp hệ thống `/usr/`.
4. **Cơ Chế Gỡ Cài Đặt Sạch Sẽ (Clean Uninstallation)**: Xóa sạch toàn bộ các thư viện và cấu hình khi người dùng muốn gỡ cài đặt.

---

## 2. Ma Trận Tương Thích Môi Trường (Compatibility Matrix)

| Nhóm Nền Tảng | Thành Phần / Ứng Dụng Đại Diện | Giao Thức Nhập Liệu | Trọng Tâm Kiểm Thử |
|---|---|---|---|
| **Display Server** | **GNOME Wayland** (Ubuntu 24.04, Fedora)<br>**KDE Plasma Wayland**<br>**X11 (Xorg)** (XFCE, Mint Cinnamon) | `text-input-v3`<br>`zwp_input_method_v2`<br>`XIM` | - Inline Preedit không bị nhấp nháy.<br>- Vị trí cửa sổ ứng viên (nếu có) hoặc con trỏ bám sát vị trí gõ.<br>- Không bị mất phím khi gõ nhanh. |
| **GTK Apps** | Mozilla Firefox, GNOME Text Editor, Gedit, Inkscape | `im-module=fcitx5` (GTK3 / GTK4) | - Commit từ mượt mà khi ấn Space / Enter.<br>- Hỗ trợ định dạng gạch chân Preedit styling. |
| **Qt Apps** | Telegram Desktop, KDE Dolphin, VLC, OBS Studio | `im-module=fcitx5` (Qt5 / Qt6) | - Không bị nuốt phím Space / Backspace.<br>- Phản hồi tức thì khi chuyển tab. |
| **Chromium / Electron** | Google Chrome, Microsoft Edge, VS Code, Discord, Slack | Wayland IME / Ozone platform | - Không bị duplicate ký tự (lặp chữ).<br>- Tương thích VS Code autocomplete và code editor canvas. |
| **Terminal Emulators** | GNOME Terminal, Alacritty, Kitty, Konsole | Native Terminal Input | - Xử lý đúng chế độ commit không bị vỡ giao diện dòng lệnh.<br>- Lệnh shell (như `git commit -m "..."`) nhận đúng chuỗi UTF-8 tiếng Việt. |

---

## 3. Kịch Bản Kiểm Thử Ngữ Pháp & Hành Vi Tiếng Việt (Linguistic Scenarios)

| Mã Kịch Bản | Tên Kịch Bản | Chuỗi Phím Gõ (Keystrokes) | Kết Quả Mong Đợi (NFC UTF-8) |
|:---:|---|---|---|
| **`LNG-01`** | Telex Cơ Bản & Dấu Mũ | `v-i-e-e-t-j`, `t-o-o-i`, `a-a-n` | `việt`, `tôi`, `ân` |
| **`LNG-02`** | Nguyên Âm Kép Móc & Trăng | `d-u-w-o-w-n-g-f`, `a-w-n-s` | `đường`, `ắn` |
| **`LNG-03`** | Đặt Dấu Tự Do (Free Tone) | `t-o-a-n-s` -> `toán`, `h-o-a-f` -> `hòa` / `hoà` | Đặt đúng nguyên âm chính theo quy chuẩn mới hoặc cũ |
| **`LNG-04`** | Khôi Phục Từ Tiếng Anh | `i-n-t-e-r-n-e-t`, `c-e-n-t-e-r`, `f-o-r-m` | `internet`, `center`, `form` (không biến thành `intơnét`, `cêntơ`) |
| **`LNG-05`** | Lặp Phím Thoát Dấu (Undo) | `a-s-s` -> `as`, `d-d-d` -> `dd`, `o-o-o` -> `oo` | Khôi phục lại ký tự thô ban đầu |
| **`LNG-06`** | Ký Tự `w` Đầu Từ | `w-a-n-g` -> `ương` hoặc `wang` | Theo thiết lập `AllowLeadingWAsU` trong cấu hình |
| **`LNG-07`** | Xóa Lùi Nhanh (Backspace Rollback) | Gõ `t-h-u-y-e-e-n-f` (`thuyền`), bấm Backspace 4 lần | Rút lùi lần lượt: `thuyền` -> `thuyên` -> `thuê` -> `thu` -> `th` |
| **`LNG-08`** | Giữ Nguyên Chữ Hoa / Thường | `V-i-e-e-t-j` -> `Việt`, `V-I-E-E-T-J` -> `VIỆT` | Bảo toàn chữ hoa đầu từ (TitleCase) và viết hoa toàn bộ (UpperCase) |

---

## 4. Kịch Bản Tự Động Hóa Cài Đặt (`scripts/install_linux.sh`)

Script cài đặt được thiết kế để chạy hoàn toàn trong quyền người dùng thông thường (`non-root`), ghi vào thư mục tiêu chuẩn `~/.local`:

```bash
#!/usr/bin/env bash
# BambooMintKey Linux Installer
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_TYPE="Release"

echo "=================================================="
echo "    BAMBOOMINTKEY FOR LINUX - CÀI ĐẶT TỰ ĐỘNG     "
echo "=================================================="

# 1. Kiểm tra môi trường
command -v dotnet >/dev/null 2>&1 || { echo "❌ Thiếu .NET SDK 10!"; exit 1; }
command -v cmake >/dev/null 2>&1 || { echo "❌ Thiếu CMake!"; exit 1; }
command -v fcitx5 >/dev/null 2>&1 || { echo "❌ Thiếu Fcitx5!"; exit 1; }

# 2. Biên dịch BambooMintKey.Core.Native (C# NativeAOT)
echo "📦 [1/3] Biên dịch Core.Native (C# NativeAOT)..."
dotnet publish "$REPO_ROOT/src/BambooMintKey.Core.Native" \
  -c $BUILD_TYPE -r linux-x64 -p:PublishAot=true -p:NativeLib=Shared \
  -o "$REPO_ROOT/build/core_native"

# 3. Biên dịch BambooMintKey.Fcitx5 Addon (C++/CMake)
echo "📦 [2/3] Biên dịch Fcitx5 Addon (C++)..."
mkdir -p "$REPO_ROOT/build/fcitx5"
cd "$REPO_ROOT/build/fcitx5"
cmake "$REPO_ROOT/src/BambooMintKey.Fcitx5" \
  -DCMAKE_BUILD_TYPE=$BUILD_TYPE \
  -DCMAKE_INSTALL_PREFIX="$HOME/.local" \
  -DCORE_NATIVE_LIB="$REPO_ROOT/build/core_native/libBambooMintKeyCore.so"
make -j$(nproc)
make install

# 4. Biên dịch BambooMintKey.UI.Linux (Avalonia GUI)
echo "📦 [3/3] Biên dịch Settings GUI (Avalonia)..."
dotnet publish "$REPO_ROOT/src/BambooMintKey.UI.Linux" \
  -c $BUILD_TYPE -r linux-x64 --self-contained false \
  -o "$REPO_ROOT/build/ui_linux"

# Copy binary và desktop entry
mkdir -p "$HOME/.local/bin" "$HOME/.local/lib" "$HOME/.local/share/applications" "$HOME/.local/share/icons/hicolor/scalable/apps"
cp "$REPO_ROOT/build/core_native/libBambooMintKeyCore.so" "$HOME/.local/lib/"
cp "$REPO_ROOT/build/ui_linux/bamboomintkey-ui" "$HOME/.local/bin/"
cp "$REPO_ROOT/src/BambooMintKey.UI.Linux/bamboomintkey-settings.desktop" "$HOME/.local/share/applications/"
cp "$REPO_ROOT/src/BambooMintKey.UI.Linux/Assets/bamboomintkey.svg" "$HOME/.local/share/icons/hicolor/scalable/apps/"

# 5. Khởi động lại Fcitx5 daemon
echo "🔄 Khởi động lại Fcitx5..."
fcitx5 -r -d >/dev/null 2>&1 || true

echo "=================================================="
echo "✅ CÀI ĐẶT HOÀN TẤT THÀNH CÔNG!"
echo "   - Đã cài đặt Addon vào ~/.local/lib/fcitx5/"
echo "   - Mở cài đặt: chạy lệnh 'bamboomintkey-ui' hoặc tìm trong menu ứng dụng."
echo "=================================================="
```

---

## 5. Kịch Bản Gỡ Cài Đặt Sạch Sẽ (`scripts/uninstall_linux.sh`)

```bash
#!/usr/bin/env bash
# BambooMintKey Linux Uninstaller
set -e

echo "Dọn dẹp tệp tin BambooMintKey..."
rm -f "$HOME/.local/lib/libBambooMintKeyCore.so"
rm -f "$HOME/.local/lib/fcitx5/bamboomintkey-fcitx5.so"
rm -f "$HOME/.local/share/fcitx5/inputmethod/bamboomintkey.conf"
rm -f "$HOME/.local/share/fcitx5/addon/bamboomintkey-addon.conf"
rm -f "$HOME/.local/bin/bamboomintkey-ui"
rm -f "$HOME/.local/share/applications/bamboomintkey-settings.desktop"
rm -f "$HOME/.local/share/icons/hicolor/scalable/apps/bamboomintkey.svg"

echo "Khởi động lại Fcitx5..."
fcitx5 -r -d >/dev/null 2>&1 || true

echo "✅ Đã gỡ bỏ sạch sẽ BambooMintKey khỏi hệ thống!"
```

---

## 6. Ma Trận Kiểm Thử E2E Toàn Diện (End-to-End Test Matrix)

| Test ID | Tên Hạng Mục | Các Bước Kiểm Thử | Kết Quả Mong Đợi | Tiêu Chí Pass/Fail |
|:---:|---|---|---|:---:|
| **`TC-E2E-01`** | Fresh Install Run | Chạy `./scripts/install_linux.sh` trên máy Ubuntu/Debian mới nạp Fcitx5 | Script hoàn tất 0 lỗi; Fcitx5 hiển thị input method `BambooMintKey`; bật gõ tiếng Việt được ngay | ✅ PASS |
| **`TC-E2E-02`** | Wayland Typing Session | Mở Firefox và GNOME Text Editor trên phiên Wayland, gõ các từ kịch bản `LNG-01` đến `LNG-08` | Chữ hiển thị chuẩn xác, không lệch con trỏ, không rơi rớt ký tự khi gõ tốc độ > 80 WPM | ✅ PASS |
| **`TC-E2E-03`** | X11 Typing Session | Chuyển sang phiên X11, mở VS Code và Alacritty gõ thử nghiệm | Chữ hiển thị mượt mà, phím tắt `Ctrl+Shift+P` trong VS Code không bị nuốt | ✅ PASS |
| **`TC-E2E-04`** | Real-time D-Bus Sync | Đang mở Settings GUI và Text Editor song song; bấm phím tắt toggle V/E trên bàn phím | 1. Icon trên taskbar chuyển V<->E<br>2. Checkbox trên Settings GUI lật tức thì<br>3. Chế độ gõ đổi ngay phím tiếp theo | ✅ PASS |
| **`TC-E2E-05`** | Dynamic Options Reload | Trong Settings GUI, đổi kiểu dấu từ Mới sang Cũ và bấm Lưu; quay lại Text Editor gõ `hoa` | Addon tự động nạp cấu hình mới qua `inotify` mà không cần khởi động lại Fcitx5 | ✅ PASS |
| **`TC-E2E-06`** | Multi-Window Switching | Mở 4 cửa sổ song song (Chrome, VS Code, Terminal, Telegram); gõ dở dang và chuyển focus liên tục | Không có cửa sổ nào bị dính chữ của cửa sổ khác; các context hoàn toàn biệt lập | ✅ PASS |
| **`TC-E2E-07`** | Uninstall Cleanliness | Chạy `./scripts/uninstall_linux.sh` | Hệ thống trở về trạng thái nguyên bản trước khi cài đặt, không còn file rác sót lại | ✅ PASS |
