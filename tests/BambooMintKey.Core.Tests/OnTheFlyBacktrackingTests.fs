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
    [<InlineData("post", "pót")>]     // post là English nhưng "pót" hợp lệ -> giữ Việt
    [<InlineData("turn", "tủn")>]     // turn là English nhưng "tủn" hợp lệ -> giữ Việt
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
    [<InlineData("form", "form")>]     // đuôi -rm
    [<InlineData("core", "core")>]     // đuôi -re
    [<InlineData("start", "start")>]   // đuôi -rt
    [<InlineData("learn", "learn")>]   // đuôi -rn (hardcode)
    [<InlineData("term", "term")>]     // đuôi -rm (qua hardcode, vì "rm" bị loại khỏi cluster)
    [<InlineData("sort", "sort")>]     // 'r' hỏi + âm tắc 't' -> không ép thành 'sót'
    [<InlineData("corp", "corp")>]     // 'r' hỏi + âm tắc 'p' -> không ép thành 'cóp'
    let ``M4.3 - từ tiếng Anh được hoàn tác đúng`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M4.6: Free tone dấu hỏi (r) + phụ âm cuối n — không bị nhận nhầm là English cluster "rn"
    // =========================================================================

    [<Theory>]
    [<InlineData("chuaarn", "chuẩn")>]  // dấu hỏi r giữa từ (fix: loại "rn" khỏi English cluster)
    [<InlineData("chuaanr", "chuẩn")>]  // dấu hỏi cuối
    [<InlineData("chuaafn", "chuần")>]  // dấu huyền (không bị, kiểm chứng các dấu khác)
    [<InlineData("chuaaxn", "chuẫn")>]  // dấu ngã
    [<InlineData("chuaasn", "chuấn")>]  // dấu sắc
    let ``M4.6 - free tone dấu hỏi + phụ âm cuối n không bị backtrack`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M4.6b: Free tone dấu hỏi (r) + phụ âm cuối m — không bị nhận nhầm là English cluster "rm"
    // =========================================================================

    [<Theory>]
    [<InlineData("kharm", "khảm")>]  // dấu hỏi r giữa từ (fix: loại "rm" khỏi English cluster)
    [<InlineData("khamr", "khảm")>]  // dấu hỏi cuối
    [<InlineData("đarm", "đảm")>]    // đảm
    let ``M4.6b - free tone dấu hỏi + phụ âm cuối m không bị backtrack`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M4.6c: Free tone dấu sắc (s) + phụ âm cuối p — không bị nhận nhầm là English cluster "sp"
    // =========================================================================

    [<Theory>]
    [<InlineData("tieesp", "tiếp")>]  // dấu sắc s giữa từ (fix: loại "sp" khỏi English cluster)
    [<InlineData("tieeps", "tiếp")>]  // dấu sắc cuối
    [<InlineData("thaasp", "thấp")>]  // thấp
    [<InlineData("clasp", "clasp")>]  // tiếng Anh vẫn được bảo vệ qua M4 (cláp không hợp lệ)
    [<InlineData("crisp", "crisp")>]  // tiếng Anh vẫn được bảo vệ qua M4
    [<InlineData("wasp", "wasp")>]    // tiếng Anh vẫn được bảo vệ qua M4
    let ``M4.6c - free tone dấu sắc + phụ âm cuối p không bị backtrack`` (input: string, expected: string) =
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
