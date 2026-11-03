<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 027: Lỗi không thể hoàn dấu bằng phím lặp (`rrr`) và kẹt phím `Backspace` khi gõ trong Windows Search Bar (`SearchHost.exe`)

**Mã tài liệu:** `027_Windows_SearchBar_AppContainer_RepeatKeyUndo_And_Backspace_Bug`  
**Trạng thái:** ✅ Đã khắc phục triệt để  
**Mức độ nghiêm trọng:** Cao (Ảnh hưởng trực tiếp thao tác tìm kiếm ứng dụng hàng ngày trên Windows 10/11)  
**Ngày ghi nhận:** 03/11/2026  
**Nền tảng ảnh hưởng:** Windows 10 / Windows 11 (Các tiến trình chạy trong **AppContainer**: Windows Search Bar `SearchHost.exe`, Start Menu, Windows Settings, UWP Apps)  
**Mô-đun liên quan:**
- `src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs`
- `src/BambooMintKey.Core/Engine/TelexEngine.fs`
- `tests/BambooMintKey.Core.Tests/RestoreAndUndoTests.fs`

---

## 1. Mô tả hiện tượng & Bảng tái hiện lỗi

### 1.1. Bối cảnh
Người dùng mở thanh tìm kiếm ứng dụng của Windows (**Search Bar** nằm cạnh nút Start trên Taskbar — tiến trình `SearchHost.exe`) và muốn gõ tên ứng dụng tiếng Anh là **`Terminal`** (hoặc chỉ gõ tiền tố **`Ter`**).

### 1.2. Bảng đối chiếu hành vi thực tế và kỳ vọng

| Kịch bản | Chuỗi phím gõ | Kết quả mong đợi | Kết quả thực tế trong Search Bar | Ghi chú |
|:---:|:---|:---|:---|:---|
| **1** | `T` $\rightarrow$ `e` $\rightarrow$ `r` | `Tẻ` | `Tẻ` | Bình thường (bộ gõ đang ở chế độ `V`). |
| **2 (Hoàn dấu)** | Đang ở `Tẻ`, bấm tiếp `r` (hoặc `rrr` nhiều lần) | Hoàn dấu hỏi, trả về chữ thô `Ter` (hoặc `Terr`...) | **Giữ nguyên ở `Tẻ`**, bấm bao nhiêu lần `r` cũng không đổi | ❌ Lỗi mất tác dụng hoàn dấu (`AllowRepeatKeyUndo`). |
| **3 (Xóa lùi)** | Sau khi bấm `rrr`, bấm `Backspace` 1–3 lần | Xóa ký tự cuối (`Tẻ` $\rightarrow$ `Te`) | **Vẫn đứng yên ở `Tẻ`**, không xóa được chữ | ❌ Lỗi kẹt `Backspace` do tích tụ phím ẩn. |
| **4 (Gõ hết từ)** | Gõ liền mạch `T-e-r-m-i-n-a-l` | Tự nhận diện từ tiếng Anh và đổi `Tẻ` $\rightarrow$ `Terminal` | Đổi từ `Tẻ` $\rightarrow$ `Terminal` chính xác | ✅ Hoạt động tốt nhờ nhánh Fallback của `SyllableParser`. |

> **Đặc điểm nhận diện quan trọng:** Hiện tượng kịch bản 2 và 3 **chỉ xảy ra trong Windows Search Bar (và các app UWP/AppContainer)**, còn trên Notepad, Chrome, Word hay VS Code thì gõ `Terr` vẫn hoàn dấu về `Ter` bình thường.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

Lỗi này hình thành từ chuỗi tác động liên hoàn qua **3 tầng kỹ thuật**:

### 2.1. Tầng 1: Cơ chế cô lập không gian tên (Namespace Isolation) của Windows AppContainer
1. Các ứng dụng Desktop truyền thống (Notepad, Chrome, Explorer, `BambooMintKey.UI`...) chạy ở mức Medium Integrity và dùng chung thư mục đối tượng Kernel:
   `\Sessions\<SessionID>\BaseNamedObjects\BambooMintKey_SharedConfig_v1`
2. Ngược lại, **Windows Search Bar (`SearchHost.exe`)** là một tiến trình đóng gói chạy trong sandbox **AppContainer** (`MicrosoftWindows.Client.CBS`).
3. Theo cơ chế bảo mật của Windows Kernel, mọi lời gọi `CreateFileMappingW` hoặc `OpenFileMappingW` với tiền tố `Local\` từ bên trong AppContainer đều bị chuyển hướng bắt buộc vào thư mục cô lập riêng của gói đó:
   `\Sessions\<SessionID>\AppContainerNamedObjects\<AppContainerSID>\BambooMintKey_SharedConfig_v1`
4. Hệ quả: Khi `BambooMintKey.dll` được nạp vào `SearchHost.exe`, nó **không thể nhìn thấy** vùng nhớ `SharedMemory` mà `BambooMintKey.UI` hay `explorer.exe` đã tạo, mà tự tạo ra một vùng nhớ 64-byte mới tinh (`isCreator = true`) chứa toàn bộ các byte `0`.

### 2.2. Tầng 2: Lỗi bỏ sót khởi tạo mặc định và lỗi đọc cấu hình trong `SharedMemoryManager.cs`
1. **Chỉ gán duy nhất byte `[0]` khi tạo mới vùng nhớ:**
   Tại `SharedMemoryManager.cs` (dòng 151–155):
   ```csharp
   if (isCreator)
   {
       _pShared[0] = 1; // 1 = IsVietnameseMode On (V)
       *(uint*)(_pShared + 8) = 1; // StateSequence ban đầu
   }
   ```
   Toàn bộ các byte cấu hình còn lại trong `_pShared` vẫn mang giá trị **`0` (`false`)**, bao gồm:
   - `_pShared[3]` (`AllowRepeatKeyUndo`) = `0` (`false`)
   - `_pShared[20]` (`AllowFreeTonePlacement`) = `0` (`false`)
   - `_pShared[22]` (`EnableVietnameseDictionary`) = `0` (`false`)
   - `_pShared[23]` (`EnableEnglishBacktracking`) = `0` (`false`)
2. **`LoadInitialConfigFromDisk(_pShared)` thất bại âm thầm trong AppContainer:**
   - Ngay dòng 158, hàm `LoadInitialConfigFromDisk` cố đọc `%APPDATA%\BambooMintKey\config.json`.
   - Tiến trình AppContainer (`SearchHost.exe`) bị Windows chặn quyền truy cập vào `%APPDATA%` thật của user (ném ngoại lệ `UnauthorizedAccessException` và bị khối `catch` ở dòng 272 bỏ qua).
   - Thậm chí nếu rơi vào nhánh `!File.Exists(path)` (dòng 196–225), hàm còn gọi `return;` sớm ở dòng 224 trước khi kịp gán giá trị mặc định vào `pShared`.
   - **Kết quả:** Bên trong Windows Search Bar, `AllowRepeatKeyUndo` bị tắt (`false`) dù trong cài đặt của người dùng đang bật (`true`).

### 2.3. Tầng 3: Hành vi nuốt phím dấu lặp và tích tụ `RawKeys` trong `TelexEngine.fs`
1. **Tại sao bấm `rrr` vẫn giữ nguyên `Tẻ`?**
   - Tại `TelexEngine.fs` (dòng 159–162), điều kiện hoàn dấu `isUndoTone` đòi hỏi `config.AllowRepeatKeyUndo = true`.
   - Do `AllowRepeatKeyUndo = false` trong Search Bar, biến `isUndoTone` trả về `false`.
   - Khi người dùng gõ tiếp `r` (`rr`, `rrr`...), luồng xử lý rơi xuống nhánh áp dấu thanh thông thường (dòng 291–293): mỗi chữ `r` lại áp đè dấu hỏi (`Tone.HookAbove`) lên chính âm tiết `"Tẻ"`, nên chữ hiển thị trên màn hình (`TransformedText`) đứng im là **`"Tẻ"`**.
2. **Tại sao bấm `Backspace` cũng không xóa được `Tẻ`?**
   - Mỗi lần người dùng bấm `r`, biến `newRaw = state.RawKeys @ [ c ]` (dòng 136) vẫn âm thầm nối thêm `'r'` vào lịch sử phím thô:
     `RawKeys = ['T'; 'e'; 'r'; 'r'; 'r'; 'r']` (6 phím thô, nhưng màn hình chỉ hiện 2 ký tự `"Tẻ"`).
   - Khi người dùng bấm `Backspace` (dòng 393–401), engine chỉ cắt đi **1 ký tự `'r'` cuối cùng** trong `RawKeys` (còn 5 ký tự `['T'; 'e'; 'r'; 'r'; 'r']`) rồi replay lại từ đầu $\rightarrow$ kết quả replay vẫn là **`"Tẻ"`**!
   - Người dùng bấm `Backspace` 3 lần liên tiếp mà trên màn hình chữ `"Tẻ"` vẫn trơ ra.
3. **Tại sao gõ tiếp `Terminal` thì lại đổi được `Tẻ` $\rightarrow$ `Terminal`?**
   - Vì khi gõ `T-e-r-m-i`, tại ký tự `i`, chuỗi `"Termi"` đi vào nhánh 5 (dòng 344–349): `SyllableParser.parse "Termi"` thấy `"ermi"` không phải vần tiếng Việt hợp lệ nên trả về `None` và fallback thẳng về chuỗi phím thô `"Termi"` $\rightarrow$ gõ tiếp `n-a-l` ra **`"Terminal"`**. Nhánh này thuần túy dựa trên luật ngữ âm, không phụ thuộc vào cờ trong `SharedMemoryManager`.

---

## 3. Giải pháp Kỹ thuật đã thực hiện

### 3.1. Giải pháp 1: Khởi tạo đầy đủ cấu hình mặc định chuẩn cho `_pShared` trong `SharedMemoryManager.cs`
- Bổ sung hàm `InitializeDefaultConfig(byte* pShared)` gán đầy đủ các giá trị mặc định chuẩn của engine ngay khi khởi tạo vùng nhớ (`isCreator == true`):
  - `pShared[0] = 1` (`IsVietnameseMode = true`)
  - `pShared[1] = 0` (`ToneStyle = Modern`)
  - `pShared[3] = 1` (`AllowRepeatKeyUndo = true`)
  - `pShared[4] = 0` (`AllowLeadingWAsU = false`)
  - `pShared[20] = 1` (`AllowFreeTonePlacement = true`)
  - `pShared[21] = 0` (`EnablePreedit = false`)
  - `pShared[22] = 1` (`EnableVietnameseDictionary = true`)
  - `pShared[23] = 1` (`EnableEnglishBacktracking = true`)
  - `*(uint*)(pShared + 12) = 0xC0` (`HotkeyVKey`)
  - `*(uint*)(pShared + 16) = 0x0001` (`HotkeyModifiers`)
- Trong `LoadInitialConfigFromDisk(byte* pShared)`:
  - Loại bỏ lệnh `return;` sớm sau khi tạo mới file `config.json` lần đầu để các giá trị mặc định luôn được nạp vào `pShared`.
  - Nhờ đó, dù chạy trong bất kỳ AppContainer nào không đọc được đĩa, bộ gõ vẫn luôn có đầy đủ `AllowRepeatKeyUndo = true`, `AllowFreeTonePlacement = true`, `EnableEnglishBacktracking = true`.

### 3.2. Giải pháp 2: Cấp quyền đọc `config.json` cho AppContainer (`ALL APPLICATION PACKAGES` — SID `S-1-15-2-1`)
- Khi tạo thư mục `%APPDATA%\BambooMintKey` và lưu file `config.json` từ tiến trình Desktop, tự động gắn thêm quyền **ReadAndExecute (`RX`)** cho SID `S-1-15-2-1` (`ALL APPLICATION PACKAGES`) trên thư mục `%APPDATA%\BambooMintKey`.
- Tiến trình AppContainer (`SearchHost.exe`) có thể đọc trực tiếp file `%APPDATA%\BambooMintKey\config.json` để nhận đúng cấu hình tùy chỉnh của người dùng mà không bị lỗi `UnauthorizedAccessException`.

### 3.3. Giải pháp 3: Chống tích tụ phím dấu trùng lặp vô hình trong `TelexEngine.fs` khi tắt `AllowRepeatKeyUndo`
- Trong `TelexEngine.fs`:
  - Khi `AllowRepeatKeyUndo = false`, nếu âm tiết hiện tại đã mang đúng dấu thanh của phím vừa gõ (ví dụ đã có `Tẻ` mà gõ thêm `r`), engine giữ nguyên `state.RawKeys` cũ thay vì âm thầm nối dư phím dấu vô hình vào `RawKeys`.
  - Đảm bảo 1 lần bấm `Backspace` luôn xóa đúng 1 thao tác hiển thị, không bao giờ bị kẹt `Backspace`.

