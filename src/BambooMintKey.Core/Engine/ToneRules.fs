module BambooMintKey.Core.Engine.ToneRules

open System
open BambooMintKey.Core.Domain.EngineConfig
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables.UnicodeTables

module ToneRules =

    /// Chuyển đổi phím ký tự sang Tone tương ứng
    let keyToTone (key: char) : Tone option =
        match Char.ToLowerInvariant(key) with
        | 's' -> Some Tone.Acute
        | 'f' -> Some Tone.Grave
        | 'r' -> Some Tone.Hook
        | 'x' -> Some Tone.Tilde
        | 'j' -> Some Tone.Dot
        | _ -> None

    /// Tìm vị trí index (0-based) trong chuỗi nguyên âm để đặt dấu thanh
    let findToneTargetIndex (vowels: string) (hasFinalConsonant: bool) (style: TonePlacementStyle) : int =
        let len = vowels.Length
        if len <= 1 then 0
        else
            let cleanVowels = 
                vowels 
                |> Seq.map (fun c -> let b, m, _ = decomposeChar c in composeChar(b, m, Tone.None) |> Option.defaultValue b)
                |> Seq.toArray
                |> String

            // 1. Nếu có phụ âm cuối: Đặt ở nguyên âm chính trước phụ âm cuối
            if hasFinalConsonant then
                // Ưu tiên nguyên âm có mũ/móc (ê, ơ, ư, â, ă, ô)
                let modIndex = cleanVowels |> Seq.tryFindIndex (fun c -> "êơưâăô".Contains(c))
                match modIndex with
                | Some idx -> idx
                | None ->
                    if len = 2 then 1      // ví dụ: "an" -> 0, "oan" -> 1 (a)
                    elif len >= 3 then 1    // ví dụ: "uyen" -> 2 (e)
                    else len - 1
            else
                // 2. Không có phụ âm cuối
                match len with
                | 2 ->
                    // Cụm nguyên âm đôi mở: oa, oe, uy
                    let isSpecialPair = cleanVowels = "oa" || cleanVowels = "oe" || cleanVowels = "uy"
                    if isSpecialPair then
                        match style with
                        | TonePlacementStyle.Modern -> 1        // òa, óa, úy
                        | TonePlacementStyle.Traditional -> 0   // oà, oá, uý
                    else
                        // Các nguyên âm đôi khác (ía, ưa, múa, mía): đặt ở âm đầu
                        0
                | 3 ->
                    // Cụm 3 nguyên âm: oai, uay, yeu -> Đặt ở giữa
                    1
                | _ -> 0

    /// Áp dụng dấu thanh vào cấu trúc Syllable
    let applyTone (tone: Tone) (style: TonePlacementStyle) (syllable: Syllable) : Syllable =
        let vowels = syllable.VowelNucleus
        let hasFinal = not (String.IsNullOrEmpty(syllable.FinalConsonant))
        let targetIdx = findToneTargetIndex vowels hasFinal style

        let vowelChars = vowels.ToCharArray()
        let resultChars = 
            vowelChars
            |> Array.mapi (fun i c ->
                let baseChar, modifier, _ = decomposeChar c
                if i = targetIdx then
                    // Đặt tone mới vào nguyên âm được chọn
                    composeChar(baseChar, modifier, tone) |> Option.defaultValue c
                else
                    // Xóa tone cũ khỏi các nguyên âm khác
                    composeChar(baseChar, modifier, Tone.None) |> Option.defaultValue c
            )

        { syllable with 
            VowelNucleus = String(resultChars)
            Tone = tone 
        }