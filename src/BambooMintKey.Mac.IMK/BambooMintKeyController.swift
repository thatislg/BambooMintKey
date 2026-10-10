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
    static var allowLeadingW: Bool = false

    /// Đảm bảo chỉ đăng ký observer một lần cho toàn bộ tiến trình.
    private static var observerRegistered = false

    /// Tên thông báo đồng bộ V/E (khớp với Menu Bar app).
    static let modeChangedNotification = Notification.Name("com.bamboomintkey.modeChanged")

    static func apply(to handle: UnsafeMutableRawPointer?) {
        guard let handle = handle else { return }
        CABIBridge.setOptions(
            handle,
            isVietnamese ? 1 : 0,
            toneStyle,
            repeatUndo ? 1 : 0,
            allowLeadingW ? 1 : 0,
            freeTone ? 1 : 0,
            1, // enableVietnameseDictionary
            englishBacktrack ? 1 : 0
        )
    }

    /// Đăng ký lắng nghe thay đổi V/E từ Menu Bar app (chạy một lần).
    static func registerModeObserver() {
        guard !observerRegistered else { return }
        observerRegistered = true
        DistributedNotificationCenter.default().addObserver(
            forName: modeChangedNotification,
            object: nil,
            queue: .main
        ) { notification in
            guard let info = notification.userInfo,
                  let enabled = info["isVietnameseMode"] as? Bool else { return }
            isVietnamese = enabled
        }
    }

    /// Lưu trạng thái V/E hiện tại vào config.json (giữ nguyên các trường khác).
    /// Ghi nguyên tử để IMK/StatusBar/UI.Mac đọc an toàn.
    static func persistMode() {
        do {
            let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
            let dir = base.appendingPathComponent("BambooMintKey", isDirectory: true)
            try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            let url = dir.appendingPathComponent("config.json")

            var json: [String: Any] = [:]
            if let data = try? Data(contentsOf: url),
               let existing = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
                json = existing
            }
            json["isVietnameseMode"] = isVietnamese

            let data = try JSONSerialization.data(withJSONObject: json, options: [.prettyPrinted, .sortedKeys])
            let tmp = dir.appendingPathComponent("config.json.tmp.\(UUID().uuidString)")
            try data.write(to: tmp, options: .atomic)
            _ = try FileManager.default.replaceItemAt(url, withItemAt: tmp)
        } catch {
            // Ghi cấu hình thất bại: giữ nguyên trạng thái trong bộ nhớ.
        }
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
        StatusBarLauncher.ensureRunning()
        _ = ensureContext()
        if let handle = contextHandle {
            CABIBridge.contextReset(handle)
        }
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

        // Luôn nạp lại tùy chọn hiện tại vào context trước khi xử lý phím.
        InputSettings.apply(to: handle)

        // 1. Chỉ thụ lý sự kiện nhấn phím (KeyDown).
        guard event.type == .keyDown else { return false }

        // 2. Phím bổ trợ hệ thống (Command/Control): chốt từ dở dang rồi nhường quyền.
        let modifiers = event.modifierFlags
        if modifiers.contains(.command) || modifiers.contains(.control) {
            flushPendingComposition(sender)
            return false
        }

        // 2.1. Phím ` (grave, keyCode 50) — chuyển V/E tức thì (giống Windows/Linux).
        if event.keyCode == 50 && !modifiers.contains(.shift) {
            toggleVietnameseMode()
            return true
        }

        // 3. Chế độ E (tiếng Anh): nhường toàn bộ phím cho ứng dụng, không gọi engine.
        //    Tránh lỗi nhân đôi: engine nhánh isEnabled=false tích lũy RawKeys rồi
        //    commit lại khi gặp space -> "testtest" (Issue 018).
        if !InputSettings.isVietnamese {
            flushPendingComposition(sender)
            return false
        }

        // 4. Backspace (keyCode 51 = delete/backspace trên bàn phím Mac).
        if event.keyCode == 51 {
            let action = CABIBridge.processBackspace(handle)
            return handleAction(action, client: sender)
        }

        // 5. Phím điều hướng & phím chức năng (mũi tên trái/phải/lên/xuống, Escape, Home/End, PgUp/PgDn...):
        // Chốt từ dở dang trước khi nhường phím cho ứng dụng (khớp chuẩn Linux Issue 015).
        // Ngăn chặn app nhận phím điều hướng khi preedit còn dở dẫn đến chèn ký tự lạ (0xF700..0xF703)
        // hoặc làm hỏng vị trí con trỏ gây rác chữ khi gõ space tiếp theo.
        if isNavigationOrFunctionKey(event) {
            flushPendingComposition(sender)
            return false
        }

        // 6. Trích xuất mã Unicode của ký tự (dùng event.characters để giữ hoa/thường theo Shift).
        guard let characters = event.characters,
              let scalar = characters.unicodeScalars.first else {
            // Phím không sinh ký tự in được: chốt từ dở dang trước khi nhường phím.
            flushPendingComposition(sender)
            return false
        }

        let unicode: UInt32 = scalar.value

        // 7. Phím ngắt từ (space, enter, tab, dấu câu...).
        if isWordBreak(unicode) {
            let action = CABIBridge.processWordbreak(handle, unicode)
            return handleAction(action, client: sender)
        }

        // 8. Ký tự in được thông thường (ASCII).
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

        // Lấy thuộc tính marked text chuẩn của Apple InputMethodKit (style 0: kTSMHiliteRawText).
        // Trả về NSMarkedClauseSegment = 1 (không chứa NSUnderline).
        // Nhờ đó, các ứng dụng Apple bản địa (Safari, Notes, TextEdit, Pages...)
        // sẽ KHÔNG hiển thị đường gạch chân (giống hệt Simple Telex của Apple).
        let range = NSRange(location: 0, length: text.utf16.count)
        let attrs = (self.mark(forStyle: 0, at: range) as? [NSAttributedString.Key: Any]) ?? [:]
        let attributed = NSAttributedString(string: text, attributes: attrs)

        // Con trỏ đặt ở cuối chuỗi (nhấp nháy tự nhiên sau từ đang gõ).
        let selection = NSRange(location: text.utf16.count, length: 0)

        // Luôn truyền notFoundRange để thay thế đúng vùng marked text hiện tại,
        // không truyền location 0 vì sẽ gây lỗi nhân đôi chữ (thuwrthử).
        input.setMarkedText(attributed, selectionRange: selection, replacementRange: notFoundRange)
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

    // MARK: - Chốt từ dở dang khi mất focus hoặc gặp phím điều hướng

    /// Chốt chuỗi đang gõ dở vào văn bản rồi đặt lại bộ đệm (tránh mất chữ / kẹt preedit).
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

    /// Nhận diện các phím điều hướng và phím chức năng không in được:
    /// - Mũi tên: Trái (123), Phải (124), Xuống (125), Lên (126)
    /// - Điều hướng trang: Home (115), End (119), Page Up (116), Page Down (121)
    /// - Hủy / Xóa phía trước: Escape (53), Forward Delete (117)
    /// - Dải Cocoa Special Function Keys (0xF700...0xF8FF) bao gồm mũi tên và F1..F20
    /// - Ký tự điều khiển ASCII (< 0x20 ngoại trừ Tab \t, LF \n, CR \r)
    private func isNavigationOrFunctionKey(_ event: NSEvent) -> Bool {
        // Phím mũi tên (Left: 123, Right: 124, Down: 125, Up: 126)
        if event.keyCode >= 123 && event.keyCode <= 126 {
            return true
        }
        // Phím điều hướng & hệ thống khác
        if event.keyCode == 53 || event.keyCode == 115 || event.keyCode == 119 ||
           event.keyCode == 116 || event.keyCode == 121 || event.keyCode == 117 {
            return true
        }
        // Ký tự trong dải function key hoặc điều khiển của Cocoa
        if let chars = event.characters, let scalar = chars.unicodeScalars.first {
            let val = scalar.value
            if val >= 0xF700 && val <= 0xF8FF {
                return true
            }
            if val < 0x20 && val != 0x09 && val != 0x0A && val != 0x0D {
                return true
            }
        }
        return false
    }

    /// Xác định ký tự ngắt từ: space, tab, enter hoặc dấu câu ASCII.
    private func isWordBreak(_ unicode: UInt32) -> Bool {
        if unicode > 0x7F { return false }
        guard let scalar = UnicodeScalar(unicode) else { return false }
        return CharacterSet.whitespacesAndNewlines.contains(scalar)
            || CharacterSet.punctuationCharacters.contains(scalar)
    }

    // MARK: - Menu Bar Dropdown

    /// Menu của Input Source. Theo yêu cầu thiết kế, thanh IMK gốc chỉ giữ tên
    /// định danh "BambooMintKey", KHÔNG chứa tùy chọn cài đặt nào (đã chuyển sang
    /// Menu Bar app V/E và UI.Mac).
    ///
    /// QUAN TRỌNG: vẫn phải có ít nhất một NSMenuItem với `target = self` để giữ
    /// reference tới controller. Nếu trả về NSMenu trống, IMKServer sẽ giải phóng
    /// controller sau mỗi phím -> mất trạng thái gõ (Issue 018/regression).
    override func menu() -> NSMenu! {
        let menu = NSMenu(title: "BambooMintKey")
        let item = NSMenuItem(title: "BambooMintKey", action: #selector(noop), keyEquivalent: "")
        item.target = self
        item.isEnabled = false
        menu.addItem(item)
        return menu
    }

    /// Selector rỗng dùng để giữ reference controller qua NSMenuItem.
    @objc private func noop() {}

    /// Chuyển đổi V/E bằng phím ` (grave), đồng bộ + lưu cấu hình.
    private func toggleVietnameseMode() {
        InputSettings.isVietnamese.toggle()
        if let handle = ensureContext() {
            InputSettings.apply(to: handle)
        }
        InputSettings.persistMode()
        broadcastMode()
    }

    /// Báo trạng thái V/E mới cho Menu Bar app (đồng bộ hai chiều).
    private func broadcastMode() {
        DistributedNotificationCenter.default().postNotificationName(
            InputSettings.modeChangedNotification,
            object: nil,
            userInfo: ["isVietnameseMode": InputSettings.isVietnamese],
            deliverImmediately: true
        )
    }
}
