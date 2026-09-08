// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables

/// <summary>
/// Module chuyên trách nhận diện và bảo vệ từ tiếng Anh trong chế độ gõ Telex.
/// Thực thi giải pháp 4 tuyến phòng thủ theo tài liệu thiết kế chi tiết 005_02_01.md.
/// </summary>
module EnglishProtection =

    /// Danh sách tinh gọn các từ tiếng Anh thông dụng và từ khóa kỹ thuật hay xung đột Telex (O(1) lookup)
    let CommonEnglishWords : Set<string> =
        set [
            // Từ vựng phổ thông có đuôi -re (nguyên âm câm)
            "core"; "more"; "care"; "share"; "fire"; "sure"; "store"; "before"; "pure"; "cure"
            "wire"; "hire"; "tire"; "bare"; "dare"; "fare"; "hare"; "rare"; "ware"; "here"
            "there"; "where"; "score"; "shore"; "ignore"; "explore"; "require"; "desire"
            "acquire"; "ensure"; "insure"; "assure"; "secure"; "feature"; "future"; "nature"
            "picture"; "structure"
            // Từ vựng có đuôi phụ âm kép -rt, -rd, -rk, -rm, -rn
            "start"; "part"; "smart"; "dart"; "chart"; "heart"; "short"; "sport"; "port"; "sort"
            "report"; "export"; "import"; "support"; "word"; "card"; "hard"; "board"; "record"
            "cord"; "standard"; "work"; "park"; "dark"; "mark"; "fork"; "form"; "term"; "warm"
            "farm"; "norm"; "storm"; "perform"; "platform"; "turn"; "born"; "burn"; "learn"; "return"
            // Từ vựng có đuôi phụ âm kép -st
            "first"; "last"; "fast"; "test"; "post"; "cost"; "list"; "best"; "rest"; "most"
            "past"; "host"; "lost"; "dust"; "must"; "just"; "rust"; "trust"; "request"; "suggest"
            "artist"; "exist"; "boost"; "blast"; "guest"
            // Từ vựng có đuôi phụ âm kép -ct, -ft, -lt, -pt, -nt
            "fact"; "effect"; "direct"; "select"; "project"; "object"; "detect"; "connect"
            "correct"; "product"; "left"; "lift"; "gift"; "soft"; "salt"; "fault"; "default"
            "accept"; "except"; "point"; "print"; "count"; "mount"; "font"; "front"
            // Từ vựng CNTT, lập trình và văn phòng
            "code"; "file"; "line"; "page"; "user"; "role"; "case"; "type"; "data"; "date"; "time"
            "mode"; "node"; "save"; "load"; "view"; "text"; "true"; "false"; "null"; "void"
            "class"; "base"; "this"; "self"; "while"; "break"; "catch"; "reset"; "clear"; "error"
            "game"; "name"; "make"; "take"; "like"; "home"; "some"; "come"; "done"; "none"
            "rule"; "scale"; "state"; "style"; "status"; "system"; "script"; "server"; "client"
            "service"; "source"; "string"; "struct"; "switch"; "syntax"; "index"; "input"; "output"
            "event"; "value"; "valid"; "token"; "table"; "total"; "query"; "queue"; "route"
            "scope"; "stack"; "stage"; "space"; "speed"; "size"; "send"; "read"; "write"
            // Dạng số nhiều / chia động từ đuôi -s phổ biến
            "files"; "lines"; "games"; "notes"; "times"; "rules"; "pages"; "types"; "cases"; "users"
            "roles"; "modes"; "nodes"; "views"; "tests"; "items"; "words"; "cards"; "parts"; "ports"
            "forms"; "terms"; "names"; "codes"; "cores"; "dates"; "rates"; "states"; "styles"
            "errors"; "servers"; "clients"; "events"; "points"; "values"; "tokens"; "tables"
            "routes"; "queues"
        ]

    /// Kiểm tra xem chuỗi có kết thúc bằng cụm phụ âm đặc trưng của tiếng Anh hay không
    let hasEnglishTerminalCluster (lower: string) : bool =
        if lower.Length < 3 then false
        else
            let terminals = [
                "rt"; "rd"; "rk"; "rm"; "rn"; "rp"
                "st"; "sk"; "sp"
                "ct"; "ft"; "lt"; "pt"; "nt"
                "ld"; "nd"; "mp"; "nk"
            ]
            terminals |> List.exists (fun t -> 
                if lower.EndsWith t && lower.Length > t.Length then
                    let prevChar = lower[lower.Length - t.Length - 1]
                    // Trước đuôi phụ âm kép tiếng Anh phải là nguyên âm (a, e, i, o, u, y) hoặc phụ âm 'r'/'l' (như 'first', 'world')
                    "aeiouyrl".Contains (string prevChar)
                else false
            )

    /// Kiểm tra xem chuỗi có kết thúc bằng nguyên âm câm "re" sau nguyên âm hay không (core, more, care...)
    /// Hỗ trợ cả trường hợp người dùng gõ lặp 'rr' theo thói quen cũ để hủy dấu (corre, morre...)
    let hasSilentREnding (lower: string) : bool =
        if lower.Length >= 3 && lower.EndsWith "re" then
            let prevChar = lower[lower.Length - 3]
            if "aeiouy".Contains (string prevChar) then true
            elif prevChar = 'r' && lower.Length >= 4 then
                let prevVowel = lower[lower.Length - 4]
                "aeiouy".Contains (string prevVowel)
            else false
        else false

    /// Chuẩn hóa từ tiếng Anh: khôi phục nguyên trạng, đồng thời tự động triệt tiêu chữ 'r' dư thừa do thói quen gõ 'rr' để hủy dấu Telex (ví dụ: 'Corre' -> 'Core', 'morre' -> 'more')
    let cleanEnglishWordText (rawStr: string) : string =
        let lower = rawStr.ToLowerInvariant()
        if lower.EndsWith "rre" && lower.Length >= 4 then
            let prevVowelIdx = lower.Length - 4
            if prevVowelIdx >= 0 && "aeiouy".Contains (string lower[prevVowelIdx]) then
                rawStr[0 .. lower.Length - 4] + rawStr[lower.Length - 2 ..]
            else rawStr
        else rawStr

    /// Kiểm tra xem chuỗi có kết thúc bằng đuôi số nhiều / chia động từ "-es" hoặc "-s" tiếng Anh hay không
    let hasEnglishPluralEnding (lower: string) : bool =
        if lower.Length >= 4 && lower.EndsWith "es" then
            // Ví dụ: files, games, lines, notes, cases
            let stem = lower[0 .. lower.Length - 2]
            CommonEnglishWords.Contains stem || hasSilentREnding stem
        elif lower.Length >= 4 && lower.EndsWith "s" then
            // Ví dụ: tests, facts, points, marks
            let stem = lower[0 .. lower.Length - 2]
            hasEnglishTerminalCluster stem || CommonEnglishWords.Contains stem
        else false

    /// Bóc tách toàn bộ dấu thanh khỏi cụm nguyên âm để lấy nguyên âm nền thuần khiết phục vụ kiểm định ngữ âm
    let stripToneFromVowels (vowels: string) : string =
        if String.IsNullOrEmpty vowels then ""
        else
            vowels.ToCharArray()
            |> Array.map (fun c ->
                let b, m, _ = decomposeChar c
                match composeChar (b, m, Tone.None) with
                | Some cleanC -> cleanC
                | None -> b
            )
            |> String

    /// Kiểm tra tính hợp lệ toàn diện của âm tiết theo ma trận ngữ âm học tiếng Việt (Tuyến 1)
    let isValidVietnameseSyllableStructure (syllable: Syllable) : bool =
        let initLower = syllable.InitialConsonant.ToLowerInvariant()
        let cleanVowel = stripToneFromVowels syllable.VowelNucleus |> fun s -> s.ToLowerInvariant()
        let finalLower = syllable.FinalConsonant.ToLowerInvariant()

        // 1. Phụ âm 'c' cấm đi với e, ê, i, y, oe, ua
        if initLower = "c" && (cleanVowel.StartsWith "e" || cleanVowel.StartsWith "ê" || cleanVowel.StartsWith "i" || cleanVowel = "oe" || cleanVowel = "ua") then
            false
        // 2. Cụm 'oe' chỉ được đi với các phụ âm ch, h, kh, l, ng, nh, th, t, x (hoặc đứng đầu)
        // Cấm đi với: c, b, d, đ, m, p, r, s, v... (ngăn chặn core, more, bore, sore biến thành cỏe, mỏe, bỏe, sỏe)
        elif cleanVowel = "oe" && not (List.contains initLower [ "ch"; "h"; "kh"; "l"; "ng"; "nh"; "th"; "t"; "x"; "" ]) then
            false
        // 3. Phụ âm 'k' chỉ được đi với e, ê, i, y
        elif initLower = "k" && not (cleanVowel.StartsWith "e" || cleanVowel.StartsWith "ê" || cleanVowel.StartsWith "i" || cleanVowel.StartsWith "y") then
            false
        // 4. Phụ âm 'g', 'ng' không được đi với e, ê, i, y (phải dùng gh, ngh)
        elif (initLower = "g" || initLower = "ng") && (cleanVowel.StartsWith "e" || cleanVowel.StartsWith "ê" || cleanVowel.StartsWith "i" || cleanVowel.StartsWith "y") then
            false
        // 5. Cụm nguyên âm phải nằm trong danh mục chính quy
        elif not (String.IsNullOrEmpty cleanVowel) && not (ModifierRules.isValidVowelCluster cleanVowel) then
            false
        else
            true

    /// Đánh giá xem một chuỗi phím thô có xác suất cao là từ tiếng Anh cần bảo vệ hay không
    let isLikelyEnglishWord (rawKeys: char list) : bool =
        if rawKeys.IsEmpty then false
        else
            let rawStr = String(Array.ofList rawKeys)
            let lower = rawStr.ToLowerInvariant()

            // 1. Tra cứu trực tiếp trong danh sách từ tiếng Anh thông dụng
            if CommonEnglishWords.Contains lower then true
            // 2. Kiểm tra đuôi nguyên âm câm -re sau nguyên âm (core, more, care, share...)
            elif hasSilentREnding lower then true
            // 3. Kiểm tra đuôi phụ âm kép tiếng Anh (-rt, -rd, -st, -rk, -rm, -ct...)
            elif hasEnglishTerminalCluster lower then true
            // 4. Kiểm tra đuôi số nhiều -es / -s của tiếng Anh
            elif hasEnglishPluralEnding lower then true
            else false
