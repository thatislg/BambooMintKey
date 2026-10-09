#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Kịch bản cài đặt tự động một chạm (One-command installer) cho macOS.
# Hỗ trợ cả 2 chế độ:
#   1. Cài đặt từ gói phân phối Release (đã có sẵn bundle trong thư mục).
#   2. Cài đặt từ mã nguồn (tự build toàn bộ .dylib, IMK, StatusBar, UI.Mac).
set -euo pipefail

ARCH="$(uname -m)"
case "$ARCH" in
    arm64) RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) echo "Kiến trúc CPU không hỗ trợ: $ARCH" >&2; exit 1 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." 2>/dev/null && pwd || echo "")"

INPUT_METHODS_DIR="$HOME/Library/Input Methods"
APPLICATIONS_DIR="$HOME/Applications"

echo "=================================================="
echo "  Cài đặt BambooMintKey cho macOS ($ARCH)"
echo "=================================================="

# Kiểm tra nếu đang chạy từ mã nguồn repo thì tiến hành build trước
if [ -n "$ROOT" ] && [ -f "$ROOT/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj" ]; then
    echo "==> Phát hiện môi trường mã nguồn. Đang biên dịch các thành phần..."

    # 1. Kiểm tra .NET SDK
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "Lỗi: Không tìm thấy 'dotnet'. Vui lòng cài đặt .NET 10 SDK trước." >&2
        exit 1
    fi

    # 2. Kiểm tra Swift compiler
    if ! command -v swiftc >/dev/null 2>&1; then
        echo "Lỗi: Không tìm thấy 'swiftc'. Vui lòng cài đặt Xcode Command Line Tools: xcode-select --install" >&2
        exit 1
    fi

    # 3. Biên dịch NativeAOT C-ABI .dylib
    echo "--- 1/4: Biên dịch thư viện lõi C-ABI ($RID) ---"
    dotnet publish "$ROOT/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj" \
        -c Release -r "$RID" -o "$ROOT/publish/$RID"

    # 4. Biên dịch StatusBar app
    echo "--- 2/4: Biên dịch BambooMintKeyStatusBar.app ---"
    bash "$ROOT/scripts/macos/build_statusbar.sh" "$ARCH"

    # 5. Biên dịch IMK Engine Service
    echo "--- 3/4: Biên dịch BambooMintKey.app (IMK) ---"
    bash "$ROOT/scripts/macos/build_imk.sh" "$ARCH"

    # 6. Biên dịch UI Cài đặt
    echo "--- 4/4: Biên dịch BambooMintKey.UI.Mac.app ---"
    bash "$ROOT/scripts/macos/build_ui_mac.sh" "$ARCH"

    IMK_SRC="$ROOT/build/imk-$ARCH/BambooMintKey.app"
    UI_SRC="$ROOT/build/ui-mac-$ARCH/BambooMintKey.app"
else
    # Chế độ cài đặt từ gói release giải nén
    IMK_SRC="$SCRIPT_DIR/BambooMintKey.app"
    UI_SRC="$SCRIPT_DIR/BambooMintKey.UI.app"
    if [ ! -d "$IMK_SRC" ]; then
        IMK_SRC="$SCRIPT_DIR/../BambooMintKey.app"
        UI_SRC="$SCRIPT_DIR/../BambooMintKey.UI.app"
    fi
fi

if [ ! -d "$IMK_SRC" ]; then
    echo "Lỗi: Không tìm thấy gói $IMK_SRC để cài đặt." >&2
    exit 1
fi

echo "==> Đang cài đặt vào hệ thống..."

# 1. Cài đặt bộ gõ vào ~/Library/Input Methods/
mkdir -p "$INPUT_METHODS_DIR"
# Dừng tiến trình cũ an toàn trước khi thay thế
pkill -f "$INPUT_METHODS_DIR/BambooMintKey.app/Contents/MacOS/BambooMintKey" || true
pkill -f "BambooMintKeyStatusBar" || true
rm -rf "$INPUT_METHODS_DIR/BambooMintKey.app"
cp -R "$IMK_SRC" "$INPUT_METHODS_DIR/"
codesign --force --deep --sign - "$INPUT_METHODS_DIR/BambooMintKey.app"

# 2. Cài đặt UI vào ~/Applications/ (nếu có)
if [ -d "$UI_SRC" ]; then
    mkdir -p "$APPLICATIONS_DIR"
    rm -rf "$APPLICATIONS_DIR/BambooMintKey.app"
    cp -R "$UI_SRC" "$APPLICATIONS_DIR/"
    codesign --force --deep --sign - "$APPLICATIONS_DIR/BambooMintKey.app"
fi

# 3. Khởi động lại dịch vụ bộ gõ
open -g "$INPUT_METHODS_DIR/BambooMintKey.app" || true

echo "=================================================="
echo "  ✅ Cài đặt hoàn tất!"
echo "  - Bộ gõ: $INPUT_METHODS_DIR/BambooMintKey.app"
if [ -d "$APPLICATIONS_DIR/BambooMintKey.app" ]; then
echo "  - Cài đặt: $APPLICATIONS_DIR/BambooMintKey.app"
fi
echo ""
echo "  Các bước tiếp theo (nếu là lần đầu cài đặt):"
echo "  1. Mở Cài đặt hệ thống (System Settings) -> Bàn phím (Keyboard)"
echo "  2. Tại 'Nguồn nhập liệu' (Input Sources) bấm 'Sửa...' (Edit...)"
echo "  3. Bấm dấu [+] -> chọn 'Tiếng Việt' -> chọn 'BambooMintKey' -> Thêm."
echo "=================================================="
