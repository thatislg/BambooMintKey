// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open System.Diagnostics
open Xunit
open BambooMintKey.Core.Dictionary
open BambooMintKey.Core.Domain

/// <summary>
/// Bộ kiểm thử & benchmark cho FrozenDictionaryService (Phase 8 / Milestone 3).
/// </summary>
module DictionaryServiceTests =

    let service = FrozenDictionaryService()

    // =========================================================================
    // M3.3/M3.5: Kiểm tra âm tiết tiếng Việt (độ phủ từ Embedded Resource)
    // =========================================================================

    [<Theory>]
    [<InlineData("vừa")>]
    [<InlineData("mưa")>]
    [<InlineData("chưa")>]
    [<InlineData("của")>]
    [<InlineData("cửa")>]
    [<InlineData("khuya")>]
    [<InlineData("đường")>]
    [<InlineData("nghiêng")>]
    [<InlineData("khướu")>]
    let ``Âm tiết tiếng Việt hợp lệ`` (word: string) =
        Assert.True(service.IsValidVietnameseSyllable word, sprintf "'%s' nên hợp lệ" word)

    [<Theory>]
    [<InlineData("xyz")>]
    [<InlineData("qqqq")>]
    [<InlineData("zzzzz")>]
    [<InlineData("")>]
    let ``Âm tiết không hợp lệ`` (word: string) =
        Assert.False(service.IsValidVietnameseSyllable word)

    // =========================================================================
    // M3.3: Kiểm tra từ tiếng Anh thông dụng
    // =========================================================================

    [<Theory>]
    [<InlineData("the")>]
    [<InlineData("of")>]
    [<InlineData("and")>]
    [<InlineData("code")>]
    [<InlineData("system")>]
    [<InlineData("development")>]
    let ``Từ tiếng Anh thông dụng`` (word: string) =
        Assert.True(service.IsLikelyEnglishWord word, sprintf "'%s' nên là tiếng Anh" word)

    [<Fact>]
    let ``Từ tiếng Việt không phải tiếng Anh`` () =
        Assert.False(service.IsLikelyEnglishWord "vừa")

    // =========================================================================
    // M3.4: MergeCustomWords (từ điển người dùng mở rộng)
    // =========================================================================

    [<Fact>]
    let ``MergeCustomWords nạp thêm từ mở rộng`` () =
        service.MergeCustomWords [ "zalo"; "quẩy" ]
        Assert.True(service.IsLikelyEnglishWord "zalo")
        Assert.True(service.IsValidVietnameseSyllable "quẩy")

    // =========================================================================
    // M3.5: Benchmark — tra cứu O(1) không cấp phát heap
    // =========================================================================

    [<Fact>]
    let ``Benchmark lookup dưới 1 micro giây`` () =
        // warm-up để lazy load dict trước khi đo
        ignore (service.IsValidVietnameseSyllable "đường")
        let sw = Stopwatch.StartNew()
        let n = 100000
        for _ in 1 .. n do
            ignore (service.IsValidVietnameseSyllable "đường")
        sw.Stop()
        let perLookup = sw.Elapsed.TotalNanoseconds / float n
        Assert.True(perLookup < 1000.0, sprintf "Lookup quá chậm: %f ns/call" perLookup)
