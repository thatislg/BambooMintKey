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

    /// Số lượng ứng viên tối đa hiển thị trên mỗi trang của cửa sổ popup dọc (phím chọn 1–7).
    let PageSize = 7

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

    /// Lấy thông tin trang ứng viên hiện tại (tối đa 7 mục) nếu đang ở Collecting và có ít nhất 1 ứng viên.
    let tryGetCandidatePage (state: WordState) : CandidatePage option =
        match state.SymbolState with
        | SymbolModeState.Collecting s when s.Candidates.Length > 0 ->
            let total = s.Candidates.Length
            let totalPages = (total + PageSize - 1) / PageSize
            let pageIdx = s.SelectedIndex / PageSize
            let pageStart = pageIdx * PageSize
            let pageCount = min PageSize (total - pageStart)
            let items = Array.sub s.Candidates pageStart pageCount
            Some {
                Items = items
                SelectedInPage = s.SelectedIndex - pageStart
                PageIndex = pageIdx
                TotalPages = totalPages
                TotalCount = total
                RawQuery = s.RawQuery
            }
        | _ -> None

    /// Chuỗi composition gọn ("::mui") dùng khi cửa sổ popup ứng viên đang hiển thị.
    let getCompactCompositionText (state: WordState) : string =
        match state.SymbolState with
        | SymbolModeState.Collecting s when s.Candidates.Length > 0 -> "::" + s.RawQuery
        | _ -> state.TransformedText

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

            | KeyInput.ArrowUp
            | KeyInput.ArrowDown
            | KeyInput.ArrowLeft
            | KeyInput.ArrowRight
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

            | KeyInput.ArrowUp
            | KeyInput.ArrowDown
            | KeyInput.ArrowLeft
            | KeyInput.ArrowRight
            | KeyInput.NonCharacter ->
                (emptyWithBoundary true, EngineAction.Commit "::")

        | SymbolModeState.Collecting session ->
            let moveSelection (nextIdx: int) =
                let nextSession = { session with SelectedIndex = nextIdx }
                let preview = formatPreview nextSession
                (withSymbolState (SymbolModeState.Collecting nextSession) preview, EngineAction.UpdateComposition preview)

            match input with
            | KeyInput.Char c when c >= '1' && c <= '7' && session.Candidates.Length > 0 ->
                // Chọn và chốt ngay ứng viên thứ (1..7) trên trang hiện tại
                let pageStart = (session.SelectedIndex / PageSize) * PageSize
                let targetIdx = pageStart + (int c - int '1')
                if targetIdx < session.Candidates.Length then
                    (emptyWithBoundary true, EngineAction.Commit session.Candidates[targetIdx])
                else
                    // Số vượt quá số mục trên trang hiện tại -> giữ nguyên phiên để tránh thoát nhầm
                    (state, EngineAction.UpdateComposition (formatPreview session))

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
                    moveSelection nextIdx
                else
                    (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery + "\t"))

            | KeyInput.ArrowDown ->
                let n = session.Candidates.Length
                if n > 0 then moveSelection ((session.SelectedIndex + 1) % n)
                else (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery))

            | KeyInput.ArrowUp ->
                let n = session.Candidates.Length
                if n > 0 then moveSelection ((session.SelectedIndex - 1 + n) % n)
                else (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery))

            | KeyInput.ArrowRight ->
                let n = session.Candidates.Length
                if n > 0 then
                    let totalPages = (n + PageSize - 1) / PageSize
                    if totalPages > 1 then
                        let nextPage = ((session.SelectedIndex / PageSize) + 1) % totalPages
                        moveSelection (nextPage * PageSize)
                    else
                        moveSelection ((session.SelectedIndex + 1) % n)
                else
                    (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery))

            | KeyInput.ArrowLeft ->
                let n = session.Candidates.Length
                if n > 0 then
                    let totalPages = (n + PageSize - 1) / PageSize
                    if totalPages > 1 then
                        let prevPage = ((session.SelectedIndex / PageSize) - 1 + totalPages) % totalPages
                        moveSelection (prevPage * PageSize)
                    else
                        moveSelection ((session.SelectedIndex - 1 + n) % n)
                else
                    (emptyWithBoundary true, EngineAction.Commit ("::" + session.RawQuery))

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
