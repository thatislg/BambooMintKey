#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Kịch bản gỡ cài đặt sạch sẽ (Clean uninstaller) cho macOS.
# Xóa mọi vị trí cài đặt của bộ gõ IMK và app Cài đặt (cả user-local lẫn
# system-wide), dừng tiến trình đang chạy, và (tùy chọn) xóa cấu hình.
#
# Cách dùng:
#   bash uninstall.sh           # gỡ app, giữ lại cấu hình
#   bash uninstall.sh --purge   # gỡ app + xóa luôn cấu hình
set -euo pipefail

PURGE=false
if [ "${1:-}" = "--purge" ] || [ "${1:-}" = "-p" ]; then
    PURGE=true
fi

USER_INPUT_METHODS="$HOME/Library/Input Methods"
USER_APPLICATIONS="$HOME/Applications"
SYSTEM_INPUT_METHODS="/Library/Input Methods"
SYSTEM_APPLICATIONS="/Applications"
CONFIG_DIR="$HOME/Library/Application Support/BambooMintKey"

echo "=================================================="
echo "  Gỡ cài đặt BambooMintKey khỏi macOS"
echo "=================================================="

# 1. Dừng các tiến trình đang chạy.
echo "==> Dừng tiến trình bộ gõ..."
pkill -f "BambooMintKey.app/Contents/MacOS/BambooMintKey" 2>/dev/null || true
pkill -f "BambooMintKeyStatusBar" 2>/dev/null || true
pkill -f "BambooMintKey.UI.Mac" 2>/dev/null || true

REMOVED_ANY=false

# Xóa một thư mục (user-local) nếu tồn tại.
remove_user() {
    local path="$1"
    if [ -d "$path" ]; then
        echo "==> Xóa $path"
        rm -rf "$path"
        REMOVED_ANY=true
    fi
}

# Xóa một thư mục system-wide (cần sudo nếu chạy dưới quyền thường).
remove_system() {
    local path="$1"
    if [ -d "$path" ]; then
        echo "==> Xóa $path (cần quyền quản trị)"
        if [ "$(id -u)" -eq 0 ]; then
            rm -rf "$path"
        else
            sudo rm -rf "$path"
        fi
        REMOVED_ANY=true
    fi
}

# 2. Xóa bộ gõ IMK và app Cài đặt ở mọi vị trí + mọi tên biến thể đã từng dùng.
remove_user "$USER_INPUT_METHODS/BambooMintKey.app"
remove_user "$USER_APPLICATIONS/BambooMintKey.app"
remove_user "$USER_APPLICATIONS/BambooMintKey.UI.app"
remove_user "$USER_APPLICATIONS/BambooMintKeySettings.app"

remove_system "$SYSTEM_INPUT_METHODS/BambooMintKey.app"
remove_system "$SYSTEM_APPLICATIONS/BambooMintKey.app"

# 3. Làm mới danh sách nguồn nhập liệu (bỏ mục BambooMintKey đã xóa).
killall TextInputMenuAgent 2>/dev/null || true
killall TextInputSwitcher 2>/dev/null || true

# 4. Tùy chọn xóa cấu hình.
if [ "$PURGE" = true ]; then
    remove_user "$CONFIG_DIR"
else
    echo "==> Giữ lại cấu hình tại $CONFIG_DIR"
    echo "    (chạy lại với --purge để xóa luôn cấu hình)"
fi

echo "=================================================="
if [ "$REMOVED_ANY" = true ]; then
    echo "  ✅ Đã gỡ cài đặt BambooMintKey."
    echo "  Nếu mục BambooMintKey còn sót trong Cài đặt hệ thống,"
    echo "  vào Bàn phím -> Nguồn nhập liệu -> bấm [-] để xóa."
else
    echo "  Không tìm thấy bản cài đặt nào để gỡ."
fi
echo "=================================================="
