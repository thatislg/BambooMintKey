// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Dictionary

module TelexEngine =

    let private reconstructSyllableText (s: Syllable) : string =
        let normVowel = ToneRules.normalizeVowels s.VowelNucleus s.InitialConsonant s.FinalConsonant
        s.InitialConsonant + normVowel + s.FinalConsonant

    /// Quyết định có hoàn tác về chuỗi thô tiếng Anh hay không:
    /// chỉ khi âm tiết đã đủ dài (hoàn chỉnh), KHÔNG hợp lệ tiếng Việt, VÀ chuỗi thô là từ tiếng Anh.
    let private shouldBacktrackEnglish (viText: string) (rawString: string) (config: EngineConfig) : bool =
        viText.Length >= 3 &&
        config.EnableVietnameseDictionary && config.EnableEnglishBacktracking &&
        not (DictionaryProvider.Default.IsValidVietnameseSyllable (viText.ToLowerInvariant())) &&
        EnglishProtection.isKnownEnglishWord (rawString.ToLowerInvariant())

    /// Tạo kết quả composition từ một âm tiết, kèm thẩm định On-the-fly & backtrack tiếng Anh.
    let private makeComposition (syllable: Syllable option) (case: LetterCase) (rawKeys: char list) (rawString: string) (config: EngineConfig) : WordState * EngineAction =
        match syllable with
        | Some syl ->
            let reconstructed = reconstructSyllableText syl
            let backtrack = shouldBacktrackEnglish reconstructed rawString config
            let finalText = if backtrack then rawString else reconstructed
            let formatted = WordBuffer.applyCase case finalText
            let newState = {
                RawKeys = rawKeys
                TransformedText = formatted
                // Giữ lại Syllable kể cả khi backtrack tiếng Anh: ưu tiên tiếng Việt trước,
                // cho phép phím modifier/tone tiếp theo (vd hopwj -> hợp) tiếp tục biến đổi
                // thay vì mất context (Syllable=None) giữa chừng.
                Syllable = Some syl
                Case = case
                IsInvalidVietnamese = backtrack
            }
            (newState, EngineAction.UpdateComposition formatted)
        | None ->
            let fallbackText = WordBuffer.applyCase case rawString
            let newState = {
                RawKeys = rawKeys
                TransformedText = fallbackText
                Syllable = None
                Case = case
                IsInvalidVietnamese = true
            }
            (newState, EngineAction.UpdateComposition fallbackText)

    let private handleCharInput (c: char) (state: WordState) (config: EngineConfig) : WordState * EngineAction =
        let lowerChar = Char.ToLowerInvariant c
        let newRaw = state.RawKeys @ [ c ]
        let rawString = String(Array.ofList newRaw)
        let detectedCase = WordBuffer.detectCase newRaw

        let isToneKey = ToneRules.keyToTone c |> Option.isSome
        let isModifierKey = "aweod".Contains lowerChar

        // Phím lặp liền kề (consecutive): phím trước đó chính là phím hiện tại (vd 'xx', 'rr', 'ss').
        // Dùng để phân biệt "gõ lặp để hủy dấu" (liền kề) với ký tự lặp không liền kề (vd 'tests' có 's' cách bởi 't').
        let isConsecutiveRepeat =
            state.RawKeys
            |> List.tryLast
            |> Option.exists (fun k -> Char.ToLowerInvariant k = lowerChar)

        // Phím 'w' đứng đầu từ (AllowLeadingWAsU): 'w' đầu -> 'ư' = 'u' + horn.
        // Khi hủy (gõ 'w' lần nữa) phải trả về 'u' thay vì chỉ bỏ phím 'w'.
        let isLeadingW =
            config.AllowLeadingWAsU &&
            (not state.RawKeys.IsEmpty) &&
            Char.ToLowerInvariant state.RawKeys.Head = 'w'


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
                (lowerChar = 'w' && not (state.TransformedText.ToLowerInvariant().Contains "o") &&
                    (state.TransformedText.ToLowerInvariant().Contains "ă" ||
                     ((state.TransformedText.ToLowerInvariant().Contains "ư") <> (state.TransformedText.ToLowerInvariant().Contains "ơ"))))
            )

        // 2b. Ngoại lệ chính tả "gì": phụ âm "gi" + phím dấu thanh (f/s/r/x/j)
        // -> "g" + "ì/í/ỉ/ĩ/ị". Đây là ngoại lệ duy nhất mà 'g' đi với 'i' (thay vì 'gh').
        let isGiToneException =
            isToneKey && newRaw.Length = 3 &&
            Char.ToLowerInvariant newRaw[0] = 'g' && Char.ToLowerInvariant newRaw[1] = 'i'

        if isUndoTone then
            // Lặp phím dấu thanh -> hủy dấu. Ưu tiên hoàn dấu trước (ưu tiên repeat-key undo):
            // lặp liền kề luôn rút về 1 ký tự thô (goxx -> gox, horr -> hor, mass -> mas).
            // Muốn gõ từ tiếng Anh 'mass' thì phải gõ 'masss'.
            let resultKeys = if isConsecutiveRepeat then state.RawKeys else newRaw
            let resultString = String(Array.ofList resultKeys)
            let formatted = WordBuffer.applyCase detectedCase resultString
            let newState = {
                RawKeys = resultKeys
                TransformedText = formatted
                Syllable = None
                Case = detectedCase
                IsInvalidVietnamese = true
            }
            (newState, EngineAction.UpdateComposition formatted)

        elif isUndoModifier then
            // Lặp phím modifier -> hủy biến đổi. Ưu tiên hoàn dấu trước: lặp liền kề luôn rút về 1 ký tự thô (ddd -> dd).
            // Ngoại lệ phím 'w' đứng đầu: 'ư' được sinh từ 'w' (u + horn) nên hủy phải trả về 'u' + 'w'
            // (Ww -> Uw, wiw -> uiw) thay vì chỉ rút về 'w' như lặp liền kề thông thường.
            let resultKeys =
                if lowerChar = 'w' && isLeadingW then
                    let baseU = if Char.IsUpper state.RawKeys.Head then 'U' else 'u'
                    let newW = if Char.IsUpper c then 'W' else 'w'
                    baseU :: (state.RawKeys.Tail @ [ newW ])
                elif isConsecutiveRepeat then state.RawKeys
                else newRaw
            let resultString = String(Array.ofList resultKeys)
            let formatted = WordBuffer.applyCase detectedCase resultString
            let newState = {
                RawKeys = resultKeys
                TransformedText = formatted
                Syllable = None
                Case = detectedCase
                IsInvalidVietnamese = true
            }
            (newState, EngineAction.UpdateComposition formatted)

        elif isGiToneException then
            // Ngoại lệ "gì": gif -> gì, gir -> gỉ, gis -> gí (g + nguyên âm i có dấu)
            let tone = ToneRules.keyToTone c |> Option.defaultValue Tone.None
            let init = if Char.IsUpper newRaw[0] then "G" else "g"
            let vowel = if Char.IsUpper newRaw[1] then "I" else "i"
            let syl = { InitialConsonant = init; VowelNucleus = vowel; FinalConsonant = ""; Tone = Tone.None; Modifiers = [] }
            let toned = ToneRules.applyTone tone config.ToneStyle syl
            let reconstructed = reconstructSyllableText toned
            let formatted = WordBuffer.applyCase detectedCase reconstructed
            let newState = {
                RawKeys = newRaw
                TransformedText = formatted
                Syllable = Some toned
                Case = detectedCase
                IsInvalidVietnamese = false
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
                // Bỏ dấu tự do thành công: tái tạo chuỗi âm tiết + thẩm định On-the-fly + backtrack English
                makeComposition (Some normalizedSyllable) wordCase newRaw rawString config

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
                        elif config.AllowLeadingWAsU && lowerChar = 'w' && state.RawKeys.IsEmpty then
                            // Phím 'w' đứng đầu từ -> nguyên âm 'ư' (khi bật AllowLeadingWAsU).
                            // w -> ư, rồi wa -> ưa, wu -> ưu, wi -> ưi, ws -> ứ.
                            Some {
                                InitialConsonant = ""
                                VowelNucleus = if Char.IsUpper c then "Ư" else "ư"
                                FinalConsonant = ""
                                Tone = Tone.None
                                Modifiers = [ ('w', Modifier.Horn) ]
                            }
                        else None

                match modifiedSyllableOpt with
                | Some updatedSyllable ->
                    makeComposition (Some updatedSyllable) detectedCase newRaw rawString config

                | None ->
                    // 5. Parse chuỗi thô để xây dựng âm tiết mới (Luồng toàn chuỗi)
                    match SyllableParser.parse rawString with
                    | Some parsedSyllable ->
                        makeComposition (Some parsedSyllable) detectedCase newRaw rawString config
                    | None ->
                        // 6. Fallback tiếng Anh (Không nhận diện được âm tiết tiếng Việt -> giữ nguyên chuỗi phím thô)
                        makeComposition None detectedCase newRaw rawString config

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