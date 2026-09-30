#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Đóng gói BambooMintKey Linux thành .deb + .tar.gz (Phase 8 / M6.3).
#
# Yêu cầu: dotnet, cmake, fcitx5 dev headers (Fcitx5Core/Config/Utils).
# Tùy chọn .deb: cần dpkg-deb (Debian/Ubuntu).
#
# Cách dùng:
#   ./scripts/package_linux.sh            # build + đóng gói .deb + .tar.gz
#   ./scripts/package_linux.sh --no-deb   # chỉ .tar.gz (không cần dpkg-deb)
#
# Output: delivery/linux/ (bamboomintkey_<ver>_<arch>.deb, bamboomintkey_<ver>_linux.tar.gz)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VERSION="1.1.1"
ARCH="$(dpkg-architecture -qDEB_HOST_ARCH 2>/dev/null || uname -m)"
MULTIARCH="$(dpkg-architecture -qDEB_HOST_MULTIARCH 2>/dev/null || echo "lib")"
BUILD_DEB=true
if [ "${1:-}" = "--no-deb" ]; then
    BUILD_DEB=false
fi

OUT_DIR="$PROJECT_ROOT/delivery/linux"
STAGE="$OUT_DIR/stage"
PKG_NAME="bamboomintkey"

# ---------------------------------------------------------------------------
# 1. Build (không cài hệ thống, chỉ tạo artifact)
# ---------------------------------------------------------------------------
echo "[1/4] Build Core.Native + Fcitx5 addon + UI..."
dotnet publish "$PROJECT_ROOT/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj" \
    -c Release -r linux-x64 -o "$PROJECT_ROOT/publish/linux-x64"
cmake -B "$PROJECT_ROOT/build-pkg" -S "$PROJECT_ROOT/src/BambooMintKey.Fcitx5" \
    -DCMAKE_INSTALL_PREFIX=/usr \
    -DBAMBOOMINTKEY_CORE_SO="$PROJECT_ROOT/publish/linux-x64/BambooMintKeyCore.so"
cmake --build "$PROJECT_ROOT/build-pkg"

# UI publish (dùng lại bước trong install-ui-linux.sh)
dotnet publish "$PROJECT_ROOT/src/BambooMintKey.UI.Linux/BambooMintKey.UI.Linux.fsproj" \
    -c Release -r linux-x64 -o "$PROJECT_ROOT/publish/ui-linux"

# ---------------------------------------------------------------------------
# 2. Staging cấu trúc /usr
# ---------------------------------------------------------------------------
echo "[2/4] Tạo staging..."
rm -rf "$STAGE"
mkdir -p "$STAGE/usr/lib/$MULTIARCH/fcitx5" \
         "$STAGE/usr/share/fcitx5/addon" \
         "$STAGE/usr/share/fcitx5/inputmethod" \
         "$STAGE/usr/share/icons/hicolor/scalable/apps" \
         "$STAGE/usr/share/applications" \
         "$STAGE/usr/local/bin"

# Cài các artifact vào staging (cmake --install với DESTDIR).
DESTDIR="$STAGE" cmake --install "$PROJECT_ROOT/build-pkg"

# UI: toàn bộ publish output (framework-dependent) + launcher + desktop entry + icon
mkdir -p "$STAGE/usr/lib/bamboomintkey/ui"
cp -a "$PROJECT_ROOT/publish/ui-linux/." "$STAGE/usr/lib/bamboomintkey/ui/"
ln -sf "/usr/lib/bamboomintkey/ui/BambooMintKey.UI.Linux" "$STAGE/usr/local/bin/bamboomintkey-ui"
cp "$PROJECT_ROOT/src/BambooMintKey.UI.Linux/bamboomintkey-settings.desktop" "$STAGE/usr/share/applications/"
cp "$PROJECT_ROOT/src/media/bamboo_mint_key_ico.svg" "$STAGE/usr/share/icons/hicolor/scalable/apps/bamboomintkey.svg"

# ---------------------------------------------------------------------------
# 3. Đóng gói .tar.gz
# ---------------------------------------------------------------------------
echo "[3/4] Đóng gói .tar.gz..."
TAR_NAME="${PKG_NAME}_${VERSION}_linux.tar.gz"
tar -C "$STAGE" -czf "$OUT_DIR/$TAR_NAME" usr
echo "  -> $OUT_DIR/$TAR_NAME"

# ---------------------------------------------------------------------------
# 4. Đóng gói .deb (tùy chọn)
# ---------------------------------------------------------------------------
if [ "$BUILD_DEB" = true ] && command -v dpkg-deb >/dev/null 2>&1; then
    echo "[4/4] Đóng gói .deb..."
    DEB_DIR="$OUT_DIR/deb"
    rm -rf "$DEB_DIR"
    mkdir -p "$DEB_DIR/DEBIAN"

    cat > "$DEB_DIR/DEBIAN/control" <<EOF
Package: $PKG_NAME
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: Dương Gia Long & LMO contributors <thatislg@users.noreply.github.com>
Homepage: https://github.com/thatislg/BambooMintKey
Depends: fcitx5, libc6, dotnet-runtime-10.0
Description: Vietnamese Telex Input Method for Fcitx5
 BambooMintKey là bộ gõ tiếng Việt (Telex) hiện đại cho Fcitx5 trên Linux,
 tích hợp từ điển âm tiết MIT, thẩm định on-the-fly và tự hoàn tác tiếng Anh.
EOF

    cp -a "$STAGE/usr" "$DEB_DIR/"

    # Thông báo hướng dẫn sau khi cài (chạy khi dpkg -i)
    cat > "$DEB_DIR/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
echo ""
echo "BambooMintKey đã được cài đặt thành công!"
echo ""
echo "=== CÁC BƯỚC HOÀN TẤT (bắt buộc) ==="
echo "  1. Mở Fcitx5 Configuration:  fcitx5-configtool"
echo "  2. Thêm bộ gõ 'BambooMintKey' vào danh sách Input Method."
echo "  3. Khởi động lại Fcitx5 hoặc đăng xuất/đăng nhập lại để kích hoạt."
echo ""
echo "Lưu ý: bảng cài đặt (bamboomintkey-ui) cần .NET 10 runtime."
echo "  Nếu chưa cài:  sudo apt install dotnet-runtime-10.0"
echo ""
exit 0
EOF
    chmod 755 "$DEB_DIR/DEBIAN/postinst"

    DEB_NAME="${PKG_NAME}_${VERSION}_${ARCH}.deb"
    dpkg-deb --build "$DEB_DIR" "$OUT_DIR/$DEB_NAME"
    echo "  -> $OUT_DIR/$DEB_NAME"
else
    echo "[4/4] Bỏ qua .deb (không có dpkg-deb hoặc --no-deb)."
fi

# ---------------------------------------------------------------------------
# 5. Đóng gói .rpm (Fedora — cần rpmbuild)
# ---------------------------------------------------------------------------
if command -v rpmbuild >/dev/null 2>&1; then
    echo "[5/5] Đóng gói .rpm..."
    RPM_ROOT="$OUT_DIR/rpmbuild"
    rm -rf "$RPM_ROOT"
    mkdir -p "$RPM_ROOT"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

    # Tìm thư mục chứa addon .so trong staging (lib/lib64/... tùy distro)
    LIB_DIR=$(dirname "$(find "$STAGE/usr" -name libbamboomintkey.so | head -1)")
    cp "$LIB_DIR/libbamboomintkey.so" "$RPM_ROOT/SOURCES/"
    cp "$LIB_DIR/BambooMintKeyCore.so" "$RPM_ROOT/SOURCES/"
    cp "$STAGE/usr/share/fcitx5/addon/bamboomintkey.conf" "$RPM_ROOT/SOURCES/bamboomintkey-addon.conf"
    cp "$STAGE/usr/share/fcitx5/inputmethod/bamboomintkey.conf" "$RPM_ROOT/SOURCES/bamboomintkey.conf"
    cp "$STAGE/usr/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey.svg" "$RPM_ROOT/SOURCES/"
    cp "$STAGE/usr/share/icons/hicolor/scalable/apps/fcitx_bamboomintkey_e.svg" "$RPM_ROOT/SOURCES/"
    cp "$STAGE/usr/share/icons/hicolor/scalable/apps/bamboomintkey.svg" "$RPM_ROOT/SOURCES/"
    cp -a "$STAGE/usr/lib/bamboomintkey/ui" "$RPM_ROOT/SOURCES/ui"
    cp "$STAGE/usr/share/applications/bamboomintkey-settings.desktop" "$RPM_ROOT/SOURCES/"
    cp "$SCRIPT_DIR/bamboomintkey.spec" "$RPM_ROOT/SPECS/"

    rpmbuild -ba \
        --define "_topdir $RPM_ROOT" \
        --define "_lib lib64" \
        --define "_libdir /usr/lib64" \
        "$RPM_ROOT/SPECS/bamboomintkey.spec"
    echo "  -> $RPM_ROOT/RPMS/"
else
    echo "[5/5] Bỏ qua .rpm (không có rpmbuild — build trên Fedora bằng scripts/bamboomintkey.spec)."
fi

echo ""
echo "Hoàn tất. Xem kết quả tại $OUT_DIR"
