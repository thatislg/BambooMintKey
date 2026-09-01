module BambooMintKey.Core.Engine.SyllableParser

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables.UnicodeTables

module SyllableParser =

    /// Tách chuỗi thô thành (InitialConsonant, VowelNucleus, FinalConsonant)
    let parse (input: string) : Syllable option =
        if String.IsNullOrEmpty(input) then None
        else
            let chars = input.ToLowerInvariant().ToCharArray() |> Array.toList

            // 1. Tách phụ âm đầu (Tìm prefix dài nhất khớp phụ âm hợp lệ)
            let rec extractInitial acc remaining =
                match remaining with
                | [] -> (acc, [])
                | c :: rest ->
                    if isVowel c then (acc, remaining)
                    else extractInitial (acc + string c) rest

            let initialRaw, afterInitial = extractInitial "" chars

            // Xử lý đặc biệt cho 'qu' và 'gi'
            let initial, afterInitialFixed =
                if initialRaw = "q" && afterInitial.Length > 0 && afterInitial.Head = 'u' && afterInitial.Tail.Length > 0 && isVowel afterInitial.Tail.Head then
                    ("qu", afterInitial.Tail)
                elif initialRaw = "g" && afterInitial.Length > 0 && afterInitial.Head = 'i' && afterInitial.Tail.Length > 0 && isVowel afterInitial.Tail.Head then
                    ("gi", afterInitial.Tail)
                else
                    (initialRaw, afterInitial)

            // Kiểm tra phụ âm đầu có hợp lệ không
            let isInitialValid = 
                String.IsNullOrEmpty(initial) || ValidInitialConsonants.Contains(initial)

            if not isInitialValid then None
            else
                // 2. Tách cụm nguyên âm hạt nhân
                let rec extractVowels acc remaining =
                    match remaining with
                    | [] -> (acc, [])
                    | c :: rest ->
                        if isVowel c then extractVowels (acc + string c) rest
                        else (acc, remaining)

                let vowelsRaw, afterVowels = extractVowels "" afterInitialFixed

                if String.IsNullOrEmpty(vowelsRaw) then None
                else
                    // 3. Tách phụ âm cuối
                    let final = String(Array.ofList afterVowels)
                    let isFinalValid = 
                        String.IsNullOrEmpty(final) || ValidFinalConsonants.Contains(final)

                    if not isFinalValid then None
                    else
                        // Trích xuất Tone và Modifiers hiện tại từ cụm nguyên âm
                        let mutable currentTone = Tone.None
                        let mutable mods = []

                        for c in vowelsRaw do
                            let _, m, t = decomposeChar c
                            if t <> Tone.None then currentTone <- t
                            if m <> Modifier.None then mods <- (c, m) :: mods

                        Some {
                            InitialConsonant = initial
                            VowelNucleus = vowelsRaw
                            FinalConsonant = final
                            Tone = currentTone
                            Modifiers = mods
                        }