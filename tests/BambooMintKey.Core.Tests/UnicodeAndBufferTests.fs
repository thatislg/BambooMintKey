// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Engine
open BambooMintKey.Core.Domain.Types
open Xunit

module UnicodeAndBufferTests =

    // 1. Nhận diện định dạng chữ HOA, chữ thường (Letter Case Detection)
    // - LowerCase: toàn bộ viết thường ("viet")
    // - TitleCase: chữ đầu viết HOA ("Viet")
    // - UpperCase: toàn bộ trường HOA ("VIET")
    // - MixedCase: lộn xộn ("vIeT")
    // wait Data can't be represented easily with objects for Mixed, so we skip parameterized array Mixed test
    [<Theory>]
    [<InlineData("viet", 1)>] // LetterCase.Lower = 1 (we will map it inside test)
    [<InlineData("Viet", 2)>] // LetterCase.Title = 2
    [<InlineData("VIET", 3)>] // LetterCase.Upper = 3
    [<InlineData("V", 3)>]    // 1 char Upper is Upper
    [<InlineData("v", 1)>]
    let ``1. Detecting exact capitalization variations correctly`` (input: string, expectedInt: int) =
        let expectedCase = 
            match expectedInt with
            | 1 -> LetterCase.Lower
            | 2 -> LetterCase.Title
            | 3 -> LetterCase.Upper
            | _ -> LetterCase.Lower
        let chars = input.ToCharArray() |> Array.toList
        let detected = WordBuffer.detectCase chars
        Assert.Equal(expectedCase, detected)

    [<Fact>]
    let ``1.1 Detecting MixedCase accurately`` () =
        let chars = "vIeT".ToCharArray() |> Array.toList
        let detected = WordBuffer.detectCase chars
        Assert.Equal(LetterCase.Mixed [false; true; false; true], detected)

    // 2. Chuyển đổi ký tự thành đúng Case gốc ban đầu (Case Consistency Preserved)
    [<Theory>]
    [<InlineData(1, "việt", "việt")>]
    [<InlineData(2, "việt", "Việt")>]
    [<InlineData(3, "việt", "VIỆT")>]
    let ``2. Format casing is restored to detected target safely`` (casingInt: int, transformedStr: string, expectedOut: string) =
        let casing = 
            match casingInt with
            | 1 -> LetterCase.Lower
            | 2 -> LetterCase.Title
            | 3 -> LetterCase.Upper
            | _ -> LetterCase.Lower
            
        let finalOut = WordBuffer.applyCase casing transformedStr
        Assert.Equal(expectedOut, finalOut)

    [<Fact>]
    let ``2.1 Format casing is restored to detected MixedCase safely`` () =
        let finalOut = WordBuffer.applyCase (LetterCase.Mixed [false; true; false; true]) "việt"
        Assert.Equal("vIệT", finalOut)