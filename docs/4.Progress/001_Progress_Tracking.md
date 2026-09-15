<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# BambooMintKey Progress Tracking

**Cập nhật:** 2026-09-15  
**Phiên bản hiện tại:** 1.0.0  
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
| Phase 6 | MSIX Store Packaging | 📝 Đang thiết kế | 10% |
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
| 005_05_CACHE | Đảm bảo 2-GUID fix tránh cache của Chromium | — | ✅ Code đã sửa, chờ test | Dev |
| 005_05_UNINSTALL | UI vẫn tự khởi động sau khi gỡ cài đặt | `docs/3.Issue/007_UIAutostartAfterUninstall.md` | ✅ Code đã sửa, chờ build | Dev |
| 005_05_INSTALLER | Build installer mới và test end-to-end | — | ⏳ Chờ | Dev |

### 2.3. Công việc còn lại trong Phase 5

- [ ] Build bộ cài Inno Setup mới từ code hiện tại.
- [ ] Cài đặt trên máy sạch hoặc VM.
- [ ] Test toggle Preedit trên VS Code, Chrome, Edge, Word, Notepad++.
- [ ] Thu thập log `BambooMintKey_Runtime.log` với `BAMBOOMINTKEY_DEBUG=1`.
- [ ] Nếu vẫn lỗi: chạy checklist điều tra trong `005_05_DisplayAttributeProvider.md` mục 6.
- [ ] Nếu ổn: cập nhật trạng thái Phase 5 → hoàn thành.

---

## 3. Phase 6 — MSIX Store Packaging (Đang thiết kế)

### 3.1. Mục tiêu

Tạo bộ cài MSIX và đưa BambooMintKey lên Microsoft Store, song song với bộ cài Inno Setup hiện tại.

### 3.2. Design document

| Tài liệu | Mô tả | Trạng thái |
|---|---|---|
| `docs/2.Design/Phase6/006_00_MSIX_Store_Packaging.md` | Draft outline quy trình MSIX + Store | 📝 Draft |

### 3.3. Các task cần thực hiện

| # | Task | Mô tả | Ưu tiên | Phụ thuộc |
|---|---|---|---|---|
| 6.1 | Quyết định kiến trúc MSIX | Sparse Package vs Packaged COM vs Hybrid | Cao | — |
| 6.2 | Reserve tên app trên Partner Center | Đăng ký `BambooMintKey` / Publisher CN | Cao | 6.1 |
| 6.3 | Tạo `AppxManifest.xml` mẫu | Identity, capabilities, COM extension | Cao | 6.1, 6.2 |
| 6.4 | Tạo assets Store | Logo 50x50, 150x150, 44x44, Wide 310x150, screenshot 1366x768 | Trung bình | 6.2 |
| 6.5 | Viết `scripts/stage-msix.ps1` | Copy artifact vào thư mục staging | Cao | 6.3 |
| 6.6 | Viết `scripts/build-msix.ps1` | MakeAppx + sign + validate | Cao | 6.5 |
| 6.7 | Test sideload MSIX | Cài trên VM sạch, verify UI mở được | Cao | 6.6 |
| 6.8 | Test TSF trong MSIX context | Gõ tiếng Việt sau khi cài từ MSIX | Cao | 6.7 |
| 6.9 | CI/CD GitHub Actions | Workflow build & upload MSIX artifact | Trung bình | 6.6 |
| 6.10 | Submit lên Microsoft Store | Partner Center submission | Thấp | 6.8 |

### 3.4. Câu hỏi cần trả lời

- [ ] Tên Publisher trong Store là gì?
- [ ] Có đồng ý dùng Sparse Package (cần full trust) không?
- [ ] Có cần migration config từ `%APPDATA%\BambooMintKey\config.json` sang MSIX container không?
- [ ] Có giữ 2 kênh phân phối song song (GitHub `.exe` + Store MSIX) không?

---

## 4. Bug / Issue đang mở

| # | Issue | Mô tả | Trạng thái | File |
|---|---|---|---|---|
| 004 | Lỗi hiển thị ICON EV | Icon chế độ V/E không đồng bộ với trạng thái thực tế | 🔍 Đang điều tra | `docs/3.Issue/004_EV_IconError.md` |
| 005 | Shortcut Key Auto Reset | Phím tắt chuyển V/E tự động reset về Ctrl+Shift | 🔍 Đang điều tra | `docs/3.Issue/005_ShortcutKeyAutoResetError.md` |
| 006 | Preedit Toggle Not Working | Toggle Preedit trên UI không có tác dụng trên ứng dụng | 🔍 Đang điều tra | `docs/3.Issue/006_PreeditToggleNotWorking.md` |
| 007 | UI Autostart After Uninstall | UI vẫn chạy cùng Windows sau khi gỡ cài đặt | ✅ Code đã sửa, chờ build | `docs/3.Issue/007_UIAutostartAfterUninstall.md` |

### 4.1. Thứ tự ưu tiên sửa bug

1. **Issue 007** — đã sửa, chỉ cần build + test installer.
2. **Issue 006** — liên quan Phase 5 đang kiểm thử, cần verify sau khi cài lại build mới.
3. **Issue 004** — cần điều tra thêm, có thể liên quan đến shared memory sync.
4. **Issue 005** — cần thu thập thêm thông tin tái hiện.

---

## 5. Kế hoạch tuần tới (Next 7 Days)

| Ngày | Công việc | Kết quả mong đợi |
|---|---|---|
| Day 1 | Build installer mới từ code hiện tại | Có `BambooMintKey-Setup.exe` mới |
| Day 2 | Test uninstall + kiểm tra key Run | Issue 007 closed |
| Day 3-4 | Test Preedit toggle trên các app | Log + kết luận Issue 006 |
| Day 5 | Nếu 006 còn lỗi: debug TSF cache/atom | Fix code nếu cần |
| Day 6 | Cập nhật tài liệu 005_05 và progress | Docs sync với code |
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
