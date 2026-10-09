// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Cocoa
import Carbon

/// Bộ điều khiển Menu Bar (Status Item) cho BambooMintKey.
///
/// - Icon động: chữ "V" (tiếng Việt) hoặc "E" (tiếng Anh) màu vàng trên nền đỏ
///   (cờ Việt Nam) thể hiện trạng thái gõ hiện tại.
/// - Menu chứa toàn bộ tùy chọn gõ (tương đương tab "Nâng cao" của UI.Mac):
///   chuyển V/E, kiểu đặt dấu, bỏ dấu tự do, lặp phím undo, phím w đầu từ,
///   khôi phục từ tiếng Anh; kèm "Cài đặt…" (mở UI.Mac) và "Thông tin…".
/// - Đồng bộ hai chiều với IMK Service qua `NSDistributedNotificationCenter`
///   và tệp `config.json` (nguồn chân lý).
final class StatusBarController: NSObject {

    private let statusItem: NSStatusItem
    private var settings: ConfigStore.Settings

    override init() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        settings = ConfigStore.readSettings()

        super.init()

        if let button = statusItem.button {
            button.image = Self.makeIcon(letter: settings.isVietnamese ? "V" : "E")
            button.image?.isTemplate = false
            button.toolTip = "BambooMintKey — Bộ gõ tiếng Việt"
        }

        rebuildMenu()

        // Hiện/ẩn icon theo input source đang chọn (chỉ hiện khi dùng BambooMintKey).
        updateVisibility()

        // Lắng nghe thay đổi từ IMK Service (phím ` bên app khác, hoặc menu IMK).
        DistributedNotificationCenter.default().addObserver(
            self,
            selector: #selector(modeDidChange(_:)),
            name: ConfigStore.modeChangedNotification,
            object: nil
        )

        // Lắng nghe sự kiện đổi input source của hệ thống.
        DistributedNotificationCenter.default().addObserver(
            self,
            selector: #selector(inputSourceChanged),
            name: NSNotification.Name("com.apple.Carbon.TISNotifySelectedKeyboardInputSourceChanged"),
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

        NSColor(calibratedRed: 0.85, green: 0.13, blue: 0.13, alpha: 1.0).setFill()
        NSBezierPath(roundedRect: NSRect(x: 0, y: 0, width: size.width, height: size.height),
                     xRadius: 3, yRadius: 3).fill()

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

        // 1. Chế độ gõ V/E
        let viItem = NSMenuItem(title: "Tiếng Việt (V)", action: #selector(setVietnamese), keyEquivalent: "")
        viItem.target = self
        viItem.state = settings.isVietnamese ? .on : .off
        menu.addItem(viItem)

        let enItem = NSMenuItem(title: "Tiếng Anh (E)", action: #selector(setEnglish), keyEquivalent: "")
        enItem.target = self
        enItem.state = settings.isVietnamese ? .off : .on
        menu.addItem(enItem)

        menu.addItem(NSMenuItem.separator())

        // 2. Tùy chọn gõ (tương đương tab "Nâng cao")
        let toneItem = NSMenuItem(title: "Kiểu đặt dấu mới (hòa, úy)", action: #selector(toggleToneStyle), keyEquivalent: "")
        toneItem.target = self
        toneItem.state = (settings.toneStyle == 0) ? .on : .off
        menu.addItem(toneItem)

        let freeToneItem = NSMenuItem(title: "Bỏ dấu tự do", action: #selector(toggleFreeTone), keyEquivalent: "")
        freeToneItem.target = self
        freeToneItem.state = settings.allowFreeTonePlacement ? .on : .off
        menu.addItem(freeToneItem)

        let repeatUndoItem = NSMenuItem(title: "Lặp phím xóa dấu", action: #selector(toggleRepeatUndo), keyEquivalent: "")
        repeatUndoItem.target = self
        repeatUndoItem.state = settings.allowRepeatKeyUndo ? .on : .off
        menu.addItem(repeatUndoItem)

        let leadingWItem = NSMenuItem(title: "Phím w đầu từ thành ư", action: #selector(toggleLeadingW), keyEquivalent: "")
        leadingWItem.target = self
        leadingWItem.state = settings.allowLeadingWAsU ? .on : .off
        menu.addItem(leadingWItem)

        let backtrackItem = NSMenuItem(title: "Khôi phục từ tiếng Anh", action: #selector(toggleEnglishBacktrack), keyEquivalent: "")
        backtrackItem.target = self
        backtrackItem.state = settings.enableEnglishBacktracking ? .on : .off
        menu.addItem(backtrackItem)

        menu.addItem(NSMenuItem.separator())

        // 3. Cài đặt & thông tin
        let settingsItem = NSMenuItem(title: "Cài đặt…", action: #selector(openSettings), keyEquivalent: "")
        settingsItem.target = self
        menu.addItem(settingsItem)

        let aboutItem = NSMenuItem(title: "Thông tin về BambooMintKey…", action: #selector(openAbout), keyEquivalent: "")
        aboutItem.target = self
        menu.addItem(aboutItem)

        menu.addItem(NSMenuItem.separator())

        let quitItem = NSMenuItem(title: "Thoát BambooMintKey", action: #selector(quit), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    // MARK: - Hành động

    @objc private func setVietnamese() {
        settings.isVietnamese = true
        persistAndSync()
    }

    @objc private func setEnglish() {
        settings.isVietnamese = false
        persistAndSync()
    }

    @objc private func toggleToneStyle() {
        settings.toneStyle = (settings.toneStyle == 0) ? 1 : 0
        persistAndSync()
    }

    @objc private func toggleFreeTone() {
        settings.allowFreeTonePlacement.toggle()
        persistAndSync()
    }

    @objc private func toggleRepeatUndo() {
        settings.allowRepeatKeyUndo.toggle()
        persistAndSync()
    }

    @objc private func toggleLeadingW() {
        settings.allowLeadingWAsU.toggle()
        persistAndSync()
    }

    @objc private func toggleEnglishBacktrack() {
        settings.enableEnglishBacktracking.toggle()
        persistAndSync()
    }

    /// Lưu toàn bộ tùy chọn + đồng bộ icon/menu + báo cho IMK.
    private func persistAndSync() {
        ConfigStore.writeSettings(settings)
        refreshIcon()
        rebuildMenu()
        broadcast()
    }

    /// Báo thay đổi cho IMK Service (để áp dụng ngay vào phiên gõ hiện tại).
    private func broadcast() {
        DistributedNotificationCenter.default().postNotificationName(
            ConfigStore.modeChangedNotification,
            object: nil,
            userInfo: ["isVietnameseMode": settings.isVietnamese, "configChanged": true],
            deliverImmediately: true
        )
    }

    @objc private func modeDidChange(_ notification: Notification) {
        guard let info = notification.userInfo else { return }
        if let enabled = info["isVietnameseMode"] as? Bool {
            settings.isVietnamese = enabled
        }
        // Nếu là thay đổi cấu hình chung, nạp lại toàn bộ từ config.json.
        if (info["configChanged"] as? Bool) == true {
            settings = ConfigStore.readSettings()
        }
        refreshIcon()
        rebuildMenu()
    }

    private func refreshIcon() {
        statusItem.button?.image = Self.makeIcon(letter: settings.isVietnamese ? "V" : "E")
    }

    // MARK: - Đồng bộ với input source đang chọn

    /// Kiểm tra input source đang chọn có phải BambooMintKey không.
    private func isBambooMintKeyActive() -> Bool {
        guard let source = TISCopyCurrentKeyboardInputSource()?.takeRetainedValue() else {
            return false
        }
        guard let raw = TISGetInputSourceProperty(source, kTISPropertyInputSourceID) else {
            return false
        }
        let id = Unmanaged<CFTypeRef>.fromOpaque(raw).takeUnretainedValue() as! CFString
        return (id as String).hasPrefix("com.bamboomintkey.inputmethod")
    }

    /// Hiện icon E/V chỉ khi BambooMintKey đang là input source được chọn;
    /// ẩn icon khi chuyển sang source khác (VI/ABC...).
    private func updateVisibility() {
        statusItem.isVisible = isBambooMintKeyActive()
    }

    @objc private func inputSourceChanged() {
        updateVisibility()
    }

    @objc private func openSettings() {
        openUIMac(arguments: [])
    }

    @objc private func openAbout() {
        openUIMac(arguments: ["--tab", "about"])
    }

    private func openUIMac(arguments: [String]) {
        let candidates = [
            "/Applications/BambooMintKey.app",
            NSHomeDirectory() + "/Applications/BambooMintKey.app",
        ]
        for path in candidates {
            if FileManager.default.fileExists(atPath: path) {
                let config = NSWorkspace.OpenConfiguration()
                if !arguments.isEmpty {
                    config.arguments = arguments
                }
                NSWorkspace.shared.openApplication(at: URL(fileURLWithPath: path),
                                                   configuration: config) { _, _ in }
                return
            }
        }
    }

    @objc private func quit() {
        NSApplication.shared.terminate(nil)
    }
}
