#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Build Menu Bar app (M5): biên dịch Swift -> BambooMintKeyStatusBar (Mach-O)
# thành .app nền (LSUIElement) hiển thị icon chữ "B" trên thanh menu macOS.
#
# Cách dùng:
#   scripts/macos/build_statusbar.sh [arch]    # arch: arm64 (mặc định) | x86_64
set -euo pipefail

ARCH="${1:-arm64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC_DIR="$ROOT/src/BambooMintKey.Mac.StatusBar"
OUT_DIR="$ROOT/build/statusbar-$ARCH"
APP_DIR="$OUT_DIR/BambooMintKeyStatusBar.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"

echo "==> Biên dịch BambooMintKey.Mac.StatusBar (Swift) cho $ARCH ..."
rm -rf "$OUT_DIR"
mkdir -p "$MACOS_DIR"

swiftc \
    -O \
    -framework AppKit \
    -framework Cocoa \
    "$SRC_DIR/main.swift" \
    "$SRC_DIR/AppDelegate.swift" \
    "$SRC_DIR/ConfigStore.swift" \
    "$SRC_DIR/StatusBarController.swift" \
    -o "$MACOS_DIR/BambooMintKeyStatusBar"

cp "$SRC_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"

# Ký ad-hoc (cần thiết trên Apple Silicon).
codesign --force --deep --sign - "$APP_DIR"

echo "==> Hoàn tất: $APP_DIR"
echo "    File thực thi: $MACOS_DIR/BambooMintKeyStatusBar"
