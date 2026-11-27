// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
module BambooMintKey.Core.Tests.SymbolEngineTests

open Xunit
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Engine

let private runInputs (config: EngineConfig) (inputs: KeyInput list) =
    let mutable state = WordState.Empty
    let mutable lastAction = EngineAction.PassThrough
    for input in inputs do
        let nextState, action = TelexEngine.processKey state input config
        state <- nextState
        lastAction <- action
    state, lastAction

let private chars (s: string) =
    s.ToCharArray() |> Array.toList |> List.map KeyInput.Char

[<Fact>]
let ``Inline preview shows index for multiple candidates and cycles with Tab and Shift+Tab`` () =
    let cfg = EngineConfig.Default

    // Gõ "::mui" -> "::mui → (1/7)"
    let s1, a1 =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "mui")
    Assert.Equal(EngineAction.UpdateComposition "::mui → (1/7)", a1)

    // Tab -> "::mui ← (2/7)"
    let s2, a2 = TelexEngine.processKey s1 (KeyInput.Tab false) cfg
    Assert.Equal(EngineAction.UpdateComposition "::mui ← (2/7)", a2)

    // Tab -> "::mui ↑ (3/7)"
    let s3, a3 = TelexEngine.processKey s2 (KeyInput.Tab false) cfg
    Assert.Equal(EngineAction.UpdateComposition "::mui ↑ (3/7)", a3)

    // Shift+Tab -> "::mui ← (2/7)"
    let s4, a4 = TelexEngine.processKey s3 (KeyInput.Tab true) cfg
    Assert.Equal(EngineAction.UpdateComposition "::mui ← (2/7)", a4)

    // Space chốt riêng ký tự "←" (không thêm khoảng trắng thừa)
    let s5, a5 = TelexEngine.processKey s4 (KeyInput.WordBreak ' ') cfg
    Assert.Equal(EngineAction.Commit "←", a5)
    Assert.Equal(SymbolModeState.Idle, s5.SymbolState)
    Assert.True(s5.CanStartSymbolTrigger)

[<Fact>]
let ``Single candidate preview omits index counter and commits on Enter`` () =
    let cfg = EngineConfig.Default
    let s1, a1 =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "vn")
    Assert.Equal(EngineAction.UpdateComposition "::vn 🇻🇳", a1)

    let s2, a2 = TelexEngine.processKey s1 (KeyInput.WordBreak '\r') cfg
    Assert.Equal(EngineAction.Commit "🇻🇳", a2)
    Assert.Equal(SymbolModeState.Idle, s2.SymbolState)

[<Fact>]
let ``Telex transformations are suspended inside symbol mode`` () =
    let cfg = EngineConfig.Default
    // "canhbao" có chứa 'a','o' nhưng không bị Telex can thiệp
    let s1, a1 =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "canhbao")
    Assert.Equal(EngineAction.UpdateComposition "::canhbao ⚠️ (1/3)", a1)

    let _, a2 = TelexEngine.processKey s1 (KeyInput.WordBreak ' ') cfg
    Assert.Equal(EngineAction.Commit "⚠️", a2)

[<Fact>]
let ``Escape cancels symbol mode and commits original raw query`` () =
    let cfg = EngineConfig.Default
    let s1, _ =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "Mui")
    let s2, a2 = TelexEngine.processKey s1 KeyInput.Escape cfg
    Assert.Equal(EngineAction.Commit "::Mui", a2)
    Assert.Equal(SymbolModeState.Idle, s2.SymbolState)

[<Fact>]
let ``Backspace steps back from query to TriggerReady to PendingColon to Idle`` () =
    let cfg = EngineConfig.Default
    let s1, _ =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "vn")

    let s2, a2 = TelexEngine.processKey s1 KeyInput.Backspace cfg
    // "::v" khớp cả "vn" (🇻🇳) và tiền tố "vuong" (1️⃣..0️⃣) -> 12 ứng viên
    Assert.Equal(EngineAction.UpdateComposition "::v 🇻🇳 (1/12)", a2)

    let s3, a3 = TelexEngine.processKey s2 KeyInput.Backspace cfg
    Assert.Equal(EngineAction.UpdateComposition "::", a3)
    Assert.Equal(SymbolModeState.TriggerReady, s3.SymbolState)

    let s4, a4 = TelexEngine.processKey s3 KeyInput.Backspace cfg
    Assert.Equal(EngineAction.UpdateComposition ":", a4)
    Assert.Equal(SymbolModeState.PendingColon, s4.SymbolState)

    let s5, a5 = TelexEngine.processKey s4 KeyInput.Backspace cfg
    Assert.Equal(EngineAction.UpdateComposition "", a5)
    Assert.Equal(SymbolModeState.Idle, s5.SymbolState)
    Assert.True(s5.CanStartSymbolTrigger)

[<Fact>]
let ``Unmatched CSS pseudo-element exits safely without losing characters`` () =
    let cfg = EngineConfig.Default
    let s1, a1 =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "before")
    Assert.Equal(EngineAction.UpdateComposition "::before", a1)

    let s2, a2 = TelexEngine.processKey s1 (KeyInput.WordBreak ' ') cfg
    Assert.Equal(EngineAction.Commit "::before ", a2)
    Assert.Equal(SymbolModeState.Idle, s2.SymbolState)

    let s3, _ =
        runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "before")
    let _, a4 = TelexEngine.processKey s3 (KeyInput.WordBreak '{') cfg
    Assert.Equal(EngineAction.Commit "::before{", a4)

[<Fact>]
let ``Protects std::vector, IPv6 ::1, and x :: xs from false triggers`` () =
    let cfg = EngineConfig.Default

    // std::vector -> dấu ':' đầu chốt "std:" với CanStartSymbolTrigger=false, dấu ':' thứ hai PassThrough
    let s1, a1 = runInputs cfg (chars "std" @ [ KeyInput.WordBreak ':' ])
    Assert.Equal(EngineAction.Commit "std:", a1)
    Assert.False(s1.CanStartSymbolTrigger)

    let s2, a2 = TelexEngine.processKey s1 (KeyInput.WordBreak ':') cfg
    Assert.Equal(EngineAction.PassThrough, a2)
    Assert.Equal(SymbolModeState.Idle, s2.SymbolState)

    // IPv6 "::1" -> sau "::" là số -> chốt nguyên văn "::1"
    let s3, a3 =
        runInputs cfg [ KeyInput.WordBreak ':'; KeyInput.WordBreak ':'; KeyInput.Char '1' ]
    Assert.Equal(EngineAction.Commit "::1", a3)
    Assert.Equal(SymbolModeState.Idle, s3.SymbolState)

    // "x :: xs" -> sau "::" là khoảng trắng -> chốt ":: "
    let s4, a4 =
        runInputs cfg [ KeyInput.WordBreak ':'; KeyInput.WordBreak ':'; KeyInput.WordBreak ' ' ]
    Assert.Equal(EngineAction.Commit ":: ", a4)
    Assert.Equal(SymbolModeState.Idle, s4.SymbolState)

[<Fact>]
let ``Activates after opening parenthesis`` () =
    let cfg = EngineConfig.Default
    let s0, a0 = TelexEngine.processKey WordState.Empty (KeyInput.WordBreak '(') cfg
    Assert.Equal(EngineAction.PassThrough, a0)
    Assert.True(s0.CanStartSymbolTrigger)

    let mutable state = s0
    let mutable action = EngineAction.PassThrough
    for inp in [ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "do" @ [ KeyInput.WordBreak ' ' ] do
        let st, act = TelexEngine.processKey state inp cfg
        state <- st
        action <- act
    Assert.Equal(EngineAction.Commit "°", action)

[<Fact>]
let ``Disabled EnableSymbolShortcode bypasses symbol mode completely`` () =
    let cfg = { EngineConfig.Default with EnableSymbolShortcode = false }
    let s1, a1 = TelexEngine.processKey WordState.Empty (KeyInput.WordBreak ':') cfg
    Assert.Equal(EngineAction.PassThrough, a1)
    Assert.Equal(SymbolModeState.Idle, s1.SymbolState)

[<Fact>]
let ``Arrow keys navigate vertically and paginate across 7-item pages`` () =
    let cfg = EngineConfig.Default
    // "::tron" khớp chính xác "tron" (13 mục) + tiền tố "tronden" (11 mục), giới hạn 20 mục -> 3 trang (7 + 7 + 6)
    let s1, _ = runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "tron")
    let p1 = SymbolEngine.tryGetCandidatePage s1
    Assert.True(p1.IsSome)
    Assert.Equal(7, p1.Value.Items.Length)
    Assert.Equal(3, p1.Value.TotalPages)
    Assert.Equal(0, p1.Value.PageIndex)
    Assert.Equal("①", p1.Value.Items[0])

    // ArrowDown -> chọn ② (index 1)
    let s2, _ = TelexEngine.processKey s1 KeyInput.ArrowDown cfg
    Assert.Equal(1, (SymbolEngine.tryGetCandidatePage s2).Value.SelectedInPage)

    // ArrowUp -> quay lại ① (index 0)
    let s3, _ = TelexEngine.processKey s2 KeyInput.ArrowUp cfg
    Assert.Equal(0, (SymbolEngine.tryGetCandidatePage s3).Value.SelectedInPage)

    // ArrowRight -> lật sang trang 2 (⑧ là mục đầu tiên)
    let s4, _ = TelexEngine.processKey s3 KeyInput.ArrowRight cfg
    let p2 = (SymbolEngine.tryGetCandidatePage s4).Value
    Assert.Equal(1, p2.PageIndex)
    Assert.Equal("⑧", p2.Items[0])

    // Phím số '2' trên trang 2 -> chốt ngay ⑨
    let s5, a5 = TelexEngine.processKey s4 (KeyInput.Char '2') cfg
    Assert.Equal(EngineAction.Commit "⑨", a5)
    Assert.Equal(SymbolModeState.Idle, s5.SymbolState)

[<Fact>]
let ``Digits 1-7 immediately commit corresponding candidate on current page`` () =
    let cfg = EngineConfig.Default
    let s1, _ = runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "vuong")
    let s2, a2 = TelexEngine.processKey s1 (KeyInput.Char '3') cfg
    Assert.Equal(EngineAction.Commit "3️⃣", a2)
    Assert.Equal(SymbolModeState.Idle, s2.SymbolState)

    // Bấm số vượt quá số mục trên trang (::vn chỉ có 1 mục, bấm '5') -> giữ nguyên phiên, không thoát nhầm
    let s3, _ = runInputs cfg ([ KeyInput.WordBreak ':'; KeyInput.WordBreak ':' ] @ chars "vn")
    let s4, a4 = TelexEngine.processKey s3 (KeyInput.Char '5') cfg
    Assert.Equal(EngineAction.UpdateComposition "::vn 🇻🇳", a4)
    Assert.NotEqual(SymbolModeState.Idle, s4.SymbolState)
