#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Kịch bản gỡ cài đặt sạch sẽ (Clean uninstaller) cho macOS.
set -euo pipefail

INPUT_METHODS_DIR="$HOME/Library/Input Methods"
APPLICATIONS_DIR="$HOME/Applications"
CONFIG_DIR="$HOME/Library/Application Support/BambooMintKey"

echo "=================================================="
echo "  Gỡ cài đặt BambooMintKey khỏi macOS"
echo "=================================================="

# 1. Dừng các tiến trình đang chạy
echo "==> Đang dừng các tiến trình bộ gõ..."
pkill -f "BambooMintKey.app/Contents/MacOS/BambooMintKey" || true
pkill -f "BambooMintKeyStatusBar" || true
pkill -f "BambooMintKey.UI.Mac" || true

# 2. Xóa bundle khỏi ~/Library/Input Methods/
if [ -d "$INPUT_METHODS_DIR/BambooMintKey.app" ]; then
    echo "==> Xóa $INPUT_METHODS_DIR/BambooMintKey.app"
    rm -rf "$INPUT_METHODS_DIR/BambooMintKey.app"
fi

# 3. Xóa UI khỏi ~/Applications/
if [ -d "$APPLICATIONS_DIR/BambooMintKey.app" ]; then
    echo "==> Xóa $APPLICATIONS_DIR/BambooMintKey.app"
    rm -rf "$APPLICATIONS_DIR/BambooMintKey.app"
fi

# 4. Tùy chọn xóa tệp cấu hình
if [ -d "$CONFIG_DIR" ]; then
    read -r -p "Bạn có muốn xóa toàn bộ tệp cấu hình tại $CONFIG_DIR không? [y/N]: " choice || choice="n"
    case "$choice" in
        [yY][eE][sS]|[yY])
            echo "==> Xóa $CONFIG_DIR"
            rm -rf "$CONFIG_DIR"
            ;;
        *)
            echo "==> Giữ nguyên tệp cấu hình tại $CONFIG_DIR"
            ;;
    esac
fi

echo "=================================================="
echo "  ✅ Đã gỡ cài đặt BambooMintKey sạch sẽ!"
echo "  Lưu ý: Nếu vẫn còn biểu tượng trong thanh menu,"
echo "  hãy vào Cài đặt hệ thống -> Bàn phím -> Nguồn nhập liệu"
echo "  và bấm dấu [-] để xóa BambooMintKey khỏi danh sách."
echo "=================================================="
