// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Cocoa
import InputMethodKit

/// Bộ điều khiển nhập liệu chính cho một phiên gõ (một ô văn bản đang focus).
///
/// Vòng đời (theo 010_03_IMK_Engine_Design.md mục 3):
/// - Khi người dùng focus vào ô văn bản, macOS khởi tạo thể hiện này và gọi
///   `bmk_context_create()` để nhận một handle ngữ cảnh độc lập.
/// - Mọi sự kiện phím được điều phối qua `handle(_:client:)`.
/// - Khi mất focus (`deactivateServer`/`deinit`), chốt từ dở dang rồi giải
///   phóng handle bằng `bmk_context_free`.
///
/// Chú ý: `@objc(BambooMintKeyController)` giữ tên class ổn định không kèm tên
/// module, khớp đúng với khóa `InputMethodServerControllerClass` trong Info.plist.
/// Trạng thái cài đặt gõ chia sẻ giữa các phiên nhập liệu và Menu Bar.
struct InputSettings {
    static var isVietnamese: Bool = true
    static var toneStyle: Int32 = 0 // 0: kiểu mới (hòa, úy), 1: kiểu cũ (hoà, uý)
    static var freeTone: Bool = true
    static var englishBacktrack: Bool = true
    static var repeatUndo: Bool = true

    static func apply(to handle: UnsafeMutableRawPointer?) {
        guard let handle = handle else { return }
        CABIBridge.setOptions(
            handle,
            isVietnamese ? 1 : 0,
            toneStyle,
            repeatUndo ? 1 : 0,
            0, // allowLeadingW
            freeTone ? 1 : 0,
            1, // enableVietnameseDictionary
            englishBacktrack ? 1 : 0
        )
    }
}

@objc(BambooMintKeyController)
final class BambooMintKeyController: IMKInputController {

    /// Handle ngữ cảnh C-ABI cho phiên gõ này (nil nếu khởi tạo thất bại).
    private var contextHandle: UnsafeMutableRawPointer?

    /// Hằng số phạm vi thay thế mặc định của Cocoa (thay thế vùng marked text hiện tại).
    private let notFoundRange = NSRange(location: NSNotFound, length: NSNotFound)

    /// Đảm bảo luôn có context hợp lệ; tự động tạo nếu chưa có hoặc sau khi mất focus.
    private func ensureContext() -> UnsafeMutableRawPointer? {
        if let handle = contextHandle {
            return handle
        }
        let handle = CABIBridge.contextCreate()
        if let h = handle {
            InputSettings.apply(to: h)
        }
        contextHandle = handle
        return handle
    }

    override init!(server: IMKServer!, delegate: Any!, client inputClient: Any!) {
        super.init(server: server, delegate: delegate, client: inputClient)
        _ = ensureContext()
    }

    override func activateServer(_ sender: Any!) {
        super.activateServer(sender)
        _ = ensureContext()
    }

    deinit {
        if let handle = contextHandle {
            CABIBridge.contextFree(handle)
            contextHandle = nil
        }
    }

    // MARK: - Xử lý sự kiện phím

    /// Điểm vào xử lý sự kiện bàn phím từ hệ điều hành.
    /// Trả về `true` nếu đã nuốt phím, `false` để nhường phím cho ứng dụng.
    override func handle(_ event: NSEvent!, client sender: Any!) -> Bool {
        guard let handle = ensureContext() else { return false }

        // 1. Chỉ thụ lý sự kiện nhấn phím (KeyDown).
        guard event.type == .keyDown else { return false }

        // 2. Phím bổ trợ hệ thống (Command/Control): chốt từ dở dang rồi nhường quyền.
        let modifiers = event.modifierFlags
        if modifiers.contains(.command) || modifiers.contains(.control) {
            flushPendingComposition(sender)
            return false
        }

        // 3. Backspace (keyCode 51 = delete/backspace trên bàn phím Mac).
        if event.keyCode == 51 {
            let action = CABIBridge.processBackspace(handle)
            return handleAction(action, client: sender)
        }

        // 4. Trích xuất mã Unicode của ký tự (dùng event.characters để giữ hoa/thường theo Shift).
        guard let characters = event.characters,
              let scalar = characters.unicodeScalars.first else {
            // Phím không sinh ký tự in được (mũi tên, Home/End, F1-F12...):
            // chốt từ dở dang trước khi nhường phím, tránh mất chữ.
            flushPendingComposition(sender)
            return false
        }

        let unicode: UInt32 = scalar.value

        // 5. Phím ngắt từ (space, enter, tab, dấu câu...).
        if isWordBreak(unicode) {
            let action = CABIBridge.processWordbreak(handle, unicode)
            return handleAction(action, client: sender)
        }

        // 6. Ký tự in được thông thường (ASCII).
        if unicode >= 0x20 && unicode <= 0x7E {
            let action = CABIBridge.processKey(handle, unicode)
            return handleAction(action, client: sender)
        }

        return false
    }

    // MARK: - Điều phối mã hành động

    /// Ánh xạ mã hành động C-ABI sang thao tác trên ứng dụng đích.
    /// Trả về `true` nếu phím được nuốt, `false` nếu nhường phím.
    private func handleAction(_ actionRaw: Int32, client: Any?) -> Bool {
        guard let action = CABIActionCode(rawValue: actionRaw) else { return false }
        switch action {
        case .updatePreedit:
            updatePreedit(client)
            return true
        case .commitString:
            commitString(client)
            return true
        case .consume:
            return true
        case .passThrough:
            return false
        }
    }

    // MARK: - Lấy proxy đối tượng nhập liệu

    private func getTextInput(_ client: Any?) -> IMKTextInput? {
        return (client as? IMKTextInput) ?? self.client()
    }

    // MARK: - Hiển thị Marked Text (Preedit) & tắt gạch chân

    /// Cập nhật chuỗi đang gõ dở lên ứng dụng đích với thuộc tính ẩn gạch chân.
    private func updatePreedit(_ client: Any?) {
        guard let handle = contextHandle,
              let input = getTextInput(client) else { return }

        let text = CABIBridge.preeditString(handle)

        if text.isEmpty {
            // Xóa vùng đánh dấu khi preedit rỗng.
            input.setMarkedText(
                "",
                selectionRange: NSRange(location: 0, length: 0),
                replacementRange: notFoundRange
            )
            return
        }

        // Chuỗi thuộc tính yêu cầu ẩn đường gạch chân (Stealth Mode):
        // không dùng underline để tránh che khuất dấu nặng (.) tiếng Việt.
        let attributed = NSAttributedString(string: text, attributes: stealthAttributes)

        // Con trỏ đặt ở cuối chuỗi (nhấp nháy tự nhiên sau từ đang gõ).
        let selection = NSRange(location: text.utf16.count, length: 0)

        // Luôn truyền notFoundRange để thay thế đúng vùng marked text hiện tại,
        // không truyền location 0 vì sẽ gây lỗi nhân đôi chữ (thuwrthử).
        input.setMarkedText(attributed, selectionRange: selection, replacementRange: notFoundRange)
    }

    /// Thuộc tính hiển thị không gạch chân cho chuỗi đang soạn thảo.
    private var stealthAttributes: [NSAttributedString.Key: Any] {
        [
            .underlineStyle: 0,
            .underlineColor: NSColor.clear,
        ]
    }

    // MARK: - Chốt chuỗi (Commit)

    /// Chèn chuỗi đã hoàn thiện vào ứng dụng và thay thế vùng đánh dấu.
    private func commitString(_ client: Any?) {
        guard let handle = contextHandle,
              let input = getTextInput(client) else { return }

        let text = CABIBridge.commitString(handle)

        if !text.isEmpty {
            input.insertText(text, replacementRange: notFoundRange)
        }
    }

    // MARK: - Chốt từ dở dang khi mất focus

    /// Chốt chuỗi đang gõ dở vào văn bản rồi đặt lại bộ đệm (tránh mất chữ).
    private func flushPendingComposition(_ client: Any?) {
        guard let handle = contextHandle,
              let input = getTextInput(client) else { return }

        let text = CABIBridge.preeditString(handle)
        if !text.isEmpty {
            input.insertText(text, replacementRange: notFoundRange)
        }
        CABIBridge.contextReset(handle)
    }

    /// macOS gọi khi ô văn bản mất focus: chốt từ dở dang và đặt lại bộ đệm (giữ context sống).
    override func deactivateServer(_ sender: Any!) {
        if let handle = contextHandle {
            if let input = getTextInput(sender) {
                let text = CABIBridge.preeditString(handle)
                if !text.isEmpty {
                    input.insertText(text, replacementRange: notFoundRange)
                }
            }
            CABIBridge.contextReset(handle)
        }
        super.deactivateServer(sender)
    }

    // MARK: - Phân loại phím

    /// Xác định ký tự ngắt từ: space, tab, enter hoặc dấu câu ASCII.
    private func isWordBreak(_ unicode: UInt32) -> Bool {
        if unicode > 0x7F { return false }
        guard let scalar = UnicodeScalar(unicode) else { return false }
        return CharacterSet.whitespacesAndNewlines.contains(scalar)
            || CharacterSet.punctuationCharacters.contains(scalar)
    }

    // MARK: - Menu Bar Dropdown

    override func menu() -> NSMenu! {
        let menu = NSMenu(title: "BambooMintKey")

        // 1. Chế độ gõ
        let viItem = NSMenuItem(title: "Tiếng Việt (Telex)", action: #selector(setVietnameseMode), keyEquivalent: "")
        viItem.target = self
        viItem.state = InputSettings.isVietnamese ? .on : .off
        menu.addItem(viItem)

        let enItem = NSMenuItem(title: "Tiếng Anh (English)", action: #selector(setEnglishMode), keyEquivalent: "")
        enItem.target = self
        enItem.state = InputSettings.isVietnamese ? .off : .on
        menu.addItem(enItem)

        menu.addItem(NSMenuItem.separator())

        // 2. Tùy chọn đặt dấu
        let toneItem = NSMenuItem(title: "Kiểu đặt dấu mới (oà, uý)", action: #selector(toggleToneStyle), keyEquivalent: "")
        toneItem.target = self
        toneItem.state = (InputSettings.toneStyle == 0) ? .on : .off
        menu.addItem(toneItem)

        let freeToneItem = NSMenuItem(title: "Bỏ dấu tự do", action: #selector(toggleFreeTone), keyEquivalent: "")
        freeToneItem.target = self
        freeToneItem.state = InputSettings.freeTone ? .on : .off
        menu.addItem(freeToneItem)

        let backtrackItem = NSMenuItem(title: "Khôi phục từ tiếng Anh", action: #selector(toggleEnglishBacktrack), keyEquivalent: "")
        backtrackItem.target = self
        backtrackItem.state = InputSettings.englishBacktrack ? .on : .off
        menu.addItem(backtrackItem)

        menu.addItem(NSMenuItem.separator())

        // 3. Thông tin
        let aboutItem = NSMenuItem(title: "Thông tin về BambooMintKey…", action: #selector(showAboutDialog), keyEquivalent: "")
        aboutItem.target = self
        menu.addItem(aboutItem)

        return menu
    }

    @objc private func setVietnameseMode() {
        InputSettings.isVietnamese = true
        if let handle = ensureContext() {
            InputSettings.apply(to: handle)
        }
    }

    @objc private func setEnglishMode() {
        InputSettings.isVietnamese = false
        if let handle = ensureContext() {
            InputSettings.apply(to: handle)
        }
    }

    @objc private func toggleToneStyle() {
        InputSettings.toneStyle = (InputSettings.toneStyle == 0) ? 1 : 0
        if let handle = ensureContext() {
            InputSettings.apply(to: handle)
        }
    }

    @objc private func toggleFreeTone() {
        InputSettings.freeTone.toggle()
        if let handle = ensureContext() {
            InputSettings.apply(to: handle)
        }
    }

    @objc private func toggleEnglishBacktrack() {
        InputSettings.englishBacktrack.toggle()
        if let handle = ensureContext() {
            InputSettings.apply(to: handle)
        }
    }

    @objc private func showAboutDialog() {
        let alert = NSAlert()
        alert.messageText = "BambooMintKey v1.1.4"
        alert.informativeText = "Bộ gõ tiếng Việt Telex bản địa cho macOS\nBản quyền © 2026 Dương Gia Long và LMO contributors\nGiấy phép nguồn mở MIT"
        alert.alertStyle = .informational
        alert.addButton(withTitle: "Đóng")
        alert.runModal()
    }
}
