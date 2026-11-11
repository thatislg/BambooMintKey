#!/usr/bin/env bash
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Kịch bản xóa sạch triệt để mọi bản BambooMintKey trên macOS:
# 1. Dừng các tiến trình đang chạy.
# 2. Hủy đăng ký tất cả các bản tích lũy trong LaunchServices (kể cả build, trash, volumes).
# 3. Xóa các bản cài đặt trong /Library/Input Methods, ~/Library/Input Methods, /Applications, ~/Applications.
# 4. Xóa rác trong ~/.Trash.
# 5. Dọn dẹp com.apple.HIToolbox.plist.
# 6. Khởi động lại các daemon quản lý bàn phím của macOS (TextInputMenuAgent, TextInputSwitcher).

set -euo pipefail

echo "=================================================="
echo "  🧹 Dọn dẹp triệt để mọi bản BambooMintKey trên macOS"
echo "=================================================="

# 1. Dừng mọi tiến trình liên quan
echo "==> 1/6: Dừng các tiến trình bộ gõ..."
pkill -9 -f "BambooMintKey.app/Contents/MacOS/BambooMintKey" 2>/dev/null || true
pkill -9 -f "BambooMintKeyStatusBar" 2>/dev/null || true
pkill -9 -f "BambooMintKey.UI.Mac" 2>/dev/null || true

# 2. Tìm lsregister
LSREGISTER="/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/LaunchServices.framework/Versions/A/Support/lsregister"
if [ ! -f "$LSREGISTER" ]; then
    LSREGISTER="/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"
fi

# 3. Hủy đăng ký tất cả các bản BambooMintKey đang lưu trong LaunchServices database
if [ -f "$LSREGISTER" ]; then
    echo "==> 2/6: Hủy đăng ký LaunchServices cho các bản BambooMintKey..."
    "$LSREGISTER" -dump 2>/dev/null | grep "path:" | grep -i "BambooMintKey" | sed -E 's/^[[:space:]]*path:[[:space:]]*//' | sed -E 's/ \(0x[0-9a-f]+\)$//' | while read -r p; do
        if [ -n "$p" ]; then
            echo "    - Unregister: $p"
            "$LSREGISTER" -u "$p" 2>/dev/null || true
        fi
    done
fi

# 4. Xóa file cài đặt trên hệ thống và người dùng
echo "==> 3/6: Xóa các file cài đặt..."
rm -rf "$HOME/Library/Input Methods/BambooMintKey.app" 2>/dev/null || true
rm -rf "$HOME/Applications/BambooMintKey.app" 2>/dev/null || true
rm -rf "$HOME/Applications/BambooMintKey.UI.app" 2>/dev/null || true
rm -rf "$HOME/Applications/BambooMintKeySettings.app" 2>/dev/null || true
rm -rf "$HOME/.Trash/"*BambooMintKey* 2>/dev/null || true

if [ "$(id -u)" -eq 0 ]; then
    rm -rf "/Library/Input Methods/BambooMintKey.app" 2>/dev/null || true
    rm -rf "/Applications/BambooMintKey.app" 2>/dev/null || true
    rm -rf "/Applications/Gỡ Cài Đặt BambooMintKey.command" 2>/dev/null || true
else
    sudo rm -rf "/Library/Input Methods/BambooMintKey.app" 2>/dev/null || true
    sudo rm -rf "/Applications/BambooMintKey.app" 2>/dev/null || true
    sudo rm -rf "/Applications/Gỡ Cài Đặt BambooMintKey.command" 2>/dev/null || true
fi

# 5. Dọn dẹp cache danh sách Input Sources trong HIToolbox.plist
echo "==> 4/6: Dọn dẹp cấu hình nguồn nhập liệu macOS (com.apple.HIToolbox.plist)..."
python3 -c "
import plistlib, os
p = os.path.expanduser('~/Library/Preferences/com.apple.HIToolbox.plist')
if os.path.exists(p):
    try:
        with open(p, 'rb') as f:
            pl = plistlib.load(f)
        changed = False
        for k in ['AppleEnabledInputSources', 'AppleSelectedInputSources']:
            if k in pl and isinstance(pl[k], list):
                orig_len = len(pl[k])
                pl[k] = [item for item in pl[k] if not (isinstance(item, dict) and any('Bamboo' in str(v) or 'bamboo' in str(v) for v in item.values()))]
                if len(pl[k]) != orig_len:
                    changed = True
        if changed:
            with open(p, 'wb') as f:
                plistlib.dump(pl, f)
            print('    - Đã làm sạch com.apple.HIToolbox.plist.')
    except Exception as e:
        print('    - Không thể cập nhật HIToolbox.plist:', e)
" 2>/dev/null || true

# 6. Rebuild LaunchServices database
if [ -f "$LSREGISTER" ]; then
    echo "==> 5/6: Tái tạo cơ sở dữ liệu LaunchServices..."
    "$LSREGISTER" -kill -r -domain local -domain user 2>/dev/null || true
fi

# 7. Khởi động lại các daemon quản lý bàn phím
echo "==> 6/6: Tải lại TextInputMenuAgent & TextInputSwitcher..."
killall TextInputMenuAgent 2>/dev/null || true
killall TextInputSwitcher 2>/dev/null || true

echo "=================================================="
echo "  ✅ ĐÃ XÓA SẠCH HOÀN TOÀN TẤT CẢ CÁC BẢN CŨ!"
echo "  Bây giờ bạn có thể cài đặt bản mới (qua file .pkg hoặc .dmg)."
echo "=================================================="
