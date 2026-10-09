// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import Cocoa

// Điểm vào của BambooMintKey Menu Bar app (background agent, LSUIElement).
// Không hiển thị trên Dock; chỉ có một icon chữ "B" trên thanh menu.

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
