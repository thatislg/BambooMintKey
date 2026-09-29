<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 012: Tùy chọn xử lý từ vựng (tiếng Anh/từ điển) khó phân biệt & tính năng gõ lặp dấu khôi phục từ bị ngược

**Mã tài liệu:** `012_SmartGrammarOptions_Duplicate_And_RepeatKeyUndoReversed`

**Trạng thái:** 🟢 Đã xử lý (1.1/1.2 loại bỏ `AutoRestoreEnglishWords`; 1.3 sửa chiều hoàn tác + ưu tiên hoàn dấu)

**Mức độ nghiêm trọng:** Trung bình (gây nhầm lẫn giao diện + một tính năng cốt lõi hoạt động sai chiều)

**Ngày ghi nhận:** 29/09/2026

---

## 0. Quyết định xử lý (đã chốt)

> **`AutoRestoreEnglishWords` ("Tự động khôi phục từ tiếng Anh") sẽ bị LOẠI BỎ.**
>
> **Lý do:** Đây là giải pháp tạm thời trước khi có từ điển tiếng Anh — chỉ hardcode vài từ thông dụng + luật đuôi. Nay đã có từ điển `english-20k.dict`, cơ chế này chồng chéo lên chính `EnableEnglishBacktracking` (từ điển) và gây nhầm lẫn ở Taskbar/UI. Danh sách hardcode (`CommonEnglishWords`) đã nằm sẵn trong nhánh `isKnownEnglishWord` của từ điển nên không mất phạm vi từ vựng.
>
> **Hướng xử lý:** xóa `AutoRestoreEnglishWords` khỏi engine, cấu hình, shared memory, UI (Windows + Linux), Taskbar và test; giữ `EnableEnglishBacktracking` làm nút tiếng Anh duy nhất (đổi nhãn "Tự động nhận diện từ tiếng Anh (20.000 từ)"); ẩn `EnableVietnameseDictionary` khỏi UI vì nó chỉ là điều kiện phụ.

---

## 1. Mô tả hiện tượng

Trong quá trình nghiệm thu nhóm tùy chọn "Tính năng thông minh" ở cả **Taskbar/Language Bar** và **Bảng điều khiển (UI Settings)**, ghi nhận 3 vấn đề liên quan đến ba tùy chọn xử lý từ vựng (tiếng Anh & từ điển) và tính năng gõ lặp dấu:

### 1.1. Ba tùy chọn xử lý từ vựng trông như bị nhân đôi (double) ở Taskbar và UI Setting

Ở cả Taskbar menu lẫn Bảng điều khiển đều xuất hiện **ba** mục liên quan đến xử lý từ vựng: hai mục tiếng Anh gần nghĩa, cộng một mục "từ điển" thực chất là tiền đề của mục hoàn tác tiếng Anh:

| Tên hiển thị (Taskbar) | Tên hiển thị (UI) | Khóa cấu hình |
|---|---|---|
| "Tự động khôi phục từ tiếng Anh" | "Tự động khôi phục từ tiếng Anh khi gõ sai ngữ pháp (start, word, ...)" | `AutoRestoreEnglishWords` |
| "Thẩm định qua từ điển" | "Thẩm định âm tiết qua từ điển (sửa lỗi đặt dấu)" | `EnableVietnameseDictionary` |
| "Tự động hoàn tác tiếng Anh" | "Tự động hoàn tác từ tiếng Anh (post, test, ...)" | `EnableEnglishBacktracking` |

Ba mục này được liệt kê ở:

- **Taskbar / Language Bar:** `src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs`
  - `ToggleAutoRestoreEnglish` (Base + 20) → "Tự động khôi phục từ tiếng Anh"
  - `ToggleEnableDictionary` (Base + 24) → "Thẩm định qua từ điển"
  - `ToggleEnableBacktracking` (Base + 25) → "Tự động hoàn tác tiếng Anh"
- **UI Settings:** `src/BambooMintKey.UI/MainWindow.axaml`
  - `ChkAutoRestore` → "Tự động khôi phục từ tiếng Anh khi gõ sai ngữ pháp (start, word, ...)"
  - `ChkEnableDictionary` → "Thẩm định âm tiết qua từ điển (sửa lỗi đặt dấu)"
  - `ChkEnableBacktracking` → "Tự động hoàn tác từ tiếng Anh (post, test, ...)"

Điểm gây nhầm lẫn: **"Thẩm định qua từ điển" (`EnableVietnameseDictionary`) thực chất là tiền đề bắt buộc của "Tự động hoàn tác tiếng Anh" (`EnableEnglishBacktracking`)** (xem mục 2.1). Ba tùy chọn liên đới nhau nhưng tên gọi lại rải rác ở "khôi phục", "từ điển", "hoàn tác" khiến người dùng khó hình dung mối quan hệ, cảm giác bị nhân đôi/chồng chéo.

### 1.2. Chưa rõ ba chức năng này có thực sự hoạt động hay không

Hiện chưa có bằng chứng kiểm chứng rõ ràng rằng ba cờ trên đang có tác động khác nhau (hay chồng lấn nhau) trên kết quả gõ, đặc biệt là quan hệ phụ thuộc giữa `EnableVietnameseDictionary` và `EnableEnglishBacktracking`. Cần làm rõ phạm vi tác dụng của từng cờ trước khi quyết định gộp hay giữ riêng.

### 1.3. Tính năng "gõ lặp dấu để khôi phục từ" đang bị ngược

Khi gõ lặp phím dấu để hoàn tác, kết quả trả về **không rút gọn** như kỳ vọng mà giữ nguyên cả 2 ký tự:

| STT | Chuỗi phím đã gõ | Kết quả mong đợi | Kết quả thực tế | Chú thích |
|-----|------------------|------------------|-----------------|-----------|
| 1 | `goxx` | `gox` | `goxx` | Lặp `x` (dấu ngã) phải rút về 1 chữ `x` |
| 2 | `horr` | `hor` | `horr` | Lặp `r` (dấu hỏi) phải rút về 1 chữ `r` |

Quy tắc mong đợi (theo `AllowRepeatKeyUndo`): gõ `xx` → dấu ngã xuất hiện rồi bị hủy, chỉ còn lại 1 chữ `x` thô (`ss -> s`, `aa -> a`, `rr -> r`). Thực tế lại trả về cả 2 ký tự, tức chiều hoàn tác đang **ngược** so với thiết kế.

---

## 2. Phân tích nguyên nhân (Root Cause Analysis)

### 2.1. Ba tùy chọn xử lý từ vựng (vấn đề 1.1 & 1.2)

Ba cờ là **ba thực thể cấu hình độc lập** trong `src/BambooMintKey.Core/Domain/EngineConfig.fs`:

```fsharp
AutoRestoreEnglishWords: bool      // Fallback tiếng Anh khi từ sai cấu trúc âm tiết tiếng Việt
EnableVietnameseDictionary: bool   // Thẩm định âm tiết tiếng Việt qua từ điển
EnableEnglishBacktracking: bool    // Hoàn tác từ tiếng Anh qua từ điển
```

Cả ba được đồng bộ đầy đủ qua shared memory (`SharedMemoryManager`) và hiển thị song song ở Taskbar lẫn UI. Vì vậy "double" không phải lỗi nhân bản dữ liệu mà là **ba tính năng liên đới được đặt tên khó phân biệt**.

> **Phát hiện từ mã nguồn (đã kiểm chứng):** cả ba cờ đều **được lõi engine tiêu thụ thực sự**, nhưng bằng ba cơ chế khác nhau, trong đó hai cơ chế có **quan hệ phụ thuộc**:
>
> - **`AutoRestoreEnglishWords`** (English Protection — heuristic):
>   - Trong `handleCharInput` (TelexEngine.fs): nếu `EnglishProtection.isLikelyEnglishWord newRaw` → lập tức trả chuỗi thô (`cleanEnglishWordText`).
>   - Trong `processKey` khi chốt từ: nếu `isLikelyEnglishWord state.RawKeys` → khôi phục chuỗi thô khi commit.
>   - Dùng bộ nhận diện "có khả năng là từ tiếng Anh" (heuristic, không qua từ điển).
>
> - **`EnableEnglishBacktracking`** (English Backtracking — dictionary-based), gói trong `shouldBacktrackEnglish`, **phụ thuộc bắt buộc vào `EnableVietnameseDictionary`**:
>   ```fsharp
>   viText.Length >= 3 &&
>   config.EnableVietnameseDictionary && config.EnableEnglishBacktracking &&
>   not (IsValidVietnameseSyllable viText) &&
>   EnglishProtection.isKnownEnglishWord rawString
>   ```
>   Chỉ hoàn tác khi: âm tiết đã đủ dài, **bật cả** `EnableVietnameseDictionary`, âm tiết KHÔNG hợp lệ tiếng Việt, và chuỗi thô là **từ tiếng Anh trong từ điển** (`isKnownEnglishWord`).
>
> - **`EnableVietnameseDictionary`** (Thẩm định âm tiết qua từ điển): ngoài việc tự sửa lỗi đặt dấu tiếng Việt, cờ này **còn là công tắc bật/tắt chung cho nhánh hoàn tác tiếng Anh** ở trên. Tức tắt "Thẩm định qua từ điển" sẽ đồng thời vô hiệu hóa "Tự động hoàn tác tiếng Anh".

Như vậy ba cơ chế bổ trợ nhau nhưng có quan hệ chồng chéo phức tạp: một heuristic độc lập (`AutoRestoreEnglishWords`) + một cặp "từ điển → hoàn tác" (`EnableVietnameseDictionary` → `EnableEnglishBacktracking`). Nhãn hiển thị "khôi phục" / "từ điển" / "hoàn tác" rải rác khiến người dùng khó nhận ra quan hệ phụ thuộc. Cần quyết định: gộp thành một tùy chọn, hay giữ riêng với tên & mô tả rõ ràng hơn (đặc biệt ghi chú rõ "hoàn tác tiếng Anh" phụ thuộc "thẩm định từ điển").

### 2.2. Repeat-key undo bị ngược (vấn đề 1.3)

Logic hoàn tác nằm trong `src/BambooMintKey.Core/Engine/TelexEngine.fs` (`handleCharInput`) và `src/BambooMintKey.Core/Engine/FreeTonePlacement.fs`:

- `isUndoTone`: lặp phím dấu thanh (`s f r x j`) khi âm tiết đã có cùng tone.
- `isUndoModifier`: lặp phím modifier (`a w e o d`).
- `HasRepeatedToneUndo` trong `extractTokens`: phát hiện 2 phím dấu liên tiếp để kích hoạt undo khi bật `AllowFreeTonePlacement`.

**Nguyên nhân gốc (đã xác nhận):** ở nhánh `isUndoTone`/`isUndoModifier`, kết quả trả về luôn dùng `newRaw` (chuỗi thô ĐÃ gồm cả phím lặp) thay vì `state.RawKeys` (chuỗi thô TRƯỚC phím lặp). Vì vậy khi lặp phím dấu liền kề, chuỗi giữ nguyên cả 2 ký tự (`goxx -> goxx`, `horr -> horr`) thay vì rút về 1 (`gox`, `hor`).

---

## 2.3. Xung đột giữa Bảo vệ từ tiếng Anh và Repeat-key undo

**Có xung đột thực sự**, nhưng chỉ xảy ra với một tập con hẹp: từ tiếng Anh **chứa ký tự lặp liền kề là phím dấu/modifier** (vd `mass`, `error`, `class`, `password`, `tests`, `add`).

- **Bảo vệ từ tiếng Anh** (từ điển `isKnownEnglishWord`) muốn giữ nguyên các từ đó (`mass -> mass`).
- **Repeat-key undo** muốn rút gọn ký tự lặp liền kề (`mass -> mas`).

**Quyết định đã chốt:** ưu tiên **hoàn dấu (repeat-key undo) trước**. Ký tự lặp liền kề luôn được rút gọn, kể cả khi chuỗi là từ tiếng Anh. Hệ quả chấp nhận được: muốn gõ từ tiếng Anh `mass` thì phải gõ `masss`.

| Gõ | Trước (từ Anh thắng) | Sau (hoàn dấu thắng) |
|---|---|---|
| `goxx` | `goxx` | `gox` |
| `horr` | `horr` | `hor` |
| `mass` | `mass` | `mas` |
| `error` | `error` | `eror` |
| `password` | `password` | `pasword` |
| `class` | `class` | `class` (không lặp liền kề phím dấu) |

---

## 3. Thông tin kỹ thuật liên quan

| Thành phần | File |
| :--- | :--- |
| Định nghĩa cấu hình | `src/BambooMintKey.Core/Domain/EngineConfig.fs` |
| Logic gõ & undo | `src/BambooMintKey.Core/Engine/TelexEngine.fs` |
| Free-tone & repeat undo | `src/BambooMintKey.Core/Engine/FreeTonePlacement.fs` |
| Menu Taskbar | `src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs` |
| Định nghĩa lệnh menu | `src/BambooMintKey.NativeBridge/TSF/MenuCommands.cs` |
| Shared memory config | `src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs` |
| UI Settings | `src/BambooMintKey.UI/MainWindow.axaml` (`.axaml.fs`) |

Danh sách đầy đủ tùy chọn "Tính năng thông minh" hiện tại (để đối chiếu khi quyết định gộp):

| # | Tên hiển thị | Khóa cấu hình |
|---|---|---|
| 1 | Tự động khôi phục từ tiếng Anh | `AutoRestoreEnglishWords` |
| 2 | Gõ lặp dấu để khôi phục (ss -> s) | `AllowRepeatKeyUndo` |
| 3 | Phím 'w' đầu từ thành 'ư' | `AllowLeadingWAsU` |
| 4 | Cho phép bỏ dấu tự do | `AllowFreeTonePlacement` |
| 5 | Thẩm định qua từ điển | `EnableVietnameseDictionary` |
| 6 | Tự động hoàn tác tiếng Anh | `EnableEnglishBacktracking` |

> **Nhóm gây nhầm lẫn là #1, #5, #6** — ba tùy chọn xử lý từ vựng liên đới nhau (xem mục 2.1).

---

## 4. Câu hỏi cần trả lời

1. Ba cờ `AutoRestoreEnglishWords`, `EnableVietnameseDictionary`, `EnableEnglishBacktracking` đã rõ về mặt kỹ thuật (một heuristic độc lập + một cặp "từ điển → hoàn tác") — còn cần quyết định **giao diện**: gộp thành một/nhóm tùy chọn, hay giữ riêng và đổi tên kèm ghi chú quan hệ phụ thuộc cho dễ phân biệt?
2. Với `goxx`/`horr`, nhánh undo nào đang được đi (hay bị bỏ qua)? Cần log `handleCharInput` để xác nhận.
3. Kết quả đúng mong muốn của `AllowRepeatKeyUndo` là rút về 1 ký tự thô (như `ss -> s`) hay chỉ đơn thuần hủy dấu giữ nguyên phím?

---

## 5. Action Items

- [x] Ghi nhận quyết định: loại bỏ `AutoRestoreEnglishWords`, giữ `EnableEnglishBacktracking` làm nút tiếng Anh duy nhất.
- [x] Loại bỏ `AutoRestoreEnglishWords` khỏi engine (`TelexEngine.fs`), config (`EngineConfig.fs`), shared memory, UI (Windows + Linux), Taskbar, và test.
- [x] Ẩn `EnableVietnameseDictionary` khỏi UI (chỉ là điều kiện phụ của hoàn tác tiếng Anh): đã xóa khỏi Taskbar (`LangBarItemButton.cs`, `MenuCommands.cs`), Settings UI (Windows + Linux `MainWindow.axaml`/`.axaml.fs`) và config Fcitx5 (`engine.h` `FCITX_CONFIGURATION`). Giữ cờ nội bộ (mặc định `true`) và khóa cấu hình trong `SharedConfig.fs`/shared memory để tương thích ngược; Fcitx5 gọi `bmk_set_options` với `enableVietnameseDictionary=true` cứng (giữ nguyên signature ABI).
- [x] Đổi nhãn `EnableEnglishBacktracking` → "Tự động nhận diện từ tiếng Anh (20.000 từ)", đồng bộ Taskbar (`LangBarItemButton.cs`), Settings UI (Windows + Linux) và Fcitx5 (`engine.h`).
- [x] Thu thập log `BambooMintKey_Runtime.log` khi gõ `goxx`, `horr` để xác định nhánh undo (vấn đề 1.3 độc lập).
- [x] Viết unit test cho repeat-key undo (`goxx -> gox`, `horr -> hor`) trong `BambooMintKey.Core.Tests`.
- [x] Sửa chiều hoàn tác: nhánh `isUndoTone`/`isUndoModifier` trả về `state.RawKeys` (trước phím lặp) thay vì `newRaw`, ưu tiên hoàn dấu trước (bỏ điều kiện `not isKnownEnglish`).
- [x] Cập nhật test tiếng Anh chứa ký tự lặp liền kề phím dấu để khóa hành vi mới: `error -> eror`, `password -> pasword`, `mass -> mas`.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **Chuỗi phím đã gõ** | Tái hiện chính xác lỗi repeat-key undo. |
| **Log runtime** | Xác định nhánh undo nào đang chạy. |
| **Tham chiếu lõi F#** | Xác nhận `EnableEnglishBacktracking` có thực sự được tiêu thụ. |
| **Phiên bản build** | Đối chiếu lỗi đã được sửa ở bản mới chưa. |

### Ghi chú kết quả triển khai (loại bỏ `AutoRestoreEnglishWords`)

Sau khi loại bỏ heuristic `isLikelyEnglishWord` (luật đuôi `-re`/cụm phụ âm cuối/đuôi số nhiều) và `cleanEnglishWordText` (xử lý lặp `rr`), nhận thấy **khác biệt hành vi giữa hai cơ chế**:

- `AutoRestoreEnglishWords` là **proactive**: ép về tiếng Anh ngay cả khi kết quả là âm tiết tiếng Việt hợp lệ.
- `EnableEnglishBacktracking` (từ điển) là **reactive**: chỉ hoàn tác khi kết quả **KHÔNG** hợp lệ tiếng Việt.

Hệ quả cụ thể (đã cập nhật test để khóa hành vi mới):

| Gõ | Trước (AutoRestore) | Sau (chỉ từ điển) |
|---|---|---|
| `post` | `post` | `pót` (hợp lệ Việt → giữ Việt) |
| `turn` | `turn` | `tủn` |
| `last` | `last` | `lát` |
| `test` | `test` | `tét` |
| `morre` | `more` | `morre` (không còn xử lý lặp `rr`) |
| `corre` | `core` | `corre` |

Các từ tiếng Anh có kết quả tiếng Việt **không** hợp lệ (như `core`, `start`, `word`, `files`, `form`…) vẫn hoàn tác đúng qua từ điển.
