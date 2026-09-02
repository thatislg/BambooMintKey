// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

module TonePlacementTests =

    let typeWordWithStyle (keys: string) (style: TonePlacementStyle) : string =
        let config = { EngineConfig.Default with ToneStyle = style }
        let mutable state = WordState.Empty
        for c in keys do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    // 1. Cụm 2 nguyên âm mở (Không có phụ âm cuối, ví dụ: oa, oe, uy)
    // - Modern (Mới): Dấu đặt ở nguyên âm thứ 2 (hóa, xòe, thúy)
    // - Traditional (Cũ): Dấu đặt ở nguyên âm thứ 1 (hoá, xoè, thuý)
    [<Theory>]
    [<InlineData("hoas", "hóa")>]
    [<InlineData("xoef", "xòe")>]
    [<InlineData("thuys", "thúy")>]
    [<InlineData("hoar", "hỏa")>]
    [<InlineData("xoex", "xõe")>]
    [<InlineData("thuyj", "thụy")>]
    let ``1. Modern style: Tone on 2nd vowel for open pairs`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)

    [<Theory>]
    [<InlineData("hoas", "hoá")>]
    [<InlineData("xoef", "xoè")>]
    [<InlineData("thuys", "thuý")>]
    [<InlineData("hoar", "hoả")>]
    [<InlineData("xoex", "xoẽ")>]
    [<InlineData("thuyj", "thuỵ")>]
    let ``2. Traditional style: Tone on 1st vowel for open pairs`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Traditional)

    // 2. Cụm có phụ âm cuối
    // - Dấu LUÔN LUÔN được đặt ngay trên nguyên âm ở trước phụ âm cuối, không phân biệt Modern / Traditional
    [<Theory>]
    [<InlineData("hoans", "hoán")>]
    [<InlineData("toanf", "toàn")>]
    [<InlineData("bieens", "biến")>]
    [<InlineData("buoonf", "buồn")>]
    [<InlineData("muownj", "mượn")>]
    [<InlineData("muowns", "mướn")>]
    [<InlineData("thuyets", "thuyết")>]
    [<InlineData("chuyenj", "chuyện")>]
    [<InlineData("tieengs", "tiếng")>]
    let ``3. Tone always placed before final consonant`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Traditional)

    // 3. Cụm 3 nguyên âm mở (Không có phụ âm cuối, ví dụ: oai, uay, yeu)
    // - Dấu LUÔN LUÔN nằm ở nguyên âm chính ở GIỮA vần
    [<Theory>]
    [<InlineData("ngoais", "ngoái")>]
    [<InlineData("khoair", "khoải")>]
    [<InlineData("khuays", "khuấy")>]
    [<InlineData("yeus", "yếu")>]
    [<InlineData("tieur", "tiểu")>]
    [<InlineData("muoix", "muỗi")>]
    [<InlineData("ruowuj", "rượu")>]
    let ``4. Tone on middle vowel for open triphthongs (oai, uay, yeu)`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)

    // 4. Nguyên âm đôi hoặc phức có chứa dấu phụ (ưa, ươ, uô, ia)
    // - Dấu đặt tự động ưu tiên vào nguyên âm chứa dấu (ươ, ư) hoặc đúng vị trí chính tả
    [<Theory>]
    [<InlineData("muas", "múa")>]
    [<InlineData("mias", "mía")>]
    [<InlineData("muwas", "mứa")>] // m + u + w + a + s
    [<InlineData("nguwaf", "ngừa")>]
    [<InlineData("chuas", "chúa")>]
    let ``5. Tone on modified element for special vowel pairs`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)

    // 5. Phụ âm đầu đặc biệt (qu- và gi-)
    // - "u" trong "qu" và "i" trong "gi" thuộc về phụ âm đầu. Dấu KHÔNG ĐƯỢC đặt vào chúng.
    [<Theory>]
    [<InlineData("quas", "quá")>]
    [<InlineData("gias", "giá")>]
    [<InlineData("quys", "quý")>]
    [<InlineData("giets", "giết")>]
    [<InlineData("quocs", "quốc")>]
    let ``6. Tone appropriately escapes qu and gi initial consonants`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)

    // 6. Chuyển dịch dấu động (Đổi dấu) trực tiếp khi gõ
    [<Theory>]
    [<InlineData("toansf", "toàn")>] // Gõ s (sắc) rồi f (huyền) trên cùng từ -> thành huyền
    [<InlineData("chuasf", "chùa")>] // Sắc -> Huyền
    [<InlineData("thuyesft", "thuyết")>] // Chuyển dấu trên hành trình gõ
    let ``7. Tone switches cleanly when overriding tone keys`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)

    // 7. Bảo toàn chữ HOA, chữ thường (Case Insensitive Typing)
    [<Theory>]
    [<InlineData("HOAS", "HÓA")>]
    [<InlineData("Thuys", "Thúy")>]
    [<InlineData("thUyS", "thÚy")>]
    let ``8. Tone placement preserves matching cases`` (input: string, expected: string) =
        Assert.Equal(expected, typeWordWithStyle input TonePlacementStyle.Modern)