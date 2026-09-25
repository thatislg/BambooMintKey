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
2. **Kiến trúc Context Handle Độc Lập**: Khác với Windows TSF (chạy in-process trong từng ứng dụng), trên Linux toàn bộ desktop dùng chung **1 tiến trình daemon `fcitx5`**. `BambooMintKey.Core.Native` phải quản lý trạng thái gõ tách biệt tuyệt đối theo từng `InputContext` qua con trỏ `IntPtr handle`.
3. **An Toàn Bộ Nhớ & Không Cần Caller Free**: Quản lý bộ đệm chuỗi UTF-8 (`preedit` và `commit`) hoàn toàn bên trong context của C#. Caller C++ chỉ đọc qua con trỏ `const char*` mà không bao giờ phải cấp phát hoặc giải phóng bộ nhớ chuỗi, triệt tiêu nguy cơ rò rỉ bộ nhớ (memory leak) hoặc lỗi bộ nhớ kép (double-free).
4. **Hiệu năng xử lý phím cực cao**: Thời gian thực thi cho mỗi sự kiện phím phải dưới **0.5 mili-giây** (tránh gây độ trễ gõ phím).

---

## 2. Kiến Trúc Context Model & Vòng Đời Đối Tượng

### 2.1. So sánh mô hình Windows TSF vs Linux Fcitx5

```
[Windows TSF Model]
App 1 (Chrome.exe)   ──> Tải BambooMintKey.dll riêng (Process 1) ──> Static State OK
App 2 (Notepad.exe)  ──> Tải BambooMintKey.dll riêng (Process 2) ──> Static State OK

[Linux Fcitx5 Model]
App 1 (Chrome)   ──┐
App 2 (Terminal) ──┼──> D-Bus / Wayland ──> [fcitx5 daemon]
App 3 (VS Code)  ──┘                        └──> Nạp DUY NHẤT 1 libBambooMintKeyCore.so
                                                 ❌ Static State sẽ gây xung đột lẫn lộn bộ đệm!
                                                 ✅ BẮT BUỘC dùng Context Handle riêng biệt!
```

### 2.2. Cấu trúc Đối Tượng `EngineContext` (C#)

Mỗi khi một cửa sổ hoặc trường nhập liệu mới được kích hoạt, Fcitx5 Addon sẽ yêu cầu tạo một instance `EngineContext`.

```csharp
namespace BambooMintKey.Core.Native;

internal sealed class EngineContext
{
    // 1. Trạng thái từ vựng F# thuần túy
    public Types.WordState State { get; set; } = Types.WordState.Empty;

    // 2. Cấu hình gõ riêng của context
    public EngineConfig.EngineConfig Config { get; set; } = EngineConfig.EngineConfig.Default;

    // 3. Bộ đệm UTF-8 nội bộ cho Preedit (tối đa 256 bytes, thừa đủ cho 1 từ tiếng Việt)
    private readonly byte[] _preeditBuffer = new byte[256];
    private int _preeditLength = 0;

    // 4. Bộ đệm UTF-8 nội bộ cho Commit Text
    private readonly byte[] _commitBuffer = new byte[256];
    private int _commitLength = 0;

    // 5. Khóa bảo vệ thread-safety nhẹ
    public readonly object SyncRoot = new();

    // Các phương thức cập nhật buffer UTF-8 null-terminated...
}
```

### 2.3. Quản lý GCHandle & Pinning

Để truyền con trỏ đối tượng `EngineContext` sang C++ an toàn:
* Khi tạo: Dùng `GCHandle.Alloc(context, GCHandleType.Normal)` -> ép kiểu `GCHandle.ToIntPtr(handle)` thành `IntPtr`.
* Khi gọi hàm: Ép `IntPtr` ngược lại `GCHandle.FromIntPtr(handle).Target as EngineContext`.
* Khi hủy: `GCHandle.FromIntPtr(handle).Free()`.
Cơ chế này giữ cho GC không di chuyển hay thu hồi context khi C++ đang nắm giữ handle.

---

## 3. Đặc Tả Giao Diện C-ABI (C-ABI Function Signatures)

Tất cả các hàm export đều được đánh dấu `[UnmanagedCallersOnly(EntryPoint = "...", CallConvs = new[] { typeof(CallConvCdecl) })]`.

### 3.1. Nhóm Hàm Vòng Đời (Lifecycle)

#### `bmk_context_create`
* **C-ABI Signature:** `intptr_t bmk_context_create(void);`
* **Mô tả:** Khởi tạo một context gõ mới.
* **Return:** Con trỏ opaque handle `intptr_t` (khác `0` nếu thành công).

#### `bmk_context_free`
* **C-ABI Signature:** `void bmk_context_free(intptr_t handle);`
* **Mô tả:** Giải phóng context và tài nguyên liên quan khi cửa sổ đóng.
* **Return:** Không. An toàn khi truyền `0` (null).

#### `bmk_context_reset`
* **C-ABI Signature:** `void bmk_context_reset(intptr_t handle);`
* **Mô tả:** Xóa sạch bộ đệm từ (`WordState.Empty`), reset preedit và commit buffer về rỗng. Được gọi khi input context mất focus hoặc người dùng hủy gõ.
* **Return:** Không.

---

### 3.2. Nhóm Hàm Xử Lý Sự Kiện Bàn Phím (Key Processing)

Tất cả các hàm xử lý phím đều trả về một số nguyên `int32_t action_code`:

| Action Code | Tên | Ý Nghĩa Đối Với Fcitx5 |
|:---:|---|---|
| `0` | **`PassThrough`** | Bộ gõ bỏ qua phím này, Fcitx5 chuyển phím gốc cho ứng dụng đích xử lý. |
| `1` | **`Consume`** | Bộ gõ đã nuốt phím (không thay đổi preedit hiển thị). Gọi `keyEvent.filterAndAccept()`. |
| `2` | **`UpdatePreedit`** | Bộ gõ cập nhật chuỗi đang gõ dở. Fcitx5 lấy chuỗi qua `bmk_get_preedit_text` và gọi `inputPanel().setPreedit(...)`. |
| `3` | **`CommitString`** | Từ đã hoàn tất (khi gặp dấu cách, dấu câu). Fcitx5 xóa preedit, lấy chuỗi qua `bmk_get_commit_text` và gọi `inputContext->commitString(...)`. |

#### `bmk_process_key`
* **C-ABI Signature:** `int32_t bmk_process_key(intptr_t handle, uint32_t unicode_char);`
* **Tham số:**
  * `handle`: Context handle.
  * `unicode_char`: Ký tự Unicode UTF-32 (ví dụ: `'a'`, `'w'`, `'s'`, `'A'`).
* **Mô tả:** Chuyển đổi thành `KeyInput.Char(char)` và gọi F# `TelexEngine.processKey`. Cập nhật `_preeditBuffer` nếu có kết quả.
* **Return:** `int32_t action_code` (`0`, `1`, `2`, `3`).

#### `bmk_process_backspace`
* **C-ABI Signature:** `int32_t bmk_process_backspace(intptr_t handle);`
* **Mô tả:** Gửi `KeyInput.Backspace` vào engine. Xóa lùi 1 ký tự hoặc rút dần các dấu phụ/thanh.
* **Return:**
  * Nếu còn ký tự trong từ: trả về `2` (`UpdatePreedit`).
  * Nếu đã xóa hết từ: trả về `2` (`UpdatePreedit` với chuỗi rỗng) hoặc `0` (`PassThrough` để ứng dụng tự xóa ký tự phía trước).

#### `bmk_process_wordbreak`
* **C-ABI Signature:** `int32_t bmk_process_wordbreak(intptr_t handle, uint32_t break_char);`
* **Mô tả:** Gửi `KeyInput.WordBreak(char)` (phím Space, Enter, Tab, dấu phẩy, chấm...). Chốt từ hiện tại vào `_commitBuffer`.
* **Return:** `3` (`CommitString`) nếu có từ đang gõ dở, hoặc `0` (`PassThrough`) nếu bộ đệm đang rỗng.

---

### 3.3. Nhóm Hàm Trích Xuất Dữ Liệu UTF-8 (String Buffers)

#### `bmk_get_preedit_text`
* **C-ABI Signature:** `const char* bmk_get_preedit_text(intptr_t handle);`
* **Mô tả:** Lấy con trỏ chuỗi UTF-8 kết thúc bằng null byte (`\0`) của từ đang gõ.
* **Hợp đồng bộ nhớ:** Con trỏ trỏ trực tiếp vào bộ đệm nội bộ của `EngineContext`. **Caller C++ TUYỆT ĐỐI KHÔNG GỌI `free()`**. Chuỗi có hiệu lực cho đến lần gọi hàm gõ phím tiếp theo trên cùng context này.
* **Return:** `const char*` UTF-8. Trả về `""` nếu rỗng.

#### `bmk_get_commit_text`
* **C-ABI Signature:** `const char* bmk_get_commit_text(intptr_t handle);`
* **Mô tả:** Lấy con trỏ chuỗi UTF-8 kết thúc bằng `\0` của từ hoàn chỉnh cần chốt vào ứng dụng.
* **Return:** `const char*` UTF-8.

---

### 3.4. Nhóm Hàm Cấu Hình (Configuration)

#### `bmk_set_options`
* **C-ABI Signature:**
  ```c
  void bmk_set_options(
      intptr_t handle,
      int32_t  is_enabled,
      int32_t  tone_style,            /* 0 = Modern (hòa), 1 = Traditional (hoà) */
      int32_t  auto_restore_english,  /* 0 = Tắt, 1 = Bật */
      int32_t  allow_repeat_undo,     /* 0 = Tắt, 1 = Bật */
      int32_t  allow_leading_w,       /* 0 = Tắt, 1 = Bật */
      int32_t  allow_free_tone        /* 0 = Tắt, 1 = Bật */
  );
  ```
* **Mô tả:** Cập nhật trực tiếp các cờ của `EngineConfig` cho context mà không cần parse JSON.

#### `bmk_load_config_json`
* **C-ABI Signature:** `int32_t bmk_load_config_json(intptr_t handle, const char* json_utf8);`
* **Mô tả:** Nạp cấu hình từ chuỗi JSON XDG. Trả về `0` nếu thành công, `-1` nếu parse lỗi.

---

## 4. Hợp Đồng Sở Hữu Bộ Nhớ (Memory Contract)

```
┌────────────────────────────────────────────────────────────────────────┐
│ C# NativeAOT Runtime (libBambooMintKeyCore.so)                         │
│                                                                        │
│   GCHandle ───► EngineContext                                          │
│                    ├── WordState (F# immutable record)                 │
│                    ├── _preeditBuffer [byte 0 ... byte N, \0]          │
│                    └── _commitBuffer  [byte 0 ... byte M, \0]          │
│                                │                                       │
└────────────────────────────────┼───────────────────────────────────────┘
                                 │ Trả về con trỏ byte* (Read-Only)
                                 ▼
┌────────────────────────────────────────────────────────────────────────┐
│ C++ Fcitx5 Addon (bamboomintkey-fcitx5.so)                             │
│                                                                        │
│   const char* text = bmk_get_preedit_text(handle);                     │
│   std::string str(text); // Copy dữ liệu sang std::string của C++     │
│   // KHÔNG BAO GIỜ gọi: free(text) hay delete text;                    │
└────────────────────────────────────────────────────────────────────────┘
```

1. **Tuổi thọ buffer**: Buffer nội bộ tồn tại cùng với `EngineContext`. Khi `bmk_context_free` được gọi, GC sẽ tự động dọn dẹp các byte array.
2. **Kích thước cố định**: 256 bytes UTF-8 là quá đủ cho 1 âm tiết tiếng Việt dài nhất (`nghiêng` = 7 ký tự = 8 bytes UTF-8). Điều này tránh việc cấp phát động `malloc`/`new` liên tục trong lúc người dùng gõ phím.

---

## 5. Ma Trận Kiểm Thử Kỹ Thuật (Test Matrix & Test Cases)

Bộ kiểm thử được viết bằng chương trình C độc lập (`tests/NativeAotTestRunner.c`) hoặc Python (`tests/test_cabi.py` dùng `ctypes`), liên kết trực tiếp với `libBambooMintKeyCore.so`.

| Test ID | Tên Hạng Mục | Các Bước Thực Hiện (Input) | Kết Quả Mong Đợi (Expected Output) | Tiêu Chí Đánh Giá (Pass/Fail) |
|:---:|---|---|---|---|
| **`TC-CABI-01`** | Lifecycle Verification | 1. Gọi `bmk_context_create()`<br>2. Kiểm tra handle `!= 0`<br>3. Gọi `bmk_context_reset()`<br>4. Gọi `bmk_context_free()` | Handle hợp lệ, không crash, không sinh SIGSEGV | ✅ PASS nếu hoàn tất chu trình không lỗi bộ nhớ. |
| **`TC-CABI-02`** | Telex Basic Typing | Gõ chuỗi ký tự: `t`, `i`, `e`, `e`, `n`, `g`, `s` | - Ký tự `t`: preedit = `"t"`, code = 2<br>- Ký tự `e`: preedit = `"tie"`, code = 2<br>- Ký tự `e`: preedit = `"tiê"`, code = 2<br>- Ký tự `s`: preedit = `"tiếng"`, code = 2 | ✅ PASS nếu `bmk_get_preedit_text` trả về đúng `"tiếng"`. |
| **`TC-CABI-03`** | Telex Complex Words | Gõ các từ phức tạp: `thuyeenf` (`thuyền`), `nghieeng` (`nghiêng`), `ddoowngf` (`đồng`) | Preedit hiển thị chính xác âm tiết tiếng Việt có dấu mũ, móc, thanh | ✅ PASS nếu kết quả chuỗi UTF-8 hoàn toàn đúng Unicode NFC. |
| **`TC-CABI-04`** | Backspace Rollback | 1. Gõ `t-i-e-e-n-g-s` -> `"tiếng"`<br>2. Bấm Backspace 1 lần -> `"tiêng"`<br>3. Bấm Backspace tiếp -> rút dần về rỗng | Mỗi lần bấm Backspace, chuỗi preedit rút gọn từng bước một cách tự nhiên | ✅ PASS nếu preedit co lại đúng cấu trúc âm tiết. |
| **`TC-CABI-05`** | WordBreak & Commit | 1. Gõ `v-i-e-e-t-j` -> preedit = `"việt"`<br>2. Gõ phím cách `' '` qua `bmk_process_wordbreak` | - Action code trả về = 3 (`CommitString`)<br>- `bmk_get_commit_text` = `"việt "` (kèm khoảng trắng)<br>- Preedit reset về rỗng | ✅ PASS nếu tách bạch rõ ràng giữa chuỗi commit và chuỗi preedit. |
| **`TC-CABI-06`** | English Word Protection | Gõ từ tiếng Anh: `i-n-t-e-r-n-e-t` (chứa `er` dễ nhầm thành `ơr`) | Với cờ `autoRestoreEnglishWords = true`, engine tự khôi phục thành `"internet"` | ✅ PASS nếu không bị bỏ dấu tiếng Việt sai trên từ tiếng Anh. |
| **`TC-CABI-07`** | Multi-Context Isolation | 1. Tạo `ctxA` và `ctxB`<br>2. `ctxA` gõ `t-i-e-e-n-g-s` (`tiếng`)<br>3. `ctxB` gõ `v-i-e-e-t-j` (`việt`) xen kẽ<br>4. Đọc preedit cả 2 | - `ctxA` phải là `"tiếng"`<br>- `ctxB` phải là `"việt"`<br>Không có hiện tượng chữ của A bay sang B | ✅ PASS nếu bộ đệm 2 context độc lập 100%. |
| **`TC-CABI-08`** | Memory Leak Stress Test | Vòng lặp: Tạo context -> gõ 10 từ -> hủy context, lặp lại 10.000 lần | Bộ nhớ RAM đo bằng `getrusage` / RSS không tăng lũy tiến vô hạn | ✅ PASS nếu RSS chênh lệch dưới 2MB sau 10.000 chu kỳ. |

---

## 6. Kế Hoạch Triển Khai Code Milestone 1

Sau khi tài liệu này được thông qua, mã nguồn của `src/BambooMintKey.Core.Native` sẽ được thiết lập gồm:
* `BambooMintKey.Core.Native.csproj` (.NET 10 NativeAOT)
* `EngineContext.cs` (Quản lý context và UTF-8 buffer)
* `NativeExports.cs` (Các hàm `[UnmanagedCallersOnly]`)
* `tests/NativeAotTestRunner.c` (Chương trình test C theo ma trận kiểm thử trên).
