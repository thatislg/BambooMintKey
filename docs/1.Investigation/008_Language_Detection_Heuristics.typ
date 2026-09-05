#set page(
  paper: "a4",
  margin: (x: 2cm, y: 2.5cm),
  header: align(right)[#text(size: 8pt, fill: rgb("666666"))[BambooMintKey Core Engine - Architecture Spec]],
  footer: context {
    let page_number = counter(page).at(here()).first()
    let total_pages = counter(page).final().first()
    align(center)[#text(size: 9pt)[#page_number / #total_pages]]
  }
)

#set text(
  font: "New Computer Modern",
  size: 10.5pt,
  lang: "vi"
)

#set par(justify: true, leading: 0.75em)
#set heading(numbering: "1.1.")

#align(center)[
  #v(0.8cm)
  #text(size: 18pt, weight: "bold")[THUẬT TOÁN NHẬN DIỆN TIẾNG ANH & PHÒNG CHỐNG FALSE-POSITIVE] \
  #v(0.2cm)
  #text(size: 12pt, style: "italic")[Đặc tả cơ chế phân biệt tiếng Anh/Việt thời gian thực và khôi phục ký tự thô cho `F#` Core Engine]
  #v(0.6cm)
]

#v(0.2cm)
#outline(
  title: [Mục lục tài liệu],
  depth: 2,
  indent: 1.5em
)
#v(0.5cm)

---

= Bài toán Nhận diện Nhầm (The False-Positive Dilemma)

Trong phương thức gõ Telex hoặc VNI, các phím gán dấu (`s, f, r, x, j, w` hoặc `1..9`) trùng hoàn toàn với các chữ cái và chữ số thông dụng trong tiếng Anh.

Hiện tượng *False-Positive* xảy ra khi người dùng có chủ đích soạn thảo văn bản tiếng Anh hoặc định danh mã nguồn (Source Code Identifiers), nhưng Engine nhận diện nhầm chuỗi phím đó là từ tiếng Việt hợp lệ, dẫn đến việc tự động biến đổi ký tự không mong muốn:
- Gõ `just` bị biến thành `jút` (phím `u` kết hợp với `s` thành `ú`).
- Gõ `system` bị biến thành `sýtem` (phím `y` kết hợp với `s` thành `ý`).
- Gõ `process` bị biến thành `prôcess` hoặc lỗi dấu lặp `s`.
- Gõ `first` bị biến thành `fỉst` hoặc `fírst`.

Mục tiêu của thuật toán là phân loại chính xác ý định người dùng với độ trễ $O(1)$, lập tức chuyển Engine sang trạng thái `Passthrough` và hoàn tác (Rollback) chuỗi ký tự về dạng thô nguyên bản ngay khi phát hiện dấu hiệu ngoại lai.

= Mô hình Phân lớp và Luật Loại trừ Ngữ âm (Phonotactic Rejection Rules)

Thay vì nạp một từ điển tiếng Anh đồ sộ hàng trăm nghìn từ gây tốn RAM và giảm hiệu năng CPU, `F#` Core sử dụng phương pháp **Heuristics dựa trên Bất đẳng thức Ngữ âm (Phonotactic Violations)**. Bất kỳ chuỗi ký tự nào vi phạm cấu trúc âm tiết tiếng Việt sẽ bị loại trừ ngay lập tức.

== Tập Phụ âm Đầu Ngoại lai (Forbidden Initial Consonants)

Tiếng Việt không chấp nhận các cụm phụ âm đôi (Consonant Clusters) ở vị trí Onset ngoại trừ các âm ghép chuẩn (`ch, gh, kh, ng, ngh, nh, ph, th, tr`):

$ cal(O)_"invalid" = {"cl", "cr", "dr", "fl", "fr", "gl", "gr", "pl", "pr", "sc", "sk", "sl", "sm", "sn", "sp", "st", "str", "sw", "tw"} $

Nếu tiền tố của từ $W$ chứa bất kỳ cụm nào thuộc $cal(O)_"invalid"$:
$ "Prefix"(W, 2) in cal(O)_"invalid" arrow.r.double.long "Reject"(W) $

_Ví dụ:_ `clear`, `free`, `project`, `start`, `string` $arrow.r$ Lập tức khóa dấu tiếng Việt.

== Tập Cụm Phụ âm Cuối Ngoại lai (Forbidden Coda Clusters)

Tiếng Việt chỉ chấp nhận tối đa 1 âm cuối ($C in {"c", "ch", "m", "n", "ng", "nh", "p", "t", "i", "y", "o", "u"}$). Toàn bộ các cụm phụ âm đôi/ba ở cuối từ đều là dấu hiệu tuyệt đối của tiếng Anh:

$ cal(C)_"cluster" = {"ct", "ft", "ld", "lf", "lk", "lm", "lp", "lt", "mp", "nd", "nt", "nk", "pt", "rk", "rt", "sk", "sp", "st"} $

$ "Suffix"(W, 2) in cal(C)_"cluster" arrow.r.double.long "Reject"(W) $

_Ví dụ:_ `act`, `lift`, `cold`, `test`, `context`, `bank` $arrow.r$ Ngắt biến đổi dấu và khôi phục từ.

== Các Ký tự Không Tồn tại trong Tiếng Việt Cơ bản

Sự xuất hiện của các con chữ sau ở các vị trí không phải âm đệm hay phụ âm ghép đặc thù:
- Con chữ `w` đứng ở vị trí âm cuối (như trong `show`, `view`, `draw`).
- Con chữ `j` đứng ở vị trí nguyên âm hoặc âm cuối (như trong `project`, `object`).
- Các ký tự thuần Latin: `f`, `z` đứng ở cuối từ (như trong `off`, `buzz`).

= Thuật toán Hoàn tác Thông minh (Smart Rollback Algorithm)

Khi người dùng đang gõ một từ tiếng Anh, có thời điểm Engine đã tạm thời ghép dấu (do chuỗi trước đó tình cờ hợp lệ). Khi ký tự ngoại lai xuất hiện, Engine phải thực hiện chuỗi hành động:
1. Xác định số ký tự đã hiển thị sai trên màn hình ứng dụng.
2. Gửi chỉ thị `Replace` với số phím Backspace tương ứng để xóa phần hiển thị sai.
3. Commit lại toàn bộ chuỗi phím thô (Raw keystrokes) người dùng đã gõ từ đầu từ đến thời điểm hiện tại.
4. Chuyển EngineState sang `Passthrough`.

#align(center)[
  #rect(stroke: 0.5pt + rgb("aaaaaa"), inset: 8pt, radius: 4pt)[
    *Kịch bản Rollback từ `just`:* \
    1. Gõ `j`: Buffering `['j']` $arrow.r$ Render `j` \
    2. Gõ `u`: Composing $arrow.r$ Render `ju` \
    3. Gõ `s`: Engine biến đổi dấu Sắc thành `jú` $arrow.r$ Emit `Replace(1, "ú")` \
    4. Gõ `t`: Phát hiện cụm `st` ở cuối từ ($"Suffix" in cal(C)_"cluster"$). \
       Engine tính toán: Màn hình đang có `jú` (2 ký tự), chuỗi thô cần có là `just` (4 ký tự). \
       $arrow.r$ Emit `Replace(2, "just")` và chuyển sang `Passthrough`.
  ]
]
