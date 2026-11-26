// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Dictionary

/// <summary>
/// Máy trạng thái thuần (pure) cho chế độ gõ ký hiệu &amp; icon bằng từ khóa "::" kèm Inline Preview.
/// Thiết kế theo 013_01_Symbol_Shortcode_Sample_Design.md.
/// </summary>
module SymbolEngine =

    let private isLineBreakChar (c: char) : bool = c = '\n' || c = '\r'

    /// Các ký tự kết thúc mà sau đó chuỗi kích hoạt "::" được phép khởi động lại.
    let isTriggerBoundaryChar (c: char) : bool =
        Char.IsWhiteSpace c || c = '(' || c = '[' || c = '{' || c = '"' || c = '\''

    let private emptyWithBoundary (canStart: bool) : WordState =
        { WordState.Empty with CanStartSymbolTrigger = canStart }

    /// Cập nhật cờ cho phép kích hoạt "::" ở đầu từ (dùng cho tầng nền tảng khi phím không bị nuốt).
    let withTriggerBoundary (canStart: bool) (state: WordState) : WordState =
        { state with CanStartSymbolTrigger = canStart }

    let private withSymbolState (symbolState: SymbolModeState) (displayText: string) : WordState =
        { WordState.Empty with
            TransformedText = displayText
            SymbolState = symbolState
            CanStartSymbolTrigger = false }

    /// Dựng chuỗi hiển thị Inline Preview trong composition:
    /// - 0 ứng viên: "::before"
    /// - 1 ứng viên: "::vn 🇻🇳"
    /// - >1 ứng viên: "::mui → (1/7)"
    let formatPreview (session: SymbolSession) : string =
        match session.Candidates.Length with
        | 0 -> "::" + session.RawQuery
        | 1 -> "::" + session.RawQuery + " " + session.Candidates[0]
        | n ->
            "::" + session.RawQuery + " " + session.Candidates[session.SelectedIndex]
            + " (" + string (session.SelectedIndex + 1) + "/" + string n + ")"

    let private buildSession (rawQuery: string) : SymbolSession =
        let norm = SymbolDictionary.normalizeKeyword rawQuery
        let candidates = SymbolDictionary.lookup norm
        {
            RawQuery = rawQuery
            NormalizedQuery = norm
            Candidates = candidates
            SelectedIndex = 0
        }

    /// Bắt đầu trạng thái chờ dấu ':' thứ hai khi gặp ':' hợp lệ ở đầu từ.
    let enterPendingColon () : WordState * EngineAction =
        (withSymbolState SymbolModeState.PendingColon ":", EngineAction.UpdateComposition ":")

    /// Xử lý một phím khi engine đang ở trong SymbolModeState (khác Idle).
    let processKey (state: WordState) (input: KeyInput) : WordState * EngineAction =
        match state.SymbolState with
        | SymbolModeState.Idle ->
            (state, EngineAction.PassThrough)

        | SymbolModeState.PendingColon ->
            match input with
            | KeyInput.WordBreak ':' ->
                (withSymbolState SymbolModeState.TriggerReady "::", EngineAction.UpdateComposition "::")

            | KeyInput.Backspace ->
                (emptyWithBoundary true, EngineAction.UpdateComposition "")

            | KeyInput.Escape ->
                (emptyWithBoundary false, EngineAction.Commit ":")

            | KeyInput.Tab _ ->
                (emptyWithBoundary true, EngineAction.Commit ":\t")

            | KeyInput.WordBreak c ->
                let committed = if isLineBreakChar c then ":" else ":" + string c
                (emptyWithBoundary (isTriggerBoundaryChar c), EngineAction.Commit committed)

            | KeyInput.Char c ->
                (emptyWithBoundary false, EngineAction.Commit (":" + string c))

            | KeyInput.NonCharacter ->
                (emptyWithBoundary true, EngineAction.Commit ":")

        | SymbolModeState.TriggerReady ->
            match input with
            | KeyInput.Char c when Char.IsLetter c ->
                let session = buildSession (string c)
                let preview = formatPreview session
                (withSymbolState (SymbolModeState.Collecting session) preview, EngineAction.UpdateComposition preview)

            | KeyInput.Char c ->
                // Sau "::" không phải chữ cái (vd "::1" IPv6) -> chốt nguyên văn, thoát an toàn
                (emptyWithBoundary false, EngineAction.Commit ("::" + string c))

            | KeyInput.Backspace ->
                (withSymbolState SymbolModeState.PendingColon ":", EngineAction.UpdateComposition ":")

            | KeyInput.Escape ->
                (emptyWithBoundary false, EngineAction.Commit "::")

            | KeyInput.Tab _ ->
                (emptyWithBoundary true, EngineAction.Commit "::\t")

            | KeyInput.WordBreak c ->
                let committed = if isLineBreakChar c then "::" else "::" + string c
                (emptyWithBoundary (isTriggerBoundaryChar c), EngineAction.Commit committed)

            | KeyInput.NonCharacter ->
                (emptyWithBoundary true, EngineAction.Commit "::")

        | SymbolModeState.Collecting session ->
            match input with
            | KeyInput.Char c when Char.IsLetterOrDigit c ->
                let nextSession = buildSession (session.RawQuery + string c)
                let preview = formatPreview nextSession
                (withSymbolState (SymbolModeState.Collecting nextSession) preview, EngineAction.UpdateComposition preview)

            | KeyInput.Char c ->
                (emptyWithBoundary false, EngineAction.Commit ("::" + session.RawQuery + string c))

            | KeyInput.Backspace ->
                if session.RawQuery.Length <= 1 then
                    (withSymbolState SymbolModeState.TriggerReady "::", EngineAction.UpdateComposition "::")
                else
                    let trimmed = session.RawQuery.Substring(0, session.RawQuery.Length - 1)
                    let nextSession = buildSession trimmed
                    let preview = formatPreview nextSession
                    (withSymbolState (SymbolModeState.Collecting nextSession) preview, EngineAction.UpdateComposition preview)

            | KeyInput.Tab isShift ->
                let n = session.Candidates.Length
                if n > 0 then
                    let nextIdx =
                        if isShift then (session.SelectedIndex - 1 + n) % n
                        else (session.SelectedIndex + 1) % n
                    let nextSession = { session with SelectedIndex = nextIdx }
                    let preview = formatPreview nextSession
                    (withSymbolState (SymbolModeState.Collecting nextSession) preview, EngineAction.UpdateComposition preview)
                else
                    (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery + "\t"))

            | KeyInput.WordBreak c ->
                if (c = ' ' || isLineBreakChar c) && session.Candidates.Length > 0 then
                    // Chốt ứng viên đang chọn và tiêu thụ phím Space/Enter (không thêm khoảng trắng thừa)
                    let chosen = session.Candidates[session.SelectedIndex]
                    (emptyWithBoundary true, EngineAction.Commit chosen)
                else
                    let committed =
                        if isLineBreakChar c then "::" + session.RawQuery
                        else "::" + session.RawQuery + string c
                    (emptyWithBoundary (isTriggerBoundaryChar c), EngineAction.Commit committed)

            | KeyInput.Escape ->
                (emptyWithBoundary false, EngineAction.Commit ("::" + session.RawQuery))

            | KeyInput.NonCharacter ->
                (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery))
