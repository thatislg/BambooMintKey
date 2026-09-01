namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

module EnglishFallbackTests =

    // 1. Nhận diện và bỏ qua các từ tiếng Anh (English Word Fallback)
    // - Engine không được vô tình biến đổi các chuỗi mang nghĩa tiếng Anh (không khớp ngữ pháp tiếng Việt)
    [<Theory>]
    [<InlineData("code", "code")>]
    [<InlineData("start", "start")>]
    [<InlineData("filter", "filter")>]
    [<InlineData("print", "print")>]
    [<InlineData("system", "system")>]
    [<InlineData("class", "class")>]
    [<InlineData("struct", "struct")>]
    [<InlineData("interface", "interface")>]
    [<InlineData("object", "object")>]
    [<InlineData("password", "password")>]
    [<InlineData("email", "email")>]
    [<InlineData("script", "script")>]
    let ``1. English structural words should bypass typing logic cleanly`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)

    // 2. Tắt bộ gõ qua cấu hình EngineConfig (Disabled Mode)
    // - Khi IsEnabled = false, không bất kỳ biến đổi nào diễn ra.
    [<Theory>]
    [<InlineData("vietj", "vietj")>]
    [<InlineData("truwowngf", "truwowngf")>]
    [<InlineData("hoas", "hoas")>]
    let ``2. Disabled Engine treats all input as pass-through characters`` (input: string, expected: string) =
        let config = { EngineConfig.Default with IsEnabled = false }
        let mutable state = WordState.Empty
        for c in input do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        Assert.Equal(expected, state.TransformedText)