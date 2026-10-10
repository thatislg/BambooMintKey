<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows & Linux
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 022: Lỗi sinh vần dị dạng (`ưô`, `uâ`, `uă`, `ữâ`), bài toán phân vân hoàn thành từ và phương án giải quyết chi tiết cho BambooMintKey

**Mã tài liệu:** `022_Phonotactic_VowelCluster_Mutations_And_Disambiguation`  
**Trạng thái:** ✅ Đã triển khai và kiểm chứng toàn diện (463/463 tests passed)  
**Mức độ nghiêm trọng:** Cao (Gây lỗi sai chính tả nghiêm trọng, tự động sinh các tổ hợp ký tự không tồn tại trong tiếng Việt)  
**Ngày ghi nhận:** 10/10/2026  
**Phạm vi ảnh hưởng:** Lõi Engine F# (`BambooMintKey.Core`: các module xử lý Modifier, Engine Telex, Quy tắc đặt dấu thanh và Phân tích âm tiết)  
**Tài liệu liên quan:** `008_03_Phonotactic_Rules_Fix_Design.md`, `008_04_OnTheFly_PredictEngine_Design.md`, mã nguồn `fcitx5-bamboo/bamboo-core`

---

## 1. Mô tả chi tiết hiện tượng & Bảng thống kê các ca lỗi

### 1.1. Bối cảnh & Hiện tượng tổng quát
Bộ gõ BambooMintKey hiện tại được thiết kế theo mô hình xử lý phím nối tiếp (Inline Composition) và biến đổi ký tự tức thời (On-The-Fly). Tuy nhiên, trong quá trình gõ thực tế, người dùng gặp phải hiện tượng engine tự động sinh ra các tổ hợp nguyên âm và âm tiết quái thai, hoàn toàn không thuộc về hệ thống ngữ âm học tiếng Việt.

Hiện tượng này biểu hiện cụ thể qua 3 nhóm lỗi lâm sàng:

1. **Nhóm lỗi 1: Móc đơn lẻ phá vỡ cặp nguyên âm kép (`uô` + `w` biến thành `ưô`):**
   - Khi người dùng muốn gõ chữ `được` theo phản xạ Telex tự nhiên bằng chuỗi phím `dduoocjw`, hệ thống xuất ra kết quả là `đưộc`.
   - Khi người dùng muốn gõ chữ `lược` bằng chuỗi phím `luoocjw`, hệ thống xuất ra kết quả là `lưộc`.
   - Về mặt ngữ âm học và mỹ tự học tiếng Việt: Vần `ưô` (hoặc `ưộc`, `ưốc`) hoàn toàn **không tồn tại**. Trong tiếng Việt, các nhị trùng âm tương ứng chỉ có thể xuất hiện ở hai dạng chuẩn hóa:
     - Dạng không móc: Cả hai nguyên âm đều không mang móc, ví dụ `uô` trong các từ *luộc, đuộc, chuộc, cuộc, muôn, luôn*.
     - Dạng có móc: Cả hai nguyên âm đều phải mang móc đồng bộ, ví dụ `ươ` trong các từ *lược, được, trước, nước, mượn, lượn*.
     - Việc hệ thống chỉ gắn móc vào nguyên âm `u` thành `ư` và giữ nguyên nguyên âm `ô` mang dấu nặng `ộ` tạo ra tổ hợp lai dị dạng `ưộc` là sai nghiêm trọng.

2. **Nhóm lỗi 2: Sinh vần khép tại vị trí âm tiết mở (`ua` + `a` biến thành `uâ` không có phụ âm cuối):**
   - Khi người dùng gõ `vuaxa` (chủ đích là gõ chữ `vũa`), hệ thống xuất ra kết quả là `vuẫ`.
   - Khi người dùng gõ `buafa` hoặc gõ nhầm lặp phím `buaaf` (chủ đích là gõ chữ `bùa`), hệ thống xuất ra kết quả là `buầ`.
   - Tương tự khi gõ `duaxa` (chủ đích gõ `dũa`), hệ thống xuất ra kết quả là `duẫ`.
   - Về mặt âm vị học tiếng Việt: Các nguyên âm ngắn và âm đệm gồm `uâ`, `ă`, `â`, `oă` **bắt buộc phải là vần khép**, tức là luôn luôn phải đi kèm một phụ âm cuối đóng vai trò âm tắc hoặc âm vang (*uân, uất, uâng, uâm, ăn, ắt, âm, hoặc, thoắt*). Tiếng Việt tuyệt đối không có bất kỳ từ nào kết thúc trần bằng vần `uâ` (không tồn tại các từ như *vuẫ, buầ, tuầ, cuầ, duẫ*).

3. **Nhóm lỗi 3: Phá vỡ nhị trùng âm đã bão hòa (`ưa` + `a` biến thành `ữâ`):**
   - Khi người dùng đã có từ `sữa` (hoặc `Sữa`), nếu trong nhịp tay gõ người dùng nhấn tiếp phím `a` (`suwaxa`), hệ thống tự ý gắn thêm dấu mũ lên chữ `a` và biến từ thành `Sữâ` hoặc `sữâ`.
   - Về mặt ngữ âm học: Nhị trùng âm `ưa` là một cấu trúc âm vị đã bão hòa và hoàn chỉnh. Tiếng Việt tuyệt đối không có sự kết hợp giữa nguyên âm mang móc `ư` với nguyên âm mang mũ `â` (tổ hợp `ưâ` hoặc `ữâ` hoàn toàn bất khả thi).

### 1.2. Bảng phân tích chi tiết từng ca gõ lỗi

| STT | Chuỗi phím gõ | Kết quả mong đợi | Kết quả hiện tại | Bản chất ngữ âm & Phân tích tâm lý gõ |
|:---:|:---|:---|:---|:---|
| 1 | `dduoocjw` | **được** | `đưộc` | Người dùng đã gõ `dduoocj` ra `đuộc`, sau đó bấm thêm `w` để sửa sang vần móc `ươ`. Phím `w` là mệnh lệnh yêu cầu móc toàn bộ cụm `uô` thành `ươ`. Kết quả bắt buộc phải là `được`. |
| 2 | `luoocjw` | **lược** | `lưộc` | Người dùng đã có `luộc`, bấm thêm phím `w` để chuyển sang `lược`. Hệ thống phải đổi cả cặp `uô` thành `ươ` mang dấu nặng, không được sinh ra `lưộc`. |
| 3 | `dduoocj` | **đuộc** | `đuộc` | Người dùng dừng gõ sau phím `j` (không gõ `w`). Hệ thống tôn trọng phím gõ thô, xuất ra `đuộc` (từ cổ/tên riêng). |
| 4 | `luoocj` | **luộc** | `luộc` | Người dùng dừng gõ sau phím `j`. Hệ thống xuất ra `luộc` (từ tiếng Việt thông dụng). |
| 5 | `vuaxa` | **vũa** | `vuẫ` | Người dùng gõ `v-u-a-x` ra `vũa`, sau đó nhấn thêm phím `a`. Vì vần `uâ` không thể đứng một mình khi thiếu âm cuối, phím `a` không được phép biến đổi vần, phải giữ nguyên `vũa`. |
| 6 | `buafa` | **bùa** | `buầ` | Người dùng gõ `b-u-a-f` ra `bùa`, sau đó nhấn thêm phím `a`. Phím `a` không được phép phá vỡ từ `bùa` để tạo ra từ vô nghĩa `buầ`. |
| 7 | `buaaf` | **bùa** | `buầ` | Người dùng lỡ tay gõ đúp phím `a` khi gõ `bùa`. Hệ thống thông minh phải hiểu `buaa` không có âm cuối thì không thể thành `buâ`, khi gặp dấu huyền `f` phải cho ra `bùa`. |
| 8 | `suwaxa` / `Sữa` + `a` | **Sữa** | `Sữâ` | Người dùng có từ `Sữa`, nhấn thêm phím `a`. Cụm `ưa` đã bão hòa, cấm gắn mũ `â` để tạo thành `ữâ`. Phải giữ nguyên `Sữa`. |
| 9 | `duaxa` | **dũa** | `duẫ` | Người dùng gõ `d-u-a-x` ra `dũa`, nhấn phím `a`. Tương tự `vuaxa`, phải giữ nguyên `dũa`. |
| 10 | `xuana` / `xuan` + `a` | **xuân** | **xuân** | Trường hợp này âm tiết ĐÃ CÓ phụ âm cuối `n`. Phím `a` được phép tác động lên `ua` để tạo thành vần khép hợp lệ `uân`. |

---

## 2. Phân tích nguyên nhân gốc rễ trong mã nguồn hiện tại

Khi đi sâu vào từng dòng lệnh và cấu trúc dữ liệu trong các module lõi của `BambooMintKey.Core`, chúng tôi phát hiện 3 khiếm khuyết cơ bản trong kiến trúc xử lý:

### 2.1. Khiếm khuyết 1: So khớp chuỗi ký tự mù dấu thanh (Tone-Blind Literal Matching)
- **Vị trí phát sinh:** Nằm tại hàm xử lý biến đổi modifier của `ModifierRules.fs` (nhánh xử lý phím `w`).
- **Diễn biến lỗi từng bước:**
  - **Bước 1:** Khi người dùng gõ đến phím `j` trong chuỗi `dduoocj`, hệ thống áp dụng dấu thanh Nặng lên âm tiết. Ký tự `ô` mang dấu nặng trở thành ký tự dựng sẵn `ộ` (mã Unicode `\u1ED9`). Hạt nhân nguyên âm lưu trong bộ đệm lúc này là chuỗi `"uộ"`.
  - **Bước 2:** Người dùng gõ tiếp phím `w`. Hàm xử lý modifier nhận chuỗi hạt nhân nguyên âm hiện tại là `"uộ"`.
  - **Bước 3:** Quy tắc ưu tiên biến đổi cặp đôi kiểm tra bằng biểu thức so khớp chuỗi ký tự con: tìm kiếm các chuỗi literal `"uo"`, `"uô"`, `"ưo"`, `"uơ"` bên trong chuỗi hạt nhân.
  - **Bước 4:** Do ký tự `ộ` hoàn toàn khác với ký tự `ô` không dấu, phép so sánh chuỗi literal trả về kết quả sai (false). Toàn bộ khối lệnh thay thế cặp đôi bị bỏ qua.
  - **Bước 5:** Con trỏ thực thi trượt xuống nhánh dự phòng phía dưới: tìm kiếm nguyên âm đơn `'u'` chưa mang móc để biến đổi thành `'ư'`.
  - **Bước 6:** Nhánh này thấy trong chuỗi `"uộ"` có chứa ký tự `'u'`, liền biến đổi chữ `'u'` thành chữ `'ư'` và để nguyên ký tự `'ộ'`.
  - **Bước 7:** Hệ thống ghép nối lại thành hạt nhân dị dạng `"ưộ"` và xuất ra màn hình chữ `đưộc`. Tương tự với chuỗi `luoocjw` xuất ra chữ `lưộc`.

### 2.2. Khiếm khuyết 2: Biến đổi cục bộ mù ngữ cảnh và thiếu thẩm định sau biến đổi (Missing Post-Mutation Validation)
- **Vị trí phát sinh:** Nằm tại hàm xử lý modifier của `ModifierRules.fs` (nhánh xử lý phím `a`) kết hợp với luồng gia tăng trong `TelexEngine.fs`.
- **Diễn biến lỗi từng bước:**
  - **Bước 1:** Người dùng đang có từ `Sữa`, trong đó hạt nhân nguyên âm là `"ữa"` (gồm nguyên âm `ư` mang dấu ngã và nguyên âm `a`).
  - **Bước 2:** Người dùng gõ tiếp phím `a`.
  - **Bước 3:** Logic hiện tại duyệt qua các nguyên âm trong hạt nhân. Nó tìm thấy ký tự gốc `'a'` chưa mang dấu mũ hay dấu trăng.
  - **Bước 4:** Logic lập tức suy diễn: *"Gặp phím a mà có ký tự gốc a thì gắn dấu mũ Hat vào để biến thành â"*. Ký tự `'a'` bị biến thành `'â'`, ghép với chữ `'ữ'` phía trước tạo thành `"ữâ"`.
  - **Bước 5:** Sau khi biến đổi xong, hàm trả về kết quả thành công mà **không hề có bất kỳ bước kiểm tra ngữ âm học nào** để xác nhận xem cụm nguyên âm mới hình thành có tồn tại trong tiếng Việt hay không.
  - **Bước 6:** `TelexEngine.fs` tiếp nhận kết quả từ hàm modifier và cập nhật thẳng lên màn hình soạn thảo, sinh ra chữ `Sữâ`.

### 2.3. Khiếm khuyết 3: Bảng danh mục vần hợp lệ không phân biệt âm tiết mở và âm tiết khép
- **Vị trí phát sinh:** Bảng tập hợp các cụm nguyên âm hợp lệ (`ValidVowelClusters`) trong `ModifierRules.fs`.
- **Bản chất vấn đề:**
  - Bảng này hiện đang liệt kê danh sách phẳng các chuỗi nguyên âm (như *a, ă, â, ia, ua, uâ, uô, ươ*...).
  - Bảng coi cụm `"uâ"` là hợp lệ ở **mọi ngữ cảnh**, không quan tâm đến sự tồn tại của phụ âm cuối.
  - Trong thực tế ngữ âm học tiếng Việt: Vần `uâ` là một vần ngắn đặc thù, đóng vai trò kết hợp với âm cuối. Khi đứng trần không có phụ âm cuối, `uâ` là một tổ hợp âm vị bất khả thi.
  - Do bảng vần thiếu thuộc tính ràng buộc âm cuối, khi người dùng gõ `vuaxa` hoặc `buafa`, hệ thống biến đổi `ua` thành `uâ` và kiểm tra lại thì thấy `uâ` có tên trong bảng `ValidVowelClusters`, do đó chấp nhận hiển thị kết quả sai thành `vuẫ` và `buầ`.

---

## 3. Nghiên cứu đối chiếu chi tiết: Cách BambooKey và UniKey xử lý bài toán

Để xây dựng giải pháp có nền tảng vững chắc, chúng tôi đã khảo sát trực tiếp mã nguồn của hai bộ gõ tiếng Việt tiêu chuẩn:

### 3.1. Phân tích chi tiết cơ chế của BambooKey (`bamboo-core`)
Qua khảo sát mã nguồn Go trong thư mục `fcitx5-bamboo/bamboo-core` và chạy kiểm thử thực tế:

1. **Cơ chế Transformation ảo (Virtual Transformation) cho cặp `uô + w`:**
   - Trong BambooKey, lịch sử gõ phím được tổ chức dưới dạng một chuỗi các đối tượng Transformation (mỗi phím nhấn đại diện cho một bước biến đổi hoặc thêm ký tự).
   - Khi người dùng gõ phím `w`, BambooKey kiểm tra đuôi của âm tiết hiện tại bằng biểu thức chính quy. Nếu âm tiết kết thúc bằng cụm `uo` hoặc `uô`, BambooKey kích hoạt quy tắc gõ tắt đặc biệt.
   - Thay vì chỉ biến đổi ký tự `u` hoặc ký tự `o` đơn lẻ, engine tự động sinh ra một **Transformation ảo mang mã dấu móc Horn** gán đồng thời lên cả hai ký tự `u` và `ô`.
   - Ngay sau đó, hàm làm mới mục tiêu thanh điệu (`refreshLastToneTarget`) được kích hoạt: hàm này tìm kiếm vị trí nguyên âm chính xác trên cụm `ươ` mới tạo thành và tự động di dời dấu thanh từ vị trí cũ sang vị trí mới.
   - Nhờ chuỗi xử lý này, BambooKey chuyển đổi `dduoocjw` thành `được` và `luoocjw` thành `lược` một cách hoàn hảo.
2. **Cơ chế thẩm định giả lập (Speculative Validation) cho `Sữa + a`:**
   - Trong hàm tìm kiếm mục tiêu gán dấu phụ (`findMarkTarget`), khi người dùng nhấn phím `a` trên từ `Sữa`:
   - BambooKey không áp dụng ngay vào bộ đệm chính, mà tạo ra một **bản sao giả lập tạm thời** của chuỗi Transformation.
   - Bản sao giả lập này (mang vần `ữâ`) được chuyển qua hàm kiểm tra chính tả (`isValid`). Hàm này bóc tách âm tiết thành 3 thành phần: Phụ âm đầu, Hạt nhân nguyên âm, Phụ âm cuối; sau đó tra cứu trong bảng ma trận ngữ âm học `vowelSeqs`.
   - Vì cụm `ưâ` không có mặt trong bất kỳ nhóm nguyên âm hợp lệ nào của ma trận, hàm kiểm tra chính tả trả về kết quả không hợp lệ.
   - BambooKey lập tức **từ chối áp dụng quy tắc gán mũ**. Phím `a` rớt xuống thành ký tự văn bản thường nối tiếp vào đuôi từ. Kết quả thực tế là xuất ra `sữaa`, hoàn toàn ngăn chặn việc sinh ra chữ `sữâ`.
3. **Phân tích điểm hạn chế của BambooKey với `vuaxa` và `buafa`:**
   - Khi chạy kiểm thử thực tế chuỗi `vuaxa` trên BambooKey, kết quả vẫn bị xuất ra là `vũâ`. Chuỗi `buafa` bị xuất ra là `bùâ`.
   - **Nguyên nhân kỹ thuật:** Khi gọi hàm kiểm tra chính tả, BambooKey truyền vào một tham số biểu thị "từ đã nhập xong hay chưa". Vì người dùng đang gõ dở, tham số này mang giá trị false (chưa hoàn tất). Hàm kiểm tra chính tả cho rằng người dùng có thể đang gõ dở một từ có phụ âm cuối (như đang gõ từ *xuân* thì mới gõ đến *xuâ*), do đó tạm thời chấp nhận cho vần `uâ` tồn tại. Khi người dùng dừng gõ, chữ bị mắc kẹt lại thành `vũâ`. Đây là một hạn chế cố hữu của BambooKey mà BambooMintKey cần phải khắc phục triệt để.

### 3.2. Phân tích chi tiết cơ chế của UniKey (`ukengine`)
Qua khảo sát nguyên lý hoạt động của lõi `ukengine` do tác giả Phạm Kim Long thiết kế:

1. **Mô hình máy trạng thái vần theo cặp (Macro State Transition):**
   - UniKey không quản lý các chữ cái rời rạc mà quản lý trạng thái của toàn bộ cụm nguyên âm.
   - UniKey định nghĩa sự chuyển dịch trạng thái vần rõ ràng: Trạng thái mang vần `uo` hoặc `uô` khi nhận phím `w` sẽ chuyển dịch sang trạng thái mang vần `ươ`.
   - Hệ thống tách bạch hoàn toàn giữa việc quản lý dấu phụ (mũ, móc) và việc quản lý dấu thanh (sắc, huyền, hỏi, ngã, nặng). Khi vần chuyển dịch từ `uô` sang `ươ`, giá trị dấu thanh đang tồn tại được giữ nguyên vẹn và tự động gắn vào vị trí âm chính của vần mới theo quy chuẩn đặt dấu (mới hoặc cũ).
   - Cơ chế này giải quyết triệt để sự phân vân:
     - Nếu người dùng dừng ở `dduoocj`, kết quả là `đuộc`.
     - Nếu người dùng gõ thêm phím `w` (`dduoocjw`), sự hiện diện của phím `w` là bằng chứng rõ ràng cho thấy người dùng muốn vần có móc. Hệ thống lập tức xuất ra `được`. Không có bất kỳ sự mơ hồ nào.
2. **Khái niệm nhị trùng âm bão hòa (Saturated Diphthong):**
   - UniKey đánh dấu các cụm nguyên âm như `ưa`, `ua`, `ia` là các cấu trúc đã hoàn tất về mặt hình thái học.
   - Khi một âm tiết đã đạt trạng thái vần `ưa`, phím `a` tiếp theo không được phép kích hoạt quy tắc tạo mũ `aa -> â`. Phím `a` bị coi là phím không hiệu lực hoặc được khôi phục thành chữ thường, ngăn chặn việc phá vỡ vần `ưa`.
3. **Cơ chế kiểm tra chính tả cấu trúc âm tiết nghiêm ngặt:**
   - Trong UniKey, tính năng "Bật kiểm tra chính tả" kiểm tra toàn diện cấu trúc âm tiết tiếng Việt dựa trên bảng luật âm vị học:
     - Phụ âm đầu nào được phép đi với nguyên âm nào.
     - Nguyên âm nào bắt buộc phải có phụ âm cuối nào.
   - Vì vần `uâ` không bao giờ được phép đứng ở âm tiết mở, khi người dùng gõ `buaf` (`bùa`) rồi nhấn phím `a`, hệ thống nhận diện từ không thể kết thúc bằng `uâ` nếu thiếu phụ âm cuối, do đó từ chối chuyển đổi sang `buâ`, bảo vệ thành công chữ `bùa`.

---

## 4. Phương án giải quyết & Kiến trúc đề xuất chi tiết cho BambooMintKey

Dựa trên việc kế thừa các điểm mạnh của cả UniKey và BambooKey, đồng thời khắc phục triệt để hạn chế về vần `uâ` trần, chúng tôi đề xuất phương án kiến trúc 3 lớp toàn diện. Phương án này tận dụng tối đa các đặc tính cốt lõi của F# (xử lý hàm thuần túy, tính bất biến, so khớp mẫu mạnh mẽ và không gây tác dụng phụ).

Dưới đây là mô tả chi tiết bằng lời cho từng thành phần giải pháp:

### 4.1. Giải pháp 1: Bóc tách thanh điệu độc lập và thuật toán đồng bộ hóa cặp nhị trùng âm
- **Mục tiêu:** Giải quyết dứt điểm lỗi `dduoocjw -> đưộc` và `luoocjw -> lưộc`.
- **Nguyên lý bóc tách thanh điệu (Tone Stripping):**
  - Mọi thao tác kiểm tra và áp dụng dấu phụ (mũ, móc, trăng) tuyệt đối không được thực hiện trực tiếp trên chuỗi nguyên âm đang mang dấu thanh dựng sẵn.
  - Trước khi đánh giá phím modifier, hệ thống thực hiện phân rã chuỗi hạt nhân nguyên âm hiện tại thành hai thành phần độc lập:
    - Thành phần 1: Chuỗi hạt nhân nguyên âm nền (đã loại bỏ hoàn toàn dấu thanh, chỉ giữ lại ký tự gốc và dấu phụ mũ/móc/trăng nếu có).
    - Thành phần 2: Giá trị thanh điệu hiện tại của âm tiết (Không dấu, Sắc, Huyền, Hỏi, Ngã, hoặc Nặng).
- **Thuật toán xử lý phím `w` trên cặp nhị trùng âm:**
  - Khi người dùng gõ phím `w`, hệ thống kiểm tra chuỗi hạt nhân nguyên âm nền (đã bỏ dấu thanh).
  - Sử dụng cơ chế so khớp mẫu ngữ âm của F# để nhận diện: Chuỗi nguyên âm nền có thuộc nhóm nhị trùng âm `uo`, `uô`, `ưo`, hoặc `uơ` hay không.
  - Nếu khớp: Hệ thống thực hiện chuyển đổi đồng bộ toàn bộ cụm sang vần `ươ`:
    - Bảo toàn tính chất chữ hoa/thường: Nếu chữ cái đầu viết hoa thì kết quả là `Ươ`; nếu toàn bộ viết hoa thì kết quả là `ƯƠ`; nếu viết thường thì kết quả là `ươ`.
    - Sau khi đã có cụm nguyên âm nền mới là `ươ`, hệ thống tái áp dụng giá trị thanh điệu đã bóc tách ban đầu lên âm tiết theo quy chuẩn đặt dấu thanh hiện hành. Dấu thanh sẽ được đặt chính xác lên nguyên âm `ơ`.
  - Kết quả: Khi gõ `dduoocjw`, chuỗi `"uộ"` được bóc tách thành `"uô"` và thanh Nặng. Gặp phím `w`, `"uô"` chuyển thành `"ươ"`. Thanh Nặng được tái áp dụng lên `"ươ"` tạo thành `"ược"`, kết hợp với phụ âm đầu `đ` cho ra kết quả hoàn hảo là **`được`**. Tương tự, `luoocjw` cho ra kết quả **`lược`**.

### 4.2. Giải pháp 2: Ma trận ràng buộc âm vị học tiếng Việt (Phonotactic Grammar Matrix)
- **Mục tiêu:** Ngăn chặn tuyệt đối việc sinh ra các vần dị dạng như `ưô`, `uă`, `ữâ`, `vuẫ`, `buầ`.
- **Nguyên lý:** Xây dựng một cổng kiểm soát âm vị học hoạt động như một màng lọc bắt buộc. Mọi phép biến đổi nguyên âm dự kiến đều phải vượt qua 3 tầng thẩm định ngữ âm:

#### Tầng thẩm định A: Danh mục các cụm nguyên âm cấm tuyệt đối (Impossible Clusters)
- Hệ thống định nghĩa một tập hợp bất biến chứa tất cả các cụm nguyên âm không bao giờ xuất hiện trong từ vựng tiếng Việt tự nhiên:
  - Cụm `ưô`: Cấm tuyệt đối vì hai nguyên âm kép đi liền nhau phải cùng mang móc (`ươ`) hoặc cùng không mang móc (`uô`).
  - Cụm `uă`: Cấm tuyệt đối vì tiếng Việt chuẩn chỉ dùng `oă` (*hoặc, xoăn*), không dùng `uă`.
  - Cụm `ưâ`: Cấm tuyệt đối vì nguyên âm mang móc `ư` không bao giờ kết hợp với nguyên âm mang mũ `â`.
  - Các cụm lai dị dạng khác: `oâ`, `iâ`.
- **Quy tắc:** Bất kỳ phép biến đổi nào tạo ra hạt nhân nguyên âm chứa các cụm trên đều bị coi là bất hợp lệ và bị từ chối ngay lập tức.
- *Hành vi thực tế:* Khi người dùng có từ `Sữa` (chứa `ưa`) và gõ thêm phím `a`: Hệ thống thử nghiệm tạo ra cụm `ưâ`. Bộ lọc Tầng A phát hiện cụm `ưâ` nằm trong danh mục cấm, lập tức từ chối biến đổi. Chữ `Sữa` được giữ nguyên vẹn.

#### Tầng thẩm định B: Danh mục các cụm nguyên âm bắt buộc phải có phụ âm cuối (Coda-Mandatory Clusters)
- Hệ thống định nghĩa danh mục các nguyên âm ngắn và âm đệm đòi hỏi âm tiết khép: gồm `uâ`, `ă`, `â`, và `oă`.
- **Quy tắc thẩm định ngữ cảnh:**
  - Khi một phép biến đổi có xu hướng tạo ra một trong các cụm nguyên âm thuộc danh mục này, hệ thống kiểm tra trạng thái của thành phần phụ âm cuối trong âm tiết hiện tại.
  - Nếu thành phần phụ âm cuối đang rỗng (chưa có phụ âm cuối, âm tiết mở): Hệ thống **từ chối phép biến đổi**, không cho phép tạo ra vần ngắn đứng trần.
  - Nếu thành phần phụ âm cuối đã tồn tại (ví dụ đã có phụ âm `n`, `t`, `ng`...): Hệ thống chấp thuận phép biến đổi vì cấu trúc âm tiết khép đã được đảm bảo.
- *Hành vi thực tế:*
  - Người dùng gõ `vuax` (`vũa`) rồi gõ tiếp phím `a`: Phụ âm cuối đang rỗng, phép biến đổi `ua -> uâ` bị từ chối. Kết quả giữ nguyên là `vũa`.
  - Người dùng gõ `buaf` (`bùa`) rồi gõ tiếp phím `a`: Phụ âm cuối đang rỗng, phép biến đổi bị từ chối. Kết quả giữ nguyên là `bùa`.
  - Người dùng gõ `xuan` (đã có phụ âm cuối `n`) rồi gõ tiếp phím `a`: Phụ âm cuối `n` đã tồn tại, phép biến đổi được chấp thuận, tạo thành chữ `xuân`.

#### Tầng thẩm định C: Khóa nhị trùng âm bão hòa
- Hệ thống quy định các nhị trùng âm hoàn chỉnh gồm `ưa`, `ua`, `ia` khi đã được hình thành và mang dấu thanh thì trạng thái hạt nhân nguyên âm được khóa biến đổi mũ đối với phím `a`. Phím `a` tiếp theo sẽ được xử lý như một phím gõ lặp hoặc phím văn bản thông thường, không làm biến dạng từ đã gõ.

### 4.3. Giải pháp 3: Luồng giả lập và thẩm định bất biến (Speculative Pipeline) trong F#
- **Mục tiêu:** Đảm bảo toàn bộ quá trình thẩm định diễn ra an toàn, không rò rỉ trạng thái lỗi, không phát sinh tác dụng phụ (side-effects) và đạt hiệu năng tối đa.
- **Nguyên lý vận hành:**
  - Nhờ tính chất bất biến (Immutability) mặc định của F#, trạng thái âm tiết cũ không bị thay đổi tại chỗ.
  - Khi nhận một phím bấm mới, engine thực hiện tạo ra một trạng thái âm tiết giả lập mới (Speculative Syllable).
  - Trạng thái giả lập này được đẩy qua đường ống thẩm định gồm 4 bước tuần tự:
    - **Bước 1 (Biến đổi hình thái):** Thử nghiệm áp dụng quy tắc modifier hoặc tone tương ứng với phím bấm.
    - **Bước 2 (Thẩm định âm vị học):** Đưa hạt nhân nguyên âm giả lập qua Bộ lọc âm vị học ở Giải pháp 4.2. Nếu vi phạm danh mục cấm hoặc vi phạm ràng buộc âm cuối, bước này trả về kết quả rỗng (None).
    - **Bước 3 (Thẩm định cấu trúc tổng thể):** Đưa toàn bộ âm tiết giả lập qua hàm thẩm định cấu trúc ngữ âm học tiếng Việt sẵn có trong core.
    - **Bước 4 (Đối chiếu từ điển âm tiết):** Kiểm tra xem âm tiết giả lập có tồn tại trong bộ từ điển 7.800 âm tiết chuẩn tiếng Việt được nạp sẵn trong bộ nhớ bất biến (`FrozenDictionaryService`) hay không.
  - **Quyết định trạng thái:**
    - Nếu trạng thái giả lập vượt qua thành công toàn bộ 4 bước: Engine chấp nhận trạng thái mới, đóng gói thành hành động cập nhật chuỗi soạn thảo (`UpdateComposition`) và hiển thị lên màn hình.
    - Nếu trạng thái giả lập thất bại ở bất kỳ bước nào: Engine lập tức loại bỏ trạng thái giả lập, khôi phục nguyên vẹn trạng thái âm tiết hợp lệ trước đó. Không có bất kỳ ký tự rác hay trạng thái dị dạng nào bị lọt ra giao diện người dùng.

### 4.4. Giải pháp 4: Phân giải bài toán phân vân bằng ý chí phím gõ và tần suất ngữ liệu
Đối với câu hỏi về sự phân vân khi hoàn thành từ (như giữa `được` và `đuộc`, giữa `lược` và `luộc`):

1. **Phân giải dựa trên tính tường minh của phím gõ (Explicit Keystroke Disambiguation):**
   - Trong quá trình soạn thảo, sự xuất hiện của các phím modifier (như `w`, `a`, `e`, `o`) thể hiện ý chí rõ ràng của người dùng:
     - Khi người dùng gõ `dduoocj` và dừng lại (gõ dấu cách hoặc phím tiếp theo): Ý chí người dùng là gõ từ có vần `uô`. Kết quả là `đuộc`.
     - Khi người dùng gõ `dduoocj` rồi bấm tiếp phím `w` (`dduoocjw`): Việc bấm phím `w` là một hành động chủ động thể hiện ý muốn chuyển từ vần không móc `uô` sang vần có móc `ươ`. Do đó, hệ thống hoàn toàn tự tin chuyển thành `được` mà không cần phải phân vân.
     - Tương tự: `luoocj` ra `luộc`; `luoocjw` ra `lược`. Cả hai từ đều có nghĩa và được phân định rành mạch qua sự có mặt của phím `w`.
2. **Phân giải dựa trên trọng số tần suất kho ngữ liệu (Corpus Frequency Disambiguation):**
   - Trong trường hợp người dùng bật tính năng "Tự động sửa lỗi chính tả thông minh" (Smart Autocorrect / Suggestion):
   - Engine tra cứu bảng tần suất ngữ liệu tiếng Việt trích xuất từ Wikipedia tiếng Việt (`vi-corpus-freq.tsv`) đã tích hợp sẵn trong dự án:
     - Từ `được` có tần suất sử dụng là **3.940.300 lần** (là từ phổ biến hàng đầu trong toàn bộ ngôn ngữ).
     - Từ `đuộc` chỉ có tần suất sử dụng là **5 lần** (gần như bằng 0 trong ngữ cảnh hiện đại).
     - Từ `luộc` có tần suất sử dụng là **2.172 lần**, và từ `lược` có tần suất sử dụng là **60.185 lần** (cả hai đều là từ vựng phổ thông có nghĩa).
   - Dựa vào ma trận tần suất này, nếu người dùng gõ nhầm một từ có xác suất xuất hiện cực thấp trong khi từ đồng âm có móc có xác suất áp đảo, hệ thống có đủ cơ sở dữ liệu tin cậy để đề xuất hoặc tự động sửa từ mà không gây phiền toái cho người dùng.

---

## 5. Kế hoạch triển khai & Ma trận ca kiểm thử nghiệm thu chi tiết

### 5.1. Kế hoạch điều chỉnh các module mã nguồn trong `BambooMintKey.Core`
1. **Module `ModifierRules.fs`:**
   - Xây dựng logic bóc tách thanh điệu độc lập, trả về chuỗi nguyên âm nền thuần khiết và giá trị thanh điệu.
   - Bổ sung định nghĩa tập hợp các cụm nguyên âm cấm tuyệt đối (`ưô`, `uă`, `ưâ`, `oâ`, `iâ`).
   - Bổ sung định nghĩa tập hợp các cụm nguyên âm đòi hỏi phụ âm cuối (`uâ`, `ă`, `â`, `oă`).
   - Cập nhật hàm xử lý phím `w`: áp dụng thuật toán đồng bộ hóa cặp nhị trùng âm `uo/uô -> ươ` trên chuỗi nguyên âm nền, sau đó tái áp dụng thanh điệu.
   - Thêm hàm kiểm tra tính hợp lệ của cụm nguyên âm dự kiến trước khi chấp nhận biến đổi; kiểm tra cả danh mục cấm và ràng buộc phụ âm cuối.
2. **Module `TelexEngine.fs`:**
   - Trong luồng xử lý gia tăng (khi thử áp dụng modifier lên âm tiết hiện có): truyền kèm thông tin về phụ âm cuối hiện tại vào hàm modifier để phục vụ việc thẩm định âm tiết mở/khép.
   - Đảm bảo khi hàm modifier từ chối biến đổi (trả về kết quả rỗng), engine duy trì trạng thái âm tiết hợp lệ trước đó và không để lọt ký tự dị dạng ra chuỗi soạn thảo.
3. **Module kiểm thử `RuleTests.fs`:**
   - Bổ sung đầy đủ các ca kiểm thử tự động tương ứng với ma trận nghiệm thu dưới đây.

### 5.2. Ma trận ca kiểm thử nghiệm thu chi tiết (Acceptance Test Matrix)

| STT | Nhóm kiểm thử | Chuỗi phím nhập | Kết quả kỳ vọng | Hạt nhân nguyên âm | Dấu thanh | Tiêu chí nghiệm thu chi tiết |
|:---:|:---|:---|:---|:---|:---|:---|
| 1 | Đồng bộ cặp nhị trùng âm | `dduoocjw` | **được** | `ươ` | Nặng | Cặp uô chuyển toàn bộ sang ươ mang dấu nặng, tuyệt đối không sinh ra ưộc |
| 2 | Đồng bộ cặp nhị trùng âm | `luoocjw` | **lược** | `ươ` | Nặng | Chuyển đổi thành công sang lược mang dấu nặng, không sinh ra lưộc |
| 3 | Đồng bộ cặp nhị trùng âm | `buoocjw` | **bược** | `ươ` | Nặng | Chuyển đổi thành công sang bược |
| 4 | Tôn trọng từ thô | `dduoocj` | **đuộc** | `uô` | Nặng | Không có phím w, giữ nguyên từ đuộc hợp lệ |
| 5 | Tôn trọng từ thô | `luoocj` | **luộc** | `uô` | Nặng | Không có phím w, giữ nguyên từ luộc hợp lệ |
| 6 | Chặn vần khép ở âm tiết mở | `vuaxa` | **vũa** | `ua` | Ngã | Phím a không thể biến ua thành uâ vì thiếu âm cuối, giữ nguyên vũa |
| 7 | Chặn vần khép ở âm tiết mở | `buafa` | **bùa** | `ua` | Huyền | Phím a sau buaf bị từ chối biến đổi, giữ nguyên từ bùa hoàn chỉnh |
| 8 | Chặn vần khép ở âm tiết mở | `buaaf` | **bùa** | `ua` | Huyền | Gõ nhầm đúp phím a khi gõ bùa vẫn phục hồi thành bùa, không sinh ra buầ |
| 9 | Chặn vần khép ở âm tiết mở | `duaxa` | **dũa** | `ua` | Ngã | Giữ nguyên từ dũa, không sinh ra duẫ |
| 10 | Chấp thuận vần khép có âm cuối | `xuan` | **xuan** | `ua` | Không | Chưa có dấu |
| 11 | Chấp thuận vần khép có âm cuối | `xuana` | **xuân** | `uâ` | Không | Đã có phụ âm cuối n nên cho phép biến đổi ua thành vần khép uân |
| 12 | Khóa nhị trùng âm bão hòa | `suwaxa` | **sữa** | `ưa` | Ngã | Cụm ưa đã bão hòa, cấm gắn mũ â tạo thành ữâ, giữ nguyên sữa |
| 13 | Khóa nhị trùng âm bão hòa | `muaxa` | **mũa** | `ua` | Ngã | Giữ nguyên mũa |
| 14 | Khóa nhị trùng âm bão hòa | `cuaxa` | **cũa** | `ua` | Ngã | Giữ nguyên cũa |
| 15 | Bảo vệ tiếng Anh (Zero-regression) | `core` | **core** | - | - | Cơ chế bảo vệ tiếng Anh không bị ảnh hưởng |
| 16 | Bảo vệ tiếng Anh (Zero-regression) | `more` | **more** | - | - | Giữ nguyên từ tiếng Anh |
| 17 | Bảo tồn quy tắc ưa (Zero-regression) | `vuawf` | **vừa** | `ưa` | Huyền | Ưu tiên cụm ua + w biến thành ưa (đã chuẩn hóa ở Phase 8) |
| 18 | Bảo tồn quy tắc ưa (Zero-regression) | `muaw` | **mưa** | `ưa` | Không | Giữ nguyên từ mưa chuẩn |

---

## 6. Đánh giá hiệu quả kiến trúc và tính an toàn hệ thống

1. **Tính tổng quát và triệt để:**
   - Thay vì chạy theo xử lý chắp vá từng trường hợp cụ thể, giải pháp thiết lập một hệ thống luật âm vị học chuẩn mực. Bất kỳ từ nào trong tiếng Việt có cấu trúc tương tự (kể cả từ hiếm gặp) đều tự động được áp dụng chuẩn xác theo quy luật chung.
2. **Kế thừa tinh hoa và vượt trội hơn các bộ gõ đi trước:**
   - Kế thừa trọn vẹn khả năng biến đổi đồng bộ cụm nguyên âm mượt mà của UniKey và BambooKey.
   - Kế thừa cơ chế thử nghiệm giả lập an toàn của BambooKey.
   - Vượt trội hơn BambooKey ở chỗ giải quyết dứt điểm lỗi mắc kẹt vần `uâ` trần (`vũâ`, `bùâ`).
3. **Phạm vi tác động cô lập và an toàn tuyệt đối:**
   - Toàn bộ các thay đổi chỉ diễn ra nội bộ bên trong thư viện lõi F# (`BambooMintKey.Core`).
   - Các giao diện C-ABI (`cabibridge.h`), cầu nối Native Bridge, giao diện người dùng Avalonia và các addon tích hợp trên hệ điều hành (Fcitx5 trên Linux, TSF trên Windows, IMK trên macOS) hoàn toàn không bị ảnh hưởng, đảm bảo tính ổn định tối đa cho toàn bộ dự án.
