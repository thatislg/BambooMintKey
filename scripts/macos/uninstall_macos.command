#!/bin/bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Uninstaller double-click cho người dùng cuối: click đúp để chạy trong Terminal,
# không cần gõ lệnh thủ công. Tự xóa mọi vị trí cài đặt (user-local lẫn system-wide).
#
# File này được đóng gói vào .dmg và .pkg (xem scripts/macos/package_macos.sh).
set -euo pipefail

USER_INPUT_METHODS="$HOME/Library/Input Methods"
USER_APPLICATIONS="$HOME/Applications"
SYSTEM_INPUT_METHODS="/Library/Input Methods"
SYSTEM_APPLICATIONS="/Applications"
CONFIG_DIR="$HOME/Library/Application Support/BambooMintKey"

echo "=================================================="
echo "  Gỡ cài đặt BambooMintKey khỏi macOS"
echo "=================================================="

echo "==> Dừng tiến trình bộ gõ..."
pkill -f "BambooMintKey.app/Contents/MacOS/BambooMintKey" 2>/dev/null || true
pkill -f "BambooMintKeyStatusBar" 2>/dev/null || true
pkill -f "BambooMintKey.UI.Mac" 2>/dev/null || true

remove_user() {
    if [ -d "$1" ]; then
        echo "==> Xóa $1"
        rm -rf "$1"
    fi
}
remove_system() {
    if [ -d "$1" ]; then
        echo "==> Xóa $1 (nhập mật khẩu quản trị khi được hỏi)"
        sudo rm -rf "$1"
    fi
}

remove_user "$USER_INPUT_METHODS/BambooMintKey.app"
remove_user "$USER_APPLICATIONS/BambooMintKey.app"
remove_user "$USER_APPLICATIONS/BambooMintKey.UI.app"
remove_user "$USER_APPLICATIONS/BambooMintKeySettings.app"
remove_system "$SYSTEM_INPUT_METHODS/BambooMintKey.app"
remove_system "$SYSTEM_APPLICATIONS/BambooMintKey.app"

killall TextInputMenuAgent 2>/dev/null || true
killall TextInputSwitcher 2>/dev/null || true

echo "==> Giữ lại cấu hình tại $CONFIG_DIR"
echo "    (xóa tay thư mục này nếu muốn gỡ hoàn toàn)"
echo "=================================================="
echo "  ✅ Đã gỡ cài đặt BambooMintKey."
echo "  Nếu mục BambooMintKey còn sót, vào Cài đặt hệ thống ->"
echo "  Bàn phím -> Nguồn nhập liệu -> bấm [-] để xóa."
echo "=================================================="
read -r -p "Nhấn Enter để đóng..." _ || true
