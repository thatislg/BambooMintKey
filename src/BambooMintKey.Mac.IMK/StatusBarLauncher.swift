// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import AppKit

/// Quản lý vòng đời và tự động khởi chạy BambooMintKeyStatusBar.app (icon EV).
///
/// Đảm bảo bất cứ khi nào BambooMintKey (IMK) được macOS khởi động hoặc kích hoạt
/// phiên gõ, app StatusBar luôn chạy ngầm để hiển thị icon E/V khi chọn B,
/// và tự động ẩn khi chuyển sang bộ gõ khác (ABC/VI).
enum StatusBarLauncher {

    static let bundleIdentifier = "com.bamboomintkey.statusbar"

    /// Kiểm tra và tự động khởi chạy app StatusBar nếu chưa chạy.
    static func ensureRunning() {
        let running = NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier)
        if !running.isEmpty {
            return
        }

        let home = NSHomeDirectory()
        var candidateURLs: [URL] = []

        // 1. Bên trong bundle hiện tại (Contents/SharedSupport)
        if let sharedSupport = Bundle.main.sharedSupportURL {
            candidateURLs.append(sharedSupport.appendingPathComponent("BambooMintKeyStatusBar.app"))
        }
        candidateURLs.append(
            Bundle.main.bundleURL.appendingPathComponent("Contents/SharedSupport/BambooMintKeyStatusBar.app")
        )

        // 2. Trong bundle cài đặt chuẩn ~/Library/Input Methods/
        let imkSharedSupport = URL(fileURLWithPath: "\(home)/Library/Input Methods/BambooMintKey.app/Contents/SharedSupport/BambooMintKeyStatusBar.app")
        candidateURLs.append(imkSharedSupport)

        // 3. Trong thư mục build workspace (môi trường phát triển)
        candidateURLs.append(URL(fileURLWithPath: "\(home)/Self-App/BambooMintKey/build/statusbar-arm64/BambooMintKeyStatusBar.app"))
        candidateURLs.append(URL(fileURLWithPath: "\(home)/Self-App/BambooMintKey/build/statusbar-x86_64/BambooMintKeyStatusBar.app"))

        // 4. Trong /Applications hoặc ~/Applications
        candidateURLs.append(URL(fileURLWithPath: "/Applications/BambooMintKeyStatusBar.app"))
        candidateURLs.append(URL(fileURLWithPath: "\(home)/Applications/BambooMintKeyStatusBar.app"))

        let fm = FileManager.default
        for url in candidateURLs {
            if fm.fileExists(atPath: url.path) {
                let config = NSWorkspace.OpenConfiguration()
                config.activates = false
                config.addsToRecentItems = false
                NSWorkspace.shared.openApplication(at: url, configuration: config) { _, _ in }
                return
            }
        }
    }
}
