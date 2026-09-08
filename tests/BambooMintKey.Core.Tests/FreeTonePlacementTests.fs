// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

/// <summary>
/// Bộ kiểm thử toàn diện cho tính năng Bỏ Dấu Tự Do (Free Tone Placement).
/// Đặc tả theo tài liệu thiết kế chi tiết 005_00_01.md.
/// </summary>
module FreeTonePlacementTests =

    let private typeWord (input: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    // =========================================================================
    // Nhóm 1: Bỏ dấu ở cuối từ không có phụ âm cuối và có phụ âm cuối
    // =========================================================================

    [<Theory>]
    [<InlineData("phari", "phải")>]      // Dấu hỏi 'r' gõ sau nguyên âm cuối
    [<InlineData("phair", "phải")>]      // Dấu hỏi 'r' gõ chuẩn Telex
    [<InlineData("nguoif", "nguồi")>]    // Dấu huyền 'f' trên cụm 'uoi' thành 'uôi' (VD: nguôi ngoai)
    [<InlineData("nguwoif", "người")>]   // Dấu huyền 'f' trên cụm 'ươi'
    [<InlineData("hoacs", "hoác")>]      // Dấu sắc 's' sau phụ âm cuối 'c'
    [<InlineData("hoacj", "hoạc")>]      // Dấu nặng 'j' sau phụ âm cuối 'c'
    [<InlineData("hoawcs", "hoắc")>]     // Dấu sắc 's' sau modifier 'aw' và phụ âm cuối 'c'
    [<InlineData("hoawcj", "hoặc")>]     // Dấu nặng 'j' sau modifier 'aw' và phụ âm cuối 'c'
    [<InlineData("toans", "toán")>]      // Dấu sắc 's' sau phụ âm cuối 'n'
    let ``1. Free tone placement at end of syllable should transform correctly`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 2: Gõ dấu trước khi hoàn chỉnh nguyên âm (Premature Tone Key)
    // =========================================================================

    [<Theory>]
    [<InlineData("phari", "phải")>]      // 'phar' ra 'phả', gõ tiếp 'i' tự chuyển dấu thành 'phải'
    [<InlineData("toasn", "toán")>]      // Gõ 's' trước khi gõ phụ âm cuối 'n'
    [<InlineData("tuwowr", "tưở")>]      // 'tuwow' ra 'tươ', gõ 'r' ra 'tưở'
    let ``2. Tone key typed before syllable is fully completed should resolve cleanly`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 3: Ghi đè dấu thanh trên đường gõ (Last-Win Tone Overrides)
    // =========================================================================

    [<Theory>]
    [<InlineData("toansf", "toàn")>]     // Dấu huyền 'f' ghi đè lên dấu sắc 's'
    [<InlineData("pharis", "phái")>]     // Dấu sắc 's' ghi đè lên dấu hỏi 'r'
    [<InlineData("thuyetjs", "thuyết")>] // Dấu sắc 's' ghi đè lên dấu nặng 'j'
    let ``3. Multiple tone keys should follow last-win precedence rule`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 4: Bảo vệ phụ âm đầu & Phòng ngừa nhầm lẫn từ tiếng Anh
    // =========================================================================

    [<Theory>]
    [<InlineData("sang", "sang")>]       // Phụ âm đầu 's' không bị coi là dấu sắc
    [<InlineData("rung", "rung")>]       // Phụ âm đầu 'r' không bị coi là dấu hỏi
    [<InlineData("xuan", "xuan")>]       // Phụ âm đầu 'x' không bị coi là dấu ngã
    [<InlineData("xuaan", "xuân")>]      // Phụ âm đầu 'x' và 'aa' thành 'â'
    [<InlineData("trang", "trang")>]     // Phụ âm đầu ghép 'tr' chứa 'r' không bị coi là dấu hỏi
    [<InlineData("tsaon", "tsaon")>]     // 's' đứng trước nguyên âm 'a' không phải là dấu thanh
    [<InlineData("first", "first")>]     // Từ tiếng Anh 'first' không bị biến đổi
    [<InlineData("core", "core")>]       // Từ tiếng Anh 'core' không bị biến đổi thành 'cỏe'
    let ``4. Protected initials and English words should not be corrupted by free tone`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 5: Lặp phím hủy dấu (Repeat-Key Undo)
    // =========================================================================

    [<Theory>]
    [<InlineData("mass", "mass")>]       // Gõ lặp 'ss' hủy dấu sắc, trả về chuỗi thô
    [<InlineData("pharr", "pharr")>]     // Gõ lặp 'rr' hủy dấu hỏi
    [<InlineData("hoacss", "hoacss")>]   // Gõ lặp 'ss' sau phụ âm cuối hủy dấu
    let ``5. Repeating tone keys should trigger undo back to raw text`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 6: Bảo tồn chữ hoa / chữ thường (LetterCase Preservation)
    // =========================================================================

    [<Theory>]
    [<InlineData("Phari", "Phải")>]      // Title Case
    [<InlineData("PHARI", "PHẢI")>]      // Upper Case
    [<InlineData("VIEETJ", "VIỆT")>]     // Upper Case với modifier inline
    [<InlineData("pHari", "pHải")>]      // Mixed Case
    let ``6. Free tone placement should preserve letter casing formats`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)

    // =========================================================================
    // Nhóm 7: Bật / Tắt tính năng qua cấu hình EngineConfig
    // =========================================================================

    [<Fact>]
    let ``7. Disabling AllowFreeTonePlacement should revert to strict classic Telex`` () =
        let disabledConfig = { EngineConfig.Default with AllowFreeTonePlacement = false }
        let result = typeWord "phari" disabledConfig
        // Khi tắt tính năng, 'phari' không được tự chuyển thành 'phải'
        Assert.Equal("phari", result)
