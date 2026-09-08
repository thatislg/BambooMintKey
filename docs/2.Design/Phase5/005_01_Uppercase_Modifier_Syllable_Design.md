<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế: Xử Lý Lỗi Gõ Chữ Hoa Đầu Từ với Modifier (`Dd` → `Đ`, `Uw` → `Ư`)

**Mã tài liệu:** `005_01_môt tả`

**Giai đoạn:** Phase 5 - Cải Tiến Trải Nghiệm Gõ

**Trạng thái:** 📝 Đang thiết kế (Design)

**Chế độ triển khai:** Chỉ thiết kế và phân tích, **chưa được phép viết code sản phẩm**.

---

## 1. Tóm tắt vấn đề

Hai lỗi được báo cáo trong `docs/3.Issue/002_TypingError_v1.md` có cùng một bản chất kỹ thuật: engine hiện tại chưa xử lý đúng trường hợp **chữ hoa đứng đầu từ kết hợp với phím modifier Telex** (`Dd` → `Đ`, `Uw` → `Ư`), dẫn đến việc không thể tiếp tục gõ các nguyên âm/phụ âm phía sau để hoàn thiện âm tiết.

### 1.1. Lỗi 2: `Ddi` + space

| Trường | Giá trị |
|--------|---------|
| **Chuỗi phím đã gõ** | `Ddi` + space |
| **Kết quả mong đợi** | `Đi` |
| **Kết quả thực tế** | `Ddi` |
| **Cách sửa tạm thời** | `Dd` + space + Backspace + `i` |

### 1.2. Lỗi 3: `Uwu` + space

| Trường | Giá trị |
|--------|---------|
| **Chuỗi phím đã gõ** | `Uwu` + space |
| **Kết quả mong đợi** | `Ưu` |
| **Kết quả thực tế** | `Uwu` |
| **Cách sửa tạm thời** | `Uw` + space + Backspace + `u` |

---

## 2. Phân tích root cause

### 2.1. Luồng xử lý hiện tại

Pipeline xử lý phím trong `TelexEngine.handleCharInput` vận hành theo 3 bước chính khi `AllowFreeTonePlacement` được bật:

1. **Free Tone Placement tiền xử lý** (`FreeTonePlacement.tryNormalizeFreeTone`).
2. **Luồng gia tăng (Incremental)** trên `state.Syllable` hiện có — áp dụng dấu thanh, modifier, hoặc phụ âm cuối.
3. **Luồng parse toàn chuỗi** (`SyllableParser.parse`) khi luồng gia tăng thất bại.

### 2.2. Tại sao `Ddi` không ra `Đi`?

Khi người dùng gõ `D` → `d` → `i`:

**Bước 1: Gõ `D`**

- `SyllableParser.parse("d")` → thất bại vì chỉ có phụ âm đầu, không có nguyên âm.
- `modifiedSyllableOpt` = `None`.
- Engine rơi vào fallback tiếng Anh: hiển thị tạm `D`.

**Bước 2: Gõ `d`**

- `rawString = "Dd"`, `lower = "dd"`.
- Luồng gia tăng: `state.Syllable` hiện tại là `None`.
- Engine gặp case đặc biệt trong `TelexEngine.handleCharInput`:

```fsharp
if rawString.ToLowerInvariant() = "dd" then
    Some {
        InitialConsonant = if Char.IsUpper(newRaw[0]) then "Đ" else "đ"
        VowelNucleus = ""
        FinalConsonant = ""
        Tone = Tone.None
        Modifiers = [ ('d', Modifier.DBar) ]
    }
```

- Vì `newRaw[0] = 'D'` (viết hoa), `InitialConsonant = "Đ"`.
- `TransformedText = "Đ"`.
- **Vấn đề:** Syllable được tạo ra có `VowelNucleus = ""` (rỗng). Đây là một âm tiết "chết" — không có hạt nhân nguyên âm để tiếp tục phát triển.

**Bước 3: Gõ `i`**

- Luồng gia tăng: `state.Syllable = { Initial="Đ"; Vowel=""; Final="" }`.
- `i` không phải phím dấu thanh, không phải modifier (`a`, `w`, `e`, `o`, `d`).
- `ModifierRules.applyModifier 'i'` trả về `None` vì `VowelNucleus` rỗng.
- Thử ghép phụ âm cuối: `i` không thuộc `cmnpt`, `n+g`, `c+h`, `n+h`.
- `modifiedSyllableOpt = None`.
- Luồng parse toàn chuỗi: `SyllableParser.parse("Ddi")`.
  - `resolveInlineModifiers` không thay đổi `Ddi`.
  - `lower = "ddi"`.
  - Tách phụ âm đầu: `initial = "d"`.
  - Phần còn lại `"di"`, phụ âm cuối `""`, nguyên âm `"di"`.
  - `'d'` không phải nguyên âm → `allVowelsValid = false` → parse thất bại.
- Fallback tiếng Anh: `WordBuffer.applyCase Title "Ddi"` → `"Ddi"`.

**Kết luận lỗi 2:** Cơ chế đặc biệt `dd` → `đ` tạo ra một `Syllable` với `VowelNucleus` rỗng. Khi người dùng tiếp tục gõ nguyên âm `i`, engine không có cơ chế "mở rộng" âm tiết từ trạng thái chỉ có phụ âm đầu `Đ`.

### 2.3. Tại sao `Uwu` không ra `Ưu`?

Khi người dùng gõ `U` → `w` → `u`:

**Bước 1: Gõ `U`**

- `SyllableParser.parse("u")` → thành công.
- `Syllable = { Initial=""; Vowel="u"; Final=""; Tone=None }`.
- `TransformedText = "U"`.

**Bước 2: Gõ `w`**

- Luồng gia tăng: `applyModifier 'w'` trên syllable có `Vowel="u"`.
- `ModifierRules.transformVowel 'u' Modifier.Horn` → `"ư"`.
- `Syllable = { Initial=""; Vowel="ư"; Final=""; Tone=None }`.
- `TransformedText = "Ư"`.
- **Vấn đề:** Tương tự như lỗi 2, sau khi modifier được áp dụng, âm tiết chỉ còn nguyên âm đơn `"ư"` và không có khả năng tiếp nhận thêm nguyên âm phía sau.

**Bước 3: Gõ `u`**

- Luồng gia tăng: `state.Syllable = { Initial=""; Vowel="ư"; Final="" }`.
- `u` không phải phím dấu thanh, không phải modifier (`u` không nằm trong tập `aweod`).
- `ModifierRules.applyModifier 'u'` trả về `None`.
- Thử ghép phụ âm cuối: `u` không thuộc các phụ âm cuối hợp lệ.
- `modifiedSyllableOpt = None`.
- Luồng parse toàn chuỗi: `SyllableParser.parse("Uwu")`.
  - `resolveInlineModifiers` xử lý theo thứ tự:
    - `"Uwu".Replace("uw", "ư")` — không khớp vì `U` viết hoa.
    - Các replace khác cũng không khớp.
    - Kết quả resolved vẫn là `"Uwu"`.
  - `lower = "uwu"`.
  - `initial = ""`, `afterInitial = "uwu"`.
  - `final = ""`, `vowelsRaw = "uwu"`.
  - `'w'` không phải nguyên âm → `allVowelsValid = false` → parse thất bại.
- Fallback: `WordBuffer.applyCase Title "Uwu"` → `"Uwu"`.

**Kết luận lỗi 3:** Sau khi `w` biến `u` thành `ư`, engine không có cơ chế ghép thêm nguyên âm `u` vào cụm nguyên âm để tạo thành `ưu`. Hơn nữa, `resolveInlineModifiers` còn có vấn đề với chữ hoa: `"Uwu"` không được chuẩn hóa thành `"Ưu"` vì các replace trong module đang dùng chuỗi thường.

---

## 3. Giải pháp đề xuất

### 3.1. Nguyên tắc thiết kế

1. **Không phá vỡ luồng Telex chuẩn:** Khi `AllowFreeTonePlacement` tắt, các case `Ddi`, `Uwu` vẫn giữ hành vi hiện tại hoặc được xử lý theo cách ít xâm lấn nhất.
2. **Âm tiết có thể "mở rộng":** Một `Syllable` vừa được tạo ra từ modifier đơn lẻ (`Đ`, `Ư`) phải có khả năng tiếp nhận thêm nguyên âm/phụ âm cuối.
3. **Tái parse khi luồng gia tăng thất bại:** Khi không thể gia tăng, engine nên thử parse lại toàn bộ `RawKeys` dưới dạng Telex từ đầu, thay vì ngay lập tức fallback tiếng Anh.
4. **Xử lý chữ hoa nhất quán:** `resolveInlineModifiers` cần hoạt động đúng với cả chữ hoa và chữ thường, hoặc engine cần normalize case trước khi resolve rồi khôi phục case sau.

### 3.2. Giải pháp chi tiết

#### 3.2.1. Tái cấu trúc `Syllable` từ trạng thái chỉ có phụ âm đầu

Khi `dd` → `Đ` được tạo ra, thay vì để `VowelNucleus = ""`, engine nên:

- Đánh dấu trạng thái là **"phụ âm đầu đã hoàn thành, chờ nguyên âm"**.
- Khi nguyên âm `i` đến, mở rộng `VowelNucleus = "i"` và tái tạo âm tiết `{ Initial="Đ"; Vowel="i"; Final="" }`.

Kỹ thuật đề xuất:

```fsharp
// Thay vì:
{ InitialConsonant = "Đ"; VowelNucleus = ""; FinalConsonant = ""; Tone = None }

// Engine nên lưu trạng thái tạm:
{ InitialConsonant = "Đ"; VowelNucleus = ""; FinalConsonant = ""; Tone = None; IsAwaitingVowel = true }
```

Khi ký tự tiếp theo là nguyên âm, chuyển trạng thái:

```fsharp
if syllable.IsAwaitingVowel && isVowel c then
    Some { syllable with VowelNucleus = string c; IsAwaitingVowel = false }
```

#### 3.2.2. Cho phép mở rộng cụm nguyên âm sau modifier

Khi `Uw` → `Ư`, engine nên nhận diện rằng nguyên âm `ư` vẫn có thể kết hợp với nguyên âm khác (`u`) để tạo thành cụm `ưu`.

Quy tắc đề xuất:

- Nếu ký tự mới là nguyên âm và cụm nguyên âm mới là hợp lệ trong tiếng Việt (`ưu`, `ươ`, `ưa`, `ơi`, `ơa`, v.v.), mở rộng `VowelNucleus`.
- Cụm nguyên âm hợp lệ có thể được định nghĩa trong một bảng tra (`ValidVowelClusters`).

Ví dụ:

| Chuỗi phím | Trung gian | Kết quả |
|------------|------------|---------|
| `Uwu` | `Uw` → `Ư`, sau đó `+u` | `Ưu` |
| `Uwo` | `Uw` → `Ư`, sau đó `+o` | `Ươ` (qua `w` tiếp theo) hoặc `Ưo` |
| `Ow` | `O` → `O`, `+w` | `Ơ` |
| `Owi` | `Ow` → `Ơ`, `+i` | `Ơi` |

#### 3.2.3. Tái parse toàn chuỗi với case normalization

Trong luồng parse toàn chuỗi, trước khi gọi `SyllableParser.parse`, engine nên:

1. Chuẩn hóa case tạm thời về chữ thường.
2. Gọi `ModifierRules.resolveInlineModifiers` trên chuỗi thường.
3. Parse ra `Syllable`.
4. Áp dụng lại `WordBuffer.applyCase` để khôi phục định dạng hoa/thường gốc.

Điều này giải quyết vấn đề `"Uwu"` không được resolve thành `"Ưu"`.

#### 3.2.4. Tách logic "modifier-only syllable" thành module riêng

Đề xuất tạo hàm helper trong `ModifierRules.fs` hoặc module mới `ModifierSyllable.fs`:

```fsharp
/// Kiểm tra xem syllable có phải là trạng thái chỉ chứa phụ âm đầu đã được modifier hóa
let isModifierOnlySyllable (s: Syllable) : bool =
    String.IsNullOrEmpty s.VowelNucleus
    && not (String.IsNullOrEmpty s.InitialConsonant)
    && not (List.isEmpty s.Modifiers)

/// Mở rộng syllable chỉ có phụ âm đầu hoặc nguyên âm đơn bằng ký tự mới
let tryExtendWithChar (c: char) (s: Syllable) : Syllable option =
    if isModifierOnlySyllable s && isVowel c then
        Some { s with VowelNucleus = string c }
    elif isVowel c && canFormVowelCluster (s.VowelNucleus + string c) then
        Some { s with VowelNucleus = s.VowelNucleus + string c }
    else
        None
```

### 3.3. Luồng xử lý mới cho `Ddi`

```
[D] -> fallback "D"
[d] -> dd special case -> Syllable {Initial="Đ", Vowel="", Final="", IsAwaitingVowel=true}
      TransformedText = "Đ"
[i] -> tryExtendWithChar 'i' -> Syllable {Initial="Đ", Vowel="i", Final=""}
      TransformedText = "Đi"
[space] -> Commit "Đi"
```

### 3.4. Luồng xử lý mới cho `Uwu`

```
[U] -> parse "u" -> Syllable {Initial="", Vowel="u", Final=""}
       TransformedText = "U"
[w] -> applyModifier 'w' -> Syllable {Initial="", Vowel="ư", Final=""}
       TransformedText = "Ư"
[u] -> tryExtendWithChar 'u' -> Syllable {Initial="", Vowel="ưu", Final=""}
       TransformedText = "Ưu"
[space] -> Commit "Ưu"
```

---

## 4. Các thay đổi cần thiết (chưa triển khai)

### 4.1. Domain Types

- Thêm cờ `IsAwaitingVowel: bool` vào `Syllable` (hoặc dùng sentinel value `VowelNucleus = ""` kết hợp với `Modifiers` không rỗng).
- Hoặc tốt hơn: thay đổi cách `dd` → `đ` được lưu trữ — không tạo `Syllable` với nguyên âm rỗng mà chỉ đánh dấu `InitialConsonant` đã hoàn thành.

### 4.2. Engine

- Cập nhật `TelexEngine.handleCharInput` để gọi `tryExtendWithChar` trước khi rơi vào luồng parse toàn chuỗi.
- Cập nhật logic tạo `Syllable` từ `dd` để đánh dấu trạng thái chờ nguyên âm.

### 4.3. ModifierRules

- Thêm `tryExtendWithChar`.
- Thêm bảng `ValidVowelClusters` để kiểm tra cụm nguyên âm hợp lệ.

### 4.4. SyllableParser / resolveInlineModifiers

- Đảm bảo `resolveInlineModifiers` hoạt động đúng với chữ hoa (normalize case trước khi resolve).

### 4.5. Tests

Bổ sung test cases:

```fsharp
[<Theory>]
[<InlineData("Ddi", "Đi")>]
[<InlineData("Uwu", "Ưu")>]
[<InlineData("Ddi + space", "Đi ")>]
[<InlineData("Uwu + space", "Ưu ")>]
let ``Modifier-only syllable can accept following vowel`` (input, expected) = ...
```

---

## 5. Rủi ro và lưu ý

| Rủi ro | Mô tả | Giải pháp giảm thiểu |
|--------|-------|---------------------|
| Phá vỡ undo lặp phím | `dd` → `đ`, gõ `d` tiếp → `đd`? Cần định nghĩa rõ hành vi undo khi `Syllable` ở trạng thái chờ nguyên âm. | Thử nghiệm kỹ các case `Ddd`, `Dddi`, `Uww`, `Uwwu`. |
| Từ tiếng Anh bị nhầm | `Uwe`, `Ddie` có thể bị chuyển thành tiếng Việt không mong muốn. | Kết hợp `AutoRestoreEnglishWords` và `isValidVietnamesePhonotactics`. |
| Tăng độ phức tạp parser | Việc mở rộng nguyên âm sau modifier làm tăng số trạng thái cần xử lý. | Giới hạn `ValidVowelClusters` chỉ gồm các cụm nguyên âm thực sự tồn tại trong tiếng Việt. |

---

## 6. Tài liệu liên quan

- `docs/3.Issue/002_TypingError_v1.md` — báo cáo lỗi gốc.
- `docs/2.Design/Phase5/005_00_Free_Tone_Placement_Design.md` — thiết kế free tone placement (lỗi 1).
- `src/BambooMintKey.Core/Engine/TelexEngine.fs` — engine xử lý phím.
- `src/BambooMintKey.Core/Engine/ModifierRules.fs` — quy tắc modifier.
- `src/BambooMintKey.Core/Engine/SyllableParser.fs` — parser âm tiết.
- `src/BambooMintKey.Core/Domain/Types.fs` — định nghĩa `Syllable`, `WordState`.

---

## 7. Lịch sử thay đổi

| Ngày | Phiên bản | Người thay đổi | Nội dung |
|------|-----------|----------------|----------|
| 2026-09-08 | 1.0 | Dương Gia Long | Thiết kế phân tích lỗi 2 (`Ddi` → `Đi`) và lỗi 3 (`Uwu` → `Ưu`). |
