<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 007_03 — Thiết Kế Chi Tiết C-ABI & Quản Lý Đa Context (`BambooMintKey.Core.Native`)

**Mã tài liệu:** `007_03_CoreNative_CABI_Design`  
**Giai đoạn:** Phase 7 — Chuẩn bị và Triển khai nền tảng Linux / Fcitx5  
**Thuộc module:** `src/BambooMintKey.Core.Native`  
**Trạng thái:** ✅ Đã phê duyệt thiết kế  
**Tài liệu tham chiếu:** [007_01_InvestigationForLinux.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_01_InvestigationForLinux.md), [007_002_Roadmap.md](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/docs/2.Design/Phase7/007_002_Roadmap.md)

---

## 1. Mục Tiêu Kỹ Thuật

1. **Cầu nối NativeAOT C-ABI**: Đóng gói lõi xử lý F# (`BambooMintKey.Core`) thành một thư viện chia sẻ gốc Linux ELF (`libBambooMintKeyCore.so`), không cần nạp toàn bộ .NET runtime cồng kềnh.
2. **Kiến trúc Context Handle Độc Lập**: Khác với Windows TSF (chạy in-process trong từng ứng dụng), trên Linux toàn bộ desktop dùng chung **1 tiến trình daemon `fcitx5`**. `BambooMintKey.Core.Native` phải quản lý trạng thái gõ tách biệt tuyệt đối theo từng `InputContext` qua con trỏ nhận diện (Handle).
3. **An Toàn Bộ Nhớ & Không Cần Caller Free**: Quản lý bộ đệm chuỗi UTF-8 (`preedit` và `commit`) hoàn toàn bên trong context của thư viện. Caller C++ chỉ đọc qua con trỏ mà không bao giờ phải cấp phát hoặc giải phóng bộ nhớ chuỗi, triệt tiêu nguy cơ rò rỉ bộ nhớ (memory leak) hoặc lỗi bộ nhớ kép (double-free).
4. **Hiệu năng xử lý phím cực cao**: Thời gian thực thi cho mỗi sự kiện phím phải dưới **0.5 mili-giây** (tránh gây độ trễ gõ phím).

---

## 2. Kiến Trúc Context Model & Vòng Đời Đối Tượng

### 2.1. So sánh mô hình Windows TSF vs Linux Fcitx5

* **Mô hình Windows TSF (In-Process)**: Mỗi tiến trình ứng dụng (Chrome, Notepad) nạp một phiên bản DLL riêng. Bộ nhớ nằm trong không gian của từng tiến trình nên biến trạng thái tĩnh (static state) không gây xung đột chéo giữa các ứng dụng.
* **Mô hình Linux Fcitx5 (Single Daemon)**: Chỉ có duy nhất một tiến trình `fcitx5` chạy nền cho toàn hệ thống. Mọi ứng dụng đều gửi phím về tiến trình này. Do đó, việc dùng biến trạng thái tĩnh là một sai lầm nghiêm trọng. Bắt buộc phải dùng **mô hình Context Handle**: mỗi cửa sổ hoặc ô nhập liệu được cấp một mã định danh (handle) riêng biệt để quản lý bộ đệm độc lập.

### 2.2. Đặc Tả Cấu Trúc Dữ Liệu `EngineContext` (Mã giả)

```
CẤU TRÚC EngineContext:
    Trường State          : Trạng thái từ vựng hiện tại (WordState của F# Core)
    Trường Config         : Cấu hình gõ áp dụng cho context (EngineConfig)
    Trường PreeditBuffer  : Mảng byte bộ đệm nội bộ chứa chuỗi UTF-8 đang gõ (cố định 256 bytes)
    Trường PreeditLength  : Độ dài chuỗi preedit tính theo byte
    Trường CommitBuffer   : Mảng byte bộ đệm nội bộ chứa chuỗi UTF-8 hoàn tất (cố định 256 bytes)
    Trường CommitLength   : Độ dài chuỗi commit tính theo byte
    Trường Lock           : Khóa đồng bộ bảo vệ truy cập đa luồng
HẾT CẤU TRÚC
```

### 2.3. Cơ Chế Quản Lý Vòng Đời Đối Tượng

* **Khởi tạo (Creation)**: Cấp phát một đối tượng `EngineContext` mới, cố định đối tượng trong bộ nhớ (GCHandle Pin) để bộ dọn rác GC không di chuyển đối tượng, sau đó chuyển đổi thành con trỏ số nguyên nguyên thủy (`IntPtr`) trả về cho C++.
* **Truy xuất (Dereference)**: Khi nhận lại con trỏ `handle` từ C++, ánh xạ ngược lại đối tượng `EngineContext` tương ứng. Nếu con trỏ không hợp lệ, từ chối xử lý an toàn.
* **Giải phóng (Destruction)**: Hủy bỏ việc ghim bộ nhớ (Unpin) và giải phóng đối tượng khi cửa sổ ứng dụng bị đóng, cho phép GC thu hồi tài nguyên sạch sẽ.

---

## 3. Đặc Tả Giao Diện C-ABI & Thuật Toán Xử Lý

Mọi hàm export đều tuân thủ quy ước gọi chuẩn C (`cdecl`).

### 3.1. Nhóm Hàm Vòng Đời (Lifecycle)

| Tên Hàm Export | Tham Số Đầu Vào | Dữ Liệu Trả Về | Mô Tả Hành Vi Bằng Lời |
|---|---|---|---|
| `bmk_context_create` | Không có | `IntPtr` (Handle) | Cấp phát mới 1 `EngineContext`, trả về con trỏ nhận diện (khác 0). |
| `bmk_context_free` | `IntPtr handle` | `void` | Thu hồi ghim bộ nhớ và giải phóng context tương ứng. Bỏ qua nếu handle bằng 0. |
| `bmk_context_reset` | `IntPtr handle` | `void` | Đặt lại trạng thái `State` về rỗng (`WordState.Empty`), xóa rỗng hai bộ đệm `PreeditBuffer` và `CommitBuffer`. |

---

### 3.2. Nhóm Hàm Xử Lý Sự Kiện Bàn Phím

Tất cả các hàm xử lý phím đều trả về một số nguyên quy ước mã hành động (`ActionCode`):
* **`0 (PassThrough)`**: Bộ gõ bỏ qua, nhường phím gốc cho ứng dụng đích xử lý.
* **`1 (Consume)`**: Bộ gõ đã nuốt phím, không làm thay đổi preedit.
* **`2 (UpdatePreedit)`**: Cập nhật chuỗi đang gõ dở trên giao diện.
* **`3 (CommitString)`**: Chốt từ thành công vào ứng dụng và xóa preedit.

#### A. Hàm Xử Lý Ký Tự (`bmk_process_key`)
* **Đầu vào:** `handle` (Context Identifier), `unicode_char` (Mã ký tự Unicode 32-bit).
* **Đầu ra:** `ActionCode` (0, 1, 2 hoặc 3).
* **Thuật toán xử lý bằng mã giả:**

```
THUẬT TOÁN ProcessKey(handle, unicode_char):
    Context = LấyĐốiTượngTừHandle(handle)
    NẾU Context == NULL THÌ TRẢ VỀ PassThrough (0)

    Khóa Context.Lock
    THỬ:
        Input = TạoKeyInputChar(unicode_char)
        (NewState, Action) = TelexEngine.processKey(Context.State, Input, Context.Config)
        Context.State = NewState

        CHỌN TRƯỜNG HỢP CỦA Action:
            TRƯỜNG HỢP UpdateComposition(text):
                GhiChuỗiUTF8VàoBuffer(Context.PreeditBuffer, text)
                TRẢ VỀ UpdatePreedit (2)

            TRƯỜNG HỢP Commit(committedText):
                GhiChuỗiUTF8VàoBuffer(Context.CommitBuffer, committedText)
                XóaRỗngBuffer(Context.PreeditBuffer)
                TRẢ VỀ CommitString (3)

            TRƯỜNG HỢP PassThrough:
                TRẢ VỀ PassThrough (0)
    CUỐI CÙNG:
        MởKhóa Context.Lock
HẾT THUẬT TOÁN
```

#### B. Hàm Xử Lý Phím Xóa Lùi (`bmk_process_backspace`)
* **Đầu vào:** `handle`.
* **Đầu ra:** `ActionCode`.
* **Thuật toán xử lý bằng mã giả:**

```
THUẬT TOÁN ProcessBackspace(handle):
    Context = LấyĐốiTượngTừHandle(handle)
    NẾU Context == NULL THÌ TRẢ VỀ PassThrough (0)

    NẾU Context.State đang rỗng THÌ:
        TRẢ VỀ PassThrough (0) // Nhường phím Backspace cho app xóa ký tự trước đó

    Input = TạoKeyInputBackspace()
    (NewState, Action) = TelexEngine.processKey(Context.State, Input, Context.Config)
    Context.State = NewState

    NẾU NewState có độ dài từ > 0 THÌ:
        GhiChuỗiUTF8VàoBuffer(Context.PreeditBuffer, NewState.TransformedText)
        TRẢ VỀ UpdatePreedit (2)
    NGƯỢC LẠI:
        XóaRỗngBuffer(Context.PreeditBuffer)
        TRẢ VỀ UpdatePreedit (2) // Cập nhật preedit rỗng để xóa gạch chân
HẾT THUẬT TOÁN
```

#### C. Hàm Xử Lý Phím Ngắt Từ (`bmk_process_wordbreak`)
* **Đầu vào:** `handle`, `break_char` (Ký tự ngắt: Space, Enter, dấu chấm, phẩy...).
* **Đầu ra:** `ActionCode`.
* **Mô tả hành vi bằng lời:**
  - Nếu context hiện tại không có từ đang gõ dở, trả về `PassThrough (0)` để ứng dụng nhận trực tiếp phím ngắt.
  - Nếu context đang có từ gõ dở, đóng gói từ đã xử lý kèm theo ký tự ngắt vào `CommitBuffer`, xóa sạch `PreeditBuffer` và trả về `CommitString (3)`.

---

### 3.3. Nhóm Hàm Trích Xuất Bộ Đệm Chuỗi UTF-8

| Tên Hàm Export | Tham Số Đầu Vào | Dữ Liệu Trả Về | Mô Tả Hành Vi Bằng Lời |
|---|---|---|---|
| `bmk_get_preedit_text` | `IntPtr handle` | `const char*` (Byte pointer) | Trả về con trỏ trỏ trực tiếp tới mảng byte UTF-8 kết thúc bằng ký tự null (`\0`) trong `PreeditBuffer` nội bộ. |
| `bmk_get_commit_text` | `IntPtr handle` | `const char*` (Byte pointer) | Trả về con trỏ trỏ trực tiếp tới mảng byte UTF-8 kết thúc bằng ký tự null (`\0`) trong `CommitBuffer` nội bộ. |
| `bmk_get_preedit_length`| `IntPtr handle` | `int32_t` | Trả về số byte của chuỗi preedit hiện tại. |

---

### 3.4. Nhóm Hàm Cấu Hình (Configuration)

* **`bmk_set_options`**:
  * **Đầu vào:** `handle`, các cờ nguyên `is_enabled`, `tone_style` (0 = Mới, 1 = Cũ), `auto_restore_english`, `allow_repeat_undo`, `allow_leading_w`, `allow_free_tone`.
  * **Hành vi:** Cập nhật trực tiếp đối tượng `EngineConfig` trong `EngineContext` mà không cần xử lý chuỗi.
* **`bmk_load_config_json`**:
  * **Đầu vào:** `handle`, con trỏ chuỗi JSON UTF-8.
  * **Hành vi:** Đọc chuỗi JSON theo schema cấu hình chuẩn XDG và nạp lại cấu hình cho context. Trả về 0 nếu thành công, -1 nếu cấu trúc JSON không hợp lệ.

---

## 4. Hợp Đồng Sở Hữu Bộ Nhớ (Memory Contract)

```
┌────────────────────────────────────────────────────────────────────────┐
│ C# NativeAOT Runtime (libBambooMintKeyCore.so)                         │
│   - Nắm giữ EngineContext và mảng PreeditBuffer / CommitBuffer         │
│   - Tự động quản lý việc ghi đè nội dung buffer khi gõ phím            │
│   - Trả về con trỏ byte* (Read-Only) trỏ vào buffer nội bộ             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Con trỏ chuỗi UTF-8 (Read-Only)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ C++ Fcitx5 Addon (bamboomintkey-fcitx5.so)                             │
│   - Đọc dữ liệu từ con trỏ được cung cấp                               │
│   - Sao chép nội dung sang vùng nhớ của Fcitx5 (std::string)          │
│   - ⛔ TUYỆT ĐỐI KHÔNG GỌI free() HOẶC delete TRÊN CON TRỎ NÀY          │
└────────────────────────────────────────────────────────────────────────┘
```

1. **Quyền sở hữu thuộc về Core.Native**: Bộ đệm thuộc về `EngineContext`, caller C++ chỉ có quyền đọc (`Read-Only`).
2. **Thời hạn hiệu lực của con trỏ chuỗi**: Con trỏ chuỗi trả về từ `bmk_get_preedit_text` hoặc `bmk_get_commit_text` có giá trị sử dụng cho đến lần gọi xử lý phím tiếp theo trên cùng context đó hoặc đến khi context bị hủy.
3. **Cố định kích thước (Zero-Allocation)**: Với kích thước cố định 256 bytes, toàn bộ quá trình gõ phím không phát sinh thêm bất kỳ lệnh cấp phát bộ nhớ động (`malloc`/`new`) nào, đảm bảo tốc độ tối đa và loại bỏ hoàn toàn hiện tượng phân mảnh bộ nhớ.

---

## 5. Ma Trận Kiểm Thử Kỹ Thuật (Test Matrix & Test Cases)

Bộ kiểm thử được thiết kế độc lập, gọi trực tiếp các hàm export của `libBambooMintKeyCore.so` để xác minh chức năng trước khi ghép nối hệ thống.

| Test ID | Tên Hạng Mục | Các Bước Thực Hiện (Input) | Kết Quả Mong Đợi (Expected Output) | Tiêu Chí Đánh Giá (Pass/Fail) |
|:---:|---|---|---|---|
| **`TC-CABI-01`** | Kiểm tra vòng đời Context | 1. Gọi `bmk_context_create`<br>2. Kiểm tra handle khác 0<br>3. Gọi `bmk_context_reset`<br>4. Gọi `bmk_context_free` | Khởi tạo thành công, reset không lỗi, giải phóng an toàn không gây crash bộ nhớ | ✅ PASS nếu không phát sinh SIGSEGV. |
| **`TC-CABI-02`** | Gõ Telex cơ bản | Gõ chuỗi ký tự: `t`, `i`, `e`, `e`, `n`, `g`, `s` | - Tại ký tự `e` thứ hai: preedit = `"tiê"`<br>- Tại ký tự `s`: preedit = `"tiếng"`<br>- Mã hành động trả về luôn là 2 (`UpdatePreedit`) | ✅ PASS nếu chuỗi preedit cuối cùng là `"tiếng"`. |
| **`TC-CABI-03`** | Gõ âm tiết phức tạp | Gõ các từ: `thuyeenf` (`thuyền`), `nghieeng` (`nghiêng`), `ddoowngf` (`đồng`) | Các ký tự biến đổi thành âm tiết tiếng Việt có dấu mũ, móc, thanh đúng chuẩn Unicode NFC | ✅ PASS nếu chuỗi UTF-8 trả về chuẩn xác 100%. |
| **`TC-CABI-04`** | Hoàn tác khi xóa lùi | 1. Gõ `t-i-e-e-n-g-s` -> `"tiếng"`<br>2. Bấm Backspace 1 lần<br>3. Bấm Backspace tiếp tục cho đến hết | - Lần 1: preedit co về `"tiêng"`<br>- Các lần tiếp theo rút dần âm tiết về rỗng mà không bị lỗi chuỗi | ✅ PASS nếu preedit co lại từng bước hợp lý. |
| **`TC-CABI-05`** | Ngắt từ & Chốt chuỗi | 1. Gõ `v-i-e-e-t-j` -> preedit = `"việt"`<br>2. Gõ phím cách `' '` qua `bmk_process_wordbreak` | - Mã hành động trả về là 3 (`CommitString`)<br>- Chuỗi commit là `"việt "` (kèm khoảng trắng)<br>- Chuỗi preedit reset về rỗng | ✅ PASS nếu chốt từ chính xác và dọn sạch preedit. |
| **`TC-CABI-06`** | Bảo vệ từ tiếng Anh | Gõ từ tiếng Anh: `i-n-t-e-r-n-e-t` (chứa cặp `er` dễ nhầm thành `ơr`) | Với cờ bảo vệ tiếng Anh bật, engine tự động phục hồi về nguyên bản `"internet"` | ✅ PASS nếu không bị bỏ dấu sai trên từ tiếng Anh. |
| **`TC-CABI-07`** | Cách ly đa Context | 1. Tạo đồng thời 2 context `A` và `B`<br>2. `A` gõ `"tiếng"`, `B` gõ `"việt"` xen kẽ từng phím<br>3. Đọc kết quả preedit của cả 2 | - Preedit của `A` là `"tiếng"`<br>- Preedit của `B` là `"việt"`<br>Không có hiện tượng chữ của `A` xuất hiện ở `B` | ✅ PASS nếu 2 phiên gõ biệt lập tuyệt đối. |
| **`TC-CABI-08`** | Stress Test rò rỉ RAM | Thực hiện vòng lặp 10.000 chu kỳ: Tạo context -> gõ 10 từ -> hủy context | Dung lượng RAM (Resident Set Size) đo từ hệ điều hành không tăng lũy tiến liên tục | ✅ PASS nếu dung lượng RAM tăng trưởng dưới 2MB sau 10.000 chu kỳ. |
