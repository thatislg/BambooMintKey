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
            // 3. Thử áp dụng biến đổi lên State Syllable hiện có
            let modifiedSyllableOpt =
                match state.Syllable with
                | Some currentSyl ->
                    match ToneRules.keyToTone c with
                    | Some tone -> Some (ToneRules.applyTone tone config.ToneStyle currentSyl)
                    | None ->
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
                // 4. Parse chuỗi thô để xây dựng âm tiết mới
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
                    // 5. Fallback tiếng Anh
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
                    let finalWord = state.TransformedText + string breakChar
                    (WordState.Empty, EngineAction.Commit finalWord)

            | KeyInput.NonCharacter ->
                (state, EngineAction.PassThrough)