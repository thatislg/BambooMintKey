// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
module BambooMintKey.Core.Domain.Types

// Biểu diễn 5 dấu thanh trong tiếng Việt + Thanh ngang (None)
[<RequireQualifiedAccess>]
type Tone =
    | None
    | Acute     // Sắc: s
    | Grave     // Huyền: f
    | Hook      // Hỏi: r
    | Tilde     // Ngã: x
    | Dot       // Nặng: j

// Biểu diễn các dấu phụ (Mũ, Móc, Trăng, Gạch ngang)
[<RequireQualifiedAccess>]
type Modifier =
    | None
    | Hat       // Mũ: â, ê, ô (aa, ee, oo)
    | Horn      // Móc: ơ, ư (ow, uw, w)
    | Breve     // Trăng: ă (aw)
    | DBar      // Gạch ngang: đ (dd)

// Quy chuẩn đặt vị trí dấu thanh
[<RequireQualifiedAccess>]
type TonePlacementStyle =
    | Modern        // Kiểu mới: hòa, thúy, xòe (dấu trên nguyên âm đầu tiên trong cụm mở)
    | Traditional   // Kiểu cũ: hoá, thuý, xoè (dấu trên nguyên âm thứ 2 trong cụm mở)

// Định dạng viết hoa / viết thường của từ để bảo toàn sau biến đổi
type LetterCase =
    | Lower                 // việt
    | Upper                 // VIỆT
    | Title                 // Việt
    | Mixed of bool list    // Mảng boolean lưu trạng thái hoa/thường từng ký tự

// Phân loại phím đầu vào được gửi từ TSF Native Bridge
[<RequireQualifiedAccess>]
type KeyInput =
    | Char of char          // Ký tự bảng chữ cái (a-z, A-Z)
    | Backspace             // Phím xóa lùi
    | WordBreak of char     // Ký tự ngắt từ: Space, Enter, Tab, Dấu câu (. , ; : ! ?...)
    | NonCharacter          // Các phím chức năng không làm thay đổi từ (Mũi tên, Home, End...)

// Cấu trúc phân tích âm tiết của một từ tiếng Việt
type Syllable = {
    InitialConsonant: string
    VowelNucleus: string
    FinalConsonant: string
    Tone: Tone
    Modifiers: (char * Modifier) list
}

// Trạng thái đầy đủ của một từ đang nằm trong bộ đệm gõ (Word Buffer)
type WordState = {
    // Danh sách các ký tự phím thô người dùng đã nhấn theo thứ tự thời gian
    RawKeys: char list
    // Chuỗi văn bản tiếng Việt đã được xử lý (NFC Unicode)
    TransformedText: string
    // Phân tích cấu trúc âm tiết hiện tại
    Syllable: Syllable option
    // Định dạng viết hoa/thường ban đầu
    Case: LetterCase
    // Cờ đánh dấu từ này có vi phạm cấu trúc tiếng Việt hay không (để fallback tiếng Anh)
    IsInvalidVietnamese: bool
}
with
    // Trạng thái rỗng khởi tạo ban đầu
    static member Empty = {
        RawKeys = []
        TransformedText = ""
        Syllable = Option.None
        Case = LetterCase.Lower
        IsInvalidVietnamese = false
    }

// Lệnh kết quả trả về từ Engine cho lớp TSF NativeBridge thực thi
[<RequireQualifiedAccess>]
type EngineAction =
    // Tiếp tục phiên composition, thay thế vùng gõ hiện tại bằng text mới
    | UpdateComposition of newText: string
    // Chốt từ hoàn tất (khi gặp phím ngắt), xóa gạch chân và giải phóng buffer
    | Commit of committedText: string
    // Nhả phím cho hệ điều hành/ứng dụng tự xử lý (không nuốt phím)
    | PassThrough