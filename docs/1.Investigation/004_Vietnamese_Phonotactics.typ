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
  #text(size: 18pt, weight: "bold")[VÙNG 1: CƠ SỞ NGỮ ÂM HỌC & TẬP LUẬT CHÍNH TẢ TIẾNG VIỆT] \
  #v(0.2cm)
  #text(size: 12pt, style: "italic")[Đặc tả toán học kèm ví dụ giải phẫu cụ thể cho F\# Core Engine]
  #v(0.6cm)
]

#v(0.2cm)
#outline(
  title: [Mục lục tài liệu],
  depth: 2,
  indent: 1.5em,
)
#v(0.5cm)

---

= Mô hình hóa Âm tiết Tiếng Việt (Vietnamese Syllable Canon)

Một âm tiết hoàn chỉnh ($S$) là đơn vị ngữ âm tối thiểu không thể phân chia tùy ý trên trục thời gian. Về mặt toán học, cấu trúc âm tiết tiếng Việt được mô hình hóa bằng một bộ 5 thành tố (5-tuple):

$ S = (O, M, N, C, T) $

Trong đó:
- $O in cal(O) union {emptyset}$: Phụ âm đầu (Onset).
- $M in cal(M) union {emptyset}$: Âm đệm (Medial / Glide).
- $N in cal(N)$: Âm chính (Nucleus) — thành tố bắt buộc duy nhất của phần vần.
- $C in cal(C) union {emptyset}$: Âm cuối (Coda).
- $T in cal(T)$: Thanh điệu (Tone) — siêu đoạn tính phủ lên toàn bộ âm tiết.

== Phân loại và Định lượng Kích thước Tập Phụ Âm Đầu ($cal(O)$)

Trong quá trình thiết kế bộ bóc tách từ vựng (Lexer/Parser) cho bộ gõ, kích thước của tập hợp $cal(O)$ có sự biến thiên tùy theo việc phân định ranh giới giữa ngữ âm học thuần túy và chính tả thực tế:

1. *Tập 25 phụ âm chữ viết cơ sở (Base Orthographic Set):*
   Gồm 16 phụ âm đơn và 9 phụ âm ghép:
   $ cal(O)_"base" = {"b", "c", "ch", "d", "đ", "g", "gh", "h", "k", "kh", "l", "m", "n", "ng", "ngh", "nh", "p", "ph", "r", "s", "t", "th", "tr", "v", "x"} $
   Độ lớn tập cơ sở: $|cal(O)_"base"| = 25$. Khi cộng thêm âm tắc thanh hầu rỗng (Zero-onset, $emptyset$) như trong các từ `án`, `uất`, `ôi`, `yến`, ta có $|cal(O)| = 26$.

2. *Xử lý con chữ `q` và tổ hợp `qu`:*
   - *Theo góc nhìn ngữ âm:* Chữ `q` không bao giờ đứng độc lập trước nguyên âm chính (không tồn tại các dạng `*qa`, `*qe`, `*qi`). Tổ hợp `qu` đại diện cho Onset `q` kết hợp với Medial `u` ($O = "q", M = "u"$).
   - *Theo trạng thái máy gõ (FSM / IME State):* Khi người dùng nhấn phím `q`, máy trạng thái chưa thể biết ký tự tiếp theo có phải là `u` hay không. Do đó, để tránh phân loại nhầm `q` thành ký tự ngoại lai, tập $cal(O)$ bắt buộc phải nạp thêm phần tử `"q"`. Lúc này:
     $ cal(O)_"with_q" = cal(O)_"base" union {"q"} union {emptyset} arrow.r.double.long |cal(O)| = 27 $

3. *Xử lý trường hợp phụ âm ghép `gi`:*
   - *Cách tiếp cận chuẩn âm học:* Coi `d` và `gi` cùng ghi lại âm đầu `/z/` (hoặc `/j/` trong phương ngữ Nam). Cụm `gi` được phân tích là phụ âm đầu `g` đi với bán âm/nguyên âm `i`.
   - *Cách tiếp cận chính tả độc lập (Tokenization shortcut):* Đưa `"gi"` thành một token Onset độc lập để giải quyết triệt để hiện tượng nuốt chữ khi đi với nguyên âm đôi `iê` (ví dụ `gi` + `iê` $arrow.r$ `giếng`, chữ `i` bị chập làm một).
   - Nếu tính cả `"q"` và `"gi"` là hai Onset độc lập, tập hợp mở rộng đạt kích thước:
     $ cal(O)_"full" = cal(O)_"base" union {"q", "gi"} union {emptyset} arrow.r.double.long |cal(O)| = 28 $

#figure(
  table(
    columns: (1.2fr, 2.8fr, 2.5fr, 3.5fr),
    inset: 6pt,
    align: horizon,
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Thành tố*], [*Tập ký hiệu chữ viết*], [*Kích thước tập hợp*], [*Ví dụ minh họa cụ thể*],
    [$cal(O)$ (Onset)],
    [b, c, ch, d, đ, g, gh, h, k, kh, l, m, n, ng, ngh, nh, p, ph, r, s, t, th, tr, v, x \ (+ q, gi tùy mô hình)],
    [$|cal(O)| in {26, 27, 28}$ \ (25 cơ sở + $emptyset$ + biến thể `q`/`gi`)],
    [`nghiêng` $arrow.r$ `ngh`, `quở` $arrow.r$ `q`, `án` $arrow.r emptyset$, `toán` $arrow.r$ `t`],

    [$cal(M)$ (Medial)], [o, u], [$|cal(M)| = 3$ (kể cả $emptyset$)], [`toán` $arrow.r$ `o`, `thuế` $arrow.r$ `u`, `tán` $arrow.r emptyset$],
    [$cal(N)$ (Nucleus)], [a, ă, â, e, ê, i, y, o, ô, ơ, u, ư, ia/iê/yê, ua/uô, ưa/ươ], [$|cal(N)| = 19$ đơn/đôi], [`toán` $arrow.r$ `a`, `thuyền` $arrow.r$ `iê`, `muỗi` $arrow.r$ `uô`],
    [$cal(C)$ (Coda)], [Phụ âm: c, ch, m, n, ng, nh, p, t \ Bán âm: i, y, o, u], [$|cal(C)| = 13$ (kể cả $emptyset$)], [`toán` $arrow.r$ `n`, `học` $arrow.r$ `c`, `mùi` $arrow.r$ `i`, `hoa` $arrow.r emptyset$],
    [$cal(T)$ (Tone)], [Ngang (0), Huyền (1), Sắc (2), Hỏi (3), Ngã (4), Nặng (5)], [$|cal(T)| = 6$ thanh chuẩn], [`toán` $arrow.r 2$, `toàn` $arrow.r 1$, `toản` $arrow.r 3$, `toãn` $arrow.r 4$, `toạn` $arrow.r 5$]
  ),
  caption: [Không gian tập hợp các thành tố âm tiết kèm các phương án phân loại Onset]
)

== Bảng giải phẫu cấu trúc thực tế (Exemplar Anatomical Table)

Mọi từ vựng tiếng Việt đều phân rã thành công theo công thức $S = (O, M, N, C, T)$ mà không có ngoại lệ:

#figure(
  table(
    columns: (1.5fr, 1fr, 1fr, 1.2fr, 1fr, 1fr, 3.3fr),
    inset: 5.5pt,
    align: (center, center, center, center, center, center, left),
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Từ*], [*$O$*], [*$M$*], [*$N$*], [*$C$*], [*$T$*], [*Giải phẫu ngữ âm*],
    [`toán`], [`t`], [`o`], [`a`], [`n`], [Sắc (2)], [Đầy đủ 5 thành tố; `o` đóng vai trò bán âm lướt làm tròn môi.],
    [`nghiêng`], [`ngh`], [$emptyset$], [`iê`], [`ng`], [Ngang (0)], [Phụ âm ghép ba ký tự; không có âm đệm; âm chính đôi `iê`.],
    [`khuých`], [`kh`], [`u`], [`y`], [`ch`], [Sắc (2)], [Âm đệm `u` đi cùng âm chính `y` và âm cuối tắc `ch`.],
    [`quở`], [`q`], [`u`], [`ơ`], [$emptyset$], [Hỏi (3)], [Quy ước chính tả: `qu` thực chất là tổ hợp của Onset `q` và Medial `u`.],
    [`giếng`], [`gi`], [$emptyset$], [`iê`], [`ng`], [Sắc (2)], [Chính tả rút gọn: `gi` ghép với `iê` bị triệt tiêu một ký tự `i`.],
    [`uống`], [$emptyset$], [$emptyset$], [`uô`], [`ng`], [Sắc (2)], [Khuyết phụ âm đầu ($O = emptyset$); bắt đầu trực tiếp bằng âm chính đôi.],
    [`oa`], [$emptyset$], [`o`], [`a`], [$emptyset$], [Ngang (0)], [Khuyết phụ âm đầu và âm cuối; chỉ gồm âm lướt `o` và âm chính `a`.],
    [`ý`], [$emptyset$], [$emptyset$], [`y`], [$emptyset$], [Sắc (2)], [Âm tiết tối giản cực hạn: chỉ có hạt nhân $N = "y"$ và thanh điệu $T = 2$.]
  ),
  caption: [Minh họa giải phẫu các dạng biến thể âm tiết]
)

= Tập luật Kết hợp Ngữ âm (Phonotactics Rules)

Phonotactics xác định không gian nghiệm hợp lệ:
$ cal(S)_"valid" subset cal(O) times cal(M) times cal(N) times cal(C) times cal(T) $

Nếu chuỗi ký tự vi phạm bất kỳ đẳng thức/bất đẳng thức nào dưới đây, bộ máy trạng thái (DFA) của F\# Core lập tức phân loại từ đó là từ ngoại lai (tiếng Anh) hoặc chuỗi phím sai ngữ pháp để từ chối xử lý hoặc hoàn tác (rollback).

== Ràng buộc giữa Âm cuối và Thanh điệu (Coda-Tone Constraints)

Tập âm cuối $cal(C)$ được chia thành 2 phân lớp toán học:
1. $cal(C)_"obstruent" = {"c", "ch", "p", "t"}$ (Âm tắc vô thanh / Khép).
2. $cal(C)_"sonorant" = cal(C) backslash cal(C)_"obstruent" = {"m", "n", "ng", "nh", "i", "y", "o", "u"}$ (Âm vang và bán âm / Hở).

#rect(fill: rgb("f8f9fa"), stroke: 0.5pt + rgb("cccccc"), inset: 10pt, radius: 4pt)[
  *Định lý 1 (Ràng buộc Coda Tắc):* Âm tiết kết thúc bằng âm tắc chỉ được phép mang thanh Sắc ($T = 2$) hoặc thanh Nặng ($T = 5$):
  $ C in {"c", "ch", "p", "t"} arrow.r.double.long T in {2, 5} $
]

#figure(
  table(
    columns: (1.5fr, 1.2fr, 1fr, 1fr, 3.3fr),
    inset: 6pt,
    align: (center, center, center, center, left),
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Ví dụ chuỗi*], [*Coda $C$*], [*Thanh $T$*], [*Hợp lệ?*], [*Phân tích ngữ âm*],
    [`bát`], [`t`], [Sắc (2)], [Có], [Thỏa mãn điều kiện $C in cal(C)_"obstruent"$ và $T = 2$.],
    [`bạt`], [`t`], [Nặng (5)], [Có], [Thỏa mãn điều kiện $C in cal(C)_"obstruent"$ và $T = 5$.],
    [`bàt`], [`t`], [Huyền (1)], [*Không*], [Vi phạm Định lý 1: $T = 1$ không thuộc ${2, 5}$.],
    [`bảt`], [`t`], [Hỏi (3)], [*Không*], [Vi phạm Định lý 1: $T = 3$ không thuộc ${2, 5}$.],
    [`bãt`], [`t`], [Ngã (4)], [*Không*], [Vi phạm Định lý 1: $T = 4$ không thuộc ${2, 5}$.],
    [`bàn`], [`n`], [Huyền (1)], [Có], [Coda $n in cal(C)_"sonorant"$, cho phép nhận đủ 6 thanh điệu.]
  ),
  caption: [Kiểm tra tính hợp lệ của thanh điệu theo âm cuối]
)

_Hệ quả lập trình trong F\# Core:_
Khi người dùng gõ `h-o-a-t` (đã có coda `t`), nếu gõ tiếp phím `f` (thanh Huyền), engine không được phép sinh ra `hoàt`. Engine phải nuốt phím hoặc tự động bỏ qua thanh điệu này.

== Ràng buộc tương thích Âm đệm (Medial Constraints)

Âm đệm $M$ chỉ nhận 1 trong 2 giá trị ký tự: `o` hoặc `u`.
- `o` xuất hiện khi nguyên âm kế tiếp là nguyên âm mở: ${a, ă, e}$.
- `u` xuất hiện khi nguyên âm kế tiếp là các nguyên âm dòng trước/dòng giữa khép: ${y, i, ê, ơ, â, "ia", "iê"}$.

#rect(fill: rgb("f8f9fa"), stroke: 0.5pt + rgb("cccccc"), inset: 10pt, radius: 4pt)[
  *Định lý 2 (Ràng buộc Bất tương thích Dòng sau):* Âm đệm không bao giờ đi liền trước các nguyên âm tròn môi dòng sau:
  $ M != emptyset arrow.r.double.long N in.not {"o", "ô", "u", "ư"} $
]

#figure(
  table(
    columns: (1.5fr, 1.2fr, 1.2fr, 1fr, 3.1fr),
    inset: 6pt,
    align: (center, center, center, center, left),
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Tổ hợp gõ*], [*$M$ giả định*], [*$N$ giả định*], [*Hợp lệ?*], [*Nguyên nhân ngữ âm*],
    [`hoa`], [`o`], [`a`], [Có], [`o` tương thích trước nguyên âm mở `a`.],
    [`thuế`], [`u`], [`ê`], [Có], [`u` tương thích trước nguyên âm dòng trước `ê`.],
    [`thuỷ`], [`u`], [`y`], [Có], [`u` tương thích trước bán âm/nguyên âm `y`.],
    [`buốc`], [`u`], [`ô`], [*Không*], [Vi phạm Định lý 2. Đây không phải âm đệm `u` ghép với `ô`, mà là nguyên âm đôi `uô` ($N = "uô", M = emptyset$).],
    [`tuơ`], [`u`], [`ơ`], [*Không*], [Vi phạm Định lý 2. Không tồn tại âm đệm `u` trước `ơ` trong từ thuần Việt (ngoại trừ từ mượn như *huơ*).],
    [`tuo`], [`u`], [`o`], [*Không*], [Vi phạm triệt để Định lý 2: cả 2 ký tự đều mang nét tròn môi.]
  ),
  caption: [Xác minh tính tương thích của âm đệm]
)

== Ràng buộc chính tả Phụ âm đầu và Nguyên âm (Orthographic Compatibility)

Sự lựa chọn chữ viết phụ âm đầu giữa ${"c", "k", "q"}$, ${"g", "gh"}$, và ${"ng", "ngh"}$ là hàm toán học phụ thuộc hoàn toàn vào thành tố kế tiếp:

$ "Onset"(M, N) = cases(
  {"k", "gh", "ngh"} & quad "khi" M = emptyset and N in {"i", "y", "e", "ê", "ia", "iê"},
  {"q"}              & quad "khi" M in {"u"},
  {"c", "g", "ng"}   & quad "khi" M = emptyset and N in.not {"i", "y", "e", "ê", "ia", "iê"}
) $

#figure(
  table(
    columns: (1.8fr, 1.8fr, 1fr, 3.4fr),
    inset: 6pt,
    align: (center, center, center, left),
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Tổ hợp chính tả*], [*Kế tiếp ($M, N$)*], [*Hợp lệ?*], [*Quy tắc kiểm tra*],
    [`kim`, `kẻ`, `kệ`], [$M = emptyset, N in {"i", "e", "ê"}$], [Có], [Đúng quy tắc: `k` đi trước nguyên âm dòng trước.],
    [`cim`, `cẻ`, `cệ`], [$M = emptyset, N in {"i", "e", "ê"}$], [*Không*], [Sai chính tả: không dùng `c` trước `i, e, ê`.],
    [`qua`, `quốc`], [$M = "u", N in {"a", "ô"}$], [Có], [Đúng quy tắc: bắt buộc dùng `q` khi có âm đệm `u`.],
    [`cua`, `cuốc`], [$M = emptyset, N = "uô"$], [Có], [Đúng quy tắc: `uô` là âm chính đôi, không có âm đệm nên dùng `c`.],
    [`nghe`, `nghi`], [$M = emptyset, N in {"e", "i"}$], [Có], [Đúng quy tắc: `ngh` đi trước nguyên âm dòng trước.],
    [`nga`, `ngơ`], [$M = emptyset, N in {"a", "ơ"}$], [Có], [Đúng quy tắc: `ng` đi trước nguyên âm dòng sau/giữa.],
    [`ngha`, `nghơ`], [$M = emptyset, N in {"a", "ơ"}$], [*Không*], [Sai chính tả: `ngh` không đi với nguyên âm dòng sau.]
  ),
  caption: [Bảng đối chiếu kiểm tra chính tả phụ âm đầu]
)

= Thuật toán Đặt Dấu Thanh (Tone Placement Algorithm)

Định vị chính xác con chữ nguyên âm mang dấu thanh trong cụm nguyên âm.

== Định nghĩa Toán học

Gọi $V$ là chuỗi các ký tự nguyên âm ghi nhận được ($V = M + N$ hoặc $V = N$), với độ dài chuỗi $|V| in {1, 2, 3}$.
- Hàm vị trí dấu: $P(V, C) in {0, 1, 2}$ biểu thị chỉ số ký tự trong chuỗi $V$ sẽ mang dấu thanh.

== Thuật toán Chuẩn Mới (Modern / Phonetic Orthography)

Chuẩn mới đặt dấu thanh vào *đỉnh âm lượng thực tế* (Phonetic Peak) của âm tiết:

$ P_"new"(V, C) = cases(
  0 & quad "nếu" |V| = 1,
  1 & quad "nếu" |V| = 2 and C = emptyset " và " V in {"oa", "oe", "uy"},
  0 & quad "nếu" |V| = 2 and C = emptyset " và " V in {"ia", "ua", "ưa"},
  1 & quad "nếu" |V| = 2 and C != emptyset,
  1 & quad "nếu" |V| = 3
) $

== Thuật toán Chuẩn Cũ (Traditional Orthography)

Chuẩn cũ đặt dấu thanh dựa trên *tính đối xứng hình học* và thói quen điện tín cổ điển:

$ P_"old"(V, C) = cases(
  0 & quad "nếu" |V| = 1,
  0 & quad "nếu" |V| = 2 and C = emptyset,
  1 & quad "nếu" |V| = 2 and C != emptyset,
  1 & quad "nếu" |V| = 3
) $

== Bảng so sánh trường hợp biên (Comprehensive Edge Cases)

#figure(
  table(
    columns: (1.5fr, 1.2fr, 1.5fr, 1.5fr, 3.3fr),
    inset: 6pt,
    align: (center, center, center, center, left),
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Cụm gõ thô*], [*Coda $C$*], [*Chuẩn Cũ*], [*Chuẩn Mới*], [*Giải thích cơ chế ngữ âm*],
    [`h-o-a + s`], [$emptyset$], [`hoá`], [`hóa`], [Chuẩn mới: `a` là âm chính mang trọng âm đỉnh $arrow.r$ đặt ở `a`.],
    [`t-h-u-y + r`], [$emptyset$], [`thuỷ`], [`thủy`], [Chuẩn mới: `y` là âm chính ($V[1]$) $arrow.r$ đặt ở `y`.],
    [`h-o-e + f`], [$emptyset$], [`hoè`], [`hòe`], [Chuẩn mới: `e` là nguyên âm mở mang trọng âm $arrow.r$ đặt ở `e`.],
    [`m-u-a + s`], [$emptyset$], [`múa`], [`múa`], [Nguyên âm đôi `ua`: cả 2 chuẩn đều đặt ở con chữ đầu `u`.],
    [`m-i-a + s`], [$emptyset$], [`mía`], [`mía`], [Nguyên âm đôi `ia`: cả 2 chuẩn đều đặt ở con chữ đầu `i`.],
    [`c-u-a + s`], [$emptyset$], [`cứa`], [`cứa`], [Nguyên âm đôi `ưa`: cả 2 chuẩn đều đặt ở con chữ đầu `ư`.],
    [`t-o-a-n + s`], [`n`], [`toán`], [`toán`], [Có Coda $C != emptyset$: Cả 2 chuẩn thống nhất đặt dấu ở âm chính `a`.],
    [`q-u-y-e-n + s`], [`n`], [`quyến`], [`quyến`], [Có Coda: Cả 2 chuẩn thống nhất đặt dấu ở âm chính `ê`.],
    [`n-g-o-a-i + f`], [`i`], [`ngoài`], [`ngoài`], [Triphthong + Coda bán âm: Cả 2 chuẩn đặt dấu tại âm chính giữa `a`.],
    [`r-u-o-u + f`], [`u`], [`rượu`], [`rượu`], [Nguyên âm đôi `ươ` đi với bán âm `u`: Đặt dấu tại chữ cái thứ hai `ơ`.]
  ),
  caption: [Bảng tra cứu so sánh vị trí dấu giữa hai chuẩn]
)

= Chuẩn Hóa Bảng Mã (Encoding Standards)

F\# Core phân rã mọi ký tự về dạng Unicode trừu tượng nội bộ, sau đó serialize sang 3 bảng mã đầu ra:

#figure(
  table(
    columns: (1.5fr, 2.5fr, 3fr, 2fr),
    inset: 6pt,
    align: horizon,
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Ký tự mẫu*], [*Dựng Sẵn (NFC)*], [*Tổ Hợp (NFD)*], [*TCVN3 (ABC)*],
    [`ệ`], [`U+1EC7` (1 Codepoint)], [`U+0065` + `U+0302` + `U+0323` (3 Codepoints)], [`0xEA` (Byte mã hóa đơn)],
    [`òa`], [`U+00F2` + `U+0061` (NFC chuẩn mới)], [`U+006F` + `U+0300` + `U+0061`], [`0xA2` (`o`) + `0xB8` (`à`)],
    [`oà`], [`U+006F` + `U+00E0` (NFC chuẩn cũ)], [`U+006F` + `U+0061` + `U+0300`], [`0xA2` (`o`) + `0xB8` (`à`)]
  ),
  caption: [Đối chiếu biểu diễn chuỗi giữa các định dạng mã hóa]
)
