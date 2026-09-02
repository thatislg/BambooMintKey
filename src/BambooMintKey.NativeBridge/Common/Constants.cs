// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.NativeBridge.Common;

/// <summary>
/// Các hằng số cấu hình COM/TSF: ngôn ngữ, tên hiển thị, threading model.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public static class Constants
{
    /// <summary>Language ID tiếng Việt (Vietnam) cho TSF Language Profile: 0x042A.</summary>
    public const ushort LangIdVietnamese = 0x042A;

    /// <summary>Tên hiển thị của Text Service trên Language Bar.</summary>
    public const string TextServiceName = "BambooMintKey Vietnamese Input";

    /// <summary>Mô tả đầy đủ của Text Service.</summary>
    public const string TextServiceDescription = "BambooMintKey TSF Telex Engine";

    /// <summary>ThreadingModel của COM InprocServer32. TSF TIP yêu cầu Apartment.</summary>
    public const string ThreadingModel = "Apartment";
}
