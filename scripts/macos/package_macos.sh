#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Kịch bản đóng gói phát hành (Distribution packager) cho macOS.
# Sinh gói zip/tar.gz chuẩn bị cho GitHub Release hoặc phân phối người dùng.
#
# Cách dùng:
#   scripts/macos/package_macos.sh [arch]     # arm64 (mặc định) | x86_64
set -euo pipefail

ARCH="${1:-$(uname -m)}"
case "$ARCH" in
    arm64) RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) echo "Kiến trúc CPU không hỗ trợ: $ARCH (dùng arm64 hoặc x86_64)" >&2; exit 1 ;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT_DELIVERY="$ROOT/delivery/macos"
STAGE_DIR="$ROOT/build/package-macos-$ARCH"
PACKAGE_NAME="BambooMintKey-macos-$ARCH"

echo "=================================================="
echo "  Đóng gói BambooMintKey Release cho macOS ($ARCH)"
echo "=================================================="

# 1. Biên dịch NativeAOT C-ABI .dylib
echo "==> 1/4: Biên dịch NativeAOT C-ABI ($RID) ..."
dotnet publish "$ROOT/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj" \
    -c Release -r "$RID" -o "$ROOT/publish/$RID"

# 2. Biên dịch StatusBar app
echo "==> 2/4: Biên dịch BambooMintKeyStatusBar.app ..."
bash "$ROOT/scripts/macos/build_statusbar.sh" "$ARCH"

# 3. Biên dịch IMK Engine Service (đã nhúng StatusBar)
echo "==> 3/4: Biên dịch BambooMintKey.app (IMK) ..."
bash "$ROOT/scripts/macos/build_imk.sh" "$ARCH"

# 4. Biên dịch UI Cài đặt
echo "==> 4/4: Biên dịch BambooMintKey.UI.Mac.app ..."
bash "$ROOT/scripts/macos/build_ui_mac.sh" "$ARCH"

# 5. Tổ chức thư mục staging
echo "==> Gom các thành phần vào gói phát hành..."
rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR/$PACKAGE_NAME"

# Copy bộ gõ IMK (chính)
cp -R "$ROOT/build/imk-$ARCH/BambooMintKey.app" "$STAGE_DIR/$PACKAGE_NAME/"

# Copy UI Cài đặt (đổi tên để phân biệt rõ trong gói giải nén)
cp -R "$ROOT/build/ui-mac-$ARCH/BambooMintKey.app" "$STAGE_DIR/$PACKAGE_NAME/BambooMintKey.UI.app"

# Copy kịch bản cài đặt và gỡ cài đặt một chạm
cp "$ROOT/scripts/macos/install_macos.sh" "$STAGE_DIR/$PACKAGE_NAME/install.sh"
cp "$ROOT/scripts/macos/uninstall_macos.sh" "$STAGE_DIR/$PACKAGE_NAME/uninstall.sh"
chmod +x "$STAGE_DIR/$PACKAGE_NAME/install.sh" "$STAGE_DIR/$PACKAGE_NAME/uninstall.sh"

# Tạo README hướng dẫn nhanh
cat << 'EOF' > "$STAGE_DIR/$PACKAGE_NAME/README.txt"
BambooMintKey - Bộ gõ tiếng Việt Telex cho macOS
Bản quyền (c) 2026 Dương Gia Long và LMO contributors (MIT License)

HƯỚNG DẪN CÀI ĐẶT NHANH:
1. Mở Terminal tại thư mục này.
2. Chạy lệnh:
   bash install.sh
3. Mở Cài đặt hệ thống (System Settings) -> Bàn phím (Keyboard) -> Nguồn nhập liệu (Input Sources).
4. Bấm dấu [+] -> chọn "Tiếng Việt" -> chọn "BambooMintKey" -> bấm Thêm (Add).

HƯỚNG DẪN GỠ CÀI ĐẶT:
Chạy lệnh:
   bash uninstall.sh
EOF

# 6. Tạo file cài đặt bản địa macOS (.pkg Installer)
mkdir -p "$OUT_DELIVERY"
PKG_FILE="$OUT_DELIVERY/$PACKAGE_NAME.pkg"
echo "==> Đang đóng gói installer .pkg: $PKG_FILE ..."

PKG_ROOT="$STAGE_DIR/pkg_root"
PKG_SCRIPTS="$STAGE_DIR/pkg_scripts"
rm -rf "$PKG_ROOT" "$PKG_SCRIPTS"
mkdir -p "$PKG_ROOT/Library/Input Methods"
mkdir -p "$PKG_ROOT/Applications"
mkdir -p "$PKG_SCRIPTS"

cp -R "$ROOT/build/imk-$ARCH/BambooMintKey.app" "$PKG_ROOT/Library/Input Methods/"
cp -R "$ROOT/build/ui-mac-$ARCH/BambooMintKey.app" "$PKG_ROOT/Applications/"

cat << 'EOF' > "$PKG_SCRIPTS/postinstall"
#!/bin/bash
set -e

# Đóng các tiến trình cũ nếu đang chạy
pkill -f "BambooMintKey.app/Contents/MacOS/BambooMintKey" || true
pkill -f "BambooMintKeyStatusBar" || true
pkill -f "BambooMintKey.UI.Mac" || true

# Xóa bản cài cũ trong ~/Library nếu có để tránh xung đột
CONSOLE_USER=$(stat -f "%Su" /dev/console 2>/dev/null || echo "")
if [ -n "$CONSOLE_USER" ] && [ "$CONSOLE_USER" != "root" ]; then
    USER_HOME=$(eval echo "~$CONSOLE_USER")
    if [ -d "$USER_HOME/Library/Input Methods/BambooMintKey.app" ]; then
        rm -rf "$USER_HOME/Library/Input Methods/BambooMintKey.app"
    fi
    if [ -d "$USER_HOME/Applications/BambooMintKey.app" ]; then
        rm -rf "$USER_HOME/Applications/BambooMintKey.app"
    fi
fi

chmod -R 755 "/Library/Input Methods/BambooMintKey.app"
chmod -R 755 "/Applications/BambooMintKey.app"
/System/Library/Frameworks/CoreServices.framework/Frameworks/CarbonCore.framework/Support/lsregister -f "/Library/Input Methods/BambooMintKey.app" || true

exit 0
EOF
chmod +x "$PKG_SCRIPTS/postinstall"

COMPONENT_PKG="$STAGE_DIR/BambooMintKey-component.pkg"
pkgbuild \
    --root "$PKG_ROOT" \
    --identifier "com.bamboomintkey.installer" \
    --version "1.1.5" \
    --scripts "$PKG_SCRIPTS" \
    --install-location "/" \
    "$COMPONENT_PKG"

productbuild \
    --package "$COMPONENT_PKG" \
    "$PKG_FILE"

# 7. Nén thành .zip và .tar.gz lưu vào delivery/macos/
ZIP_FILE="$OUT_DELIVERY/$PACKAGE_NAME.zip"
TAR_FILE="$OUT_DELIVERY/$PACKAGE_NAME.tar.gz"

echo "==> Đang nén $ZIP_FILE ..."
(cd "$STAGE_DIR" && zip -q -r -y "$ZIP_FILE" "$PACKAGE_NAME")

echo "==> Đang nén $TAR_FILE ..."
(cd "$STAGE_DIR" && tar -czf "$TAR_FILE" "$PACKAGE_NAME")

echo "=================================================="
echo "  ✅ Đóng gói hoàn tất!"
echo "  - PKG (Cài trực tiếp): $PKG_FILE"
echo "  - ZIP:                 $ZIP_FILE"
echo "  - TAR.GZ:              $TAR_FILE"
echo "=================================================="
