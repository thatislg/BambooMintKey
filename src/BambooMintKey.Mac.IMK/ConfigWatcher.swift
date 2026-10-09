// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Foundation

/// Theo dõi THƯ MỤC cấu hình và nạp lại `config.json` vào `InputSettings` khi thay đổi.
///
/// Vì UI.Mac/StatusBar ghi `config.json` bằng ghi nguyên tử (rename tệp tạm -> config.json),
/// phải theo dõi **thư mục** (không phải file) để bắt được sự kiện rename — file descriptor
/// của file cũ không nhận biết inode mới sau khi đổi tên.
final class ConfigWatcher {

    private var source: DispatchSourceFileSystemObject?
    private let configDir: URL

    init() {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        configDir = base.appendingPathComponent("BambooMintKey", isDirectory: true)
    }

    /// Đọc và áp dụng cấu hình hiện tại một lần (khởi động).
    func loadOnce() {
        applyConfig()
    }

    /// Bắt đầu theo dõi thay đổi của thư mục chứa config.json.
    func start() {
        loadOnce()

        try? FileManager.default.createDirectory(at: configDir, withIntermediateDirectories: true)

        let fd = open(configDir.path, O_EVTONLY)
        guard fd >= 0 else { return }

        let source = DispatchSource.makeFileSystemObjectSource(
            fileDescriptor: fd,
            eventMask: [.write, .rename, .delete],
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
        let url = configDir.appendingPathComponent("config.json")
        guard let data = try? Data(contentsOf: url),
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
