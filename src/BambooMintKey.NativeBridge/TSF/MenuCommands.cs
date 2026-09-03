// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Các mã lệnh (Command IDs) cho Context Menu của nút Taskbar.
/// </summary>
public static class MenuCommands
{
    public const uint Base = TsfMenuFlags.TfLbMenuIdApp;

    // 1. Chuyển chế độ gõ
    public const uint ToggleVietnameseMode        = Base + 1;

    // 2. Kiểu đặt dấu thanh
    public const uint SubmenuToneStyle           = Base + 10;
    public const uint ToneStyleModern            = Base + 11; // Kiểu mới (òa, xòe, thủy)
    public const uint ToneStyleClassic           = Base + 12; // Kiểu cũ (oà, xoè, thuỷ)

    // 3. Tùy chọn ngữ pháp thông minh
    public const uint ToggleAutoRestoreEnglish   = Base + 20; // Khôi phục từ tiếng Anh
    public const uint ToggleRepeatKeyUndo        = Base + 21; // Gõ lặp để hoàn tác dấu
    public const uint ToggleLeadingWAsU          = Base + 22; // Phím 'w' đầu từ thành 'ư'

    // 4. Kiểu gõ (Mở rộng)
    public const uint SubmenuInputMethod         = Base + 30;
    public const uint MethodTelex                = Base + 31;
    public const uint MethodVni                  = Base + 32;
    public const uint MethodSimpleTelex          = Base + 33;

    // 5. Bảng mã (Mở rộng)
    public const uint SubmenuCharset             = Base + 40;
    public const uint CharsetUnicodePrecomposed  = Base + 41;
    public const uint CharsetUnicodeDecomposed   = Base + 42;
    public const uint CharsetTcvn3               = Base + 43;

    // 6. Cài đặt & Hệ thống
    public const uint OpenSettings               = Base + 50;
    public const uint AboutApp                   = Base + 51;
}
