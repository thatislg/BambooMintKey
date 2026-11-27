// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
module BambooMintKey.Core.Tests.SymbolDictionaryTests

open Xunit
open BambooMintKey.Core.Dictionary

[<Fact>]
let ``Default embedded dictionary loads sample entries`` () =
    let entries = SymbolDictionary.getDefaultEntries ()
    Assert.True(entries.Length >= 15)
    Assert.Contains(entries, fun e -> e.Keyword = "mui" && e.Symbols[0] = "→")
    Assert.Contains(entries, fun e -> e.Keyword = "vn" && e.Symbols[0] = "🇻🇳")
    Assert.Contains(entries, fun e -> e.Keyword = "canhbao" && e.Symbols[0] = "⚠️")
    Assert.Contains(entries, fun e -> e.Keyword = "tron" && e.Symbols[0] = "①")
    Assert.Contains(entries, fun e -> e.Keyword = "tronden" && e.Symbols[0] = "❶")
    Assert.Contains(entries, fun e -> e.Keyword = "vuong" && e.Symbols[0] = "1️⃣")

[<Theory>]
[<InlineData("mui", "mui")>]
[<InlineData("MŨI", "mui")>]
[<InlineData("Mũi", "mui")>]
[<InlineData("cảnh báo", "canhbao")>]
[<InlineData("độ", "do")>]
[<InlineData("ĐỘ C", "doc")>]
let ``normalizeKeyword strips diacritics, spaces, and lowercases`` (input: string) (expected: string) =
    Assert.Equal(expected, SymbolDictionary.normalizeKeyword input)

[<Fact>]
let ``lookup ranks exact match before prefix match and deduplicates`` () =
    let customEntries =
        SymbolDictionary.parseJson """
        {
          "m":   ["★"],
          "mui": ["→", "★", "←"],
          "mu":  ["⇒"]
        }
        """
    let results = SymbolDictionary.lookupIn customEntries "m"
    Assert.Equal<string array>([| "★"; "⇒"; "→"; "←" |], results)

[<Fact>]
let ``lookup supports accent-insensitive and prefix queries`` () =
    let exact = SymbolDictionary.lookup "MŨI"
    Assert.Equal("→", exact[0])
    Assert.Equal(7, exact.Length)

    let prefix = SymbolDictionary.lookup "ca"
    Assert.Contains("⚠️", prefix)
