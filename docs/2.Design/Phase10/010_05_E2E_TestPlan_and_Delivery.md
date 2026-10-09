# 010_05_E2E_TestPlan_and_Delivery — Kế Hoạch Kiểm Thử E2E Toàn Diện & Kịch Bản Đóng Gói Triển Khai macOS

- Mã tài liệu: 010_05_E2E_TestPlan_and_Delivery
- Giai đoạn: Phase 10 — Thiết kế kỹ thuật nền tảng macOS (InputMethodKit)
- Thuộc module: Toàn bộ giải pháp BambooMintKey trên macOS
- Trạng thái: Đã hoàn thiện thiết kế — Chờ phê duyệt

Tài liệu tham chiếu:
- Khảo sát khả thi và Kế hoạch: docs/2.Design/Phase10/010_01_Investigation.md
- Kiến trúc và C-ABI: docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md
- Thiết kế IMK Engine: docs/2.Design/Phase10/010_03_IMK_Engine_Design.md
- Giao diện UI.Mac: docs/2.Design/Phase10/010_04_UIMac_Design.md
- Theo dõi tiến độ: docs/4.Progress/007_MacOSProgressTracking.md

---

## 1. Mục Tiêu Kiểm Thử & Tiêu Chuẩn Chất Lượng

1. Thực thi nguyên tắc cốt lõi — Đầu tiên là đảm bảo gõ đúng được đã:
   Bộ gõ phải đảm bảo tính chuẩn xác tuyệt đối của ngữ pháp tiếng Việt theo chuẩn Unicode dựng sẵn (NFC), không nuốt phím, không nhân đôi ký tự và không làm gián đoạn việc soạn thảo.

2. Kiểm chứng chính sách hiển thị Preedit và Tắt gạch chân:
   - Xác nhận đường gạch chân được ẩn thành công trên các ứng dụng hỗ trợ, đem lại trải nghiệm chữ hiển thị tự nhiên, không che khuất dấu nặng tiếng Việt.
   - Xác nhận trên các ứng dụng không hỗ trợ ẩn gạch chân (tự vẽ đường gạch chân mặc định), văn bản vẫn được xử lý biến đổi dấu và chốt từ hoàn toàn chính xác, không phát sinh lỗi hiển thị hay ký tự lạ.

3. Kiểm chứng tương thích đa ứng dụng diện rộng:
   Đảm bảo bộ gõ hoạt động ổn định trên toàn bộ hệ sinh thái phần mềm macOS (trình duyệt, bộ công cụ lập trình, ứng dụng chat, terminal và bộ văn phòng).

4. Kiểm chứng quy trình cài đặt và gỡ cài đặt một chạm:
   Đảm bảo kịch bản đóng gói và triển khai tự động hoạt động mượt mà, người dùng chỉ cần một câu lệnh duy nhất là hoàn tất cài đặt vào hệ thống.

---

## 2. Ma Trận Kiểm Thử Ngữ Pháp Tiếng Việt (Language Correctness Matrix)

Toàn bộ các ca kiểm thử ngôn ngữ được thực hiện bằng phương pháp đối chiếu chuỗi phím vật lý đầu vào và chuỗi ký tự Unicode đầu ra tương ứng:

| Mã Kiểm Thử | Tên Kịch Bản | Chuỗi Phím Gõ | Kết Quả Kỳ Vọng | Tiêu Chuẩn Đạt |
|---|---|---|---|---|
| TC-LNG-01 | Telex Cơ Bản & Dấu Mũ | v-i-e-e-t-j | việt | Ghép đúng dấu mũ và dấu nặng, không sót phím |
| TC-LNG-02 | Dấu Móc & Dấu Trăng | d-u-w-o-w-n-g-f | đường | Ghép đúng dấu móc và dấu đ |
| TC-LNG-03 | Đặt Dấu Thanh Tự Do | t-o-a-n-s | toán | Vị trí dấu thanh di chuyển đúng theo chuẩn cấu hình |
| TC-LNG-04 | Lặp Phím Thoát Dấu (Undo) | a-s-s | as | Nhấn phím dấu lần 2 sẽ hoàn tác dấu về ký tự nguyên bản |
| TC-LNG-05 | Khôi Phục Từ Tiếng Anh | c-e-n-t-e-r | center | Tự động trả về từ tiếng Anh chuẩn, không biến thành từ lỗi |
| TC-LNG-06 | Xóa Lùi Preedit (Backspace) | t-h-u-y-e-e-n-f rồi Delete | rút lùi từng âm | Âm tiết rút lùi từng bước chính xác, không xóa đứt đoạn |
| TC-LNG-07 | Phím Tắt Hệ Thống (Cmd+Keys) | vieetj rồi Cmd+A | chốt việt và chọn hết | Không nuốt phím Cmd, không làm mất từ đang gõ dở |
| TC-LNG-08 | Chuyển Đổi V/E Tức Thì | vieetj chế độ V rồi đổi E | việt vieetj | Chế độ E nhường toàn bộ phím, icon Menu Bar cập nhật tức thì |

---

## 3. Ma Trận Kiểm Thử Hiển Thị Preedit & Chính Sách Tắt Gạch Chân

| Mã Kiểm Thử | Tên Kịch Bản | Thao Tác Thực Hiện | Kết Quả Kỳ Vọng | Tiêu Chuẩn Đạt |
|---|---|---|---|---|
| TC-PRE-01 | Tắt gạch chân trên Apple Cocoa | Mở TextEdit hoặc Safari, gõ từ đang | Chữ hiển thị sạch sẽ, không có đường gạch chân | PASS nếu dấu nặng không bị che khuất |
| TC-PRE-02 | Ứng xử với app tự vẽ gạch chân | Mở terminal có renderer riêng, gõ đang | Chấp nhận hiển thị mặc định của app, gõ đúng 100% | PASS nếu không xuất hiện ký tự rác |
| TC-PRE-03 | Chuyển cửa sổ khi đang gõ dở | Đang gõ dở ở Safari, click sang Word | Từ dở được chốt an toàn ở Safari, Word mở đệm rỗng | PASS nếu không mất từ, không lẫn ký tự |
| TC-PRE-04 | Chốt từ bằng phím ngắt | Gõ xong từ đường, nhấn Space | Chuỗi được chốt vào văn bản, vùng đánh dấu biến mất | PASS nếu chốt từ mượt mà, con trỏ theo sát |

---

## 4. Ma Trận Tương Thích Đa Ứng Dụng Trên macOS (Compatibility Matrix)

Bộ gõ phải vượt qua các ca kiểm thử trên toàn bộ danh mục ứng dụng đại diện:

### 4.1. Nhóm Ứng Dụng Apple Native Cocoa
- Ứng dụng đại diện: Safari, Apple Notes, TextEdit, Pages, Mail.
- Giao thức nhập liệu: Cocoa Text System (NSTextInputClient).
- Trọng tâm đánh giá:
  - Chữ hiển thị mượt mà không có đường gạch chân.
  - Bôi đen hoặc nhấp chuột không làm mất dấu tiếng Việt.
  - Nhấn Space hoặc Enter chốt từ tức thì.

### 4.2. Nhóm Môi Trường Lập Trình (IDEs)
- Ứng dụng đại diện: Xcode, Visual Studio Code, JetBrains IDEs (IntelliJ, Rider).
- Giao thức nhập liệu: Cocoa, Electron và JVM Input Framework.
- Trọng tâm đánh giá:
  - Không xung đột với tính năng gợi ý mã (Autocomplete, IntelliSense).
  - Không làm phá vỡ thụt lề tự động khi nhấn Enter chốt từ.
  - Các tổ hợp phím tắt lập trình (Cmd+Slash, Cmd+D, Cmd+F) hoạt động bình thường.

### 4.3. Nhóm Ứng Dụng Nền Chromium & Electron
- Ứng dụng đại diện: Google Chrome, Slack, Discord, Microsoft Teams.
- Giao thức nhập liệu: Chromium IME Layer.
- Trọng tâm đánh giá:
  - Không xảy ra hiện tượng nhân đôi ký tự khi gõ nhanh.
  - Nhập bình luận hoặc tin nhắn trong khung chat mượt mà, không bị trễ.

### 4.4. Nhóm Dòng Lệnh (Terminal)
- Ứng dụng đại diện: macOS Terminal.app, iTerm2.
- Giao thức nhập liệu: Direct Pty và Command Line Input.
- Trọng tâm đánh giá:
  - Xuất đúng mã UTF-8 dựng sẵn vào dòng lệnh của zsh hoặc bash.
  - Không làm hỏng các chuỗi escape sequence khi gõ lệnh.

### 4.5. Nhóm Ứng Dụng Văn Phòng Đa Nền Tảng
- Ứng dụng đại diện: Microsoft Word, Microsoft Excel trên macOS.
- Giao thức nhập liệu: Microsoft Mac Office Framework.
- Trọng tâm đánh giá:
  - Con trỏ văn bản không bị lệch vị trí khi đang ráp từ tiếng Việt.
  - Định dạng văn bản (đậm, nghiêng, màu sắc) giữ nguyên vẹn sau khi chốt từ.

---

## 5. Kịch Bản Đóng Gói Bundle & Script Triển Khai Tự Động

### 5.1. Kịch bản cài đặt tự động (scripts/install_macos.sh)
Kịch bản shell cung cấp trải nghiệm cài đặt một chạm (One-Command Deployment):
1. Kiểm tra môi trường:
   - Kiểm tra xem máy đã cài đặt .NET 10 SDK chưa.
   - Kiểm tra xem công cụ biên dịch Swift và Xcode Command Line Tools đã sẵn sàng chưa.
2. Biên dịch các thành phần:
   - Biên dịch thư viện C-ABI NativeAOT thành file libBambooMintKeyCore.dylib.
   - Biên dịch ứng dụng nền Swift BambooMintKey.Mac.IMK.
   - Biên dịch ứng dụng giao diện Avalonia BambooMintKey.UI.Mac.
3. Cấu trúc gói Bundle (BambooMintKey.app):
   - Tạo cấu trúc thư mục chuẩn tại ~/Library/Input Methods/BambooMintKey.app.
   - Sao chép tệp thực thi chính, thư viện dylib, tệp tài nguyên icon và tệp Info.plist.
   - Phân quyền thực thi an toàn cho toàn bộ các tệp nhị phân trong bundle.
4. Đăng ký dịch vụ với hệ thống:
   - Kích hoạt đăng ký với hệ điều hành macOS để bộ gõ xuất hiện ngay trong danh sách Nguồn nhập liệu của System Settings.
   - Khởi động ứng dụng Cài đặt để người dùng có thể tùy chỉnh ngay nếu muốn.

### 5.2. Kịch bản gỡ cài đặt sạch sẽ (scripts/uninstall_macos.sh)
1. Dừng các tiến trình đang chạy của bộ gõ và cửa sổ Cài đặt.
2. Xóa bỏ hoàn toàn gói bundle BambooMintKey.app khỏi thư mục ~/Library/Input Methods/.
3. Hỏi người dùng có muốn xóa bỏ toàn bộ tệp cấu hình tại ~/Library/Application Support/BambooMintKey/ hay không.
4. Yêu cầu hệ thống làm mới danh sách phương thức nhập liệu, đưa hệ thống trở về trạng thái sạch sẽ ban đầu.
