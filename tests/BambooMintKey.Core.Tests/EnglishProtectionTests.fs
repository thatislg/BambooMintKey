// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

/// <summary>
/// Bộ kiểm thử cho Cơ chế Nhận diện & Bảo vệ Từ Tiếng Anh trong Chế độ Gõ Telex (English Word Protection).
/// Đặc tả theo tài liệu thiết kế chi tiết 005_02_01.md (Khắc phục triệt để Issue 4 Core/Corre).
/// </summary>
module EnglishProtectionTests =

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
    // Nhóm 1: Các từ kết thúc bằng nguyên âm câm đuôi -re (Core, more, care...)
    // =========================================================================

    [<Theory>]
    [<InlineData("Core", "Core")>]       // Issue 4 gốc: Gõ Core ra thẳng Core mà không cần gõ Corre
    [<InlineData("core", "core")>]       // Chữ thường core
    [<InlineData("CORE", "CORE")>]       // All-Caps CORE
    [<InlineData("Corre", "Core")>]     // Người dùng theo thói quen gõ 'Corre' (lặp r để hủy dấu) vẫn ra đúng 'Core'
    [<InlineData("corre", "core")>]     // Chữ thường corre -> core
    [<InlineData("more", "more")>]       // more không bị thành mỏe
    [<InlineData("morre", "more")>]     // morre -> more
    [<InlineData("care", "care")>]       // care không bị thành cảe
    [<InlineData("share", "share")>]     // share
    [<InlineData("fire", "fire")>]       // fire không bị thành fỉe
    [<InlineData("sure", "sure")>]       // sure không bị thành sủe
    [<InlineData("store", "store")>]     // store không bị thành stỏe
    [<InlineData("before", "before")>]   // before
    [<InlineData("pure", "pure")>]       // pure
    [<InlineData("cure", "cure")>]       // cure
    let ``1. Silent -re words should be protected and restored automatically`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 2: Các từ có đuôi phụ âm kép (-rt, -rd, -st, -rk, -rm, -rn...)
    // =========================================================================

    [<Theory>]
    [<InlineData("start", "start")>]     // Đuôi -rt
    [<InlineData("smart", "smart")>]     // Đuôi -rt
    [<InlineData("word", "word")>]       // Đuôi -rd
    [<InlineData("card", "card")>]       // Đuôi -rd
    [<InlineData("first", "first")>]     // Đuôi -st
    [<InlineData("last", "last")>]       // Đuôi -st
    [<InlineData("fast", "fast")>]       // Đuôi -st
    [<InlineData("test", "test")>]       // Đuôi -st
    [<InlineData("post", "post")>]       // Đuôi -st
    [<InlineData("work", "work")>]       // Đuôi -rk
    [<InlineData("form", "form")>]       // Đuôi -rm
    [<InlineData("term", "term")>]       // Đuôi -rm
    [<InlineData("turn", "turn")>]       // Đuôi -rn
    let ``2. Terminal consonant cluster words should bypass typing transforms`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 3: Các từ số nhiều / chia động từ đuôi -s (files, games, lines...)
    // =========================================================================

    [<Theory>]
    [<InlineData("files", "files")>]     // files không bị thành filés / fíle
    [<InlineData("lines", "lines")>]     // lines không bị thành linés / lín
    [<InlineData("games", "games")>]     // games không bị thành gám
    [<InlineData("notes", "notes")>]     // notes không bị thành nót
    [<InlineData("times", "times")>]     // times không bị thành tím
    [<InlineData("rules", "rules")>]     // rules
    [<InlineData("pages", "pages")>]     // pages
    [<InlineData("types", "types")>]     // types
    [<InlineData("cases", "cases")>]     // cases
    [<InlineData("tests", "tests")>]     // tests
    let ``3. English plural -s and -es endings should not be mangled by acute tone`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 4: Từ khóa lập trình & CNTT thông dụng
    // =========================================================================

    [<Theory>]
    [<InlineData("code", "code")>]
    [<InlineData("class", "class")>]
    [<InlineData("data", "data")>]
    [<InlineData("time", "time")>]
    [<InlineData("user", "user")>]
    [<InlineData("save", "save")>]
    [<InlineData("load", "load")>]
    [<InlineData("true", "true")>]
    [<InlineData("false", "false")>]
    [<InlineData("null", "null")>]
    [<InlineData("void", "void")>]
    [<InlineData("error", "error")>]
    let ``4. Common technical and programming keywords should be preserved cleanly`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 5: Gõ kèm phím ngắt từ WordBreak (Space Commit)
    // =========================================================================

    [<Theory>]
    [<InlineData("Core", ' ', "Core ")>]
    [<InlineData("start", ' ', "start ")>]
    [<InlineData("files", ' ', "files ")>]
    [<InlineData("first", ' ', "first ")>]
    let ``5. English words commit cleanly with break character`` (input: string, breakChar: char, expected: string) =
        let result = typeWordWithBreak input breakChar EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 6: Bảo vệ tiếng Việt không bị hồi quy (Zero Regression)
    // =========================================================================

    [<Theory>]
    [<InlineData("co", "co")>]           // Từ đơn tiếng Việt
    [<InlineData("cor", "cỏ")>]          // Gõ dấu hỏi Telex vẫn phải ra cỏ
    [<InlineData("cos", "có")>]          // Gõ dấu sắc Telex vẫn phải ra có
    [<InlineData("mor", "mỏ")>]          // mỏ
    [<InlineData("xoes", "xóe")>]        // xóe (cụm oe với x hợp lệ)
    [<InlineData("khoer", "khỏe")>]      // khỏe (cụm oe với kh hợp lệ)
    [<InlineData("phari", "phải")>]      // Bỏ dấu tự do vẫn hoạt động
    [<InlineData("Ddi", "Đi")>]          // Modifier hoa vẫn hoạt động
    [<InlineData("Uwu", "Ưu")>]          // Modifier hoa vẫn hoạt động
    let ``6. Standard Vietnamese typing must never regress`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 7: Khi tắt cấu hình AutoRestoreEnglishWords
    // =========================================================================

    [<Fact>]
    let ``7. When AutoRestoreEnglishWords is false, engine behaves under strict Telex rules`` () =
        let config = { EngineConfig.Default with AutoRestoreEnglishWords = false }
        // Khi tắt, gõ cor vẫn ra cỏ
        let result = typeWord "cor" config
        Assert.Equal("cỏ", result)
