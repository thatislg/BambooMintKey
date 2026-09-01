Dưới đây là tài liệu thiết kế chi tiết cho **Phần 2: Bảng tra cứu tĩnh & Phân loại ký tự (Static Lookup Tables & Character Classification)** thuộc dự án **BambooMintKey**.

### 1. Phân bổ File Thiết kế & Mã nguồn (Source File Allocation)

Toàn bộ logic tra cứu tĩnh và chuẩn hóa bảng mã Unicode được tổ chức trong thư mục `src/BambooMintKey.Core/Domain/` với file chính:

- **`src/BambooMintKey.Core/Domain/UnicodeTables.fs`**
  - Chứa toàn bộ các tập hợp tĩnh (Static Sets), bảng ánh xạ tổ hợp nguyên âm (Mapping Tables), từ điển chuyển đổi dấu thanh và các hàm phân loại ký tự thuần túy (Pure Classification Functions).

### 2. Mô tả Nội dung Kỹ thuật & Luồng Xử lý (Technical Specification)

#### A. Phân loại tập hợp phụ âm & nguyên âm (Consonant & Vowel Classification)

- **Phụ âm đầu (Initial Consonants):**
  - Bao gồm phụ âm đơn: `b, c, d, đ, g, h, k, l, m, n, p, q, r, s, t, v, x`.
  - Bao gồm phụ âm ghép 2 chữ cái: `ch, gh, gi, kh, nh, ng, ph, qu, th, tr`.
  - Bao gồm phụ âm ghép 3 chữ cái: `ngh`.
  - Phục vụ bộ phân rã âm tiết xác định chính xác phần đầu của từ, loại trừ các phụ âm ngoại lai không hợp lệ tiếng Việt (như `cl, fl, pl, pr, str`).
- **Phụ âm cuối (Final Consonants):**
  - Bao gồm phụ âm đơn: `c, m, n, p, t`.
  - Bao gồm phụ âm ghép: `ch, ng, nh`.
  - Giúp thuật toán nhận diện ranh giới kết thúc của hạt nhân nguyên âm.
- **Nguyên âm cơ bản (Base Vowels):**
  - Tập hợp 6 nguyên âm latin gốc: `a, e, i, o, u, y`.

#### B. Bảng ánh xạ dấu phụ / Modifier (Hat, Horn, Breve, DBar)

- Chuyển đổi cặp `(Ký tự gốc, Modifier)` thành ký tự Unicode dựng sẵn (NFC):
  - `a + Hat -> â`, `a + Breve -> ă`.
  - `e + Hat -> ê`.
  - `o + Hat -> ô`, `o + Horn -> ơ`.
  - `u + Horn -> ư`.
  - `d + DBar -> đ`.
- Hỗ trợ ánh xạ ngược để phục vụ cơ chế hoàn tác (Undo/Escape).

#### C. Bảng ánh xạ dấu thanh Unicode NFC (Tone Mapping Matrix)

- Tiếng Việt có tổng cộng **12 nguyên âm đơn** trong bảng chữ cái: `a, ă, â, e, ê, i, o, ô, ơ, u, ư, y`.
- Mỗi nguyên âm kết hợp với 5 dấu thanh (`Acute`, `Grave`, `Hook`, `Tilde`, `Dot`) tạo thành ma trận 60 ký tự có dấu thanh độc lập ở chuẩn **Unicode dựng sẵn (NFC)**.
- Sử dụng cấu trúc `Map<(char * Tone), char>` hoặc mảng tra cứu 2 chiều tĩnh để đạt tốc độ truy xuất $O(1)$ với độ trễ xấp xỉ $0\text{ns}$.

#### D. Trích xuất thuộc tính ký tự (Character Decomposition & Normalization)

- Cung cấp các hàm phân tích ngược: Khi nhận một ký tự Unicode bất kỳ (ví dụ `ệ`), hàm có khả năng trích xuất ra:
  - Ký tự gốc nguyên bản (`e`).
  - Dấu phụ đi kèm (`Modifier.Hat`).
  - Dấu thanh hiện tại (`Tone.Dot`).

### 3. Thiết kế Chi tiết & Sample Code F# (`src/BambooMintKey.Core/Domain/UnicodeTables.fs`)

F#

```F#
namespace BambooMintKey.Core.Domain

open System

module UnicodeTables =

    // =========================================================================
    // 1. TẬP HỢP PHÂN LOẠI KÝ TỰ (CHARACTER SETS)
    // =========================================================================

    /// Tập hợp các nguyên âm cơ bản không dấu
    let BaseVowels = set [ 'a'; 'e'; 'i'; 'o'; 'u'; 'y' ]

    /// Tập hợp toàn bộ 12 nguyên âm tiếng Việt (cả nguyên bản và có mũ/móc)
    let AllVietnameseVowels = 
        set [ 'a'; 'ă'; 'â'; 'e'; 'ê'; 'i'; 'o'; 'ô'; 'ơ'; 'u'; 'ư'; 'y' ]

    /// Tập hợp các phụ âm đầu hợp lệ trong ngữ pháp tiếng Việt (đã chuẩn hóa chữ thường)
    let ValidInitialConsonants = 
        set [
            "b"; "c"; "d"; "đ"; "g"; "gh"; "gi"; "h"; "k"; "kh";
            "l"; "m"; "n"; "ng"; "ngh"; "nh"; "p"; "ph"; "q"; "qu";
            "r"; "s"; "t"; "th"; "tr"; "v"; "x"
        ]

    /// Tập hợp các phụ âm cuối hợp lệ trong ngữ pháp tiếng Việt
    let ValidFinalConsonants = 
        set [ "c"; "ch"; "m"; "n"; "ng"; "nh"; "p"; "t" ]

    // =========================================================================
    // 2. BẢNG ÁNH XẠ DẤU PHỤ - MODIFIER MAPPING (MŨ, MÓC, TRĂNG, GẠCH)
    // =========================================================================

    /// Ánh xạ từ (Ký tự gốc + Modifier) -> Ký tự có dấu phụ NFC
    let ModifierTable : Map<char * Modifier, char> =
        Map.ofList [
            (('a', Modifier.Hat),   'â')
            (('a', Modifier.Breve), 'ă')
            (('e', Modifier.Hat),   'ê')
            (('o', Modifier.Hat),   'ô')
            (('o', Modifier.Horn),  'ơ')
            (('u', Modifier.Horn),  'ư')
            (('d', Modifier.DBar),  'đ')
        ]

    /// Ánh xạ ngược từ Ký tự có dấu phụ NFC -> (Ký tự gốc, Modifier)
    let ReverseModifierTable : Map<char, char * Modifier> =
        Map.ofList [
            ('â', ('a', Modifier.Hat))
            ('ă', ('a', Modifier.Breve))
            ('ê', ('e', Modifier.Hat))
            ('ô', ('o', Modifier.Hat))
            ('ơ', ('o', Modifier.Horn))
            ('ư', ('u', Modifier.Horn))
            ('đ', ('d', Modifier.DBar))
        ]

    // =========================================================================
    // 3. MA TRẬN ÁNH XẠ DẤU THANH UNICODE NFC (TONE MAPPING MATRIX)
    // =========================================================================

    /// Bảng ánh xạ: (Nguyên âm có/không mũ/móc + Tone) -> Ký tự NFC hoàn chỉnh
    let ToneTable : Map<char * Tone, char> =
        Map.ofList [
            // Nguyên âm 'a'
            (('a', Tone.None),  'a'); (('a', Tone.Acute), 'á'); (('a', Tone.Grave), 'à'); (('a', Tone.Hook), 'ả'); (('a', Tone.Tilde), 'ã'); (('a', Tone.Dot), 'ạ')
            // Nguyên âm 'ă'
            (('ă', Tone.None),  'ă'); (('ă', Tone.Acute), 'ắ'); (('ă', Tone.Grave), 'ằ'); (('ă', Tone.Hook), 'ẳ'); (('ă', Tone.Tilde), 'ẵ'); (('ă', Tone.Dot), 'ặ')
            // Nguyên âm 'â'
            (('â', Tone.None),  'â'); (('â', Tone.Acute), 'ấ'); (('â', Tone.Grave), 'ầ'); (('â', Tone.Hook), 'ẩ'); (('â', Tone.Tilde), 'ẫ'); (('â', Tone.Dot), 'ậ')
            // Nguyên âm 'e'
            (('e', Tone.None),  'e'); (('e', Tone.Acute), 'é'); (('e', Tone.Grave), 'è'); (('e', Tone.Hook), 'ẻ'); (('e', Tone.Tilde), 'ẽ'); (('e', Tone.Dot), 'ẹ')
            // Nguyên âm 'ê'
            (('ê', Tone.None),  'ê'); (('ê', Tone.Acute), 'ế'); (('ê', Tone.Grave), 'ề'); (('ê', Tone.Hook), 'ể'); (('ê', Tone.Tilde), 'ễ'); (('ê', Tone.Dot), 'ệ')
            // Nguyên âm 'i'
            (('i', Tone.None),  'i'); (('i', Tone.Acute), 'í'); (('i', Tone.Grave), 'ì'); (('i', Tone.Hook), 'ỉ'); (('i', Tone.Tilde), 'ĩ'); (('i', Tone.Dot), 'ị')
            // Nguyên âm 'o'
            (('o', Tone.None),  'o'); (('o', Tone.Acute), 'ó'); (('o', Tone.Grave), 'ò'); (('o', Tone.Hook), 'ỏ'); (('o', Tone.Tilde), 'õ'); (('o', Tone.Dot), 'ọ')
            // Nguyên âm 'ô'
            (('ô', Tone.None),  'ô'); (('ô', Tone.Acute), 'ố'); (('ô', Tone.Grave), 'ồ'); (('ô', Tone.Hook), 'ổ'); (('ô', Tone.Tilde), 'ỗ'); (('ô', Tone.Dot), 'ộ')
            // Nguyên âm 'ơ'
            (('ơ', Tone.None),  'ơ'); (('ơ', Tone.Acute), 'ớ'); (('ơ', Tone.Grave), 'ờ'); (('ơ', Tone.Hook), 'ở'); (('ơ', Tone.Tilde), 'ỡ'); (('ơ', Tone.Dot), 'ợ')
            // Nguyên âm 'u'
            (('u', Tone.None),  'u'); (('u', Tone.Acute), 'ú'); (('u', Tone.Grave), 'ù'); (('u', Tone.Hook), 'ủ'); (('u', Tone.Tilde), 'ũ'); (('u', Tone.Dot), 'ụ')
            // Nguyên âm 'ư'
            (('ư', Tone.None),  'ư'); (('ư', Tone.Acute), 'ứ'); (('ư', Tone.Grave), 'ừ'); (('ư', Tone.Hook), 'ử'); (('ư', Tone.Tilde), 'ữ'); (('ư', Tone.Dot), 'ự')
            // Nguyên âm 'y'
            (('y', Tone.None),  'y'); (('y', Tone.Acute), 'ý'); (('y', Tone.Grave), 'ỳ'); (('y', Tone.Hook), 'ỷ'); (('y', Tone.Tilde), 'ỹ'); (('y', Tone.Dot), 'ỵ')
        ]

    /// Ánh xạ ngược: Ký tự NFC có dấu -> (Nguyên âm gốc dạng mũ/móc, Tone)
    let ReverseToneTable : Map<char, char * Tone> =
        ToneTable
        |> Map.toList
        |> List.map (fun ((vowel, tone), resChar) -> (resChar, (vowel, tone)))
        |> Map.ofList

    // =========================================================================
    // 4. CÁC HÀM TRỢ GIÚP TRA CỨU NHANH (FAST LOOKUP HELPERS)
    // =========================================================================

    /// Kiểm tra xem một ký tự có phải là nguyên âm tiếng Việt (kể cả có dấu thanh/mũ) hay không
    let isVowel (c: char) : bool =
        let lower = Char.ToLowerInvariant(c)
        AllVietnameseVowels.Contains(lower) || ReverseToneTable.ContainsKey(lower)

    /// Lấy dấu thanh hiện tại của một ký tự đơn
    let extractTone (c: char) : Tone =
        let lower = Char.ToLowerInvariant(c)
        match ReverseToneTable.TryFind(lower) with
        | Some (_, tone) -> tone
        | None -> Tone.None

    /// Chuẩn hóa ký tự có dấu về nguyên âm gốc và tách dấu phụ/dấu thanh
    let decomposeChar (c: char) : char * Modifier * Tone =
        let lower = Char.ToLowerInvariant(c)
        let (baseWithMod, tone) =
            match ReverseToneTable.TryFind(lower) with
            | Some (v, t) -> (v, t)
            | None -> (lower, Tone.None)
        
        let (baseChar, modifier) =
            match ReverseModifierTable.TryFind(baseWithMod) with
            | Some (b, m) -> (b, m)
            | None -> (baseWithMod, Modifier.None)
            
        (baseChar, modifier, tone)

    /// Ghép lại nguyên âm từ (Ký tự gốc, Modifier, Tone) thành ký tự Unicode NFC
    let composeChar (baseChar: char, modifier: Modifier, tone: Tone) : char option =
        let lowerBase = Char.ToLowerInvariant(baseChar)
        let charWithMod =
            if modifier = Modifier.None then
                Some lowerBase
            else
                ModifierTable.TryFind(lowerBase, modifier)

        match charWithMod with
        | Some cm -> ToneTable.TryFind(cm, tone)
        | None -> None
```

