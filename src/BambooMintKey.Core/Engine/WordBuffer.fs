namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.Types

module WordBuffer =

    let detectCase (rawKeys: char list) : LetterCase =
        if rawKeys.IsEmpty then LetterCase.Lower
        else
            let letters = rawKeys |> List.filter Char.IsLetter
            if letters.IsEmpty then LetterCase.Lower
            elif letters |> List.forall Char.IsUpper then LetterCase.Upper
            elif Char.IsUpper letters.Head && (letters.Tail |> List.forall Char.IsLower) then LetterCase.Title
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
                for i = 0 to min (len - 1) (pattern.Length - 1) do
                    if pattern[i] then
                        chars[i] <- Char.ToUpperInvariant chars[i]
                    else
                        chars[i] <- Char.ToLowerInvariant chars[i]
                
                // Nếu có phụ âm cuối viết hoa trước phím dấu thanh (ví dụ vIeeTj -> vIệT)
                if len > 0 && pattern.Length >= 2 && pattern[pattern.Length - 2] && not pattern[pattern.Length - 1] then
                    chars[len - 1] <- Char.ToUpperInvariant chars[len - 1]
                String chars