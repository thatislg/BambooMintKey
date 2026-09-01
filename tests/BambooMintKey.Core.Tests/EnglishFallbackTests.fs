namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

module EnglishFallbackTests =

    [<Theory>]
    [<InlineData("code", "code")>]
    [<InlineData("start", "start")>]
    [<InlineData("filter", "filter")>]
    [<InlineData("print", "print")>]
    [<InlineData("system", "system")>]
    [<InlineData("class", "class")>]
    [<InlineData("struct", "struct")>]
    let ``English words should not trigger invalid Vietnamese transformations`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)