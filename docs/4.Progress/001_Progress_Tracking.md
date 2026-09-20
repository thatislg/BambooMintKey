<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# BambooMintKey Progress Tracking

**Cập nhật:** 2026-09-18  
**Phiên bản hiện tại:** 1.0.1  
**Branch chính:** `main`

---

## 1. Tổng quan các Phase

| Phase | Tên | Trạng thái | Tỷ lệ hoàn thành |
|---|---|---|---|
| Phase 1 | Investigation & Research | ✅ Hoàn thành | 100% |
| Phase 2 | TSF Integration & COM Lifecycle | ✅ Hoàn thành | 100% |
| Phase 3 | Taskbar, Icon, Settings GUI | ✅ Hoàn thành | 100% |
| Phase 4 | Free Tone, English Protection | ✅ Hoàn thành | 100% |
| Phase 5 | Display Attribute, Preedit Toggle | 🧪 Đang kiểm thử | 90% |
| Phase 6 | Win32 Store App & Distribution | 🛠️ Đang triển khai | 70% |
| Phase 7 | (Dự phòng) | ⏸️ Chưa bắt đầu | 0% |

---

## 2. Phase 5 — Cải Tiến Trải Nghiệm Gõ (Đang kiểm thử)

### 2.1. Đã hoàn thành

| Task | Mô tả | File liên quan |
|---|---|---|
| 005_00 | Free Tone Placement | `docs/2.Design/Phase5/005_00_Free_Tone_Placement_Design.md` |
| 005_01 | Uppercase Modifier & Syllable Expansion | `docs/2.Design/Phase5/005_01_Uppercase_Modifier_Syllable_Design.md` |
| 005_02 | English Word Protection | `docs/2.Design/Phase5/005_02_English_Word_Telex_Protection.md` |
| 005_03 | Auto-capitalization Fix | `docs/2.Design/Phase5/005_03_Auto-capitalization-after-uppercase-D.md` |
| 005_04 | Invalid Tone Order | `docs/2.Design/Phase5/005_04_Invalid-tone-from-wrong-Telex-order-suawr.md` |
| 005_05 | Display Attribute Provider & Preedit Toggle | `docs/2.Design/Phase5/005_05_DisplayAttributeProvider.md` |

### 2.2. Đang mở

| Task | Mô tả | Issue | Trạng thái | Owner |
|---|---|---|---|---|
| 005_05_VERIFY | Verify Preedit toggle hoạt động đúng trên VS Code, Chrome, Edge, Word | `docs/3.Issue/006_PreeditToggleNotWorking.md` | 🔍 Đang điều tra | Dev |
| 005_05_GLOBALVE | Ép global V/E mode khi chuyển focus giữa các ứng dụng | `docs/3.Issue/008_PerApplicationVEMode.md` | 🛠️ Code đã sửa, chờ build/test | Dev |
| 005_05_CACHE | Đảm bảo 2-GUID fix tránh cache của Chromium | — | ✅ Code đã sửa, chờ test | Dev |
| 005_05_UNINSTALL | UI vẫn tự khởi động sau khi gỡ cài đặt | `docs/3.Issue/007_UIAutostartAfterUninstall.md` | ✅ Code đã sửa, chờ build | Dev |
| 005_05_INSTALLER | Build installer mới và test end-to-end | — | ⏳ Chờ | Dev |

### 2.3. Công việc còn lại trong Phase 5

- [ ] Build bộ cài Inno Setup mới từ code hiện tại (bao gồm fix global V/E mode).
- [ ] Cài đặt trên máy sạch hoặc VM.
- [ ] Test chuyển focus giữa các app: mode V/E phải giữ nguyên toàn hệ thống.
- [ ] Test toggle Preedit trên VS Code, Chrome, Edge, Word, Notepad++.
- [ ] Thu thập log `BambooMintKey_Runtime.log` với `BAMBOOMINTKEY_DEBUG=1`.
- [ ] Nếu vẫn lỗi: chạy checklist điều tra trong `005_05_DisplayAttributeProvider.md` mục 6.
- [ ] Nếu ổn: cập nhật trạng thái Phase 5 → hoàn thành.

---

## 3. Phase 6 — Win32 Store App & Phân Phối (Inno Setup)

### 3.1. Mục tiêu

Phát hành BambooMintKey lên Microsoft Store dưới dạng **Win32 Desktop App (`.exe`)** sử dụng bộ cài Inno Setup, đồng bộ với GitHub Releases và WinGet. Hủy bỏ hoàn toàn mô hình MSIX thuần do không tương thích cơ chế TSF.

### 3.2. Design document

| Tài liệu | Mô tả | Trạng thái |
|---|---|---|
| `docs/2.Design/Phase6/006_00_MSIX_Store_Packaging.md` | Thiết kế phân phối Win32 Store App | 🎯 Approved |

### 3.3. Các task cần thực hiện

| # | Task | Mô tả | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| 6.1 | Quyết định kiến trúc Store | Chọn Win32 Store App (.exe), loại bỏ MSIX | Cao | ✅ Hoàn thành |
| 6.2 | Quản lý phiên bản tập trung | `Directory.Build.props` -> Inno Setup + UI | Cao | ✅ Hoàn thành |
| 6.3 | Hoàn thiện Inno Setup script | Hỗ trợ `/VERYSILENT /NORESTART`, restart ctfmon | Cao | ✅ Hoàn thành |
| 6.4 | Script `build-installer.ps1` | Tự động build NativeAOT + UI + Inno Setup | Cao | ✅ Hoàn thành |
| 6.5 | Kiểm thử cài đặt / gỡ cài đặt local | Test silent install và gỡ sạch sẽ | Cao | ⏳ Đang kiểm thử |
| 6.6 | Chuẩn bị tài sản Store | Icon 512x512, screenshots giao diện | Trung bình | ⏳ Chờ |
| 6.7 | Đăng ký Partner Center | Đăng ký app name `BambooMintKey`, nộp link .exe | Trung bình | ⏳ Chờ |

## 4. Bug / Issue đang mở

| # | Issue | Mô tả | Trạng thái | File |
|---|---|---|---|---|
| 004 | Lỗi hiển thị ICON EV | Icon chế độ V/E không đồng bộ với trạng thái thực tế | 🔍 Đang điều tra | `docs/3.Issue/004_EV_IconError.md` |
| 005 | Phím tắt chuyển V/E cần thêm phím Space mới có hiệu lực | Bấm Ctrl+Shift toggle không kích hoạt ngay, phải thêm Space | 🔍 Đang điều tra | `docs/3.Issue/005_ShortcutKeyAutoResetError.md` |
| 006 | Preedit Toggle Not Working | Toggle Preedit trên UI không có tác dụng trên ứng dụng | 🔍 Đang điều tra | `docs/3.Issue/006_PreeditToggleNotWorking.md` |
| 007 | UI Autostart After Uninstall | UI vẫn chạy cùng Windows sau khi gỡ cài đặt | ✅ Code đã sửa, chờ build | `docs/3.Issue/007_UIAutostartAfterUninstall.md` |
| 008 | Per-Application V/E Mode | Trạng thái V/E không đồng nhất giữa các ứng dụng | 🛠️ Code đã sửa, chờ build/test | `docs/3.Issue/008_PerApplicationVEMode.md` |

### 4.1. Thứ tự ưu tiên sửa bug

1. **Issue 008** — đã implement `OnSetFocus` resync global V/E mode; cần build + test chuyển focus giữa các app.
2. **Issue 007** — đã sửa, chỉ cần build + test installer.
3. **Issue 006** — liên quan Phase 5 đang kiểm thử, cần verify sau khi cài lại build mới.
4. **Issue 005** — phím tắt chuyển V/E cần thêm Space mới có hiệu lực; cần xác nhận cấu hình hotkey và fix nhận diện phím tắt.
5. **Issue 004** — cần điều tra thêm, có thể liên quan đến shared memory sync.

---

## 5. Kế hoạch tuần tới (Next 7 Days)

| Ngày | Công việc | Kết quả mong đợi |
|---|---|---|
| Day 1 | Build installer mới từ code hiện tại | Có `BambooMintKey-Setup.exe` mới |
| Day 2 | Test uninstall + kiểm tra key Run + test chuyển focus V/E global | Issue 007 & 008 closed |
| Day 3-4 | Test Preedit toggle trên các app | Log + kết luận Issue 006 |
| Day 5 | Nếu 006/008 còn lỗi: debug TSF cache/atom/resync | Fix code nếu cần |
| Day 6 | Cập nhật tài liệu 005_05, 008, và progress | Docs sync với code |
| Day 7 | Bắt đầu Phase 6: chọn kiến trúc MSIX | Decision record cho Sparse Package / Packaged COM |

---

## 6. Definition of Done cho từng Phase

### Phase 5 Done Criteria

- [ ] Build installer mới thành công.
- [ ] Cài đặt + uninstall không để lại key Run.
- [ ] Preedit toggle hoạt động đúng trên ít nhất VS Code, Chrome, Notepad++.
- [ ] Không có regression trong engine gõ tiếng Việt.
- [ ] Tài liệu `005_05_DisplayAttributeProvider.md` phản ánh đúng triển khai.

### Phase 6 Done Criteria

- [ ] Có thể build `BambooMintKey.msix` từ command line.
- [ ] MSIX cài được trên VM sạch (sideload).
- [ ] TSF hoạt động sau khi cài MSIX.
- [ ] Có GitHub Actions workflow build MSIX.
- [ ] Submit lên Microsoft Store (hoặc ít nhất ready for submission).

---

## 7. Notes

- Phase 6 phụ thuộc vào Phase 5 ổn định, vì MSIX sẽ đóng gói cùng DLL/UI.
- Các warning null-safety trong `SharedConfig.fs` không blocker nhưng nên dọn dẹp trước khi lên Store.
- Nên tạo `CHANGELOG.md` khi bắt đầu publish nhiều channel.
