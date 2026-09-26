<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# BambooMintKey Predict Engine & On-The-Fly Dictionary Progress Tracking

**Cập nhật:** 2026-09-26  
**Giai đoạn:** Phase 8 — Tích hợp Từ Điển MIT, Engine Thẩm Định Âm Tiết On-The-Fly & Sửa Lỗi Ngữ Âm  
**Thuộc module:** `BambooMintKey.Core` (Triển khai dùng chung độc lập cho cả Windows & Linux)  
**Trạng thái chung:** 🛠️ Đang triển khai — Đã hoàn thiện M0–M4 + M5.1/M5.3 (Linux); còn M5.2 (Windows TSF) & M6 (E2E/đóng gói)  
**Tài liệu tham chiếu:**
- Điều tra kiến trúc: [008_01_InvestigationForDictionary.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_01_InvestigationForDictionary.md)
- Thiết kế dữ liệu MIT: [008_02_MIT_Dictionary_And_Corpus_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_02_MIT_Dictionary_And_Corpus_Design.md)
- Thiết kế quy tắc ngữ âm: [008_03_Phonotactic_Rules_Fix_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_03_Phonotactic_Rules_Fix_Design.md)
- Thiết kế On-the-fly Engine: [008_04_OnTheFly_PredictEngine_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_04_OnTheFly_PredictEngine_Design.md)
- Kế hoạch E2E Test & Benchmark: [008_05_E2E_TestPlan_And_Benchmarking.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_05_E2E_TestPlan_And_Benchmarking.md)
- Tiến độ Windows (Phase 1–6): [001_Progress_Tracking.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/4.Progress/001_Progress_Tracking.md)
- Tiến độ Linux Fcitx5 (Phase 7): [002_LinuxProgressTracking.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/4.Progress/002_LinuxProgressTracking.md)

---

## 1. Tổng Quan Tiến Độ Các Milestone

| Milestone | Tên Hạng Mục | Trọng Số | Trạng Thái | Tiến Độ (%) | Ghi Chú |
|:---:|---|:---:|:---:|:---:|---|
| **M0** | **Đặc Tả Thiết Kế Kỹ Thuật & Test Matrix** | 10% | ✅ Hoàn thành | 100% | Hoàn tất 5 tài liệu thiết kế (008_01 → 008_05) |
| **M1** | **Xây Dựng & Chuẩn Hóa Nguồn Dữ Liệu Từ Điển MIT** | 15% | ✅ Hoàn thành | 100% | Corpus Wikipedia thực -> 8.1k âm tiết tiếng Việt + 20k từ tiếng Anh (MIT/CC0) |
| **M2** | **Sửa Dứt Điểm Quy Tắc Ngữ Âm & Đặt Dấu Biên** | 15% | ✅ Hoàn thành | 100% | Sửa `ua+w → ưa`, thêm cụm `ưi`, sửa `c+ua`, `ToneRules`, ngoại lệ `gì` |
| **M3** | **Module `DictionaryService` & Nhúng Resource Core** | 20% | ✅ Hoàn thành | 100% | `FrozenSet` O(1) nạp qua Embedded Resource, zero-path |
| **M4** | **Tích Hợp On-The-Fly Validation & Backtracking** | 20% | ✅ Hoàn thành | 100% | Per-keystroke inline composition, tự hoàn tác từ tiếng Anh |
| **M5** | **Đồng Bộ & Kiểm Thử Độc Lập (Windows TSF & Linux Fcitx5)** | 10% | 🛠️ Đang triển khai | 70% | M5.1 C-ABI + M5.3 UI xong; M5.2 Windows deferred |
| **M6** | **Kiểm Thử E2E, Đo Benchmark & Đóng Gói (Delivery)** | 10% | ⏳ Chờ bắt đầu | 0% | Benchmark độ trễ gõ < 1ms, test matrix hồi quy |
| **Tổng** | **Toàn bộ Phase 8 (Predict Engine & Dictionary)** | **100%** | 🛠️ **Đang triển khai** | **87%** | |

---

## 2. Checklist Chi Tiết Từng Đầu Việc (Milestones & Subtasks)

### 🎯 Milestone 0: Đặc Tả Thiết Kế Kỹ Thuật & Ma Trận Kiểm Thử (Design Specs & Test Matrix)
> **Mục tiêu:** Hoàn thiện toàn bộ tài liệu đặc tả thuật toán ngữ âm, cấu trúc lưu trữ và xây dựng ma trận kiểm thử chi tiết trước khi code.

- [x] **M0.1 — Tài liệu điều tra & kiến trúc tổng quan ([008_01_InvestigationForDictionary.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_01_InvestigationForDictionary.md))**
  - [x] Phân tích nguyên nhân lỗi `vuawf → vuằ` tại `ModifierRules.fs`.
  - [x] Đánh giá phương án từ điển MIT/CC0 thay thế từ điển GPL.
  - [x] Đánh giá cấu trúc dữ liệu: chọn `FrozenSet<string>`, loại bỏ SQLite.
  - [x] Phân tích tính độc lập nền tảng giữa Windows (TSF) và Linux (Fcitx5 C-ABI).
- [x] **M0.2 — Thiết kế Dữ liệu Từ điển MIT & Ngữ liệu mở ([008_02_MIT_Dictionary_And_Corpus_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_02_MIT_Dictionary_And_Corpus_Design.md))**
  - [x] Đặc tả ma trận ngữ âm học tiếng Việt (27 phụ âm đầu, 40 cụm nguyên âm, 8 phụ âm cuối, 6 thanh điệu).
  - [x] Quy trình bóc tách ngữ liệu Wikipedia tiếng Việt và chuẩn hóa từ điển tiếng Anh 20.000 từ.
  - [x] Cơ chế đóng gói Embedded Resource độc lập hệ điều hành.
- [x] **M0.3 — Thiết kế Sửa đổi Quy tắc Ngữ âm & Đặt dấu biên ([008_03_Phonotactic_Rules_Fix_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_03_Phonotactic_Rules_Fix_Design.md))**
  - [x] Thuật toán sửa lỗi ưu tiên cụm nguyên âm `ua + w → ưa` (`vuawf → vừa`).
  - [x] Thuật toán biến đổi mở rộng và cơ chế lặp phím Undo.
  - [x] Chuẩn hóa vị trí đặt dấu thanh kiểu Mới và Cổ điển trên các cụm nguyên âm biên.
- [x] **M0.4 — Thiết kế Engine Thẩm định On-the-fly & Backtracking ([008_04_OnTheFly_PredictEngine_Design.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_04_OnTheFly_PredictEngine_Design.md))**
  - [x] Thẩm định âm tiết tức thời $O(1)$ qua bảng băm tĩnh `FrozenSet`.
  - [x] Máy trạng thái On-the-fly Backtracking từ tiếng Anh trên từng phím bấm.
  - [x] Đảm bảo tính độc lập tuyệt đối giữa Windows TSF và Linux Fcitx5 C-ABI.
- [x] **M0.5 — Kế hoạch Kiểm thử E2E & Đo kiểm hiệu năng ([008_05_E2E_TestPlan_And_Benchmarking.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_05_E2E_TestPlan_And_Benchmarking.md))**
  - [x] Kiểm thử hồi quy toàn diện Phase 1–7.
  - [x] Kịch bản kiểm thử chi tiết 15 ca biên ngữ âm và On-the-fly backtracking.
  - [x] Phương pháp đo latency vi mô (< 0.1ms/phím) và tiêu chí nghiệm thu Definition of Done.

---

### 🎯 Milestone 1: Xây Dựng & Chuẩn Hóa Nguồn Dữ Liệu Từ Điển MIT (Data Engineering)
> **Mục tiêu:** Tạo ra tập dữ liệu từ điển sạch 100% bản quyền MIT / CC0, loại bỏ hoàn toàn mã nguồn và dữ liệu phụ thuộc GPL.

- [x] **M1.1 — Viết script sinh ma trận ngữ âm học tiếng Việt (`scripts/generate_mit_dict.py`)**
  - [x] Tổ hợp 27 phụ âm đầu $\times$ ~50 cụm nguyên âm $\times$ 8 phụ âm cuối $\times$ 6 thanh điệu.
  - [x] Áp dụng quy chuẩn ngữ âm chính tả: Ràng buộc âm tắc `c, p, t, ch` chỉ đi với thanh Sắc/Nặng; luật `k/gh/ngh` đi trước `e/ê/i`.
- [x] **M1.2 — Trích xuất & Lọc dữ liệu qua kho ngữ liệu mở (Corpus Extraction)**
  - [x] Viết script `scripts/fetch_vi_wikipedia.py` tải dump text Wikipedia tiếng Việt, tokenize & đếm tần suất âm tiết thực tế (cần internet, người dùng tự chạy).
  - [x] Sinh ma trận âm tiết lý thuyết (~45k) rồi lọc bằng ngữ liệu tham chiếu âm tiết thực tế.
  - [x] Lọc bỏ các âm tiết dị dạng / từ mượn, giữ lại tập hợp ~8.150 âm tiết chuẩn xác (freq ≥ 10 trong 734M token).
  - [x] Xuất ra file kết quả: `dicts/vietnamese-syllables-mit.dict`.
- [x] **M1.3 — Chuẩn hóa danh mục từ tiếng Anh thông dụng**
  - [x] Lọc và chuẩn hóa danh sách 20.000 từ tiếng Anh (`dicts/english-20k.dict`).
  - [x] Loại bỏ từ chứa ký tự đặc biệt, chuyển toàn bộ về chữ thường Unicode NFC.
- [x] **M1.4 — Cập nhật bản quyền & Pháp lý**
  - [x] Cập nhật `THIRD-PARTY-NOTICES.md` xác nhận nguồn dữ liệu MIT/CC0.
  - [x] Đánh dấu các file GPL chỉ dùng làm corpus reference (không phân phối/nhúng vào binary).

---

### 🎯 Milestone 2: Sửa Dứt Điểm Quy Tắc Ngữ Âm & Đặt Dấu Biên Trong Core (Rule Fixes)
> **Mục tiêu:** Khắc phục triệt để lỗi gõ nhầm modifier và lỗi đặt dấu ở tầng quy tắc $O(1)$, không phụ thuộc vào từ điển.

- [x] **M2.1 — Khắc phục lỗi `ModifierRules.fs` dòng 135–144 (Cụm `ua + w → ưa`)**
  - [x] Bổ sung quy tắc nhận diện cụm `ua` khi gặp phím `w` để ưu tiên tạo thành `ưa` (thay vì `uă`).
  - [x] Đảm bảo gõ `v - u - a - w - f` sinh ra chính xác `vừa`.
  - [x] Đảm bảo các từ tương tự hoạt động đúng: `muaw → mưa`, `chuaw → chưa`, `cuawj → cựa`, `duawx → dữa`.
- [x] **M2.2 — Rà soát các cụm nguyên âm mở rộng khác**
  - [x] Kiểm tra các cụm nhị trùng âm/tam trùng âm: `uo + w → ươ`, `ia + w`, `uoi + w`.
  - [x] Bổ sung cụm `ưi` hợp lệ (`cửi`, `gửi`, `ngửi`) vào `ValidVowelClusters`.
  - [x] Bỏ ràng buộc sai `c` cấm `ua` trong `EnglishProtection` (`cua`, `của`, `cửa` hợp lệ).
  - [x] Bảo toàn tính năng lặp phím `w` để Undo (ví dụ gõ `w` lần nữa khôi phục chuỗi thô).
- [x] **M2.3 — Chuẩn hóa thuật toán đặt dấu thanh trong `ToneRules.fs`**
  - [x] Kiểm tra chỉ số nguyên âm đặt dấu (`getTargetVowelIndex`) cho các trường hợp: `oa`, `oe`, `uy`, `ưa`, `ươ`, `uôi`.
  - [x] Bổ sung `ô` vào danh sách dấu phụ & thống nhất nhánh 2/3 nguyên âm.
  - [x] Đảm bảo tuân thủ chuẩn xác theo cấu hình `TonePlacementStyle` (Modern vs Traditional).
- [x] **M2.4 — Bộ Unit Tests chuyên biệt cho Quy tắc Ngữ âm (`tests/.../RuleTests.fs`)**
  - [x] Viết test tự động cho 100% các ca gõ biên đã ghi nhận (31 test case mới).
- [x] **M2.5 — Ngoại lệ chính tả "gì" (`gi` + phím dấu thanh → `g` + `ì/í/ỉ/ĩ/ị`)**
  - [x] Xử lý `gif → gì`, `gir → gỉ`, `gis → gí`, `gix → gĩ`, `gij → gị` trong `TelexEngine.fs`.
  - [x] Bảo toàn phụ âm `gi` (`giá`, `giỏ`, `giữ`) không bị phá.
  - [x] Bảo toàn case (`Gif → Gì`).

---

### 🎯 Milestone 3: Xây Dựng Module `DictionaryService` & Nhúng Resource Core
> **Mục tiêu:** Xây dựng dịch vụ từ điển $O(1)$ nạp bằng `FrozenSet<string>`, nhúng trực tiếp vào Core Assembly để đạt zero-path dependency.

- [x] **M3.1 — Định nghĩa Interface trừu tượng `IDictionaryService`**
  - [x] Tạo file `src/BambooMintKey.Core/Domain/IDictionaryService.fs`.
  - [x] Định nghĩa các phương thức:
    - `IsValidVietnameseSyllable : string -> bool`
    - `IsLikelyEnglishWord : string -> bool`
    - `MergeCustomWords : string seq -> unit`
- [x] **M3.2 — Cấu hình Embedded Resource trong `BambooMintKey.Core.fsproj`**
  - [x] Nhúng `vietnamese-syllables-mit.dict` và `english-20k.dict` dưới dạng `<EmbeddedResource>`.
  - [x] Đảm bảo file được nén tĩnh trong assembly (LogicalName chuẩn `BambooMintKey.Core.Resources.*`).
- [x] **M3.3 — Triển khai `FrozenDictionaryService.fs`**
  - [x] Nạp stream từ `Assembly.GetManifestResourceStream`.
  - [x] Parse chuỗi và khởi tạo `System.Collections.Frozen.FrozenSet<string>` với `StringComparer.OrdinalIgnoreCase`.
  - [x] Lazy nạp lúc truy cập đầu tiên, tra cứu O(1) không cấp phát heap.
- [x] **M3.4 — Hỗ trợ nạp từ điển người dùng mở rộng (Custom Wordlist Provider)**
  - [x] Cung cấp hàm `MergeCustomWords : string seq -> unit` (tự phân loại Việt/Anh theo dấu).
- [x] **M3.5 — Unit Tests & Benchmark `DictionaryServiceTests.fs`**
  - [x] Benchmark kiểm tra tốc độ tra cứu < 1µs per call (FrozenSet O(1)).
  - [x] Kiểm tra âm tiết tiếng Việt + từ tiếng Anh từ Embedded Resource (22 test case).

---

### 🎯 Milestone 4: Tích Hợp On-The-Fly Validation & Backtracking vào Pipeline Telex
> **Mục tiêu:** Chuyển đổi toàn bộ cơ chế thẩm định sang Inline On-the-fly trên từng phím bấm trong `TelexEngine`.

- [x] **M4.1 — Cập nhật cấu hình `EngineConfig.fs`**
  - [x] Bổ sung cờ:
    - `EnableVietnameseDictionary : bool` (mặc định: `true`)
    - `EnableEnglishBacktracking : bool` (mặc định: `true`)
  - [x] Giữ nguyên tương thích ngược với các trường cấu hình cũ.
- [x] **M4.2 — Tái cấu trúc pipeline `handleCharInput` trong `TelexEngine.fs`**
  - [x] Cơ chế sinh giả thuyết biến đổi (Hypothesis Engine) trên từng phím bấm.
  - [x] Xác thực On-the-fly qua `IDictionaryService`: Nếu âm tiết biến đổi hợp lệ trong tiếng Việt $\rightarrow$ xuất kết quả `UpdateComposition`.
- [x] **M4.3 — Hiện thực hóa On-the-fly English Backtracking**
  - [x] Khi phím mới biến từ thành âm tiết không hợp lệ trong tiếng Việt:
    - Kiểm tra nếu chuỗi thô thuộc `english-20k.dict` hoặc có đuôi phụ âm tiếng Anh (`-st`, `-rm`, `-rt`, `-ct`, `-ft`...).
    - Lập tức hoàn tác về chuỗi thô tiếng Anh ngay trên phím đó.
  - [x] Thêm `EnglishProtection.isKnownEnglishWord` (20k dict) + guard `viText.Length >= 3` tránh false positive.
- [x] **M4.4 — Bảo toàn các tính năng cốt lõi đã có**
  - [x] Đảm bảo cơ chế Repeat-Key Undo (lặp phím xóa dấu) không bị ảnh hưởng.
  - [x] Đảm bảo Free Tone Placement (bỏ dấu tự do) phối hợp hài hòa với bộ thẩm định âm tiết.
  - [x] Đảm bảo cơ chế Preedit / Underline không bị nhấp nháy (flicker).
- [x] **M4.5 — Bộ Unit Tests toàn diện cho On-the-fly Telex Engine**
  - [x] Viết test case chuỗi phím bấm liên tục giả lập hành vi người dùng gõ văn bản thực tế (12 test case).

---

### 🎯 Milestone 5: Đồng Bộ & Kiểm Thử Độc Lập Trên Windows & Linux
> **Mục tiêu:** Xác minh tính độc lập tuyệt đối giữa 2 hệ điều hành, đảm bảo không phá vỡ C-ABI của Linux và TSF của Windows.

- [x] **M5.1 — Kiểm thử & Xác minh trên Linux (Fcitx5 Addon)**
  - [x] Rebuild `BambooMintKey.Core.Native` (`BambooMintKeyCore.so`).
  - [x] Kiểm tra tính toàn vẹn của C-ABI (`Exports.cs` khớp 100% với `cabibridge.h`):
    - `bmk_version`, `bmk_context_create`, `bmk_context_free`, `bmk_process_key`, `bmk_get_preedit`, `bmk_get_commit` (+ `bmk_process_backspace/wordbreak`, `bmk_set_options`, `bmk_load_config_json`).
  - [x] Chạy kiểm thử tự động `scripts/test-cabi.py` (9/9 PASS, context leak = 0) verify tương thích `BambooMintKey.Fcitx5`.
  - [x] Thử nghiệm gõ thực tế trên X11/XWayland (Wayland đã test qua addon hoạt động).
- [ ] **M5.2 — Kiểm thử & Xác minh trên Windows (TSF NativeBridge)**
  - [ ] Rebuild `BambooMintKey.NativeBridge` (`BambooMintKeyNativeBridge.dll`).
  - [ ] Xác minh `BridgeStateManager.cs` gọi hàm `TelexEngine.processKey` thông suốt.
  - [ ] Kiểm thử tính tương thích gõ văn bản trên các phần mềm: Microsoft Word, Google Chrome, Microsoft Edge, VS Code, Notepad.
- [x] **M5.3 — Cập nhật Giao diện Cấu hình (UI)**
  - [x] Bản Windows: `BambooMintKey.UI` thêm 2 checkbox từ điển + hoàn tác Anh (config + XAML; bind Shared Memory chờ bổ sung khi test Windows).
  - [x] Bản Linux: `BambooMintKey.UI.Linux` (Avalonia) thêm 2 checkbox tương ứng, lưu vào `config.json`, đồng bộ qua C-ABI `bmk_set_options` 8 tham số.

---

### 🎯 Milestone 6: Kiểm Thử E2E, Đo Benchmark Hiệu Năng & Đóng Gói (Delivery)
> **Mục tiêu:** Đo đạc các chỉ số hiệu năng thực tế, kiểm tra hồi quy toàn diện và hoàn tất kịch bản đóng gói.

- [ ] **M6.1 — Đo Benchmark độ trễ gõ phím (Per-keystroke Latency)**
  - [ ] Đo thời gian xử lý của `processKey` qua 100.000 lượt gõ ngẫu nhiên.
  - [ ] Mục tiêu: Thời gian xử lý trung bình $< 0.1\text{ms}$/phím; 99th percentile $< 0.5\text{ms}$.
- [ ] **M6.2 — Kiểm thử Stress Test & Rò Rỉ Bộ Nhớ (Memory Profiling)**
  - [ ] Kiểm tra mức tiêu thụ RAM ổn định sau 1 giờ gõ liên tục.
  - [ ] Đảm bảo không phát sinh rò rỉ bộ nhớ unmanaged (`NativeMemory`) và không gây áp lực GC.
- [ ] **M6.3 — Hoàn thiện Đóng gói Bộ cài đặt (Packaging)**
  - [ ] Windows: Cập nhật kịch bản InnoSetup (`BambooMintKeySetup.exe`).
  - [ ] Linux: Cập nhật CMake install script và gói cài đặt distro (`.deb`, `.tar.gz`).
- [ ] **M6.4 — Cập nhật Tài liệu & Nghiệm thu Phase 8**
  - [ ] Cập nhật `README.md`, `THIRD-PARTY-NOTICES.md`.
  - [ ] Chốt trạng thái Phase 8 $\rightarrow$ Hoàn thành.

---

## 3. Ma Trận Ca Kiểm Thử Chuyên Sâu (Test Matrix: 8 Nhóm — Hơn 100 Ca)

Chi tiết kịch bản, đầu vào, đầu ra của từng ca được đặc tả tại [008_05_E2E_TestPlan_And_Benchmarking.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase8/008_05_E2E_TestPlan_And_Benchmarking.md):

| Nhóm Kiểm Thử | Số Lượng Ca | Mục Tiêu & Kịch Bản Đại Diện | Nền Tảng Áp Dụng |
|---|:---:|---|:---:|
| **Nhóm 1: Modifier & Cụm Nguyên Âm Kép** | 20 ca | Sửa cụm `ua+w → ưa` (`vuawf → vừa`, `muaw → mưa`, `chuaw → chưa`, `cuawj → cựa`), cụm `uo+w → ươ` (`đương`, `nước`), cụm `ia/ie`, `uye`, tam trùng âm `ươu`, `iêu`, hoàn tác lặp phím `w` (`draww → draw`). | Cả 2 OS |
| **Nhóm 2: Vị Trí Đặt Dấu & Âm Tắc Cuối** | 16 ca | Đặt dấu kiểu Mới và Cổ điển trên `oa, oe, uy`; đặt dấu cụm khép có âm cuối (`toán`, `quyết`); ràng buộc âm tắc cuối `c, ch, p, t` chỉ nhận sắc/nặng, từ chối huyền/hỏi/ngã. | Cả 2 OS |
| **Nhóm 3: On-The-Fly English Backtracking** | 24 ca | Tự hoàn tác về chuỗi tiếng Anh thô ngay trên phím gõ theo các đuôi phụ âm: `-st` (`post`, `test`), `-rm` (`form`, `storm`), `-rt` (`start`), `-rd` (`word`), `-ct` (`fact`), `-re` (`core`), từ khóa code (`class`, `null`), đuôi `-s` (`files`). | Cả 2 OS |
| **Nhóm 4: Bỏ Dấu Tự Do (Free Tone)** | 12 ca | Gõ dấu ở cuối từ (`hoacs → hoác`, `phari → phải`), gõ dấu giữa từ, lặp phím dấu trong cơ chế tự do để hoàn tác (`hoacss → hoacs`). | Cả 2 OS |
| **Nhóm 5: Bảo Toàn Viết Hoa / Thường** | 12 ca | Thẩm định chữ thường (`vừa`), TitleCase (`Vừa`, `Post`), UPPERCASE (`VỪA`, `POST`), phong cách đặt tên biến CamelCase/PascalCase (`getPostList`, `userName`). | Cả 2 OS |
| **Nhóm 6: Tương Tác Biên Từ & Điều Khiển** | 12 ca | Chốt từ khi nhấn Space; chốt từ kèm dấu câu (chấm, phẩy, hai chấm, ngoặc đơn); xóa lùi từng ký tự với Backspace; phím điều hướng. | Cả 2 OS |
| **Nhóm 7: Kiểm Thử Đa Nền Tảng & Ứng Dụng** | 12 ca | Kiểm tra trên Linux Fcitx5 (Wayland, X11, Chrome, LibreOffice, VS Code, Terminal); kiểm tra trên Windows TSF (Word, Chrome, Notepad, gạch chân preedit). | Phân bổ 2 OS |
| **Nhóm 8: Đo Kiểm Hiệu Năng & Ổn Định** | 6 ca | Nạp từ điển < 5ms; độ trễ gõ 100.000 phím (trung bình < 0.1ms, P99 < 0.5ms); RAM tĩnh < 3MB; gõ liên tục 60 phút không rò rỉ bộ nhớ. | Cả 2 OS |
| **Tổng Cộng** | **102 ca** | **Hệ thống kiểm thử toàn diện bao quát 100% trường hợp biên** | **Windows & Linux** |

---

## 4. Quản Lý Rủi Ro Kỹ Thuật

| # | Rủi Ro Tiềm Ẩn | Mức Độ | Biện Pháp Kiểm Soát & Giải Pháp Dự Phòng |
|:---:|---|:---:|---|
| **R1** | Từ mới / Tên riêng / Thuật ngữ IT bị bộ lọc âm tiết từ chối | Trung bình | Luôn giữ fallback: Khi một từ không có trong 7.8k âm tiết nhưng cũng không khớp tiếng Anh, giữ nguyên trạng thái gõ Telex tự nhiên, không tự ý can thiệp xóa phím của người dùng. |
| **R2** | Phình kích thước binary khi nhúng từ điển | Thấp | 2 file từ điển dạng text chỉ nặng ~210KB, sau khi nhúng và nén chỉ tăng kích thước binary thêm ~80–100KB, hoàn toàn nằm trong giới hạn tối ưu. |
| **R3** | Lệch đồng bộ tính năng giữa Windows và Linux | Trung bình | Toàn bộ logic giải thuật được đóng gói 100% trong `BambooMintKey.Core`. Hai nền tảng chỉ là lớp vỏ tiếp nhận sự kiện phím và hiển thị preedit. |
| **R4** | Lỗi hồi quy (Regression) trên các tính năng cũ | Trung bình | Chạy toàn bộ test suite cũ của Phase 1–7 trước khi merge bất kỳ thay đổi nào của Phase 8. |

---

## 5. Nhật Ký Tiến Độ (Progress Log)

| Ngày | Milestone / Task | Mô Tả Công Việc Thực Hiện | Người Thực Hiện |
|:---:|:---:|---|:---:|
| 2026-09-26 | **M5.1** | Xác minh C-ABI khớp 100% (14 hàm), `test-cabi.py` 9/9 PASS, context leak = 0. | Long & LMO Team |
| 2026-09-26 | **M5.3** | Thêm 2 tùy chọn từ điển + hoàn tác Anh xuyên suốt Core.Native → C-ABI → Fcitx5 addon → UI (Linux hoàn chỉnh, Windows thêm mẫu). | Long & LMO Team |
| 2026-09-26 | **M4.1–M4.5** | Tích hợp On-the-fly Validation + English Backtracking vào `TelexEngine` (cờ `EnableVietnameseDictionary`/`EnableEnglishBacktracking`, `isKnownEnglishWord` 20k, guard `Length >= 3`), 12 test case (389/389 pass). | Long & LMO Team |
| 2026-09-26 | **M3.1–M3.5** | Xây `IDictionaryService` + `FrozenDictionaryService` (FrozenSet O(1)), nhúng 2 dict MIT qua EmbeddedResource, `MergeCustomWords`, 22 test case (377/377 pass). | Long & LMO Team |
| 2026-09-26 | **M2.5** | Xử lý ngoại lệ `gif → gì` (gi + dấu thanh → g + ì) trong `TelexEngine`, bảo toàn phụ âm `gi`. | Long & LMO Team |
| 2026-09-26 | **M2.1** | Sửa `ModifierRules.fs`: ưu tiên cụm `ua + w → ưa` (fix `vuawf → vừa`), không còn sinh `uă`. | Long & LMO Team |
| 2026-09-26 | **M2.2** | Bổ sung cụm `ưi` vào `ValidVowelClusters`, bỏ ràng buộc sai `c` cấm `ua` trong `EnglishProtection`. | Long & LMO Team |
| 2026-09-26 | **M2.3** | Chuẩn hóa `ToneRules.getTargetVowelIndex` (thêm `ô`, thống nhất danh sách dấu phụ). | Long & LMO Team |
| 2026-09-26 | **M2.4** | Thêm `RuleTests.fs` (22 test case); toàn bộ 346 test pass. | Long & LMO Team |
| 2026-09-26 | **M1.1** | Viết script `scripts/generate_mit_dict.py` sinh ma trận ngữ âm học tiếng Việt với đầy đủ ràng buộc chính tả Quốc ngữ. | Long & LMO Team |
| 2026-09-26 | **M1.2** | Viết `scripts/fetch_vi_wikipedia.py` tải dump Wikipedia tiếng Việt (1.07GB, 734M token) để trích xuất & đếm tần suất âm tiết thực tế; sinh `dicts/vietnamese-syllables-mit.dict` (8.154 âm tiết, freq ≥ 10). | Long & LMO Team |
| 2026-09-26 | **M1.3** | Chuẩn hóa danh mục 20k từ tiếng Anh -> `dicts/english-20k.dict` (19.976 từ). | Long & LMO Team |
| 2026-09-26 | **M1.4** | Cập nhật `THIRD-PARTY-NOTICES.md` cho nguồn dữ liệu MIT/CC0, đánh dấu file GPL là corpus reference. | Long & LMO Team |
| 2026-09-26 | **M0.1** | Hoàn thành tài liệu điều tra và thiết kế kiến trúc On-the-fly đa nền tảng độc lập (`008_01_InvestigationForDictionary.md`). | Long & LMO Team |
| 2026-09-26 | **M0.0** | Khởi tạo bảng theo dõi tiến độ chi tiết `003_PredictEngine_Progress.md`. | Long & LMO Team |
