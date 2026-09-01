# Báo Cáo Phân Loại Lỗi & Phân Tích Nguyên Nhân (Giai Đoạn 1 - Vòng 4)
**Mã tài liệu:** `001_07_Issues_v4`  
**Dựa trên:** Kết quả chạy kiểm thử toàn diện `Core_v2.log` (Tổng: 119 test cases, Đạt: 98, Thất bại: 21)  
**Ngày cập nhật:** 01/09/2026

---

## 1. Tóm Tắt Tình Hình Kiểm Thử (Executive Summary)

Sau khi hoàn thành 4 tài liệu thiết kế chi tiết và triển khai các bước nền tảng (001_07_01 đến 001_07_04):
- **Tiến độ vượt bậc:** Số lượng test thất bại đã giảm mạnh từ **35 lỗi xuống còn 21 lỗi** (đạt tỷ lệ vượt qua **82.4%**).
- **Các thành phần đã pass 100%:**
  - `SyllableParserTests`: Phân rã cấu trúc âm tiết, bóc tách phụ âm đầu/cuối, loại bỏ âm tiết rỗng nguyên âm (`k`, `nghm`, `abc` $\rightarrow$ `None`).
  - `EnglishFallbackTests`: Nhận diện từ tiếng Anh không dấu và luồng pass-through khi tắt engine (`IsEnabled = false`).
  - Toàn bộ các quy tắc gõ Telex cơ bản (`aweod`) và bảo toàn chữ hoa/thường đơn giản.
- **Hiện trạng 21 lỗi còn lại:** Tập trung chủ yếu vào 5 nhóm nguyên nhân cụ thể, trong đó lỗi đảo ngược chỉ số đặt dấu mở chiếm tới 76% (16/21 lỗi).

---

## 2. Bảng Thống Kê Phân Loại 21 Lỗi Còn Lại

| STT | Nhóm Lỗi | Số Lỗi | Ví Dụ Input | Kết Quả Thực Tế (Actual) | Kết Quả Mong Đợi (Expected) | Mức Độ |
| :---: | :--- | :---: | :--- | :--- | :--- | :---: |
| **1** | **Vị trí dấu nguyên âm mở (Modern vs Traditional)** | **16** | `hoas`, `xoef`, `thuys`, `hoar`, `xoex`, `thuyj`, `HOAS`, `Thuys`, `thUyS` | `hoá`, `xoè`, `thuý` (ngược chỉ số giữa 2 chế độ) | `hóa`, `xòe`, `thúy` (Modern) & `hoá`, `xoè`, `thuý` (Traditional) | Nghiêm trọng |
| **2** | **Cờ Undo bắt nhầm Modifier kép `uwow`** | **2** | `uwow`, `tuwowr` | `uwow`, `tuwowr` (bị văng ra chuỗi thô) | `ươ`, `tưở` | Trung bình |
| **3** | **Trọng âm tam nguyên âm `ươu` (`ruowuj`)** | **1** | `ruowuj` | `rựơu` (dấu nặng đặt nhầm vào `ư`) | `rượu` (dấu bắt buộc đặt vào `ơ`) | Trung bình |
| **4** | **Quy tắc thanh điệu âm tắc cuối `c, p, t, ch`** | **1** | `thuyesft` | `thuyèt` (nhận dấu huyền sai chính tả) | `thuyết` (tự động điều chỉnh về thanh sắc) | Thấp |
| **5** | **Ánh xạ Mixed Case từ chuỗi thô co ngắn** | **1** | `vIeeTj` | `vIệt` (mất chữ `T` viết hoa cuối) | `vIệT` | Thấp |

---

## 3. Phân Tích Nguyên Nhân Kỹ Thuật (Root Causes)

### 3.1. Nhóm 1: Đảo ngược chỉ số Index nguyên âm mở (`ToneRules.fs`) - 16 lỗi
* **Hiện tượng:** 
  - Khi gõ `hoas` ở chế độ `Modern`, thực tế ra `hoá` (mong đợi `hóa`).
  - Khi gõ `hoas` ở chế độ `Traditional`, thực tế ra `hóa` (mong đợi `hoá`).
* **Nguyên nhân gốc rễ:** 
  - Trong cụm 2 nguyên âm mở `"oa"`, `"oe"`, `"uy"`:
    - Ký tự thứ nhất (`'o'`, `'u'`) có index mảng là `0` $\rightarrow$ Đây chính là kiểu **Modern** (`hóa`, `xòe`, `thúy`).
    - Ký tự thứ hai (`'a'`, `'e'`, `'y'`) có index mảng là `1` $\rightarrow$ Đây chính là kiểu **Traditional** (`hoá`, `xoè`, `thuý`).
  - Trong file `ToneRules.fs`, nhánh so khớp đang gán: `Modern -> 1` và `Traditional -> 0` (bị gán ngược logic định vị).
* **Giải pháp:** Sửa lại quy tắc định vị trong `ToneRules.fs`:
  ```fsharp
  match style with
  | TonePlacementStyle.Modern -> 0       // Dấu trên nguyên âm đầu: hóa, xòe, thúy
  | TonePlacementStyle.Traditional -> 1  // Dấu trên nguyên âm sau: hoá, xoè, thuý
  ```

---

### 3.2. Nhóm 2: Cờ `isUndoModifier` chặn nhầm tổ hợp `uwow` (`TelexEngine.fs`) - 2 lỗi
* **Hiện tượng:** Gõ `uwow` hoặc `tuwowr` bị coi là từ tiếng Anh/chuỗi thô thay vì chuyển thành `ươ`/`tưở`.
* **Nguyên nhân gốc rễ:** 
  - Khi người dùng gõ chuỗi `u` $\rightarrow$ `w` (thành `ư`) $\rightarrow$ `o` (thành `ươ`), sau đó gõ tiếp `w` thứ 2 trong `uwow`:
  - `isUndoModifier` phát hiện ký tự vừa gõ là `'w'` và trong từ đã chứa `"ươ"`, nên ngỡ rằng người dùng đang muốn gõ lặp phím `w` để **hủy dấu móc** $\rightarrow$ kích hoạt cờ Undo và xuất chuỗi thô `"uwow"`.
* **Giải pháp:** 
  - Chỉ kích hoạt `isUndoModifier` cho `'w'` khi chuỗi trước đó đã kết thúc bằng `'w'` hoặc phím gõ không nằm trong tổ hợp `uwow`.

---

### 3.3. Nhóm 3: Vị trí trọng âm trong cụm `ươu` (`ToneRules.fs`) - 1 lỗi
* **Hiện tượng:** Gõ `ruowuj` ra `rựơu` thay vì `rượu`.
* **Nguyên nhân gốc rễ:** 
  - Hàm `getTargetVowelIndex` tìm ký tự có dấu mũ/móc đầu tiên trong chuỗi `"ươu"`. Do chữ `'ư'` ở index 0 đứng trước nên dấu nặng `j` bị áp vào `'ư'`.
* **Quy tắc chuẩn:** 
  - Trong cụm nguyên âm `ươ` (bao gồm `ươu`, `ươi`, `ươn`, `ương`), dấu thanh bắt buộc phải nằm trên nguyên âm thứ hai là `'ơ'`.
* **Giải pháp:** Khi chuỗi chứa `"ươ"`, vị trí đặt dấu luôn ưu tiên index của ký tự `'ơ'`.

---

### 3.4. Nhóm 4: Âm tắc cuối `c, p, t, ch` (`ToneRules.fs`) - 1 lỗi
* **Hiện tượng:** Gõ `thuyesft` ra `thuyèt` thay vì `thuyết`.
* **Nguyên nhân gốc rễ:** 
  - Trong quy tắc ngữ âm tiếng Việt, âm tiết có phụ âm kết thúc bằng âm tắc `c, p, t, ch` chỉ có 2 thanh điệu hợp lệ: **Thanh Sắc** (`Acute`) và **Thanh Nặng** (`Dot`).
  - Khi gõ chuỗi đổi dấu trên từ có âm cuối là `'t'`, phím `f` (thanh Huyền) không hợp lệ về mặt chính tả.
* **Giải pháp:** Khi từ kết thúc bằng `c, p, t, ch`, nếu dấu thanh không phải là Sắc hoặc Nặng thì tự động chuẩn hóa về thanh Sắc hoặc giữ thanh hợp lệ.

---

### 3.5. Nhóm 5: Ánh xạ Mixed Case trên chuỗi phím thô co ngắn (`WordBuffer.fs`) - 1 lỗi
* **Hiện tượng:** `vIeeTj` $\rightarrow$ `vIệt` thay vì `vIệT`.
* **Nguyên nhân gốc rễ:** 
  - Chuỗi thô đầu vào có 6 ký tự `[v, I, e, e, T, j]` với mảng case `[false, true, false, false, true, false]`.
  - Kết quả hiển thị co ngắn chỉ còn 4 ký tự `[v, I, ệ, t]`.
  - Hàm `applyCase` lấy cờ hoa/thường theo chỉ số `0..3` $\rightarrow$ ký tự thứ 4 lấy cờ của phần tử index 3 (`'e'` - chữ thường) thay vì lấy cờ của chữ `'T'` (index 4).
* **Giải pháp:** Khi áp dụng `LetterCase.Mixed`, căn chỉnh theo vị trí phụ âm cuối và các ký tự thực thụ trong từ.

---

## 4. Kế Hoạch Khắc Phục Tiếp Theo (Next Steps)

1. Cập nhật `ToneRules.fs` đảo lại đúng chỉ số Modern (`0`) / Traditional (`1`) và ưu tiên `'ơ'` trong cụm `"ươ"`.
2. Tinh chỉnh `isUndoModifier` trong `TelexEngine.fs` để hỗ trợ mượt mà `uwow`.
3. Bổ sung ràng buộc thanh trắc cho âm tắc cuối trong `ToneRules.fs`.
4. Cập nhật thuật toán ánh xạ `LetterCase.Mixed` trong `WordBuffer.fs`.
