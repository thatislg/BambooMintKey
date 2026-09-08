// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

/// <summary>
/// Bộ kiểm thử cho tính năng Xử lý Modifier chữ hoa đầu từ & Mở rộng âm tiết động (Uppercase Modifier & Dynamic Syllable Expansion).
/// Đặc tả theo tài liệu thiết kế chi tiết 005_01_01.md (Khắc phục lỗi Ddi -> Đi, Uwu -> Ưu).
/// </summary>
module UppercaseModifierTests =

    let private typeWord (input: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    let private typeWordWithBreak (input: string) (breakChar: char) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        let mutable committed = ""
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        let _, action = TelexEngine.processKey state (KeyInput.WordBreak breakChar) config
        match action with
        | EngineAction.Commit text -> text
        | _ -> state.TransformedText

    // =========================================================================
    // Nhóm 1: Biến thể của Dd (Ddi -> Đi, Ddaat -> Đât, Ddaats -> Đất...)
    // =========================================================================

    [<Theory>]
    [<InlineData("Ddi", "Đi")>]          // Lỗi 2 gốc: Ddi -> Đi
    [<InlineData("ddi", "đi")>]          // Chữ thường: ddi -> đi
    [<InlineData("DDI", "ĐI")>]          // All-Caps: DDI -> ĐI
    [<InlineData("Ddaat", "Đât")>]       // Dd đi với vần có modifier kép (aa + t -> ât)
    [<InlineData("Ddaats", "Đất")>]      // Dd đi với vần ât + sắc -> Đất
    [<InlineData("Ddo", "Đo")>]          // Dd đi với nguyên âm đơn o
    [<InlineData("Dduowng", "Đương")>]   // Dd đi với vần phức ươ + ng
    [<InlineData("Ddaau", "Đâu")>]       // Dd đi với nhị trùng âm âu
    [<InlineData("Ddiem", "Điêm")>]      // Dd đi với vần iem
    [<InlineData("Ddaay", "Đây")>]       // Dd đi với vần ây
    let ``1. Dd with following vowels should transform correctly into D bar syllables`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 2: Biến thể của Uw (Uwu -> Ưu, Uwa -> Ưa, Uwng -> Ưng...)
    // =========================================================================

    [<Theory>]
    [<InlineData("Uwu", "Ưu")>]          // Lỗi 3 gốc: Uwu -> Ưu
    [<InlineData("uwu", "ưu")>]          // Chữ thường: uwu -> ưu
    [<InlineData("UWU", "ƯU")>]          // All-Caps: UWU -> ƯU
    [<InlineData("Uwa", "Ưa")>]          // Cụm nguyên âm ưa
    [<InlineData("Uwng", "Ưng")>]        // Ư + phụ âm cuối ng
    [<InlineData("Uwowsc", "Ước")>]      // Vần phức ươ + c + sắc
    [<InlineData("Uwoi", "Ươi")>]        // Cụm nguyên âm ươi
    [<InlineData("Uwowu", "Ươu")>]       // Cụm nguyên âm ươu (như trong rượu)
    let ``2. Uw with following vowels should expand into valid horn vowel clusters`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 3: Các Modifier Chữ Hoa Khác (Ow, Aw, Aa, Ee, Oo)
    // =========================================================================

    [<Theory>]
    [<InlineData("Owi", "Ơi")>]          // Ow + i -> Ơi
    [<InlineData("OWI", "ƠI")>]          // All-Caps OWI -> ƠI
    [<InlineData("Owsc", "Ớc")>]        // Ơ + c + sắc
    [<InlineData("Awn", "Ăn")>]          // Aw + n -> Ăn
    [<InlineData("AWN", "ĂN")>]          // All-Caps AWN -> ĂN
    [<InlineData("Aam", "Âm")>]          // Aa + m -> Âm
    [<InlineData("Eem", "Êm")>]          // Ee + m -> Êm
    [<InlineData("Oom", "Ôm")>]          // Oo + m -> Ôm
    let ``3. Other uppercase modifiers should resolve and expand correctly`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 4: Kết hợp Bỏ Dấu Tự Do (Free Tone Placement)
    // =========================================================================

    [<Theory>]
    [<InlineData("Ddiemr", "Điểm")>]     // Dd + iem + r (dấu hỏi ở cuối từ)
    [<InlineData("Ddenr", "Đẻn")>]       // Dd + en + r -> Đẻn
    [<InlineData("Ddeenr", "Đển")>]      // Dd + een + r -> Đển
    [<InlineData("Dduwowngf", "Đường")>] // Dd + uwowng + f
    let ``4. Capitalized modifier syllables with free tone placement should resolve cleanly`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 5: Undo Lặp Phím (Repeat Key Undo)
    // =========================================================================

    [<Theory>]
    [<InlineData("Ddd", "Ddd")>]         // Dd -> Đ, lặp d -> Ddd
    [<InlineData("ddd", "ddd")>]         // dd -> đ, lặp d -> ddd
    [<InlineData("Uww", "Uww")>]         // Uw -> Ư, lặp w -> Uww
    [<InlineData("uww", "uww")>]         // uw -> ư, lặp w -> uww
    [<InlineData("Oww", "Oww")>]         // Ow -> Ơ, lặp w -> Oww
    let ``5. Repeat modifier key should trigger undo correctly`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 6: An Toàn Tiếng Anh & Phản Ví Dụ (Negative Tests)
    // =========================================================================

    [<Theory>]
    [<InlineData("Uwe", "Uwe")>]         // Tên riêng tiếng Anh Uwe không bị biến đổi bừa bãi (ưe không hợp lệ)
    [<InlineData("Uwi", "Uwi")>]         // Cụm ưi không tồn tại trong tiếng Việt -> giữ nguyên
    [<InlineData("Ddx", "Ddx")>]         // Phụ âm bất quy tắc x đi sau Dd -> giữ nguyên
    let ``6. Invalid Vietnamese vowel clusters should fall back to original text`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 7: Gõ Kèm Phím Ngắt Từ (WordBreak Space)
    // =========================================================================

    [<Theory>]
    [<InlineData("Ddi", ' ', "Đi ")>]    // Tái hiện chính xác thao tác trong Issue 2
    [<InlineData("Uwu", ' ', "Ưu ")>]    // Tái hiện chính xác thao tác trong Issue 3
    [<InlineData("Owi", ' ', "Ơi ")>]
    let ``7. WordBreak with space commits correctly`` (input: string, breakChar: char, expected: string) =
        let result = typeWordWithBreak input breakChar EngineConfig.Default
        Assert.Equal(expected, result)
