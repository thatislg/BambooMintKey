namespace BambooMintKey.Core.Tests

open BambooMintKey.Core.Engine
open BambooMintKey.Core.Domain.Types
open Xunit

module SyllableParserTests =

    // 1. Phân rã phụ âm đầu, cụm nguyên âm, và phụ âm cuối (Decomposition)
    // - Đảm bảo chính xác khả năng cắt InitialConsonant, VowelNucleus, FinalConsonant.
    [<Theory>]
    [<InlineData("nghieng", "ngh", "ie", "ng")>] // Phụ âm đầu 3 chữ cái 'ngh'
    [<InlineData("thuyet", "th", "uye", "t")>]   // Phụ âm đầu 2 chữ cái 'th'
    [<InlineData("chon", "ch", "o", "n")>]       // Phụ âm đầu 'ch'
    [<InlineData("hoan", "h", "oa", "n")>]       // Phụ âm 'h' 
    [<InlineData("an", "", "a", "n")>]           // Không có phụ âm đầu (chỉ vần & phụ âm cuối)
    [<InlineData("eo", "", "eo", "")>]           // Chỉ có cụm nguyên âm (không âm đầu, cuối)
    let ``1. Extract standard syllable components accurately`` (input: string, init: string, vowel: string, final: string) =
        let parsed = SyllableParser.parse input
        Assert.True(parsed.IsSome)
        let syl = parsed.Value
        Assert.Equal(init, syl.InitialConsonant)
        Assert.Equal(vowel, syl.VowelNucleus)
        Assert.Equal(final, syl.FinalConsonant)

    // 2. Chặn và phân loại các chuỗi sai ngữ pháp (Invalid Syllables Validation)
    // - Các chuỗi không hợp lệ theo quy tắc tiếng Việt sẽ trả về None để Fallback sang tiếng Anh.
    [<Theory>]
    [<InlineData("abc")>]     // 'c' hợp lệ làm âm cuối, nhưng Initial="", Vowel=a, Final=bc -> Invalid Final
    [<InlineData("k")>]       // Không chứa nguyên âm (Empty Vowel) -> Invalid
    [<InlineData("nghm")>]    // Nghm -> Initial 'ngh', Vowel '', Final 'm' -> Invalid
    [<InlineData("tamr")>]    // 'r' vô nghĩa dưới dạng chữ vì nằm sau nguyên âm, 'mr' ko hợp lệ -> Invalid Final
    let ``2. Invalid structure should be rejected by parser`` (input: string) =
        let parsed = SyllableParser.parse input
        Assert.True(parsed.IsNone)

    // 3. Phụ âm đầu ngoại lệ 'qu' và 'gi'
    // - 'u' trong 'qu', 'i' trong 'gi' không được xét vào VowelNucleus nếu sau đó vẫn còn nguyên âm.
    [<Theory>]
    [<InlineData("quan", "qu", "a", "n")>]
    [<InlineData("quoc", "qu", "o", "c")>]
    [<InlineData("gian", "gi", "a", "n")>]
    [<InlineData("gieng", "gi", "e", "ng")>]
    let ``3. Special initial consonants qu and gi correctly isolated`` (input: string, init: string, vowel: string, final: string) =
        let parsed = SyllableParser.parse input
        Assert.True(parsed.IsSome, $"Failed to parse: {input}")
        let syl = parsed.Value
        Assert.Equal(init, syl.InitialConsonant)
        Assert.Equal(vowel, syl.VowelNucleus)
        Assert.Equal(final, syl.FinalConsonant)