module BambooMintKey.Core.Domain.EngineConfig

open BambooMintKey.Core.Domain.Types

// Cấu hình hoạt động của Engine Telex
type EngineConfig = {
    // Bật/tắt chế độ gõ tiếng Việt (True: V, False: E)
    IsEnabled: bool
    // Tự động phục hồi từ gốc khi gõ từ sai ngữ pháp tiếng Việt (Fallback tiếng Anh)
    AutoRestoreEnglishWords: bool
    // Cho phép gõ lặp dấu để khôi phục ký tự thô (ví dụ: 'ss' -> 's', 'aa' -> 'a')
    AllowRepeatKeyUndo: bool
    // Cho phép phím 'w' đứng đầu từ biến thành 'ư' (True: w -> ư, False: w -> w)
    AllowLeadingWAsU: bool
    // Quy chuẩn đặt vị trí dấu thanh (Mới: hòa, xòe / Cũ: hoá, xoè)
    ToneStyle: TonePlacementStyle
}
with
    // Cấu hình mặc định của BambooMintKey
    static member Default = {
        IsEnabled = true
        AutoRestoreEnglishWords = true
        AllowRepeatKeyUndo = true
        AllowLeadingWAsU = true
        ToneStyle = TonePlacementStyle.Modern
    }