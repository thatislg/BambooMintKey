<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Báo Cáo Kỹ Thuật: Tự Động Ẩn Context Menu Khi Rê Chuột Ra Ngoài & Sửa Lỗi Click-Away

**Mã tài liệu:** `025_Windows_TrayIcon_Menu_AutoDismiss_On_MouseLeave`  
**Ngày lập:** 13/10/2026  
**Trạng thái:** ✅ Đã hoàn thành, kiểm thử thành công  
**Tác giả:** BambooMintKey Core Team  

---

## 1. Vấn đề Người Dùng Phản Ánh

1. **Khó chịu khi click ra ngoài không ẩn:**  
   Khi chuột phải lên biểu tượng Tray Icon (V/E), bảng danh sách options (Kiểu gõ, Bảng mã, Dấu thanh, Thiết lập...) hiện ra. Nếu người dùng chỉ bật lên xem và click chuột ra ngoài (vào ứng dụng khác, màn hình Desktop), menu không tự động ẩn đi mà bị "kẹt". Người dùng bắt buộc phải click vào 1 tùy chọn nào đó thì mới đóng được.
2. **Yêu cầu tính năng tự động ẩn khi rê chuột ra ngoài (Mouse Leave Auto-Dismiss):**  
   Trong trường hợp người dùng chỉ bật lên xem và di chuyển chuột đi chỗ khác (rời khỏi khung options của icon tray), bảng tùy chọn phải tự động ẩn đi ngay lập tức mà không bắt người dùng phải click chọn hay bấm nút gì.

---

## 2. Nguyên Nhân Kỹ Thuật (Root Causes)

1. **Cửa sổ `hMsgWnd` được tạo dạng Message-Only (`HWND_MESSAGE = -3`):**  
   * Trong Win32 API, một message-only window không phải là top-level window và không thể được gán quyền Foreground Window (`SetForegroundWindow` trả về `FALSE`).
   * Theo tài liệu chính thức của Microsoft (**KB135788 - PRB: Menus for Notification Icons Don't Work Correctly**), để Context Menu của khay hệ thống (Tray Icon) tự động hủy khi người dùng click ra ngoài, cửa sổ nhận thông điệp phải là top-level window và phải được đưa lên Foreground trước khi gọi `TrackPopupMenuEx`, đồng thời phải gửi thông điệp `WM_NULL` sau khi kết thúc.
2. **Chưa có cơ chế giám sát ranh giới con trỏ chuột (Mouse Cursor Boundary Watcher):**  
   * `TrackPopupMenuEx` là một Win32 modal loop blocking. Khi người dùng rê chuột ra khỏi các khung cửa sổ menu (`#32768`), Windows mặc định vẫn giữ menu hiển thị trừ khi có sự kiện click hoặc phím bấm.

---

## 3. Giải Pháp Đã Triển Khai

Tệp chỉnh sửa: [TrayIconManager.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.UI/TrayIconManager.fs)

### 3.1. Chuyển `hMsgWnd` thành Top-Level Hidden Window chuẩn Microsoft KB135788
* Tạo `hMsgWnd` với `hWndParent = IntPtr.Zero` (cửa sổ 0x0 ẩn, không có cờ `WS_VISIBLE`, không xuất hiện trên thanh Taskbar hay Alt-Tab).
* Trước khi gọi `TrackPopupMenuEx`: Gọi `TrayWin32.SetForegroundWindow(hMsgWnd)` để gán quyền foreground.
* Sau khi gọi `TrackPopupMenuEx`: Gửi `TrayWin32.PostMessageW(hMsgWnd, TrayWin32.WM_NULL, UIntPtr.Zero, IntPtr.Zero)`.
$\rightarrow$ **Kết quả:** Người dùng click chuột ra bất kỳ đâu ngoài menu, menu sẽ tự động đóng ngay lập tức.

### 3.2. Triển khai Watcher Thread tự động ẩn khi chuột rời khỏi khung options
1. **Theo dõi tất cả các cửa sổ popup menu của BambooMintKey (`#32768`):**  
   * Thu thập toàn bộ RECT của Menu chính và các Submenu đang mở (Kiểu gõ, Bảng mã, v.v.).
2. **Kiểm tra vị trí con trỏ chuột với biên an toàn (`margin = 12px`):**  
   * Nếu chuột nằm trong bất kỳ khung menu nào (hoặc chuyển tiếp mượt giữa menu chính và submenu): trạng thái là `inside`, reset bộ đếm `outsideCount = 0`.
   * Nếu chuột di chuyển ra khỏi tất cả các khung options:
     * **Trường hợp 1 (đã xem menu rồi rời đi):** Chuột ở ngoài liên tục `150ms` (5 chu kỳ x 30ms) $\rightarrow$ Tự động ẩn menu.
     * **Trường hợp 2 (vừa bật lên đã rê đi chỗ khác):** Chuột ở ngoài liên tục sau 400ms kể từ lúc mở $\rightarrow$ Tự động ẩn menu.
3. **Cơ chế hủy menu đồng bộ (`cancelActiveMenu`):**  
   * Gửi `WM_KEYDOWN VK_ESCAPE` tới các cửa sổ `#32768`.
   * Gửi `WM_CANCELMODE` tới `hMsgWnd`, trong `wndProc` gọi trực tiếp `TrayWin32.EndMenu()` trên chính UI thread.
   * `TrackPopupMenuEx` kết thúc an toàn, `finally DestroyMenu(hMenu)` dọn dẹp sạch sẽ tài nguyên.

---

## 4. Kết Quả Kiểm Thử

* Toàn bộ 522/522 bài kiểm thử tự động (Unit Tests) vượt qua thành công:
  * `BambooMintKey.Core.Tests`: 511 passed.
  * `BambooMintKey.NativeBridge.Tests`: 11 passed.
* Biên dịch `BambooMintKey.UI` hoàn thành sạch sẽ không lỗi, không cảnh báo.
