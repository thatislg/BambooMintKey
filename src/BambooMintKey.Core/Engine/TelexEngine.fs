namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Engine.ModifierRules
open BambooMintKey.Core.Engine.SyllableParser
open BambooMintKey.Core.Engine.ToneRules
open BambooMintKey.Core.Engine.WordBuffer

module TelexEngine =

    /// Tái tạo chuỗi văn bản từ cấu trúc Syllable
    let private reconstructSyllableText (s: Syllable) : string =
        s.InitialConsonant + s.VowelNucleus + s.FinalConsonant

    /// Xử lý phím thêm ký tự mới vào State
    let private handleCharInput (c: char) (state: WordState) (config: EngineConfig) : WordState * EngineAction =
        let newRaw = state.RawKeys @ [ c ]
        let rawString = String(Array.ofList newRaw)
        let detectedCase = WordBuffer.detectCase newRaw
        let lowerChar = Char.ToLowerInvariant c

        // 1. Kiểm tra lặp phím để khôi phục (Undo/Escape)
        let isToneKey = ToneRules.keyToTone c |> Option.isSome
        let isModifierKey = "aweod".Contains lowerChar

        let isUndoTone =
            config.AllowRepeatKeyUndo && isToneKey && state.Syllable.IsSome &&
            state.Syllable.Value.Tone <> Tone.None &&
            ToneRules.keyToTone c = Some state.Syllable.Value.Tone

        let isUndoModifier =
            config.AllowRepeatKeyUndo && isModifierKey && state.Syllable.IsSome &&
            match lowerChar with
            | 'a' -> state.TransformedText.Contains "â" || state.TransformedText.Contains "ă"
            | 'e' -> state.TransformedText.Contains "ê"
            | 'o' -> state.TransformedText.Contains "ô" || state.TransformedText.Contains "ơ"
            | 'w' -> state.TransformedText.Contains "ư" || state.TransformedText.Contains "ơ" || state.TransformedText.Contains "ă"
            | 'd' -> state.TransformedText.Contains "đ"
            | _ -> false

        if isUndoTone then
            let cleanSyllable = ToneRules.applyTone Tone.None config.ToneStyle state.Syllable.Value
            let reconstructed = reconstructSyllableText cleanSyllable + string c
            let formatted = WordBuffer.applyCase detectedCase reconstructed
            let newState = {
                RawKeys = newRaw
                TransformedText = formatted
                Syllable = SyllableParser.parse reconstructed
                Case = detectedCase
                IsInvalidVietnamese = false
            }
            (newState, EngineAction.UpdateComposition formatted)

        elif isUndoModifier then
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
            // 2. Thử phân tích cấu trúc ngữ pháp
            match SyllableParser.parse rawString with
            | Some parsedSyllable ->
                let updatedSyllable =
                    match ToneRules.keyToTone c with
                    | Some tone -> ToneRules.applyTone tone config.ToneStyle parsedSyllable
                    | None ->
                        match ModifierRules.applyModifier c parsedSyllable with
                        | Some modSyllable -> modSyllable
                        | None -> parsedSyllable

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
                // 3. Fallback tiếng Anh
                let fallbackText = WordBuffer.applyCase detectedCase rawString
                let newState = {
                    RawKeys = newRaw
                    TransformedText = fallbackText
                    Syllable = None
                    Case = detectedCase
                    IsInvalidVietnamese = true
                }
                (newState, EngineAction.UpdateComposition fallbackText)

    /// Hàm chuyển trạng thái chính của Telex Engine
    let processKey (state: WordState) (input: KeyInput) (config: EngineConfig) : WordState * EngineAction =
        if not config.IsEnabled then
            (WordState.Empty, EngineAction.PassThrough)
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
                        let rawString = String(Array.ofList newRaw)
                        let detectedCase = WordBuffer.detectCase newRaw
                        match SyllableParser.parse rawString with
                        | Some s ->
                            let reconstructed = reconstructSyllableText s
                            let formatted = WordBuffer.applyCase detectedCase reconstructed
                            let newState = {
                                RawKeys = newRaw
                                TransformedText = formatted
                                Syllable = Some s
                                Case = detectedCase
                                IsInvalidVietnamese = false
                            }
                            (newState, EngineAction.UpdateComposition formatted)
                        | None ->
                            let formatted = WordBuffer.applyCase detectedCase rawString
                            let newState = {
                                RawKeys = newRaw
                                TransformedText = formatted
                                Syllable = None
                                Case = detectedCase
                                IsInvalidVietnamese = true
                            }
                            (newState, EngineAction.UpdateComposition formatted)

            | KeyInput.WordBreak breakChar ->
                if state.RawKeys.IsEmpty then
                    (WordState.Empty, EngineAction.PassThrough)
                else
                    let finalWord = state.TransformedText + string breakChar
                    (WordState.Empty, EngineAction.Commit finalWord)

            | KeyInput.NonCharacter ->
                (state, EngineAction.PassThrough)