#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Build IMK Engine Service (M3): biên dịch Swift -> BambooMintKey (Mach-O)
# và liên kết động tới BambooMintKeyCore.dylib (C-ABI NativeAOT).
#
# Cách dùng:
#   scripts/macos/build_imk.sh [arch]      # arch: arm64 (mặc định) | x86_64
#
# Yêu cầu trước: đã chạy M2 để sinh publish/osx-<rid>/BambooMintKeyCore.dylib.
set -euo pipefail

ARCH="${1:-arm64}"
case "$ARCH" in
    arm64)  RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) echo "Kiến trúc không hợp lệ: $ARCH (dùng arm64 hoặc x86_64)" >&2; exit 1 ;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC_DIR="$ROOT/src/BambooMintKey.Mac.IMK"
DYLIB="$ROOT/publish/$RID/BambooMintKeyCore.dylib"
OUT_DIR="$ROOT/build/imk-$ARCH"
APP_DIR="$OUT_DIR/BambooMintKey.app"
MACOS_DIR="$APP_DIR/Contents/MacOS"

if [ ! -f "$DYLIB" ]; then
    echo "Không tìm thấy $DYLIB" >&2
    echo "Hãy chạy M2 trước: dotnet publish src/BambooMintKey.Core.Native ... -r $RID" >&2
    exit 1
fi

echo "==> Biên dịch BambooMintKey.Mac.IMK (Swift) cho $ARCH ..."
rm -rf "$OUT_DIR"
mkdir -p "$MACOS_DIR"

# Liên kết framework InputMethodKit + AppKit, và thư viện lõi C-ABI.
# Dylib đã có install_name @rpath/BambooMintKeyCore.dylib (do NativeAOT đặt),
# nên chỉ cần thêm -rpath @loader_path để dyld tìm thấy thư viện cạnh file
# thực thi trong bundle (không phụ thuộc đường dẫn tuyệt đối).
swiftc \
    -O \
    -framework InputMethodKit \
    -framework AppKit \
    -Xlinker -rpath -Xlinker @loader_path \
    "$SRC_DIR/main.swift" \
    "$SRC_DIR/BambooMintKeyController.swift" \
    "$SRC_DIR/CABIBridge.swift" \
    "$SRC_DIR/ConfigWatcher.swift" \
    "$SRC_DIR/StatusBarLauncher.swift" \
    "$DYLIB" \
    -o "$MACOS_DIR/BambooMintKey"

# Sao chép dylib vào cạnh file thực thi để @rpath/@loader_path nạp đúng.
cp "$DYLIB" "$MACOS_DIR/BambooMintKeyCore.dylib"

# Sao chép Info.plist.
cp "$SRC_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"

# Tạo thư mục Resources và sao chép icon (16x16 @1x + 32x32 @2x Retina chuẩn macOS Menu Bar)
RESOURCES_DIR="$APP_DIR/Contents/Resources"
mkdir -p "$RESOURCES_DIR"
if [ -f "$ROOT/src/media/rendered_b_64x64.png" ]; then
    TMP_ICON16="/tmp/bmk_icon_16.png"
    TMP_ICON32="/tmp/bmk_icon_32.png"
    sips -s format png -z 16 16 -s dpiWidth 72.0 -s dpiHeight 72.0 "$ROOT/src/media/rendered_b_64x64.png" --out "$TMP_ICON16" >/dev/null 2>&1
    sips -s format png -z 32 32 -s dpiWidth 144.0 -s dpiHeight 144.0 "$ROOT/src/media/rendered_b_64x64.png" --out "$TMP_ICON32" >/dev/null 2>&1
    tiffutil -cathidpicheck "$TMP_ICON16" "$TMP_ICON32" -out "$SRC_DIR/BambooMintKey.tiff" >/dev/null 2>&1
    rm -f "$TMP_ICON16" "$TMP_ICON32"
fi
if [ -f "$SRC_DIR/BambooMintKey.tiff" ]; then
    cp "$SRC_DIR/BambooMintKey.tiff" "$RESOURCES_DIR/BambooMintKey.tiff"
fi

# Sao chép localization strings (InfoPlist.strings để macOS hiển thị tên đẹp thay vì bundle ID)
if [ -d "$SRC_DIR/Resources" ]; then
    cp -R "$SRC_DIR/Resources/"* "$RESOURCES_DIR/"
fi

# Nhúng BambooMintKeyStatusBar.app (nếu đã build) vào Contents/SharedSupport để IMK tự khởi chạy
STATUSBAR_APP="$ROOT/build/statusbar-$ARCH/BambooMintKeyStatusBar.app"
if [ -d "$STATUSBAR_APP" ]; then
    SHARED_SUPPORT_DIR="$APP_DIR/Contents/SharedSupport"
    mkdir -p "$SHARED_SUPPORT_DIR"
    cp -R "$STATUSBAR_APP" "$SHARED_SUPPORT_DIR/"
fi

# Ký ad-hoc bundle (bắt buộc trên Apple Silicon): macOS từ chối nạp Input Method
# chưa ký. --deep ký đệ quy cả dylib bên trong; dùng - để dùng identity ad-hoc.
codesign --force --deep --sign - "$APP_DIR"

echo "==> Hoàn tất: $APP_DIR"
echo "    File thực thi: $MACOS_DIR/BambooMintKey"
echo "    Thư viện lõi:  $MACOS_DIR/BambooMintKeyCore.dylib"
