// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Engine
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit

module SimpleTelexTests =

    let typeWord (keys: string) : string =
        let mutable state = WordState.Empty
        for c in keys do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        state.TransformedText

    // 1. Phím tạo dấu và modifier cơ bản
    [<Theory>]
    [<InlineData("as", "á")>]
    [<InlineData("af", "à")>]
    [<InlineData("ar", "ả")>]
    [<InlineData("ax", "ã")>]
    [<InlineData("aj", "ạ")>]
    [<InlineData("aa", "â")>]
    [<InlineData("aw", "ă")>]
    [<InlineData("ee", "ê")>]
    [<InlineData("oo", "ô")>]
    [<InlineData("ow", "ơ")>]
    [<InlineData("uw", "ư")>]
    [<InlineData("dd", "đ")>]
    let ``1. Basic Telex modifier and tone mappings match`` (input: string, expected: string) =
        Assert.Equal(expected, typeWord input)

    // 2. Combo Modifiers phức tạp (ư, ơ, ươ)
    // - w đi sau o -> ơ, đi sau u -> ư
    // - w đi sau uo -> ươ
    [<Theory>]
    [<InlineData("uow", "ươ")>] // phím w làm móc cả u và o
    [<InlineData("uwow", "ươ")>] // w làm ư rồi w lần nữa làm ơ 
    [<InlineData("huwowng", "hương")>]
    [<InlineData("huowng", "hương")>]
    [<InlineData("tuwowr", "tưở")>] // -> u w -> ư, o -> ơ, r -> hỏi
    let ``2. Complex modifiers uow should format ươ naturally`` (input: string, expected: string) =
        Assert.Equal(expected, typeWord input)

    // 3. Fallback khi thứ tự gõ sai hoặc âm tiết vô nghĩa (không thuộc tiếng Việt)
    [<Theory>]
    [<InlineData("hnoa", "hnoa")>] 
    [<InlineData("tsaon", "tsaon")>] 
    let ``3. Irregular typing acts as fallback or parses accordingly`` (input: string, expected: string) =
        Assert.Equal(expected, typeWord input)