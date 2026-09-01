Dưới đây là tài liệu thiết kế chi tiết cho **Phần 1: Hệ thống Domain Types** của dự án **BambooMintKey**, bao gồm đặc tả thuật ngữ, phân bổ file mã nguồn và sample code chuẩn F#.

### 1. Đặc tả ngôn từ & Thuật ngữ Domain (Ubiquitous Language)

Để toàn bộ mã nguồn F# và tài liệu kỹ thuật đồng nhất, các khái niệm ngữ pháp tiếng Việt và trạng thái phím được chuẩn hóa bằng tiếng Anh chuyên ngành:

- **Tone (Dấu thanh):**
  - `Acute`: Dấu sắc (phím `s`).  
  - `Grave`: Dấu huyền (phím `f`).  
  - `Hook`: Dấu hỏi (phím `r`).  
  - `Tilde`: Dấu ngã (phím `x`).  
  - `Dot`: Dấu nặng (phím `j`).  
  - `None`: Không có dấu thanh (thanh ngang).
- **Modifier (Dấu phụ / Mũ, Móc, Ngang):**
  - `Hat`: Dấu mũ trên `â, ê, ô` (phím `aa, ee, oo`).  
  - `Horn`: Dấu móc trên `ơ, ư` (phím `ow, uw` hoặc `w`).  
  - `Breve`: Dấu trăng trên `ă` (phím `aw`).  
  - `DBar`: Dấu gạch ngang trên `đ` (phím `dd`).  
  - `None`: Ký tự nguyên bản không có dấu phụ.
- **Syllable Structure (Cấu trúc âm tiết tiếng Việt):**
  - `InitialConsonant`: Phụ âm đầu (ví dụ: `th`, `ngh`, `tr`, `b`).
  - `VowelNucleus`: Hạt nhân nguyên âm (ví dụ: `a`, `ươ`, `uyê`).
  - `FinalConsonant`: Phụ âm cuối (ví dụ: `nh`, `ng`, `ch`, `c`, `t`, `m`, `n`).
- **LetterCase (Trạng thái viết hoa/thường):**
  - `Lower`: Chữ thường toàn bộ (`việt`).
  - `Upper`: Chữ hoa toàn bộ (`VIỆT`).
  - `Title`: Chữ hoa đầu từ (`Việt`).
  - `Mixed`: Viết hoa lẫn lộn tùy biến (`vIệT`).
- **Engine Action (Lệnh đầu ra của State Machine):**
  - `UpdateComposition`: Cập nhật nội dung tạm thời và tiếp tục mở phiên gõ dở.  
  - `Commit`: Kết thúc phiên gõ, chốt văn bản và giải phóng bộ đệm.  
  - `PassThrough`: Không can thiệp, chuyển phím nguyên bản cho ứng dụng xử lý.  

### 2. Phân bổ các File F# trong Project `BambooMintKey.Core`

Toàn bộ hệ thống Domain Types được đặt trong thư mục `src/BambooMintKey.Core/Domain/` với 2 file chính:  

1. **`src/BambooMintKey.Core/Domain/Types.fs`**:  
   - Định nghĩa toàn bộ Discriminated Unions và Records đại diện cho phím nhấn, ngữ pháp, trạng thái từ và phản hồi của Engine.  
2. **`src/BambooMintKey.Core/Domain/EngineConfig.fs`**:
   - Định nghĩa các tham số cấu hình bộ gõ (tùy chọn dấu mới/cũ `òa/oà`, phím chuyển E/V, chế độ tự do).

### 3. Thiết kế chi tiết & Sample Code F#

#### File 1: `src/BambooMintKey.Core/Domain/Types.fs`

F#

```F#
namespace BambooMintKey.Core.Domain

open System

/// Biểu diễn 5 dấu thanh trong tiếng Việt + Thanh ngang (None)
type Tone =
    | None
    | Acute     // Sắc: s
    | Grave     // Huyền: f
    | Hook      // Hỏi: r
    | Tilde     // Ngã: x
    | Dot       // Nặng: j

/// Biểu diễn các dấu phụ (Mũ, Móc, Trăng, Gạch ngang)
type Modifier =
    | None
    | Hat       // Mũ: â, ê, ô (aa, ee, oo)
    | Horn      // Móc: ơ, ư (ow, uw, w)
    | Breve     // Trăng: ă (aw)
    | DBar      // Gạch ngang: đ (dd)

/// Định dạng viết hoa / viết thường của từ để bảo toàn sau biến đổi
type LetterCase =
    | Lower                 // việt
    | Upper                 // VIỆT
    | Title                 // Việt
    | Mixed of bool list    // Mảng boolean lưu trạng thái hoa/thường từng ký tự

/// Phân loại phím đầu vào được gửi từ TSF Native Bridge
[<RequireQualifiedAccess>]
type KeyInput =
    | Char of char          // Ký tự bảng chữ cái (a-z, A-Z)
    | Backspace             // Phím xóa lùi
    | WordBreak of char     // Ký tự ngắt từ: Space, Enter, Tab, Dấu câu (. , ; : ! ?...)
    | NonCharacter          // Các phím chức năng không làm thay đổi từ (Mũi tên, Home, End...)

/// Cấu trúc phân tích âm tiết của một từ tiếng Việt
type Syllable = {
    InitialConsonant: string
    VowelNucleus: string
    FinalConsonant: string
    Tone: Tone
    Modifiers: (char * Modifier) list
}

/// Trạng thái đầy đủ của một từ đang nằm trong bộ đệm gõ (Word Buffer)
type WordState = {
    /// Danh sách các ký tự phím thô người dùng đã nhấn theo thứ tự thời gian
    RawKeys: char list
    /// Chuỗi văn bản tiếng Việt đã được xử lý (NFC Unicode)
    TransformedText: string
    /// Phân tích cấu trúc âm tiết hiện tại
    Syllable: Syllable option
    /// Định dạng viết hoa/thường ban đầu
    Case: LetterCase
    /// Cờ đánh dấu từ này có vi phạm cấu trúc tiếng Việt hay không (để fallback tiếng Anh)
    IsInvalidVietnamese: bool
}
with
    /// Trạng thái rỗng khởi tạo ban đầu
    static member Empty = {
        RawKeys = []
        TransformedText = ""
        Syllable = Option.None
        Case = LetterCase.Lower
        IsInvalidVietnamese = false
    }

/// Lệnh kết quả trả về từ Engine cho lớp TSF NativeBridge thực thi
[<RequireQualifiedAccess>]
type EngineAction =
    /// Tiếp tục phiên composition, thay thế vùng gõ hiện tại bằng text mới
    | UpdateComposition of newText: string
    /// Chốt từ hoàn tất (khi gặp phím ngắt), xóa gạch chân và giải phóng buffer
    | Commit of committedText: string
    /// Nhả phím cho hệ điều hành/ứng dụng tự xử lý (không nuốt phím)
    | PassThrough
```

#### File 2: `src/BambooMintKey.Core/Domain/EngineConfig.fs`

F#

```F#
namespace BambooMintKey.Core.Domain

/// Chuẩn đặt dấu thanh cho nguyên âm mở (oa, oe, uy)
type TonePlacementStyle =
    /// Chuẩn mới: òa, óa, úy, xòa
    | Modern
    /// Chuẩn cũ: oà, oá, uý, xoà
    | Traditional

/// Cấu hình hoạt động của Engine Telex
type EngineConfig = {
    /// Bật/tắt chế độ gõ tiếng Việt (True: V, False: E)
    IsEnabled: bool
    /// Kiểu đặt dấu thanh (Modern vs Traditional)
    ToneStyle: TonePlacementStyle
    /// Tự động phục hồi từ gốc khi gõ từ sai ngữ pháp tiếng Việt (Fallback tiếng Anh)
    AutoRestoreEnglishWords: bool
    /// Cho phép gõ lặp dấu để khôi phục ký tự thô (ví dụ: 'ss' -> 's', 'aa' -> 'a')
    AllowRepeatKeyUndo: bool
    /// Cho phép phím 'w' đứng đầu từ biến thành 'ư' (True: w -> ư, False: w -> w)
    AllowLeadingWAsU: bool
}
with
    /// Cấu hình mặc định của BambooMintKey
    static member Default = {
        IsEnabled = true
        ToneStyle = TonePlacementStyle.Modern
        AutoRestoreEnglishWords = true
        AllowRepeatKeyUndo = true
        AllowLeadingWAsU = true
    }
```

### 4. Đánh giá tính toàn vẹn của Model

- **Bảo toàn tính bất biến (Immutability):** Toàn bộ cấu trúc `WordState` và `Syllable` là pure records, hỗ trợ cơ chế lưu lịch sử dạng danh sách (`WordState list`) để xử lý Undo/Backspace mà không sinh lỗi race-condition.
- **Tương thích Native C ABI:** `EngineAction` phân định rõ ràng 3 hành vi mà TSF Bridge cần xử lý (`SetText`, `EndComposition`, hoặc `pfEaten = FALSE`).  

