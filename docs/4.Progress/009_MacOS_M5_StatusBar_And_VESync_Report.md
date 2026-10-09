<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Báo Cáo Kỹ Thuật: Đồng Bộ Trạng Thái V/E & Tích Hợp Menu Bar (Milestone 5)

**Mã tài liệu:** `009_MacOS_M5_StatusBar_And_VESync_Report`

**Ngày thực hiện:** 2026-10-09

**Giai đoạn:** Phase 10 — Nền tảng macOS (InputMethodKit) — Milestone 5 (Đồng bộ V/E & Menu Bar)

**Trạng thái:** ✅ Đã hoàn thành 3/3 hạng mục — Người dùng xác nhận icon hiển thị đúng

**Tài liệu liên quan:**
- Thiết kế UI.Mac: [010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md)
- Thiết kế IMK Engine: [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md)
- Tiến độ Phase 10: [007_MacOSProgressTracking.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/007_MacOSProgressTracking.md)
- Báo cáo M3: [008_MacOS_M3_Verification_And_Fixes_Report.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/008_MacOS_M3_Verification_And_Fixes_Report.md)

---

## 1. Tổng Quan

Milestone 5 hoàn thiện cơ chế đồng bộ trạng thái V/E và tích hợp Menu Bar cho bộ gõ macOS. Mục tiêu: người dùng luôn thấy rõ trạng thái gõ hiện tại (Việt/Anh) qua một icon động, đồng thời mọi thay đổi cấu hình được phản ánh tức thì giữa giao diện Cài đặt (UI.Mac), Menu Bar và bộ gõ (IMK Service).

---

## 2. Kiến Trúc V/E Trên macOS — Quyết Định Thiết Kế Quan Trọng

Trong quá trình triển khai, phát hiện một **giới hạn cứng** của macOS cần ghi nhận:

| Khái niệm | Bản chất | Khả năng đổi động |
|---|---|---|
| **Input Source icon** (menu nguồn nhập) | Do hệ thống TIS quản lý | **Tĩnh** — chỉ render icon `template` đơn sắc hoặc nhãn chữ, không render icon màu RGB |
| **NSStatusItem** (icon app tự quản lý) | Do app tạo và điều khiển 100% | **Động** — đổi hình/chữ tùy ý lúc chạy |

Hệ quả: **icon V/E động bắt buộc phải là `NSStatusItem`** của một app riêng (như Ollama/Claude), không thể nhét vào input source icon.

### Quyết định cuối cùng (theo phản hồi người dùng)

- **Input Source** (vị trí M3): dùng nhãn `BM` trên menu bar (không render được icon màu), icon màu chữ "B" (cờ VN) hiển thị ở System Settings → Input Sources.
- **StatusBar** (app nền riêng): icon **động `V`/`E`** màu vàng trên nền đỏ (cờ VN).

Kết quả thanh menu: `[V/E động]  [BM]` — một icon trạng thái + một định danh bộ gõ, không còn 2 icon trùng nghĩa.

---

## 3. Các Hạng Mục Đã Hoàn Thành

### 3.1. M5.1 — Biểu tượng trạng thái Menu Bar (Status Item)

- Tạo app nền `src/BambooMintKey.Mac.StatusBar/` (Swift, `NSStatusItem`, `LSUIElement`):
  - `main.swift` — điểm vào `NSApplication`.
  - `AppDelegate.swift` — giữ tham chiếu controller.
  - `StatusBarController.swift` — vẽ icon động `V`/`E`, dựng menu thả xuống.
  - `ConfigStore.swift` — đọc/ghi `config.json`.
  - `Info.plist` — `LSUIElement` (chạy nền, không hiện Dock).
- Menu thả xuống: Tiếng Việt (V) / Tiếng Anh (E) có dấu tick, Cài đặt…, Thoát.
- Icon input source đổi thành chữ "B" cờ VN (`src/media/rendered_b_64x64.png`, sinh bởi `scripts/macos/generate_b_icon.swift`).

### 3.2. M5.2 — File Watcher theo dõi cấu hình thời gian thực

- Thêm `src/BambooMintKey.Mac.IMK/ConfigWatcher.swift`:
  - Dùng `DispatchSourceFileSystemObject` theo dõi sự kiện `write/delete/rename` của `config.json`.
  - Khi UI.Mac lưu (ghi nguyên tử), IMK nạp lại toàn bộ tùy chọn gõ ngay lập tức, không cần logout.

### 3.3. M5.3 — Kênh đồng bộ V/E hai chiều

- Dùng `NSDistributedNotificationCenter` với tên `com.bamboomintkey.modeChanged`:
  - **Menu Bar → IMK:** khi người dùng đổi V/E trên StatusBar, phát thông báo kèm `isVietnameseMode`.
  - **IMK → Menu Bar:** khi đổi V/E từ menu IMK, phát thông báo ngược lại.
  - **UI.Mac → IMK:** qua file watcher (config.json).
- Thêm mục "Cài đặt…" vào `menu()` của IMK để mở UI.Mac.

---

## 4. Các File Đã Thay Đổi / Tạo Mới

| Đường dẫn tệp | Loại | Mô tả |
|---|---|---|
| `src/BambooMintKey.Mac.StatusBar/main.swift` | Tạo mới | Điểm vào app nền |
| `src/BambooMintKey.Mac.StatusBar/AppDelegate.swift` | Tạo mới | Giữ tham chiếu StatusBarController |
| `src/BambooMintKey.Mac.StatusBar/StatusBarController.swift` | Tạo mới | NSStatusItem + icon V/E động + menu |
| `src/BambooMintKey.Mac.StatusBar/ConfigStore.swift` | Tạo mới | Đọc/ghi config.json + tên notification |
| `src/BambooMintKey.Mac.StatusBar/Info.plist` | Tạo mới | LSUIElement, bundle ID `com.bamboomintkey.statusbar` |
| `src/BambooMintKey.Mac.IMK/ConfigWatcher.swift` | Tạo mới | DispatchSource file watcher cho config.json |
| `src/BambooMintKey.Mac.IMK/BambooMintKeyController.swift` | Sửa | Thêm mục Cài đặt…, broadcast V/E, observer mode |
| `src/BambooMintKey.Mac.IMK/main.swift` | Sửa | Đăng ký mode observer + khởi động ConfigWatcher |
| `src/BambooMintKey.Mac.IMK/Info.plist` | Sửa | Nhãn `BM` + icon palette (bỏ menu icon màu) |
| `src/media/rendered_b_64x64.png` | Tạo mới | Icon "B" cờ VN cho input source |
| `scripts/macos/generate_b_icon.swift` | Tạo mới | Script sinh icon "B" |
| `scripts/macos/build_statusbar.sh` | Tạo mới | Build app StatusBar |
| `scripts/macos/build_imk.sh` | Sửa | Dùng icon "B", thêm ConfigWatcher.swift vào biên dịch |

---

## 5. Kết Quả Nghiệm Thu

- ✅ Icon `V`/`E` động hiển thị đúng, đổi theo chế độ gõ.
- ✅ Icon "B" màu vàng/nền đỏ hiển thị ở System Settings → Input Sources.
- ✅ Nhãn `BM` hiển thị ở menu bar (định danh bộ gõ).
- ✅ Đồng bộ V/E hai chiều giữa Menu Bar ↔ IMK qua notification.
- ✅ File watcher nạp lại cấu hình tức thì khi UI.Mac lưu.

---

## 6. Vấn Đề Còn Tồn Đọng

1. **Phím `` ` `` chưa chuyển V/E** trên macOS (đã ghi nhận tại [Issue 017](file:///Users/lmo1720/Self-App/BambooMintKey/docs/3.Issue/017_MacOS_Missing_Grave_VE_Toggle.md)) — Windows/Linux dùng phím này, macOS hiện chưa có.
2. **App StatusBar chưa tự khởi động cùng hệ thống** — cần `LaunchAgent` (`~/Library/LaunchAgents/com.bamboomintkey.statusbar.plist`), thuộc phạm vi M7 (Đóng gói & Cài đặt).

---

## 7. Kết Luận

Milestone 5 đã hoàn thành 100% mục tiêu, giữ vững các nguyên tắc kiến trúc (Zero Regression trên Windows/Linux, thuần IMK, ưu tiên gõ đúng). Tổng tiến độ Phase 10 đạt 85%, sẵn sàng bước vào Milestone 6 (Kiểm thử E2E) và Milestone 7 (Đóng gói & Cài đặt).
