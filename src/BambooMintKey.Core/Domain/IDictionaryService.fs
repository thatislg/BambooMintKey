// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.Core.Domain

/// <summary>
/// Giao diện trừu tượng cho dịch vụ từ điển (Phase 8 / M3.1).
/// Tách biệt dữ liệu từ điển khỏi luồng xử lý Telex để dễ mở rộng và kiểm thử độc lập
/// trên cả Windows (TSF) lẫn Linux (Fcitx5 C-ABI).
/// </summary>
type IDictionaryService =
    /// Kiểm tra một âm tiết tiếng Việt có hợp lệ (tồn tại trong từ điển âm tiết chuẩn).
    abstract IsValidVietnameseSyllable : string -> bool

    /// Kiểm tra một từ có phải từ tiếng Anh thông dụng (phục vụ English Protection / Backtracking).
    abstract IsLikelyEnglishWord : string -> bool

    /// Nạp thêm từ điển người dùng mở rộng (custom wordlist) từ tầng ngoài.
    abstract MergeCustomWords : string seq -> unit
