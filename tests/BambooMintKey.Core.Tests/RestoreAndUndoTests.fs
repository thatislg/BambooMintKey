namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

module RestoreAndUndoTests =

    [<Theory>]
    [<InlineData("mass", "mas")>]
    [<InlineData("toff", "tof")>]
    [<InlineData("luxx", "lux")>]
    [<InlineData("xaaa", "xaa")>]
    [<InlineData("deee", "dee")>]
    [<InlineData("dđ", "dd")>]
    let ``Repeating tone or modifier key should restore raw text`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)

    [<Theory>]
    [<InlineData("VIETJ", "VIỆT")>]
    [<InlineData("Vietj", "Việt")>]
    [<InlineData("vietj", "việt")>]
    [<InlineData("vIeTj", "vIệT")>]
    let ``Engine should preserve original casing format`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)

    [<Fact>]
    let ``Pressing backspace should step back to previous state correctly`` () =
        let config = EngineConfig.Default
        let mutable state = WordState.Empty

        // Gõ "viet" -> "việt"
        for c in "vietj" do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        Assert.Equal("việt", state.TransformedText)

        // Nhấn Backspace -> xóa 'j', trở về "viê"
        let backState1, _ = TelexEngine.processKey state KeyInput.Backspace config
        Assert.Equal("viê", backState1.TransformedText)

        // Nhấn Backspace tiếp -> xóa 't', trở về "vi"
        let backState2, _ = TelexEngine.processKey backState1 KeyInput.Backspace config
        Assert.Equal("vi", backState2.TransformedText)