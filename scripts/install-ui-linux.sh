#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Cài đặt giao diện Cài đặt BambooMintKey (BambooMintKey.UI.Linux) trên Linux.
#
# Thực hiện:
#   1. dotnet publish ứng dụng Avalonia (framework-dependent).
#   2. Tạo launcher `bamboomintkey-ui` trên PATH (để Fcitx5 addon gọi được).
#   3. Cài desktop entry.
#   4. Cài icon ứng dụng.
#   5. Làm mới cache icon / desktop database.
#
# Cách dùng:
#   ./scripts/install-ui-linux.sh          # launcher /usr/local/bin, desktop+icon /usr/share (cần sudo)
#   ./scripts/install-ui-linux.sh --user   # launcher+desktop+icon vào ~/.local (không cần sudo)

set -euo pipefail

# Resolve thư mục gốc dự án (script nằm trong scripts/).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PROJECT="$PROJECT_ROOT/src/BambooMintKey.UI.Linux/BambooMintKey.UI.Linux.fsproj"
PUBLISH_DIR="$PROJECT_ROOT/publish/ui-linux"
BIN_NAME="BambooMintKey.UI.Linux"
APP_ID="bamboomintkey-settings"
ICON_SRC="$PROJECT_ROOT/src/media/bamboo_mint_key_ico.svg"
DESKTOP_SRC="$PROJECT_ROOT/src/BambooMintKey.UI.Linux/bamboomintkey-settings.desktop"

USER_INSTALL=0
if [ "${1:-}" = "--user" ]; then
    USER_INSTALL=1
fi

# ---------------------------------------------------------------------------
# 1. Publish
# ---------------------------------------------------------------------------
echo "==> Publishing $PROJECT ..."
dotnet publish "$PROJECT" -c Release -o "$PUBLISH_DIR"

if [ ! -x "$PUBLISH_DIR/$BIN_NAME" ]; then
    echo "Lỗi: không tìm thấy apphost $PUBLISH_DIR/$BIN_NAME sau khi publish." >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# 2. Launcher trên PATH
# ---------------------------------------------------------------------------
if [ "$USER_INSTALL" -eq 1 ]; then
    BIN_DIR="$HOME/.local/bin"
    mkdir -p "$BIN_DIR"
    ln -sf "$PUBLISH_DIR/$BIN_NAME" "$BIN_DIR/bamboomintkey-ui"
else
    BIN_DIR="/usr/local/bin"
    if [ -w "$BIN_DIR" ]; then
        ln -sf "$PUBLISH_DIR/$BIN_NAME" "$BIN_DIR/bamboomintkey-ui"
    else
        echo "==> Cần quyền ghi $BIN_DIR (dùng sudo)."
        sudo ln -sf "$PUBLISH_DIR/$BIN_NAME" "$BIN_DIR/bamboomintkey-ui"
    fi
fi
echo "==> Launcher: $BIN_DIR/bamboomintkey-ui -> $PUBLISH_DIR/$BIN_NAME"

if [ "$USER_INSTALL" -eq 1 ]; then
    echo "    Lưu ý: đảm bảo $BIN_DIR nằm trong PATH của phiên đăng nhập để"
    echo "    Fcitx5 addon gọi được 'bamboomintkey-ui'."
fi

# ---------------------------------------------------------------------------
# 3. Desktop entry
# ---------------------------------------------------------------------------
if [ "$USER_INSTALL" -eq 1 ]; then
    APPS_DIR="$HOME/.local/share/applications"
    ICONS_DIR="$HOME/.local/share/icons/hicolor/scalable/apps"
    ICON_THEME_DIR="$HOME/.local/share/icons/hicolor"
    SUDO=""
else
    APPS_DIR="/usr/share/applications"
    ICONS_DIR="/usr/share/icons/hicolor/scalable/apps"
    ICON_THEME_DIR="/usr/share/icons/hicolor"
    if [ "$(id -u)" -eq 0 ]; then
        SUDO=""
    else
        SUDO="sudo"
    fi
fi

$SUDO mkdir -p "$APPS_DIR" "$ICONS_DIR"
$SUDO cp "$DESKTOP_SRC" "$APPS_DIR/$APP_ID.desktop"
$SUDO cp "$ICON_SRC" "$ICONS_DIR/bamboomintkey.svg"
echo "==> Desktop entry: $APPS_DIR/$APP_ID.desktop"
echo "==> Icon: $ICONS_DIR/bamboomintkey.svg"

# ---------------------------------------------------------------------------
# 5. Làm mới cache
# ---------------------------------------------------------------------------
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    $SUDO gtk-update-icon-cache "$ICON_THEME_DIR" 2>/dev/null || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
    $SUDO update-desktop-database "$APPS_DIR" 2>/dev/null || true
fi

echo "==> Xong. Chạy 'bamboomintkey-ui' hoặc tìm 'BambooMintKey Settings' trong menu ứng dụng."
