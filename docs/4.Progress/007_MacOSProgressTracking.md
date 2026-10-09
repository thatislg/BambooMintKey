<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for macOS
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# BambooMintKey macOS (InputMethodKit) Progress Tracking

**Ngày khởi tạo:** 2026-10-09  
**Giai đoạn:** Phase 10 — Khảo sát & Triển khai nền tảng macOS (InputMethodKit)  
**Trạng thái chung:** 🛠️ Đã hoàn thành 100% Milestone 1 (Thiết kế kiến trúc bằng lời), 100% Milestone 2 (Biên dịch C-ABI NativeAOT macOS), 100% Milestone 3 (Bộ gõ IMK Engine Service) và 100% Milestone 4 (Giao diện cài đặt UI.Mac), chuẩn bị khởi động Milestone 5 (M5: Đồng bộ trạng thái V/E & Menu Bar).  
**Tài liệu tham chiếu:**
- Khảo sát khả thi & Kế hoạch: [010_01_Investigation.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_01_Investigation.md)
- Thiết kế Kiến trúc & C-ABI: [010_02_Architecture_and_CABI_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md)
- Thiết kế IMK Engine Service: [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md)
- Thiết kế Giao diện UI.Mac: [010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md)
- Kế hoạch E2E Test & Delivery: [010_05_E2E_TestPlan_and_Delivery.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.md) (bản văn bản thuần: [010_05_E2E_TestPlan_and_Delivery.txt](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.txt))
- Báo cáo Xử lý Lỗi M3: [008_MacOS_M3_Verification_And_Fixes_Report.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/008_MacOS_M3_Verification_And_Fixes_Report.md)
- Issue 016 (Đã giải quyết): [016_MacOS_IMK_NotAppearing_In_InputSources.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/3.Issue/016_MacOS_IMK_NotAppearing_In_InputSources.md)

---

## 1. Bốn Nguyên Tắc Bất Biến Của Phase 10

1. **Bảo toàn 100% mã nguồn Windows và Linux (Zero Regression):** Tuyệt đối không chỉnh sửa bất kỳ logic nào trong các module của Windows (`BambooMintKey.NativeBridge`, `BambooMintKey.UI`, `BambooMintKey.DevHarness`) và Linux (`BambooMintKey.Fcitx5`, `BambooMintKey.UI.Linux`). Lõi F# (`BambooMintKey.Core`) được tái sử dụng nguyên vẹn.
2. **Vẫn sử dụng cơ chế Preedit (Inline Marked Text):** Vận hành hoàn toàn theo tiêu chuẩn Input Method Service của Apple Text System, không sử dụng cơ chế hook phím ảo (`CGEventTap`) và không giả lập phím xóa lùi (Backspace).
3. **Tắt gạch chân tối đa ở các ứng dụng có thể hỗ trợ:** Cấu hình thuộc tính hiển thị để ẩn đường gạch chân (Stealth Mode) tại các ứng dụng cho phép, giúp văn bản sạch sẽ và không che khuất dấu nặng (`.`) tiếng Việt. Với các ứng dụng tự vẽ gạch chân cứng hoặc không cho tắt, chấp nhận hiển thị mặc định của ứng dụng và không can thiệp sâu.
4. **Ưu tiên số 1 tuyệt đối — Đảm bảo gõ đúng và ổn định:** Đảm bảo chính xác 100% quy tắc ghép âm tiết, bỏ dấu tự do, lặp phím undo, khôi phục từ tiếng Anh, điều hướng phím ngắt và commit mượt mà trước mọi yếu tố về hình thức hiển thị.

---

## 2. Tổng Quan Tiến Độ Các Milestone

| Milestone | Tên Hạng Mục | Trọng Số | Trạng Thái | Tiến Độ (%) | Ghi Chú |
|:---:|---|:---:|:---:|:---:|---|
| **M1** | **Thiết Kế Kiến Trúc Bằng Lời (No Sample Code)** | 15% | ✅ Hoàn thành | 100% | Hoàn thành 4 tài liệu đặc tả: C-ABI, IMK Engine, UI.Mac, E2E Test & Delivery |
| **M2** | **Thư Viện Lõi C-ABI NativeAOT macOS (`.dylib`)** | 20% | ✅ Hoàn thành | 100% | Xuất Mach-O dylib cho arm64 (4.4MB) & x86_64 (4.6MB), 14 hàm C-ABI, 9/9 PASS |
| **M3** | **Bộ Gõ Bản Địa macOS (IMK Engine Service)** | 25% | ✅ Hoàn thành | 100% | Swift `IMKInputController` + C-ABI, Marked Text & commit string, bundle hợp lệ |
| **M4** | **Giao Diện Cài Đặt Bản Địa (`BambooMintKey.UI.Mac`)** | 15% | ✅ Hoàn thành | 100% | Clone UI Avalonia, bỏ D-Bus, cấu hình JSON Application Support, single instance |
| **M5** | **Đồng Bộ Trạng Thái V/E & Menu Bar** | 10% | ⏳ Chưa bắt đầu | 0% | Biểu tượng V/E Menu Bar, IPC thông báo hai chiều nội bộ |
| **M6** | **Kiểm Thử E2E Tính Đúng Đắn & Tương Thích** | 10% | ⏳ Chưa bắt đầu | 0% | Ma trận kiểm thử ngữ pháp tiếng Việt và tương thích đa ứng dụng |
| **M7** | **Đóng Gói Bundle & Script Cài Đặt Tự Động** | 5% | ⏳ Chưa bắt đầu | 0% | Cấu trúc `BambooMintKey.app` và script cài/gỡ tự động một chạm |
| **Tổng** | **Toàn bộ Phase 10 (macOS / IMK)** | **100%** | 🛠️ **Đang triển khai** | **75%** | Sẵn sàng bước vào Milestone 5 |

---

## 3. Checklist Chi Tiết Từng Đầu Việc

### 🎯 Milestone 1: Hoàn Thiện Hồ Sơ Thiết Kế Kiến Trúc Bằng Lời
> **Mục tiêu:** Xây dựng đầy đủ tài liệu đặc tả kiến trúc, hợp đồng giao tiếp C-ABI, và luồng điều phối sự kiện văn bản trên macOS **hoàn toàn bằng lời văn**, không sử dụng code mẫu.  
> **Tài liệu hoàn thành:**
> - [010_02_Architecture_and_CABI_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md)
> - [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md)
> - [010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md)
> - [010_05_E2E_TestPlan_and_Delivery.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.md)

- [x] **M1.1 — Đặc tả kiến trúc tổng thể và vòng đời IMK Server** ([010_02_Architecture_and_CABI_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md), [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md))
  - [x] Phân tích chu trình khởi động và đăng ký dịch vụ của tiến trình Input Method với hệ điều hành macOS.
  - [x] Phân tích cơ chế sinh phiên điều khiển nhập liệu (`IMKInputController`) khi người dùng trỏ chuột vào ô nhập văn bản.
  - [x] Đặc tả chu trình hủy phiên và dọn dẹp tài nguyên khi ô nhập văn bản mất tiêu điểm (deactivate/blur).
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Tài liệu phân tích rõ ràng toàn bộ các trạng thái vòng đời của IMK Server và Controller.

- [x] **M1.2 — Thiết kế hợp đồng giao tiếp C-ABI và bộ đệm ngữ cảnh (Context Handle)** ([010_02_Architecture_and_CABI_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md))
  - [x] Đặc tả danh mục các hàm C-ABI cần thiết: khởi tạo phiên, hủy phiên, làm mới bộ đệm, đẩy sự kiện ký tự, trích xuất văn bản preedit và trích xuất văn bản commit.
  - [x] Nguyên tắc quản lý con trỏ unmanaged: mỗi phiên nhập liệu nắm giữ một handle độc lập, cách ly hoàn toàn dữ liệu giữa các cửa sổ ứng dụng khác nhau.
  - [x] Cơ chế sở hữu bộ nhớ: bên gọi chỉ đọc từ con trỏ bộ đệm cố định của lõi, không giải phóng trực tiếp.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Hợp đồng C-ABI được mô tả tường minh về kiểu dữ liệu, trách nhiệm cấp phát và giải phóng bộ nhớ.

- [x] **M1.3 — Thiết kế cơ chế điều phối văn bản (Marked Text) và chính sách gạch chân** ([010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md))
  - [x] Phân tích luồng nhận sự kiện phím từ hệ điều hành và ánh xạ sang hàm C-ABI.
  - [x] Đặc tả cơ chế hiển thị Inline Marked Text: gán thuộc tính văn bản để ẩn đường gạch chân trên các ứng dụng Cocoa bản địa hỗ trợ.
  - [x] Xác lập quy tắc chấp nhận hiển thị mặc định của ứng dụng đối với các phần mềm tự vẽ gạch chân cứng; không can thiệp sâu vào tiến trình ngoài.
  - [x] Luồng chốt văn bản (Commit): chốt chuỗi và xóa đánh dấu dứt khoát khi gặp phím ngắt, dấu cách hoặc phím Enter.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Luồng điều phối văn bản và chính sách gạch chân được mô tả chặt chẽ, đáp ứng đúng nguyên tắc ưu tiên gõ đúng.

- [x] **M1.4 — Thiết kế cơ chế xử lý phím bổ trợ hệ thống (`Command`, `Control`)** ([010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md))
  - [x] Nhận diện các tổ hợp phím tắt phổ biến (`Cmd+A`, `Cmd+C`, `Cmd+V`, `Cmd+Z`, `Cmd+S`, `Cmd+Tab`).
  - [x] Hành vi khi đang ráp từ dở dang: tự động chốt từ hiện tại vào văn bản trước khi nhường quyền cho hệ thống xử lý phím tắt, tránh làm gián đoạn ngữ cảnh.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Thuật toán kiểm tra và nhường phím tắt hệ thống được đặc tả đầy đủ.

- [x] **M1.5 — Thiết kế cơ chế đồng bộ trạng thái V/E và lưu trữ cấu hình** ([010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md))
  - [x] Thiết kế cơ chế lưu trữ bền vững tại đường dẫn chuẩn `~/Library/Application Support/BambooMintKey/config.json`.
  - [x] Cơ chế ghi file an toàn (ghi tệp tạm thời rồi đổi tên) để tránh xung đột ghi đọc.
  - [x] Cơ chế theo dõi sự kiện thay đổi tệp tin để cập nhật cấu hình thời gian thực trong tiến trình bộ gõ.
  - [x] Thiết kế kênh truyền thông điệp nội bộ hai chiều giữa Menu Bar, Service bộ gõ và cửa sổ Cài đặt.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Cơ chế đồng bộ được thiết kế tối ưu, không phụ thuộc vào daemon bên ngoài như D-Bus.

---

### 🎯 Milestone 2: Biên Dịch Thư Viện Lõi C-ABI NativeAOT macOS (`libBambooMintKeyCore.dylib`)
> **Mục tiêu:** Cấu hình và biên dịch thành công thư viện C-ABI NativeAOT từ mã nguồn `BambooMintKey.Core.Native` trên macOS cho cả hai kiến trúc Apple Silicon và Intel.  
> **Tài liệu tham chiếu:** [010_02_Architecture_and_CABI_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md)

- [x] **M2.1 — Khảo sát và cấu hình điều kiện biên dịch NativeAOT cho macOS**
  - [x] Kiểm tra điều kiện xuất thư viện động Mach-O (`.dylib`) trong dự án C# NativeAOT.
  - [x] Thiết lập cấu hình điều kiện trong file dự án để không gây ảnh hưởng đến cấu hình xuất `.so` trên Linux và `.dll` trên Windows.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Dự án C# được cấu hình sẵn sàng biên dịch NativeAOT trên macOS.

- [x] **M2.2 — Thực hiện biên dịch thử nghiệm trên môi trường macOS**
  - [x] Chạy lệnh xuất bản NativeAOT nhắm mục tiêu `osx-arm64` (Apple Silicon) và `osx-x64` (Intel).
  - [x] Kiểm tra sự tồn tại của tệp nhị phân `BambooMintKeyCore.dylib` và các biểu tượng hàm được xuất.
  - [x] Đo đạc kích thước tệp nhị phân và độ trễ nạp thư viện.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Sinh thành công tệp `BambooMintKeyCore.dylib` hợp lệ trên macOS.

- [x] **M2.3 — Viết chương trình kiểm tra độc lập (C-ABI Harness trên macOS)**
  - [x] Xây dựng chương trình dòng lệnh nhỏ để nạp trực tiếp `BambooMintKeyCore.dylib`.
  - [x] Kiểm tra toàn bộ chuỗi chức năng C-ABI: tạo context, gửi phím Telex, kiểm tra chuỗi preedit, gửi dấu cách, kiểm tra chuỗi commit, xóa context.
  - [x] Kiểm tra tính độc lập đa context giữa 2 phiên giả lập chạy song song.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* 100% các ca kiểm tra C-ABI đạt kết quả chính xác, bộ đệm không bị rò rỉ bộ nhớ.

---

### 🎯 Milestone 3: Xây Dựng Bộ Gõ Bản Địa macOS (IMK Engine Service)
> **Mục tiêu:** Hiện thực hóa Input Method Service chuẩn của Apple bằng Swift, tích hợp gọi C-ABI vào thư viện lõi, điều phối Marked Text và commit string.  
> **Tài liệu tham chiếu:** [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md)

- [x] **M3.1 — Khởi tạo cấu trúc dự án ứng dụng nền `BambooMintKey.Mac.IMK`**
  - [x] Tạo thư mục dự án độc lập tại `src/BambooMintKey.Mac.IMK/`.
  - [x] Thiết lập tệp cấu hình thuộc tính `Info.plist` chuẩn Input Method của Apple.
  - [x] Cấu hình liên kết động tới `BambooMintKeyCore.dylib`.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Khởi tạo dự án thành công, biên dịch ra gói ứng dụng nền ban đầu.

- [x] **M3.2 — Triển khai thành phần Server và Controller (`IMKInputController`)**
  - [x] Khởi tạo lớp điều khiển nhập liệu kế thừa từ `IMKInputController`.
  - [x] Quản lý con trỏ context C-ABI riêng cho từng thể hiện của Controller.
  - [x] Cài đặt hàm khởi tạo và hàm hủy phiên để giải phóng context handle tương ứng.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Mỗi ô văn bản được kích hoạt tương ứng với một phiên context C-ABI độc lập.

- [x] **M3.3 — Cài đặt quy trình xử lý sự kiện phím bấm (`handleEvent`)**
  - [x] Đón sự kiện nhấn phím từ hệ điều hành và trích xuất mã ký tự Unicode.
  - [x] Kiểm tra và nhường phím cho hệ thống khi phát hiện cờ bổ trợ `Command` hoặc `Control`.
  - [x] Gọi hàm C-ABI `bmk_process_key` và tiếp nhận mã hành động trả về.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Sự kiện phím được chuyển giao chính xác vào lõi F# và nhận diện đúng các trường hợp cần xử lý.

- [x] **M3.4 — Cài đặt hiển thị Marked Text và cơ chế tắt gạch chân**
  - [x] Lấy chuỗi preedit từ C-ABI khi nhận mã hành động cập nhật trạng thái đang gõ.
  - [x] Thiết lập chuỗi thuộc tính văn bản yêu cầu ẩn đường gạch chân (Stealth mode) gửi đến đối tượng văn bản đích.
  - [x] Giữ nguyên hiển thị mặc định của ứng dụng đối với các app không tuân theo chỉ thị ẩn gạch chân.
  - [x] Di chuyển vị trí con trỏ văn bản chính xác về cuối chuỗi đang soạn thảo.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Chữ đang gõ hiển thị mượt mà tại con trỏ, tắt gạch chân tại các ứng dụng hỗ trợ, không phát sinh lỗi hiển thị.

- [x] **M3.5 — Cài đặt cơ chế chốt văn bản (Commit String)**
  - [x] Lấy chuỗi commit từ C-ABI khi gặp phím ngắt từ, dấu cách hoặc phím Enter.
  - [x] Gửi lệnh chèn văn bản hoàn chỉnh vào ứng dụng đích và xóa sạch vùng đệm đánh dấu.
  - [x] Xử lý an toàn khi người dùng nhấp chuột ra ngoài ô nhập liệu hoặc chuyển cửa sổ.
- [x] **M3.6 — Kiểm chứng thực địa & Khắc phục sự cố runtime IMK** ([008_MacOS_M3_Verification_And_Fixes_Report.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/008_MacOS_M3_Verification_And_Fixes_Report.md))
  - [x] Khắc phục Issue 016: cấu hình `ComponentInputModeDict`, `TISInputSourceID` trong `Info.plist`, nhận diện thành công trong Input Sources.
  - [x] Sửa triệt để lỗi nhân đôi ký tự (`thuwrthử`) bằng chuẩn Cocoa `NSRange(location: NSNotFound, length: NSNotFound)`.
  - [x] Sửa lỗi mất gõ chữ khi đổi focus/click menu: bảo toàn `contextHandle` trong `deactivateServer`, thêm `activateServer` và `ensureContext()` tự phục hồi.
  - [x] Rút gọn tên hiển thị thô thành `BambooMintKey` qua `Resources/{en,vi}.lproj/InfoPlist.strings`.
  - [x] Chuẩn hóa kích thước Icon Retina 16x16 & 32x32 Aqua và hiện thực hóa menu tương tác (`override func menu() -> NSMenu!`).
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Người dùng thử nghiệm thực tế xác nhận gõ mượt mà, ổn định đa ứng dụng, menu bar trực quan, đạt 100% nghiệm thu M3.

---

### 🎯 Milestone 4: Xây Dựng Giao Diện Cài Đặt Bản Địa (`BambooMintKey.UI.Mac`)
> **Mục tiêu:** Nhân bản và độc lập hóa giao diện cấu hình Avalonia UI cho người dùng macOS, loại bỏ hoàn toàn tầng D-Bus của Linux.  
> **Tài liệu tham chiếu:** [010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md)

- [x] **M4.1 — Khởi tạo dự án `BambooMintKey.UI.Mac`**
  - [x] Tạo thư mục dự án độc lập tại `src/BambooMintKey.UI.Mac/`.
  - [x] Tạo file dự án F# nhắm mục tiêu .NET 10, tham chiếu Avalonia UI và các thư viện cần thiết.
  - [x] Kế thừa toàn bộ giao diện trực quan và tài nguyên kiểu dáng hiện đại từ phiên bản Linux.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Dự án biên dịch thành công ứng dụng giao diện trên macOS.

- [x] **M4.2 — Thay thế tầng D-Bus bằng lớp quản lý cấu hình chuẩn macOS**
  - [x] Loại bỏ hoàn toàn mã kết nối D-Bus và các thư viện phụ thuộc của Linux.
  - [x] Xây dựng lớp quản lý cấu hình đọc ghi tệp JSON tại `~/Library/Application Support/BambooMintKey/config.json`.
  - [x] Áp dụng cơ chế ghi tệp nguyên tử (ghi tệp tạm rồi đổi tên) để đảm bảo toàn vẹn dữ liệu.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Cấu hình được tải và lưu chính xác theo đường dẫn chuẩn của macOS.

- [x] **M4.3 — Cài đặt cơ chế chạy đơn phiên bản (Single Instance)**
  - [x] Xây dựng cơ chế đảm bảo chỉ một cửa sổ Cài đặt duy nhất được mở.
  - [x] Nếu người dùng mở lại từ Menu Bar, ứng dụng tự động đưa cửa sổ hiện có lên trước màn hình.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Không xảy ra tình trạng mở trùng lặp nhiều cửa sổ Cài đặt.

- [x] **M4.4 — Tích hợp tab gõ thử nghiệm trực tiếp**
  - [x] Kết nối trực tiếp vào lõi F# Core để người dùng thử nghiệm gõ phím ngay trong cửa sổ Cài đặt.
  - [x] Thử nghiệm tức thì các tùy chọn gõ: Telex/VNI, kiểu đặt dấu mới/cũ, khôi phục từ tiếng Anh on-the-fly.
  - [x] *Tiêu chuẩn hoàn thành (DoD):* Tab gõ thử nghiệm phản ánh chính xác cấu hình người dùng vừa thiết lập.

---

### 🎯 Milestone 5: Đồng Bộ Trạng Thái V/E & Tích Hợp Menu Bar
> **Mục tiêu:** Cung cấp biểu tượng trạng thái gõ trên thanh tác vụ Menu Bar macOS và cơ chế đồng bộ tức thì hai chiều giữa UI và IMK Service.  
> **Tài liệu tham chiếu:** [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md), [010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md)

- [ ] **M5.1 — Xây dựng biểu tượng trạng thái Menu Bar (Status Item)**
  - [ ] Tạo biểu tượng hiển thị ký tự `V` (tiếng Việt) hoặc `E` (tiếng Anh) trên thanh Menu Bar của macOS.
  - [ ] Xây dựng menu thả xuống khi nhấn vào biểu tượng: chuyển đổi chế độ gõ, mở cửa sổ Cài đặt, thoát bộ gõ.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Biểu tượng hiển thị sắc nét trên Menu Bar, phản hồi nhanh khi nhấp chuột.

- [ ] **M5.2 — Cài đặt cơ chế theo dõi tệp cấu hình thời gian thực (File Watcher)**
  - [ ] Thiết lập trình theo dõi sự kiện tệp tin trong tiến trình bộ gõ đối với tệp `config.json`.
  - [ ] Khi người dùng bấm Lưu trên giao diện Cài đặt, bộ gõ tự động nạp lại cấu hình mới ngay lập tức.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Thay đổi cấu hình có hiệu lực tức thì mà không cần khởi động lại máy hay logout.

- [ ] **M5.3 — Cài đặt kênh truyền thông báo chuyển đổi chế độ gõ hai chiều**
  - [ ] Thiết lập kênh thông báo nội bộ hệ thống để đồng bộ trạng thái khi chuyển chế độ bằng phím tắt hoặc Menu Bar.
  - [ ] Biểu tượng Menu Bar và giao diện Cài đặt tự động cập nhật đồng bộ khi trạng thái V/E thay đổi.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Chuyển đổi V/E diễn ra trơn tru, không có độ trễ, giao diện đồng bộ chính xác.

---

### 🎯 Milestone 6: Kiểm Thử E2E Tính Đúng Đắn & Tương Thích
> **Mục tiêu:** Kiểm chứng toàn diện chất lượng gõ tiếng Việt và khả năng tương thích đa ứng dụng theo đúng nguyên tắc "Đầu tiên là đảm bảo gõ đúng được đã".  
> **Tài liệu tham chiếu:** [010_05_E2E_TestPlan_and_Delivery.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.md) ([010_05_E2E_TestPlan_and_Delivery.txt](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.txt))

- [ ] **M6.1 — Kiểm thử bộ quy tắc ngữ pháp tiếng Việt (Language Correctness)**
  - [ ] Kiểm tra dấu mũ Telex: `aa` -> `â`, `ee` -> `ê`, `oo` -> `ô`.
  - [ ] Kiểm tra dấu móc và dấu trăng: `ow` -> `ơ`, `uw` -> `ư`, `aw` -> `ă`, `dd` -> `đ`.
  - [ ] Kiểm tra đặt dấu thanh tự do và di chuyển dấu theo âm đệm: `toans` -> `toán`, `hoaf` -> `hòa` / `hoà`.
  - [ ] Kiểm tra lặp phím hoàn tác dấu (Undo): `ass` -> `as`, `ooo` -> `oo`.
  - [ ] Kiểm tra khôi phục từ tiếng Anh on-the-fly: `center`, `internet`, `post`, `simple`.
  - [ ] Kiểm tra xóa lùi (Backspace): xóa từng ký tự trong từ đang gõ mà không bị vỡ âm tiết.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* 100% các ca kiểm thử ngữ pháp tiếng Việt đạt kết quả chính xác theo chuẩn Unicode dựng sẵn (NFC).

- [ ] **M6.2 — Kiểm thử chính sách hiển thị Preedit và tắt gạch chân**
  - [ ] Xác nhận đường gạch chân được ẩn thành công trên các ứng dụng Apple bản địa (Safari, Notes, TextEdit).
  - [ ] Kiểm tra các ứng dụng không hỗ trợ ẩn gạch chân: xác nhận văn bản vẫn được gõ đúng, không phát sinh lỗi gián đoạn hoặc ký tự lạ.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Trải nghiệm gõ tự nhiên, không che khuất dấu nặng, không phát sinh lỗi hiển thị.

- [ ] **M6.3 — Kiểm thử ma trận tương thích đa ứng dụng trên macOS**
  - [ ] *Ứng dụng Apple bản địa:* Safari, Apple Notes, TextEdit, Mail, Pages.
  - [ ] *Môi trường lập trình:* Xcode, Visual Studio Code, JetBrains IDEs.
  - [ ] *Ứng dụng Chromium/Electron:* Google Chrome, Slack, Discord, Microsoft Teams.
  - [ ] *Dòng lệnh (Terminal):* macOS Terminal.app, iTerm2.
  - [ ] *Ứng dụng văn phòng:* Microsoft Word, Excel trên macOS.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Không xuất hiện hiện tượng nuốt phím, không nhân đôi ký tự, không giật màn hình trên tất cả các ứng dụng trong ma trận.

---

### 🎯 Milestone 7: Đóng Gói Bundle & Script Cài Đặt Tự Động
> **Mục tiêu:** Xây dựng gói ứng dụng bundle chuẩn và kịch bản cài đặt tự động một chạm cho người dùng macOS.  
> **Tài liệu tham chiếu:** [010_05_E2E_TestPlan_and_Delivery.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.md) ([010_05_E2E_TestPlan_and_Delivery.txt](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.txt))

- [ ] **M7.1 — Xây dựng cấu trúc Application Bundle (`BambooMintKey.app`)**
  - [ ] Tổ chức cấu trúc thư mục bundle chuẩn: file thực thi chính, thư viện `libBambooMintKeyCore.dylib`, tài nguyên icon và file thông tin `Info.plist`.
  - [ ] Kiểm tra phân quyền thực thi và tính toàn vẹn của gói bundle.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Gói bundle đạt chuẩn định dạng ứng dụng Input Method của macOS.

- [ ] **M7.2 — Viết kịch bản cài đặt tự động (`scripts/install_macos.sh`)**
  - [ ] Tự động kiểm tra môi trường: công cụ biên dịch .NET 10 và Swift/Xcode command line tools.
  - [ ] Tự động biên dịch thư viện lõi C-ABI, ứng dụng IMK và giao diện Cài đặt.
  - [ ] Đóng gói và cài đặt bundle vào thư mục `~/Library/Input Methods/BambooMintKey.app`.
  - [ ] Đăng ký dịch vụ với hệ thống để người dùng có thể kích hoạt ngay trong System Settings.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Người dùng chỉ cần chạy một lệnh trong Terminal là hoàn tất cài đặt toàn bộ hệ thống.

- [ ] **M7.3 — Viết kịch bản gỡ cài đặt sạch sẽ (`scripts/uninstall_macos.sh`)**
  - [ ] Dừng các tiến trình bộ gõ đang chạy trong hệ thống.
  - [ ] Xóa sạch bundle trong thư mục Input Methods và tệp cấu hình nếu được yêu cầu.
  - [ ] Trả lại trạng thái sạch sẽ cho hệ thống macOS.
  - [ ] *Tiêu chuẩn hoàn thành (DoD):* Kịch bản gỡ cài đặt thực thi an toàn, không để lại rác trong hệ thống.

---

## 4. Nhật Ký Tiến Độ (Progress Log)

| Ngày | Milestone liên quan | Tóm Tắt Hoạt Động | Trạng Thái |
|:---:|:---:|---|:---:|
| **2026-10-09** | **Khởi động Phase 10** | Hoàn thành tài liệu Khảo sát khả thi & Kế hoạch tổng thể ([010_01_Investigation.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_01_Investigation.md)). Thiết lập 4 nguyên tắc kiến trúc bất biến (bảo toàn Windows/Linux, thuần IMK Preedit, tắt gạch chân tối đa, ưu tiên gõ đúng). Khởi tạo tài liệu theo dõi tiến độ chi tiết `007_MacOSProgressTracking.md`. | ✅ Hoàn thành |
| **2026-10-09** | **Milestone 1** | Hoàn thành toàn diện 4 tài liệu thiết kế kỹ thuật kiến trúc bằng lời (hoàn toàn không dùng code mẫu) tại `docs/2.Design/Phase10/`: [010_02_Architecture_and_CABI_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_02_Architecture_and_CABI_Design.md), [010_03_IMK_Engine_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_03_IMK_Engine_Design.md), [010_04_UIMac_Design.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_04_UIMac_Design.md), [010_05_E2E_TestPlan_and_Delivery.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/2.Design/Phase10/010_05_E2E_TestPlan_and_Delivery.md). Đã liên kết đầy đủ vào Progress Tracking, đạt 100% Milestone 1 (tổng tiến độ Phase 10 đạt 15%). | ✅ Hoàn thành |
| **2026-10-09** | **Milestone 2** | Biên dịch thành công thư viện lõi C-ABI NativeAOT macOS `BambooMintKeyCore.dylib` (Mach-O) cho cả `osx-arm64` (4.4MB) và `osx-x64` (4.6MB). Xuất đủ 14 hàm C-ABI (`bmk_*`). Bộ kiểm thử C-ABI `scripts/tests/test-cabi.py` được điều chỉnh chạy đa nền tảng (bỏ phụ thuộc `/proc/self/statm`, thêm đường dẫn mặc định macOS) và đạt 9/9 PASS (lifecycle, telex cơ bản, tổ hợp dấu, backspace, wordbreak, khôi phục tiếng Anh, đa context song song, chống rò rỉ bộ nhớ, config JSON). Đạt 100% Milestone 2 (tổng tiến độ Phase 10 đạt 35%). | ✅ Hoàn thành |
| **2026-10-09** | **Milestone 3** | Xây dựng Bộ Gõ Bản Địa macOS (IMK Engine Service) bằng Swift tại `src/BambooMintKey.Mac.IMK/`: `main.swift` (điểm vào `IMKServer`), `BambooMintKeyController.swift` (kế thừa `IMKInputController`, quản lý context C-ABI riêng từng phiên, `handleEvent` phân loại phím Command/Control/Backspace/ngắt từ, Marked Text ẩn gạch chân, commit string). Giải quyết triệt để Issue 016 (đăng ký Input Sources), sửa lỗi nhân đôi ký tự (`thuwrthử`), sửa lỗi mất gõ chữ khi đổi focus, chuẩn hóa tên hiển thị `BambooMintKey` và icon Retina 16x16/32x32 Aqua kèm Menu Bar thả xuống ([008_MacOS_M3_Verification_And_Fixes_Report.md](file:///Users/lmo1720/Self-App/BambooMintKey/docs/4.Progress/008_MacOS_M3_Verification_And_Fixes_Report.md)). Người dùng nghiệm thu thực tế đạt 100% Milestone 3 (tổng tiến độ Phase 10 đạt 60%). | ✅ Hoàn thành |
| **2026-10-09** | **Milestone 4** | Xây dựng Giao Diện Cài Đặt Bản Địa `src/BambooMintKey.UI.Mac/` (F# .NET 10 + Avalonia): `MainWindow.axaml` (6 tab clone từ Linux, đổi text macOS), `MainWindow.axaml.fs` (tab gõ thử nghiệm trực tiếp qua F# Core), `SharedConfig.fs` (đọc/ghi `~/Library/Application Support/BambooMintKey/config.json` theo chuẩn macOS, atomic write), `SingleInstance.fs` (Unix socket đưa cửa sổ lên trước), `Program.fs`, `App.axaml`. Loại bỏ hoàn toàn tầng D-Bus. Đã thêm vào `BambooMintKey.slnx`. Khắc phục lỗi Avalonia macOS (thoát app bằng `Environment.Exit(0)` thay `Close()`/`Shutdown()` vì treo Not Responding). Người dùng nghiệm thu giao diện hiển thị + lưu cấu hình đúng, đạt 100% Milestone 4 (tổng tiến độ Phase 10 đạt 75%). | ✅ Hoàn thành |
| *Tiếp theo* | **M5** | Bắt đầu Milestone 5: Đồng Bộ Trạng Thái V/E & Tích Hợp Menu Bar — Biểu tượng V/E, file watcher config, IPC thông báo hai chiều. | ⏳ Sẵn sàng |
