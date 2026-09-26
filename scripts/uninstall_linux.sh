#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Gỡ cài đặt BambooMintKey sạch sẽ khỏi hệ thống.
# Dọn bản cài hệ thống (/usr) là chính, kèm dọn bản cài user (~/.local) cũ nếu có.

set -euo pipefail

echo "Đang gỡ cài đặt BambooMintKey..."

# ---------------------------------------------------------------------------
# Bản cài hệ thống (/usr) — cần sudo
# ---------------------------------------------------------------------------
SYSTEM_ANY="/usr/share/fcitx5/addon/bamboomintkey.conf"
if [ -f "$SYSTEM_ANY" ] || ls /usr/lib/*/fcitx5/libbamboomintkey.so >/dev/null 2>&1; then
    echo "Phát hiện bản cài hệ thống /usr — cần sudo để dọn."
    sudo rm -f \
        /usr/lib/*/fcitx5/libbamboomintkey.so \
        /usr/lib/*/fcitx5/BambooMintKeyCore.so \
        /usr/share/fcitx5/addon/bamboomintkey.conf \
        /usr/share/fcitx5/inputmethod/bamboomintkey.conf \
        /usr/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey.svg \
        /usr/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey_e.svg \
        /usr/share/icons/hicolor/scalable/apps/bamboomintkey.svg \
        /usr/share/applications/bamboomintkey-settings.desktop \
        /usr/local/bin/bamboomintkey-ui
    sudo gtk-update-icon-cache /usr/share/icons/hicolor 2>/dev/null || true
    sudo update-desktop-database /usr/share/applications 2>/dev/null || true
fi

# ---------------------------------------------------------------------------
# Bản cài user (~/.local) — dọn fallback (nếu từng cài theo thiết kế cũ)
# ---------------------------------------------------------------------------
PREFIX="$HOME/.local"
rm -f \
    "$PREFIX/lib/fcitx5/libbamboomintkey.so" \
    "$PREFIX/lib/fcitx5/BambooMintKeyCore.so" \
    "$PREFIX/share/fcitx5/addon/bamboomintkey.conf" \
    "$PREFIX/share/fcitx5/inputmethod/bamboomintkey.conf" \
    "$PREFIX/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey.svg" \
    "$PREFIX/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey_e.svg" \
    "$PREFIX/share/icons/hicolor/scalable/apps/bamboomintkey.svg" \
    "$PREFIX/bin/bamboomintkey-ui" \
    "$PREFIX/share/applications/bamboomintkey-settings.desktop"

# ---------------------------------------------------------------------------
# Restart Fcitx5
# ---------------------------------------------------------------------------
echo "Khởi động lại Fcitx5..."
if command -v fcitx5 >/dev/null 2>&1; then
    fcitx5 -r -d 2>/dev/null || true
    pkill -f fcitx5 2>/dev/null || true
    sleep 1
    fcitx5 -d 2>/dev/null || true
fi

echo "Hoàn tất! BambooMintKey đã được gỡ bỏ sạch sẽ."
