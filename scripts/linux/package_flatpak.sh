#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Đóng gói và kiểm thử Flatpak Extension cho Fcitx 5 (Phase 9 / Steam Deck).
#
# Yêu cầu: flatpak, flatpak-builder
#
# Cách dùng:
#   ./scripts/package_flatpak.sh            # build vào delivery/flatpak/
#   ./scripts/package_flatpak.sh --install  # build + cài đặt vào user session
#
# Output: delivery/flatpak/

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

MANIFEST="$PROJECT_ROOT/manifests/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.yaml"
OUT_DIR="$PROJECT_ROOT/delivery/flatpak"
BUILD_DIR="$OUT_DIR/stage"
REPO_DIR="$OUT_DIR/repo"

DO_INSTALL=false
if [ "${1:-}" = "--install" ]; then
    DO_INSTALL=true
fi

if ! command -v flatpak >/dev/null 2>&1; then
    echo "Lỗi: Chưa cài đặt 'flatpak'."
    exit 1
fi

if ! command -v flatpak-builder >/dev/null 2>&1; then
    echo "Lỗi: Chưa cài đặt 'flatpak-builder'."
    echo "  Ubuntu/Debian: sudo apt install flatpak-builder"
    echo "  Fedora:        sudo dnf install flatpak-builder"
    exit 1
fi

echo "=== [1/3] Kiểm tra SDK và Runtime Flatpak ==="
flatpak install --user -y flathub \
    org.fcitx.Fcitx5//stable \
    org.kde.Sdk//6.11 \
    org.freedesktop.Sdk.Extension.dotnet10//24.08 2>/dev/null || true

echo "=== [2/3] Tiến hành build Flatpak Extension ==="
mkdir -p "$BUILD_DIR" "$REPO_DIR"

flatpak-builder --force-clean \
    --user \
    --repo="$REPO_DIR" \
    "$BUILD_DIR" \
    "$MANIFEST"

echo "=== [3/3] Build hoàn tất thành công! ==="
echo "Artifact repo: $REPO_DIR"

if [ "$DO_INSTALL" = true ]; then
    echo "Đang cài đặt extension vào user session..."
    flatpak-builder --user --install --force-clean "$BUILD_DIR" "$MANIFEST"
    echo "Cài đặt thành công! Khởi động lại Fcitx5 để nạp addon:"
    echo "  flatpak kill org.fcitx.Fcitx5 2>/dev/null || true"
    echo "  flatpak run org.fcitx.Fcitx5 -d &"
else
    echo "Để cài đặt trực tiếp vào Fcitx5 Flatpak trên máy, chạy:"
    echo "  $0 --install"
fi
