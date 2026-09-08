// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain.Types
open BambooMintKey.Core.Domain.UnicodeTables

/// <summary>
/// Module xử lý tính năng Bỏ Dấu Tự Do (Free Tone Placement).
/// Cho phép người dùng gõ phím dấu thanh ở bất kỳ vị trí nào trong từ
/// (ví dụ: phari -> phải, hoacs -> hoác, phar + i -> phải),
/// tự động bóc tách dấu, phân giải ưu tiên và áp dụng đúng vị trí theo quy chuẩn thanh điệu.
/// </summary>
module FreeTonePlacement =

    /// <summary>
    /// Danh sách toàn bộ phụ âm đầu hợp lệ trong tiếng Việt,
    /// sắp xếp theo thứ tự độ dài giảm dần để ưu tiên khớp cụm dài trước (VD: 'ngh' trước 'ng').
    /// </summary>
    let private validInitials = [
        "ngh"; "ng"; "nh"; "ch"; "gh"; "gi"; "kh"; "ph"; "qu"; "th"; "tr";
        "b"; "c"; "d"; "đ"; "g"; "h"; "k"; "l"; "m"; "n"; "p"; "r"; "s"; "t"; "v"; "x"
    ]

    /// <summary>
    /// Kiểm tra xem một ký tự có phải là phím dấu thanh Telex (s, f, r, x, j) hay không (không phân biệt hoa/thường).
    /// </summary>
    let isToneKeyChar (c: char) : bool =
        match Char.ToLowerInvariant c with
        | 's' | 'f' | 'r' | 'x' | 'j' -> true
        | _ -> false

    /// <summary>
    /// Cấu trúc lưu kết quả bóc tách chuỗi phím thô người dùng đã gõ.
    /// </summary>
    type ExtractionResult = {
        /// Phụ âm đầu được bảo vệ (không bị nhầm lẫn thành phím dấu)
        ProtectedInitial: string
        /// Chuỗi nền sau khi đã loại bỏ toàn bộ các phím dấu thanh tự do
        BaseString: string
        /// Danh sách các phím dấu thanh tự do thu thập được kèm vị trí: (ký tự dấu, chỉ số index)
        CandidateTones: (char * int) list
        /// Cờ đánh dấu có sự lặp lại của 2 phím dấu liên tiếp (để kích hoạt Repeat-Key Undo)
        HasRepeatedToneUndo: bool
    }

    /// <summary>
    /// Bóc tách chuỗi phím thô thành phụ âm đầu bảo vệ, chuỗi nền và danh sách các phím dấu tiềm năng.
    /// Quy tắc quan trọng:
    /// 1. Các phụ âm đầu như 's', 'r', 'x' (trong 'sang', 'rung', 'xuan') thuộc vùng bảo vệ, không bao giờ bị bóc tách làm dấu.
    /// 2. Phím dấu thanh chỉ có thể xuất hiện TRÊN hoặc SAU nguyên âm. Nếu chưa gặp nguyên âm nào trong từ,
    ///    ký tự phụ âm (kể cả s, f, r, x, j như 's' trong 'tsaon') là phụ âm bất quy tắc, KHÔNG PHẢI phím dấu thanh.
    /// </summary>
    let extractTokens (rawKeys: char list) : ExtractionResult =
        let rawStr = String(Array.ofList rawKeys)
        let lowerStr = rawStr.ToLowerInvariant()

        // 1. Nhận diện phụ âm đầu thuộc vùng bảo vệ
        let matchedInitial =
            validInitials
            |> List.tryFind (fun init -> lowerStr.StartsWith init)
            |> Option.defaultValue ""

        let initialLen = matchedInitial.Length
        let protectedInit = if initialLen > 0 then rawStr[0 .. initialLen - 1] else ""
        let remainder = rawKeys |> List.skip initialLen

        // 2. Duyệt qua phần còn lại để gom phím dấu và tạo chuỗi nền
        let mutable baseChars = []
        let mutable candidateTones = []
        let mutable lastToneChar = Option.None
        let mutable hasRepeatUndo = false
        let mutable hasSeenVowel = false

        for i, c in remainder |> List.indexed do
            let absIdx = initialLen + i
            let lowerC = Char.ToLowerInvariant c

            // Kiểm tra xem ký tự này có phải nguyên âm hoặc ký tự modifier nguyên âm không
            if isVowel lowerC || lowerC = 'w' then
                hasSeenVowel <- true
                baseChars <- baseChars @ [ c ]
            elif isToneKeyChar lowerC && hasSeenVowel then
                // Chỉ nhận là phím dấu thanh nếu ĐÃ gặp ít nhất một nguyên âm trước đó
                match lastToneChar with
                | Some prevChar when prevChar = lowerC ->
                    hasRepeatUndo <- true
                | _ -> ()

                lastToneChar <- Some lowerC
                candidateTones <- candidateTones @ [ (c, absIdx) ]
            else
                baseChars <- baseChars @ [ c ]

        let baseStr = protectedInit + String(Array.ofList baseChars)

        {
            ProtectedInitial = protectedInit
            BaseString = baseStr
            CandidateTones = candidateTones
            HasRepeatedToneUndo = hasRepeatUndo
        }

    /// <summary>
    /// Thẩm định quy tắc ngữ âm chính tả tiếng Việt để ngăn chặn việc biến đổi sai các từ tiếng Anh.
    /// Ví dụ: Trong tiếng Việt, phụ âm 'c' không bao giờ đi trực tiếp với 'e', 'ê', 'i' hoặc cụm 'oe'
    /// (phải viết là 'k' hoặc 'qu' như 'ke', 'kê', 'que'). Do đó 'core' -> chuỗi nền 'coe' là không hợp lệ.
    /// <summary>
    /// Kiểm tra ràng buộc chính tả tiếng Việt dựa trên bảng ma trận ngữ âm học EnglishProtection (Tuyến 1):
    /// - Ngăn chặn các từ tiếng Anh (như 'core', 'more', 'first', 'start'...) bị biến đổi thành âm tiết dị dạng.
    /// </summary>
    let private isValidVietnamesePhonotactics (syllable: Syllable) : bool =
        EnglishProtection.isValidVietnameseSyllableStructure syllable

    /// <summary>
    /// Chuẩn hóa và áp dụng bỏ dấu tự do lên chuỗi phím thô:
    /// 1. Tách phụ âm đầu bảo vệ, chuỗi nền và candidate tone keys (chỉ sau khi đã có nguyên âm).
    /// 2. Nếu có dấu hiệu lặp phím hủy dấu -> trả về None để phục hồi chuỗi thô.
    /// 3. Parse chuỗi nền bằng SyllableParser (chuẩn hóa chữ thường để nhận diện inline modifiers).
    /// 4. Thẩm định ngữ âm chính tả tiếng Việt.
    /// 5. Áp dụng dấu thanh hiệu lực cuối cùng (quy tắc Last-Win override).
    /// </summary>
    let tryNormalizeFreeTone 
        (rawKeys: char list) 
        (toneStyle: TonePlacementStyle) 
        (allowRepeatUndo: bool) 
        : (Syllable * LetterCase) option =

        let extraction = extractTokens rawKeys

        // Nếu người dùng gõ lặp phím dấu và bật cờ Undo -> Báo None để engine khôi phục chuỗi thô
        if allowRepeatUndo && extraction.HasRepeatedToneUndo then
            None
        // Nếu không có bất kỳ phím dấu thanh tự do nào -> Trả về None để luồng xử lý chuẩn tiếp quản
        elif extraction.CandidateTones.IsEmpty then
            None
        else
            // Phân tích chuỗi nền qua SyllableParser (sử dụng chuỗi thường để giải quyết đầy đủ modifier inline như 'ee' -> 'ê')
            let normalizedBaseInput = extraction.BaseString.ToLowerInvariant()
            match SyllableParser.parse normalizedBaseInput with
            | None ->
                // Chuỗi nền không tạo thành âm tiết tiếng Việt hợp lệ (VD: 'phr' chưa có nguyên âm, hoặc từ tiếng Anh)
                None
            | Some baseSyllable ->
                // Kiểm tra âm tiết nền phải có nguyên âm
                if String.IsNullOrEmpty baseSyllable.VowelNucleus then
                    None
                // Kiểm tra ràng buộc ngữ âm tiếng Việt
                elif not (isValidVietnamesePhonotactics baseSyllable) then
                    None
                else
                    // Lấy phím dấu cuối cùng được gõ (Quy tắc Last-Win: phím dấu mới nhất có hiệu lực)
                    let lastToneKeyChar, _ = extraction.CandidateTones |> List.last
                    let targetTone = ToneRules.keyToTone lastToneKeyChar |> Option.defaultValue Tone.None

                    // Áp dụng dấu thanh lên âm tiết nền theo kiểu đặt dấu (Modern / Traditional)
                    let tonedSyllable = ToneRules.applyTone targetTone toneStyle baseSyllable
                    let detectedCase = WordBuffer.detectCase rawKeys
                    Some (tonedSyllable, detectedCase)
