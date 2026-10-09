// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Cocoa
import InputMethodKit

// Điểm vào của BambooMintKey Input Method Service.
//
// macOS khởi chạy tiến trình này từ `~/Library/Input Methods/BambooMintKey.app`.
// IMKServer đăng ký tên kết nối dịch vụ với trung tâm quản lý phương thức nhập
// liệu, và lớp điều khiển `BambooMintKeyController` được khai báo trong Info.plist
// (khóa `InputMethodServerControllerClass`).

let connectionName = (Bundle.main.infoDictionary?["InputMethodConnectionName"] as? String)
    ?? "com.bamboomintkey.inputmethod_1_Connection"
let bundleIdentifier = Bundle.main.bundleIdentifier ?? "com.bamboomintkey.inputmethod"

let server = IMKServer(
    name: connectionName,
    bundleIdentifier: bundleIdentifier
)

// Lắng nghe thay đổi V/E từ Menu Bar app để đồng bộ trạng thái ngay.
InputSettings.registerModeObserver()

// Theo dõi config.json để nạp lại cấu hình khi người dùng lưu từ UI.Mac.
let configWatcher = ConfigWatcher()
configWatcher.start()

// Đảm bảo app StatusBar (icon EV) luôn được khởi chạy cùng bộ gõ.
StatusBarLauncher.ensureRunning()

// Giữ tiến trình sống để lắng nghe các yêu cầu mở phiên gõ từ ứng dụng.
NSApplication.shared.run()
