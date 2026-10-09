// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Foundation

/// Quản lý cấu hình chia sẻ giữa Menu Bar app và IMK Service.
///
/// Nguồn chân lý là tệp `~/Library/Application Support/BambooMintKey/config.json`
/// (schema camelCase, khớp với UI.Mac và C-ABI `bmk_load_config_json`). Trạng thái
/// V/E được đồng bộ hai chiều giữa Menu Bar và IMK qua `NSDistributedNotificationCenter`.
enum ConfigStore {

    /// Tên thông báo phân tán dùng để đồng bộ trạng thái V/E giữa các tiến trình.
    static let modeChangedNotification = Notification.Name("com.bamboomintkey.modeChanged")

    /// Đường dẫn thư mục cấu hình: ~/Library/Application Support/BambooMintKey/
    static var configDir: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return base.appendingPathComponent("BambooMintKey", isDirectory: true)
    }

    /// Đường dẫn tệp config.json.
    static var configURL: URL {
        configDir.appendingPathComponent("config.json")
    }

    /// Đọc trạng thái V/E hiện tại (mặc định true = tiếng Việt).
    static func readVietnameseMode() -> Bool {
        guard let data = try? Data(contentsOf: configURL),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return true
        }
        return json["isVietnameseMode"] as? Bool ?? true
    }

    /// Ghi trạng thái V/E nguyên tử (ghi tạm rồi rename) để IMK đọc an toàn.
    /// Trả về true nếu ghi thành công.
    @discardableResult
    static func writeVietnameseMode(_ enabled: Bool) -> Bool {
        do {
            try FileManager.default.createDirectory(at: configDir, withIntermediateDirectories: true)

            // Đọc giữ nguyên các trường khác, chỉ cập nhật isVietnameseMode.
            var json: [String: Any] = [:]
            if let data = try? Data(contentsOf: configURL),
               let existing = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                json = existing
            }
            json["isVietnameseMode"] = enabled

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
