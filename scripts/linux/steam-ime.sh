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
#   ./scripts/linux/steam-ime.sh --install  # cài wrapper ~/.local/bin/steam
#   ./scripts/linux/steam-ime.sh --help

set -euo pipefail

if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
    echo "Cách dùng: $0 [--install]"
    echo "  (không đối số)  Chạy Steam với môi trường IME"
    echo "  --install       Cài wrapper vào ~/.local/bin/steam"
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
    WRAP="$HOME/.local/bin/steam"
    mkdir -p "$HOME/.local/bin"
    cat > "$WRAP" <<EOF
#!/usr/bin/env bash
# Steam + IME (do BambooMintKey sinh ra)
exec env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" ${STEAM_BIN} "\$@"
EOF
    chmod +x "$WRAP"
    echo "Đã cài wrapper: $WRAP"
    echo "Đảm bảo $HOME/.local/bin nằm TRƯỚC /usr/bin trong PATH khi chạy 'steam'."
    echo "Kiểm tra: echo \$PATH"
    exit 0
fi

exec env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" "${STEAM_BIN}" "$@"
