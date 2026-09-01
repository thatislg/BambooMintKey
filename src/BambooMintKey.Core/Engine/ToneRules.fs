namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables

module ToneRules =

    let keyToTone (c: char) : Tone option =
        match Char.ToLowerInvariant c with
        | 's' -> Some Tone.Acute
        | 'f' -> Some Tone.Grave
        | 'r' -> Some Tone.Hook
        | 'x' -> Some Tone.Tilde
        | 'j' -> Some Tone.Dot
        | _ -> None

    /// Chuẩn hóa cụm nguyên âm khi đi kèm phụ âm cuối và các trường hợp đặc biệt
    let normalizeVowels (vowels: string) (initial: string) (final: string) : string =
        let vLower = vowels.ToLowerInvariant()
        let initLower = initial.ToLowerInvariant()
        let hasFinal = not (String.IsNullOrEmpty final)
        match vLower with
        | "ie" when hasFinal -> "iê"
        | "uye" when hasFinal -> "uyê"
        | "uay" -> "uây"
        | "yeu" -> "yêu"
        | "uoi" -> "uôi"
        | "ieu" -> "iêu"
        | "uo" when hasFinal -> "uô"
        | "ye" when hasFinal -> "yê"
        | "e" when initLower = "gi" && hasFinal -> "ê"
        | "o" when initLower = "qu" && hasFinal -> "ô"
        | _ -> vowels

    /// Xác định chỉ số nguyên âm (0-based) để đặt dấu thanh
    let getTargetVowelIndex (vowels: string) (hasFinal: bool) (style: TonePlacementStyle) : int =
        let len = vowels.Length
        if len <= 1 then 0
        elif len = 2 then
            let vLower = vowels.ToLowerInvariant()
            if hasFinal then
                // Có phụ âm cuối: luôn đặt ở nguyên âm thứ 2
                1
            else
                // Không có phụ âm cuối
                if vLower = "oa" || vLower = "oe" || vLower = "uy" then
                    match style with
                    | TonePlacementStyle.Modern -> 0       // Modern: dấu trên nguyên âm đầu - hóa, xòe, thúy (index 0)
                    | TonePlacementStyle.Traditional -> 1  // Traditional: dấu trên nguyên âm sau - hoá, xoè, thuý (index 1)
                elif "êơưâă".Contains(string vLower[1]) then
                    1
                elif "êơưâă".Contains(string vLower[0]) then
                    0
                else
                    0
        elif len >= 3 then
            // Cụm 3 nguyên âm:
            let vLower = vowels.ToLowerInvariant()
            if vLower.Contains "ươ" || vLower.Contains "ưo" then
                let idx = vLower.IndexOf "ơ"
                if idx >= 0 then idx else 1
            else
                let modIdx = vowels.ToCharArray() |> Array.tryFindIndex (fun c -> "êơâă".Contains(string (Char.ToLowerInvariant c)))
                match modIdx with
                | Some idx -> idx
                | None ->
                    if hasFinal then len - 1
                    else 1
        else
            0

    /// Áp dụng dấu thanh lên âm tiết
    let applyTone (tone: Tone) (style: TonePlacementStyle) (syllable: Syllable) : Syllable =
        if String.IsNullOrEmpty syllable.VowelNucleus then syllable
        else
            let hasFinal = not (String.IsNullOrEmpty syllable.FinalConsonant)
            
            // 1. Chỉ gỡ dấu thanh (Tone), bảo toàn nguyên vẹn dấu mũ/móc (Modifier) như ư, ơ, â, ă, ê, ô
            let cleanToneVowels =
                syllable.VowelNucleus.ToCharArray()
                |> Array.map (fun c ->
                    let baseChar, modifier, _ = decomposeChar c
                    match composeChar (baseChar, modifier, Tone.None) with
                    | Some cleanC -> cleanC
                    | None -> baseChar
                )
                |> String

            // 2. Chuẩn hóa nguyên âm nền tảng dựa trên ngữ cảnh (ví dụ uye + final -> uyê)
            let normVowel = normalizeVowels cleanToneVowels syllable.InitialConsonant syllable.FinalConsonant
            let targetIdx = getTargetVowelIndex normVowel hasFinal style

            // 3. Ràng buộc âm tắc cuối (c, p, t, ch) chỉ nhận thanh Sắc hoặc Nặng
            let fLower = syllable.FinalConsonant.ToLowerInvariant()
            let validTone =
                if (fLower = "t" || fLower = "c" || fLower = "p" || fLower = "ch") && (tone = Tone.Grave || tone = Tone.Hook || tone = Tone.Tilde) then
                    Tone.Acute
                else tone

            let finalChars = normVowel.ToCharArray()
            if validTone <> Tone.None && targetIdx < finalChars.Length then
                let targetChar = finalChars[targetIdx]
                let baseChar, modifier, _ = decomposeChar targetChar
                match composeChar (baseChar, modifier, validTone) with
                | Some tonedChar -> finalChars[targetIdx] <- tonedChar
                | None -> ()

            { syllable with
                VowelNucleus = String(finalChars)
                Tone = validTone }