// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Foundation

/// Quản lý cấu hình chia sẻ giữa Menu Bar app và IMK Service.
///
/// Nguồn chân lý là tệp `~/Library/Application Support/BambooMintKey/config.json`
/// (schema camelCase, khớp với UI.Mac và C-ABI `bmk_load_config_json`). Trạng thái
/// V/E và các tùy chọn gõ được đồng bộ hai chiều giữa Menu Bar và IMK qua
/// `NSDistributedNotificationCenter`.
enum ConfigStore {

    /// Tên thông báo phân tán dùng để đồng bộ trạng thái giữa các tiến trình.
    static let modeChangedNotification = Notification.Name("com.bamboomintkey.modeChanged")

    /// Toàn bộ tùy chọn gõ (khớp schema config.json của UI.Mac).
    struct Settings {
        var isVietnamese: Bool = true
        var toneStyle: Int = 0            // 0: kiểu mới (hòa), 1: kiểu cũ (hoà)
        var allowFreeTonePlacement: Bool = true
        var enableEnglishBacktracking: Bool = true
        var allowRepeatKeyUndo: Bool = true
        var allowLeadingWAsU: Bool = false
    }

    /// Đường dẫn thư mục cấu hình: ~/Library/Application Support/BambooMintKey/
    static var configDir: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return base.appendingPathComponent("BambooMintKey", isDirectory: true)
    }

    /// Đường dẫn tệp config.json.
    static var configURL: URL {
        configDir.appendingPathComponent("config.json")
    }

    /// Đọc toàn bộ tùy chọn từ config.json (mặc định nếu chưa có tệp).
    static func readSettings() -> Settings {
        var s = Settings()
        guard let data = try? Data(contentsOf: configURL),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return s
        }
        s.isVietnamese = json["isVietnameseMode"] as? Bool ?? s.isVietnamese
        if let n = json["toneStyle"] as? NSNumber { s.toneStyle = n.intValue }
        s.allowFreeTonePlacement = json["allowFreeTonePlacement"] as? Bool ?? s.allowFreeTonePlacement
        s.enableEnglishBacktracking = json["enableEnglishBacktracking"] as? Bool ?? s.enableEnglishBacktracking
        s.allowRepeatKeyUndo = json["allowRepeatKeyUndo"] as? Bool ?? s.allowRepeatKeyUndo
        s.allowLeadingWAsU = json["allowLeadingWAsU"] as? Bool ?? s.allowLeadingWAsU
        return s
    }

    /// Đọc trạng thái V/E hiện tại (tiện ích ngắn).
    static func readVietnameseMode() -> Bool {
        readSettings().isVietnamese
    }

    /// Ghi toàn bộ tùy chọn nguyên tử (ghi tạm rồi rename) để IMK/UI.Mac đọc an toàn.
    @discardableResult
    static func writeSettings(_ s: Settings) -> Bool {
        do {
            try FileManager.default.createDirectory(at: configDir, withIntermediateDirectories: true)

            var json: [String: Any] = [:]
            if let data = try? Data(contentsOf: configURL),
               let existing = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                json = existing
            }
            json["isVietnameseMode"] = s.isVietnamese
            json["toneStyle"] = s.toneStyle
            json["allowFreeTonePlacement"] = s.allowFreeTonePlacement
            json["enableEnglishBacktracking"] = s.enableEnglishBacktracking
            json["allowRepeatKeyUndo"] = s.allowRepeatKeyUndo
            json["allowLeadingWAsU"] = s.allowLeadingWAsU

            let data = try JSONSerialization.data(withJSONObject: json, options: [.prettyPrinted, .sortedKeys])
            let tmp = configDir.appendingPathComponent("config.json.tmp.\(UUID().uuidString)")
            try data.write(to: tmp, options: .atomic)
            _ = try FileManager.default.replaceItemAt(configURL, withItemAt: tmp)
            return true
        } catch {
            return false
        }
    }
}
