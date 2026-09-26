<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 008_04 — Thiết Kế Engine Thẩm Định Âm Tiết On-The-Fly & Cơ Chế Hoàn Tác Từ Tiếng Anh

**Mã tài liệu:** `008_04_OnTheFly_PredictEngine_Design`  
**Giai đoạn:** Phase 8 — Tích hợp từ điển & sửa lỗi vặt  
**Thuộc module:** `BambooMintKey.Core` (Dùng chung độc lập cho cả Windows & Linux)  
**Trạng thái:** 📝 Đã hoàn thiện thiết kế chi tiết  
**Tài liệu tham chiếu:** [008_01_InvestigationForDictionary.md](008_01_InvestigationForDictionary.md), [008_02_MIT_Dictionary_And_Corpus_Design.md](008_02_MIT_Dictionary_And_Corpus_Design.md), [008_03_Phonotactic_Rules_Fix_Design.md](008_03_Phonotactic_Rules_Fix_Design.md)

---

## 1. Mục Tiêu & Phạm Vi Thiết Kế

Tài liệu này đặc tả kiến trúc can thiệp trực tiếp trên từng phím bấm (On-The-Fly Per-Keystroke Pipeline) của bộ gõ BambooMintKey, bao gồm:
1. Xây dựng dịch vụ thẩm định âm tiết tiếng Việt với thời gian phản hồi sub-microsecond ($O(1)$) trên từng thao tác nhấn phím.
2. Hiện thực hóa cơ chế tự động hoàn tác thông minh về từ tiếng Anh (On-The-Fly Backtracking), loại bỏ triệt để hiện tượng xung đột dấu khi gõ văn bản song ngữ hoặc gõ mã lập trình (code).
3. Đảm bảo tính nhất quán của trải nghiệm gõ nối tiếp (Inline Composition) theo thói quen tự nhiên của người Việt, tuyệt đối không sử dụng cửa sổ popup chọn từ gây phân tâm.
4. Bảo toàn tính độc lập kiến trúc giữa hai nền tảng Windows (TSF) và Linux (Fcitx5), không làm thay đổi giao diện lập trình C-ABI hay cấu trúc cầu nối hiện có.

---

## 2. Vấn Đề Cần Giải Quyết & Phương Pháp Thực Hiện

### Vấn Đề 1: Thẩm định tính hợp lệ của âm tiết tiếng Việt tức thời trên từng phím bấm

- **Hiện trạng & Thách thức:**
  Bộ gõ hiện tại chỉ áp dụng các quy tắc kiểm tra ngữ âm lỏng lẻo. Nhiều chuỗi ký tự bất khả thi trong tiếng Việt nhưng vẫn bị ghép dấu thành các từ dị dạng, hoặc ngược lại, một số từ tiếng Anh bị biến đổi sai mà hệ thống không nhận biết được để khôi phục kịp thời. Nếu việc tra cứu từ điển tốn nhiều thời gian hoặc cấp phát rác trên bộ nhớ, tốc độ gõ phím của người dùng sẽ bị trễ (lag), gây cảm giác nặng nề.
- **Đầu vào (Input):**
  - Âm tiết tiếng Việt tạm thời được hình thành sau khi áp dụng các quy tắc ghép âm và đặt dấu cho phím vừa nhấn.
  - Bộ từ điển âm tiết chuẩn ~7.800 từ đã được nạp sẵn trong bộ nhớ RAM từ tài nguyên nhúng.
- **Đầu ra (Output):**
  - Kết quả xác thực nhị phân: Hợp lệ (âm tiết tồn tại trong tiếng Việt chuẩn) hoặc Bất khả thi (âm tiết dị dạng, không có trong ngôn ngữ tự nhiên).
- **Cách giải quyết:**
  - Sử dụng cấu trúc dữ liệu tập hợp bất biến được tối ưu hóa cực hạn tại thời điểm khởi tạo (`FrozenSet`). Cấu trúc này tính toán bảng băm hoàn hảo cho 7.800 phần tử, cho phép phép toán kiểm tra sự tồn tại đạt độ phức tạp hằng số $O(1)$ với độ trễ đo được dưới 5 nanosecond.
  - Chuẩn hóa chuỗi đầu vào về dạng Unicode dựng sẵn (NFC) và chữ thường trước khi thực hiện tra cứu.
  - Toàn bộ quá trình tra cứu không thực hiện cấp phát bất kỳ đối tượng mới nào trên bộ nhớ Heap của môi trường thực thi (Zero-allocation per keystroke), đảm bảo trình thu gom rác (GC) không bị kích hoạt trong lúc người dùng đang gõ phím tốc độ cao.

---

### Vấn Đề 2: Cơ chế tự động hoàn tác thông minh về từ tiếng Anh (On-The-Fly Backtracking)

- **Hiện trạng & Thách thức:**
  Khi người dùng gõ các từ tiếng Anh có chứa các phím trùng với phím dấu hoặc modifier Telex (ví dụ: gõ phím `s` trong `post`, phím `r` trong `form`, phím `e` trong `core`), trong các bước gõ ban đầu, hệ thống tạm thời coi đó là tiếng Việt và hiển thị chữ có dấu (như `pó`, `fỏ`, `cò`).
  Nếu hệ thống chờ đến khi người dùng nhấn phím dấu cách (Space) mới quay lại sửa từ, con trỏ soạn thảo sẽ bị giật lùi (phải gửi chuỗi phím xóa lùi Backspace để thay thế), gây ra hiện tượng nhảy chữ (flicker) rất khó chịu. Nếu người dùng gõ từ tiếng Anh rồi gõ tiếp dấu chấm, phẩy hoặc phím điều hướng, từ đó sẽ bị mắc kẹt ở dạng tiếng Việt sai chính tả.
- **Đầu vào (Input):**
  - Chuỗi toàn bộ các ký tự phím thô người dùng đã gõ từ đầu từ đến thời điểm hiện tại.
  - Ký tự phím vừa được nhấn thêm vào từ đệm.
  - Trạng thái âm tiết tiếng Việt vừa được tính toán thử nghiệm.
  - Từ điển tiếng Anh thông dụng 20.000 từ.
- **Đầu ra (Output):**
  - Quyết định hành động của bộ gõ:
    - Tiếp tục duy trì hiển thị âm tiết tiếng Việt nếu âm tiết đó hợp lệ.
    - Hoặc ngay lập tức kích hoạt hoàn tác (Backtrack): Hủy bỏ toàn bộ dấu tiếng Việt tạm thời và hiển thị chuỗi ký tự thô nguyên bản của từ tiếng Anh ngay trên chính phím bấm vừa gõ.
- **Cách giải quyết:**
  - Quy trình đánh giá trên từng phím bấm được thiết kế theo mô hình máy trạng thái giả thuyết (Hypothesis Evaluation):
    1. Khi ký tự mới được gõ vào, hệ thống tính toán âm tiết tiếng Việt tiềm năng.
    2. Đưa âm tiết tiềm năng qua bộ thẩm định âm tiết ở Vấn đề 1:
       - Nếu âm tiết tiềm năng là một âm tiết tiếng Việt hợp lệ: Hệ thống giữ nguyên trạng thái tiếng Việt và hiển thị cho người dùng.
       - Nếu âm tiết tiềm năng là âm tiết bất khả thi trong tiếng Việt (ví dụ: `pót` không phải từ tiếng Việt, `fỏm` không phải từ tiếng Việt, `cơe` không phải từ tiếng Việt):
         - Hệ thống lập tức chuyển sang kiểm tra chuỗi phím thô đối với tiêu chuẩn tiếng Anh.
         - Kiểm tra 1: Chuỗi phím thô có nằm trong danh mục 20.000 từ tiếng Anh thông dụng hay không.
         - Kiểm tra 2: Chuỗi phím thô có kết thúc bằng các cụm phụ âm đặc trưng chỉ xuất hiện trong tiếng Anh hay không (như đuôi `-st` trong `post/fast`, đuôi `-rm` trong `form/storm`, đuôi `-rt` trong `start/part`, đuôi `-ct` trong `fact/direct`, đuôi `-ft` trong `left/gift`, đuôi `-re` trong `core/more/before`).
         - Nếu một trong hai kiểm tra trên thỏa mãn: Engine lập tức kết luận người dùng đang gõ từ tiếng Anh. Toàn bộ các dấu thanh và modifier tạm thời bị bãi bỏ; chuỗi hiển thị được cập nhật về dạng chữ thô tiếng Anh nguyên bản với quy tắc viết hoa/thường chuẩn xác tương ứng.
  - Nhờ cơ chế này, quá trình chuyển đổi diễn ra tức thì trong microsecond: Người dùng vừa nhấn phím `t` sau `pó` là trên màn hình đã hiện ngay lập tức chữ `post`, hoàn toàn không có cảm giác bị giật lùi con trỏ hay phải nhấn thêm bất kỳ phím phụ nào.

---

### Vấn Đề 3: Tích hợp độc lập vào kiến trúc đa nền tảng Windows và Linux

- **Hiện trạng & Thách thức:**
  Bộ gõ đang phục vụ song song hai môi trường có cơ chế xử lý văn bản khác biệt: Windows sử dụng kiến trúc Text Services Framework (TSF) hướng đối tượng COM chạy nội bộ trong từng tiến trình ứng dụng; Linux sử dụng Fcitx5 giao tiếp qua tiến trình daemon chia sẻ chung và thư viện động C-ABI. Bất kỳ sự thay đổi nào làm xáo trộn hợp đồng giao tiếp giữa các tầng đều có nguy cơ gây sập ứng dụng (crash) hoặc phá vỡ tính tương thích ngược.
- **Đầu vào (Input):**
  - Lệnh gọi xử lý phím từ Windows qua hàm cầu nối in-memory `processKey`.
  - Lệnh gọi xử lý phím từ Linux qua hàm C-ABI `bmk_process_key`.
- **Đầu ra (Output):**
  - Chuỗi văn bản đã được thẩm định và xử lý On-the-fly.
  - Mã hành động tương ứng: Yêu cầu cập nhật chuỗi soạn thảo đang gõ (Update Composition / Preedit), chốt từ (Commit), hoặc nhường phím cho hệ điều hành (PassThrough).
- **Cách giải quyết:**
  - Toàn bộ dịch vụ từ điển, bộ thẩm định âm tiết và máy trạng thái On-the-fly được đóng gói thành phần lõi độc lập bên trong thư viện Core thuần túy.
  - **Đối với Linux (Fcitx5):**
    - Giữ nguyên 100% chữ ký của toàn bộ các hàm C-ABI đã công bố trong tệp giao diện `cabibridge.h`.
    - Khi nhận được kết quả từ Core, hàm xuất C-ABI sao chép chuỗi kết quả vào bộ đệm tiền cấp phát (Preedit Buffer) và trả về mã hành động cập nhật tiền soạn thảo. Trình bổ trợ Fcitx5 phía C++ chỉ việc gọi lệnh cập nhật giao diện người dùng có sẵn, hoàn toàn không cần biên dịch lại hay thay đổi cấu trúc mã nguồn C++.
  - **Đối với Windows (TSF):**
    - Bộ quản lý trạng thái cầu nối của TSF tiếp nhận chuỗi kết quả và truyền trực tiếp vào đối tượng phạm vi soạn thảo của Windows TSF. Chữ tiếng Việt hoặc tiếng Anh đã hoàn tác sẽ hiển thị mượt mà kèm đường gạch chân trạng thái đang gõ của hệ điều hành.
  - **Tính tương thích ngược của cấu hình:**
    - Cấu hình điều khiển bật/tắt từ điển và tính năng tự động hoàn tác được gán giá trị mặc định là kích hoạt. Nếu các phiên bản giao diện người dùng cũ chưa có nút bấm điều khiển, hệ thống tự động chạy theo cấu hình mặc định tối ưu mà không phát sinh bất kỳ lỗi thiếu trường dữ liệu nào.
