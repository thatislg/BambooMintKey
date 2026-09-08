<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Tính Năng: Bỏ Dấu Tự Do (Free Tone Placement)

**Mã tài liệu:** `004_00_Free_Tone_Placement_Design`

**Giai đoạn:** Phase 4 - Cải Tiến Trải Nghiệm Gõ

**Trạng thái:** 📝 Đang thiết kế (Design)

**Chế độ triển khai:** Chỉ thiết kế, **chưa được phép viết code**.

---

## 1. Tóm tắt vấn đề

BambooMintKey hiện tại tuân theo quy tắc Telex chuẩn: phím dấu thanh (`s f r x j`) phải được gõ ngay sau nguyên âm cần đặt dấu (hoặc sau cùng nếu engine tự tính vị trí). Điều này gây khó chịu với người dùng quen các bộ gõ khác (ví dụ Unikey, GoTiengViet, EVKey...) cho phép **bỏ dấu tự do** — tức là gõ dấu ở bất kỳ vị trí nào trong từ, engine sẽ tự động chuyển dấu về đúng vị trí theo quy tắc thanh điệu tiếng Việt.

### Ví dụ lỗi hiện tại

| Chuỗi phím | Kết quả mong đợi | Kết quả thực tế | Nguyên nhân |
|------------|------------------|-----------------|-------------|
| `phari` + space | `phải` | `phari` | Dấu `r` (hỏi) gõ ở cuối từ, engine chưa tự chuyển về nguyên âm `a`. |

Với tính năng **bỏ dấu tự do**, người dùng có thể gõ:

- `phari` → `phải`
- `phair` → `phải`
- `phar` + `i` → `phải` (gõ dấu trước, bổ sung nguyên âm sau)

Tất cả đều cho ra cùng một kết quả đúng.

---

## 2. Mục tiêu kỹ thuật

1. **Tương thích ngược:** Khi tắt tính năng, engine vẫn hoạt động đúng như Telex chuẩn hiện tại.
2. **Tự động chuyển dấu:** Khi bật, bất kỳ phím dấu thanh nào xuất hiện trong chuỗi phím thô đều được nhận diện và đặt vào đúng vị trí nguyên âm theo quy tắc thanh điệu tiếng Việt.
3. **Hỗ trợ cả hai kiểu gõ mới và cũ:** Dấu vẫn phải đặt đúng theo `TonePlacementStyle` (Modern/Traditional) sau khi được chuyển về vị trí chuẩn.
4. **Không phá vỡ undo bằng lặp phím:** Ví dụ `mass` (`ma` + `s` + `s`) vẫn phải hoạt động để trả về `mas` (xoá dấu) hoặc `ma` + `s` (tùy chính sách undo).
5. **Xử lý từ tiếng Anh và fallback:** Khi từ không phải tiếng Việt, engine phải quyết định hợp lý: giữ nguyên chuỗi phím thô hoặc fallback theo `AutoRestoreEnglishWords`.

---

## 3. Định nghĩa phạm vi

### 3.1. Phím dấu thanh được hỗ trợ

| Phím | Dấu |
|------|-----|
| `s` | Sắc |
| `f` | Huyền |
| `r` | Hỏi |
| `x` | Ngã |
| `j` | Nặng |

### 3.2. Trường hợp áp dụng

- Từ đang gõ là âm tiết tiếng Việt hợp lệ (có nguyên âm).
- Phím dấu thanh xuất hiện ở bất kỳ vị trí nào trong chuỗi phím thô, **ngoại trừ** trường hợp nó đã nằm đúng vị trí Telex chuẩn.
- Hỗ trợ cả từ không có phụ âm cuối (`phari` → `phải`) và có phụ âm cuối (`hoacs` → `hoặc`).

### 3.3. Trường hợp KHÔNG áp dụng

- Phím dấu thanh nằm sau từ đã commit.
- Phím dấu thanh là một phần của từ tiếng Anh mà engine đã xác định rõ ràng (ví dụ `Corre` vẫn cần cơ chế riêng, xem issue tương ứng).
- Tổ hợp phím tắt (`Ctrl`, `Alt`, `Win`).

---

## 4. Phân tích root cause

Dựa trên mã nguồn hiện tại (`src/BambooMintKey.Core/Engine/TelexEngine.fs`, `ToneRules.fs`, `SyllableParser.fs`):

1. **SyllableParser.parse** hiện chỉ nhận diện dấu thanh nếu nó đã tồn tại dưới dạng ký tự Unicode có dấu trong nguyên âm. Nó không nhận diện các phím dấu thanh Telex (`s`, `f`, `r`, `x`, `j`) lẫn trong chuỗi phím thô.
2. **TelexEngine.handleCharInput** xử lý phím dấu thanh theo luồng `applyTone` trực tiếp lên `state.Syllable` hiện có. Nếu `state.Syllable` chưa được xây dựng (vì dấu đến trước khi nguyên âm hoàn chỉnh) hoặc dấu đến sau phụ âm cuối, engine sẽ không áp dụng được dấu.
3. **ToneRules.getTargetVowelIndex** đã có logic tính vị trí đặt dấu đúng theo quy tắc thanh điệu. Chúng ta có thể tái sử dụng logic này sau khi trích xuất dấu từ chuỗi phím thô.

---

## 5. Giải pháp đề xuất

### 5.1. Tổng quan kiến trúc

Thêm một bước tiền xử lý **tùy chọn** trong pipeline xử lý phím, chỉ kích hoạt khi `AllowFreeTonePlacement = true`:

```
[Phím mới nhập]
       │
       ▼
┌─────────────────────────────────────────────┐
│ 1. Cập nhật RawKeys                          │
│ 2. Trích xuất tone keys từ RawKeys           │
│ 3. Tái cấu trúc chuỗi phím:                  │
│    - Loại bỏ tone keys khỏi vị trí cũ         │
│    - Chèn tone key vào sau nguyên âm đích     │
│ 4. Parse lại theo thứ tự Telex chuẩn          │
│ 5. Áp dụng dấu bằng ToneRules.applyTone       │
└──────────────────────────────────────────────┘
```

### 5.2. Cấu hình

Bổ sung cờ trong `EngineConfig`:

```fsharp
{
    // ... các trường hiện có ...
    AllowFreeTonePlacement: bool   // Mặc định: true
}
```

Nếu `AllowFreeTonePlacement = false`, engine giữ nguyên hành vi Telex chuẩn.

### 5.3. Luật chuyển đổi chuỗi phím

Cho một chuỗi phím thô, ví dụ `phari`:

1. **Tách phụ âm đầu, nguyên âm, phụ âm cuối, và dấu thanh**:
   - Phụ âm đầu: `ph`
   - Nguyên âm thô: `ai` (vì `r` là tone key, không phải nguyên âm)
   - Phụ âm cuối: `"`
   - Dấu thanh: `r` (hỏi)

2. **Xác định vị trí nguyên âm đích** theo `TonePlacementStyle`:
   - `ph` + `ai` + (không có phụ âm cuối) → vị trí dấu là nguyên âm `a` (index 0 theo Modern style).

3. **Tái cấu trúc thành chuỗi Telex chuẩn**:
   - `ph` + `a` + `r` + `i` = `phari` → nhưng bây giờ `r` đứng ngay sau `a`.
   - Thực tế, chuỗi chuẩn hóa là `phar` + `i` vẫn cho ra `phải` vì engine đặt dấu vào `a`.

> **Lưu ý:** Không cần di chuyển ký tự vật lý. Chỉ cần khi parse, engine nhận diện `r` là tone key và áp dụng nó vào đúng nguyên âm, bất kể `r` đang nằm ở đâu.

### 5.4. Thuật toán đề xuất (concept)

```fsharp
let normalizeFreeTone (rawKeys: char list) (config: EngineConfig) : char list option =
    if not config.AllowFreeTonePlacement then None
    else
        let toneKeys = rawKeys |> List.filter (fun c -> ToneRules.keyToTone c |> Option.isSome)
        if toneKeys.IsEmpty then None
        else
            // Tách non-tone keys để parse âm tiết nền
            let baseChars = rawKeys |> List.filter (fun c -> ToneRules.keyToTone c |> Option.isNone)
            let baseString = String(Array.ofList baseChars)
            
            match SyllableParser.parse baseString with
            | Some syllable ->
                // Xác định vị trí nguyên âm đích
                let hasFinal = not (String.IsNullOrEmpty syllable.FinalConsonant)
                let normVowel = ToneRules.normalizeVowels syllable.VowelNucleus syllable.InitialConsonant syllable.FinalConsonant
                let targetIdx = ToneRules.getTargetVowelIndex normVowel hasFinal config.ToneStyle
                
                // Xác định tone cuối cùng (nếu có nhiều tone key, quy tắc xử lý cần định nghĩa)
                let lastTone = toneKeys |> List.last |> ToneRules.keyToTone |> Option.get
                
                // Tái cấu trúc: chèn tone key vào sau nguyên âm đích
                let vowelPart = syllable.VowelNucleus
                let beforeTarget = vowelPart.Substring(0, targetIdx + 1)
                let afterTarget = vowelPart.Substring(targetIdx + 1)
                let newVowel = beforeTarget + string toneKey + afterTarget
                
                // Tái tạo chuỗi phím theo thứ tự: initial + newVowel + final
                let normalized =
                    syllable.InitialConsonant
                    + newVowel
                    + syllable.FinalConsonant
                Some (List.ofSeq normalized)
            | None -> None
```

### 5.5. Xử lý các trường hợp đặc biệt

#### 5.5.1. Nhiều phím dấu thanh

Quy tắc đề xuất:

- Nếu có nhiều tone key khác nhau: tone key **cuối cùng** được ưu tiên.
- Nếu lặp lại cùng một tone key: áp dụng cơ chế undo hiện có (`AllowRepeatKeyUndo`).

Ví dụ:

| Chuỗi phím | Kết quả |
|------------|---------|
| `pharis` | `phái` (sắc, vì `s` là tone cuối) |
| `phassi` | `phái` nếu `ss` được xử lý là undo, hoặc `phasi` nếu không |

#### 5.5.2. Dấu đến trước nguyên âm hoàn chỉnh

Ví dụ: `phr` → chưa đủ nguyên âm để xác định vị trí. Engine nên:

- Giữ nguyên chuỗi thô (`phr`) và hiển thị tạm thời.
- Khi người dùng gõ tiếp `i` → `phri` → normalize → `phải`.

#### 5.5.3. Từ không phải tiếng Việt

Nếu `baseChars` không parse được thành âm tiết tiếng Việt:

- Nếu `AutoRestoreEnglishWords = true`: giữ nguyên chuỗi phím thô (không áp dụng dấu).
- Nếu `AutoRestoreEnglishWords = false`: vẫn cố gắng áp dụng theo cách nào đó, nhưng có thể gây ra kết quả lạ.

---

## 6. Tích hợp vào pipeline hiện tại

### 6.1. Cách tích hợp tối thiểu

Thay đổi trong `TelexEngine.handleCharInput`:

1. Sau khi thêm ký tự mới vào `newRaw`, kiểm tra xem có tone key nào trong `newRaw` không.
2. Nếu có và `AllowFreeTonePlacement = true`, gọi hàm mới `FreeTonePlacement.normalize` để tái cấu trúc chuỗi phím.
3. Parse và áp dụng dấu theo chuỗi đã chuẩn hóa.
4. Cập nhật `WordState.RawKeys` vẫn giữ nguyên chuỗi người dùng đã gõ (để undo/backspace đúng), nhưng `TransformedText` là kết quả sau khi chuẩn hóa.

### 6.2. Lưu ý về RawKeys

- `RawKeys` nên **vẫn lưu chuỗi gốc** của người dùng, ví dụ `[p; h; a; r; i]`.
- Khi backspace, xóa ký tự cuối cùng của chuỗi gốc, sau đó re-normalize toàn bộ chuỗi còn lại.
- Điều này đảm bảo trải nghiệm xóa lùi tự nhiên.

### 6.3. Tách module mới

Đề xuất tạo file mới:

```
src/BambooMintKey.Core/Engine/FreeTonePlacement.fs
```

Module này chịu trách nhiệm:

- `extractToneKeys`: trích xuất danh sách tone key từ chuỗi phím thô.
- `buildBaseSyllable`: parse phần còn lại (không có tone key) thành `Syllable`.
- `normalize`: trả về `Syllable` đã được áp dụng đúng dấu, hoặc `None` nếu không áp dụng được.

---

## 7. Test cases đề xuất

| STT | Chuỗi phím | Kết quả mong đợi | Ghi chú |
|-----|------------|------------------|---------|
| 1 | `phari` | `phải` | Dấu ở cuối, không có phụ âm cuối |
| 2 | `phair` | `phải` | Dấu ngay sau nguyên âm (Telex chuẩn) |
| 3 | `hoacs` | `hoặc` | Dấu ở cuối, có phụ âm cuối `c` |
| 4 | `phar` + `i` | `phải` | Dấu đến trước khi nguyên âm hoàn chỉnh |
| 5 | `pharis` | `phái` | Nhiều tone key, ưu tiên tone cuối |
| 6 | `phassi` | `phái` hoặc `phasi` | Tùy chính sách undo lặp phím |
| 7 | `Core` | `Core` | Từ tiếng Anh, không áp dụng |
| 8 | `DDdi` | `Đi` | Xử lý chữ hoa + bỏ dấu tự do (nếu liên quan) |

---

## 8. Rủi ro và hạn chế

| Rủi ro | Mức độ | Giải pháp giảm thiểu |
|--------|--------|---------------------|
| Phá vỡ hành vi Telex chuẩn | Trung bình | Thêm cờ `AllowFreeTonePlacement`, mặc định bật nhưng cho phép tắt trong Settings. |
| Xung đột với undo lặp phím | Trung bình | Định nghĩa rõ quy tắc ưu tiên giữa free tone và repeat-key undo. |
| Tăng độ phức tạp parser | Thấp | Tách thành module `FreeTonePlacement.fs`, giữ `SyllableParser` không đổi. |
| Lỗi với từ tiếng Anh | Cao | Kết hợp với `AutoRestoreEnglishWords` và detection từ tiếng Anh. |

---

## 9. Phụ thuộc và tiên quyết

- Cần hoàn thành việc phân tích issue `002_TypingError_v1.md` để xác nhận tất cả các case lỗi liên quan.
- Cần cập nhật `EngineConfig` để thêm cờ `AllowFreeTonePlacement`.
- Cần cập nhật Settings GUI để cho phép bật/tắt tính năng này.

---

## 10. Tài liệu tham khảo

- `docs/3.Issue/002_TypingError_v1.md` — báo cáo lỗi gõ thực tế.
- `src/BambooMintKey.Core/Engine/TelexEngine.fs` — engine xử lý phím hiện tại.
- `src/BambooMintKey.Core/Engine/ToneRules.fs` — quy tắc đặt dấu thanh.
- `src/BambooMintKey.Core/Engine/SyllableParser.fs` — parser âm tiết tiếng Việt.
- `src/BambooMintKey.Core/Domain/EngineConfig.fs` — cấu hình engine.

---

## 11. Lịch sử thay đổi

| Ngày | Phiên bản | Người thay đổi | Nội dung |
|------|-----------|----------------|----------|
| 2026-09-08 | 1.0 | Dương Gia Long | Thiết kế ban đầu cho tính năng bỏ dấu tự do. |
