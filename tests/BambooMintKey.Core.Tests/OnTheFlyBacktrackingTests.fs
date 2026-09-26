// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

/// <summary>
/// Bộ kiểm thử cho On-the-fly Validation & English Backtracking (Phase 8 / Milestone 4).
/// </summary>
module OnTheFlyBacktrackingTests =

    let private typeWord (input: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    // =========================================================================
    // M4.2: Thẩm định âm tiết tiếng Việt — từ hợp lệ KHÔNG bị backtrack nhầm
    // =========================================================================

    [<Theory>]
    [<InlineData("has", "há")>]       // "has" là English nhưng "há" hợp lệ -> giữ Việt
    [<InlineData("cos", "có")>]       // có
    [<InlineData("cor", "cỏ")>]       // cỏ
    [<InlineData("mor", "mỏ")>]       // mỏ
    [<InlineData("xoes", "xóe")>]     // xóe
    [<InlineData("khoer", "khỏe")>]   // khỏe
    let ``M4.2 - từ Việt hợp lệ không bị backtrack nhầm thành English`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M4.3: Backtrack English — từ tiếng Anh không bị biến đổi thành âm tiết Việt
    // =========================================================================

    [<Theory>]
    [<InlineData("post", "post")>]     // đuôi -st
    [<InlineData("form", "form")>]     // đuôi -rm
    [<InlineData("core", "core")>]     // đuôi -re
    [<InlineData("start", "start")>]   // đuôi -rt
    let ``M4.3 - từ tiếng Anh được hoàn tác đúng`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M4.1: Cờ cấu hình EnableEnglishBacktracking
    // =========================================================================

    [<Fact>]
    let ``M4.1 - tắt EnableEnglishBacktracking vẫn giữ nguyên hành vi Việt`` () =
        let config = { EngineConfig.Default with EnableEnglishBacktracking = false }
        // Khi tắt backtrack, các từ Việt vẫn hoạt động bình thường
        Assert.Equal("há", typeWord "has" config)
        Assert.Equal("có", typeWord "cos" config)

    [<Fact>]
    let ``M4.1 - tắt EnableVietnameseDictionary vẫn giữ nguyên hành vi Việt`` () =
        let config = { EngineConfig.Default with EnableVietnameseDictionary = false }
        Assert.Equal("há", typeWord "has" config)
        Assert.Equal("có", typeWord "cos" config)
