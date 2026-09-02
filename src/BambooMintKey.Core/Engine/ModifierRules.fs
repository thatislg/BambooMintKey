// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables

module ModifierRules =

    /// Tiền xử lý chuỗi ký tự thô Telex lồng để giải phóng nguyên âm tiếng Việt chuẩn
    let resolveInlineModifiers (raw: string) : string =
        if String.IsNullOrEmpty raw then raw
        else
            raw
                .Replace("uwow", "ươ")
                .Replace("uow", "ươ")
                .Replace("uwo", "ươ")
                .Replace("ưo", "ươ")
                .Replace("uơ", "ươ")
                .Replace("uw", "ư")
                .Replace("ow", "ơ")
                .Replace("aw", "ă")
                .Replace("aa", "â")
                .Replace("ee", "ê")
                .Replace("oo", "ô")
                .Replace("dd", "đ")

    let applyModifier (c: char) (syllable: Syllable) : Syllable option =
        let lower = Char.ToLowerInvariant c
        let vowels = syllable.VowelNucleus.ToLowerInvariant()
        let initial = syllable.InitialConsonant.ToLowerInvariant()

        // 1. Biến đổi d -> đ
        if lower = 'd' && initial = "d" then
            let newInit = if Char.IsUpper(syllable.InitialConsonant[0]) then "Đ" else "đ"
            Some { syllable with 
                    InitialConsonant = newInit
                    Modifiers = ('d', Modifier.DBar) :: syllable.Modifiers }

        // 2. Biến đổi nguyên âm có mũ / móc
        elif String.IsNullOrEmpty vowels then None
        else
            let transformVowel (targetBase: char) (modType: Modifier) =
                let chars = syllable.VowelNucleus.ToCharArray()
                let mutable changed = false
                for i = 0 to chars.Length - 1 do
                    let b, _, t = decomposeChar chars[i]
                    if not changed && b = targetBase then
                        match composeChar (b, modType, t) with
                        | Some newC ->
                            chars[i] <- if Char.IsUpper(chars[i]) then Char.ToUpperInvariant newC else newC
                            changed <- true
                        | None -> ()
                if changed then Some (String(chars)) else None

            let newNucleusOpt =
                match lower with
                | 'w' when (vowels.Contains "uo" || vowels.Contains "uô" || vowels.Contains "ưo" || vowels.Contains "uơ") ->
                    // Biến đổi cặp đôi uo -> ươ
                    let replaced = 
                        vowels
                            .Replace("uo", "ươ")
                            .Replace("uô", "ươ")
                            .Replace("ưo", "ươ")
                            .Replace("uơ", "ươ")
                    Some replaced
                | 'a' when vowels.Contains "a" && not (vowels.Contains "â") && not (vowels.Contains "ă") ->
                    transformVowel 'a' Modifier.Hat
                | 'w' when vowels.Contains "a" && not (vowels.Contains "ă") && not (vowels.Contains "â") ->
                    transformVowel 'a' Modifier.Breve
                | 'e' when vowels.Contains "e" && not (vowels.Contains "ê") ->
                    transformVowel 'e' Modifier.Hat
                | 'o' when vowels.Contains "o" && not (vowels.Contains "ô") && not (vowels.Contains "ơ") ->
                    transformVowel 'o' Modifier.Hat
                | 'w' when vowels.Contains "o" && not (vowels.Contains "ơ") && not (vowels.Contains "ô") ->
                    transformVowel 'o' Modifier.Horn
                | 'w' when vowels.Contains "u" && not (vowels.Contains "ư") ->
                    transformVowel 'u' Modifier.Horn
                | _ -> None

            match newNucleusOpt with
            | Some newNucleus -> Some { syllable with VowelNucleus = newNucleus }
            | None -> None