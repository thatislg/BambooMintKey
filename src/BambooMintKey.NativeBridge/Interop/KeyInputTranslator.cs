// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.NativeBridge.Interop;

/// <summary>
/// Chuyển đổi phím ảo Win32 (VK_*) sang ký tự Unicode và kiểm tra trạng thái modifier.
/// Sử dụng User32 GetKeyboardState + ToUnicode.
/// Theo thiết kế 002_03_KeyEventSink_and_Core_Interop.md.
/// </summary>
public static class KeyInputTranslator
{
    // =========================================================================
    // Virtual-key codes (Win32 User Input API)
    // Đặt tên PascalCase + 'Vk' prefix theo quy ước .NET/F# analyzer.
    // =========================================================================

    /// <summary>VK_BACK (0x08) - Phím Backspace.</summary>
    public const uint VkBack = 0x08;

    /// <summary>VK_RETURN (0x0D) - Phím Enter.</summary>
    public const uint VkReturn = 0x0D;

    /// <summary>VK_SPACE (0x20) - Phím Space.</summary>
    public const uint VkSpace = 0x20;

    /// <summary>VK_CONTROL (0x11) - Phím Ctrl (modifier).</summary>
    private const uint VkControl = 0x11;

    /// <summary>VK_MENU (0x12) - Phím Alt (modifier).</summary>
    private const uint VkMenu = 0x12;

    /// <summary>VK_LWIN (0x5B) - Phím Windows trái (modifier).</summary>
    private const uint VkLeftWin = 0x5B;

    /// <summary>VK_RWIN (0x5C) - Phím Windows phải (modifier).</summary>
    private const uint VkRightWin = 0x5C;

    // =========================================================================
    // user32 P/Invokes
    // =========================================================================

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 4)] System.Text.StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags);

    // =========================================================================
    // Modifier detection
    // =========================================================================

    /// <summary>
    /// Kiểm tra xem Ctrl, Alt hoặc phím Win có đang được đè không.
    /// Nếu có, bộ gõ không can thiệp để tránh nuốt nhầm phím tắt ứng dụng.
    /// </summary>
    public static bool IsModifierModifierPressed()
    {
        bool isCtrl = (GetKeyState((int)VkControl) & 0x8000) != 0;
        bool isAlt = (GetKeyState((int)VkMenu) & 0x8000) != 0;
        bool isWin = ((GetKeyState((int)VkLeftWin) & 0x8000) != 0) || ((GetKeyState((int)VkRightWin) & 0x8000) != 0);

        return isCtrl || isAlt || isWin;
    }

    // =========================================================================
    // Virtual key -> Unicode char conversion
    // =========================================================================

    /// <summary>
    /// Chuyển đổi mã phím ảo Win32 và trạng thái bàn phím hiện tại thành ký tự UTF-16.
    /// Trả về null nếu không thể chuyển đổi.
    /// </summary>
    public static char? ConvertVirtualKeyToChar(UIntPtr wParam, IntPtr lParam)
    {
        uint vkCode = (uint)wParam;
        uint scanCode = ((uint)lParam >> 16) & 0xFF;

        byte[] keyState = new byte[256];
        if (!GetKeyboardState(keyState)) return null;

        var sb = new System.Text.StringBuilder(4);
        int result = ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);

        if (result > 0 && sb.Length > 0)
        {
            return sb[0];
        }

        return null;
    }

    // =========================================================================
    // Word break classification
    // =========================================================================

    /// <summary>
    /// Kiểm tra xem ký tự có phải là ký tự ngắt từ (whitespace, punctuation, symbol) không.
    /// </summary>
    public static bool IsWordBreakChar(char c)
    {
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool IsKeyDown(int vKey)
    {
        return ((GetKeyState(vKey) & 0x8000) != 0) || ((GetAsyncKeyState(vKey) & 0x8000) != 0);
    }

    // =========================================================================
    // Hotkey detection
    // =========================================================================

    /// <summary>VK_SHIFT (0x10) - Phím Shift (modifier).</summary>
    private const uint VkShift = 0x10;

    /// <summary>VK_Q (0x51) - Phím ký tự Q.</summary>
    public const uint VkQ = 0x51;

    /// <summary>VK_Z (0x5A) - Phím ký tự Z.</summary>
    public const uint VkZ = 0x5A;

    /// <summary>
    /// Kiểm tra xem sự kiện bàn phím hiện tại có phải là phím tắt chuyển đổi chế độ V/E hay không.
    /// Theo yêu cầu người dùng: Ctrl + Shift + Q.
    /// <summary>
    /// Kiểm tra xem tổ hợp phím hiện tại có khớp với phím tắt chuyển đổi V/E đã cài đặt trong cấu hình hay không.
    /// Hỗ trợ cả 1 phím, 2 phím, 3 phím (Ctrl+Shift+Z, Ctrl+Alt+Space, ...) và 4 phím.
    /// </summary>
    public static bool IsToggleHotkeyPressed(UIntPtr wParam, IntPtr lParam)
    {
        uint currentVk = (uint)wParam;
        uint targetVk = SharedMemoryManager.HotkeyVKey;
        uint targetMods = SharedMemoryManager.HotkeyModifiers;

        if (targetVk == 0 && targetMods == 0)
        {
            return false; // Phím tắt bị tắt
        }

        // 1. Kiểm tra Virtual Key chính
        bool vkMatches = (currentVk == targetVk) || targetVk == 0x10 /* Shift */ && (currentVk == 0x10 || currentVk == 0x11 || currentVk == 0xA0 || currentVk == 0xA1 || currentVk == 0xA2 || currentVk == 0xA3);

        if (!vkMatches)
        {
            return false;
        }

        // 2. Kiểm tra các phím bổ trợ bắt buộc
        bool needCtrl = (targetMods & TsfModFlags.Control) != 0;
        bool needAlt = (targetMods & TsfModFlags.Alt) != 0;
        bool needShift = (targetMods & TsfModFlags.Shift) != 0;

        bool isCtrlDown = IsKeyDown((int)VkControl) || IsKeyDown(0xA2) || IsKeyDown(0xA3);
        bool isAltDown = IsKeyDown((int)VkMenu) || IsKeyDown(0xA4) || IsKeyDown(0xA5);
        bool isShiftDown = IsKeyDown((int)VkShift) || IsKeyDown(0xA0) || IsKeyDown(0xA1);

        if (needCtrl && !isCtrlDown) return false;
        if (needAlt && !isAltDown) return false;
        if (needShift && !isShiftDown) return false;

        return true;
    }
}
