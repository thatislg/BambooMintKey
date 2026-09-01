Dưới đây là tài liệu thiết kế chi tiết cho **Phần 3: Thuật toán Ngữ pháp & Đặt dấu thanh (Grammar Algorithms & Dynamic Tone Placement)** thuộc dự án **BambooMintKey**.

### 1. Phân bổ File Thiết kế & Mã nguồn (Source File Allocation)

Toàn bộ thuật toán xử lý biến đổi dấu phụ và đặt dấu thanh được tổ chức trong thư mục `src/BambooMintKey.Core/Engine/` với 3 file chính:

1. **`src/BambooMintKey.Core/Engine/SyllableParser.fs`**
   - Phân rã chuỗi ký tự thô thành 3 khối cấu trúc âm tiết: Phụ âm đầu (`InitialConsonant`), Cụm hạt nhân nguyên âm (`VowelNucleus`), và Phụ âm cuối (`FinalConsonant`).
2. **`src/BambooMintKey.Core/Engine/ModifierRules.fs`**
   - Xử lý biến đổi dấu mũ, móc, trăng và gạch ngang (`aa -> â`, `aw -> ă`, `ee -> ê`, `oo -> ô`, `ow -> ơ`, `uw -> ư`, `dd -> đ`, `w -> ư/ơ`).
3. **`src/BambooMintKey.Core/Engine/ToneRules.fs`**
   - Xác định chỉ số (Index) của nguyên âm chính trong cụm hạt nhân để gán dấu thanh theo chuẩn Mới (`Modern`: `hòa, hóa, thúy`) hoặc chuẩn Cũ (`Traditional`: `hoà, hoá, thuý`).

### 2. Mô tả Nội dung Kỹ thuật & Luồng Thuật toán (Technical Specification)

#### A. Thuật toán Phân rã Âm tiết (Syllable Decomposition Algorithm)

Một từ tiếng Việt chuẩn luôn tuân theo công thức hình thái học:

Syllable = Initial Consonant + Vowel Nucleus + Final Consonant

1. **Tách phụ âm đầu (`InitialConsonant`):**
   - Quét từ đầu chuỗi ký tự, tìm kiếm chuỗi phụ âm dài nhất khớp với danh sách `ValidInitialConsonants` (ưu tiên so khớp 3 ký tự `ngh`, sau đó 2 ký tự `th, ph, tr, ch, gh, gi, kh, nh, ng, qu`, cuối cùng là 1 ký tự).
   - *Ngoại lệ ngữ pháp:* `qu` và `gi` được coi là phụ âm đầu nguyên khối khi đi kèm nguyên âm phía sau (ví dụ `qu-an`, `gi-a`).
2. **Tách cụm hạt nhân nguyên âm (`VowelNucleus`):**
   - Quét tiếp các ký tự liên tiếp thỏa mãn điều kiện `isVowel` (độ dài cụm nguyên âm thường từ 1 đến 3 ký tự: `a`, `oa`, `uye`, `uou`).
3. **Tách phụ âm cuối (`FinalConsonant`):**
   - Toàn bộ phần ký tự còn lại sau hạt nhân nguyên âm phải khớp với `ValidFinalConsonants` (`c, ch, m, n, ng, nh, p, t`). Nếu phần còn lại chứa nguyên âm hoặc phụ âm không hợp lệ, từ đó bị đánh dấu vi phạm ngữ pháp.

#### B. Quy tắc Biến đổi Dấu phụ Telex (Modifier Rules)

- **Gõ lặp nguyên âm (Double Vowel):**
  - `a + a -> â`, `e + e -> ê`, `o + o -> ô`, `d + d -> đ`.
- **Phím biến đổi phái sinh (`w` Key):**
  - `a + w -> ă`.
  - `o + w -> ơ`, `u + w -> ư`.
  - Cụm `uo + w -> ươ` (tự động gắn móc cho cả hai nguyên âm `u` và `o`).
  - `w` đứng đầu từ khi chưa có nguyên âm: Biến đổi thành `ư` (nếu cấu hình `AllowLeadingWAsU = true`).

#### C. Quy tắc Đặt Dấu Thanh Động (Dynamic Tone Placement Rules)

Xác định chính xác vị trí nguyên âm nhận dấu thanh trong cụm nguyên âm:

1. **Nguyên tắc 1: Có phụ âm cuối (`FinalConsonant` không rỗng):**
   - Dấu thanh luôn đặt trên nguyên âm đứng liền kề trước phụ âm cuối.
   - Ví dụ: `hoàn` (dấu trên `a`), `thuyết` (dấu trên `ê`), `mượn` (dấu trên `ơ`), `tiến` (dấu trên `ê`).
2. **Nguyên tắc 2: Nguyên âm đôi mang dấu phụ (`ươ`, `iê`, `yê`, `uô`):**
   - Dấu luôn đặt trên nguyên âm thứ hai mang dấu mũ/móc.
   - Ví dụ: `rượu` (dấu trên `ợ`), `tiểu` (dấu trên `ể`), `muộn` (dấu trên `ộ`).
3. **Nguyên tắc 3: Không có phụ âm cuối (Nguyên âm mở `oa`, `oe`, `uy`):**
   - **Chuẩn Mới (`Modern`):** Đặt trên nguyên âm thứ 2 (`hòa, xòe, thúy, thủy`).
   - **Chuẩn Cũ (`Traditional`):** Đặt trên nguyên âm thứ 1 (`hoà, xoè, thuý, thuỷ`).
4. **Nguyên tắc 4: Cụm 3 nguyên âm không có phụ âm cuối (`oai`, `uay`, `yeu`):**
   - Dấu luôn đặt trên nguyên âm đứng ở giữa (`ngoại`, `khuấy`).

### 3. Thiết kế Chi tiết & Sample Code F#

#### File 1: `src/BambooMintKey.Core/Engine/SyllableParser.fs`

F#

```F#
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.UnicodeTables

module SyllableParser =

    /// Tách chuỗi thô thành (InitialConsonant, VowelNucleus, FinalConsonant)
    let parse (input: string) : Syllable option =
        if String.IsNullOrEmpty(input) then None
        else
            let chars = input.ToLowerInvariant().ToCharArray() |> Array.toList

            // 1. Tách phụ âm đầu (Tìm prefix dài nhất khớp phụ âm hợp lệ)
            let rec extractInitial acc remaining =
                match remaining with
                | [] -> (acc, [])
                | c :: rest ->
                    if isVowel c then (acc, remaining)
                    else extractInitial (acc + string c) rest

            let (initialRaw, afterInitial) = extractInitial "" chars

            // Xử lý đặc biệt cho 'qu' và 'gi'
            let (initial, afterInitialFixed) =
                if initialRaw = "q" && afterInitial.Length > 0 && afterInitial.Head = 'u' && afterInitial.Tail.Length > 0 && isVowel afterInitial.Tail.Head then
                    ("qu", afterInitial.Tail)
                elif initialRaw = "g" && afterInitial.Length > 0 && afterInitial.Head = 'i' && afterInitial.Tail.Length > 0 && isVowel afterInitial.Tail.Head then
                    ("gi", afterInitial.Tail)
                else
                    (initialRaw, afterInitial)

            // Kiểm tra phụ âm đầu có hợp lệ không
            let isInitialValid = 
                String.IsNullOrEmpty(initial) || ValidInitialConsonants.Contains(initial)

            if not isInitialValid then None
            else
                // 2. Tách cụm nguyên âm hạt nhân
                let rec extractVowels acc remaining =
                    match remaining with
                    | [] -> (acc, [])
                    | c :: rest ->
                        if isVowel c then extractVowels (acc + string c) rest
                        else (acc, remaining)

                let (vowelsRaw, afterVowels) = extractVowels "" afterInitialFixed

                if String.IsNullOrEmpty(vowelsRaw) then None
                else
                    // 3. Tách phụ âm cuối
                    let final = String(Array.ofList afterVowels)
                    let isFinalValid = 
                        String.IsNullOrEmpty(final) || ValidFinalConsonants.Contains(final)

                    if not isFinalValid then None
                    else
                        // Trích xuất Tone và Modifiers hiện tại từ cụm nguyên âm
                        let mutable currentTone = Tone.None
                        let mutable mods = []

                        for c in vowelsRaw do
                            let (_, m, t) = decomposeChar c
                            if t <> Tone.None then currentTone <- t
                            if m <> Modifier.None then mods <- (c, m) :: mods

                        Some {
                            InitialConsonant = initial
                            VowelNucleus = vowelsRaw
                            FinalConsonant = final
                            Tone = currentTone
                            Modifiers = mods
                        }
```

#### File 2: `src/BambooMintKey.Core/Engine/ModifierRules.fs`

F#

```F#
namespace BambooMintKey.Core.Engine

open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.UnicodeTables

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
```

#### File 3: `src/BambooMintKey.Core/Engine/ToneRules.fs`

F#

```F#
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.UnicodeTables

module ToneRules =

    /// Chuyển đổi phím ký tự sang Tone tương ứng
    let keyToTone (key: char) : Tone option =
        match Char.ToLowerInvariant(key) with
        | 's' -> Some Tone.Acute
        | 'f' -> Some Tone.Grave
        | 'r' -> Some Tone.Hook
        | 'x' -> Some Tone.Tilde
        | 'j' -> Some Tone.Dot
        | _ -> None

    /// Tìm vị trí index (0-based) trong chuỗi nguyên âm để đặt dấu thanh
    let findToneTargetIndex (vowels: string) (hasFinalConsonant: bool) (style: TonePlacementStyle) : int =
        let len = vowels.Length
        if len <= 1 then 0
        else
            let cleanVowels = 
                vowels 
                |> Seq.map (fun c -> let (b, m, _) = decomposeChar c in composeChar(b, m, Tone.None) |> Option.defaultValue b)
                |> Seq.toArray
                |> String

            // 1. Nếu có phụ âm cuối: Đặt ở nguyên âm chính trước phụ âm cuối
            if hasFinalConsonant then
                // Ưu tiên nguyên âm có mũ/móc (ê, ơ, ư, â, ă, ô)
                let modIndex = cleanVowels |> Seq.tryFindIndex (fun c -> "êơưâăô".Contains(c))
                match modIndex with
                | Some idx -> idx
                | None ->
                    if len = 2 then 1      // ví dụ: "an" -> 0, "oan" -> 1 (a)
                    elif len >= 3 then 1    // ví dụ: "uyen" -> 2 (e)
                    else len - 1
            else
                // 2. Không có phụ âm cuối
                match len with
                | 2 ->
                    // Cụm nguyên âm đôi mở: oa, oe, uy
                    let isSpecialPair = cleanVowels = "oa" || cleanVowels = "oe" || cleanVowels = "uy"
                    if isSpecialPair then
                        match style with
                        | TonePlacementStyle.Modern -> 1        // òa, óa, úy
                        | TonePlacementStyle.Traditional -> 0   // oà, oá, uý
                    else
                        // Các nguyên âm đôi khác (ía, ưa, múa, mía): đặt ở âm đầu
                        0
                | 3 ->
                    // Cụm 3 nguyên âm: oai, uay, yeu -> Đặt ở giữa
                    1
                | _ -> 0

    /// Áp dụng dấu thanh vào cấu trúc Syllable
    let applyTone (tone: Tone) (style: TonePlacementStyle) (syllable: Syllable) : Syllable =
        let vowels = syllable.VowelNucleus
        let hasFinal = not (String.IsNullOrEmpty(syllable.FinalConsonant))
        let targetIdx = findToneTargetIndex vowels hasFinal style

        let vowelChars = vowels.ToCharArray()
        let resultChars = 
            vowelChars
            |> Array.mapi (fun i c ->
                let (baseChar, modifier, _) = decomposeChar c
                if i = targetIdx then
                    // Đặt tone mới vào nguyên âm được chọn
                    composeChar(baseChar, modifier, tone) |> Option.defaultValue c
                else
                    // Xóa tone cũ khỏi các nguyên âm khác
                    composeChar(baseChar, modifier, Tone.None) |> Option.defaultValue c
            )

        { syllable with 
            VowelNucleus = String(resultChars)
            Tone = tone 
        }
```

