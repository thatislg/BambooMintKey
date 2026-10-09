#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Build & đóng gói BambooMintKey.UI.Mac (Avalonia F#) thành .app và cài vào
# ~/Applications/BambooMintKey.app — nơi mà nút "Cài đặt…" trong menu IMK và
# StatusBar trỏ tới.
#
# Cách dùng:
#   scripts/macos/build_ui_mac.sh [arch]    # arch: arm64 (mặc định) | x86_64
set -euo pipefail

ARCH="${1:-arm64}"
case "$ARCH" in
    arm64)  RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) echo "Kiến trúc không hợp lệ: $ARCH (dùng arm64 hoặc x86_64)" >&2; exit 1 ;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC_DIR="$ROOT/src/BambooMintKey.UI.Mac"
PROJECT="$SRC_DIR/BambooMintKey.UI.Mac.fsproj"
PUBLISH_DIR="$ROOT/build/ui-mac-$ARCH"
APP_DIR="$PUBLISH_DIR/BambooMintKey.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"
BIN="BambooMintKey.UI.Mac"

echo "==> Publish BambooMintKey.UI.Mac (framework-dependent) cho $ARCH ..."
rm -rf "$PUBLISH_DIR"
mkdir -p "$MACOS_DIR"

dotnet publish "$PROJECT" -c Release -r "$RID" -o "$PUBLISH_DIR"

# Di chuyển toàn bộ output publish vào Contents/MacOS (trừ chính thư mục .app).
for item in "$PUBLISH_DIR"/*; do
    base="$(basename "$item")"
    if [ "$base" = "BambooMintKey.app" ]; then continue; fi
    mv "$item" "$MACOS_DIR/"
done

cp "$SRC_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"

# Ký ad-hoc (cần thiết trên Apple Silicon).
codesign --force --deep --sign - "$APP_DIR"

# Cài vào ~/Applications để nút "Cài đặt…" tìm thấy.
mkdir -p "$HOME/Applications"
rm -rf "$HOME/Applications/BambooMintKey.app"
cp -R "$APP_DIR" "$HOME/Applications/BambooMintKey.app"

echo "==> Hoàn tất: $HOME/Applications/BambooMintKey.app"
echo "    File thực thi: $HOME/Applications/BambooMintKey.app/Contents/MacOS/$BIN"
