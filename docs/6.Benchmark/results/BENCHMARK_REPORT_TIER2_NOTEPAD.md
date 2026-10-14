# Báo Cáo Phân Tích Thực Nghiệm Benchmark Tầng 2: Notepad Integration

> **Tài liệu kiểm thử thực nghiệm Black-box trên Windows 11 (Notepad.exe)**  
> **Bộ kịch bản:** `passage_500.cases` (Tổng cộng **583 từ**, gồm **495 từ tiếng Việt** và **88 từ tiếng Anh xen kẽ**)  
> **Các mốc độ trễ:** 8 nấc từ **60ms** (16.7 phím/s) xuống **5ms** (200.0 phím/s)  
> **Bộ gõ tham gia:** **BambooMintKey** (TSF), **EVKey** (Hook), **UniKey** (Hook), **OpenKey** (Hook), **Windows Built-in Telex** (TSF)  
> **Tổng số lượt chạy:** **40 runs** độc lập, ghi nhận dữ liệu tại `docs/6.Benchmark/results/benchmark_all.csv` và log chi tiết tại `docs/6.Benchmark/results/logs/v2/`.

---

## 1. Tóm Tắt Kết Quả Then Chốt (Executive Summary)

1. **BambooMintKey giành vị trí Quán quân toàn diện:**
   - **Độ chính xác tổng thể cao nhất:** **98.80%** (576/583 từ đúng, chỉ sai duy nhất 7 từ tiếng Anh).
   - **Khả năng bảo vệ từ tiếng Anh (Auto-restore) dẫn đầu:** **92.05%** (81/88 từ), vượt qua UniKey & EVKey (90.91%), OpenKey (87.50%) và bỏ xa Windows Telex (68.18%).
   - **Tốc độ gõ thực tế cao nhất:** Đạt đỉnh **244.6 WPM** ở nhịp 5ms (200 keys/giây).
   - **Xử lý đặc biệt xuất sắc:** BambooMintKey xử lý đúng từ `loop;`, trong khi tất cả các bộ gõ khác (UniKey, EVKey, OpenKey, Windows Telex) đều bị biến dạng thành `lôp;`.

2. **Độ chính xác tiếng Việt đạt mức tuyệt đối (100.0% Across All IMEs):**
   - Tất cả 5 bộ gõ đều gõ đúng toàn bộ **495/495 từ tiếng Việt**, chứng minh engine chính tả của các bộ gõ trên thị trường hiện nay đều xử lý rất tốt ngữ âm tiếng Việt thuần.

3. **Tính Tất Định Tuyệt Đối (100% Deterministic & Zero Swallowed Keys):**
   - Dù tăng tốc độ bơm phím gấp 12 lần (từ 16.7 phím/giây lên tới 200 phím/giây), **số lượng và danh sách từ lỗi của từng bộ gõ không hề thay đổi dù chỉ 1 từ**.
   - Không xuất hiện bất kỳ hiện tượng nuốt phím (swallowed keys) hay đảo thứ tự phím (transposed characters) nào trên môi trường Win32 Notepad. Toàn bộ lỗi 100% là lỗi thuật toán xử lý từ tiếng Anh và quy tắc thanh điệu.

4. **Hiện tượng Bão hòa Tốc độ (Saturation Wall) tại ngưỡng 10ms - 5ms:**
   - Từ 60ms xuống 10ms (100 keys/s), WPM tăng tuyến tính từ ~107 WPM lên ~242 WPM.
   - Khi tăng tốc độ bơm từ 10ms lên 5ms (tăng gấp đôi số phím phát ra mỗi giây), WPM gần như đi ngang (BambooMintKey chỉ tăng thêm +1.9 WPM). Đây là giới hạn xử lý hàng đợi sự kiện (Message Queue) và tốc độ dựng giao diện (Repaint rate) của Windows UI/Notepad.

---

## 2. Bảng Xếp Hạng & Chỉ Số Tổng Hợp (Leaderboard)

| Hạng | Bộ Gõ (IME) | Kiến Trúc | Tổng Accuracy (%) | VN Accuracy (495 từ) | EN Accuracy (88 từ) | Số Từ Lỗi (VN / EN) | WPM Trung Bình | WPM Tối Đa (Peak) | Độ Lệch Chuẩn WPM |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 🥇 **1** | **BambooMintKey** | **TSF** | **98.80%** | **100.0%** | **92.05%** | **0 / 7** | **188.59** | **244.6** | 53.10 |
| 🥈 **2** | **EVKey** | Hook | 98.63% | 100.0% | 90.91% | 0 / 8 | 185.23 | 243.0 | 52.06 |
| 🥉 **3** | **UniKey** | Hook | 98.63% | 100.0% | 90.91% | 0 / 8 | 182.69 | 233.8 | 49.82 |
| **4** | **OpenKey** | Hook | 98.11% | 100.0% | 87.50% | 0 / 11 | 181.86 | 236.8 | 50.31 |
| **5** | **Windows Built-in Telex** | TSF | 95.20% | 100.0% | 68.18% | 0 / 28 | 188.13 | 236.8 | 51.80 |

---

## 3. Hệ Thống Biểu Đồ Trực Quan (Visual Analytics)

### 3.1. Tốc Độ WPM Theo Tốc Độ Bơm Phím (60ms &rarr; 5ms)
![Tốc độ WPM theo Delay](charts/01_wpm_vs_delay.png)
*Biểu đồ 1: Thể hiện độ dốc tăng trưởng tốc độ từ 60ms đến 10ms và vùng bão hòa từ 10ms đến 5ms.*

### 3.2. So Sánh Độ Chính Xác Tổng Thể & Tiếng Anh
![Độ chính xác](charts/02_accuracy_breakdown.png)
*Biểu đồ 2: Toàn bộ 5 bộ gõ đều đạt 100% tiếng Việt, sự phân hóa đẳng cấp nằm hoàn toàn ở tính năng bảo vệ từ tiếng Anh.*

### 3.3. Số Lượng Từ Lỗi Phân Loại Theo Bộ Gõ
![Số lượng từ lỗi](charts/03_failed_words_comparison.png)
*Biểu đồ 3: BambooMintKey có ít lỗi nhất (7 từ), tiếp theo là UniKey/EVKey (8 từ), OpenKey (11 từ), và Windows Telex (28 từ).*

### 3.4. Đánh Giá Toàn Diện 5 Chiều (Radar Chart)
![Radar Chart](charts/04_radar_comparison.png)
*Biểu đồ 4: So sánh 5 chiều: Tổng Accuracy, Độ đúng VN, Bảo vệ EN, WPM Đỉnh, và Kiến trúc TSF hiện đại.*

> [!TIP]
> Bạn có thể mở trực tiếp trang [Interactive Dashboard HTML](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/dashboard.html) trên trình duyệt để tương tác lọc lỗi, chuyển đổi tab và xem biểu đồ động.

---

## 4. Ma Trận Đối Chiếu Chi Tiết Toàn Bộ Các Từ Bị Lỗi (Error Deep Dive)

Dưới đây là bảng đối chiếu từng từ lỗi cụ thể được trích xuất từ 40 file log tại `docs/6.Benchmark/results/logs/v2/`:

| STT | Chuỗi phím đầu vào | Kết quả mong đợi | BambooMintKey | UniKey / EVKey | OpenKey | Windows Telex | Phân tích nguyên nhân kỹ thuật |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **1** | `buffer,` | `buffer,` | ❌ `bufer,` | ❌ `bufer,` | ❌ `bufer,` | ❌ `bufer,` | Phụ âm kép `ff` bị cơ chế Telex nuốt thành 1 chữ `f` kèm dấu phẩy `,` ngắt từ. |
| **2** | `test,` | `test,` | ❌ `tét,` | ❌ `tét,` | ❌ `tét,` | ❌ `tét,` | Phím `s` đóng vai trò phụ âm trong `st`, nhưng bị engine tạm gán làm dấu sắc cho `te` &rarr; `tét,`. |
| **3** | `post,` | `post,` | ❌ `pót,` | ❌ `pót,` | ❌ `pót,` | ❌ `pót,` | Tương tự `test`, phím `s` trong `st` biến `po` thành `pót,`. |
| **4** | `loop;` | `loop;` | ✅ `loop;` | ❌ `lôp;` | ❌ `lôp;` | ❌ `lôp;` | **Điểm sáng của BambooMintKey!** Ký tự `;` ngắt từ khiến UniKey/EVKey/OpenKey/Windows biến `oo` &rarr; `ô`. Chỉ BambooMintKey giữ đúng `loop;`. |
| **5** | `[Shift+']buffer[Shift+'],` | `"buffer",` | ❌ `"bufer",` | ❌ `"bufer",` | ❌ `"bufer",` | ❌ `"bufer",` | Nuốt phụ âm lặp `ff` khi nằm trong cặp dấu ngoặc kép `"..."`. |
| **6** | `[Shift+']case[Shift+']` | `"case"` | ✅ `"case"` | ✅ `"case"` | ❌ `"case\|"` | ❌ `"cáe"` | OpenKey bị kẹt ký tự pipe `\|`, Windows Telex bị `s` &rarr; dấu sắc `cáe`. BambooMintKey & UniKey xử lý chuẩn. |
| **7** | `([Shift+a]uto-restore)` | `(Auto-restore)` | ✅ `(Auto-restore)` | ✅ `(Auto-restore)` | ❌ `(Auto-rétore)` | ❌ `(Auto-rétore)` | OpenKey & Windows Telex bị phím `s` biến `re` thành `ré` &rarr; `rétore`. BambooMintKey khôi phục hoàn hảo. |
| **8** | `issue,` | `issue,` | ❌ `isue,` | ❌ `isue,` | ❌ `isue,` | ❌ `isue,` | Phụ âm kép `ss` bị cơ chế Telex coi là phím lặp hủy dấu, nuốt thành 1 chữ `s`. |
| **9** | `offline,` | `offline,` | ❌ `ofline,` | ❌ `ofline,` | ❌ `ofline,` | ❌ `ofline,` | Tương tự `buffer`, phụ âm kép `ff` bị nuốt thành `f`. |
| **10** | `([Shift+w][Shift+p][Shift+m]).` | `(WPM).` | ✅ `(WPM).` | ✅ `(WPM).` | ❌ `(ƯPM).` | ❌ `(ƯPM).` | Phím `W` viết hoa bị OpenKey và Windows Telex biến thành `Ư`. BambooMintKey & UniKey giữ đúng `WPM`. |
| **11** | `[Shift+o]ffice,` | `Office,` | ❌ `Ofice,` | ❌ `Ofice,` | ❌ `Ofice,` | ❌ `Ofice,` | Chữ hoa đầu từ kèm phụ âm kép `ff` bị nuốt thành `Ofice,`. |
| **12-28** | 17 từ tiếng Anh khác (`terminal,`, `Discord`, `system,`, `software,`, `hardware,`, `database,`, `server,`, `case,`, `merge,`, `push,`, `hook,`, `default,`, `Text`, `Services`, `Windows,`, `Photoshop,`, `(Mixed`) | Từ gốc tiếng Anh | ✅ **ĐÚNG 17/17** | ✅ **ĐÚNG 17/17** | ✅ **ĐÚNG 17/17** | ❌ **SAI CẢ 17 TỪ** | Windows Telex không có cơ chế khôi phục từ ngoại lai, biến thành: `tẻminal,`, `Dícord`, `sýtem,`, `sờtare,`, `hảdware,`, `dâtbase,`, `sẻver,`, `cáe,`, `mẻge,`, `púh,`, `hôk,`, `dèault,`, `Tẽt`, `Sẻvices`, `Ưindows,`, `Phốthop,`, `(Mĩed`. |

---

## 5. Phân Tích Kiến Trúc Kỹ Thuật (TSF vs. Win32 Hook)

### 5.1. Cơ Chế Thao Tác Bộ Đệm Nhập Liệu
* **Win32 Hook (`WH_KEYBOARD_LL` - UniKey, EVKey, OpenKey):**
  - Khi người dùng gõ phím, hook chặn phím ở cấp OS, tính toán từ tiếng Việt trong bộ nhớ riêng của tiến trình IME.
  - Khi cần biến đổi chữ (ví dụ: gõ `t-o-a-n-s` &rarr; `toán`), bộ gõ Hook phải phát sinh chuỗi phím `Backspace` giả lập (bằng `SendInput` hoặc `keybd_event`) để xóa lùi 4 ký tự `toan` cũ trong Notepad, rồi mới bơm chuỗi ký tự Unicode mới `toán` vào.
  - Thao tác này tạo ra **gấp 2 đến 3 lần số lượng Windows Message (`WM_KEYDOWN`, `WM_CHAR`, `WM_KEYUP`)** đổ dồn vào Message Loop của ứng dụng.
* **Text Services Framework (TSF - BambooMintKey, Windows Built-in):**
  - TSF giao tiếp trực tiếp với Text Store của ứng dụng thông qua interface COM `ITfContext` và `ITfInsertAtSelection`.
  - Thay vì phát Backspace giả lập, BambooMintKey thay thế trực tiếp đoạn composition string ngay trong buffer của Edit Control. Nhờ đó, giảm thiểu tối đa overhead và giúp BambooMintKey đạt đỉnh **244.6 WPM** mà không gây giật lag con trỏ.

### 5.2. An Toàn, Ứng Dụng Hiện Đại & Game Anti-Cheat
* Các bộ gõ dùng Hook thường bị các hệ thống bảo mật (Windows UAC, Sandbox, UWP, Chrome sandbox) hoặc các phần mềm chống gian lận trong game (Easy Anti-Cheat, BattlEye, Vanguard) chặn hoặc cô lập, dẫn tới hiện tượng không gõ được tiếng Việt hoặc gây crash.
* BambooMintKey xây dựng trên chuẩn **TSF thuần túy**, được Windows công nhận là Input Method Provider hợp lệ, tương thích tự nhiên với mọi ứng dụng từ Win32 truyền thống đến WinUI 3, UWP, Terminal và Game đồ họa.

---

## 6. Lộ Trình Tối Ưu Hóa Kỹ Thuật Cho BambooMintKey (Đưa Từ 98.80% Lên 100%)

Với kết quả thực nghiệm chỉ còn **7 từ lỗi duy nhất**, chúng ta hoàn toàn có thể đưa BambooMintKey lên mức **100% độ chính xác hoàn hảo** thông qua 2 nâng cấp cụ thể trong engine:

### 6.1. Xử Lý Phụ Âm Kép Tiếng Anh (Khắc phục 5 từ lỗi: `buffer`, `"buffer"`, `issue`, `offline`, `office`)
* **Vấn đề:** Khi gặp phụ âm kép `ff`, `ss`, engine đang áp dụng luật Telex lặp phím làm triệt tiêu 1 ký tự.
* **Giải pháp:**
  - Trong cấu trúc Trie kiểm tra từ điển tiếng Anh của BambooMintKey, bổ sung cờ kiểm tra phụ âm kép (`ff`, `ss`, `ll`, `tt`, `pp`, `gg`, `cc`, `mm`, `nn`, `rr`).
  - Khi chuỗi phím hình thành một từ tiếng Anh hợp lệ trong Trie có chứa phụ âm kép và theo sau là dấu ngắt từ (dấu phẩy, dấu chấm, ngoặc kép, khoảng trắng), engine sẽ **giữ nguyên toàn bộ phụ âm kép** thay vì nuốt phím.

### 6.2. Xử Lý Đuôi Vần `st` Cuối Từ (Khắc phục 2 từ lỗi: `test`, `post`)
* **Vấn đề:** Phím `s` vừa là phụ âm trong vần `st`, vừa là phím dấu sắc. Vì tiếng Việt không có phụ âm đôi kết thúc từ bằng `st`, engine ưu tiên gán `s` làm dấu sắc cho nguyên âm đứng trước (`te` &rarr; `tét`, `po` &rarr; `pót`).
* **Giải pháp:**
  - Bổ sung quy tắc **Deferred Tone for `st` cluster**: Khi gặp từ kết thúc bằng `st` (`test`, `post`, `fast`, `cost`, `first`, `list`, `best`, `west`, `rest`, `cast`, `past`), nếu sau chữ `t` người dùng gõ phím ngắt từ hoặc phím cách, engine tự động kích hoạt luật hoàn tác Auto-restore:
    $$\text{`tét`} + \text{boundary} \longrightarrow \text{`test`}$$
    $$\text{`pót`} + \text{boundary} \longrightarrow \text{`post`}$$

---

## 7. Tài Nguyên & Liên Kết Tham Khảo

- **Dashboard HTML Trực Quan Tương Tác:** [dashboard.html](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/dashboard.html)
- **Tệp dữ liệu CSV tổng hợp:** [benchmark_all.csv](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/benchmark_all.csv)
- **Thư mục chứa 40 file log chi tiết:** [docs/6.Benchmark/results/logs/v2/](file:///D:/Kojin/BambooMintKey/docs/6.Benchmark/results/logs/v2/)
- **Kịch bản sinh biểu đồ tự động:** [generate_benchmark_visuals.py](file:///D:/Kojin/BambooMintKey/scripts/benchmark/generate_benchmark_visuals.py)
- **Kịch bản chạy benchmark Tầng 2:** [tier2_sendinput_runner.ps1](file:///D:/Kojin/BambooMintKey/scripts/benchmark/tier2_sendinput_runner.ps1)
