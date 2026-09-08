<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế: Xử Lý Từ Tiếng Anh Trong Chế Độ Telex (Core/Corre)

**Mã tài liệu:** `005_02_English_Word_Telex_Protection`

**Giai đoạn:** Phase 5 - Cải Tiến Trải Nghiệm Gõ

**Trạng thái:** 📝 Đang thiết kế (Design)

**Chế độ triển khai:** Chỉ thiết kế và phân tích, **chưa được phép viết code sản phẩm**.

---

## 1. Tóm tắt vấn đề

### 1.1. Lỗi 4: Gõ `Core` trong chế độ Telex

| Trường | Giá trị |
|--------|---------|
| **Chuỗi phím đã gõ** | `Core` + space (nhưng phải gõ `Corre` + xoá `r`) |
| **Kết quả mong đợi** | `Core` |
| **Kết quả thực tế** | `Corre` (nếu gõ `Core`) hoặc `Core` (nếu gõ `Corre` rồi xoá `r`) |
| **Ứng dụng** | Hầu hết các ứng dụng, cả 2 kiểu gõ mới và cũ |
| **Cách sửa tạm thời** | Gõ `Corre`, rồi di chuyển tới chữ `r` dư và xoá đi 1 chữ |

### 1.2. Bản chất của lỗi

Trong bộ gõ Telex, ký tự `r` được dùng làm phím dấu **hỏi**. Khi người dùng gõ từ tiếng Anh `Core` trong chế độ Telex:

1. Gõ `C` → hiển thị `C`
2. Gõ `o` → hiển thị `Co`
3. Gõ `r` → engine hiểu `r` là dấu hỏi → biến `Co` thành `Cỏ`
4. Gõ `e` → engine không thể ghép `e` vào `Cỏ` một cách hợp lý → rơi vào các trạng thái fallback lộn xộn

Để đạt được kết quả `Core`, người dùng phải gõ thừa một chữ `r` (`Corre`). Lúc này:

- `Co` + `r` → `Cỏ`
- `Cỏ` + `r` (lặp lại phím dấu) → cơ chế undo khôi phục thành `Cor`
- `Cor` + `e` → `Core`

Đây là một workaround rất khó chịu và không tự nhiên.

---

## 2. Phân tích root cause

### 2.1. Luồng xử lý hiện tại cho `Core`

Pipeline trong `TelexEngine.handleCharInput`:

#### Bước 1: Gõ `C`

- `SyllableParser.parse("c")` → thất bại (chỉ có phụ âm đầu, không có nguyên âm).
- Fallback tiếng Anh: hiển thị `C`.
- `state.IsInvalidVietnamese = true`.

#### Bước 2: Gõ `o`

- `state.Syllable = None` (vì `C` không parse được).
- Luồng parse toàn chuỗi: `SyllableParser.parse("Co")`.
  - `initial = "c"`, `afterInitial = "o"`, `final = ""`, `vowelsRaw = "o"`.
  - `"o"` là nguyên âm hợp lệ.
  - Parse thành công: `Syllable { Initial="C"; Vowel="o"; Final=""; Tone=None }`.
- `TransformedText = "Co"`.
- `state.IsInvalidVietnamese = false`.

#### Bước 3: Gõ `r`

- Luồng gia tăng: `state.Syllable` hiện có, `r` là phím dấu hỏi.
- `ToneRules.applyTone Hook Modern {Initial="C"; Vowel="o"; Final=""}` → `Cỏ`.
- `TransformedText = "Cỏ"`.

#### Bước 4: Gõ `e`

- Luồng gia tăng: `state.Syllable = {Initial="C"; Vowel="ỏ"; Final=""}`.
- `e` không phải phím dấu thanh, không phải modifier.
- `ModifierRules.applyModifier 'e'` trả về `None` (vì `ỏ` đã có dấu, không phải nguyên âm cơ bản `e`).
- Thử ghép phụ âm cuối: `e` không thuộc các phụ âm cuối hợp lệ (`c, ch, m, n, ng, nh, p, t`).
- `modifiedSyllableOpt = None`.
- Luồng parse toàn chuỗi: `SyllableParser.parse("Core")`.
  - `resolveInlineModifiers` không thay đổi `Core`.
  - `lower = "core"`.
  - `initial = "c"`, `afterInitial = "ore"`, `final = ""`.
  - `vowelsRaw = "ore"`.
  - `'r'` không phải nguyên âm → `allVowelsValid = false` → parse thất bại.
- **Vấn đề:** Tại đây, engine có thể:
  - Fallback tiếng Anh: hiển thị `Core` — đây là kết quả mong đợi!
  - Hoặc do `FreeTonePlacement` đang bật, engine đã biến `Co` → `Cỏ` ở bước 3, và giờ không thể quay lại.

### 2.2. Vấn đề cốt lõi

Có hai vấn đề chính:

1. **Engine áp dụng Telex transformation quá sớm:** Khi `r` được gõ, engine ngay lập tức biến `Co` → `Cỏ`, mà không xét xem chuỗi đang gõ có khả năng là tiếng Anh hay không.
2. **Thiếu cơ chế "nhìn trước" (lookahead):** Engine không dự đoán rằng sau `r` còn có `e`, tạo thành từ `Core` (tiếng Anh), nên đã lỡ áp dụng dấu thanh.

### 2.3. Tại sao `isValidVietnamesePhonotactics` chưa đủ?

`FreeTonePlacement.isValidVietnamesePhonotactics` hiện đã có quy tắc:

```fsharp
if initLower = "c" then
    not (vowelLower.StartsWith "e" || vowelLower.StartsWith "ê" || vowelLower.StartsWith "i" || vowelLower = "oe")
```

Tuy nhiên quy tắc này chỉ được kiểm tra **sau khi** `SyllableParser.parse` thành công. Trong trường hợp `Core`:

- Khi mới gõ `Cor`, parse thất bại vì `r` không phải nguyên âm.
- Khi gõ `Core`, parse cũng thất bại vì `re` chứa `r`.
- Do đó `isValidVietnamesePhonotactics` chưa bao giờ được gọi với chuỗi `Core`.

Ngoài ra, ngay cả khi parse thành công, việc áp dụng dấu thanh đã xảy ra ở bước trước (khi gõ `r`), nên quy tắc phonotactics đến quá muộn.

---

## 3. Giải pháp đề xuất

### 3.1. Nguyên tắc thiết kế

1. **Bảo vệ từ tiếng Anh ngay từ đầu:** Khi chuỗi phím thô có dấu hiệu là từ tiếng Anh, engine không nên áp dụng Telex transformation lên các ký tự có thể là dấu thanh/modifier trong tiếng Anh.
2. **Không phá vỡ trải nghiệm Telex chuẩn:** `cor` trong tiếng Việt vẫn phải được xử lý đúng (dù `cor` không phải từ tiếng Việt hợp lệ, nhưng `co` + `r` vẫn là `cỏ` theo quy tắc Telex).
3. **Tận dụng cờ `AutoRestoreEnglishWords`:** Khi bật, engine ưu tiên giữ nguyên chuỗi tiếng Anh. Khi tắt, engine tuân theo Telex nghiêm ngặt hơn.
4. **Cơ chế đơn giản, dễ maintain:** Tránh dùng từ điển tiếng Anh lớn. Ưu tiên heuristic dựa trên phonotactics tiếng Việt.

### 3.2. Giải pháp 1: Trì hoãn áp dụng dấu thanh (Lazy Tone Application)

#### Ý tưởng

Thay vì áp dụng dấu thanh ngay khi gặp `r`, engine sẽ **trì hoãn** việc áp dụng dấu cho đến khi:

- Gặp phím ngắt từ (space, enter, dấu câu), hoặc
- Chuỗi phím thô đã được xác nhận là âm tiết tiếng Việt hợp lệ.

Trong thời gian trì hoãn, engine hiển thị chuỗi phím thô gốc (`Cor`) và theo dõi các candidate tone keys.

#### Ví dụ luồng

```
[C]    -> hiển thị "C",   state = {raw="C",   syllable=None}
[o]    -> hiển thị "Co",  state = {raw="Co",  syllable=None (đang chờ)}
[r]    -> hiển thị "Cor", state = {raw="Cor", pendingTones=[('r', 2)]}
[e]    -> hiển thị "Core", state = {raw="Core", pendingTones=[('r', 2)]}
        -> Phân tích: "Core" không phải tiếng Việt -> discard pending tones
[space]-> commit "Core"
```

#### Ưu điểm

- Không cần từ điển tiếng Anh.
- Tự động xử lý hầu hết các từ tiếng Anh chứa `r`, `s`, `f`, `x`, `j` sau nguyên âm.

#### Nhược điểm

- Người dùng không thấy dấu thanh hiện ra ngay lập tức trong quá trình gõ (`co` vẫn hiển thị `co` thay vì `cỏ` cho đến khi ngắt từ). Điều này thay đổi trải nghiệm Telex truyền thống.
- Các từ tiếng Việt dang dở như `cỏ` cũng sẽ không hiển thị dấu ngay.

### 3.3. Giải pháp 2: Nhận diện tiếng Anh qua phonotactics (English Detection Heuristic)

#### Ý tưởng

Khi gặp phím có thể là dấu thanh (`r`, `s`, `f`, `x`, `j`), engine kiểm tra xem nếu coi phím đó là ký tự thông thường (không phải dấu), thì chuỗi kết quả có vi phạm nghiêm trọng quy tắc tiếng Việt hay không.

Nếu **vi phạm nghiêm trọng** → coi đó là từ tiếng Anh, bảo vệ chuỗi phím thô.
Nếu **không vi phạm** hoặc **có thể là tiếng Việt** → áp dụng Telex transformation bình thường.

#### Quy tắc heuristic đề xuất

Cho chuỗi hiện tại `rawString` và ký tự mới `c` là phím dấu thanh tiềm năng:

1. **Thử áp dụng Telex:** tạo `telexResult`.
2. **Thử coi `c` là ký tự thường:** tạo `englishResult = rawString + c`.
3. **Kiểm tra `englishResult` có dấu hiệu tiếng Anh rõ ràng không:**
   - Chứa cụm phụ âm không tồn tại trong tiếng Việt (`cr`, `cl`, `br`, `tr` + nguyên âm `e/i`, `st`, `sp`, v.v.).
   - Chứa nguyên âm kết hợp không hợp lệ trong tiếng Việt (`oe`, `oa` + `e`, `ou`, v.v.).
   - Theo quy tắc `isValidVietnamesePhonotactics` mở rộng.
4. **Quyết định:**
   - Nếu `englishResult` rõ ràng là tiếng Anh và `telexResult` parse thất bại → chọn `englishResult`.
   - Nếu `telexResult` parse thành công và là tiếng Việt hợp lệ → chọn Telex.
   - Nếu mơ hồ → ưu tiên Telex (hành vi hiện tại) hoặc theo cấu hình `AutoRestoreEnglishWords`.

#### Ví dụ `Core`

```
[C]  -> "C"  -> không rõ tiếng Anh hay Việt
[o]  -> "Co" -> parse thành công tiếng Việt {C, o}
[r]  -> Thử Telex: "Cỏ" -> parse thành công tiếng Việt {C, ỏ}
        Thử English: "Cor" -> chứa "cor", phụ âm cuối r? Không hợp lệ trong tiếng Việt
        Nhưng "Cỏ" vẫn hợp lệ tiếng Việt, nên tạm chọn Telex
[e]  -> Thử Telex từ "Cỏ": thêm "e" -> không hợp lệ
        Thử English từ "Cor": "Core" -> chứa "re" không hợp lệ tiếng Việt
        -> Quyết định: đây là tiếng Anh, khôi phục chuỗi thô "Core"
```

#### Ưu điểm

- Vẫn hiển thị dấu thanh ngay khi gõ tiếng Việt.
- Tự động phát hiện tiếng Anh mà không cần từ điển.

#### Nhược điểm

- Logic phức tạp hơn giải pháp 1.
- Có thể nhầm lẫn với các từ tiếng Việt hiếm hoặc sai chính tả.

### 3.4. Giải pháp 3: Kết hợp Free Tone Placement + Phonotactics (Khuyến nghị)

#### Ý tưởng

Tận dụng module `FreeTonePlacement` đã có để:

1. Trích xuất các phím dấu thanh tự do từ chuỗi phím thô.
2. Tạo chuỗi nền (base string) sau khi loại bỏ tone keys.
3. Parse chuỗi nền xem có phải âm tiết tiếng Việt hợp lệ không.
4. Nếu **không** hợp lệ → coi toàn bộ là tiếng Anh, giữ nguyên chuỗi phím thô.
5. Nếu **có** hợp lệ → áp dụng dấu thanh vào đúng vị trí.

#### Áp dụng cho `Core`

```
rawKeys = [C; o; r; e]

extractTokens:
  - ProtectedInitial = "C"
  - Duyệt [o; r; e]:
    * 'o' -> nguyên âm, baseChars = [o], hasSeenVowel = true
    * 'r' -> tone key, hasSeenVowel = true -> candidateTones = [('r', 2)]
    * 'e' -> nguyên âm, baseChars = [o; e]
  - BaseString = "Coe"
  - CandidateTones = [('r', 2)]

SyllableParser.parse("coe"):
  - initial = "c", vowelsRaw = "oe"
  - 'o', 'e' đều là nguyên âm
  - Nhưng "coe" có phải cụm nguyên âm hợp lệ tiếng Việt không?
  - Theo phonotactics: "c" không đi với "oe" -> isValidVietnamesePhonotactics trả về false
  -> tryNormalizeFreeTone trả về None

Kết quả: engine fallback tiếng Anh -> "Core"
```

#### Áp dụng cho `Corre`

```
rawKeys = [C; o; r; r; e]

extractTokens:
  - ProtectedInitial = "C"
  - Duyệt [o; r; r; e]:
    * 'o' -> nguyên âm
    * 'r' -> tone key
    * 'r' -> tone key lặp lại -> HasRepeatedToneUndo = true
  - Vì AllowRepeatKeyUndo = true -> trả về None

Engine xử lý theo luồng gia tăng:
  - Co + r -> Cỏ
  - Cỏ + r (undo) -> Cor
  - Cor + e -> ???

Vấn đề: vẫn cần xử lý `Cor` + `e` đúng.
```

#### Khuyến nghị bổ sung

Giải pháp 3 cần được bổ sung thêm một bước: **sau khi undo tone**, nếu chuỗi kết quả có dấu hiệu tiếng Anh rõ ràng, engine nên chuyển sang chế độ bảo vệ tiếng Anh thay vì tiếp tục parse tiếng Việt.

### 3.5. Giải pháp 4: Phím tắt chuyển sang chế độ tiếng Anh tạm thời

#### Ý tưởng

Cung cấp một phím tắt (ví dụ `Ctrl + Shift` hoặc `~`) để người dùng tạm thời chuyển sang chế độ tiếng Anh trong khi gõ một từ.

Tuy nhiên, đây là giải pháp thủ công, không giải quyết triệt để vấn đề tự động nhận diện.

---

## 4. Giải pháp được khuyến nghị: Kết hợp Heuristic + Phonotactics

### 4.1. Kiến trúc tổng thể

```
[Ký tự mới c]
       │
       ▼
┌─────────────────────────────────────────────┐
│ 1. Cập nhật RawKeys = state.RawKeys @ [c]   │
└─────────────────────┬───────────────────────┘
                      ▼
┌─────────────────────────────────────────────┐
│ 2. FreeTonePlacement.tryNormalizeFreeTone    │
│    - Trích tone keys từ RawKeys              │
│    - Parse baseString                        │
│    - Kiểm tra phonotactics                   │
│    - Nếu thành công -> Trả về Syllable      │
│      (xử lý cả tiếng Việt hợp lệ)            │
└─────────────────────┬───────────────────────┘
                      │
        ┌─────────────┴─────────────┐
        ▼                           ▼
   [Thành công]               [Thất bại]
        │                           │
        ▼                           ▼
┌─────────────────┐     ┌─────────────────────────────┐
│ Áp dụng dấu     │     │ 3. EnglishWordHeuristic     │
│ hiện kết quả    │     │ Kiểm tra nếu RawKeys là     │
│ Telex/Vietnamese│     │ từ tiếng Anh rõ ràng         │
└─────────────────┘     └─────────────┬───────────────┘
                                      │
                          ┌───────────┴───────────┐
                          ▼                       ▼
                     [Là tiếng Anh]        [Không rõ/Không]
                          │                       │
                          ▼                       ▼
                 ┌────────────────┐      ┌─────────────────┐
                 │ Hiển thị thô    │      │ Fallback tiếng  │
                 │ RawKeys gốc     │      │ Anh bình thường │
                 │ (không áp dụng  │      │ hoặc giữ Telex  │
                 │ Telex)          │      │                 │
                 └────────────────┘      └─────────────────┘
```

### 4.2. Module `EnglishWordHeuristic`

Đề xuất tạo module mới:

```
src/BambooMintKey.Core/Engine/EnglishWordHeuristic.fs
```

Chức năng chính:

```fsharp
/// Kiểm tra xem chuỗi phím thô có phải là từ tiếng Anh rõ ràng hay không
let isLikelyEnglishWord (rawKeys: char list) : bool =
    let rawStr = String(Array.ofList rawKeys)
    let lower = rawStr.ToLowerInvariant()
    
    // 1. Loại bỏ các từ tiếng Việt ngắn hợp lệ (ví dụ: "co", "cỏ", "có")
    if isShortValidVietnameseSyllable lower then false
    else
        // 2. Kiểm tra các pattern tiếng Anh phổ biến
        let englishPatterns = [
            // Phụ âm + r/s sau nguyên âm e/i
            "re"; "res"; "rest"; "co"; "cor"; "core"; "wor"; "word"; "fir"; "first"
            "sp"; "st"; "str"; "spr"; "br"; "cr"; "cl"; "fl"; "fr"; "gr"; "pr"; "tr"
            // Cụm nguyên âm tiếng Anh không có trong tiếng Việt
            "ou"; "oi"; "ua"; "ue"; "ie"; "io"; "ia"; "ea"; "ee"; "oo"
        ]
        
        // 3. Kiểm tra theo phonotactics tiếng Việt
        let baseStr = extractBaseWithoutToneKeys rawStr
        match SyllableParser.parse baseStr with
        | Some s when isValidVietnamesePhonotactics s -> false
        | _ ->
            // 4. Nếu chứa pattern tiếng Anh rõ ràng -> true
            englishPatterns |> List.exists (fun p -> lower.Contains p)
```

### 4.3. Cập nhật `TelexEngine.handleCharInput`

Thêm bước sau khi `FreeTonePlacement.tryNormalizeFreeTone` thất bại:

```fsharp
// 5. Phát hiện và bảo vệ từ tiếng Anh
let isEnglishFallback =
    config.AutoRestoreEnglishWords &&
    EnglishWordHeuristic.isLikelyEnglishWord newRaw

if isEnglishFallback then
    let fallbackText = WordBuffer.applyCase detectedCase rawString
    let newState = {
        RawKeys = newRaw
        TransformedText = fallbackText
        Syllable = None
        Case = detectedCase
        IsInvalidVietnamese = true
    }
    (newState, EngineAction.UpdateComposition fallbackText)
else
    // 6. Luồng gia tăng Telex chuẩn
    ...
```

### 4.4. Quy tắc ưu tiên (Priority Rules)

Khi có xung đột giữa Telex và English:

1. Nếu `FreeTonePlacement` thành công → dùng Vietnamese (ưu tiên cao nhất vì parse được).
2. Nếu `EnglishWordHeuristic` báo là tiếng Anh rõ ràng và `AutoRestoreEnglishWords = true` → dùng English.
3. Nếu mơ hồ → dùng Telex (hành vi hiện tại).

---

## 5. Test cases đề xuất

| STT | Chuỗi phím | Kết quả mong đợi | Ghi chú |
|-----|------------|------------------|---------|
| 1 | `Core` + space | `Core` | Từ tiếng Anh cơ bản |
| 2 | `Corre` + space | `Corre` hoặc `Core` | Workaround hiện tại, cần xử lý nhất quán |
| 3 | `word` + space | `word` | `or` trong tiếng Việt không hợp lệ |
| 4 | `first` + space | `first` | Nhiều phụ âm tiếng Anh |
| 5 | `sport` + space | `sport` | Cụm `sp`, `or` |
| 6 | `co` + space | `có` | Từ tiếng Việt, vẫn phải giữ được |
| 7 | `cor` + space | `cỏ` hoặc `cor` | Biên giới mơ hồ, cần định nghĩa rõ |
| 8 | `cỏ` (gõ `cor`) | `cỏ` | Telex chuẩn vẫn phải hoạt động |

---

## 6. Rủi ro và hạn chế

| Rủi ro | Mức độ | Giải pháp |
|--------|--------|-----------|
| Nhầm từ tiếng Việt thành tiếng Anh | Trung bình | Dùng `isValidVietnamesePhonotactics` chặt chẽ, ưu tiên parse thành công. |
| Pattern tiếng Anh không đầy đủ | Thấp | Có thể mở rộng danh sách pattern dần dần. |
| Ảnh hưởng hiệu năng | Thấp | Heuristic đơn giản, chỉ chạy khi FreeTonePlacement thất bại. |
| Xung đột với undo lặp phím | Trung bình | Cần test kỹ `Corre`, `Cores`, `Corer`. |

---

## 7. Phụ thuộc

- Hoàn thiện `FreeTonePlacement` (lỗi 1).
- Cập nhật `isValidVietnamesePhonotactics` để bao phủ nhiều trường hợp tiếng Anh hơn.
- Cờ `AutoRestoreEnglishWords` phải được tôn trọng.

---

## 8. Tài liệu liên quan

- `docs/3.Issue/002_TypingError_v1.md` — báo cáo lỗi gốc.
- `docs/2.Design/Phase5/005_00_Free_Tone_Placement_Design.md` — thiết kế free tone placement.
- `docs/2.Design/Phase5/005_01_Uppercase_Modifier_Syllable_Design.md` — thiết kế lỗi 2, 3.
- `src/BambooMintKey.Core/Engine/FreeTonePlacement.fs` — module phonotactics hiện có.
- `src/BambooMintKey.Core/Engine/TelexEngine.fs` — engine xử lý phím.

---

## 9. Lịch sử thay đổi

| Ngày | Phiên bản | Người thay đổi | Nội dung |
|------|-----------|----------------|----------|
| 2026-09-08 | 1.0 | Dương Gia Long | Thiết kế xử lý từ tiếng Anh trong chế độ Telex (lỗi 4). |
