// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.Types

module WordBuffer =

    /// Kiểm tra xem 2 ký tự đầu có tạo thành cặp modifier Telex (đúp hoặc kèm w) hay không
    let private isInitialDoubleModifierPair (c1: char) (c2: char) : bool =
        let pair = String [| Char.ToLowerInvariant c1; Char.ToLowerInvariant c2 |]
        match pair with
        | "dd" | "ee" | "aa" | "oo" | "aw" | "ow" | "uw" -> true
        | _ -> false

    let detectCase (rawKeys: char list) : LetterCase =
        if rawKeys.IsEmpty then LetterCase.Lower
        else
            let letters = rawKeys |> List.filter Char.IsLetter
            if letters.IsEmpty then LetterCase.Lower
            elif letters |> List.forall Char.IsUpper then LetterCase.Upper
            elif Char.IsUpper letters.Head && (letters.Tail |> List.forall Char.IsLower) then LetterCase.Title
            // Hỗ trợ trường hợp gõ chữ hoa có modifier kép đầu từ:
            // Người dùng thường nhấn giữ Shift khi gõ đúp phím modifier (VD: 'DD'i, 'EE'm, 'AA'n, 'OO'n, 'AW'n...).
            // Nếu phím đầu viết hoa và các ký tự sau cặp modifier đều viết thường -> Phân loại chuẩn xác là TitleCase!
            elif letters.Length >= 2 &&
                 isInitialDoubleModifierPair letters[0] letters[1] &&
                 Char.IsUpper letters[0] &&
                 (letters |> List.skip 2 |> List.forall Char.IsLower) then
                LetterCase.Title
            elif letters |> List.forall Char.IsLower then LetterCase.Lower
            else
                LetterCase.Mixed (rawKeys |> List.map Char.IsUpper)

    let applyCase (letterCase: LetterCase) (transformed: string) : string =
        if String.IsNullOrEmpty transformed then transformed
        else
            match letterCase with
            | LetterCase.Lower -> transformed.ToLowerInvariant()
            | LetterCase.Upper -> transformed.ToUpperInvariant()
            | LetterCase.Title ->
                if transformed.Length = 1 then transformed.ToUpperInvariant()
                else
                    string (Char.ToUpperInvariant transformed[0]) + transformed[1..].ToLowerInvariant()
            | LetterCase.Mixed pattern ->
                let chars = transformed.ToCharArray()
                let len = chars.Length

                // Kiểm tra xem ký tự đầu của transformed có phải là ký tự gộp từ modifier kép hay không (Đ, Ê, Â, Ô, Ă, Ơ, Ư)
                let firstCharIsContracted =
                    len > 0 && pattern.Length >= 2 &&
                    "đêâôăơư".Contains (Char.ToLowerInvariant chars[0])

                for i = 0 to len - 1 do
                    let patternIdx =
                        if firstCharIsContracted && i > 0 then
                            // Ký tự từ vị trí 1 trở đi trong transformed ứng với raw key từ vị trí 2 trở đi
                            min (pattern.Length - 1) (i + 1)
                        else
                            min (pattern.Length - 1) i

                    if pattern[patternIdx] then
                        chars[i] <- Char.ToUpperInvariant chars[i]
                    else
                        chars[i] <- Char.ToLowerInvariant chars[i]
                
                // Nếu có phụ âm cuối viết hoa trước phím dấu thanh (ví dụ vIeeTj -> vIệT)
                if len > 0 && pattern.Length >= 2 && pattern[pattern.Length - 2] && not pattern[pattern.Length - 1] then
                    chars[len - 1] <- Char.ToUpperInvariant chars[len - 1]
                String chars