// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Foundation

/// Cầu nối C-ABI tới thư viện lõi NativeAOT `BambooMintKeyCore.dylib`.
///
/// Thư viện được liên kết động qua tên Mach-O (được `install_name_tool` đặt
/// thành `@loader_path/BambooMintKeyCore.dylib` khi đóng gói bundle), nhờ đó
/// Swift có thể gọi trực tiếp các hàm export `bmk_*` bằng quy ước C (cdecl).
///
/// Quy ước sở hữu bộ nhớ (theo 010_02_Architecture_and_CABI_Design.md mục 4):
/// - Bộ đệm preedit/commit thuộc quyền quản lý của lõi; Swift chỉ đọc và chép
///   sang `String`, tuyệt đối không giải phóng con trỏ.
/// - Handle ngữ cảnh do `bmk_context_create` cấp phát, phải giải phóng đúng một
///   lần bằng `bmk_context_free` khi phiên gõ kết thúc.
enum CABIActionCode: Int32 {
    /// Nhường phím cho ứng dụng.
    case passThrough = 0
    /// Nuốt phím (engine hiện chưa phát sinh).
    case consume = 1
    /// Cập nhật chuỗi preedit đang gõ.
    case updatePreedit = 2
    /// Chốt từ và xóa preedit.
    case commitString = 3
}

/// Cờ cấu hình đặt dấu (toneStyle): 0 = kiểu mới (hòa), 1 = kiểu cũ (hoà).
enum CABIToneStyle: Int32 {
    case modern = 0
    case traditional = 1
}

enum CABIBridge {
    // =========================================================================
    // Lifecycle
    // =========================================================================

    /// Trả về phiên bản ABI.
    @_silgen_name("bmk_version")
    static func version() -> Int32

    /// Cấp phát một context mới; trả về handle (khác 0 nếu thành công).
    @_silgen_name("bmk_context_create")
    static func contextCreate() -> UnsafeMutableRawPointer?

    /// Giải phóng bộ nhớ unmanaged của context. An toàn với handle = nil.
    @_silgen_name("bmk_context_free")
    static func contextFree(_ handle: UnsafeMutableRawPointer?)

    /// Đặt lại trạng thái về rỗng và xóa hai bộ đệm.
    @_silgen_name("bmk_context_reset")
    static func contextReset(_ handle: UnsafeMutableRawPointer?)

    // =========================================================================
    // Key processing
    // =========================================================================

    /// Xử lý một ký tự Unicode (code point 32-bit), trả về mã hành động.
    @_silgen_name("bmk_process_key")
    static func processKey(_ handle: UnsafeMutableRawPointer?, _ unicodeChar: UInt32) -> Int32

    /// Xử lý phím Backspace, trả về mã hành động.
    @_silgen_name("bmk_process_backspace")
    static func processBackspace(_ handle: UnsafeMutableRawPointer?) -> Int32

    /// Xử lý ký tự ngắt từ (space, enter, dấu câu...), trả về mã hành động.
    @_silgen_name("bmk_process_wordbreak")
    static func processWordbreak(_ handle: UnsafeMutableRawPointer?, _ breakChar: UInt32) -> Int32

    // =========================================================================
    // UTF-8 buffer extraction (read-only, null-terminated)
    // =========================================================================

    @_silgen_name("bmk_get_preedit_text")
    static func getPreeditText(_ handle: UnsafeMutableRawPointer?) -> UnsafePointer<CChar>?

    @_silgen_name("bmk_get_commit_text")
    static func getCommitText(_ handle: UnsafeMutableRawPointer?) -> UnsafePointer<CChar>?

    @_silgen_name("bmk_get_preedit_length")
    static func getPreeditLength(_ handle: UnsafeMutableRawPointer?) -> Int32

    // =========================================================================
    // Configuration
    // =========================================================================

    @_silgen_name("bmk_set_options")
    static func setOptions(
        _ handle: UnsafeMutableRawPointer?,
        _ isEnabled: Int32,
        _ toneStyle: Int32,
        _ allowRepeatUndo: Int32,
        _ allowLeadingW: Int32,
        _ allowFreeTone: Int32,
        _ enableVietnameseDictionary: Int32,
        _ enableEnglishBacktracking: Int32
    )

    /// Nạp cấu hình từ chuỗi JSON UTF-8. Trả về 0 nếu thành công, -1 nếu lỗi.
    @_silgen_name("bmk_load_config_json")
    static func loadConfigJson(_ handle: UnsafeMutableRawPointer?, _ jsonUtf8: UnsafePointer<CChar>?) -> Int32

    // =========================================================================
    // Helpers
    // =========================================================================

    /// Chép chuỗi UTF-8 từ bộ đệm preedit sang `String` (rỗng nếu không có).
    static func preeditString(_ handle: UnsafeMutableRawPointer?) -> String {
        guard let ptr = getPreeditText(handle) else { return "" }
        return String(cString: ptr)
    }

    /// Chép chuỗi UTF-8 từ bộ đệm commit sang `String` (rỗng nếu không có).
    static func commitString(_ handle: UnsafeMutableRawPointer?) -> String {
        guard let ptr = getCommitText(handle) else { return "" }
        return String(cString: ptr)
    }

    /// Áp dụng bộ tùy chọn gõ mặc định (tiếng Việt, kiểu đặt dấu mới, lặp phím undo).
    static func applyDefaultOptions(_ handle: UnsafeMutableRawPointer?) {
        setOptions(
            handle,
            1,                          // isEnabled = tiếng Việt
            CABIToneStyle.modern.rawValue,
            1,                          // allowRepeatUndo
            0,                          // allowLeadingWAsU
            1,                          // allowFreeTonePlacement
            1,                          // enableVietnameseDictionary
            1                           // enableEnglishBacktracking
        )
    }
}
