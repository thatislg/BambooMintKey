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
  #text(size: 18pt, weight: "bold")[VÙNG 2: CẤU TRÚC DỮ LIỆU & LÝ THUYẾT TÍNH TOÁN] \
  #v(0.2cm)
  #text(size: 12pt, style: "italic")[Mô hình hóa Máy trạng thái hữu hạn (DFA) và Biến đổi trạng thái hàm thuần túy cho F\# Core]
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

= Hình thức hóa Máy Trạng thái Hữu hạn (Deterministic Finite Automaton - DFA)

Trong kiến trúc lõi của bộ gõ, việc nhận diện và cấu tạo âm tiết được mô hình hóa toán học bằng một bộ ngũ thành tố DFA:

$ M = (Q, Sigma, delta, q_0, F) $

Trong đó:
- $Q$: Tập hữu hạn các trạng thái của Engine.
- $Sigma$: Bảng chữ cái đầu vào (tập hợp tất cả các mã phím gõ từ bàn phím và cờ điều khiển Modifier).
- $delta: Q times Sigma arrow.r Q times cal(A)$: Hàm chuyển trạng thái kèm theo hành động xuất (Action output).
- $q_0 in Q$: Trạng thái khởi tạo mặc định (`Idle`).
- $F subset Q$: Tập hợp các trạng thái kết thúc/hợp lệ (Terminal valid states).

== Không gian trạng thái $Q$

Khác với mô hình hướng đối tượng dùng biến toàn cục khả biến, F\# Core mô hình hóa không gian trạng thái thành một tập hợp các trạng thái loại trừ lẫn nhau (Discriminated Unions):

1. $q_"Idle"$: Trạng thái rỗng. Bộ đệm rỗng, con trỏ văn bản không chứa âm tiết đang soạn thảo dở dang.
2. $q_"Buffering"$: Đang gom cụm phụ âm đầu $cal(O)$ hoặc các ký tự khởi đầu nhưng chưa xác lập nguyên âm chính.
3. $q_"Composing"$: Đang xây dựng cấu trúc âm tiết hợp lệ $S = (O, M, N, C, T)$.
4. $q_"Passthrough"$: Chuỗi ký tự nhập vào vi phạm luật ngữ âm tiếng Việt (từ tiếng Anh, chuỗi code, hoặc định danh biến). Mọi phím bấm tiếp theo trong từ này được chuyển tiếp trực tiếp vào ứng dụng mà không can thiệp biến đổi dấu.

== Không gian sự kiện đầu vào $Sigma$

Mỗi phần tử $sigma in Sigma$ đại diện cho một sự kiện gõ phím hoàn chỉnh:
$ sigma = (k, m) $
- $k in [0, 255]$: Mã ký tự hoặc Virtual Key code.
- $m in {"None", "Shift", "Ctrl", "Alt", "Meta"}$: Mặt nạ phím bổ trợ.

== Không gian hành động đầu ra $cal(A)$

Sau mỗi bước chuyển trạng thái, Engine phát ra một hành vi cụ thể cho Adapter phía ngoài (C++ Fcitx5 hoặc C\# TSF):

$ cal(A) = cases(
  "Pass",                       & quad "Cho phép phím đi thẳng vào ứng dụng",
  "Consume",                    & quad "Nuốt phím, không chuyển tiếp cho ứng dụng",
  "Commit"(s),                  & quad "Gửi chuỗi hoàn chỉnh " s " vào văn bản",
  "Replace"(b, s)               & quad "Gửi " b " phím Backspace, sau đó gửi chuỗi mới " s
) $

= Biến đổi Trạng thái Hàm Thuần túy (Pure Functional State Transition)

Trọng tâm của F\# Core là triệt tiêu hoàn toàn hiệu ứng lề (Side-effects). Toàn bộ chu trình gõ được gói gọn trong một hàm thuần túy:

$ "step": Q times Sigma arrow.r Q times cal(A) $

== Bảng chuyển trạng thái mở rộng

#figure(
  table(
    columns: (1.5fr, 1.8fr, 1.8fr, 1.8fr, 3.1fr),
    inset: 6pt,
    align: horizon,
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Trạng thái hiện tại ($q$)*], [*Sự kiện vào ($sigma$)*], [*Trạng thái kế tiếp ($q'$)*], [*Hành động ($alpha$)*], [*Giải thích nghiệp vụ*],
    [`Idle`], [Ký tự alpha $c$], [`Buffering([c])`], [`Pass`], [Ký tự đầu tiên của từ, lưu vào bộ đệm và cho hiển thị bình thường.],
    [`Buffering(buf)`], [Ký tự biến đổi dấu $c$], [`Composing(...)`], [`Replace(b, s)`], [Khớp thành công luật gõ, lùi con trỏ và đè ký tự mang dấu.],
    [`Composing(s)`], [Ký tự vi phạm Phonotactics], [`Passthrough(...)`], [`Pass`], [Phát hiện vi phạm cấu trúc tiếng Việt, hủy chế độ gõ dấu.],
    [`Composing(s)`], [Dấu cách (Space) / Dấu câu], [`Idle`], [`Pass`], [Kết thúc âm tiết, đưa bộ đệm về rỗng để đón từ tiếp theo.],
    [`*`], [Tổ hợp `Ctrl+Key` / `Alt+Key`], [`Idle`], [`Pass`], [Gãy mạch gõ do phím tắt hệ thống, reset toàn bộ bộ đệm.]
  ),
  caption: [Ma trận chuyển trạng thái cốt lõi của Engine]
)

= Cấu trúc Dữ liệu Bộ đệm (Immutable Buffer & Rollback)

Engine không sử dụng mảng cố định có thể ghi đè như trong C++, mà sử dụng cấu trúc danh sách liên kết bất biến (Immutable Persistent List) để phục vụ quay lui.

== Lịch sử trạng thái và Cơ chế Backtracking (Undo / Rollback)

Để giải quyết bài toán người dùng muốn sửa phím hoặc bấm phím lặp để thoát dấu (ví dụ: gõ `s` thành `á`, gõ thêm `s` nữa thành `as`), Engine duy trì một ngăn xếp trạng thái (State Stack):

$ cal(H) = [q_n, q_(n-1), ..., q_0] $

Mỗi khi nhận một phím $sigma$, trạng thái mới $q_(n+1)$ được sinh ra và đẩy lên đầu ngăn xếp. Khi người dùng nhấn phím `Backspace`:

$ "pop": cal(H) arrow.r (q_n, cal(H)') $

Thuật toán quay lui chỉ việc lấy lại trạng thái $q_(n-1)$ trước đó và hoàn trả chuỗi tương ứng.

== Nhận diện Ranh giới Từ (Word Boundary Detection)

Một từ tiếng Việt được định nghĩa toán học là một chuỗi ký tự liên tục nằm giữa hai biên phân cách (Delimiters).

Tập ký tự ngắt từ được định nghĩa:
$ cal(D) = cal(W) union cal(P) union cal(S) $
- $cal(W)$: Tập ký tự khoảng trắng (`Space`, `Tab`, `Newline`).
- $cal(P)$: Tập dấu câu (`.`, `,`, `;`, `:`, `!`, `?`, `"`, `'`, `(`, `)`...).
- $cal(S)$: Tập toán tử và ký tự đặc biệt (`+`, `-`, `*`, `/`, `=`, `<`, `>`, `@`, `#`, `$`...).

Khi ký tự $sigma_k in cal(D)$, hàm chuyển trạng thái xác định biên kết thúc:
$ delta(q, (sigma_k, m)) = (q_"Idle", "Pass") $

= Cấu trúc Lưu trữ Từ điển và Bảng Tra cứu (Lookup Data Structures)

Để kiểm tra tính hợp lệ của âm tiết và hỗ trợ mở rộng từ viết tắt (Macro), F\# Core sử dụng hai cấu trúc dữ liệu chính:

== Mảng Tra cứu Trực tiếp (Direct Flat Array Lookup) cho Âm học
Đối với các kiểm tra ngữ âm học ở mức độ phần tử đơn (kiểm tra nguyên âm, phụ âm, bảng mã Unicode), F\# Core sử dụng các mảng băm phân giải trực tiếp theo mã ASCII/Codepoint:
- Độ phức tạp truy vấn: $O(1)$
- Chi phí bộ nhớ: Tối ưu trực tiếp trong bộ nhớ đệm L1/L2 của CPU, không cấp phát heap.

== Cây Tiền tố (Radix Tree / Trie) cho Bảng Gõ tắt và Từ điển Hợp lệ
Đối với tập từ điển âm tiết hợp lệ $cal(S)_"valid"$ (khoảng hơn 6.000 âm tiết tiếng Việt) hoặc bảng gõ tắt mở rộng:
- Độ phức tạp tìm kiếm: $O(k)$ với $k$ là độ dài chuỗi ký tự đang gõ ($k <= 7$).
- Được biểu diễn bằng cấu trúc dữ liệu bất biến dạng cây:

#align(center)[
  #rect(stroke: 0.5pt + rgb("888888"), radius: 4pt, inset: 8pt)[
    *Cấu trúc Cây Tiền tố (Trie Node)* \
    $"Node" = ("IsTerminal": "bool", "Children": "Map"<"char", "Node">)$
  ]
]
