// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Domain

open System

/// <summary>
/// Module chuẩn hóa và định dạng phím tắt hiển thị trên Giao diện điều khiển (UI) và Thanh ngôn ngữ (Language Bar).
/// Đảm bảo đồng bộ tuyệt đối giữa nhãn menu Language Bar và cấu hình phím tắt trong Shared Memory.
/// </summary>
module HotkeyFormatter =

    /// <summary>
    /// Chuyển đổi mã phím ảo (Virtual Key) và cờ phím bổ trợ (Modifiers) thành chuỗi hiển thị phím tắt ngắn gọn.
    /// Ví dụ: (0x10, 0x0202) -> "Ctrl + Shift", (0x51, 0x0006) -> "Ctrl + Shift + Q"
    /// </summary>
    let getHotkeyDisplayString (vKey: uint32) (modifiers: uint32) : string =
        if vKey = 0u && modifiers = 0u then "Không sử dụng phím tắt"
        elif vKey = 0x10u && (modifiers = 0x0202u || modifiers = 0x0002u) then "Ctrl + Shift"
        elif vKey = 0x10u && (modifiers = 0x0201u || modifiers = 0x0001u) then "Alt + Shift"
        elif vKey = 0x5Au && modifiers = 0x0001u then "Alt + Z"
        elif vKey = 0x20u && modifiers = 0x0002u then "Ctrl + Space"
        else
            let parts = System.Collections.Generic.List<string>()
            if (modifiers &&& 0x0002u) <> 0u then parts.Add("Ctrl")
            if (modifiers &&& 0x0001u) <> 0u then parts.Add("Alt")
            if (modifiers &&& 0x0004u) <> 0u then parts.Add("Shift")
            
            let keyName =
                match vKey with
                | 0x20u -> "Space"
                | 0x10u -> "Shift"
                | 0x11u -> "Ctrl"
                | 0x12u -> "Alt"
                | 0xC0u -> "~"
                | 0xDCu -> "\\"
                | 0xBFu -> "/"
                | 0xDBu -> "["
                | 0xDDu -> "]"
                | 0xBAu -> ";"
                | 0xDEu -> "'"
                | 0xBCu -> ","
                | 0xBEu -> "."
                | 0xBDu -> "-"
                | 0xBBu -> "="
                | 0x08u -> "Backspace"
                | 0x09u -> "Tab"
                | 0x0Du -> "Enter"
                | 0x14u -> "CapsLock"
                | 0x1Bu -> "Esc"
                | vk when vk >= 0x41u && vk <= 0x5Au -> (char vk).ToString()
                | vk when vk >= 0x30u && vk <= 0x39u -> (char vk).ToString()
                | vk when vk >= 0x60u && vk <= 0x69u -> sprintf "Num%d" (vk - 0x60u)
                | vk when vk >= 0x70u && vk <= 0x7Bu -> sprintf "F%d" (vk - 0x70u + 1u)
                | vk -> sprintf "0x%X" vk

            if not (parts.Contains(keyName)) then
                parts.Add(keyName)

            if parts.Count > 0 then
                String.Join(" + ", parts)
            else
                "Không sử dụng phím tắt"

    /// <summary>
    /// Tạo chuỗi nhãn hiển thị cho mục menu chuyển đổi chế độ gõ trên Language Bar / Taskbar context menu.
    /// Ví dụ: "Gõ tiếng Việt (Ctrl + Shift)", "Gõ tiếng Việt (Ctrl + Shift + Q)", hoặc "Gõ tiếng Việt" khi tắt phím tắt.
    /// </summary>
    let getToggleMenuLabel (vKey: uint32) (modifiers: uint32) : string =
        if vKey = 0u && modifiers = 0u then
            "Gõ tiếng Việt"
        else
            let display = getHotkeyDisplayString vKey modifiers
            if display = "Không sử dụng phím tắt" then
                "Gõ tiếng Việt"
            else
                sprintf "Gõ tiếng Việt (%s)" display
