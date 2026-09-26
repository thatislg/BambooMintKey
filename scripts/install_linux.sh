#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Cài đặt toàn bộ BambooMintKey trên Linux bằng MỘT lệnh.
# Cài vào hệ thống /usr (cần sudo), phù hợp apt/rpm cho Ubuntu/Debian & Fedora.
#
# Thực hiện:
#   1. Kiểm tra công cụ (dotnet, cmake, fcitx5).
#   2. Biên dịch Core.Native (C# NativeAOT) -> BambooMintKeyCore.so.
#   3. Biên dịch + cài Fcitx5 addon (C++/CMake) vào /usr (multiarch tự động).
#   4. Biên dịch + cài giao diện Cài đặt (Avalonia UI).
#   5. Khởi động lại Fcitx5 daemon.
#
# Cách dùng:
#   ./scripts/install_linux.sh
#
# Lưu ý: cần quyền root (sudo) để ghi vào /usr. Trên Ubuntu/Debian addon vào
# /usr/lib/<multiarch>/fcitx5/, trên Fedora vào /usr/lib64/fcitx5/.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PREFIX="/usr"

# ---------------------------------------------------------------------------
# 1. Kiểm tra công cụ build
# ---------------------------------------------------------------------------
for tool in dotnet cmake; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Lỗi: thiếu '$tool'. Yêu cầu .NET 10 SDK + CMake." >&2
        exit 1
    fi
done
if ! command -v fcitx5 >/dev/null 2>&1; then
    echo "Cảnh báo: không thấy 'fcitx5' trong PATH (cần có để chạy bộ gõ)." >&2
fi
if [ "$(id -u)" -ne 0 ] && ! command -v sudo >/dev/null 2>&1; then
    echo "Lỗi: cần quyền root (sudo) để cài vào /usr." >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# 2. Core.Native (C# NativeAOT)
# ---------------------------------------------------------------------------
echo "[1/3] Biên dịch Core.Native (C# NativeAOT)..."
dotnet publish "$PROJECT_ROOT/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj" \
    -c Release -r linux-x64 -o "$PROJECT_ROOT/publish/linux-x64"

# ---------------------------------------------------------------------------
# 3. Fcitx5 addon (C++/CMake) -> /usr
# ---------------------------------------------------------------------------
echo "[2/3] Biên dịch + cài Fcitx5 addon vào $PREFIX ..."
cmake -B "$PROJECT_ROOT/build" -S "$PROJECT_ROOT/src/BambooMintKey.Fcitx5" \
    -DCMAKE_INSTALL_PREFIX="$PREFIX" \
    -DBAMBOOMINTKEY_CORE_SO="$PROJECT_ROOT/publish/linux-x64/BambooMintKeyCore.so"
cmake --build "$PROJECT_ROOT/build"
if [ "$(id -u)" -eq 0 ]; then
    cmake --install "$PROJECT_ROOT/build"
else
    sudo cmake --install "$PROJECT_ROOT/build"
fi

# ---------------------------------------------------------------------------
# 4. UI (Avalonia) -> /usr/local/bin + /usr/share (tái dùng install-ui-linux.sh)
# ---------------------------------------------------------------------------
echo "[3/3] Biên dịch + cài giao diện Cài đặt (Avalonia UI)..."
"$SCRIPT_DIR/install-ui-linux.sh"

# ---------------------------------------------------------------------------
# 5. Restart Fcitx5
# ---------------------------------------------------------------------------
echo "Khởi động lại Fcitx5..."
if command -v fcitx5 >/dev/null 2>&1; then
    fcitx5 -r -d 2>/dev/null || true
    pkill -f fcitx5 2>/dev/null || true
    sleep 1
    fcitx5 -d 2>/dev/null || true
fi

echo ""
echo "Cài đặt thành công! BambooMintKey đã sẵn sàng (cài vào $PREFIX)."
echo "  - Thêm bộ gõ 'BambooMintKey' trong Fcitx5 Configuration."
echo "  - Mở cài đặt: 'bamboomintkey-ui' hoặc menu 'BambooMintKey Settings'."
