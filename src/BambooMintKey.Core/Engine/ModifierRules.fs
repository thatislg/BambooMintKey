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
            "ua"; "uâ"; "uo"; "uô"; "uê"; "ui"; "uy"; "uơ"; "ưa"; "ưi"; "ươ"; "ưu"
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

    /// Danh sách các cụm nguyên âm cấm tuyệt đối trong tiếng Việt
    let isForbiddenVowelCluster (cluster: string) : bool =
        if String.IsNullOrEmpty cluster then false
        else
            let lower = cluster.ToLowerInvariant()
            lower.Contains "ưô" || lower.Contains "uă" || lower.Contains "ưâ" || lower.Contains "oâ" || lower.Contains "iâ"

    /// Danh sách các cụm nguyên âm bắt buộc phải có phụ âm cuối
    let requiresFinalConsonant (cluster: string) : bool =
        if String.IsNullOrEmpty cluster then false
        else
            let lower = cluster.ToLowerInvariant()
            lower = "uâ" || lower = "ă" || lower = "â" || lower = "oă"

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
                let currentLower = currentVowel.ToLowerInvariant()
                // Hòa hợp móc (horn harmonization): ư + o -> ươ (cả hai cùng mang móc),
                // khớp với resolveInlineModifiers ("ưo" -> "ươ") và "uow" -> "ươ".
                if currentLower = "ư" && lowerChar = 'o' then
                    Some { syllable with VowelNucleus = "ươ" }
                else
                    let candidateCluster = currentLower + string lowerChar
                    if isValidVowelCluster candidateCluster then
                        Some { syllable with VowelNucleus = candidateCluster }
                    else
                        None

    let applyModifier (c: char) (syllable: Syllable) : Syllable option =
        let lower = Char.ToLowerInvariant c
        let initial = syllable.InitialConsonant.ToLowerInvariant()

        // 1. Biến đổi d -> đ
        if lower = 'd' && initial = "d" then
            let newInit = if Char.IsUpper(syllable.InitialConsonant[0]) then "Đ" else "đ"
            Some { syllable with 
                    InitialConsonant = newInit
                    Modifiers = ('d', Modifier.DBar) :: syllable.Modifiers }

        // 2. Biến đổi nguyên âm có mũ / móc
        elif String.IsNullOrEmpty syllable.VowelNucleus then None
        else
            // Bóc tách dấu thanh khỏi hạt nhân nguyên âm để xử lý modifier thuần khiết
            let cleanChars, detectedTone =
                let mutable detTone = Tone.None
                let chars =
                    syllable.VowelNucleus.ToCharArray()
                    |> Array.map (fun ch ->
                        let b, m, t = decomposeChar ch
                        if t <> Tone.None && detTone = Tone.None then
                            detTone <- t
                        match composeChar (b, m, Tone.None) with
                        | Some cleanC -> if Char.IsUpper ch then Char.ToUpperInvariant cleanC else cleanC
                        | None -> ch)
                (String(chars), detTone)

            let currentTone = if detectedTone <> Tone.None then detectedTone else syllable.Tone
            let cleanLower = cleanChars.ToLowerInvariant()
            let hasFinal = not (String.IsNullOrEmpty syllable.FinalConsonant)

            let transformVowel (targetBase: char) (modType: Modifier) =
                let chars = cleanChars.ToCharArray()
                let mutable changed = false
                for i = 0 to chars.Length - 1 do
                    let b, _, _ = decomposeChar chars[i]
                    if not changed && b = targetBase then
                        match composeChar (b, modType, Tone.None) with
                        | Some newC ->
                            chars[i] <- if Char.IsUpper(chars[i]) then Char.ToUpperInvariant newC else newC
                            changed <- true
                        | None -> ()
                if changed then Some (String(chars)) else None

            let formatCaseLike (sample: string) (target: string) =
                if sample.Length >= 2 && Char.IsUpper sample[0] && Char.IsUpper sample[1] then target.ToUpperInvariant()
                elif sample.Length >= 1 && Char.IsUpper sample[0] then
                    string (Char.ToUpperInvariant target[0]) + (if target.Length > 1 then target[1..] else "")
                else target.ToLowerInvariant()

            let newNucleusOpt =
                match lower with
                | 'w' when (cleanLower = "uo" || cleanLower = "uô" || cleanLower = "ưo" || cleanLower = "uơ") ->
                    // Biến đổi cặp đôi uo/uô -> ươ (đồng bộ cả cặp, bảo toàn hoa/thường)
                    Some (formatCaseLike cleanChars "ươ")
                | 'w' when cleanLower.Contains "ua" && not (cleanLower.Contains "ư") ->
                    // Ưu tiên cụm "ua" -> "ưa" (vừa, mưa, chưa, cửa...).
                    // Phải đặt TRƯỚC nhánh biến 'a' -> 'ă' để không sinh "uă" (lỗi vuawf -> vuằ).
                    transformVowel 'u' Modifier.Horn
                | 'a' when cleanLower = "ưa" || cleanLower.Contains "ưa" ->
                    // Nhị trùng âm ưa đã bão hòa: phím a thừa -> giữ nguyên (no-op)
                    Some cleanChars
                | 'a' when cleanLower = "ua" && not hasFinal && currentTone <> Tone.None ->
                    // Đã có thanh điệu ở âm tiết mở (vũa, bùa, dũa): cấm sinh uâ trần -> giữ nguyên (no-op)
                    Some cleanChars
                | 'a' when cleanLower.Contains "a" && not (cleanLower.Contains "â") && not (cleanLower.Contains "ă") ->
                    transformVowel 'a' Modifier.Hat
                | 'w' when cleanLower.Contains "a" && not (cleanLower.Contains "ă") && not (cleanLower.Contains "â") ->
                    transformVowel 'a' Modifier.Breve
                | 'e' when cleanLower.Contains "e" && not (cleanLower.Contains "ê") ->
                    transformVowel 'e' Modifier.Hat
                | 'o' when cleanLower.Contains "o" && not (cleanLower.Contains "ô") && not (cleanLower.Contains "ơ") ->
                    transformVowel 'o' Modifier.Hat
                | 'w' when cleanLower.Contains "o" && not (cleanLower.Contains "ơ") && not (cleanLower.Contains "ô") ->
                    transformVowel 'o' Modifier.Horn
                | 'w' when cleanLower.Contains "u" && not (cleanLower.Contains "ư") ->
                    transformVowel 'u' Modifier.Horn
                | 'w' when cleanLower = "ươ" ->
                    // Đã đủ móc (ư + ơ): phím w thừa -> giữ nguyên (no-op)
                    Some cleanChars
                | _ -> None

            match newNucleusOpt with
            | Some newNucleus ->
                let candLower = newNucleus.ToLowerInvariant()
                // Thẩm định âm vị học: Cấm tuyệt đối các cụm dị dạng (ưô, uă, ưâ, oâ, iâ)
                if isForbiddenVowelCluster candLower then
                    None
                else
                    Some { syllable with VowelNucleus = newNucleus; Tone = currentTone }
            | None -> None