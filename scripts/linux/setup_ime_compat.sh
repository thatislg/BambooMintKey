#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Thiết lập môi trường IME để các ứng dụng "khó tính" (Chrome, Opera, Zed,
# Steam) kết nối đúng kênh input method của Fcitx 5.
#
# Xem chi tiết: docs/2.Design/Phase9/009_08_App_Compatibility_Fix_Matrix.md
#
# Cách dùng:
#   ./scripts/linux/setup_ime_compat.sh            # Ghi biến môi trường + hướng dẫn
#   ./scripts/linux/setup_ime_compat.sh --dry-run  # Chỉ in, không ghi gì
#   ./scripts/linux/setup_ime_compat.sh --help     # Trợ giúp

set -euo pipefail

DRY_RUN=0
case "${1:-}" in
    --dry-run) DRY_RUN=1 ;;
    --help|-h)
        echo "Cách dùng: $0 [--dry-run]"
        exit 0
        ;;
esac

info()  { echo "[INFO] $*"; }
warn()  { echo "[WARN] $*" >&2; }
step()  { echo; echo "==> $*"; }

write_file() {
    # $1 = đường dẫn, $2 = nội dung
    local path="$1" content="$2"
    if [ "$DRY_RUN" = "1" ]; then
        echo "  [dry-run] sẽ ghi: $path"
        return
    fi
    mkdir -p "$(dirname "$path")"
    printf '%s\n' "$content" > "$path"
    info "Đã ghi: $path"
}

# ---------------------------------------------------------------------------
# 1. Phát hiện phiên làm việc
# ---------------------------------------------------------------------------
SESSION_TYPE="${XDG_SESSION_TYPE:-}"
if [ -z "$SESSION_TYPE" ]; then
    if [ -n "${WAYLAND_DISPLAY:-}" ]; then SESSION_TYPE="wayland";
    elif [ -n "${DISPLAY:-}" ]; then SESSION_TYPE="x11";
    else SESSION_TYPE="unknown"; fi
fi
info "Phiên làm việc phát hiện: ${SESSION_TYPE}"

ENV_BLOCK='# BambooMintKey IME environment (fcitx5)
# GTK dùng XIM vì Steam (app 32-bit) không nạp được libfcitx5gclient.so 64-bit.
GTK_IM_MODULE=xim
QT_IM_MODULE=fcitx
XMODIFIERS=@im=fcitx'

EXPORT_BLOCK='# BambooMintKey IME environment (fcitx5)
# GTK dùng XIM vì Steam (app 32-bit) không nạp được libfcitx5gclient.so 64-bit.
export GTK_IM_MODULE=xim
export QT_IM_MODULE=fcitx
export XMODIFIERS=@im=fcitx'

# ---------------------------------------------------------------------------
# 2. Ghi biến môi trường vào các vị trí phù hợp
# ---------------------------------------------------------------------------
step "Ghi biến môi trường IME (GTK/Qt/XIM)"

# systemd user environment (hoạt động rộng rãi trên mọi distro dùng systemd)
write_file "$HOME/.config/environment.d/bamboomintkey.conf" "$ENV_BLOCK"

case "$SESSION_TYPE" in
    wayland)
        # KDE Plasma Wayland nạp script trong plasma-workspace/env
        if [ -d "$HOME/.config/plasma-workspace" ] || [ "$DRY_RUN" = "1" ]; then
            write_file "$HOME/.config/plasma-workspace/env/bamboomintkey-ime.sh" \
                "#!/bin/sh\n${EXPORT_BLOCK}"
            if [ "$DRY_RUN" != "1" ]; then
                chmod +x "$HOME/.config/plasma-workspace/env/bamboomintkey-ime.sh"
            fi
        fi
        ;;
    x11)
        # Phiên X11 đọc ~/.xprofile khi đăng nhập
        write_file "$HOME/.xprofile" "$EXPORT_BLOCK"
        ;;
    *)
        warn "Không xác định được phiên; chỉ ghi vào environment.d (systemd)."
        ;;
esac

# ---------------------------------------------------------------------------
# 3. Hướng dẫn theo từng ứng dụng
# ---------------------------------------------------------------------------
step "Hướng dẫn cho từng ứng dụng"

cat <<'EOF'
[Steam Client] (XIM qua CEF/GTK — cần GTK_IM_MODULE=xim)
  - Steam là app 32-bit, dùng CEF (GTK) cho ô nhập liệu; không nạp được
    libfcitx5gclient.so (64-bit) nên phải dùng XIM thuần túy.
  - Khởi chạy Steam bằng wrapper đã cài (hoặc tự chạy lệnh sau):
      env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" steam
  - Cài wrapper tự động: ./scripts/linux/steam-ime.sh --install
    (rồi dùng lệnh 'steam' như bình thường, miễn ~/.local/bin đứng trước PATH).

[Google Chrome / Opera] (Flatpak — Wayland IME)
  - Mở chrome://flags/#enable-wayland-ime  ->  Enabled  ->  Relaunch.
  - Hoặc khởi chạy trực tiếp:
      flatpak run com.google.Chrome --enable-wayland-ime
      flatpak run com.opera.Opera   --enable-wayland-ime

[Zed Editor] (Wayland — GPUI)
  - Ưu tiên cập nhật Zed lên bản mới nhất.
  - Workaround chạy qua XWayland + XIM:
      env -u WAYLAND_DISPLAY XMODIFIERS=@im=fcitx zed
EOF

echo
info "Hoàn tất. Đăng xuất/đăng nhập lại (hoặc khởi động lại Fcitx5) để áp dụng."
