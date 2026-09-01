namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

module RestoreAndUndoTests =

    let typeWord (keys: string) : WordState =
        let mutable state = WordState.Empty
        for c in keys do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        state

    // 1. Phục hồi nguyên thể (Undo) khi lặp phím modifier / tone
    // - Engine BambooMintKey.Core trả về raw string hiện đang gõ
    [<Theory>]
    [<InlineData("mass", "mass")>]
    [<InlineData("toff", "toff")>]
    [<InlineData("luxx", "luxx")>]
    [<InlineData("dajj", "dajj")>]
    let ``1. Repeating tone key should restore raw text correctly (based on engine rule)`` (input: string, expected: string) =
        let state = typeWord input
        Assert.Equal(expected, state.TransformedText)

    // Xử lý các modifier a, e, o, d, w lặp lại (hủy dấu mũ, móc)
    [<Theory>]
    [<InlineData("ddd", "ddd")>] 
    [<InlineData("xaaa", "xaaa")>] 
    [<InlineData("deee", "deee")>]
    [<InlineData("cooo", "cooo")>]
    [<InlineData("awww", "awww")>] 
    let ``2. Repeating modifier key undoes the format back to raw string stream`` (input: string, expected: string) =
        let state = typeWord input
        Assert.Equal(expected, state.TransformedText)

    // 2. Bảo toàn chữ HOA, chữ thường (Case Preservation)
    [<Theory>]
    [<InlineData("VIEETJ", "VIỆT")>]
    [<InlineData("Vieetj", "Việt")>]
    [<InlineData("vieetj", "việt")>]
    [<InlineData("vIeeTj", "vIệT")>]
    [<InlineData("HOANS", "HOÁN")>]
    let ``3.1 Engine should preserve original casing format with valid inputs`` (input: string, expected: string) =
        let state = typeWord input
        Assert.Equal(expected, state.TransformedText)

    // 3. Tiến trình xoá phím (Backspace)
    [<Fact>]
    let ``4. Pressing backspace should step back gradually mapping to character states`` () =
        let config = EngineConfig.Default
        let mutable state = WordState.Empty

        // Từng bước của: "v" -> "i" -> "ê" ("e" + "e") -> "t" -> "nặng" ("j") = "việt"
        for c in "vieetj" do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        
        // Backspace xóa "j" (dấu nặng) -> còn "vieet" = "viêt"
        let backState1, _ = TelexEngine.processKey state KeyInput.Backspace config
        Assert.Equal("viêt", backState1.TransformedText)

        // Xóa tiếp "t" -> còn "viee" = "viê"
        let backState2, _ = TelexEngine.processKey backState1 KeyInput.Backspace config
        Assert.Equal("viê", backState2.TransformedText)

        // Xóa tiếp "e" -> còn "vie" = "vie"
        let backState3, _ = TelexEngine.processKey backState2 KeyInput.Backspace config
        Assert.Equal("vie", backState3.TransformedText)

        // Xóa nốt "e" -> còn "vi" = "vi"
        let backState4, _ = TelexEngine.processKey backState3 KeyInput.Backspace config
        Assert.Equal("vi", backState4.TransformedText)
        
        // Xóa "i" -> còn "v"
        let backState5, _ = TelexEngine.processKey backState4 KeyInput.Backspace config
        Assert.Equal("v", backState5.TransformedText)
