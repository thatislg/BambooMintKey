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
  #text(size: 18pt, weight: "bold")[VÙNG 3: ÁNH XẠ PHƯƠNG THỨC NHẬP LIỆU & BÀN PHÍM ĐỘNG] \
  #v(0.2cm)
  #text(size: 12pt, style: "italic")[Mô hình hình thức hóa ánh xạ phím gõ Telex, VNI và cơ chế gõ tắt / macro cho F\# Core]
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

= Hình thức hóa Ánh xạ Phương thức Nhập liệu (Input Method Formalization)

Một phương thức gõ tiếng Việt (Input Method) được định nghĩa toán học là một ánh xạ từ một mã phím vật lý $k in Sigma$ kết hợp với ngữ cảnh bộ đệm hiện tại $B$ sang một phép biến đổi cấu trúc ngữ âm:

$ f_"IM": B times Sigma arrow.r B' times cal(A) $

Trong đó phép biến đổi ngữ âm tác động lên một trong ba nhóm thành tố:
1. *Biến đổi mũ / móc nguyên âm (Diacritic Modifier):* Thay đổi hình thái hạt nhân ($N arrow.r N'$ hoặc $O arrow.r O'$ như `d` sang `đ`).
2. *Gán thanh điệu (Tone Modifier):* Gán giá trị $T in {0, 1, 2, 3, 4, 5}$.
3. *Phím thoát / Lặp phím (Escape / Toggle Action):* Hoàn tác biến đổi để trả về ký tự Latin nguyên thủy.

== Phân loại Toán học của Phím Nhập liệu

Tập phím $Sigma$ dưới góc nhìn của Engine được phân thành 3 tập con rời rạc:
$ Sigma = Sigma_"base" union Sigma_"modifier" union Sigma_"delimiter" $

- $Sigma_"base"$: Tập các phím ký tự chữ cái chuẩn (`a-z`, `A-Z`) dùng để tạo phụ âm và nguyên âm thô ban đầu.
- $Sigma_"modifier"$: Tập các phím mang ngữ nghĩa biến đổi dấu (phụ thuộc kiểu gõ Telex hoặc VNI).
- $Sigma_"delimiter"$: Tập các phím ngắt từ (`Space`, `Enter`, phím điều hướng, dấu câu).

= Đặc tả Phương thức Nhập liệu Telex

Quy ước Telex sử dụng chính các chữ cái Latin không có trong bảng chữ cái tiếng Việt cơ bản (`w`, `f`, `s`, `r`, `x`, `j`) hoặc lặp lại ký tự để mã hóa dấu.

== Bảng Ánh xạ Phím Telex Chuẩn

#figure(
  table(
    columns: (1.2fr, 1.8fr, 1.8fr, 3.2fr),
    inset: 6pt,
    align: horizon,
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Phím gõ*], [*Mục tiêu biến đổi*], [*Kết quả biểu diễn*], [*Quy tắc ngữ cảnh*],
    [`s`], [Thanh Sắc ($T = 2$)], [á, ắ, ấ, é, ...], [Đặt vào âm chính $N$ theo thuật toán Vùng 1.],
    [`f`], [Thanh Huyền ($T = 1$)], [à, ằ, ầ, è, ...], [Đặt vào âm chính $N$ theo thuật toán Vùng 1.],
    [`r`], [Thanh Hỏi ($T = 3$)], [ả, ẳ, ổ, ẻ, ...], [Đặt vào âm chính $N$ theo thuật toán Vùng 1.],
    [`x`], [Thanh Ngã ($T = 4$)], [ã, ẵ, ỗ, ẽ, ...], [Đặt vào âm chính $N$ theo thuật toán Vùng 1.],
    [`j`], [Thanh Nặng ($T = 5$)], [ạ, ặ, ậ, ẹ, ...], [Đặt vào âm chính $N$ theo thuật toán Vùng 1.],
    [`a` (lặp)], [Biến đổi nón `â`], [a $arrow.r$ â], [Chỉ kích hoạt khi trước đó là `a` đơn.],
    [`e` (lặp)], [Biến đổi nón `ê`], [e $arrow.r$ ê], [Chỉ kích hoạt khi trước đó là `e` đơn.],
    [`o` (lặp)], [Biến đổi nón `ô`], [o $arrow.r$ ô], [Chỉ kích hoạt khi trước đó là `o` đơn.],
    [`d` (lặp)], [Biến đổi gạch `đ`], [d $arrow.r$ đ], [Chỉ kích hoạt khi trước đó là `d` đầu từ.],
    [`w`], [Biến đổi móc / trăng], [u $arrow.r$ ư, o $arrow.r$ ơ, a $arrow.r$ ă], [Tự động suy luận nguyên âm đích theo ngữ cảnh.],
    [`w` (đầu từ)], [Nguyên âm đơn `ư`], [$emptyset arrow.r$ ư], [Khai mở âm tiết mới bằng ký tự `ư`.]
  ),
  caption: [Bảng quy tắc chuyển dịch Telex]
)

== Cơ chế Đảo dấu và Khôi phục Phím (Toggle & Escape)

Để hỗ trợ người dùng gõ từ ngoại lai mà không bị dính dấu (ví dụ: gõ từ tiếng Anh `just` hay `forward`), Telex tuân theo luật đảo trạng thái (Toggle Inversion):

$ "ApplyTone"(N, T_k) = cases(
  (N', T_k) & quad "nếu" "CurrentTone"(N) != T_k, \
  (N_"raw", 0) & quad "nếu" "CurrentTone"(N) = T_k quad ("Nhấn lại phím dấu để gỡ dấu")
) $

Khi một phím bổ trợ bị nhấn lặp lại lần thứ hai liên tiếp:
1. Gỡ bỏ dấu thanh hoặc dấu phụ đã áp dụng.
2. Trả lại ký tự thô nguyên bản của phím đó (Ví dụ: `a` + `s` $arrow.r$ `á`, gõ tiếp `s` $arrow.r$ `as`).

= Đặc tả Phương thức Nhập liệu VNI

Quy ước VNI sử dụng các phím số hàng trên (`0-9`) làm tập $Sigma_"modifier"$, giữ nguyên bảng chữ cái hoàn toàn cho văn bản thô.

== Bảng Ánh xạ Phím VNI Chuẩn

#figure(
  table(
    columns: (1.2fr, 1.8fr, 1.8fr, 3.2fr),
    inset: 6pt,
    align: horizon,
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Phím gõ*], [*Mục tiêu biến đổi*], [*Kết quả biểu diễn*], [*Quy tắc ngữ cảnh*],
    [`1`], [Thanh Sắc ($T = 2$)], [á, é, ó, ...], [Áp dụng dấu thanh lên $N$.],
    [`2`], [Thanh Huyền ($T = 1$)], [à, è, ò, ...], [Áp dụng dấu thanh lên $N$.],
    [`3`], [Thanh Hỏi ($T = 3$)], [ả, ẻ, ỏ, ...], [Áp dụng dấu thanh lên $N$.],
    [`4`], [Thanh Ngã ($T = 4$)], [ã, ẽ, õ, ...], [Áp dụng dấu thanh lên $N$.],
    [`5`], [Thanh Nặng ($T = 5$)], [ạ, ẹ, ọ, ...], [Áp dụng dấu thanh lên $N$.],
    [`6`], [Biến đổi nón], [a $arrow.r$ â, e $arrow.r$ ê, o $arrow.r$ ô], [Áp dụng cho nguyên âm có dấu nón.],
    [`7`], [Biến đổi móc], [u $arrow.r$ ư, o $arrow.r$ ơ], [Áp dụng cho nguyên âm có dấu móc.],
    [`8`], [Biến đổi trăng], [a $arrow.r$ ă], [Áp dụng riêng cho chữ `a`.],
    [`9`], [Biến đổi gạch ngang], [d $arrow.r$ đ], [Áp dụng riêng cho phụ âm `d`.],
    [`0`], [Xóa dấu thanh], [á $arrow.r$ a, à $arrow.r$ a], [Đưa thanh điệu hiện tại về thanh Ngang ($T = 0$).]
  ),
  caption: [Bảng quy tắc chuyển dịch VNI]
)

= Cơ chế Bàn phím Động và Gõ tắt (Macro / Dynamic Mapping)

Để phục vụ mở rộng người dùng chuyên sâu, cấu trúc bàn phím không được fix cứng trong code mà được mô hình hóa thành bảng cấu hình động (Keymap Profile).

== Hình thức hóa Cấu hình Bàn phím (Keymap Profile)

Một bảng cấu hình bàn phím là một bản ghi hàm thuần túy:

$ cal(P) = (M_"tone", M_"diacritic", M_"macro") $

- $M_"tone": Sigma_"modifier" arrow.r cal(T)$: Ánh xạ gán thanh điệu.
- $M_"diacritic": Sigma_"modifier" times "char" arrow.r "char"$: Ánh xạ thay đổi hình thái con chữ.
- $M_"macro": "string" arrow.r "string"$: Bảng gõ tắt mở rộng.

== Luật kích hoạt Gõ tắt (Macro Expansion Rule)

Macro chỉ được kích hoạt khi con trỏ gặp ranh giới kết thúc từ ($sigma_k in cal(D)$):

$ "ExpandMacro"(W) = cases(
  M_"macro"(W) & quad "nếu" W in "dom"(M_"macro"), \
  W & quad "ngược lại"
) $

= Cài đặt Tham chiếu trong F\# Core

Khung mã nguồn F\# sau hiện thực hóa sự trừu tượng hóa kiểu gõ thành các First-class Functions:
