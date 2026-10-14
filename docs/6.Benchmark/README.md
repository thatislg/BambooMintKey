# Chiến Lược Benchmark Bộ Gõ Tiếng Việt (Vietnamese IME Benchmark Specification)

Tài liệu này định hình kiến trúc, phương pháp luận và tiêu chuẩn đánh giá toàn diện cho hệ sinh thái bộ gõ tiếng Việt (bao gồm BambooMintKey và các bộ gõ mã nguồn mở / thương mại khác như UniKey, EVKey, OpenKey, Bamboo, GoTiengViet, IBus-Bamboo, Fcitx5-Bamboo,...).

---

## 1. Bối Cảnh & Động Lực

Trải qua hơn 30 năm phát triển kể từ thời VietKey, VietStar đến UniKey và các bộ gõ hiện đại, cộng đồng người dùng và lập trình viên Việt Nam đã chứng kiến sự ra đời của gần **40 bộ gõ** trên đa nền tảng (Windows, macOS, Linux, Android, iOS).

Tuy nhiên, hệ sinh thái đang đối mặt với những vấn đề tồn đọng kéo dài:
1. **Thiếu chuẩn kiểm thử khách quan (Ground Truth Benchmark):** Chưa có một bộ dữ liệu kiểm thử (Golden Dataset) chuẩn hóa được cộng đồng đồng thuận để đo lường độ đúng chính tả và khả năng phục hồi lỗi.
2. **Tranh cãi cảm tính:** Các so sánh giữa các bộ gõ thường dựa trên trải nghiệm chủ quan của từng cá nhân ("bộ này mượt hơn", "bộ kia hay lỗi").
3. **Môi trường ứng dụng ngày càng phân mảnh & phức tạp:** 
   - Ứng dụng Desktop truyền thống (Win32 Edit control, WinForms, Cocoa).
   - Ứng dụng UI hiện đại (WPF, WinUI 3, UWP).
   - Ứng dụng Web / Electron (VS Code, Chrome, Slack, Discord) với cơ chế Text Input Renderer riêng.
   - Canvas-based editors (Google Docs canvas mode, Figma).
   - Terminal emulator (Windows Terminal, Alacritty, iTerm2).

Nhằm giải quyết triệt để bài toán này với chi phí tối ưu và tính thuyết phục cao, hệ thống benchmark được phân tách thành **2 tầng độc lập (Two-Tier Architecture)**.

---

## 2. Kiến Trúc Benchmark 2 Tầng (Two-Tier Architecture)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        VIETNAMESE IME BENCHMARK                        │
└────────────────────────────────────────────────────────────────────────┘
                                    │
          ┌─────────────────────────┴─────────────────────────┐
          ▼                                                   ▼
┌───────────────────────────────────┐       ┌───────────────────────────────────┐
│     TẦNG 1: ENGINE CORRECTNESS    │       │     TẦNG 2: REAL APP TESTING      │
│          (Golden Test)            │       │         (Integration)             │
├───────────────────────────────────┤       ├───────────────────────────────────┤
│ • Không phụ thuộc OS UI hook      │       │ • Bắn phím qua OS API:            │
│ • Chạy in-memory, deterministic   │       │   - Windows: SendInput            │
│ • Kiểm thử độ đúng chính tả       │       │   - macOS: CGEvent                │
│ • Phục hồi lỗi & Undo (<BS>)      │       │   - Linux: uinput / ydotool       │
│ • Bảo vệ từ tiếng Anh             │       │ • Kiểm thử trên app thật:         │
│ • Corpus thực tế 1.000 - 5.000 câu│       │   Chrome, VS Code, Word, Terminal │
│ • Kết quả: % đúng theo từng nhóm  │       │ • Đo độ trễ, nuốt phím, RAM/CPU   │
│ • Chi phí: Cực rẻ, chạy CI/CD     │       │ • Chi phí: Cần harness giả lập    │
└───────────────────────────────────┘       └───────────────────────────────────┘
```

---

## 3. Tầng 1: Độ Đúng Của Engine (Golden Test)

### 3.1. Đặc Điểm
- **In-process / Standalone:** Gọi trực tiếp vào engine xử lý (ví dụ `TelexEngine.processKey` trong F# hoặc hàm tương đương trong C/Rust/Go).
- **Zero Flakiness:** Đầu vào chuỗi phím thuần túy (deterministic input), đầu ra chuỗi văn bản UTF-8 (NFC).
- **Thực thi siêu tốc:** Hàng chục nghìn test case có thể chạy trong vài giây trên máy tính cá nhân hoặc trong pipeline GitHub Actions.

### 3.2. Định Dạng File Test Case (`.cases`)
Mỗi dòng là một bản ghi phân tách bởi dấu `|`:
```text
<kiểu gõ> | <chuỗi phím đầu vào> | <kết quả mong đợi> | <nhóm> | <ghi chú>
```

#### Quy ước token đặc biệt trong chuỗi phím:
- `<BS>`: Phím Backspace (xóa lùi một ký tự hoặc kích hoạt logic hoàn tác).
- `<SPACE>`: Phím dấu cách (khoảng trắng / ngắt từ).
- Ký tự gạch đứng (`|`) hoặc ký hiệu thăng (`#`) bắt đầu comment giải thích.

#### Ví dụ:
```text
telex | thuaanj   | thuận              | vowel_cluster | Gõ nguyên âm đôi kèm dấu nặng
telex | hoaf      | hòa / hoá          | tone_style    | Chấp nhận cả kiểu mới (hòa) và kiểu cũ (hoá)
telex | dduwowcj  | được               | shortcuts     | Gõ tắt uow kèm dd
telex | w         | ư                  | standalone_w  | w đứng riêng làm ư
telex | aa<BS>    | a                  | undo_bs       | â lùi 1 phím về a
telex | windows   | windows            | english       | Không biến thành windơs
telex | toans     | toán               | free_tone     | Bỏ dấu tự do muộn
```

### 3.3. Các Nhóm Tiêu Chí Phân Loại (Test Categories)

1. **Đặt dấu thanh (Tone Placement - `tone_style`):**
   - Phân biệt kiểu mới (hòa, thúy, xòe) và kiểu cũ (hoá, thuý, xoè).
   - **Nguyên tắc chấm điểm:** Cả hai kiểu đều được coi là đúng nếu bộ gõ xuất đúng theo cấu hình được kích hoạt hoặc chấp nhận đồng thời (Dual-Acceptance) để không chấm sai oan uổng tác giả.

2. **Gõ tắt và biến thể nguyên âm (Shortcuts & Digraphs - `shortcuts`):**
   - Các tổ hợp nguyên âm đặc biệt: `uow` -> `ươ`, `w` -> `ư`, `[`, `]` -> `ơ`, `ư`.
   - Modifier lặp: `dd` -> `đ`, `aa` -> `â`, `ee` -> `ê`, `oo` -> `ô`, `aw` -> `ă`, `ow` -> `ơ`, `uw` -> `ư`.

3. **Phục hồi lỗi & Hoàn tác (Error Recovery & Undo - `undo`):**
   - Xóa lùi giữa từ bằng `<BS>`: `aa<BS>` -> `a`, `thoong<BS>` -> `thôn`.
   - Hoàn tác dấu do gõ lặp phím: `mass` -> `mas` (hủy sắc), `aww` -> `aw`.
   - Gõ lại / đổi dấu: `toanfs` -> `toán` (huyền rồi đổi thành sắc), `toansz` -> `toan` (hủy dấu bằng phím z).
   - Gõ dấu muộn (Free Tone Placement): `toan` rồi gõ `s` -> `toán`.

4. **Bảo vệ từ tiếng Anh (English Word Protection - `english`):**
   - Từ vựng phổ biến: `windows`, `user`, `facebook`, `format`, `post`, `clear`.
   - Cơ chế tự phục hồi (Auto-Restore): Khi người dùng đang gõ một từ tiếng Anh hợp lệ hoặc cấu trúc vi phạm ngữ âm tiếng Việt, engine tự rollback về chuỗi thô.

5. **Corpus tiếng Việt thực tế (Real-world Corpus - `corpus`):**
   - Trích xuất 1.000 – 5.000 câu văn bản chuẩn từ Wikipedia tiếng Việt, báo chí, văn học.
   - Sử dụng bộ sinh phím tự động (Keystroke Synthesizer) để biến văn bản đích thành chuỗi phím Telex/VNI tự nhiên.
   - Nạp chuỗi phím vào engine và so sánh văn bản tái lập (Round-trip Fidelity).
   - Đây là bài kiểm tra toàn diện và có tính thuyết phục thực tế cao nhất.

### 3.4. Cách Thức Chấm Điểm
- **Không dùng một điểm số tổng duy nhất:** Tránh việc một bộ gõ đạt 98% tổng điểm nhưng lại hỏng hoàn toàn ở tính năng gõ tiếng Anh hoặc backspace.
- **Báo cáo theo ma trận %:**
  $$\text{Score}_{\text{Category}} = \frac{\text{Số test case đạt}}{\text{Tổng số test case trong nhóm}} \times 100\%$$
- **Đo hiệu năng (Throughput & Latency):**
  - Thời gian xử lý trung bình mỗi phím ($\mu s/\text{key}$).
  - Bộ nhớ phân bổ trên mỗi 100.000 lượt gõ (Zero Allocation / GC Stability).

---

## 4. Tầng 2: Chạy Thật Trong Ứng Dụng (Real-world Integration)

### 4.1. Đặc Điểm
- Sử dụng công cụ bắn sự kiện bàn phím OS cấp thấp:
  - **Windows:** Win32 API `SendInput` (giả lập scancode và virtual key).
  - **macOS:** Quartz Event Services API `CGEventCreateKeyboardEvent` / `CGEventPost`.
  - **Linux:** Kernel level `/dev/uinput` hoặc công cụ `ydotool` / `xdotool`.
- Đọc lại văn bản hiển thị từ clipboard, UI Automation (UIA/AXUIElement), hoặc text buffer của ứng dụng.

### 4.2. Danh Sách Ứng Dụng Mục Tiêu (Application Matrix)
Bắt đầu với 5 ứng dụng cốt lõi đại diện cho các công nghệ giao diện phổ biến:

| Ứng Dụng | Công Nghệ Giao Diện | Thách Thức Điển Hình |
| :--- | :--- | :--- |
| **Google Chrome / Edge** | Chromium / Blink (Address bar & DOM content) | Ô tìm kiếm tự động gợi ý (Autocomplete conflict), mất focus |
| **VS Code / Cursor** | Electron + WebGL/Canvas + Monaco Editor | Xung đột phím tắt IDE, mất chữ khi gõ nhanh, extension lồng ghép |
| **Microsoft Word** | RichEdit / Win32 Custom Text Framework | AutoCorrect, xử lý dấu phức tạp, con trỏ giật |
| **Windows Terminal** | DirectWrite / Win32 ConPTY | Gõ ký tự tiếng Việt trong shell (PowerShell/WSL), echo ký tự |
| **Password Box** | PasswordBox (`ES_PASSWORD`) | Engine phải tự động bypass/passthrough không lưu buffer |

### 4.3. Các Chỉ Số Đo Lường Định Lượng
1. **Tỷ lệ mất/đảo ký tự ở tốc độ cao (Keystroke Swallowing & Reordering):**
   - Bắn phím ở tốc độ cao: 10, 15, 20 phím/giây (keys/second).
   - Đếm số lần ký tự bị nhảy thứ tự (ví dụ: `thựoc` thay vì `được`) do xung đột async/sync giữa hook và ứng dụng.
2. **Độ trễ phím → hiển thị (Input-to-Display Latency):**
   - Đo thời gian từ lúc phát event `KeyDown` đến khi văn bản xuất hiện trên màn hình.
3. **Mức tiêu thụ tài nguyên (Footprint):**
   - RAM tĩnh (Working Set khi chạy nền).
   - CPU % khi gõ liên tục ở 100 WPM.
4. **Trải nghiệm thị giác & Tính ổn định:**
   - Preedit underline: Đường gạch chân có bị giật, nhấp nháy (flicker) hay không.
   - Tỷ lệ crash / treo tiến trình host.

---

## 5. Tổ Chức Mã Nguồn & Tinh Thần Cộng Đồng (Open Collective)

Để dự án benchmark trở thành tài sản chung của cộng đồng lập trình viên Việt Nam:

### 5.1. Cấu Trúc Repository Đề Xuất
```text
vietnamese-ime-benchmark/
├── cases/                     # Kho test case chung (Golden Dataset)
│   ├── 01_tone_placement.cases
│   ├── 02_shortcuts.cases
│   ├── 03_undo_recovery.cases
│   ├── 04_english_words.cases
│   └── 05_real_corpus_1k.cases
├── generators/                # Bộ sinh test case & synthetic typing
│   ├── CorpusExtractor/       # Trích xuất câu từ Wikipedia/văn bản
│   └── KeystrokeSynthesizer/  # Chuyển text thành chuỗi Telex/VNI
├── runners/                   # Bộ runner thực thi
│   ├── core-inmemory/         # Runner in-memory (F#, Rust, C, Go, C#)
│   ├── windows-sendinput/     # Runner bắn phím Windows
│   ├── macos-cgevent/         # Runner bắn phím macOS
│   └── linux-uinput/          # Runner bắn phím Linux
├── results/                   # Kết quả benchmark dạng JSON
│   ├── windows/
│   │   ├── bamboomintkey-v1.0.json
│   │   ├── unikey-4.3rc5.json
│   │   └── evkey-v5.0.json
│   ├── macos/
│   └── linux/
└── site/                      # Dashboard tĩnh hiển thị bảng xếp hạng
```

### 5.2. Nguyên Tắc Văn Hóa Cộng Đồng
- **Công bằng & Minh bạch:** Mọi cấu hình thử nghiệm đều phải được công khai (phiên bản, commit hash, hệ điều hành, cờ thiết lập: Bỏ dấu tự do ON/OFF, Kiểm tra chính tả ON/OFF).
- **Không xếp hạng một con số tổng duy nhất (No Single Rank):** Bảng xếp hạng luôn hiển thị đa chiều theo từng tiêu chí, tránh tạo sự đối đầu ác ý giữa các nhóm tác giả.
- **Đóng góp mở (Pull Request driven):** Bất kỳ tác giả bộ gõ nào cũng có thể đóng góp runner hoặc nộp kết quả benchmark cho bộ gõ của mình.

---

## 6. Lộ Trình Triển Khai (Roadmap)

| Bước | Mục Tiêu | Trạng Thái |
| :---: | :--- | :---: |
| **1** | Định dạng file test case và chuẩn bị ~500 case mẫu cho 4 nhóm cốt lõi | ✅ Hoàn thành (`passage_500.cases`) |
| **2** | Xây dựng Test Runner chạy trực tiếp với lõi F# BambooMintKey trên Windows | 🚀 Đang thực hiện |
| **3** | Xây dựng bộ sinh Keystroke Synthesizer từ văn bản tiếng Việt thực tế | ✅ Hoàn thành (`passage_500.cases` 583 từ) |
| **4** | Xây dựng Runner Tầng 2 (SendInput harness trên Windows) cho 3 app | ✅ Hoàn thành (`tier2_sendinput_runner.ps1`) |
| **5** | Đóng gói repo mã nguồn mở và công bố kết quả với cộng đồng | 🚀 Đang thực hiện |

---

## 7. Báo Cáo Thực Nghiệm Mới Nhất (Latest Benchmark Results)

- 📊 **Báo cáo phân tích chuyên sâu Tầng 2 (Notepad Integration):** [BENCHMARK_REPORT_TIER2_NOTEPAD.md](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/BENCHMARK_REPORT_TIER2_NOTEPAD.md)
- 📈 **Bảng điều khiển tương tác (Interactive Dashboard):** [dashboard.html](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/dashboard.html)
- 📁 **File dữ liệu tổng hợp (40 runs):** [benchmark_all.csv](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/benchmark_all.csv)
- 🔍 **Chi tiết 40 file log thực tế:** [docs/6.Benchmark/results/logs/v2/](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/logs/v2/)

