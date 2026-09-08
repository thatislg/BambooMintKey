// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types

module TelexEngine =

    let private reconstructSyllableText (s: Syllable) : string =
        let normVowel = ToneRules.normalizeVowels s.VowelNucleus s.InitialConsonant s.FinalConsonant
        s.InitialConsonant + normVowel + s.FinalConsonant

    let private handleCharInput (c: char) (state: WordState) (config: EngineConfig) : WordState * EngineAction =
        let lowerChar = Char.ToLowerInvariant c
        let newRaw = state.RawKeys @ [ c ]
        let rawString = String(Array.ofList newRaw)
        let detectedCase = WordBuffer.detectCase newRaw

        let isToneKey = ToneRules.keyToTone c |> Option.isSome
        let isModifierKey = "aweod".Contains lowerChar

        // 1. Kiểm tra lặp phím dấu thanh (Undo Tone: má + s -> mass, dà + f -> daff)
        let isUndoTone =
            config.AllowRepeatKeyUndo && isToneKey && state.Syllable.IsSome &&
            state.Syllable.Value.Tone <> Tone.None &&
            ToneRules.keyToTone c = Some state.Syllable.Value.Tone

        // 2. Kiểm tra lặp phím modifier (Undo Modifier: xâ + a -> xaaa, dê + e -> deee, đ + d -> ddd)
        let isUndoModifier =
            config.AllowRepeatKeyUndo && isModifierKey &&
            not (rawString.ToLowerInvariant().Contains "uwow") &&
            (
                (lowerChar = 'd' && state.TransformedText.ToLowerInvariant().Contains "đ") ||
                (lowerChar = 'a' && (state.TransformedText.ToLowerInvariant().Contains "â" || state.TransformedText.ToLowerInvariant().Contains "ă")) ||
                (lowerChar = 'e' && state.TransformedText.ToLowerInvariant().Contains "ê") ||
                (lowerChar = 'o' && (state.TransformedText.ToLowerInvariant().Contains "ô" || state.TransformedText.ToLowerInvariant().Contains "ơ")) ||
                (lowerChar = 'w' && not (state.TransformedText.ToLowerInvariant().Contains "o") && state.TransformedText.ToLowerInvariant().Contains "ư")
            )

        if isUndoTone then
            // Lặp lại phím dấu thanh -> Hủy dấu, khôi phục toàn bộ chuỗi phím thô
            let formatted = WordBuffer.applyCase detectedCase rawString
            let newState = {
                RawKeys = newRaw
                TransformedText = formatted
                Syllable = None
                Case = detectedCase
                IsInvalidVietnamese = true
            }
            (newState, EngineAction.UpdateComposition formatted)

        elif isUndoModifier then
            // Lặp lại phím modifier -> Hủy biến đổi, khôi phục chuỗi thô của lần lặp
            let formatted = WordBuffer.applyCase detectedCase rawString
            let newState = {
                RawKeys = newRaw
                TransformedText = formatted
                Syllable = None
                Case = detectedCase
                IsInvalidVietnamese = true
            }
            (newState, EngineAction.UpdateComposition formatted)

        else
            // Tuyến bảo vệ từ tiếng Anh (English Word Protection):
            // Nếu chuỗi gõ thô có xác suất cao là từ tiếng Anh (như 'core', 'more', 'post', 'turn', 'files'...),
            // lập tức khôi phục về chuỗi thô để tránh bị cơ chế Free Tone hoặc biến đổi Telex nuốt nhầm phím.
            if config.AutoRestoreEnglishWords && EnglishProtection.isLikelyEnglishWord newRaw then
                let cleaned = EnglishProtection.cleanEnglishWordText rawString
                let formatted = WordBuffer.applyCase detectedCase cleaned
                let newState = {
                    RawKeys = newRaw
                    TransformedText = formatted
                    Syllable = None
                    Case = detectedCase
                    IsInvalidVietnamese = true
                }
                (newState, EngineAction.UpdateComposition formatted)
            else
                // 3. Cơ chế Bỏ dấu tự do (Free Tone Placement)
                // Khi bật cấu hình AllowFreeTonePlacement, kiểm tra xem có phím dấu thanh nằm ở vị trí tự do hay không
                // (ví dụ: gõ dấu ở cuối từ 'phari', 'hoacs', hoặc gõ dấu trước nguyên âm sau 'phar' + 'i').
                let freeToneOpt =
                    if config.AllowFreeTonePlacement then
                        FreeTonePlacement.tryNormalizeFreeTone newRaw config.ToneStyle config.AllowRepeatKeyUndo
                    else
                        None

                match freeToneOpt with
                | Some (normalizedSyllable, wordCase) ->
                    // Bỏ dấu tự do thành công: tái tạo chuỗi âm tiết và áp dụng định dạng hoa/thường chính xác
                    let reconstructed = reconstructSyllableText normalizedSyllable
                    let formatted = WordBuffer.applyCase wordCase reconstructed
                    let newState = {
                        RawKeys = newRaw
                        TransformedText = formatted
                        Syllable = Some normalizedSyllable
                        Case = wordCase
                        IsInvalidVietnamese = false
                    }
                    (newState, EngineAction.UpdateComposition formatted)

                | None ->
                    // 4. Thử áp dụng biến đổi lên State Syllable hiện có (Luồng gia tăng Telex chuẩn)
                    let modifiedSyllableOpt =
                        match state.Syllable with
                        | Some currentSyl ->
                            match ToneRules.keyToTone c with
                            | Some tone when not (String.IsNullOrEmpty currentSyl.VowelNucleus) ->
                                Some (ToneRules.applyTone tone config.ToneStyle currentSyl)
                            | _ ->
                                match ModifierRules.applyModifier c currentSyl with
                                | Some s -> Some s
                                | None ->
                                    // Thử ghép phụ âm cuối vào Syllable hiện có
                                    let f = currentSyl.FinalConsonant.ToLowerInvariant()
                                    let candFinalOpt =
                                        if String.IsNullOrEmpty f && "cmnpt".Contains(string lowerChar) then
                                            Some (string c)
                                        elif f = "n" && lowerChar = 'g' then Some "ng"
                                        elif f = "c" && lowerChar = 'h' then Some "ch"
                                        elif f = "n" && lowerChar = 'h' then Some "nh"
                                        else None
                                    match candFinalOpt with
                                    | Some newF ->
                                        let newSyl = { currentSyl with FinalConsonant = newF }
                                        Some (ToneRules.applyTone currentSyl.Tone config.ToneStyle newSyl)
                                    | None ->
                                        // Thử mở rộng âm tiết: từ trạng thái chỉ có phụ âm đầu (VD: 'Đ' sau 'Dd' + 'i' -> 'Đi')
                                        // hoặc mở rộng cụm nguyên âm hợp lệ (VD: 'Ư' sau 'Uw' + 'u' -> 'Ưu', 'Ơ' sau 'Ow' + 'i' -> 'Ơi')
                                        match ModifierRules.tryExtendSyllableWithChar c currentSyl with
                                        | Some extSyl ->
                                            Some (ToneRules.applyTone currentSyl.Tone config.ToneStyle extSyl)
                                        | None -> None
                        | None ->
                            if rawString.ToLowerInvariant() = "dd" then
                                Some {
                                    InitialConsonant = if Char.IsUpper(newRaw[0]) then "Đ" else "đ"
                                    VowelNucleus = ""
                                    FinalConsonant = ""
                                    Tone = Tone.None
                                    Modifiers = [ ('d', Modifier.DBar) ]
                                }
                            else None

                    match modifiedSyllableOpt with
                    | Some updatedSyllable ->
                        let reconstructed = reconstructSyllableText updatedSyllable
                        let formatted = WordBuffer.applyCase detectedCase reconstructed
                        let newState = {
                            RawKeys = newRaw
                            TransformedText = formatted
                            Syllable = Some updatedSyllable
                            Case = detectedCase
                            IsInvalidVietnamese = false
                        }
                        (newState, EngineAction.UpdateComposition formatted)

                    | None ->
                        // 5. Parse chuỗi thô để xây dựng âm tiết mới (Luồng toàn chuỗi)
                        match SyllableParser.parse rawString with
                        | Some parsedSyllable ->
                            let reconstructed = reconstructSyllableText parsedSyllable
                            let formatted = WordBuffer.applyCase detectedCase reconstructed
                            let newState = {
                                RawKeys = newRaw
                                TransformedText = formatted
                                Syllable = Some parsedSyllable
                                Case = detectedCase
                                IsInvalidVietnamese = false
                            }
                            (newState, EngineAction.UpdateComposition formatted)
                        | None ->
                            // 6. Fallback tiếng Anh (Không nhận diện được âm tiết tiếng Việt -> giữ nguyên chuỗi phím thô)
                            let fallbackText = WordBuffer.applyCase detectedCase rawString
                            let newState = {
                                RawKeys = newRaw
                                TransformedText = fallbackText
                                Syllable = None
                                Case = detectedCase
                                IsInvalidVietnamese = true
                            }
                            (newState, EngineAction.UpdateComposition fallbackText)

    let processKey (state: WordState) (input: KeyInput) (config: EngineConfig) : WordState * EngineAction =
        if not config.IsEnabled then
            match input with
            | KeyInput.Char c ->
                let nextRaw = state.RawKeys @ [ c ]
                let text = String(Array.ofList nextRaw)
                let newState = {
                    RawKeys = nextRaw
                    TransformedText = text
                    Syllable = None
                    Case = LetterCase.Lower
                    IsInvalidVietnamese = true
                }
                (newState, EngineAction.PassThrough)
            | KeyInput.Backspace ->
                if state.RawKeys.IsEmpty then (WordState.Empty, EngineAction.PassThrough)
                else
                    let nextRaw = state.RawKeys |> List.take (state.RawKeys.Length - 1)
                    let text = String(Array.ofList nextRaw)
                    let newState = {
                        RawKeys = nextRaw
                        TransformedText = text
                        Syllable = None
                        Case = LetterCase.Lower
                        IsInvalidVietnamese = true
                    }
                    (newState, EngineAction.PassThrough)
            | KeyInput.WordBreak breakChar ->
                let finalWord = state.TransformedText + string breakChar
                (WordState.Empty, EngineAction.Commit finalWord)
            | KeyInput.NonCharacter ->
                (state, EngineAction.PassThrough)
        else
            match input with
            | KeyInput.Char c ->
                handleCharInput c state config

            | KeyInput.Backspace ->
                if state.RawKeys.IsEmpty then
                    (WordState.Empty, EngineAction.PassThrough)
                else
                    let newRaw = state.RawKeys |> List.take (state.RawKeys.Length - 1)
                    if newRaw.IsEmpty then
                        (WordState.Empty, EngineAction.UpdateComposition "")
                    else
                        let mutable replayState = WordState.Empty
                        for k in newRaw do
                            let st, _ = handleCharInput k replayState config
                            replayState <- st
                        (replayState, EngineAction.UpdateComposition replayState.TransformedText)

            | KeyInput.WordBreak breakChar ->
                if state.RawKeys.IsEmpty then
                    (WordState.Empty, EngineAction.PassThrough)
                else
                    // Kiểm tra cơ chế tự động khôi phục từ tiếng Anh khi chốt từ (AutoRestoreEnglishWords)
                    let finalWordText =
                        if config.AutoRestoreEnglishWords && EnglishProtection.isLikelyEnglishWord state.RawKeys then
                            let rawStr = String(Array.ofList state.RawKeys)
                            let cleaned = EnglishProtection.cleanEnglishWordText rawStr
                            WordBuffer.applyCase state.Case cleaned
                        else
                            state.TransformedText

                    let finalWord = finalWordText + string breakChar
                    (WordState.Empty, EngineAction.Commit finalWord)

            | KeyInput.NonCharacter ->
                (state, EngineAction.PassThrough)