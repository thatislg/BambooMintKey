// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
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
    // - Lặp phím dấu liền kề rút gọn về 1 ký tự thô (ss -> s, xx -> x, rr -> r)
    [<Theory>]
    [<InlineData("mass", "mas")>]    // Ưu tiên hoàn dấu: lặp 'ss' hủy dấu sắc, rút về 'mas' (muốn gõ từ Anh 'mass' -> gõ 'masss')
    [<InlineData("toff", "tof")>]
    [<InlineData("luxx", "lux")>]
    [<InlineData("dajj", "daj")>]
    [<InlineData("goxx", "gox")>]       // Issue 012: lặp x (ngã) rút về 1 chữ x
    [<InlineData("horr", "hor")>]       // Issue 012: lặp r (hỏi) rút về 1 chữ r
    let ``1. Repeating tone key should restore raw text correctly (based on engine rule)`` (input: string, expected: string) =
        let state = typeWord input
        Assert.Equal(expected, state.TransformedText)

    // Xử lý các modifier a, e, o, d, w lặp lại (hủy dấu mũ, móc)
    [<Theory>]
    [<InlineData("ddd", "dd")>] 
    [<InlineData("xaaa", "xaa")>] 
    [<InlineData("deee", "dee")>]
    [<InlineData("cooo", "coo")>]
    [<InlineData("awww", "aww")>]    // aw -> ă, lặp w rút về aw, w thứ 3 lại thành aw+w
    [<InlineData("aww", "aw")>]      // aw -> ă, lặp w rút về aw
    [<InlineData("exee", "exe")>]    // Issue 014/015: ễ (ê + ngã) lặp 'e' hủy mũ ê -> exe
    [<InlineData("xeee", "xee")>]    // xê lặp 'e' hủy mũ ê -> xee
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
