# Ghi chép lỗi biên dịch Typst: `004_Vietnamese_Phonotactics.typ`

File này tổng hợp các lỗi và cảnh báo đã gặp khi biên dịch tài liệu `004_Vietnamese_Phonotactics.typ` bằng Typst, cùng cách khắc phục tương ứng.

---

## Tóm tắt trạng thái hiện tại

```powershell
typst compile 004_Vietnamese_Phonotactics.typ
# Command executed successfully.
```

Hiện tại file đã biên dịch thành công, **không còn lỗi hay warning**.

---

## 1. Lỗi `expected expression` tại `F#`

**Mô tả:**

```text
error: expected expression
   ┌─ 004_Vietnamese_Phonotactics.typ:25:64
25 │ ...Mô hình hóa toán học cho F# Core Engine...
   │                                ^
```

**Nguyên nhân:**
Ký tự `#` trong Typst được dùng để bắt đầu lệnh hoặc gọi hàm (`#text`, `#set`, ...). Khi xuất hiện trong văn bản thường như `F# Core`, Typst hiểu nhầm `# Core` là một biểu thức và báo lỗi.

**Cách sửa:**
Escape ký tự `#` bằng dấu `\`:

```typst
F\# Core
```

**Các vị trí đã sửa:**
- Dòng 25: `F# Core Engine` → `F\# Core Engine`
- Dòng 75: `F# Core` → `F\# Core`
- Dòng 149: `Engine F# Core` → `Engine F\# Core`
- Dòng 164: `F# Core` → `F\# Core`
- Dòng 166: `F# Core` → `F\# Core`
- Dòng 168: `trong F#` → `trong F\#`

---

## 2. Lỗi `only element functions can be used as selectors`

**Mô tả:**

```text
error: only element functions can be used as selectors
  ┌─ 004_Vietnamese_Phonotactics.typ:5:17
5 │     footer: locate(loc => {
  │ ╭──────────────────^
```

**Nguyên nhân:**
Hàm `locate(...)` thuộc API cũ của Typst và đã bị loại bỏ trong các phiên bản mới. Thay vào đó, Typst sử dụng `context` để lấy thông tin bối cảnh trang hiện tại.

**Cách sửa:**
Thay `locate(loc => { ... })` bằng `context { ... }`:

```typst
footer: context {
  let page_number = counter(page).at(here()).first()
  let total_pages = counter(page).final().first()
  align(center)[#text(size: 9pt)[#page_number / #total_pages]]
}
```

---

## 3. Lỗi `unexpected argument` trong `counter(page).final(here())`

**Mô tả:**

```text
error: unexpected argument
  ┌─ 004_Vietnamese_Phonotactics.typ:7:42
7 │     let total_pages = counter(page).final(here()).first()
  │                                           ^^^^^^
```

**Nguyên nhân:**
Khi đã ở trong khối `context`, hàm `counter(...).final()` không cần và không nhận đối số `here()`. Nó tự động lấy tổng số trang trong ngữ cảnh hiện tại.

**Cách sửa:**
Bỏ `here()`:

```typst
let total_pages = counter(page).final().first()
```

---

## 4. Lỗi `unknown variable: ch`

**Mô tả:**

```text
error: unknown variable: ch
   ┌─ 004_Vietnamese_Phonotactics.typ:67:29
67 │ 1. $cal(C)_"obstruent" = {c, ch, p, t}$ ...
   │                              ^^
```

**Nguyên nhân:**
Trong chế độ toán học (math mode) của Typst, các chuỗi chữ cái như `ch`, `gh`, `ng` bị coi là tích của các biến đơn lẻ (`c × h`) hoặc biến chưa định nghĩa.

**Cách sửa:**
Bọc chuỗi ký tự trong dấu nháy kép để Typst hiểu là văn bản:

```typst
$ {"c", "ch", "p", "t"} $
```

**Các vị trí đã sửa:**
- Dòng 67: `{c, ch, p, t}` → `{"c", "ch", "p", "t"}`
- Dòng 75: `$c, ch, p, t$` → `$"c"$, $("ch"$, $("p"$, $("t"$`
- Dòng 91-93: các chuỗi nguyên âm/phụ âm trong `cases()`

---

## 5. Lỗi `unknown variable: implies`

**Mô tả:**

```text
error: unknown variable: implies
   ┌─ 004_Vietnamese_Phonotactics.typ:72:31
72 │   $ C in {"c", "ch", "p", "t"} implies T in {2, 5} $
   │                                ^^^^^^^
```

**Nguyên nhân:**
Typst không cung cấp sẵn ký hiệu `implies` trong math mode như LaTeX.

**Cách sửa:**
Thay bằng ký hiệu mũi tên kép tương đương:

```typst
$ C in {"c", "ch", "p", "t"} arrow.r.double.long T in {2, 5} $
```

---

## 6. Lỗi `unknown variable: ia`

**Mô tả:**

```text
error: unknown variable: ia
   ┌─ 004_Vietnamese_Phonotactics.typ:81:55
81 │ - `u` đi trước các nguyên âm còn lại: ${y, i, ê, ơ, â, ia/iê...}$.
   │                                                        ^^
```

**Nguyên nhân:**
Tương tự lỗi `ch`, chuỗi `ia`, `iê` trong math mode bị hiểu sai.

**Cách sửa:**
Với danh sách ký tự đơn thuần, không cần dùng math mode. Chuyển sang dùng code inline (backtick):

```typst
- `u` đi trước các nguyên âm còn lại: `y`, `i`, `ê`, `ơ`, `â`, `ia/iê...`.
```

---

## 7. Warning `linebreaks are ignored in branches`

**Mô tả:**

```text
warning: linebreaks are ignored in branches
   ┌─ 004_Vietnamese_Phonotactics.typ:91:90
91 │     {"k", "gh", "ngh"} & quad "khi" ..., \
   │ ╭──────────────────────────────────────────^
   │ = hint: use commas instead to separate each line
```

**Nguyên nhân:**
Hàm `cases(...)` trong Typst mới dùng dấu phẩy `,` để phân tách các nhánh, thay vì ký tự xuống dòng `\` như LaTeX.

**Cách sửa:**
Thay `\` ở cuối mỗi nhánh (trừ nhánh cuối cùng) bằng `,`:

```typst
$ "Onset"(N) = cases(
  {"k", "gh", "ngh"} & quad "khi" ...,
  {"q"}          & quad "khi" ...,
  {"c", "g", "ng"}   & quad "khi" ...
) $
```

**Các vị trí đã sửa:**
- Dòng 91-93: `cases()` trong phần Orthographic Compatibility
- Dòng 111-115: `cases()` thuật toán Chuẩn Mới
- Dòng 125-127: `cases()` thuật toán Chuẩn Cũ

---

## 8. Warning `unknown font family: linux libertine`

**Mô tả:**

```text
warning: unknown font family: linux libertine
   ┌─ 004_Vietnamese_Phonotactics.typ:13:8
13 │   font: "Linux Libertine",
   │         ^^^^^^^^^^^^^^^^^
```

**Nguyên nhân:**
Font `Linux Libertine` không được cài đặt trên hệ thống hoặc Typst không tìm thấy.

**Cách sửa:**
Thay bằng font có sẵn, ví dụ `New Computer Modern`:

```typst
#set text(
  font: "New Computer Modern",
  size: 11pt,
  lang: "vi"
)
```

---

## 9. Duplicate số hiệu đầu mục (ví dụ: `2.3. 2.3.`)

**Mô tả:**
Sau khi biên dịch, các heading hiển thị dạng lặp số, ví dụ:
```
2.3. 2.3. Ràng buộc chính tả phụ âm đầu và nguyên âm
```

**Nguyên nhân:**
File đã bật tự động đánh số heading:
```typst
#set heading(numbering: "1.1.")
```
Tuy nhiên, nội dung heading lại tự ghi thêm số thứ tự thủ công, ví dụ:
```typst
== 2.3. Ràng buộc chính tả phụ âm đầu và nguyên âm
```
Kết quả Typst đánh số một lần nữa, tạo ra `2.3. 2.3.`.

**Cách sửa:**
Bỏ số thủ công trong các heading, để Typst tự đánh số:

```typst
== Ràng buộc chính tả phụ âm đầu và nguyên âm
```

**Các vị trí đã sửa:**
- `= 1. Mô hình hóa...` → `= Mô hình hóa...`
- `= 2. Tập luật...` → `= Tập luật...`
- `== 2.1. Ràng buộc...` → `== Ràng buộc...`
- `== 2.2. Ràng buộc...` → `== Ràng buộc...`
- `== 2.3. Ràng buộc...` → `== Ràng buộc...`
- `= 3. Thuật toán...` → `= Thuật toán...`
- `== 3.1. Phân loại...` → `== Phân loại...`
- `== 3.2. Thuật toán Chuẩn Mới...` → `== Thuật toán Chuẩn Mới...`
- `== 3.3. Thuật toán Chuẩn Cũ...` → `== Thuật toán Chuẩn Cũ...`
- `= 4. Chuẩn Hóa Bảng Mã...` → `= Chuẩn Hóa Bảng Mã...`
- `== 4.1. Dựng Sẵn...` → `== Dựng Sẵn...`
- `== 4.2. Tổ Hợp...` → `== Tổ Hợp...`
- `== 4.3. Bảng mã TCVN3...` → `== Bảng mã TCVN3...`
- `= 5. Mô hình hóa kiểu dữ liệu...` → `= Mô hình hóa kiểu dữ liệu...`

---

## Bài học rút ra

1. **Ký tự đặc biệt trong Typst:** `#`, `$`, `_`, `&`, `\`, `{`, `}` đều có ý nghĩa cú pháp. Muốn hiển thị trong văn bản thường thì cần escape hoặc đặt trong chuỗi.
2. **Math mode không phải là chế độ văn bản tự do:** các chuỗi chữ trong `$...$` sẽ bị phân tích thành biến/ký hiệu. Nên dùng `"..."` cho văn bản hoặc dùng code inline `` `...` `` cho ký tự đơn.
3. **Typst thay đổi API nhanh:** `locate()` đã bị xóa, thay bằng `context`. Nên tham khảo tài liệu phiên bản mới nhất.
4. **`cases()` dùng dấu phẩy `,` phân nhánh**, không dùng `\`.
5. **Font cần có sẵn trong hệ thống.** Nên dùng font phổ biến như `New Computer Modern`, `Arial`, `Times New Roman`, hoặc kiểm tra `typst fonts`.
