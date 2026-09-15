<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Tính Năng: Kiến Trúc Dữ Liệu Predictive Engine

**Mã tài liệu:** `006_01_Predictive_Engine_Data_Architecture`

**Tài liệu:** `docs/2.Design/Phase6/006_01_Predictive_Engine_Data_Architecture.md`

**Giai đoạn:** Phase 6 - (Draft) Predictive Engine & Data Storage

**Trạng thái:** 📝 Đang thiết kế (Draft)

**Chế độ triển khai:** Chỉ thiết kế kiến trúc, **chưa được phép viết code**.

**Liên quan:**
- `docs/2.Design/Phase6/006_00_MSIX_Store_Packaging.md`
- `src/BambooMintKey.Core` (F# engine)
- `src/BambooMintKey.NativeBridge` (TSF integration)

---

## 1. Tóm tắt vấn đề

BambooMintKey hiện tại là một bộ gõ Telex/VNI thuần tuý, không có tính năng **gợi ý từ (predictive text)**. Để nâng cao trải nghiệm gõ tiếng Việt, cần thiết kế một **Predictive Engine** nhẹ, nhanh, và dễ đóng gói.

Vấn đề lớn nhất của Predictive Engine là **quản lý dữ liệu**:
- Từ điển tiếng Việt lấy từ đâu?
- Lưu trữ như thế nào để khởi động nhanh?
- Dữ liệu cá nhân của người dùng học được lưu ở đâu?
- Có nên dùng SQLite hay không?

Giải pháp: **chia tách rành mạch** thành **Static Dictionary** (từ điển tĩnh đóng gói sẵn) và **User Data** (dữ liệu động học theo thời gian).

---

## 2. Mục tiêu kỹ thuật

1. **Gợi ý từ tiếng Việt chính xác** ngay sau khi cài đặt, không cần học từ đầu.
2. **Học thói quen người dùng** theo thời gian (từ viết tắt, tên riêng, thuật ngữ chuyên ngành).
3. **Khởi động tức thì** — dữ liệu từ điển phải load trong vài mili-giây.
4. **Không làm tăng độ trễ gõ phím** — lookup phải dưới 1ms mỗi phím.
5. **Zero-dependency hoặc ít dependency** — ưu tiên không kéo SQLite nếu không cần thiết.
6. **Tương thích MSIX** — dữ liệu cá nhân phải lưu ngoài package container.

---

## 3. Kiến trúc dữ liệu: Static + Dynamic

```text
┌────────────────────────────────────────────────────────┐
│                   Predictive Engine                    │
└──────────────┬──────────────────────────┬──────────────┘
               │                          │
               ▼                          ▼
    ┌──────────────────────┐   ┌──────────────────────┐
    │  Static Lexicon      │   │  User Dynamic Store  │
    │  (Read-only / RAM)   │   │  (Read-Write / Disk) │
    ├──────────────────────┤   ├──────────────────────┤
    │ - File binary nhúng  │   │ - Cụm từ cá nhân     │
    │ - Tần suất chuẩn     │   │ - Từ viết tắt tự tạo │
    │ - Tốc độ: ~0.01ms    │   │ - Tần suất học được  │
    └──────────────────────┘   └──────────────────────┘
```

### 3.1. Static Dictionary (Read-only)

Đóng gói sẵn trong DLL, load một lần khi khởi động.

| Loại dữ liệu | Nguồn | Mục đích | Kích thước ước tính |
|---|---|---|---|
| Syllable Lexicon | UniKey / OpenKey / Viện Ngôn ngữ học | Validate âm tiết tiếng Việt, chặn bỏ dấu sai | ~6.500 - 7.000 âm tiết |
| Word & N-gram Corpus | VnCoreNLP / pyVi / Wikipedia tiếng Việt | Gợi ý từ ghép và cụm từ thông dụng | ~40.000 - 75.000 từ |
| Frequency Table | Wikipedia corpus / VnCoreNLP | Xếp hạng gợi ý theo tần suất thực tế | Vài trăm KB |

### 3.2. User Dynamic Store (Read-Write)

Lưu dữ liệu cá nhân hóa trên disk, cập nhật định kỳ.

| Loại dữ liệu | Mô tả | Ví dụ |
|---|---|---|
| Personal phrases | Cụm từ người dùng hay gõ | "Nguyễn Văn A", "NTQ Solution" |
| Abbreviations | Từ viết tắt tự định nghĩa | "bmk" → "BambooMintKey" |
| Learned frequency | Tần suất xuất hiện của từ/cụm theo người dùng | "phải" được gõ nhiều hơn "phái" |
| Context bi-grams | Cặp từ hay đi kèm | "trưởng phòng" → "nhân sự" |

---

## 4. Nguồn dữ liệu từ điển tiếng Việt

### 4.1. Từ điển âm tiết (Syllable Lexicon)

**Nguồn gợi ý:**
- Bộ dữ liệu âm tiết tiếng Việt chuẩn từ các dự án mã nguồn mở: **UniKey**, **OpenKey**, **ibus-bamboo**.
- Danh sách âm tiết hợp lệ từ Viện Ngôn ngữ học (nếu có license phù hợp).

**Mục đích:**
- Kiểm tra xem một chuỗi phím Telex có tạo ra âm tiết tiếng Việt hợp lệ không.
- Ngăn chặn việc bỏ dấu sai vào từ tiếng Anh.

**Kích thước:**
- ~6.500 - 7.000 âm tiết hợp lệ (đã bao gồm đầy đủ biến thể dấu).

### 4.2. Từ điển từ ghép và cụm từ

**Nguồn gợi ý:**
- **VnCoreNLP** / **pyVi**: từ điển từ ghép tiếng Việt, ~40.000 - 75.000 từ vựng.
- **Wikipedia tiếng Việt corpus**: dùng để trích xuất tần suất xuất hiện thực tế của các cặp từ (bi-grams).
- **Bộ ngữ liệu VLSP** (nếu license cho phép).

**Mục đích:**
- Gợi ý từ tiếp theo dựa trên từ đã gõ.
- Xếp hạng gợi ý theo tần suất sử dụng thực tế trong tiếng Việt.

### 4.3. Quy trình đóng gói (Build Pipeline)

> **Nguyên tắc:** Không bao giờ để app client đọc file `.txt` hay `.csv` thô khi khởi động.

```text
Raw Text Data (.txt / .csv)
        │
        ▼
┌─────────────────────┐
│  F# Build Script    │
│  - Parse words      │
│  - Compute weights  │
│  - Build tries/     │
│    hash tables      │
└──────────┬──────────┘
           ▼
   lexicon.bin (1.5 - 2 MB)
           │
           ▼
   Embedded Resource in DLL
           │
           ▼
   Memory-mapped / Static array at runtime
```

**Công cụ đề xuất:**
- Script F# chạy trong quá trình build.
- Output: file binary `lexicon.bin` nhúng vào `BambooMintKey.dll` làm `EmbeddedResource`.
- Runtime: đọc vào `Span<byte>` hoặc `MemoryMappedFile`, parse thành immutable trie/hash map.

---
## 5. Lựa chọn lưu trữ dữ liệu cá nhân

### 5.1. Cách 1: File nhị phân tự serialize hoặc JSON/FlatBuffers (Khuyến nghị)

**Lý do khuyến nghị:**
- Thói quen cá nhân thường chỉ tích lũy **2.000 - 5.000 từ/cụm từ** riêng biệt.
- Cấu trúc dữ liệu phẳng `Map<string, int>` (cụm từ → tần suất) là đủ.
- Zero-dependency, khởi động tức thì.
- Dung lượng chỉ vài chục KB.

**Định dạng đề xuất:**
- JSON đơn giản cho dễ debug (giai đoạn dev).
- FlatBuffers hoặc MessagePack cho production (nhỏ + nhanh).
- Hoặc file binary tự serialize trong F#.

**Vị trí lưu:**
- Windows: `%LOCALAPPDATA%\BambooMintKey\userLexicon.bin`
- Linux: `~/.local/share/BambooMintKey/userLexicon.bin`
- MSIX: dùng `ApplicationData.LocalFolder` hoặc `%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\Roaming\BambooMintKey\`

**Cơ chế flush:**
- Tích lũy thay đổi trong memory.
- Ghi đè file định kỳ (ví dụ mỗi 5 phút) hoặc khi app tắt.

### 5.2. Cách 2: SQLite nhúng (`Microsoft.Data.Sqlite`)

**Chỉ dùng khi cần các tính năng nâng cao:**
- Đồng bộ đám mây nhiều thiết bị.
- Full-text search lịch sử gõ.
- Import/export hàng trăm nghìn dòng từ điển chuyên ngành từ Excel/CSV.

**Schema tối thiểu:**

```sql
CREATE TABLE UserVocabulary (
    Phrase TEXT PRIMARY KEY,
    Frequency INTEGER DEFAULT 1,
    LastUsedTimestamp INTEGER
);

CREATE TABLE UserAbbreviations (
    Shortcut TEXT PRIMARY KEY,
    Expansion TEXT NOT NULL,
    Frequency INTEGER DEFAULT 1
);

CREATE TABLE UserBigrams (
    Word1 TEXT NOT NULL,
    Word2 TEXT NOT NULL,
    Frequency INTEGER DEFAULT 1,
    PRIMARY KEY (Word1, Word2)
);
```

**Nhược điểm:**
- Tăng kích thước installer (~1-2 MB thêm).
- Thêm dependency `Microsoft.Data.Sqlite` và native `e_sqlite3.dll`.
- Cần xử lý concurrency và MSIX container path.

### 5.3. Quyết định tạm thời

| Thành phần | Công nghệ | Lý do |
|---|---|---|
| Static Lexicon | Embedded binary (`lexicon.bin`) | Nhanh, zero-dep, đóng gói sạch |
| User Vocabulary | JSON/FlatBuffers file | Đủ cho 90% use case, nhẹ |
| Advanced sync / enterprise | SQLite (sau này) | Optional, chỉ khi cần |

---

## 6. Tích hợp vào kiến trúc hiện tại

### 6.1. Vị trí trong codebase

```text
src/BambooMintKey.Core/
├── Engine/
│   ├── TelexEngine.fs          (hiện có)
│   ├── PredictiveEngine.fs     (mới)
│   └── LexiconLoader.fs        (mới)
└── Data/
    ├── lexicon.txt             (raw source, không ship)
    └── build-lexicon.fsx       (build script)

src/BambooMintKey.NativeBridge/
├── TSF/
│   └── KeyEventSinkImpl.cs     (gọi PredictiveEngine)
└── Resources/
    └── lexicon.bin             (embedded resource)
```

### 6.2. Luồng gọi khi gõ phím

```text
User nhấn phím
        │
        ▼
┌─────────────────────┐
│ KeyEventSinkImpl    │
│  - OnTestKeyDown    │
│  - OnKeyDown        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  BridgeStateManager │
│  - ProcessKey       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  PredictiveEngine   │
│  - Lookup candidates│
│  - Score by context │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  UI candidate window│
│  (hoặc inline TSF)  │
└─────────────────────┘
```

### 6.3. Giao diện F# đề xuất

```fsharp
type Candidate = {
    Text: string
    Score: float
    Source: Static | User | Abbreviation
}

type PredictiveEngine = {
    StaticLexicon: ImmutableLexicon
    UserStore: UserVocabularyStore
    Lookup: string -> string list -> Candidate list
    Learn: string -> unit
}
```

---

## 7. Vấn đề đặc biệt với MSIX

### 7.1. Static lexicon trong MSIX

- `lexicon.bin` nhúng trong DLL → không vấn đề, vẫn đọc qua EmbeddedResource.
- Hoặc đặt trong package folder, đọc qua `Package.Current.InstalledLocation`.

### 7.2. User data trong MSIX

- Không được ghi vào package install folder sau khi cài.
- Phải dùng:
  - `Windows.Storage.ApplicationData.Current.LocalFolder` (UWP API).
  - Hoặc `%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalCache\...`.
- Code hiện tại dùng `%APPDATA%\BambooMintKey\config.json` cần migration nếu chuyển sang Store.

### 7.3. SQLite trong MSIX

- Nếu dùng SQLite, database file phải đặt trong `LocalFolder`.
- `Microsoft.Data.Sqlite` hoạt động trong MSIX nhưng cần test kỹ path và native DLL.

---

## 8. Câu hỏi cần trả lời trước khi triển khai

1. Có muốn predictive engine chạy **inline** (thay thế từ đang gõ) hay **candidate window** (danh sách chọn)?
2. Có hỗ trợ **từ viết tắt (macro)** riêng hay gộp chung vào user vocabulary?
3. Có cần **đồng bộ cloud** nhiều thiết bị không?
4. Có giới hạn **privacy** — dữ liệu cá nhân có được gửi lên server không?
5. License của các bộ từ điển nguồn mở (VnCoreNLP, pyVi, OpenKey) có cho phép đóng gói trong sản phẩm thương mại không?

---

## 9. Kế hoạch hành động (Action Items)

| # | Công việc | Mô tả | Ưu tiên | Phụ thuộc |
|---|---|---|---|---|
| 9.1 | Chọn nguồn từ điển | Quyết định dùng UniKey/OpenKey/VnCoreNLP/Wikipedia | Cao | — |
| 9.2 | License review | Kiểm tra license các nguồn dữ liệu | Cao | 9.1 |
| 9.3 | Thiết kế format binary | Định nghĩa cấu trúc `lexicon.bin` | Cao | 9.1 |
| 9.4 | Viết build script | F# script chuyển raw text → binary | Cao | 9.3 |
| 9.5 | Tích hợp EmbeddedResource | Nhúng `lexicon.bin` vào `BambooMintKey.dll` | Cao | 9.4 |
| 9.6 | Viết loader | `LexiconLoader` đọc binary tại runtime | Cao | 9.5 |
| 9.7 | Thiết kế UserVocabularyStore | Map<string, int> + persistence | Trung bình | — |
| 9.8 | Quyết định SQLite hay không | Chỉ dùng nếu cần sync/cloud | Thấp | 9.7 |
| 9.9 | Viết PredictiveEngine API | `Lookup`, `Learn`, `GetCandidates` | Cao | 9.6, 9.7 |
| 9.10 | Tích hợp vào KeyEventSink | Gọi PredictiveEngine từ TSF | Cao | 9.9 |
| 9.11 | UI candidate window | Hiển thị danh sách gợi ý | Trung bình | 9.10 |
| 9.12 | Test MSIX path | Verify user data lưu đúng chỗ | Trung bình | 9.7 |

---

## 10. Tài liệu liên quan

- `docs/2.Design/Phase6/006_00_MSIX_Store_Packaging.md` — packaging và storage trong MSIX.
- `docs/2.Design/Phase5/005_05_DisplayAttributeProvider.md` — hiển thị composition.
- `docs/2.Design/Phase2/002_03_KeyEventSink_and_Core_Interop.md` — luồng xử lý phím.
- VnCoreNLP: https://github.com/vncorenlp/VnCoreNLP
- pyVi: https://github.com/duongtunganh/pyvi
- OpenKey: https://github.com/tuyenvm/OpenKey
- FlatBuffers: https://google.github.io/flatbuffers/
