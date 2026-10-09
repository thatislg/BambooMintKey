// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Cocoa

/// AppDelegate giữ tham chiếu tới StatusBarController để icon không bị thu hồi.
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusBarController: StatusBarController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusBarController = StatusBarController()
    }

    /// Chặn mọi lệnh thoát (Cmd+Q, menu Quit): icon E/V phải luôn sẵn sàng,
    /// chỉ ẩn/hiện theo input source BambooMintKey, không được tự ý tắt.
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        return .terminateCancel
    }
}
