module BambooMintKey.Core.Engine.WordBuffer

open System
open BambooMintKey.Core.Domain.Types

module WordBuffer =

    /// Xác định kiểu viết hoa/viết thường từ chuỗi phím thô
    let detectCase (rawChars: char list) : LetterCase =
        match rawChars with
        | [] -> LetterCase.Lower
        | [ c ] when Char.IsUpper(c) -> LetterCase.Title
        | chars ->
            let isAllUpper = chars |> List.forall Char.IsUpper
            let isAllLower = chars |> List.forall Char.IsLower
            let isTitle = Char.IsUpper(chars.Head) && (chars.Tail |> List.forall Char.IsLower)

            if isAllUpper then LetterCase.Upper
            elif isAllLower then LetterCase.Lower
            elif isTitle then LetterCase.Title
            else LetterCase.Mixed (chars |> List.map Char.IsUpper)

    /// Áp dụng định dạng viết hoa/thường lên chuỗi kết quả đã biến đổi
    let applyCase (letterCase: LetterCase) (text: string) : string =
        if String.IsNullOrEmpty(text) then text
        else
            match letterCase with
            | LetterCase.Lower -> text.ToLowerInvariant()
            | LetterCase.Upper -> text.ToUpperInvariant()
            | LetterCase.Title ->
                let first = Char.ToUpperInvariant(text[0])
                let rest = if text.Length > 1 then text.Substring(1).ToLowerInvariant() else ""
                string first + rest
            | LetterCase.Mixed masks ->
                let chars = text.ToCharArray()
                let applied =
                    chars
                    |> Array.mapi (fun i c ->
                        if i < masks.Length && masks[i] then Char.ToUpperInvariant(c)
                        else Char.ToLowerInvariant(c)
                    )
                String(applied)