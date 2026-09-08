// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

/// <summary>
/// Bộ kiểm thử chống hồi quy cho lỗi tự động viết hoa (Auto-capitalization bug 005_03).
/// Kiểm tra triệt để hành vi viết hoa đầu từ với modifier kép (DDi -> Đi, EEm -> Êm, AAn -> Ân, OOn -> Ôn, AWn -> Ăn...)
/// và đảm bảo không suy thoái các trường hợp hợp lệ (Om, Ung, Owm, Own, Uwong, v.v.).
/// </summary>
module AutoCapitalizationTests =

    let private typeWord (input: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    let private typeWordWithBreak (input: string) (breakChar: char) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        let _, action = TelexEngine.processKey state (KeyInput.WordBreak breakChar) config
        match action with
        | EngineAction.Commit text -> text
        | _ -> state.TransformedText

    // =========================================================================
    // Nhóm 1: Các trường hợp modifier kép gõ khi giữ Shift (DDi, EEm, AAn, OOn, AWn...)
    // =========================================================================

    [<Theory>]
    [<InlineData("DDi", "Đi")>]          // Lỗi 005_03 gốc: DDi -> Đi
    [<InlineData("Ddi", "Đi")>]          // Ddi -> Đi
    [<InlineData("ddi", "đi")>]          // ddi -> đi
    [<InlineData("DDI", "ĐI")>]          // DDI (cố ý All-Caps) -> ĐI
    [<InlineData("DDeem", "Đêm")>]       // DD đi với ee + m -> Đêm
    [<InlineData("DDaats", "Đất")>]      // DD đi với aa + t + s -> Đất
    [<InlineData("DDuowng", "Đương")>]   // DD đi với uowng -> Đương
    [<InlineData("DDang", "Đang")>]      // DD đi với ang -> Đang
    let ``1. Initial DD modifier should produce Title case when following characters are lowercase`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("EEm", "Êm")>]          // Lỗi 005_03: EEm -> Êm
    [<InlineData("Eem", "Êm")>]          // Eem -> Êm
    [<InlineData("EEM", "ÊM")>]          // All-Caps: EEM -> ÊM
    [<InlineData("EEn", "Ên")>]          // EEn -> Ên
    let ``2. Initial EE modifier should produce Title case when following characters are lowercase`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("AAn", "Ân")>]          // Lỗi 005_03: AAn -> Ân
    [<InlineData("Aan", "Ân")>]          // Aan -> Ân
    [<InlineData("AAN", "ÂN")>]          // All-Caps: AAN -> ÂN
    [<InlineData("AAm", "Âm")>]          // AAm -> Âm
    let ``3. Initial AA modifier should produce Title case when following characters are lowercase`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("OOn", "Ôn")>]          // Lỗi 005_03: OOn -> Ôn
    [<InlineData("Oon", "Ôn")>]          // Oon -> Ôn
    [<InlineData("OON", "ÔN")>]          // All-Caps: OON -> ÔN
    [<InlineData("OOm", "Ôm")>]          // OOm -> Ôm
    let ``4. Initial OO modifier should produce Title case when following characters are lowercase`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("AWn", "Ăn")>]          // Lỗi 005_03: AWn -> Ăn
    [<InlineData("Awn", "Ăn")>]          // Awn -> Ăn
    [<InlineData("AWN", "ĂN")>]          // All-Caps: AWN -> ĂN
    [<InlineData("AWm", "Ăm")>]          // AWm -> Ăm
    let ``5. Initial AW modifier should produce Title case when following characters are lowercase`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("OWm", "Ơm")>]          // OWm -> Ơm
    [<InlineData("Owm", "Ơm")>]          // Owm -> Ơm
    [<InlineData("OWn", "Ơn")>]          // OWn -> Ơn
    [<InlineData("Own", "Ơn")>]          // Own -> Ơn
    [<InlineData("UWu", "Ưu")>]          // UWu -> Ưu
    [<InlineData("Uwu", "Ưu")>]          // Uwu -> Ưu
    let ``6. Initial OW and UW modifiers should produce Title case when following characters are lowercase`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 2: Các trường hợp không lỗi đã chuẩn (Om, Ung, Uwong...) đảm bảo không suy thoái
    // =========================================================================

    [<Theory>]
    [<InlineData("Om", "Om")>]
    [<InlineData("Ung", "Ung")>]
    [<InlineData("Uwong", "Ương")>]
    [<InlineData("Anh", "Anh")>]
    [<InlineData("Em", "Em")>]
    let ``7. Standard single-character uppercase words should remain unaffected`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 3: Kiểm tra kèm phím ngắt từ (WordBreak space)
    // =========================================================================

    [<Theory>]
    [<InlineData("DDi", ' ', "Đi ")>]
    [<InlineData("EEm", ' ', "Êm ")>]
    [<InlineData("AWn", ' ', "Ăn ")>]
    [<InlineData("AAn", ' ', "Ân ")>]
    [<InlineData("OOn", ' ', "Ôn ")>]
    [<InlineData("Om", ' ', "Om ")>]
    [<InlineData("Ung", ' ', "Ung ")>]
    let ``8. WordBreak space should commit TitleCase correctly without capitalization leak`` (input: string, breakChar: char, expected: string) =
        let result = typeWordWithBreak input breakChar EngineConfig.Default
        Assert.Equal(expected, result)
