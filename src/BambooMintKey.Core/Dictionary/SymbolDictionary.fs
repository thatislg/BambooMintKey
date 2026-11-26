// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Dictionary

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Reflection
open System.Text
open System.Text.Json
open BambooMintKey.Core.Domain

/// <summary>
/// Từ điển ký hiệu và icon tra cứu bằng từ khóa tiếng Việt không dấu.
/// Nạp lười từ EmbeddedResource <c>BambooMintKey.Core.Resources.symbols-vi.json</c>
/// bằng <see cref="JsonDocument"/> (không dùng reflection, an toàn 100% với NativeAOT).
/// </summary>
module SymbolDictionary =

    let private resourceName = "BambooMintKey.Core.Resources.symbols-vi.json"
    let private maxCandidates = 20

    /// Chuẩn hóa từ khóa về chữ thường, không dấu tiếng Việt, không khoảng trắng.
    let normalizeKeyword (input: string) : string =
        if String.IsNullOrWhiteSpace input then ""
        else
            let decomposed = input.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            let sb = StringBuilder(decomposed.Length)
            for c in decomposed do
                if Char.IsWhiteSpace c then ()
                elif c = 'đ' then sb.Append('d') |> ignore
                else
                    match CharUnicodeInfo.GetUnicodeCategory c with
                    | UnicodeCategory.NonSpacingMark -> ()
                    | _ -> sb.Append(c) |> ignore
            sb.ToString()

    /// Phân tích nội dung JSON dạng { "keyword": ["sym1", "sym2"] } thành mảng SymbolEntry.
    let parseJson (json: string) : SymbolEntry array =
        use doc = JsonDocument.Parse(json)
        let root = doc.RootElement
        if root.ValueKind <> JsonValueKind.Object then
            [||]
        else
            let entries = ResizeArray<SymbolEntry>()
            for prop in root.EnumerateObject() do
                let kw = normalizeKeyword prop.Name
                if not (String.IsNullOrEmpty kw) && prop.Value.ValueKind = JsonValueKind.Array then
                    let syms = ResizeArray<string>()
                    for item in prop.Value.EnumerateArray() do
                        if item.ValueKind = JsonValueKind.String then
                            match item.GetString() with
                            | null | "" -> ()
                            | s -> syms.Add(s)
                    if syms.Count > 0 then
                        entries.Add({ Keyword = kw; Symbols = syms.ToArray() })
            entries.ToArray()

    let private loadEmbeddedEntries () : SymbolEntry array =
        let asm = Assembly.GetExecutingAssembly()
        match asm.GetManifestResourceStream(resourceName) with
        | null -> failwith ("Embedded resource '" + resourceName + "' not found")
        | stream ->
            use reader = new StreamReader(stream, Encoding.UTF8)
            parseJson (reader.ReadToEnd())

    let private defaultEntries = lazy (loadEmbeddedEntries ())

    /// Tra cứu danh sách ký tự ứng viên từ một tập SymbolEntry cho trước.
    /// Xếp hạng: khớp chính xác (exact match) trước, sau đó đến khớp tiền tố (prefix match,
    /// ưu tiên từ khóa ngắn hơn rồi theo thứ tự từ điển), khử trùng lặp và giới hạn tối đa 20 mục.
    let lookupIn (entries: SymbolEntry array) (query: string) : string array =
        let normQuery = normalizeKeyword query
        if String.IsNullOrEmpty normQuery then
            [||]
        else
            let seen = HashSet<string>(StringComparer.Ordinal)
            let results = ResizeArray<string>()

            let addSymbols (syms: string array) =
                for s in syms do
                    if results.Count < maxCandidates && seen.Add(s) then
                        results.Add(s)

            // 1. Khớp chính xác (Exact match)
            for entry in entries do
                if entry.Keyword = normQuery then
                    addSymbols entry.Symbols

            // 2. Khớp tiền tố (Prefix match), ưu tiên độ dài ngắn hơn rồi theo alphabet
            let prefixEntries =
                entries
                |> Array.filter (fun e -> e.Keyword <> normQuery && e.Keyword.StartsWith(normQuery, StringComparison.Ordinal))
                |> Array.sortWith (fun a b ->
                    let lenCmp = compare a.Keyword.Length b.Keyword.Length
                    if lenCmp <> 0 then lenCmp
                    else StringComparer.Ordinal.Compare(a.Keyword, b.Keyword))

            for entry in prefixEntries do
                if results.Count < maxCandidates then
                    addSymbols entry.Symbols

            results.ToArray()

    /// Tra cứu trong từ điển mặc định đi kèm Core.
    let lookup (query: string) : string array =
        lookupIn defaultEntries.Value query

    /// Lấy toàn bộ mục trong từ điển mặc định (phục vụ kiểm thử).
    let getDefaultEntries () : SymbolEntry array =
        defaultEntries.Value
