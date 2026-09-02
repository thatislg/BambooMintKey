// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open Xunit
open BambooMintKey.Core.Engine

module EngineLifecycleTests =

    // 1. Tổ hợp phím ngắt từ (Word Break Actions)
    // - Dấu cách, Enter, phím dấu câu (Non-characters) sẽ ngắt luồng từ, commit chuỗi hiện tại và reset state.
    [<Fact>]
    let ``1. Space or punctuation acts as a WordBreak to commit texts`` () =
        let config = EngineConfig.Default
        let mutable state = WordState.Empty
        
        // Gõ "hoas" -> "hóa"
        for c in "hoas" do
            let newState, _ = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        
        // Nhấn dấu cách
        let finalState, action = TelexEngine.processKey state (KeyInput.WordBreak ' ') config
        
        // state được reset về Empty
        Assert.Equal(WordState.Empty, finalState)
        // action yêu cầu commit toàn bộ từ kèm space
        match action with
        | EngineAction.Commit text -> 
            Assert.Equal("hóa ", text)
        | _ -> Assert.Fail("Expected action to be Commit")

    // 2. Không xử lý phím lạ nếu đang dùng
    [<Fact>]
    let ``2. Non-character actions bypass silently`` () =
        let config = EngineConfig.Default
        let initialState = WordState.Empty
        
        // Nhập phím điều hướng hoặc tổ hợp Ctrl+C -> Bypass
        let finalState, action = TelexEngine.processKey initialState KeyInput.NonCharacter config
        
        Assert.Equal(WordState.Empty, finalState)
        Assert.Equal(EngineAction.PassThrough, action)