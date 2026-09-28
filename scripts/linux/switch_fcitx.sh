#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Tiện ích chuyển đổi qua lại giữa Fcitx 5 Hệ Thống (Native DEB/APT)
# và Fcitx 5 Sandbox (Flatpak / Steam Deck).
#
# Cách dùng:
#   ./scripts/linux/switch_fcitx.sh status    # Xem bộ gõ nào đang chạy
#   ./scripts/linux/switch_fcitx.sh flatpak   # Chuyển sang Fcitx5 Flatpak (Steam Deck mode)
#   ./scripts/linux/switch_fcitx.sh native    # Chuyển về Fcitx5 Hệ Thống (Linux Mint mode)
#   ./scripts/linux/switch_fcitx.sh config    # Mở bảng cài đặt phù hợp với bộ gõ đang chạy

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

ensure_flatpak_icons() {
    local target_dir="$HOME/.local/share/icons/hicolor/scalable/apps"
    mkdir -p "$target_dir"
    local ext_icons="$HOME/.local/share/flatpak/runtime/org.fcitx.Fcitx5.Addon.BambooMintKey/x86_64/stable/active/files/share/icons/hicolor/scalable/apps"
    if [ -d "$ext_icons" ]; then
        cp -f "$ext_icons/fcitx_bamboomintkey.svg" "$target_dir/org.fcitx.Fcitx5.fcitx_bamboomintkey.svg" 2>/dev/null || true
        cp -f "$ext_icons/fcitx_bamboomintkey_e.svg" "$target_dir/org.fcitx.Fcitx5.fcitx_bamboomintkey_e.svg" 2>/dev/null || true
        cp -f "$ext_icons/fcitx_bamboomintkey.svg" "$target_dir/fcitx_bamboomintkey.svg" 2>/dev/null || true
        cp -f "$ext_icons/fcitx_bamboomintkey_e.svg" "$target_dir/fcitx_bamboomintkey_e.svg" 2>/dev/null || true
    elif [ -d "$PROJECT_ROOT/src/media" ]; then
        cp -f "$PROJECT_ROOT/src/media/bamboo_mint_key_ico.svg" "$target_dir/org.fcitx.Fcitx5.fcitx_bamboomintkey.svg" 2>/dev/null || true
        cp -f "$PROJECT_ROOT/src/media/bamboo_mint_key_ico_e.svg" "$target_dir/org.fcitx.Fcitx5.fcitx_bamboomintkey_e.svg" 2>/dev/null || true
        cp -f "$PROJECT_ROOT/src/media/bamboo_mint_key_ico.svg" "$target_dir/fcitx_bamboomintkey.svg" 2>/dev/null || true
        cp -f "$PROJECT_ROOT/src/media/bamboo_mint_key_ico_e.svg" "$target_dir/fcitx_bamboomintkey_e.svg" 2>/dev/null || true
    fi
    gtk-update-icon-cache -f "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
}

get_current_mode() {
    if pgrep -f "bwrap.*fcitx5" >/dev/null 2>&1 || pgrep -f "flatpak.*org.fcitx.Fcitx5" >/dev/null 2>&1; then
        echo "flatpak"
    elif pgrep -f "/usr/bin/fcitx5" >/dev/null 2>&1; then
        echo "native"
    elif pgrep -x "fcitx5" >/dev/null 2>&1; then
        echo "running"
    else
        echo "stopped"
    fi
}

stop_all_fcitx() {
    echo "Đang dừng toàn bộ tiến trình Fcitx 5 (cả Native và Flatpak)..."
    flatpak kill org.fcitx.Fcitx5 2>/dev/null || true
    killall fcitx5 fcitx5-bin 2>/dev/null || true
    pkill -f "bwrap.*fcitx5" 2>/dev/null || true
    sleep 1
}

# Chờ một tiến trình khớp mẫu xuất hiện trong vòng N giây (cơ chế đếm thời gian).
# $1 = timeout (giây), $2 = mẫu pgrep (regex khớp full command line).
wait_for() {
    local timeout="$1" pattern="$2" i
    for i in $(seq 1 "$timeout"); do
        if pgrep -f "$pattern" >/dev/null 2>&1; then
            return 0
        fi
        sleep 1
    done
    return 1
}

ACTION="${1:-status}"

case "$ACTION" in
    status)
        MODE="$(get_current_mode)"
        echo "=========================================================="
        echo "           TRẠNG THÁI BỘ GÕ FCITX 5 HIỆN TẠI"
        echo "=========================================================="
        if [ "$MODE" = "flatpak" ]; then
            echo "🟢 Chế độ đang chạy: [Fcitx 5 Flatpak] (Môi trường Steam Deck / Sandbox)"
            PID=$(pgrep -f "bwrap.*fcitx5" | head -n 1 || echo "")
            echo "   PID Sandbox: $PID"
            IM=$(fcitx5-remote -n 2>/dev/null || echo "Chưa nạp")
            echo "   Input Method đang chọn: $IM"
            ADDON=$(flatpak run --command=fcitx5-remote org.fcitx.Fcitx5 -m bamboomintkey 2>/dev/null || echo "Chưa nhận")
            echo "   Trạng thái Addon BambooMintKey: $ADDON"
        elif [ "$MODE" = "native" ]; then
            echo "🔵 Chế độ đang chạy: [Fcitx 5 Hệ Thống (Native)] (Linux Mint APT/DEB)"
            PID=$(pgrep -x "fcitx5" | head -n 1 || echo "")
            echo "   PID Hệ thống: $PID"
            IM=$(fcitx5-remote -n 2>/dev/null || echo "Chưa nạp")
            echo "   Input Method đang chọn: $IM"
        else
            echo "⚪ Fcitx 5 hiện KHÔNG chạy."
        fi
        echo "=========================================================="
        echo "Lệnh chuyển đổi:"
        echo "  $0 flatpak   -> Kích hoạt Fcitx 5 Flatpak"
        echo "  $0 native    -> Kích hoạt Fcitx 5 Hệ Thống"
        echo "  $0 config    -> Mở cấu hình bộ gõ tương ứng"
        ;;

    flatpak)
        stop_all_fcitx

        echo "Đảm bảo icon Flatpak Fcitx5 có mặt trên khay hệ thống..."
        ensure_flatpak_icons

        echo "Khởi chạy Fcitx 5 Flatpak..."
        flatpak run org.fcitx.Fcitx5 -d </dev/null >/dev/null 2>&1 &

        if wait_for 15 'bwrap.*fcitx5'; then
            echo "✅ Đã chuyển thành công sang Fcitx 5 Flatpak!"
            $0 status
        else
            echo "❌ KHÔNG khởi động được Fcitx 5 Flatpak trong 15 giây!" >&2
            echo "   Gỡ lỗi: chạy 'flatpak run org.fcitx.Fcitx5' (foreground) để xem lỗi chi tiết." >&2
            exit 1
        fi
        ;;

    native)
        stop_all_fcitx

        echo "Khởi chạy Fcitx 5 Hệ Thống (/usr/bin/fcitx5)..."
        /usr/bin/fcitx5 -d </dev/null >/dev/null 2>&1 &

        if wait_for 10 'fcitx5-bin'; then
            echo "✅ Đã chuyển thành công về Fcitx 5 Hệ Thống!"
            $0 status
        else
            echo "❌ KHÔNG khởi động được Fcitx 5 Hệ Thống trong 10 giây!" >&2
            echo "   Gỡ lỗi: chạy '/usr/bin/fcitx5' (foreground) để xem lỗi chi tiết." >&2
            exit 1
        fi
        ;;

    config)
        MODE="$(get_current_mode)"
        if [ "$MODE" = "flatpak" ]; then
            echo "Đang mở bảng cấu hình Fcitx 5 Flatpak..."
            flatpak run --command=fcitx5-configtool org.fcitx.Fcitx5 &
        else
            echo "Đang mở bảng cấu hình Fcitx 5 Hệ Thống..."
            fcitx5-configtool &
        fi
        ;;

    *)
        echo "Cách dùng: $0 {status|flatpak|native|config}"
        exit 1
        ;;
esac
