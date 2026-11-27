// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Domain

/// Một mục trong từ điển ký hiệu: từ khóa đã chuẩn hóa (không dấu, chữ thường, không khoảng trắng)
/// ánh xạ sang danh sách ký tự Unicode theo thứ tự ưu tiên.
type SymbolEntry = {
    Keyword: string
    Symbols: string array
}

/// Phiên tra cứu ký hiệu đang hoạt động sau chuỗi kích hoạt "::".
type SymbolSession = {
    /// Chuỗi người dùng thực gõ sau "::" (giữ nguyên hoa/thường để Esc trả về đúng nguyên văn).
    RawQuery: string
    /// Chuỗi từ khóa đã chuẩn hóa không dấu, chữ thường.
    NormalizedQuery: string
    /// Danh sách ký tự ứng viên khớp hiện tại.
    Candidates: string array
    /// Chỉ số ứng viên đang được chọn trong Candidates (0-based).
    SelectedIndex: int
}

/// Trang ứng viên hiện tại (tối đa 7 mục/trang) phục vụ cửa sổ popup chọn ký hiệu chiều dọc.
type CandidatePage = {
    Items: string array
    SelectedInPage: int
    PageIndex: int
    TotalPages: int
    TotalCount: int
    RawQuery: string
}

/// Máy trạng thái của chế độ gõ ký hiệu / icon (Phase 13 Sample).
[<RequireQualifiedAccess>]
type SymbolModeState =
    /// Không ở trong chế độ ký hiệu.
    | Idle
    /// Đã nhận 1 dấu ':' hợp lệ ở đầu từ, chờ phím tiếp theo kiểm tra xem có thành "::" hay không.
    | PendingColon
    /// Đã nhận đủ "::", chờ chữ cái đầu tiên của từ khóa.
    | TriggerReady
    /// Đang thu thập từ khóa và hiển thị inline preview.
    | Collecting of SymbolSession
