module BambooMintKey.Core.Engine.ModifierRules

open BambooMintKey.Core.Domain.Types

module ModifierRules =

    /// Xử lý phím dấu mũ / móc / trăng / gạch ngang Telex
    let applyModifier (key: char) (syllable: Syllable) : Syllable option =
        let lowerKey = System.Char.ToLowerInvariant(key)
        let vowels = syllable.VowelNucleus

        match lowerKey with
        // Phím 'a': aa -> â
        | 'a' when vowels.Contains("a") && not (vowels.Contains("â")) && not (vowels.Contains("ă")) ->
            let newVowels = vowels.Replace('a', 'â')
            Some { syllable with VowelNucleus = newVowels }

        // Phím 'e': ee -> ê
        | 'e' when vowels.Contains("e") && not (vowels.Contains("ê")) ->
            let newVowels = vowels.Replace('e', 'ê')
            Some { syllable with VowelNucleus = newVowels }

        // Phím 'o': oo -> ô
        | 'o' when vowels.Contains("o") && not (vowels.Contains("ô")) && not (vowels.Contains("ơ")) ->
            let newVowels = vowels.Replace('o', 'ô')
            Some { syllable with VowelNucleus = newVowels }

        // Phím 'w': aw -> ă, ow -> ơ, uw -> ư, uow -> ươ
        | 'w' ->
            if vowels.Contains("uo") || vowels.Contains("uô") then
                let newVowels = vowels.Replace("uo", "ươ").Replace("uô", "ươ")
                Some { syllable with VowelNucleus = newVowels }
            elif vowels.Contains("a") && not (vowels.Contains("â")) && not (vowels.Contains("ă")) then
                let newVowels = vowels.Replace('a', 'ă')
                Some { syllable with VowelNucleus = newVowels }
            elif vowels.Contains("o") && not (vowels.Contains("ô")) && not (vowels.Contains("ơ")) then
                let newVowels = vowels.Replace('o', 'ơ')
                Some { syllable with VowelNucleus = newVowels }
            elif vowels.Contains("u") && not (vowels.Contains("ư")) then
                let newVowels = vowels.Replace('u', 'ư')
                Some { syllable with VowelNucleus = newVowels }
            else None

        // Phím 'd': dd -> đ (xử lý trên phụ âm đầu)
        | 'd' when syllable.InitialConsonant = "d" ->
            Some { syllable with InitialConsonant = "đ" }

        | _ -> None