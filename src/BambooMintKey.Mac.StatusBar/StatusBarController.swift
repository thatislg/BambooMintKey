// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Cocoa

/// Bộ điều khiển Menu Bar (Status Item) cho BambooMintKey.
///
/// - Icon động: chữ "V" (tiếng Việt) hoặc "E" (tiếng Anh) màu vàng trên nền đỏ
///   (cờ Việt Nam) thể hiện trạng thái gõ hiện tại.
/// - Menu gồm: chuyển chế độ V/E, mở Cài đặt (UI.Mac), thoát.
/// - Đồng bộ V/E hai chiều với IMK Service qua `NSDistributedNotificationCenter`.
final class StatusBarController: NSObject {

    private let statusItem: NSStatusItem
    private var isVietnamese: Bool

    override init() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        isVietnamese = ConfigStore.readVietnameseMode()

        super.init()

        if let button = statusItem.button {
            button.image = Self.makeIcon(letter: isVietnamese ? "V" : "E")
            button.image?.isTemplate = false
            button.toolTip = "BambooMintKey — Bộ gõ tiếng Việt"
        }

        rebuildMenu()

        // Lắng nghe thay đổi V/E từ IMK Service (khi người dùng bấm phím ` bên app khác).
        DistributedNotificationCenter.default().addObserver(
            self,
            selector: #selector(modeDidChange(_:)),
            name: ConfigStore.modeChangedNotification,
            object: nil
        )
    }

    deinit {
        DistributedNotificationCenter.default().removeObserver(self)
    }

    // MARK: - Icon động V/E (cờ Việt Nam)

    private static func makeIcon(letter: String) -> NSImage {
        let size = NSSize(width: 18, height: 18)
        let image = NSImage(size: size)
        image.lockFocus()

        // Nền đỏ.
        NSColor(calibratedRed: 0.85, green: 0.13, blue: 0.13, alpha: 1.0).setFill()
        NSBezierPath(roundedRect: NSRect(x: 0, y: 0, width: size.width, height: size.height),
                     xRadius: 3, yRadius: 3).fill()

        // Chữ V/E màu vàng.
        let attrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.boldSystemFont(ofSize: 12),
            .foregroundColor: NSColor(calibratedRed: 1.0, green: 0.84, blue: 0.0, alpha: 1.0),
        ]
        let str = NSAttributedString(string: letter, attributes: attrs)
        let strSize = str.size()
        str.draw(at: NSPoint(x: (size.width - strSize.width) / 2,
                             y: (size.height - strSize.height) / 2))

        image.unlockFocus()
        return image
    }

    // MARK: - Menu

    private func rebuildMenu() {
        let menu = NSMenu()

        let viItem = NSMenuItem(title: "Tiếng Việt (V)", action: #selector(setVietnamese), keyEquivalent: "")
        viItem.target = self
        viItem.state = isVietnamese ? .on : .off
        menu.addItem(viItem)

        let enItem = NSMenuItem(title: "Tiếng Anh (E)", action: #selector(setEnglish), keyEquivalent: "")
        enItem.target = self
        enItem.state = isVietnamese ? .off : .on
        menu.addItem(enItem)

        menu.addItem(NSMenuItem.separator())

        let settingsItem = NSMenuItem(title: "Cài đặt…", action: #selector(openSettings), keyEquivalent: "")
        settingsItem.target = self
        menu.addItem(settingsItem)

        menu.addItem(NSMenuItem.separator())

        let quitItem = NSMenuItem(title: "Thoát BambooMintKey", action: #selector(quit), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    // MARK: - Hành động

    @objc private func setVietnamese() {
        applyMode(true)
    }

    @objc private func setEnglish() {
        applyMode(false)
    }

    private func applyMode(_ enabled: Bool) {
        isVietnamese = enabled
        ConfigStore.writeVietnameseMode(enabled)
        refreshIcon()
        rebuildMenu()

        // Báo cho IMK Service cập nhật tức thì.
        DistributedNotificationCenter.default().postNotificationName(
            ConfigStore.modeChangedNotification,
            object: nil,
            userInfo: ["isVietnameseMode": enabled],
            deliverImmediately: true
        )
    }

    @objc private func modeDidChange(_ notification: Notification) {
        guard let info = notification.userInfo,
              let enabled = info["isVietnameseMode"] as? Bool else { return }
        isVietnamese = enabled
        refreshIcon()
        rebuildMenu()
    }

    /// Cập nhật icon chữ V/E theo trạng thái hiện tại.
    private func refreshIcon() {
        statusItem.button?.image = Self.makeIcon(letter: isVietnamese ? "V" : "E")
    }

    @objc private func openSettings() {
        // Mở UI.Mac (BambooMintKey.UI.Mac). Nếu chưa có bản cài, thử tìm qua đường dẫn chuẩn.
        let candidates = [
            "/Applications/BambooMintKey.app",
            NSHomeDirectory() + "/Applications/BambooMintKey.app",
            NSHomeDirectory() + "/Library/Application Support/BambooMintKey/BambooMintKey.UI.Mac",
        ]
        for path in candidates {
            if FileManager.default.fileExists(atPath: path) {
                NSWorkspace.shared.open(URL(fileURLWithPath: path))
                return
            }
        }
        // Fallback: thông báo nhẹ nếu chưa cài UI.
        let alert = NSAlert()
        alert.messageText = "Chưa tìm thấy ứng dụng Cài đặt"
        alert.informativeText = "Hãy cài đặt BambooMintKey.UI.Mac trước."
        alert.alertStyle = .informational
        alert.addButton(withTitle: "Đóng")
        alert.runModal()
    }

    @objc private func quit() {
        NSApplication.shared.terminate(nil)
    }
}
