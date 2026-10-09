// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
import AppKit

// Sinh icon chữ "B" màu vàng trên nền đỏ (cờ Việt Nam) cho Input Source (M3).
// Đầu ra: src/media/rendered_b_64x64.png (dùng bởi build_imk.sh để sinh .tiff).

let size = 64.0
let image = NSImage(size: NSSize(width: size, height: size))
image.lockFocus()

// Nền đỏ.
NSColor(calibratedRed: 0.85, green: 0.13, blue: 0.13, alpha: 1.0).setFill()
NSBezierPath(roundedRect: NSRect(x: 0, y: 0, width: size, height: size),
             xRadius: 12, yRadius: 12).fill()

// Chữ "B" màu vàng.
let attrs: [NSAttributedString.Key: Any] = [
    .font: NSFont.boldSystemFont(ofSize: 44),
    .foregroundColor: NSColor(calibratedRed: 1.0, green: 0.84, blue: 0.0, alpha: 1.0),
]
let str = NSAttributedString(string: "B", attributes: attrs)
let strSize = str.size()
str.draw(at: NSPoint(x: (size - strSize.width) / 2, y: (size - strSize.height) / 2))

image.unlockFocus()

// Xuất PNG.
guard let tiff = image.tiffRepresentation,
      let rep = NSBitmapImageRep(data: tiff),
      let png = rep.representation(using: .png, properties: [:]) else {
    fputs("Lỗi: không tạo được PNG\n", stderr)
    exit(1)
}

let outPath = CommandLine.arguments.count > 1
    ? CommandLine.arguments[1]
    : "src/media/rendered_b_64x64.png"
do {
    try png.write(to: URL(fileURLWithPath: outPath))
    print("Đã sinh: \(outPath)")
} catch {
    fputs("Lỗi ghi file: \(error)\n", stderr)
    exit(1)
}
