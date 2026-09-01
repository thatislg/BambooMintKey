namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Engine
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit

module SimpleTelexTests =

    /// Helper mô phỏng quá trình gõ chuỗi phím tuần tự
    let typeWord (keys: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in keys do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

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
    let ``Telex basic transformations should match expected result`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)