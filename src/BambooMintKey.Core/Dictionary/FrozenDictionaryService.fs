// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Dictionary

open System
open System.Collections.Frozen
open System.IO
open System.Reflection
open BambooMintKey.Core.Domain

/// <summary>
/// Dịch vụ từ điển dùng cấu trúc bất biến <c>FrozenSet&lt;string&gt;</c> (O(1) lookup),
/// nạp dữ liệu từ Embedded Resource đóng gói sẵn trong Core assembly (zero-path dependency).
/// Tham chiếu thiết kế: 008_02 (Vấn đề 4) và 008_04 (Vấn đề 1).
/// </summary>
type FrozenDictionaryService() =

    static let resourcePrefix = "BambooMintKey.Core.Resources."

    /// Nạp một Embedded Resource dạng text (mỗi dòng một từ, lowercase NFC) thành FrozenSet.
    static let loadFrozenSet (resourceName: string) : FrozenSet<string> =
        let asm = Assembly.GetExecutingAssembly()
        use stream = asm.GetManifestResourceStream(resourceName)
        if isNull stream then
            failwithf "Embedded resource '%s' not found" resourceName
        use reader = new StreamReader(stream)
        let words =
            seq {
                let mutable line = reader.ReadLine()
                while not (isNull line) do
                    let w = line.Trim()
                    if not (String.IsNullOrEmpty w) then
                        yield w
                    line <- reader.ReadLine()
            }
        words.ToFrozenSet(StringComparer.OrdinalIgnoreCase)

    // Lazy nạp 2 bộ từ điển nền (chỉ nạp lần đầu tiên truy cập, thời gian < 5ms).
    let vietSyllables = lazy (loadFrozenSet (resourcePrefix + "vietnamese-syllables-mit.dict"))
    let englishWords = lazy (loadFrozenSet (resourcePrefix + "english-20k.dict"))

    // Từ điển người dùng mở rộng (custom) — tách riêng Việt/Anh theo heuristic dấu.
    let mutable customViet = Set.empty<string>
    let mutable customEng = Set.empty<string>
    let sync = obj()

    /// Heuristic: từ đã lowercase mà vẫn chứa ký tự ngoài a-z là có dấu tiếng Việt.
    static let hasVietnameseDiacritics (w: string) =
        w |> Seq.exists (fun c -> c < 'a' || c > 'z')

    member _.IsValidVietnameseSyllable (word: string) : bool =
        if String.IsNullOrWhiteSpace word then false
        else
            let w = word.Trim()
            vietSyllables.Value.Contains w || customViet.Contains w

    member _.IsLikelyEnglishWord (word: string) : bool =
        if String.IsNullOrWhiteSpace word then false
        else
            let w = word.Trim()
            englishWords.Value.Contains w || customEng.Contains w

    member _.MergeCustomWords (words: string seq) : unit =
        lock sync (fun () ->
            for raw in words do
                let w = raw.Trim()
                if not (String.IsNullOrEmpty w) then
                    let lower = w.ToLowerInvariant()
                    if hasVietnameseDiacritics lower then
                        customViet <- customViet.Add lower
                    else
                        customEng <- customEng.Add lower)

    interface IDictionaryService with
        member this.IsValidVietnameseSyllable word = this.IsValidVietnameseSyllable word
        member this.IsLikelyEnglishWord word = this.IsLikelyEnglishWord word
        member this.MergeCustomWords words = this.MergeCustomWords words
