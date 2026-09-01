Dưới đây là tài liệu thiết kế chi tiết cho **Phần 5: Ma trận Test Cases Kiểm thử (Unit Test Matrix & Test Suite Specification)** thuộc dự án **BambooMintKey**, sử dụng toàn bộ ký tự thuần Unicode (không dùng LaTeX).

### 1. Phân bổ File Thiết kế & Mã nguồn (Source File Allocation)

Toàn bộ test suite kiểm thử ngữ pháp và State Machine được tổ chức trong thư mục `tests/BambooMintKey.Core.Tests/` với 5 file kịch bản chính:

1. **`tests/BambooMintKey.Core.Tests/SimpleTelexTests.fs`**
   - Kiểm thử các phép biến đổi phím đơn giản: mũ/móc/ngang (`aa`, `aw`, `ee`, `oo`, `ow`, `uw`, `dd`) và 5 dấu thanh (`s, f, r, x, j`).
2. **`tests/BambooMintKey.Core.Tests/TonePlacementTests.fs`**
   - Kiểm thử vị trí đặt dấu thanh động theo chuẩn Mới (`Modern`) và chuẩn Cũ (`Traditional`) trên các nguyên âm đôi/ba.
3. **`tests/BambooMintKey.Core.Tests/CompoundVowelTests.fs`**
   - Kiểm thử các từ tiếng Việt phức tạp có chứa 2 đến 3 nguyên âm kèm phụ âm cuối (`uyê`, `ươ`, `uô`, `oai`, `uay`).
4. **`tests/BambooMintKey.Core.Tests/RestoreAndUndoTests.fs`**
   - Kiểm thử cơ chế gõ lặp phím để khôi phục ký tự thô (`ss`, `aa`, `ww`, `dd`), xóa lùi liên tục (`Backspace`), và bảo toàn trạng thái viết hoa/thường.
5. **`tests/BambooMintKey.Core.Tests/EnglishFallbackTests.fs`**
   - Kiểm thử nhận diện và tự động giữ nguyên từ tiếng Anh / từ chuyên ngành kỹ thuật không bị dính dấu sai.

### 2. Mô tả Nội dung Kỹ thuật & Luồng Kiểm thử (Technical Specification)

#### A. Hàm Trợ giúp Kiểm thử Luồng Gõ Phím (Test Execution Pipeline Helper)

- Mô phỏng chính xác hành vi gõ bàn phím của người dùng:
  - Nhận vào chuỗi phím gõ thô (ví dụ: `"vietj"` hoặc `"hoas"`).
  - Khởi tạo `WordState.Empty`.
  - Duyệt từng ký tự qua `KeyInput.Char`, gọi hàm `TelexEngine.processKey` để cập nhật trạng thái liên tục.
  - Trích xuất kết quả `TransformedText` cuối cùng để so khớp với kỳ vọng.

#### B. Quy hoạch Ma trận 5 Nhóm Test Cases

1. **Nhóm 1: Biến đổi Telex cơ bản (Simple Telex Matrix)**
   - Dấu thanh: `as -> á`, `af -> à`, `ar -> ả`, `ax -> ã`, `aj -> ạ`.
   - Dấu phụ: `aa -> â`, `aw -> ă`, `ee -> ê`, `oo -> ô`, `ow -> ơ`, `uw -> ư`, `dd -> đ`.
   - Phím tắt móc: `uow -> ươ`, `w` đứng đầu -> `ư` (khi bật cấu hình).
2. **Nhóm 2: Đặt dấu thanh chuẩn Mới vs Cũ (Dynamic Tone Matrix)**
   - So sánh cấu hình `Modern` vs `Traditional`:
     - Chuỗi `"hoas"`: `Modern -> hóa` vs `Traditional -> hoá`.
     - Chuỗi `"hoaf"`: `Modern -> hòa` vs `Traditional -> hoà`.
     - Chuỗi `"thuys"`: `Modern -> thúy` vs `Traditional -> thuý`.
   - Từ có phụ âm cuối (Cả 2 chuẩn đều cho kết quả đồng nhất):
     - `"hoans" -> hoán`, `"thuyets" -> thuyết`, `"muowns" -> mượn`.
3. **Nhóm 3: Từ ghép 2-3 nguyên âm & Phụ âm phức tạp (Compound Vowels)**
   - Cụm nguyên âm 3 chữ: `"khuyur" -> khuỷu`, `"ngoaix" -> ngoại`, `"khuays" -> khuấy`.
   - Cụm phụ âm đầu đặc biệt (`qu`, `gi`): `"quas" -> quá`, `"quans" -> quán`, `"gias" -> giá`, `"giangs" -> giáng`.
4. **Nhóm 4: Lặp phím khôi phục & Bảo toàn chữ hoa (Undo & Case Preservation)**
   - Lặp phím dấu thanh: `"mass" -> mas`, `"toff" -> tof`, `"luxx" -> lux`.
   - Lặp phím dấu mũ: `"xaaa" -> xaa`, `"deee" -> dee`.
   - Giữ trạng thái viết hoa:
     - Chữ hoa toàn bộ: `"VIETJ" -> VIỆT`.
     - Chữ hoa đầu từ: `"Vietj" -> Việt`.
     - Hoa/thường lẫn lộn: `"vIeTj" -> vIệT`.
   - Xóa Backspace: Gõ `"viet" -> việt`, nhấn Backspace -> trở về `"viê"`.
5. **Nhóm 5: Tự động nhận diện từ tiếng Anh (English Fallback Matrix)**
   - Các từ code / IT phổ biến: `"code"`, `"start"`, `"filter"`, `"print"`, `"system"`, `"class"`, `"struct"`, `"string"`.
   - Không bị dính dấu sai: `"filter"` không bị biến đổi phím `f` thành dấu huyền sai vị trí.

### 3. Thiết kế Chi tiết & Sample Code F#

#### File 1: `tests/BambooMintKey.Core.Tests/SimpleTelexTests.fs`

F#

```F#
namespace BambooMintKey.Core.Tests

open Xunit
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Engine

module SimpleTelexTests =

    /// Helper mô phỏng quá trình gõ chuỗi phím tuần tự
    let typeWord (keys: string) (config: EngineConfig) : string =
        let mutable state = WordState.Empty
        for c in keys do
            let (newState, _) = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    [<Theory>]
    [<InlineData("as", "á")>]
    [<InlineData("af", "à")>]
    [<InlineData("ar", "ả")>]
    [<InlineData("ax", "ã")>]
    [<InlineData("aj", "ạ")>]
    [<InlineData("aa", "â")>]
    [<InlineData("aw", "ă")>]
    [<InlineData("ee", "ê")>]
    [<InlineData("oo", "ô")>]
    [<InlineData("ow", "ơ")>]
    [<InlineData("uw", "ư")>]
    [<InlineData("dd", "đ")>]
    let ``Telex basic transformations should match expected result`` (input: string, expected: string) =
        let result = typeWord input EngineConfig.Default
        Assert.Equal(expected, result)
```

#### File 2: `tests/BambooMintKey.Core.Tests/TonePlacementTests.fs`

F#

```F#
namespace BambooMintKey.Core.Tests

open Xunit
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Engine

module TonePlacementTests =

    let typeWordWithStyle (keys: string) (style: TonePlacementStyle) : string =
        let config = { EngineConfig.Default with ToneStyle = style }
        let mutable state = WordState.Empty
        for c in keys do
            let (newState, _) = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        state.TransformedText

    [<Theory>]
    [<InlineData("hoas", "hóa")>]
    [<InlineData("hoaf", "hòa")>]
    [<InlineData("thuys", "thúy")>]
    [<InlineData("xoef", "xòe")>]
    let ``Modern tone style should place tone on second vowel for open pairs`` (input: string, expected: string) =
        let result = typeWordWithStyle input TonePlacementStyle.Modern
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("hoas", "hoá")>]
    [<InlineData("hoaf", "hoà")>]
    [<InlineData("thuys", "thuý")>]
    [<InlineData("xoef", "xoè")>]
    let ``Traditional tone style should place tone on first vowel for open pairs`` (input: string, expected: string) =
        let result = typeWordWithStyle input TonePlacementStyle.Traditional
        Assert.Equal(expected, result)

    [<Theory>]
    [<InlineData("hoans", "hoán")>]
    [<InlineData("thuyets", "thuyết")>]
    [<InlineData("muowns", "mượn")>]
    [<InlineData("tieengs", "tiếng")>]
    let ``Both styles should place tone before final consonant consistently`` (input: string, expected: string) =
        let modernResult = typeWordWithStyle input TonePlacementStyle.Modern
        let traditionalResult = typeWordWithStyle input TonePlacementStyle.Traditional
        Assert.Equal(expected, modernResult)
        Assert.Equal(expected, traditionalResult)
```

#### File 3: `tests/BambooMintKey.Core.Tests/RestoreAndUndoTests.fs`

F#

```F#
namespace BambooMintKey.Core.Tests

open Xunit
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Engine

module RestoreAndUndoTests =

    [<Theory>]
    [<InlineData("mass", "mas")>]
    [<InlineData("toff", "tof")>]
    [<InlineData("luxx", "lux")>]
    [<InlineData("xaaa", "xaa")>]
    [<InlineData("deee", "dee")>]
    [<InlineData("dđ", "dd")>]
    let ``Repeating tone or modifier key should restore raw text`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let (newState, _) = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)

    [<Theory>]
    [<InlineData("VIETJ", "VIỆT")>]
    [<InlineData("Vietj", "Việt")>]
    [<InlineData("vietj", "việt")>]
    [<InlineData("vIeTj", "vIệT")>]
    let ``Engine should preserve original casing format`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let (newState, _) = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)

    [<Fact>]
    let ``Pressing backspace should step back to previous state correctly`` () =
        let config = EngineConfig.Default
        let mutable state = WordState.Empty

        // Gõ "viet" -> "việt"
        for c in "vietj" do
            let (newState, _) = TelexEngine.processKey state (KeyInput.Char c) config
            state <- newState
        Assert.Equal("việt", state.TransformedText)

        // Nhấn Backspace -> xóa 'j', trở về "viê"
        let (backState1, _) = TelexEngine.processKey state KeyInput.Backspace config
        Assert.Equal("viê", backState1.TransformedText)

        // Nhấn Backspace tiếp -> xóa 't', trở về "vi"
        let (backState2, _) = TelexEngine.processKey backState1 KeyInput.Backspace config
        Assert.Equal("vi", backState2.TransformedText)
```

#### File 4: `tests/BambooMintKey.Core.Tests/EnglishFallbackTests.fs`

F#

```F#
namespace BambooMintKey.Core.Tests

open Xunit
open BambooMintKey.Core.Domain
open BambooMintKey.Core.Engine

module EnglishFallbackTests =

    [<Theory>]
    [<InlineData("code", "code")>]
    [<InlineData("start", "start")>]
    [<InlineData("filter", "filter")>]
    [<InlineData("print", "print")>]
    [<InlineData("system", "system")>]
    [<InlineData("class", "class")>]
    [<InlineData("struct", "struct")>]
    let ``English words should not trigger invalid Vietnamese transformations`` (input: string, expected: string) =
        let mutable state = WordState.Empty
        for c in input do
            let (newState, _) = TelexEngine.processKey state (KeyInput.Char c) EngineConfig.Default
            state <- newState
        Assert.Equal(expected, state.TransformedText)
```

