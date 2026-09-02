// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables

module SyllableParser =

    let parse (input: string) : Syllable option =
        if String.IsNullOrEmpty input then None
        else
            let resolved = ModifierRules.resolveInlineModifiers input
            let lower = resolved.ToLowerInvariant()
            
            // 1. Tách phụ âm đầu (Initial Consonant)
            let initialConsonants = [
                "ngh"; "ng"; "nh"; "ch"; "gh"; "gi"; "kh"; "ph"; "qu"; "th"; "tr";
                "b"; "c"; "d"; "đ"; "g"; "h"; "k"; "l"; "m"; "n"; "p"; "r"; "s"; "t"; "v"; "x"
            ]
            let initial =
                initialConsonants
                |> List.tryFind (fun ic -> lower.StartsWith ic)
                |> Option.defaultValue ""

            let afterInitial = lower[initial.Length..]
            if String.IsNullOrEmpty afterInitial then
                // Chỉ có phụ âm đầu mà không có nguyên âm -> Không phải âm tiết tiếng Việt hợp lệ
                None
            else
                // 2. Tách phụ âm cuối (Final Consonant)
                let finalConsonants = [
                    "ch"; "nh"; "ng"; "c"; "m"; "n"; "p"; "t"
                ]
                let final =
                    finalConsonants
                    |> List.tryFind (fun fc -> afterInitial.EndsWith fc)
                    |> Option.defaultValue ""

                let vowelsRaw = afterInitial[0 .. afterInitial.Length - 1 - final.Length]
                
                if String.IsNullOrEmpty vowelsRaw then None
                else
                    // Kiểm tra tất cả ký tự trong vowelsRaw có phải nguyên âm tiếng Việt hợp lệ không
                    let allVowelsValid =
                        vowelsRaw.ToCharArray()
                        |> Array.forall isVowel

                    if not allVowelsValid then None
                    else
                        // Trích xuất Tone hiện tại nếu có trong nguyên âm
                        let detectedTone =
                            vowelsRaw.ToCharArray()
                            |> Array.tryPick (fun c ->
                                let _, _, t = decomposeChar c
                                if t <> Tone.None then Some t else None
                            )
                            |> Option.defaultValue Tone.None

                        Some {
                            InitialConsonant = if initial.Length > 0 then resolved[0..initial.Length - 1] else ""
                            VowelNucleus = vowelsRaw
                            FinalConsonant = if final.Length > 0 then resolved[resolved.Length - final.Length..] else ""
                            Tone = detectedTone
                            Modifiers = []
                        }