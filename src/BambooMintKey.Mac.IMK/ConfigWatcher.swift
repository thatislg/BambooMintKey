// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Foundation

/// Theo dõi tệp config.json và nạp lại cấu hình vào `InputSettings` khi có thay đổi.
///
/// Khi người dùng bấm Lưu trên UI.Mac, tệp `config.json` được ghi nguyên tử
/// (rename). Trình theo dõi phát hiện sự kiện và nạp lại toàn bộ tùy chọn gõ
/// (V/E, kiểu dấu, bỏ dấu tự do, khôi phục tiếng Anh, lặp phím undo) ngay lập tức,
/// không cần khởi động lại hay logout.
final class ConfigWatcher {

    private var source: DispatchSourceFileSystemObject?
    private let configURL: URL

    init() {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        configURL = base.appendingPathComponent("BambooMintKey").appendingPathComponent("config.json")
    }

    /// Đọc và áp dụng cấu hình hiện tại một lần (khởi động).
    func loadOnce() {
        applyConfig()
    }

    /// Bắt đầu theo dõi thay đổi của tệp config.json.
    func start() {
        loadOnce()

        let fd = open(configURL.path, O_EVTONLY)
        guard fd >= 0 else { return }

        let source = DispatchSource.makeFileSystemObjectSource(
            fileDescriptor: fd,
            eventMask: [.write, .delete, .rename],
            queue: .main
        )
        source.setEventHandler { [weak self] in
            self?.applyConfig()
        }
        source.setCancelHandler {
            close(fd)
        }
        source.resume()
        self.source = source
    }

    deinit {
        source?.cancel()
    }

    /// Đọc config.json và cập nhật `InputSettings`.
    private func applyConfig() {
        guard let data = try? Data(contentsOf: configURL),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return
        }

        InputSettings.isVietnamese = json["isVietnameseMode"] as? Bool ?? InputSettings.isVietnamese
        if let n = json["toneStyle"] as? NSNumber {
            InputSettings.toneStyle = n.int32Value
        }
        InputSettings.freeTone = json["allowFreeTonePlacement"] as? Bool ?? InputSettings.freeTone
        InputSettings.englishBacktrack = json["enableEnglishBacktracking"] as? Bool ?? InputSettings.englishBacktrack
        InputSettings.repeatUndo = json["allowRepeatKeyUndo"] as? Bool ?? InputSettings.repeatUndo

        // Báo trạng thái V/E mới cho Menu Bar app (nếu thay đổi từ UI.Mac).
        DistributedNotificationCenter.default().postNotificationName(
            InputSettings.modeChangedNotification,
            object: nil,
            userInfo: ["isVietnameseMode": InputSettings.isVietnamese],
            deliverImmediately: true
        )
    }
}
