Dưới đây là tài liệu thiết kế chi tiết cho **Phần 4: Chuyển trạng thái, Hoàn tác & Fallback tiếng Anh (State Transitions, Undo & English Word Fallback)** thuộc dự án **BambooMintKey**.

### 1. Phân bổ File Thiết kế & Mã nguồn (Source File Allocation)

Toàn bộ logic kết nối State Machine, quản lý lịch sử gõ phím và điều phối kết quả trả về cho TSF Native Bridge được tổ chức trong thư mục `src/BambooMintKey.Core/Engine/` với 2 file chính:

1. **`src/BambooMintKey.Core/Engine/WordBuffer.fs`**
   - Quản lý bộ đệm từ hiện tại, ngăn xếp lịch sử (`History Stack`) phục vụ lùi phím (Backspace) và bảo toàn trạng thái chữ hoa / chữ thường.
2. **`src/BambooMintKey.Core/Engine/TelexEngine.fs`**
   - Cung cấp hàm biến đổi trạng thái thuần túy (`processKey`), thực hiện điều hướng logic gõ phím, lặp phím khôi phục (Undo/Escape) và cơ chế tự động trả về chuỗi thô khi gặp từ tiếng Anh hoặc từ vi phạm âm tiết tiếng Việt.

### 2. Mô tả Nội dung Kỹ thuật & Luồng Xử lý (Technical Specification)

#### A. Kiến trúc State Transition thuần túy (Pure Functional State Transition)

Hàm xử lý phím tuân thủ triệt để nguyên lý bất biến:

```
processKey : WordState -> KeyInput -> EngineConfig -> (WordState * EngineAction)
```

Engine nhận vào trạng thái từ hiện tại, phím người dùng vừa nhấn và cấu hình bộ gõ; sau đó trả về trạng thái từ mới cùng lệnh thực thi tương ứng cho TSF Bridge:

- **Nhận ký tự ngắt từ (`KeyInput.WordBreak` - Space, Enter, Tab, Dấu câu):** Chốt từ hiện tại (`EngineAction.Commit`), làm rỗng bộ đệm để bắt đầu từ mới.
- **Nhận phím xóa (`KeyInput.Backspace`):** Lấy trạng thái liền trước từ ngăn xếp `History`. Nếu lịch sử rỗng, xóa hoàn toàn và trả về chuỗi rỗng.
- **Nhận ký tự thông thường (`KeyInput.Char`):** Đưa phím vào chuỗi thô, phân tích lại âm tiết và quyết định biến đổi.

#### B. Cơ chế Lặp phím Hoàn tác (Undo / Escape via Key Repeat)

Khi người dùng đã có một ký tự biến đổi (dấu thanh hoặc dấu mũ/móc) mà tiếp tục gõ lặp lại phím dấu đó:

- **Lặp phím dấu thanh:** Đang có `má` (chứa Tone.Acute), nếu gõ tiếp phím `s` -> Hủy dấu sắc và biến thành `mas`.
- **Lặp phím dấu mũ/móc:** Đang có `â` (chứa Modifier.Hat), nếu gõ tiếp phím `a` -> Hủy dấu mũ và khôi phục về `aa`.
- **Lặp phím gạch ngang:** Đang có `đ` (chứa Modifier.DBar), nếu gõ tiếp phím `d` -> Hủy gạch ngang và khôi phục về `dd`.

#### C. Cơ chế Phát hiện & Fallback Từ tiếng Anh (English Word Fallback)

Khi người dùng gõ các từ ngoại ngữ (ví dụ: `code`, `start`, `text`, `system`, `filter`, `print`), cấu trúc phụ âm hoặc nguyên âm sẽ vi phạm bảng ngữ pháp tiếng Việt:

- Bộ phân rã âm tiết `SyllableParser.parse` sẽ trả về `None`.
- Khi phát hiện không thể tạo thành âm tiết tiếng Việt hợp lệ và tùy chọn `AutoRestoreEnglishWords = true` được bật:
  - Trạng thái từ chuyển `IsInvalidVietnamese = true`.
  - Văn bản biến đổi (`TransformedText`) được gán ngược lại bằng đúng chuỗi ký tự thô ban đầu (`RawKeys`), loại bỏ mọi biến dạng sai lệch.

#### D. Thuật toán Bảo toàn Trạng thái Viết hoa / Viết thường (Case Preservation)

Khi chuỗi thô được người dùng nhập vào với các định dạng hoa/thường khác nhau:

- **Chữ hoa toàn bộ (Upper):** `VIET` -> `VIỆT`.
- **Chữ hoa đầu từ (Title):** `Viet` -> `Việt`.
- **Chữ thường toàn bộ (Lower):** `viet` -> `việt`.
- **Hoa/thường lẫn lộn (Mixed):** `vIeT` -> Áp dụng lại mảng mask boolean `[false; true; false; true]` lên từng vị trí ký tự của chuỗi kết quả.

### 3. Thiết kế Chi tiết & Sample Code F#

#### File 1: `src/BambooMintKey.Core/Engine/WordBuffer.fs`

F#

```F#
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain

module WordBuffer =

    /// Xác định kiểu viết hoa/viết thường từ chuỗi phím thô
    let detectCase (rawChars: char list) : LetterCase =
        match rawChars with
        | [] -> LetterCase.Lower
        | [ c ] when Char.IsUpper(c) -> LetterCase.Title
        | chars ->
            let isAllUpper = chars |> List.forall Char.IsUpper
            let isAllLower = chars |> List.forall Char.IsLower
            let isTitle = Char.IsUpper(chars.Head) && (chars.Tail |> List.forall Char.IsLower)

            if isAllUpper then LetterCase.Upper
            elif isAllLower then LetterCase.Lower
            elif isTitle then LetterCase.Title
            else LetterCase.Mixed (chars |> List.map Char.IsUpper)

    /// Áp dụng định dạng viết hoa/thường lên chuỗi kết quả đã biến đổi
    let applyCase (letterCase: LetterCase) (text: string) : string =
        if String.IsNullOrEmpty(text) then text
        else
            match letterCase with
            | LetterCase.Lower -> text.ToLowerInvariant()
            | LetterCase.Upper -> text.ToUpperInvariant()
            | LetterCase.Title ->
                let first = Char.ToUpperInvariant(text.[0])
                let rest = if text.Length > 1 then text.Substring(1).ToLowerInvariant() else ""
                string first + rest
            | LetterCase.Mixed masks ->
                let chars = text.ToCharArray()
                let applied =
                    chars
                    |> Array.mapi (fun i c ->
                        if i < masks.Length && masks.[i] then Char.ToUpperInvariant(c)
                        else Char.ToLowerInvariant(c)
                    )
                String(applied)
```

#### File 2: `src/BambooMintKey.Core/Engine/TelexEngine.fs`

F#

```F#
namespace BambooMintKey.Core.Engine

open System
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Domain.UnicodeTables

module TelexEngine =

    /// Tái tạo chuỗi văn bản từ cấu trúc Syllable
    let private reconstructSyllableText (s: Syllable) : string =
        s.InitialConsonant + s.VowelNucleus + s.FinalConsonant

    /// Xử lý phím thêm ký tự mới vào State
    let private handleCharInput (c: char) (state: WordState) (config: EngineConfig) : WordState * EngineAction =
        let newRaw = state.RawKeys @ [ c ]
        let rawString = String(Array.ofList newRaw)
        let detectedCase = WordBuffer.detectCase newRaw

        // 1. Kiểm tra lặp phím để khôi phục (Undo/Escape)
        let isToneKey = ToneRules.keyToTone c |> Option.isSome
        let isModifierKey = "aweod".Contains(Char.ToLowerInvariant(c))

        let isUndoTone =
            config.AllowRepeatKeyUndo && isToneKey && state.Syllable.IsSome &&
            state.Syllable.Value.Tone <> Tone.None &&
            ToneRules.keyToTone c = Some state.Syllable.Value.Tone

        if isUndoTone then
            // Bỏ dấu thanh, phục hồi ký tự thô
            let cleanSyllable = ToneRules.applyTone Tone.None config.ToneStyle state.Syllable.Value
            let reconstructed = reconstructSyllableText cleanSyllable + string c
            let formatted = WordBuffer.applyCase detectedCase reconstructed
            let newState = {
                RawKeys = newRaw
                TransformedText = formatted
                Syllable = SyllableParser.parse reconstructed
                Case = detectedCase
                IsInvalidVietnamese = false
            }
            (newState, EngineAction.UpdateComposition formatted)
        else
            // 2. Thử phân tích cấu trúc ngữ pháp
            match SyllableParser.parse rawString with
            | Some parsedSyllable ->
                // Kiểm tra gán dấu thanh hoặc dấu phụ
                let updatedSyllable =
                    match ToneRules.keyToTone c with
                    | Some tone -> ToneRules.applyTone tone config.ToneStyle parsedSyllable
                    | None ->
                        match ModifierRules.applyModifier c parsedSyllable with
                        | Some modSyllable -> modSyllable
                        | None -> parsedSyllable

                let reconstructed = reconstructSyllableText updatedSyllable
                let formatted = WordBuffer.applyCase detectedCase reconstructed
                let newState = {
                    RawKeys = newRaw
                    TransformedText = formatted
                    Syllable = Some updatedSyllable
                    Case = detectedCase
                    IsInvalidVietnamese = false
                }
                (newState, EngineAction.UpdateComposition formatted)

            | None ->
                // 3. Không tạo thành âm tiết tiếng Việt hợp lệ (Fallback tiếng Anh)
                let fallbackText = WordBuffer.applyCase detectedCase rawString
                let newState = {
                    RawKeys = newRaw
                    TransformedText = fallbackText
                    Syllable = None
                    Case = detectedCase
                    IsInvalidVietnamese = true
                }
                (newState, EngineAction.UpdateComposition fallbackText)

    /// Hàm chuyển trạng thái chính của Telex Engine
    let processKey (state: WordState) (input: KeyInput) (config: EngineConfig) : WordState * EngineAction =
        if not config.IsEnabled then
            // Nếu bộ gõ đang ở chế độ E (Tắt), nhả phím cho ứng dụng
            (WordState.Empty, EngineAction.PassThrough)
        else
            match input with
            | KeyInput.Char c ->
                handleCharInput c state config

            | KeyInput.Backspace ->
                if state.RawKeys.IsEmpty then
                    (WordState.Empty, EngineAction.PassThrough)
                else
                    let newRaw = state.RawKeys |> List.take (state.RawKeys.Length - 1)
                    if newRaw.IsEmpty then
                        (WordState.Empty, EngineAction.UpdateComposition "")
                    else
                        let rawString = String(Array.ofList newRaw)
                        let detectedCase = WordBuffer.detectCase newRaw
                        match SyllableParser.parse rawString with
                        | Some s ->
                            let reconstructed = reconstructSyllableText s
                            let formatted = WordBuffer.applyCase detectedCase reconstructed
                            let newState = {
                                RawKeys = newRaw
                                TransformedText = formatted
                                Syllable = Some s
                                Case = detectedCase
                                IsInvalidVietnamese = false
                            }
                            (newState, EngineAction.UpdateComposition formatted)
                        | None ->
                            let formatted = WordBuffer.applyCase detectedCase rawString
                            let newState = {
                                RawKeys = newRaw
                                TransformedText = formatted
                                Syllable = None
                                Case = detectedCase
                                IsInvalidVietnamese = true
                            }
                            (newState, EngineAction.UpdateComposition formatted)

            | KeyInput.WordBreak breakChar ->
                if state.RawKeys.IsEmpty then
                    (WordState.Empty, EngineAction.PassThrough)
                else
                    // Chốt từ khi gặp khoảng trắng, enter hoặc dấu câu
                    let finalWord = state.TransformedText + string breakChar
                    (WordState.Empty, EngineAction.Commit finalWord)

            | KeyInput.NonCharacter ->
                (state, EngineAction.PassThrough)
```

