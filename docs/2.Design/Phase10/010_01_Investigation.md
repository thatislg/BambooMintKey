<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 001_Investigation — Khảo Sát Khả Thi & Kế Hoạch Triển Khai Nền Tảng macOS (InputMethodKit)

**Mã tài liệu:** `001_Investigation`  
**Giai đoạn:** Phase 10 — Khảo sát & Kế hoạch phát triển nền tảng macOS  
**Thuộc module:** Toàn bộ giải pháp BambooMintKey  
**Trạng thái:** 📋 Kế hoạch đã hoàn thiện — Chờ phê duyệt  
**Mục tiêu:** Khảo sát môi trường, phân tích tính khả thi kiến trúc và xác lập lộ trình triển khai chi tiết cho bộ gõ BambooMintKey trên hệ điều hành macOS.

---

## 1. Bối Cảnh & Mục Tiêu Cốt Lõi

### 1.1. Hiện trạng dự án
BambooMintKey đã hoàn thiện và hoạt động ổn định trên hai nền tảng máy tính để bàn:
- **Windows (Phase 1–6):** Tích hợp sâu thông qua Windows Text Services Framework (TSF) với cầu nối NativeAOT C# và giao diện cấu hình Avalonia UI.
- **Linux (Phase 7–9):** Tích hợp vào hệ sinh thái Fcitx5 qua addon C++ giao tiếp với lõi F# được biên dịch NativeAOT (`libBambooMintKeyCore.so`), giao diện cấu hình Avalonia UI độc lập kết nối D-Bus.

### 1.2. Mục tiêu đưa lên macOS (Phase 10)
Mang toàn bộ trải nghiệm gõ tiếng Việt Telex/VNI chuẩn xác, nhanh và thông minh của BambooMintKey sang hệ điều hành macOS, hỗ trợ đầy đủ cả hai kiến trúc vi xử lý Apple Silicon (`arm64`) và Intel (`x86_64`).

### 1.3. Bốn nguyên tắc kiến trúc bất biến
1. **Bảo toàn tuyệt đối mã nguồn Windows và Linux (Zero Regression):**
   - Giữ nguyên 100% hiện trạng các dự án của Windows (`BambooMintKey.NativeBridge`, `BambooMintKey.UI`, `BambooMintKey.DevHarness`) và Linux (`BambooMintKey.Fcitx5`, `BambooMintKey.UI.Linux`).
   - Tận dụng lõi F# xử lý âm tiết (`BambooMintKey.Core`) mà không sửa đổi bất kỳ logic tiếng Việt nào đã được kiểm chứng. Mọi thành phần đặc thù của macOS sẽ được tạo mới hoàn toàn độc lập.
2. **Vẫn sử dụng cơ chế Preedit (Inline Composition / Marked Text):**
   - Vận hành theo đúng chuẩn Input Method Service chính thống của Apple Text System.
   - Quản lý từ đang soạn thảo ngay tại con trỏ văn bản qua vùng đệm Preedit của hệ thống.
   - Tuyệt đối nói không với cơ chế đánh chặn phím toàn cục và gửi Backspace giả lập.
3. **Tắt gạch chân tối đa ở các ứng dụng có thể hỗ trợ:**
   - Cấu hình thuộc tính hiển thị để ẩn đường gạch chân (Stealth Mode) tại các ứng dụng cho phép, đem lại cảm giác gõ tự nhiên, sạch sẽ và không gây rối mắt hoặc che khuất dấu nặng (`.`) tiếng Việt.
   - Đối với các ứng dụng tự vẽ đường gạch chân mặc định hoặc không hỗ trợ tùy biến thuộc tính, giữ nguyên hiển thị mặc định của ứng dụng; tuyệt đối không can thiệp hay hack sâu vào tiến trình bên ngoài.
4. **Ưu tiên số 1 tuyệt đối: Đảm bảo gõ đúng và ổn định:**
   - Tính đúng đắn của văn bản (đúng dấu, đúng âm tiết, điều hướng phím ngắt chuẩn, không nuốt phím, không lặp ký tự) là tiêu chuẩn nghiệm thu cao nhất, luôn đứng trước mọi yếu tố hình thức hiển thị.

---

## 2. Khảo Sát Tính Khả Thi Kiến Trúc macOS

### 2.1. Phân tích so sánh: InputMethodKit (IMK) vs. Cơ chế chặn phím toàn cục (Global Hook)

Trên macOS, các phần mềm gõ tiếng Việt truyền thống thường lựa chọn giữa hai trường phái:

#### Trường phái 1: Đánh chặn sự kiện phím toàn cục (`CGEventTap`)
- **Cách thức hoạt động:** Tiến trình bộ gõ chạy như một ứng dụng nền thông thường, đăng ký lắng nghe bàn phím vật lý của toàn hệ thống thông qua hàm hệ thống bắt sự kiện.
- **Nhược điểm nghiêm trọng:**
  - Bắt buộc người dùng phải cấp quyền Trợ năng (Accessibility) trong System Settings, tiềm ẩn cảnh báo bảo mật của macOS.
  - Phải giả lập gửi chuỗi phím xóa lùi (Backspace) ảo vào ứng dụng để xóa ký tự cũ rồi gõ ký tự có dấu mới. Đây là nguyên nhân gốc rễ gây ra lỗi nuốt phím, mất chữ, nhân đôi ký tự trên các trình duyệt, ứng dụng Electron và game.

#### Trường phái 2: Khung dịch vụ nhập liệu chính thức (InputMethodKit - IMK)
- **Cách thức hoạt động:** Đăng ký trực tiếp với hệ điều hành dưới dạng một Input Method Component chuẩn bản địa tại thư mục quản lý phương thức nhập liệu của người dùng. Hệ điều hành tự động định tuyến sự kiện gõ phím đến bộ gõ trước khi hiển thị lên văn bản.
- **Ưu điểm vượt trội:**
  - Hoạt động tự nhiên, không đòi hỏi quyền Accessibility nhạy cảm.
  - Không bao giờ phải gửi phím Backspace giả lập; hệ thống tự quản lý vùng đệm soạn thảo (Marked Text).
  - Tương đồng hoàn toàn 1:1 với mô hình kiến trúc TSF trên Windows và Fcitx5 trên Linux.

**Kết luận:** BambooMintKey trên macOS bắt buộc phải triển khai theo trường phái **InputMethodKit (IMK)** thuần túy.

---

### 2.2. Khảo sát cơ chế Hiển thị Preedit & Tắt gạch chân trên macOS

- **Cơ chế Marked Text:** Khi người dùng đang ráp một âm tiết tiếng Việt dở dang (ví dụ đang gõ liên tiếp các ký tự để tạo dấu mũ hay dấu móc), IMK yêu cầu ứng dụng khách hiển thị một đoạn chuỗi đánh dấu tạm thời tại con trỏ văn bản.
- **Quy tắc gạch chân:** Theo mặc định của hệ thống văn bản Cocoa, chuỗi đánh dấu sẽ được hệ điều hành gạch chân một nét đơn mỏng.
- **Khả năng tắt gạch chân:**
  - Khung giao tiếp văn bản của macOS cho phép truyền vào chuỗi có định dạng thuộc tính văn bản. Bằng cách gán chỉ thị kiểu gạch chân là không có đường kẻ, các ứng dụng tuân thủ chuẩn văn bản của Apple (như Safari, Pages, Notes, TextEdit) sẽ hiển thị chữ đang gõ một cách tự nhiên, trơn tru, không có đường gạch chân bên dưới.
  - Một số ứng dụng có engine dựng hình văn bản tự tạo (như một số trình giả lập terminal hoặc ứng dụng đồ họa đặc thù) có thể bỏ qua chỉ thị này và tự vẽ gạch chân hoặc đổi màu nền riêng của chúng.
- **Chính sách tiếp cận:** Áp dụng chỉ thị tắt gạch chân tối đa ở mọi nơi hệ thống cho phép. Nếu ứng dụng đích không hỗ trợ tắt gạch chân, chấp nhận giao diện mặc định của ứng dụng đó mà không sử dụng bất kỳ biện pháp can thiệp thô bạo nào, giữ vững tôn chỉ ưu tiên gõ đúng.

---

### 2.3. Khảo sát C-ABI NativeAOT trên môi trường macOS

- Dự án `BambooMintKey.Core.Native` hiện tại đã được cấu hình NativeAOT trong .NET 10.
- Trên Linux, dự án xuất ra file thư viện chia sẻ định dạng ELF (`.so`).
- Trên macOS, trình biên dịch NativeAOT của .NET hỗ trợ xuất ra thư viện động định dạng Mach-O (`.dylib`).
- Nhờ việc giao tiếp hoàn toàn qua chuẩn C-ABI (con trỏ bộ nhớ, các kiểu số nguyên và mảng byte UTF-8), tầng ngôn ngữ phía macOS (Swift hoặc Objective-C) có thể gọi trực tiếp vào lõi xử lý F# với chi phí chuyển giao ngữ cảnh gần như bằng 0 (độ trễ dưới 0.1 mili-giây), hoàn toàn không cần khởi tạo managed runtime trong tiến trình gõ.
- Quản lý bộ nhớ theo cơ chế Context Handle đảm bảo mỗi ứng dụng mở trên màn hình có một không gian đệm độc lập, không bị lẫn lộn ký tự giữa các cửa sổ.

---

### 2.4. Khảo sát Giao diện cấu hình & Đồng bộ trạng thái trên macOS

- **Giao diện Avalonia UI:** Dự án `BambooMintKey.UI.Linux` sử dụng Avalonia UI, một framework giao diện người dùng đa nền tảng chạy rất tốt trên macOS. Phương án tối ưu là nhân bản thành `BambooMintKey.UI.Mac` để giữ nguyên giao diện hiện đại, trực quan nhưng cách ly hoàn toàn hệ thống.
- **Loại bỏ phụ thuộc D-Bus:** Trên Linux, việc đồng bộ giữa UI và Addon dựa vào daemon D-Bus của desktop session. Trên macOS, hệ thống không có D-Bus mặc định.
- **Giải pháp đồng bộ trên macOS:**
  1. *Lưu trữ bền vững:* Sử dụng file định dạng JSON chuẩn tại thư mục Application Support của người dùng (`~/Library/Application Support/BambooMintKey/config.json`).
  2. *Lắng nghe thay đổi:* Thành phần IMK Server sử dụng cơ chế theo dõi sự kiện file của macOS để tự động nạp lại cấu hình ngay khi file JSON được ghi đè, không cần khởi động lại.
  3. *Đồng bộ chế độ gõ (V/E):* Tận dụng trung tâm thông báo phân tán nội bộ của macOS hoặc cổng kết nối miền cục bộ nhẹ để truyền trạng thái chuyển đổi giữa biểu tượng Menu Bar, Service bộ gõ và cửa sổ Cài đặt.

---

## 3. Kế Hoạch Triển Khai Chi Tiết (Milestones & Roadmap)

Lộ trình thực hiện được chia thành 7 cột mốc tuần tự, tuân thủ nghiêm ngặt nguyên tắc: hoàn thành và nghiệm thu từng bước trước khi chuyển sang bước tiếp theo.

```
M1: Đặc Tả Kiến Trúc Bằng Lời
   │
   ▼
M2: Biên Dịch C-ABI NativeAOT macOS (.dylib)
   │
   ▼
M3: Xây Dựng Bộ Gõ IMK Server & Controller
   │
   ▼
M4: Xây Dựng Giao Diện Cài Đặt (UI.Mac)
   │
   ▼
M5: Đồng Bộ Trạng Thái V/E & Menu Bar Icon
   │
   ▼
M6: Kiểm Thử E2E Tính Đúng Đắn & Tương Thích
   │
   ▼
M7: Đóng Gói Bundle & Script Cài Đặt Tự Động
```

---

### Cột mốc 1 (M1): Hoàn Thiện Hồ Sơ Thiết Kế Kiến Trúc Bằng Lời
- **Nội dung công việc:**
  - Soạn thảo tài liệu đặc tả cấu trúc dữ liệu, luồng điều phối sự kiện văn bản và ranh giới trách nhiệm giữa các module mà không sử dụng code mẫu.
  - Phân tích chi tiết chu trình sống của đối tượng điều khiển nhập liệu từ khi một ô nhập văn bản được chọn đến khi mất tiêu điểm.
  - Đặc tả chi tiết cơ chế định dạng chuỗi đánh dấu để tắt gạch chân tối đa và luồng xử lý phím ngắt để chốt văn bản.
  - Thiết kế quy ước đường dẫn thư mục, cơ chế phân định file cấu hình và giao thức truyền thông điệp chuyển đổi V/E trên macOS.
- **Tiêu chuẩn nghiệm thu (DoD):** Hồ sơ thiết kế kiến trúc bằng lời được hoàn thiện, nhất quán với định hướng dự án và được phê duyệt.

---

### Cột mốc 2 (M2): Biên Dịch Thư Viện Lõi C-ABI Cho macOS
- **Nội dung công việc:**
  - Bổ sung cấu hình điều kiện trong dự án NativeAOT để hỗ trợ xuất file thư viện Mach-O (`libBambooMintKeyCore.dylib`) khi biên dịch trên hệ điều hành macOS.
  - Đảm bảo cấu hình hỗ trợ xuất nhị phân cho cả hai kiến trúc vi xử lý của Apple (`arm64` và `x86_64`).
  - Viết chương trình kiểm tra nhanh bằng dòng lệnh trên macOS để xác nhận các hàm C-ABI: tạo context, giải phóng context, xử lý ký tự, lấy văn bản preedit và lấy văn bản commit hoạt động chính xác.
  - Xác nhận việc biên dịch trên macOS không tạo ra bất kỳ thay đổi ngoài ý muốn nào đối với quy trình build trên Windows và Linux.
- **Tiêu chuẩn nghiệm thu (DoD):** File thư viện `libBambooMintKeyCore.dylib` được sinh ra hợp lệ, thực thi chính xác logic Telex F# trên macOS với độ trễ dưới 0.1ms.

---

### Cột mốc 3 (M3): Xây Dựng Bộ Gõ Bản Địa macOS (IMK Service)
- **Nội dung công việc:**
  - Khởi tạo dự án ứng dụng nền IMK độc lập dành cho macOS.
  - Thiết lập thành phần Server đăng ký dịch vụ nhập liệu với hệ điều hành và thành phần Controller quản lý từng phiên gõ của các ứng dụng đích.
  - Tích hợp gọi C-ABI vào thư viện lõi để nhận diện phím bấm và nhận kết quả hành động tương ứng.
  - Cài đặt cơ chế cập nhật Marked Text với thuộc tính ẩn gạch chân tối đa, và chốt văn bản dứt khoát khi kết thúc từ.
  - Xử lý các phím bổ trợ hệ thống (`Command`, `Control`) để nhường quyền cho các phím tắt hệ thống (sao chép, dán, chọn tất cả, lưu file) mà không làm loạn từ đang gõ dở.
- **Tiêu chuẩn nghiệm thu (DoD):** Bộ gõ xuất hiện trong danh sách Nguồn nhập của macOS System Settings, gõ được tiếng Việt có dấu đúng chuẩn trong các ứng dụng cơ bản (TextEdit, Safari, Notes).

---

### Cột mốc 4 (M4): Xây Dựng Giao Diện Cài Đặt Độc Lập (`BambooMintKey.UI.Mac`)
- **Nội dung công việc:**
  - Khởi tạo dự án giao diện cấu hình độc lập trên nền tảng Avalonia UI bằng cách kế thừa thiết kế từ phiên bản Linux.
  - Loại bỏ hoàn toàn mã giao tiếp D-Bus, xây dựng lớp đọc ghi cấu hình trực tiếp vào đường dẫn Application Support chuẩn của macOS.
  - Cài đặt cơ chế đảm bảo chỉ một cửa sổ Cài đặt duy nhất được phép hiển thị tại một thời điểm (Single Instance).
  - Giữ trọn vẹn phong cách thiết kế hiện đại, đầy đủ các tùy chọn: chuyển đổi Telex/VNI, kiểu đặt dấu mới/cũ, khôi phục từ tiếng Anh, lặp phím xóa dấu.
- **Tiêu chuẩn nghiệm thu (DoD):** Ứng dụng Cài đặt khởi chạy mượt mà trên macOS, lưu cấu hình thành công vào đĩa và không làm ảnh hưởng đến mã nguồn của các nền tảng khác.

---

### Cột mốc 5 (M5): Đồng Bộ Trạng Thái V/E & Tích Hợp Menu Bar
- **Nội dung công việc:**
  - Bổ sung biểu tượng trạng thái hiển thị chế độ gõ `V` hoặc `E` trên thanh tác vụ Menu Bar góc trên bên phải của macOS.
  - Xây dựng menu ngữ cảnh khi nhấn vào biểu tượng: cho phép đổi nhanh chế độ gõ, mở cửa sổ Cài đặt, và thoát bộ gõ.
  - Cài đặt cơ chế lắng nghe thay đổi file cấu hình thời gian thực trong tiến trình bộ gõ để tự động cập nhật thiết lập ngay khi người dùng bấm Lưu trên giao diện Cài đặt.
  - Thiết lập kênh thông báo hai chiều để khi người dùng đổi chế độ bằng phím tắt hoặc qua Menu Bar, trạng thái được cập nhật đồng bộ ngay lập tức trên toàn hệ thống.
- **Tiêu chuẩn nghiệm thu (DoD):** Chuyển đổi V/E diễn ra tức thì, biểu tượng Menu Bar cập nhật trạng thái đồng bộ, các thay đổi cài đặt có hiệu lực ngay mà không cần khởi động lại.

---

### Cột mốc 6 (M6): Kiểm Thử Toàn Diện Tính Đúng Đắn & Tương Thích (E2E)
- **Nội dung công việc:**
  - Thực hiện kiểm chứng tính đúng đắn ngữ pháp tiếng Việt theo tiêu chuẩn cao nhất:
    - Quy tắc Telex cơ bản: dấu mũ (`aa`, `ee`, `oo`), dấu móc (`ow`, `uw`), dấu trăng (`aw`), chữ đ (`dd`).
    - Quy tắc đặt dấu thanh động và bỏ dấu tự do.
    - Cơ chế lặp phím hoàn tác dấu thanh (`as` -> `s` -> `as`).
    - Cơ chế khôi phục từ tiếng Anh on-the-fly (`center`, `post`, `internet`).
    - Thao tác xóa lùi (Backspace) từng bước trong âm tiết đang soạn thảo.
  - Kiểm tra hiển thị Preedit: xác nhận đường gạch chân được ẩn thành công trên các ứng dụng hỗ trợ; xác nhận trên các ứng dụng không hỗ trợ ẩn gạch chân thì việc nhập liệu vẫn hoàn toàn chính xác, ổn định.
  - Chạy ma trận kiểm thử tương thích đa ứng dụng trên macOS:
    - *Ứng dụng Apple bản địa:* Safari, Apple Notes, TextEdit, Pages, Mail.
    - *Công cụ phát triển:* Xcode, Visual Studio Code, JetBrains IDEs.
    - *Ứng dụng nền Chromium/Electron:* Google Chrome, Slack, Discord, Microsoft Teams.
    - *Dòng lệnh:* macOS Terminal, iTerm2.
    - *Văn phòng:* Microsoft Word, Excel trên macOS.
- **Tiêu chuẩn nghiệm thu (DoD):** 100% các ca kiểm thử ngữ pháp đạt kết quả chính xác; không phát sinh lỗi nuốt phím, không nhân đôi chữ, không giật màn hình trên tất cả các nhóm ứng dụng mục tiêu.

---

### Cột mốc 7 (M7): Đóng Gói Ứng Dụng & Kịch Bản Cài Đặt Tự Động
- **Nội dung công việc:**
  - Xây dựng cấu trúc thư mục Bundle hoàn chỉnh (`BambooMintKey.app`) tuân thủ nghiêm ngặt tiêu chuẩn ứng dụng Input Method của macOS, chứa đầy đủ tệp thực thi, thư viện C-ABI, tài nguyên icon và file mô tả `Info.plist`.
  - Soạn thảo kịch bản shell tự động hóa (`scripts/install_macos.sh`): tự động biên dịch, tổ chức bundle, triển khai vào thư mục Nguồn nhập liệu của người dùng (`~/Library/Input Methods/`), và kích hoạt đăng ký với hệ điều hành.
  - Cung cấp kịch bản gỡ cài đặt sạch sẽ (`scripts/uninstall_macos.sh`): dừng tiến trình, dọn dẹp bundle và tệp cấu hình, trả lại trạng thái nguyên bản cho máy người dùng.
- **Tiêu chuẩn nghiệm thu (DoD):** Người dùng có thể cài đặt hoàn tất bộ gõ chỉ bằng một câu lệnh trên Terminal và bắt đầu gõ tiếng Việt ngay lập tức mà không cần thao tác cấu hình phức tạp.

---

## 4. Quản Lý Rủi Ro Kỹ Thuật

| Rủi ro tiềm ẩn | Mức độ | Biện pháp phòng ngừa & Xử lý |
|---|:---:|---|
| **Ứng dụng không tôn trọng thuộc tính tắt gạch chân** | Thấp | Chấp nhận hiển thị mặc định của ứng dụng đó; không cố gắng can thiệp sâu vào tiến trình ngoài, ưu tiên tuyệt đối tính chính xác khi gõ. |
| **Xung đột phím tắt hệ thống (Command + Keys)** | Trung bình | Kiểm tra cờ phím bổ trợ `Command` / `Control` ngay tại tầng đón sự kiện; nếu có, lập tức chốt từ đang gõ dở và nhường quyền hoàn toàn cho hệ thống. |
| **Xung đột cổng giao tiếp hoặc quyền hạn IPC** | Thấp | Không sử dụng các daemon ngoài như D-Bus; sử dụng kênh thông báo phân tán bản địa và tệp cấu hình chuẩn trong thư mục người dùng. |
| **Hồi quy mã nguồn Windows hoặc Linux** | Rất thấp | Toàn bộ mã nguồn mới cho macOS được đặt trong các thư mục và dự án tách biệt; quy trình kiểm thử đối chiếu đảm bảo `git status` trên các module cũ hoàn toàn sạch. |

---

## 5. Kế Hoạch Bước Tiếp Theo

Sau khi bản Kế hoạch tổng thể này được rà soát và thông qua, công việc tiếp theo sẽ là bắt đầu **Cột mốc 1 (M1): Soạn thảo tài liệu đặc tả thiết kế kiến trúc bằng lời**, đảm bảo không chứa bất kỳ code mẫu nào theo đúng quy chuẩn đã thống nhất.
