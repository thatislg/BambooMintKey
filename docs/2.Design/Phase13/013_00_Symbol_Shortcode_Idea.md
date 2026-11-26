<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 013_00 — Ý Tưởng: Gõ Ký Hiệu & Icon Bằng Từ Khóa Tiếng Việt (`::mui → →`)

**Mã tài liệu:** `013_00_Symbol_Shortcode_Idea`

**Giai đoạn:** Phase 13 — Gõ ký hiệu, icon, emoji

**Thuộc module (dự kiến):** `BambooMintKey.Core` (engine + từ điển), các lớp UI/bridge theo từng nền tảng

**Trạng thái:** 💡 Bản nháp ý tưởng. Chưa có thiết kế chi tiết, chưa có code. Dùng để chốt phạm vi trước khi viết tài liệu thiết kế `013_01`.

---

## 1. Bối Cảnh Và Vấn Đề

Khi viết ghi chú, Markdown, chat hay tài liệu kỹ thuật, người dùng thường cần các ký hiệu như `→ ✓ ※ × ° ₫ 「」` và icon/emoji như `✅ ⚠️ 📌 🔥`.

Hiện nay họ phải:

- tìm trên mạng rồi copy-paste;
- mở bảng emoji của hệ điều hành (`Win + .` trên Windows), nhưng bảng này **không tìm được bằng tiếng Việt**, và ký hiệu thường (mũi tên, dấu nhân, độ C) khó tìm;
- hoặc nhớ mã Alt / mã Unicode.

Các bộ gõ tiếng Việt phổ biến (UniKey, EVKey, Windows Telex) có gõ tắt (macro) nhưng không có từ điển ký hiệu dựng sẵn, cũng không có bảng chọn.

## 2. Ý Tưởng Cốt Lõi

Về bản chất là **một bảng tra `từ khóa → danh sách ký tự` cộng với một chế độ chọn**.

Người dùng gõ chuỗi kích hoạt (mặc định `::`), tiếp theo là từ khóa không dấu. Bộ gõ hiện danh sách gợi ý, người dùng chọn rồi chốt.

```text
Gõ:      ::mui
Hiện:    1 →   2 ←   3 ↑   4 ↓   5 ⇒   6 ⇔   7 ↔
Bấm 1 / Space / Enter  →  thay "::mui" bằng "→"
Bấm Esc                →  giữ nguyên "::mui"
```

### 2.1. Bốn việc của phần lõi

| # | Việc | Mô tả |
|---|---|---|
| 1 | **Từ điển** | Từ khóa ứng với danh sách ký tự, có thứ tự ưu tiên. |
| 2 | **Nhận diện lệnh** | Thấy chuỗi kích hoạt `::` ở đầu từ → chuyển sang chế độ ký hiệu, **tạm ngừng áp Telex** cho phần từ khóa. |
| 3 | **Chọn** | `1–9` chọn trực tiếp; `↑ ↓` / `Tab` di chuyển; `Space` / `Enter` chốt mục đang chọn; `Esc` hủy. |
| 4 | **Chốt** | Thay toàn bộ đoạn `::từkhóa` trong composition bằng ký tự đã chọn. |

### 2.2. Tra không dấu ở cả hai phía

Chuẩn hóa từ khóa và đầu vào bằng cách bỏ dấu, đưa về chữ thường. Gõ `::mui`, `::mũi` hay `::MUI` đều ra cùng kết quả, nên trong chế độ lệnh không cần đặt dấu.

### 2.3. Tìm theo tiền tố

Gõ đến đâu lọc đến đó: `::m` → mọi từ khóa bắt đầu bằng `m` (mui, mat, mua...), `::mu` → thu hẹp dần. Khớp chính xác được xếp trước, sau đó đến khớp tiền tố.

## 3. Từ Điển

### 3.1. Định dạng đề xuất

JSON, cùng kiểu với `config.json` hiện có:

```json
{
  "mui":   ["→", "←", "↑", "↓", "⇒", "⇔", "↔"],
  "do":    ["°", "℃", "℉"],
  "nhan":  ["×", "·"],
  "chia":  ["÷"],
  "tick":  ["✓", "✔", "✗"],
  "tien":  ["₫", "¥", "$", "€"],
  "sao":   ["★", "☆", "※"],
  "ngoac": ["「」", "『』", "【】", "《》"]
}
```

### 3.2. Nhóm ký hiệu dựng sẵn

**Ký hiệu văn bản / kỹ thuật**

| Từ khóa | Ký tự | Ghi chú |
|---|---|---|
| `mui`, `ten` | → ← ↑ ↓ ⇒ ⇔ ↔ ↗ ↘ | mũi tên |
| `tick`, `dung`, `sai` | ✓ ✔ ✗ ✘ | đánh dấu |
| `nhan`, `chia`, `cong`, `tru` | × ÷ ± · | toán |
| `bang`, `xapxi`, `khac` | ≈ ≠ ≤ ≥ ≡ | so sánh |
| `vocuc`, `can`, `tong`, `pi` | ∞ √ ∑ π | toán nâng cao |
| `do` | ° ℃ ℉ | nhiệt độ, góc |
| `tien` | ₫ ¥ $ € £ | tiền tệ |
| `sao`, `chu` | ★ ☆ ※ | đánh dấu ghi chú |
| `cham`, `bullet` | • ◦ ‣ ・ | gạch đầu dòng |
| `gach` | — – … | gạch dài, gạch ngắn, ba chấm |
| `ngoac` | 「」 『』 【】 《》 “” ‘’ | ngoặc (chốt cả cặp, con trỏ đặt giữa nếu nền tảng cho phép) |
| `ban quyen`, `c`, `r`, `tm` | © ® ™ | pháp lý |
| `phan` | ½ ¼ ¾ ‰ | phân số, phần nghìn |

**Icon / emoji**

| Từ khóa | Icon | Dùng khi |
|---|---|---|
| `ok`, `xong` | ✅ ☑️ | checklist, trạng thái xong |
| `loi`, `khong` | ❌ ⛔ | lỗi, không được |
| `canh bao`, `chu y` | ⚠️ ❗ ❓ | cảnh báo |
| `ghim`, `note` | 📌 📍 📝 | ghi chú |
| `lua`, `hot` | 🔥 | nổi bật |
| `y tuong`, `den` | 💡 | ý tưởng |
| `lich`, `gio` | 📅 ⏰ ⏳ | thời gian |
| `file`, `thu muc` | 📄 📁 📂 | tài liệu |
| `bug`, `sua` | 🐛 🔧 🛠️ | kỹ thuật |
| `tim`, `thich` | ❤️ 👍 👎 | phản hồi |
| `cuoi`, `buon` | 😄 😂 😢 | cảm xúc |
| `co vn`, `vietnam` | 🇻🇳 | cờ |

> Từ khóa có khoảng trắng (`canh bao`) được gõ liền khi tra (`::canhbao`). Lúc nạp từ điển, bỏ khoảng trắng của từ khóa.

### 3.3. Nguồn dữ liệu nền: Unicode CLDR

Không cần tự nghĩ toàn bộ từ khóa. **Unicode CLDR** có bộ chú thích tiếng Việt (`annotations/vi.xml`, `annotationsDerived/vi.xml`) cho emoji và nhiều ký hiệu, ví dụ `→` đã có sẵn các từ khóa kiểu "mũi tên", "phải".

Kế hoạch:

1. Viết script trong `scripts/tools/` chuyển CLDR `vi` sang JSON từ điển (bỏ dấu, chuẩn hóa).
2. Trộn với bộ **từ khóa tự biên soạn** (bảng ở mục 3.2) và ưu tiên bộ này, vì từ khóa của CLDR dài và mang tính mô tả hơn là từ khóa để gõ nhanh.
3. Ghi nguồn và giấy phép vào `THIRD-PARTY-NOTICES.md`. CLDR dùng giấy phép Unicode (Unicode License v3), khá thoáng, nhưng **cần kiểm tra lại điều khoản trước khi đóng gói**.

### 3.4. Ba tầng từ điển

| Tầng | Vị trí | Ai dùng được | Giai đoạn |
|---|---|---|---|
| Mặc định | `dicts/symbols-vi.json` (đi kèm bản phát hành) | Mọi người dùng BambooMintKey, có khi cập nhật bản mới | **Bước 1** |
| Người dùng | Cạnh `config.json` (`%APPDATA%` / `~/.config/bamboomintkey/`) | Chỉ máy đó; chia sẻ qua xuất/nhập file | Để sau |
| Gần đây | Lưu cục bộ | Chỉ máy đó | Để sau |

> **Quyết định (2026-11-26):** Bước đầu **chỉ dùng từ điển mặc định**. Từ khóa/icon tác giả muốn bổ sung sẽ được thêm thẳng vào `dicts/symbols-vi.json`, để mọi người dùng cùng nhận được khi cập nhật. Từ điển người dùng, xuất/nhập gói ký hiệu và danh sách "gần đây" để sau.

### 3.5. Phạm vi của "icon"

- Bộ gõ chỉ chèn **ký tự Unicode**. Người nhận văn bản không cần cài BambooMintKey vẫn thấy icon.
- Không chèn được ảnh tự vẽ. Ký tự vùng Private Use chỉ hiện đúng trên máy có font riêng, máy khác sẽ thấy `□`, nên không đưa vào từ điển.
- Hình dạng emoji phụ thuộc font của hệ điều hành người xem. Ưu tiên emoji đã phổ biến lâu để tránh hiện ô vuông trên máy cũ.

## 4. Quy Tắc Kích Hoạt

### 4.1. Chọn `::` thay vì `:`

**Quyết định:** chuỗi kích hoạt mặc định là `::` (hai dấu hai chấm liền nhau).

Lý do không dùng `:` đơn:

- Trong văn bản tiếng Việt, `:` xuất hiện thường xuyên (`Lưu ý:`, `Thời gian: 10:30`, `Tỉ lệ 1:2`...). Dù có quy tắc "đầu từ", vẫn dễ bị bộ gõ **"liếm"** nhầm vào chữ phía sau.
- Discord, Slack, GitHub dùng `:smile:` làm shortcode emoji; dùng `:` đơn sẽ tranh phím với các app này.

`::` gần như không xuất hiện trong văn bản tiếng Việt thông thường, nên rủi ro nhầm thấp hơn nhiều và **không cần danh sách loại trừ app cho Discord/Slack**.

### 4.2. Điều kiện kích hoạt

Kích hoạt khi **cả ba** điều kiện đúng:

1. `::` đứng **đầu từ**: đầu dòng, hoặc ngay sau khoảng trắng / dấu câu mở (`(`, `[`, `"`...).
2. Hai dấu `:` được gõ **liền nhau** (phím thứ hai đến ngay sau phím thứ nhất, không có phím di chuyển con trỏ ở giữa).
3. Ký tự gõ tiếp theo là **chữ cái**.

| Gõ | Kết quả | Lý do |
|---|---|---|
| `::mui` | kích hoạt | đầu từ, sau đó là chữ cái |
| `Lưu ý: abc` | bình thường | chỉ có một `:` |
| `10:30`, `http://` | bình thường | chỉ có một `:` liên tiếp |
| `std::vector`, `Foo::Bar` (C++, Rust, C#) | bình thường | `::` không ở đầu từ |
| `x :: xs` (F#, Haskell) | bình thường | sau `::` là khoảng trắng |
| `::1` (IPv6) | bình thường | sau `::` là chữ số |
| `::before`, `::selection` (CSS ở đầu dòng) | **kích hoạt nhầm** | xem 4.3 |

### 4.3. Thoát an toàn khi kích hoạt nhầm

- Từ khóa **không khớp** gì (như `::before`, `::selection`) thì không hiện danh sách; gõ `Space`, `{`, `(`... sẽ chốt nguyên văn chữ đã gõ, không mất ký tự nào.
- Có khớp nhưng không muốn dùng: `Esc` trả lại nguyên văn `::từkhóa`.
- `Backspace` sau khi vừa chốt: hoàn tác về `::từkhóa` (giống cơ chế undo của Telex).

### 4.4. Ghi chú cài đặt engine

Khi gặp `:` ở đầu từ, engine **giữ `:` trong composition** (trạng thái chờ) thay vì chốt ngay:

- phím tiếp theo là `:` → chuyển sang chế độ ký hiệu;
- phím tiếp theo là gì khác → chốt `:` rồi xử lý phím đó như bình thường.

Nhờ vậy không cần đọc lại văn bản xung quanh con trỏ (việc này không ổn định trên mọi app). Đổi lại, dấu `:` đầu từ sẽ có gạch chân composition trong tích tắc. Cần thử xem điều này có gây khó chịu hoặc lỗi ở app nào không.

### 4.5. Tùy chọn khác

- Cho phép **đổi chuỗi kích hoạt** trong Settings (`::`, `;;`, `\`, `//`), phòng khi người dùng hay viết code CSS/C++.
- Một **phím tắt** mở thẳng chế độ ký hiệu (ví dụ `Ctrl + .`), dành cho người không muốn dùng chuỗi kích hoạt.
- Vẫn giữ **tắt theo từng app** (`ExcludedProcesses`), mặc định rỗng.

## 5. Giao Diện Chọn

### 5.1. MVP không cần cửa sổ: xoay vòng inline

Để làm được sớm mà chưa cần popup:

- Gõ `::mui` → composition hiện ngay ứng viên đầu tiên `→` (có gạch chân như composition thường).
- `Tab` → chuyển sang `←`, `↑`...
- `Space` / `Enter` chốt, `Esc` trả lại `::mui`.

Cách này chỉ dùng composition sẵn có của TSF / Fcitx5 / IMK, không cần thêm UI.

### 5.2. Bản đầy đủ: cửa sổ ứng viên

| Nền tảng | Cách làm | Độ khó |
|---|---|---|
| Linux (Fcitx5) | Dùng candidate list có sẵn của Fcitx5 (`CommonCandidateList`) | Thấp |
| macOS (IMK) | `IMKCandidates` | Trung bình |
| Windows (TSF) | `ITfCandidateListUIElement` (để app tự vẽ, hỗ trợ ứng dụng full-screen) hoặc cửa sổ popup riêng đặt theo vị trí con trỏ từ `ITfContextView::GetTextExt` | Cao |

Windows là phần khó nhất. Nên làm Fcitx5 trước để kiểm chứng engine, sau đó mới làm popup TSF.

Bố cục đề xuất (ngang, giống IME tiếng Nhật):

```text
 ::mui
┌──────────────────────────────────────────┐
│ 1 →  2 ←  3 ↑  4 ↓  5 ⇒  6 ⇔  7 ↔    ‹ › │
└──────────────────────────────────────────┘
```

## 6. Tích Hợp Vào Kiến Trúc Hiện Tại

```text
BambooMintKey.Core (F#)
├── Domain/SymbolTypes.fs        // SymbolEntry, SymbolQuery, SymbolState
├── Dictionary/SymbolDictionary.fs  // nạp JSON, chuẩn hóa không dấu, tra tiền tố
└── Engine/SymbolEngine.fs       // máy trạng thái: Idle → Collecting → Selecting → Committed/Cancelled
          ▲
          │  TelexEngine chuyển quyền xử lý phím sang SymbolEngine khi gặp chuỗi kích hoạt `::` hợp lệ
          │
NativeBridge (TSF) / Core.Native (C-ABI) / IMK
└── chỉ thêm API: lấy danh sách ứng viên + chỉ số đang chọn, để UI từng nền tảng hiển thị
```

Nguyên tắc:

- **Toàn bộ logic trong F# Core**, dùng chung 100% cho Windows / Linux / macOS như engine Telex.
- Engine thuần (pure), dễ viết unit test: đầu vào là chuỗi phím, đầu ra là composition + danh sách ứng viên + lệnh chốt.
- Lớp nền tảng chỉ hiển thị và chuyển phím.

Cấu hình thêm vào `config.json` (dự kiến):

```json
{
  "Symbol": {
    "Enabled": true,
    "Trigger": "::",
    "ExcludedProcesses": []
  }
}
```

Settings GUI: thêm tab **"Ký hiệu"** gồm bật/tắt, chọn chuỗi kích hoạt, ô gõ thử, (nút mở file từ điển người dùng: để sau).

## 7. Điểm Khác Biệt So Với Bộ Gõ Khác

- Tìm ký hiệu và icon **bằng tiếng Việt không dấu**, điều mà bảng emoji của hệ điều hành chưa làm được.
- Từ điển dựng sẵn, không phải tự khai báo macro từ đầu.
- Dùng chung một cách gõ trên Windows, Linux (kể cả Steam Deck) và macOS.
- Hợp với dân viết Markdown / ghi chú / tài liệu kỹ thuật, nhóm người dùng mà BambooMintKey đang nhắm tới.

## 8. Rủi Ro Và Câu Hỏi Mở

| # | Câu hỏi | Ghi chú |
|---|---|---|
| Q1 | ~~Ký tự kích hoạt mặc định~~ | ✅ Đã chốt `::` (mục 4.1). Còn lại: thử thực tế xem gạch chân chờ của `:` đầu từ (4.4) có gây phiền không. |
| Q2 | Emoji nhiều code point (cờ, ZWJ, `⚠️` có VS16) chốt có ổn định trên mọi app không? | Thử trên Notepad, Word, trình duyệt, terminal. |
| Q3 | Popup TSF có hoạt động trong app full-screen, UWP, ô mật khẩu không? | Ô mật khẩu nên tắt hẳn chế độ ký hiệu. |
| Q4 | Ảnh hưởng tới benchmark tốc độ gõ (`6.Benchmark`)? | Chỉ kiểm tra thêm một điều kiện khi gặp phím `:`, chi phí gần như bằng 0; cần đo lại. |
| Q5 | Kích thước từ điển CLDR đầy đủ có làm nặng DLL NativeAOT không? | Có thể nạp lười (lazy) từ file thay vì nhúng. |
| Q6 | Có cho chốt cả cặp ngoặc rồi đặt con trỏ vào giữa không? | Phụ thuộc khả năng di chuyển con trỏ của từng nền tảng. |
| Q7 | Gợi ý theo ngữ cảnh (gõ `->` tự đổi thành `→`) có nên làm không? | Có thể làm sau, dưới dạng tùy chọn "tự thay thế". |

## 9. Lộ Trình Đề Xuất

| Bước | Nội dung | Kết quả |
|---|---|---|
| 1 | `SymbolDictionary` (chỉ nạp từ điển mặc định) + `SymbolEngine` trong Core, unit test | Engine chạy được trong test / DevHarness |
| 2 | MVP xoay vòng inline (mục 5.1) trên Windows và Fcitx5 | Dùng được hằng ngày, chưa có popup |
| 3 | Candidate list trên Fcitx5, sau đó là popup TSF | Trải nghiệm đầy đủ |
| 4 | Nhập CLDR `vi` vào từ điển mặc định | Phủ nhiều ký hiệu/emoji hơn |
| 5 | Từ điển người dùng, xuất/nhập gói ký hiệu, danh sách "gần đây", tab "Ký hiệu" trong Settings, loại trừ theo app | Cá nhân hóa |
