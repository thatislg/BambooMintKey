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

    [<Theory>]
    [<InlineData("hoas", "hóa")>]
    [<InlineData("hoaf", "hòa")>]
    [<InlineData("thuys", "thúy")>]
    [<InlineData("xoef", "xòe")>]
    let ``Modern tone style should place tone on second vowel for open pairs`` (input: string, expected: string) =
        let result = typeWordWithStyle input TonePlacementStyle.Modern
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("hoas", "hoá")>]
    [<InlineData("hoaf", "hoà")>]
    [<InlineData("thuys", "thuý")>]
    [<InlineData("xoef", "xoè")>]
    let ``Traditional tone style should place tone on first vowel for open pairs`` (input: string, expected: string) =
        let result = typeWordWithStyle input TonePlacementStyle.Traditional
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("hoans", "hoán")>]
    [<InlineData("thuyets", "thuyết")>]
    [<InlineData("muowns", "mượn")>]
    [<InlineData("tieengs", "tiếng")>]
    let ``Both styles should place tone before final consonant consistently`` (input: string, expected: string) =
        let modernResult = typeWordWithStyle input TonePlacementStyle.Modern
        let traditionalResult = typeWordWithStyle input TonePlacementStyle.Traditional
        Assert.Equal(expected, modernResult)
        Assert.Equal(expected, traditionalResult)