#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Đóng gói và kiểm thử Flatpak Extension cho Fcitx 5 (Phase 9 / Steam Deck).
#
# Yêu cầu: flatpak, flatpak-builder (hoặc org.flatpak.Builder qua flatpak)
#
# Cách dùng:
#   ./scripts/linux/package_flatpak.sh            # build vào delivery/flatpak/
#   ./scripts/linux/package_flatpak.sh --local    # build từ mã nguồn thư mục local
#   ./scripts/linux/package_flatpak.sh --install  # build + cài đặt vào user session
#   ./scripts/linux/package_flatpak.sh --bundle   # build + tạo bundle .flatpak
#
# Output: delivery/flatpak/

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
# Version lấy động: env VERSION (CI truyền từ git tag) hoặc tự đọc git tag.
VERSION="${VERSION:-}"
if [ -z "$VERSION" ]; then
    VERSION="$(git -C "$PROJECT_ROOT" describe --tags --abbrev=0 2>/dev/null || true)"
fi
VERSION="${VERSION#v}"
if [ -z "$VERSION" ]; then
    echo "Lỗi: chưa đặt VERSION (env) và không có git tag. Truyền qua env VERSION." >&2
    exit 1
fi

MANIFEST="$PROJECT_ROOT/manifests/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.yaml"
OUT_DIR="$PROJECT_ROOT/delivery/flatpak"
BUILD_DIR="$OUT_DIR/stage"
REPO_DIR="$OUT_DIR/repo"

DO_INSTALL=false
USE_LOCAL=false
DO_BUNDLE=false

for arg in "$@"; do
    case "$arg" in
        --install)
            DO_INSTALL=true
            ;;
        --local)
            USE_LOCAL=true
            ;;
        --bundle)
            DO_BUNDLE=true
            ;;
        -h|--help)
            echo "Cách dùng: $0 [tùy chọn]"
            echo "  --install   Cài đặt extension vào user session sau khi build"
            echo "  --local     Biên dịch từ thư mục mã nguồn hiện tại thay vì tải git"
            echo "  --bundle    Tạo tệp bundle .flatpak độc lập vào delivery/flatpak/"
            exit 0
            ;;
    esac
done

if ! command -v flatpak >/dev/null 2>&1; then
    echo "Lỗi: Chưa cài đặt 'flatpak'."
    exit 1
fi

flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo

BUILDER_CMD=""
if command -v flatpak-builder >/dev/null 2>&1; then
    BUILDER_CMD="flatpak-builder"
elif flatpak info org.flatpak.Builder >/dev/null 2>&1; then
    BUILDER_CMD="flatpak run --command=flatpak-builder org.flatpak.Builder"
else
    echo "Lỗi: Chưa cài đặt 'flatpak-builder'."
    echo "  Cách 1 (cần sudo):   sudo apt install flatpak-builder  (hoặc: sudo dnf install flatpak-builder)"
    echo "  Cách 2 (không sudo):  flatpak install --user -y flathub org.flatpak.Builder"
    exit 1
fi

echo "=== [1/3] Kiểm tra SDK và Runtime Flatpak ==="
flatpak install --user -y flathub \
    org.fcitx.Fcitx5//stable \
    org.kde.Sdk//6.11 \
    org.freedesktop.Sdk.Extension.dotnet10//25.08 2>/dev/null || true

ACTIVE_MANIFEST="$MANIFEST"
if [ "$USE_LOCAL" = true ]; then
    echo "=== Tạo manifest cục bộ cho mã nguồn hiện tại ==="
    mkdir -p "$OUT_DIR"
    ACTIVE_MANIFEST="$OUT_DIR/manifest_local.yaml"
    # Thay thế khối source git thành thư mục local
    python3 - <<EOF
import yaml

with open("$MANIFEST", "r", encoding="utf-8") as f:
    data = yaml.safe_load(f)

for mod in data.get("modules", []):
    if mod.get("name") == "bamboomintkey":
        mod["sources"] = [
            {
                "type": "dir",
                "path": "$PROJECT_ROOT",
                "skip": [
                    ".git", "delivery", "publish", "build", "build-pkg",
                    ".system_generated", ".flatpak-builder", ".idea", ".vscode",
                    "*.bz2", "*.tar.gz", "*.zip"
                ]
            },
            {
                "type": "file",
                "path": "$PROJECT_ROOT/manifests/flatpak/org.fcitx.Fcitx5.Addon.BambooMintKey.metainfo.xml"
            },
            "$PROJECT_ROOT/manifests/flatpak/nuget-sources.json"
        ]

with open("$ACTIVE_MANIFEST", "w", encoding="utf-8") as f:
    yaml.dump(data, f, sort_keys=False, allow_unicode=True)
EOF
fi

echo "=== [2/3] Tiến hành build Flatpak Extension ==="
mkdir -p "$BUILD_DIR" "$REPO_DIR"

$BUILDER_CMD --force-clean \
    --disable-rofiles-fuse \
    --user \
    --repo="$REPO_DIR" \
    "$BUILD_DIR" \
    "$ACTIVE_MANIFEST"

echo "=== [3/3] Build hoàn tất thành công! ==="
echo "Artifact repo: $REPO_DIR"

if [ "$DO_BUNDLE" = true ]; then
    BUNDLE_FILE="$OUT_DIR/org.fcitx.Fcitx5.Addon.BambooMintKey_${VERSION}.flatpak"
    echo "Đang tạo bundle $BUNDLE_FILE..."
    flatpak build-bundle --runtime "$REPO_DIR" "$BUNDLE_FILE" org.fcitx.Fcitx5.Addon.BambooMintKey stable
    echo "Đã tạo bundle thành công: $BUNDLE_FILE"
fi

if [ "$DO_INSTALL" = true ]; then
    echo "Đang cài đặt extension vào user session..."
    $BUILDER_CMD --disable-rofiles-fuse --user --install --force-clean "$BUILD_DIR" "$ACTIVE_MANIFEST"
    echo "Cài đặt thành công! Khởi động lại Fcitx5 để nạp addon:"
    echo "  flatpak kill org.fcitx.Fcitx5 2>/dev/null || true"
    echo "  flatpak run org.fcitx.Fcitx5 -d &"
else
    echo "Để cài đặt trực tiếp vào Fcitx5 Flatpak trên máy, chạy:"
    echo "  $0 --install"
fi
