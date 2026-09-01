module BambooMintKey.Core.Domain.EngineConfig

// Chuẩn đặt dấu thanh cho nguyên âm mở (oa, oe, uy)
type TonePlacementStyle =
    // Chuẩn mới: òa, óa, úy, xòa
    | Modern
    // Chuẩn cũ: oà, oá, uý, xoà
    | Traditional

// Cấu hình hoạt động của Engine Telex
type EngineConfig = {
    // Bật/tắt chế độ gõ tiếng Việt (True: V, False: E)
    IsEnabled: bool
    // Kiểu đặt dấu thanh (Modern vs Traditional)
    ToneStyle: TonePlacementStyle
    // Tự động phục hồi từ gốc khi gõ từ sai ngữ pháp tiếng Việt (Fallback tiếng Anh)
    AutoRestoreEnglishWords: bool
    // Cho phép gõ lặp dấu để khôi phục ký tự thô (ví dụ: 'ss' -> 's', 'aa' -> 'a')
    AllowRepeatKeyUndo: bool
    // Cho phép phím 'w' đứng đầu từ biến thành 'ư' (True: w -> ư, False: w -> w)
    AllowLeadingWAsU: bool
}
with
    // Cấu hình mặc định của BambooMintKey
    static member Default = {
        IsEnabled = true
        ToneStyle = TonePlacementStyle.Modern
        AutoRestoreEnglishWords = true
        AllowRepeatKeyUndo = true
        AllowLeadingWAsU = true
    }