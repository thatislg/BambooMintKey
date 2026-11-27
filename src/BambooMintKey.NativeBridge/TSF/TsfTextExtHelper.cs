// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
//
// File giữ lại làm shim tương thích tối giản (013_03):
// Toàn bộ lời gọi COM ITfContext::GetActiveView / ITfContextView::GetTextExt đã được gỡ bỏ
// để tránh tuyệt đối crash MSCTF.dll / WinUIEdit.dll trong tiến trình app chủ.
// Tọa độ popup hiện được lấy thuần Win32 trong CandidatePopupWindow.TryGetWin32AnchorRect.
namespace BambooMintKey.NativeBridge.TSF;

public static class TsfTextExtHelper
{
    public static bool TryGetAnchorRect(out Win32Rect anchorRect)
    {
        return CandidatePopupWindow.TryGetWin32AnchorRect(out anchorRect, out _);
    }
}
