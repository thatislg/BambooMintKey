#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Khởi chạy Steam với đúng môi trường IME cho Fcitx5 (XIM qua CEF/GTK).
#
# Steam (app 32-bit) dùng CEF (nền GTK) cho ô nhập liệu. Vì là 32-bit nên
# không nạp được libfcitx5gclient.so (64-bit), do đó phải dùng XIM thuần túy
# thay vì kênh GTK IM module fcitx. Biến GTK_IM_MODULE=xim + XMODIFIERS=@im=fcitx
# giúp Steam kết nối XIM server của Fcitx5 (native hoặc flatpak đều được).
#
# Cách dùng:
#   ./scripts/linux/steam-ime.sh            # chạy Steam với IME
#   ./scripts/linux/steam-ime.sh --install  # cài wrapper + desktop entry (thường trực)
#   ./scripts/linux/steam-ime.sh --help

set -euo pipefail

if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
    echo "Cách dùng: $0 [--install]"
    echo "  (không đối số)  Chạy Steam với môi trường IME"
    echo "  --install       Cài wrapper ~/.local/bin/steam + desktop entry"
    exit 0
fi

STEAM_BIN=""
for cand in /usr/games/steam /usr/bin/steam steam; do
    if command -v "$cand" >/dev/null 2>&1; then
        STEAM_BIN="$cand"
        break
    fi
done

if [ "${1:-}" = "--install" ]; then
    # 1. Wrapper dòng lệnh
    WRAP="$HOME/.local/bin/steam"
    mkdir -p "$HOME/.local/bin"
    cat > "$WRAP" <<EOF
#!/usr/bin/env bash
# Steam + IME (do BambooMintKey sinh ra)
exec env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" ${STEAM_BIN} "\$@"
EOF
    chmod +x "$WRAP"
    echo "Đã cài wrapper dòng lệnh: $WRAP"

    # 2. Desktop entry override (cho khởi chạy bằng icon/menu)
    DESK_DIR="$HOME/.local/share/applications"
    DESK="$DESK_DIR/steam.desktop"
    mkdir -p "$DESK_DIR"
    SRC_DESK=""
    for d in /usr/share/applications/steam.desktop /usr/local/share/applications/steam.desktop; do
        if [ -f "$d" ]; then SRC_DESK="$d"; break; fi
    done
    if [ -n "$SRC_DESK" ]; then
        cp -f "$SRC_DESK" "$DESK"
        sed -i "s#^Exec=.*#Exec=env XMODIFIERS=\"@im=fcitx\" GTK_IM_MODULE=\"xim\" QT_IM_MODULE=\"xim\" ${STEAM_BIN} %U#" "$DESK"
        # Sửa tên cho đúng (gói steam-installer đặt tên là "Install Steam")
        sed -i "s#^Name=.*#Name=Steam#" "$DESK"
    else
        cat > "$DESK" <<EOF
[Desktop Entry]
Name=Steam
Comment=Steam with Fcitx5 IME
Exec=env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" ${STEAM_BIN} %U
Type=Application
Icon=steam
Categories=Game;
EOF
    fi
    echo "Đã cài desktop entry: $DESK"
    update-desktop-database "$DESK_DIR" 2>/dev/null || true

    echo
    echo "Để áp dụng:"
    echo "  1. TẮT HẲN Steam nếu đang chạy:  killall steam steamwebhelper"
    echo "  2. Chạy lại bằng lệnh 'steam' (dòng lệnh) HOẶC đăng xuất/đăng nhập lại rồi mở bằng icon."
    echo "     (Nếu Steam đang chạy mà mở lại, lệnh sẽ chỉ chuyển tiếp tới phiên cũ — không có IME.)"
    exit 0
fi

exec env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" "${STEAM_BIN}" "$@"
