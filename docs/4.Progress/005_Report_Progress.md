# BambooMintKey — Báo Cáo Khoa Học 002 Progress Tracking

**Cập nhật:** 2026-09-29
**Giai đoạn:** Viết báo cáo khoa học `docs/5.Research/002/` (Technical Showcase: Zero-Dependency, Cross-Platform IME với .NET 10 NativeAOT & C-ABI Interop)
**Thuộc module:** Tài liệu nghiên cứu (`docs/5.Research/002/`)
**Trạng thái chung:** 🛠️ Đã scaffold xong cây chương `.mdx` (40 file) + bảng khảo sát Related Work. Chuẩn bị viết nội dung từng chương theo 2 RQ.
**Tài liệu tham chiếu:**
- Outline tổng: [002_ShowCase_ZeroDependency.md](../5.Research/002_ShowCase_ZeroDependency.md)
- Bộ luật viết & kiểm chứng: [006_Report_Writing_Rules.md](006_Report_Writing_Rules.md)
- Script benchmark: [bench-cabi.py](../../scripts/tests/bench-cabi.py)

---

## 1. Tổng Quan Tiến Độ Các Milestone

| Milestone | Tên Hạng Mục | Trọng Số | Trạng Thái | Tiến Độ (%) | Ghi Chú |
|:---:|---|:---:|:---:|:---:|---|
| **M0** | **Thiết lập luật viết & chốt 2 Research Questions** | 5% | ✅ Hoàn thành | 100% | Tạo 006_Report_Writing_Rules.md, chốt RQ1 + RQ2 |
| **M1** | **Chương 1 — Introduction & Background** (4 file) | 10% | ⬜ Chưa bắt đầu | 0% | Đặt bối cảnh + RQ + contributions |
| **M2** | **Chương 2 — Related Work** (survey + patterns + positioning) | 10% | 🛠️ Đang triển khai | 30% | Bảng khảo sát đã có, cần chốt số liệu + viết 2.2/2.3 |
| **M3** | **Chương 3 — System Architecture** (4 file) | 10% | ⬜ Chưa bắt đầu | 0% | Layer separation, source map, diagram, runtime boundary |
| **M4** | **Chương 4 — C-ABI Surface** (4 file) | 10% | ⬜ Chưa bắt đầu | 0% | SDK config, unmanaged surface, action code, exports |
| **M5** | **Chương 5 — Memory Management** (4 file) | 10% | ⬜ Chưa bắt đầu | 0% | Ownership, fixed buffers, why-design, thread safety |
| **M6** | **Chương 6 — Fcitx5 Addon** (5 file) | 10% | ⬜ Chưa bắt đầu | 0% | Build/link, factory, composition loop, preedit, D-Bus |
| **M7** | **Chương 7 — Cross-Platform** (2 file) | 10% | ⬜ Chưa bắt đầu | 0% | Windows TSF + macOS IMK future |
| **M8** | **Chương 8 — Deployment** (3 file) | 10% | ⬜ Chưa bắt đầu | 0% | Linux layout, Flatpak/SteamOS, Windows packaging |
| **M9** | **Chương 9 — Evaluation & Measurements** (4 file) | 10% | ⬜ Chưa bắt đầu | 0% | Chạy bench-cabi.py để có số đo thật |
| **M10** | **Chương 10–12 + Abstract** (wrap-up) | 5% | ⬜ Chưa bắt đầu | 0% | Limitations, Conclusion, References, Abstract viết cuối |
| **Tổng** | **Toàn bộ báo cáo 002** | **100%** | 🛠️ **Đang viết** | **~8%** | |

---

## 2. Checklist Chi Tiết Từng Đầu Việc

### 🎯 M0 — Luật viết & Research Questions (✅ 100%)
- [x] Tạo `006_Report_Writing_Rules.md` (traceability + kiểm chứng, không suy diễn)
- [x] Chốt RQ1 (cross-platform .NET) và RQ2 (F# trong desktop)
- [x] Định nghĩa marker bằng chứng `[MEASURE]` / `[ESTIMATE]` / `[CLAIM]` / `[TODO]`

### 🎯 M1 — Chương 1 Introduction (⬜ 0%)
- [ ] `01-cross-platform-input-method-problem.mdx`
- [ ] `02-vietnamese-orthography.mdx`
- [ ] `03-the-project.mdx`
- [ ] `04-purpose-and-contributions.mdx` — phát biểu RQ1 + RQ2 + giả thuyết
- [ ] Mục `1.4 Evaluation` (tự đánh giá chương)

### 🎯 M2 — Chương 2 Related Work (🛠️ 30%)
- [x] `01-survey-of-vietnamese-imes.mdx` — bảng khảo sát (cần chốt số liệu `~`/`?`)
- [ ] `02-architectural-patterns.mdx`
- [ ] `03-positioning-this-work.mdx`
- [ ] Chốt nguồn cho từng hàng (năm phát hành, user base, license)
- [ ] Mục `2.x Evaluation` (so sánh định vị)

### 🎯 M3 — Chương 3 System Architecture (⬜ 0%)
- [ ] `01-layer-separation.mdx`
- [ ] `02-source-tree-mapping.mdx`
- [ ] `03-architecture-diagram.mdx`
- [ ] `04-runtime-boundary.mdx` — phạm vi trung thực của "zero-dependency"
- [ ] Mục `3.x Evaluation` (tỷ lệ code dùng chung)

### 🎯 M4 — Chương 4 C-ABI Surface (⬜ 0%)
- [ ] `01-project-sdk-configuration.mdx`
- [ ] `02-unmanaged-surface.mdx`
- [ ] `03-action-code-contract.mdx`
- [ ] `04-exporting-entrypoints.mdx`
- [ ] Mục `4.x Evaluation` (footprint, symbol, overhead)

### 🎯 M5 — Chương 5 Memory Management (⬜ 0%)
- [ ] `01-ownership-contract.mdx`
- [ ] `02-fixed-size-native-buffers.mdx`
- [ ] `03-why-this-design.mdx`
- [ ] `04-thread-safety.mdx`
- [ ] Mục `5.x Evaluation` (allocation, GC pressure, leak)

### 🎯 M6 — Chương 6 Fcitx5 Addon (⬜ 0%)
- [ ] `01-build-and-link-strategy.mdx`
- [ ] `02-addon-factory-lifecycle.mdx`
- [ ] `03-composition-loop.mdx`
- [ ] `04-preedit-vs-direct-commit.mdx`
- [ ] `05-dbus-and-config-hot-reload.mdx`
- [ ] Mục `6.x Evaluation` (composition correctness, latency)

### 🎯 M7 — Chương 7 Cross-Platform (⬜ 0%)
- [ ] `01-windows-tsf-bridge.mdx`
- [ ] `02-macos-imk-future.mdx`
- [ ] Mục `7.x Evaluation` (tỷ lệ tái sử dụng lõi)

### 🎯 M8 — Chương 8 Deployment (⬜ 0%)
- [ ] `01-linux-system-layout.mdx`
- [ ] `02-flatpak-steamos.mdx`
- [ ] `03-windows-packaging.mdx`
- [ ] Mục `8.x Evaluation` (kích thước gói, độ phức tạp cài)

### 🎯 M9 — Chương 9 Evaluation (⬜ 0%)
- [ ] Build `.so` trên Linux để chạy `bench-cabi.py`
- [ ] `01-binary-footprint.mdx` — `[MEASURE]`
- [ ] `02-startup-and-latency.mdx` — `[MEASURE]`
- [ ] `03-correctness.mdx` — `[MEASURE]`
- [ ] `04-memory-safety-leak-checks.mdx` — `[MEASURE]`

### 🎯 M10 — Wrap-up (⬜ 0%)
- [ ] `10-limitations-and-future-work/index.mdx`
- [ ] `11-conclusion/index.mdx` — tổng kết RQ1 + RQ2
- [ ] `12-references/index.mdx` — tổng hợp citation
- [ ] `00-abstract.mdx` — viết cuối cùng

---

## 3. Nhật Ký Tiến Độ (Progress Log)

| Ngày | Milestone | Hoạt động | Kết quả |
|---|---|---|---|
| 2026-09-29 | M0 | Scaffold cây chương `.mdx`, đổi `.md` → `.mdx`, tạo bảng khảo sát Related Work (tách cột phát triển/user base) | 40 file + bảng ~30 bộ gõ |
| 2026-09-29 | M0 | Chốt luật viết (traceability) + 2 RQ | `006_Report_Writing_Rules.md` |
