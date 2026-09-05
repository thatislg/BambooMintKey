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
  #text(size: 18pt, weight: "bold")[VÙNG 4: KIẾN TRÚC GIAO TIẾP HỆ THỐNG & C-ABI INTEROP] \
  #v(0.2cm)
  #text(size: 12pt, style: "italic")[Hợp đồng nhị phân tầng C-ABI giữa `F#` NativeAOT Core và Fcitx5 C++ Adapter]
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

= Tổng quan Kiến trúc Đa tầng (Tiered System Architecture)

Hệ thống BambooMintKey được tách biệt thành hai phân vùng độc lập qua ranh giới nhị phân (Binary Boundary):

1. *Core Engine (`F#` NativeAOT):*
   - Chịu trách nhiệm toàn bộ logic xử lý ngôn ngữ, DFA Parser, Phonotactics và bộ đệm trạng thái bất biến.
   - Được biên dịch tĩnh thành thư viện liên kết động (`libbmk_core.so` trên Linux hoặc `bmk_core.dll` trên Windows) mà không cần cài đặt .NET Runtime.
   - Không chứa bất kỳ phụ thuộc nào liên quan đến UI hay khung nhập liệu cụ thể.

2. *Host Adapter (Fcitx5 C++ Addon):*
   - Cài đặt giao diện `fcitx::InputMethodEngineV2` và `fcitx::Factory`.
   - Đón nhận các sự kiện phím vật lý từ X11/Wayland thông qua Fcitx5 Event Loop.
   - Chuyển tiếp mã phím qua ranh giới C-ABI và nhận lại các chỉ thị hành động (Commit, Replace, Forward).

#align(center)[
  #rect(stroke: 0.5pt + rgb("888888"), radius: 4pt, inset: 10pt)[
    *Sơ đồ Phân tầng Dữ liệu* \
    `[X11 / Wayland KeyEvent]` $arrow.r$ `[Fcitx5 C++ Addon]` $arrow.r$ *(C-ABI Boundary)* $arrow.r$ `[F# NativeAOT Core]` \
    `[Client App / Text Box]` $arrow.l$ `[Commit / Forward]` $arrow.l$ *(C-ABI Boundary)* $arrow.l$ `[Engine Action Result]`
  ]
]

= Đặc tả Hợp đồng C-ABI (C-ABI Contract Definition)

Để đảm bảo khả năng tương thích nhị phân tuyệt đối, dữ liệu trao đổi qua ranh giới FFI (Foreign Function Interface) chỉ sử dụng các kiểu dữ liệu nguyên thủy (Primitives) và cấu trúc bộ nhớ phẳng có thứ tự tường minh (`Sequential Layout`).

== Cấu trúc Sự kiện Đầu vào (Native Key Event)

Mỗi phím bấm gửi từ C++ sang `F#` Core được đóng gói trong một struct 64-bit có cấu trúc cố định:

$ "NativeKeyEvent" = (k_"code", c_"unicode", m_"flags") $

Trong đó:
- $k_"code" in "uint32"$: Mã phím ảo của hệ thống (X11 Keysym hoặc Linux Evdev Scancode).
- $c_"unicode" in "uint32"$: Mã UTF-32 của ký tự sinh ra (hoặc $0$ nếu là phím chức năng).
- $m_"flags" in "uint32"$: Mặt nạ bit chứa trạng thái của Shift, Ctrl, Alt, CapsLock, Super.

== Cấu trúc Chỉ thị Phản hồi (Native Output Action)

Sau khi xử lý, `F#` Core trả về một bản ghi chỉ thị hành động gồm 4 trường dữ liệu:

#figure(
  table(
    columns: (1.5fr, 1.5fr, 3.5fr),
    inset: 6pt,
    align: horizon,
    stroke: 0.5pt + rgb("aaaaaa"),
    [*Tên trường*], [*Kiểu dữ liệu*], [*Mô tả nghiệp vụ*],
    [`action_type`], [`int32`], [Mã định danh loại hành động: 0 (Pass), 1 (Consume), 2 (Commit), 3 (Replace).],
    [`backspaces`], [`int32`], [Số lượng phím Backspace mà Host cần mô phỏng trước khi gửi chuỗi mới.],
    [`output_text`], [`const char*`], [Con trỏ chuỗi byte UTF-8 kết quả (kết thúc bằng ký tự null).],
    [`text_length`], [`int32`], [Độ dài chuỗi byte UTF-8 (không tính null terminator).]
  ),
  caption: [Bố cục bộ nhớ của Native Output Action]
)

= Quản lý Bộ nhớ Phi Quản lý & Vòng đời Thể hiện (Memory & Instance Lifecycle)

Kiến trúc `F#` NativeAOT sử dụng mô hình Handle Opaque Pointer để quản lý trạng thái đa thể hiện (Multi-instance), cho phép mỗi cửa sổ ứng dụng hoặc ngữ cảnh nhập liệu sở hữu một máy trạng thái độc lập.

== Bảng Hàm Xuất Bản Chuẩn C (Exported C Functions)

Bộ thư viện lõi xuất bản 4 hàm ngoại vi chính thức qua cơ chế `[<UnmanagedCallersOnly>]`:

1. *Khởi tạo Thể hiện (Allocate):*
   $ "bmk_engine_create"(): "nativeint" $
   Cấp phát một phiên bản `EngineState` mới trên bộ nhớ heap và trả về con trỏ mờ (`IntPtr` / `void*`).

2. *Hủy bỏ Thể hiện (Free):*
   $ "bmk_engine_destroy"("handle": "nativeint"): "void" $
   Giải phóng bộ đệm và thu hồi tài nguyên gắn liền với handle đã cấp phát.

3. *Xử lý Sự kiện Phím (Process):*
   $ "bmk_engine_process"("handle": "nativeint", "event": "NativeKeyEvent"*, "result": "NativeActionResult"*): "int32" $
   Nạp sự kiện phím vào máy trạng thái, ghi đè trực tiếp kết quả vào con trỏ struct do phía C++ cung cấp.

4. *Thiết lập Cấu hình (Configure):*
   $ "bmk_engine_set_option"("handle": "nativeint", "option_id": "int32", "val": "int32"): "void" $
   Thay đổi kiểu gõ (Telex/VNI), kiểu đặt dấu (Mới/Cũ), hoặc kích hoạt kiểm tra chính tả khi đang chạy.

== Nguyên tắc An toàn Bộ nhớ (Memory Safety Rules)

Để triệt tiêu hiện tượng rò rỉ bộ nhớ (Memory Leak) và con trỏ treo (Dangling Pointer):
- *Quy tắc cấp phát:* Phía C++ Adapter luôn là bên chủ động cấp phát bộ đệm tiếp nhận kết quả (`NativeActionResult`) trên ngăn xếp (Stack) trước khi truyền địa chỉ con trỏ sang `F#` Core.
- *Quy tắc chuỗi ký tự:* Con trỏ `output_text` trỏ tới vùng đệm nội bộ bất biến của Engine. Phía C++ Adapter phải sao chép dữ liệu ngay lập tức vào chuỗi `std::string` và không bao giờ được gọi lệnh giải phóng (`free` / `delete`) đối với con trỏ này.
