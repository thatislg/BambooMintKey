// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

/// <summary>
/// Bộ kiểm thử cho các sửa lỗi ngữ âm & đặt dấu biên (Phase 8 / Milestone 2).
/// Đặc tả theo tài liệu thiết kế 008_03_Phonotactic_Rules_Fix_Design.md.
/// </summary>
module RuleTests =

    let private typeWord (input: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    // =========================================================================
    // M2.1: Cụm "ua" + phím "w" phải ưu tiên biến thành "ưa" (không phải "uă")
    // =========================================================================

    [<Theory>]
    [<InlineData("vuawf", "vừa")>]     // vừa (lỗi gốc: vuawf -> vuằ)
    [<InlineData("muaw", "mưa")>]       // mưa
    [<InlineData("chuaw", "chưa")>]     // chưa
    [<InlineData("cuawj", "cựa")>]      // cựa
    [<InlineData("duawx", "dữa")>]      // dữa
    let ``M2.1 - ua + w nên thành ưa thay vì uă`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M2.1b: Phụ âm 'c' + cụm 'ua' là hợp lệ (cua, của, cửa)
    // =========================================================================

    [<Theory>]
    [<InlineData("cua", "cua")>]        // cua
    [<InlineData("cuar", "của")>]       // của
    [<InlineData("cuawr", "cửa")>]      // cửa
    let ``M2.1b - c + ua là âm tiết hợp lệ`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M2.2: Cụm "uo" + "w" biến thành "ươ" (đương, thương...) không hồi quy
    // =========================================================================

    [<Theory>]
    [<InlineData("duowng", "dương")>]    // dương (d + ương)
    [<InlineData("dduowng", "đương")>]    // đương (đ + ương)
    [<InlineData("thuowng", "thương")>]   // thương
    let ``M2.2 - uo + w nên thành ươ`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M2.2b: Cụm nguyên âm "ưi" hợp lệ (gửi, cửi, ngửi)
    // =========================================================================

    [<Theory>]
    [<InlineData("guwir", "gửi")>]
    [<InlineData("cuwir", "cửi")>]
    let ``M2.2b - cụm ưi hợp lệ`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M2.3: Chuẩn hóa vị trí đặt dấu thanh trên các cụm nguyên âm biên
    // =========================================================================

    [<Theory>]
    [<InlineData("hoas", "hóa")>]       // oa (Modern) -> dấu trên o (index 0)
    [<InlineData("thuys", "thúy")>]     // uy (Modern) -> dấu trên u (index 0)
    [<InlineData("tuooir", "tuổi")>]    // uôi -> dấu trên ô (index 1)
    [<InlineData("tuoois", "tuối")>]    // uôi -> dấu trên ô (index 1)
    let ``M2.3 - đặt dấu thanh đúng vị trí trên cụm nguyên âm biên`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M2.5: Ngoại lệ chính tả "gì" — 'gi' + phím dấu thanh -> 'g' + 'ì/í/ỉ/ĩ/ị'
    // =========================================================================

    [<Theory>]
    [<InlineData("gif", "gì")>]       // gì (ngoại lệ g + i)
    [<InlineData("gir", "gỉ")>]       // gỉ
    [<InlineData("gis", "gí")>]       // gí
    [<InlineData("gix", "gĩ")>]       // gĩ
    [<InlineData("gij", "gị")>]       // gị
    [<InlineData("Gif", "Gì")>]       // Gì (title case)
    let ``M2.5 - gi + tone nên thành gì`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("gias", "giá")>]       // gi (phụ âm) + a sắc = giá, KHÔNG thành gís
    [<InlineData("gior", "giỏ")>]       // gi + o hỏi = giỏ
    [<InlineData("giuwx", "giữ")>]      // gi + ư ngã = giữ
    let ``M2.5 - không hồi quy phụ âm gi`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // M2.4: Zero-regression — bảo vệ từ tiếng Anh & các từ tiếng Việt chuẩn
    // =========================================================================

    [<Theory>]
    [<InlineData("core", "core")>]      // English protection không bị phá
    [<InlineData("more", "more")>]
    [<InlineData("start", "start")>]
    [<InlineData("cor", "cỏ")>]         // cỏ (Telex chuẩn)
    [<InlineData("cos", "có")>]         // có
    [<InlineData("mor", "mỏ")>]         // mỏ
    [<InlineData("xoes", "xóe")>]       // xóe (oe với x hợp lệ)
    [<InlineData("khoer", "khỏe")>]     // khỏe
    let ``M2.4 - không hồi quy các từ chuẩn`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)
