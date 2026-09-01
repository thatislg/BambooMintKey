module BambooMintKey.Core.Domain.UnicodeTables

open System
open BambooMintKey.Core.Domain.Types

module UnicodeTables =

    // =========================================================================
    // 1. TẬP HỢP PHÂN LOẠI KÝ TỰ (CHARACTER SETS)
    // =========================================================================

    // Tập hợp các nguyên âm cơ bản không dấu
    let BaseVowels = set [ 'a'; 'e'; 'i'; 'o'; 'u'; 'y' ]

    // Tập hợp toàn bộ 12 nguyên âm tiếng Việt (cả nguyên bản và có mũ/móc)
    let AllVietnameseVowels = 
        set [ 'a'; 'ă'; 'â'; 'e'; 'ê'; 'i'; 'o'; 'ô'; 'ơ'; 'u'; 'ư'; 'y' ]

    // Tập hợp các phụ âm đầu hợp lệ trong ngữ pháp tiếng Việt (đã chuẩn hóa chữ thường)
    let ValidInitialConsonants = 
        set [
            "b"; "c"; "d"; "đ"; "g"; "gh"; "gi"; "h"; "k"; "kh";
            "l"; "m"; "n"; "ng"; "ngh"; "nh"; "p"; "ph"; "q"; "qu";
            "r"; "s"; "t"; "th"; "tr"; "v"; "x"
        ]

    // Tập hợp các phụ âm cuối hợp lệ trong ngữ pháp tiếng Việt
    let ValidFinalConsonants = 
        set [ "c"; "ch"; "m"; "n"; "ng"; "nh"; "p"; "t" ]

    // =========================================================================
    // 2. BẢNG ÁNH XẠ DẤU PHỤ - MODIFIER MAPPING (MŨ, MÓC, TRĂNG, GẠCH)
    // =========================================================================

    // Ánh xạ từ (Ký tự gốc + Modifier) -> Ký tự có dấu phụ NFC
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

    // Ánh xạ ngược từ Ký tự có dấu phụ NFC -> (Ký tự gốc, Modifier)
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

    // Bảng ánh xạ: (Nguyên âm có/không mũ/móc + Tone) -> Ký tự NFC hoàn chỉnh
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

    // Ánh xạ ngược: Ký tự NFC có dấu -> (Nguyên âm gốc dạng mũ/móc, Tone)
    let ReverseToneTable : Map<char, char * Tone> =
        ToneTable
        |> Map.toList
        |> List.map (fun ((vowel, tone), resChar) -> (resChar, (vowel, tone)))
        |> Map.ofList

    // =========================================================================
    // 4. CÁC HÀM TRỢ GIÚP TRA CỨU NHANH (FAST LOOKUP HELPERS)
    // =========================================================================

    // Kiểm tra xem một ký tự có phải là nguyên âm tiếng Việt (kể cả có dấu thanh/mũ) hay không
    let isVowel (c: char) : bool =
        let lower = Char.ToLowerInvariant(c)
        AllVietnameseVowels.Contains(lower) || ReverseToneTable.ContainsKey(lower)

    // Lấy dấu thanh hiện tại của một ký tự đơn
    let extractTone (c: char) : Tone =
        let lower = Char.ToLowerInvariant(c)
        match ReverseToneTable.TryFind(lower) with
        | Some (_, tone) -> tone
        | Option.None -> Tone.None

    // Chuẩn hóa ký tự có dấu về nguyên âm gốc và tách dấu phụ/dấu thanh
    let decomposeChar (c: char) : char * Modifier * Tone =
        let lower = Char.ToLowerInvariant(c)
        let baseWithMod, tone =
            match ReverseToneTable.TryFind(lower) with
            | Some (v, t) -> (v, t)
            | Option.None -> (lower, Tone.None)
        
        let baseChar, modifier =
            match ReverseModifierTable.TryFind(baseWithMod) with
            | Some (b, m) -> (b, m)
            | Option.None -> (baseWithMod, Modifier.None)
            
        (baseChar, modifier, tone)

    // Ghép lại nguyên âm từ (Ký tự gốc, Modifier, Tone) thành ký tự Unicode NFC
    let composeChar (baseChar: char, modifier: Modifier, tone: Tone) : char option =
        let lowerBase = Char.ToLowerInvariant(baseChar)
        let charWithMod =
            if modifier = Modifier.None then
                Some lowerBase
            else
                ModifierTable.TryFind(lowerBase, modifier)

        match charWithMod with
        | Some cm -> ToneTable.TryFind(cm, tone)
        | Option.None -> Option.None