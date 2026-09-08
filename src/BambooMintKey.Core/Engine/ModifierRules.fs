// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables

module ModifierRules =

    /// Thay thế chuỗi không phân biệt hoa/thường, bảo toàn tính hoa/thường của ký tự gốc
    let private replaceCaseInsensitive (target: string) (replacement: string) (input: string) : string =
        if String.IsNullOrEmpty input then input
        else
            let regex = System.Text.RegularExpressions.Regex(target, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            regex.Replace(input, System.Text.RegularExpressions.MatchEvaluator(fun m ->
                let matched = m.Value
                if Char.IsUpper matched[0] then
                    if matched.Length > 1 && Char.IsUpper matched[1] then
                        replacement.ToUpperInvariant()
                    elif matched.Length = 1 && Char.IsUpper matched[0] then
                        replacement.ToUpperInvariant()
                    else
                        // TitleCase: chữ đầu viết hoa, các chữ sau viết thường
                        string (Char.ToUpperInvariant replacement[0]) + (if replacement.Length > 1 then replacement[1..] else "")
                else
                    replacement.ToLowerInvariant()
            ))

    /// Tiền xử lý chuỗi ký tự thô Telex lồng để giải phóng nguyên âm tiếng Việt chuẩn (không phân biệt hoa/thường)
    let resolveInlineModifiers (raw: string) : string =
        if String.IsNullOrEmpty raw then raw
        else
            raw
            |> replaceCaseInsensitive "uwow" "ươ"
            |> replaceCaseInsensitive "uow" "ươ"
            |> replaceCaseInsensitive "uwo" "ươ"
            |> replaceCaseInsensitive "ưo" "ươ"
            |> replaceCaseInsensitive "uơ" "ươ"
            |> replaceCaseInsensitive "uw" "ư"
            |> replaceCaseInsensitive "ow" "ơ"
            |> replaceCaseInsensitive "aw" "ă"
            |> replaceCaseInsensitive "aa" "â"
            |> replaceCaseInsensitive "ee" "ê"
            |> replaceCaseInsensitive "oo" "ô"
            |> replaceCaseInsensitive "dd" "đ"

    /// Tập hợp các cụm nguyên âm hợp lệ trong ngữ âm học tiếng Việt (cả dạng trung gian và có dấu phụ)
    let ValidVowelClusters : Set<string> =
        set [
            // Nguyên âm đơn
            "a"; "ă"; "â"; "e"; "ê"; "i"; "o"; "ô"; "ơ"; "u"; "ư"; "y"
            // Nhị trùng âm
            "ai"; "ao"; "au"; "ay"; "âu"; "ây"
            "eo"; "êu"
            "ia"; "ie"; "iê"; "iu"
            "oa"; "oă"; "oe"; "oi"; "ôi"; "ơi"; "oo"
            "ua"; "uâ"; "uo"; "uô"; "uê"; "ui"; "uy"; "uơ"; "ưa"; "ươ"; "ưu"
            "ye"; "yê"
            // Tam trùng âm
            "oai"; "oay"; "oao"; "oeo"
            "uai"; "uay"; "uoi"; "uôi"; "ươi"; "ươu"; "uya"; "uye"; "uyê"; "uyu"
            "ieu"; "iêu"; "yeu"; "yêu"
        ]

    /// Kiểm tra tính hợp lệ của cụm nguyên âm theo chuẩn ngữ âm học tiếng Việt
    let isValidVowelCluster (cluster: string) : bool =
        if String.IsNullOrEmpty cluster then false
        else ValidVowelClusters.Contains (cluster.ToLowerInvariant())

    /// Thử mở rộng Syllable hiện tại khi ký tự mới là nguyên âm:
    /// - Từ trạng thái chỉ có phụ âm đầu (VowelNucleus = "") tiếp nhận nguyên âm đầu tiên (Đ + i -> Đi)
    /// - Từ trạng thái đã có nguyên âm sau modifier tiếp nhận thêm nguyên âm hợp lệ (Ư + u -> Ưu, Ơ + i -> Ơi)
    let tryExtendSyllableWithChar (c: char) (syllable: Syllable) : Syllable option =
        let lowerChar = Char.ToLowerInvariant c
        if not (BaseVowels.Contains lowerChar) then None
        elif not (String.IsNullOrEmpty syllable.FinalConsonant) then None
        else
            let currentVowel = syllable.VowelNucleus
            if String.IsNullOrEmpty currentVowel then
                // Trường hợp 1: Âm tiết chỉ mới có phụ âm đầu (VD: 'Đ' sau khi gõ 'Dd')
                // Khi gõ nguyên âm đầu tiên, khởi tạo VowelNucleus
                Some { syllable with VowelNucleus = string lowerChar }
            else
                // Trường hợp 2: Âm tiết đã có nguyên âm (VD: 'ư' sau khi gõ 'Uw', 'ơ' sau 'Ow')
                // Thử ghép nguyên âm mới vào cụm nguyên âm theo bảng ngữ âm học
                let candidateCluster = currentVowel.ToLowerInvariant() + string lowerChar
                if isValidVowelCluster candidateCluster then
                    Some { syllable with VowelNucleus = candidateCluster }
                else
                    None

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