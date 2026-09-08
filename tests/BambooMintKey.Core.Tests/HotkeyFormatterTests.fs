// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.HotkeyFormatter
open Xunit

/// <summary>
/// Bộ kiểm thử chống hồi quy (Anti-regression Test Suite) cho việc định dạng nhãn phím tắt
/// trên Giao diện điều khiển (UI) và Menu thanh ngôn ngữ (Language Bar).
/// </summary>
module HotkeyFormatterTests =

    // =========================================================================
    // Nhóm 1: Các Preset phím tắt mặc định & phổ biến
    // =========================================================================

    [<Theory>]
    [<InlineData(0x10u, 0x0202u, "Ctrl + Shift", "Gõ tiếng Việt (Ctrl + Shift)")>] // Preset 0: Ctrl + Shift (có cờ OnKeyUp 0x0200)
    [<InlineData(0x10u, 0x0002u, "Ctrl + Shift", "Gõ tiếng Việt (Ctrl + Shift)")>] // Preset 0: Ctrl + Shift (không cờ OnKeyUp)
    [<InlineData(0x5Au, 0x0001u, "Alt + Z", "Gõ tiếng Việt (Alt + Z)")>]             // Preset 1: Alt + Z
    [<InlineData(0x20u, 0x0002u, "Ctrl + Space", "Gõ tiếng Việt (Ctrl + Space)")>]     // Preset 2: Ctrl + Space
    [<InlineData(0x10u, 0x0201u, "Alt + Shift", "Gõ tiếng Việt (Alt + Shift)")>]   // Alt + Shift (có cờ OnKeyUp)
    [<InlineData(0x10u, 0x0001u, "Alt + Shift", "Gõ tiếng Việt (Alt + Shift)")>]   // Alt + Shift (không cờ OnKeyUp)
    let ``1. Preset hotkeys should format correctly for UI and Language Bar`` 
        (vKey: uint32, modifiers: uint32, expectedDisplay: string, expectedLabel: string) =
        
        let actualDisplay = getHotkeyDisplayString vKey modifiers
        let actualLabel = getToggleMenuLabel vKey modifiers

        Assert.Equal(expectedDisplay, actualDisplay)
        Assert.Equal(expectedLabel, actualLabel)

    // =========================================================================
    // Nhóm 2: Tổ hợp 3 phím tự chọn (Custom 3-key Hotkeys)
    // =========================================================================

    [<Theory>]
    [<InlineData(0x51u, 0x0006u, "Ctrl + Shift + Q", "Gõ tiếng Việt (Ctrl + Shift + Q)")>] // Ctrl + Shift + Q (Issue thực tế người dùng cài)
    [<InlineData(0x5Au, 0x0006u, "Ctrl + Shift + Z", "Gõ tiếng Việt (Ctrl + Shift + Z)")>] // Ctrl + Shift + Z
    [<InlineData(0x20u, 0x0003u, "Ctrl + Alt + Space", "Gõ tiếng Việt (Ctrl + Alt + Space)")>] // Ctrl + Alt + Space
    [<InlineData(0x41u, 0x0003u, "Ctrl + Alt + A", "Gõ tiếng Việt (Ctrl + Alt + A)")>]     // Ctrl + Alt + A
    let ``2. Multi-key custom combinations should build correct human-readable labels`` 
        (vKey: uint32, modifiers: uint32, expectedDisplay: string, expectedLabel: string) =

        let actualDisplay = getHotkeyDisplayString vKey modifiers
        let actualLabel = getToggleMenuLabel vKey modifiers

        Assert.Equal(expectedDisplay, actualDisplay)
        Assert.Equal(expectedLabel, actualLabel)

    // =========================================================================
    // Nhóm 3: Các phím đặc biệt, phím chức năng (Function keys, Numpad, Symbols)
    // =========================================================================

    [<Theory>]
    [<InlineData(0xC0u, 0x0002u, "Ctrl + ~", "Gõ tiếng Việt (Ctrl + ~)")>]                 // Ctrl + ~
    [<InlineData(0x70u, 0x0004u, "Shift + F1", "Gõ tiếng Việt (Shift + F1)")>]             // Shift + F1
    [<InlineData(0x7Bu, 0x0002u, "Ctrl + F12", "Gõ tiếng Việt (Ctrl + F12)")>]             // Ctrl + F12
    [<InlineData(0x60u, 0x0002u, "Ctrl + Num0", "Gõ tiếng Việt (Ctrl + Num0)")>]           // Ctrl + Num0
    [<InlineData(0x69u, 0x0001u, "Alt + Num9", "Gõ tiếng Việt (Alt + Num9)")>]             // Alt + Num9
    [<InlineData(0x08u, 0x0002u, "Ctrl + Backspace", "Gõ tiếng Việt (Ctrl + Backspace)")>] // Ctrl + Backspace
    [<InlineData(0x0Du, 0x0006u, "Ctrl + Shift + Enter", "Gõ tiếng Việt (Ctrl + Shift + Enter)")>] // Ctrl + Shift + Enter
    [<InlineData(0x14u, 0x0002u, "Ctrl + CapsLock", "Gõ tiếng Việt (Ctrl + CapsLock)")>]   // Ctrl + CapsLock
    [<InlineData(0x1Bu, 0x0001u, "Alt + Esc", "Gõ tiếng Việt (Alt + Esc)")>]               // Alt + Esc
    [<InlineData(0xBFu, 0x0002u, "Ctrl + /", "Gõ tiếng Việt (Ctrl + /)")>]                 // Ctrl + /
    [<InlineData(0xDCu, 0x0002u, "Ctrl + \\", "Gõ tiếng Việt (Ctrl + \\)")>]               // Ctrl + \
    [<InlineData(0xBDu, 0x0002u, "Ctrl + -", "Gõ tiếng Việt (Ctrl + -)")>]                 // Ctrl + -
    [<InlineData(0xBBu, 0x0002u, "Ctrl + =", "Gõ tiếng Việt (Ctrl + =)")>]                 // Ctrl + =
    let ``3. Special keys and functional keys should format properly`` 
        (vKey: uint32, modifiers: uint32, expectedDisplay: string, expectedLabel: string) =

        let actualDisplay = getHotkeyDisplayString vKey modifiers
        let actualLabel = getToggleMenuLabel vKey modifiers

        Assert.Equal(expectedDisplay, actualDisplay)
        Assert.Equal(expectedLabel, actualLabel)

    // =========================================================================
    // Nhóm 4: Khi tắt phím tắt (Disabled / None)
    // =========================================================================

    [<Fact>]
    let ``4. When hotkey is disabled (0, 0), display text indicates disabled and menu has no parenthesis`` () =
        let display = getHotkeyDisplayString 0u 0u
        let label = getToggleMenuLabel 0u 0u

        Assert.Equal("Không sử dụng phím tắt", display)
        Assert.Equal("Gõ tiếng Việt", label)
