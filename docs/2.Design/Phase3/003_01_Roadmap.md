Lộ trình triển khai **Phase 3: User Interface & Context Management** cho BambooMintKey sau khi đã lược bỏ phần Candidate Window không cần thiết.

### Mục tiêu cốt lõi của Phase 3

- **Khả năng nhận diện trạng thái:** Cung cấp icon hiển thị động (**V** / **E**) tích hợp tự nhiên vào Windows Taskbar.
- **Tương tác nhanh:** Cho phép toggle chế độ gõ bằng click chuột trái, phím tắt nội bộ, hoặc menu chuột phải.
- **Cấu hình độc lập:** Xây dựng cửa sổ Settings GUI nhẹ (`BambooMintKey.UI.exe`), tách biệt, giao tiếp với NativeBridge qua **vùng nhớ dùng chung (Shared Memory)** và tệp `config.json` để sẵn sàng tái sử dụng khi đưa sang Linux.

### Lộ trình chi tiết Phase 3 (Roadmap)

| **Sprint / Milestone**                | **Trạng thái** | **Hạng mục công việc**               | **Output kỹ thuật chính**                                    |
| ------------------------------------- |:--------------:| ------------------------------------ | ------------------------------------------------------------ |
| **M1: Taskbar Button COM Bridge**     | ✅ Hoàn thành  | Cài đặt interface TSF LangBar        | `ITfLangBarItemButton`, `ITfLangBarItemSink`, struct VTable  |
| **M2: Icon Resource & State Binding**   | ✅ Hoàn thành  | Tạo icon động (V/E) và cơ chế toggle | Tài nguyên Icon Win32 GDI/`.ico`, cập nhật `BridgeStateManager` |
| **M3: Taskbar Context Menu**          | ✅ Hoàn thành  | Menu chuột phải chuyển nhanh chế độ  | Cài đặt `InitMenu` & `OnMenuSelect` (Telex/VNI, Bảng mã, Mở Settings) |
| **M4: Shared Configuration Contract**   | ✅ Hoàn thành  | Chuẩn hóa schema cấu hình dùng chung | `config.json` (Version 2) + Shared Memory 64 bytes + module parser trong F# Core |
| **M5: Settings GUI (Standalone)**     | ✅ Hoàn thành  | Cửa sổ cài đặt giao diện             | Binary GUI độc lập **Avalonia UI + F#** (`BambooMintKey.UI.exe`) + khởi chạy qua `SettingsLauncher` |

### Kế hoạch hành động từng bước

**Bước 1: Triển khai `ITfLangBarItemButton` trên NativeBridge**

- Định nghĩa COM VTable cho `ITfLangBarItemButton` trong C# NativeAOT.
- Lấy `ITfLangBarItemMgr` từ `ITfThreadMgr` trong hàm `ActivateEx` để gọi `AddItem()`.
- Không gọi `RemoveItem()` trong `Deactivate` vì Windows Shell tự quản lý vòng đời icon; gỡ nút khi Deactivate sẽ làm icon biến mất khi chuyển focus giữa các cửa sổ. `Unregister()` được giữ lại để dùng khi cần gỡ hoàn toàn.

**Bước 2: Xử lý Icon và sự kiện Click chuyển trạng thái**

- Tạo icon chữ **V** / **E** với nền xanh lá Bamboo `#16a34a`, viền mint `#86efac`, chữ trắng ngà `#fbf8f9` (theo nhận diện thương hiệu) và nền trong suốt 4 góc bo tròn.
- Triển khai hàm `OnClick`: Khi click chuột trái vào icon, đảo cờ `IsVietnameseMode` trong engine.
- Gọi `ITfLangBarItemSink.OnUpdate(TF_LBI_ICON | TF_LBI_TEXT | TF_LBI_TOOLTIP)` và đồng bộ `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` qua `TsfCompartmentHelper.SetConversionMode` để Taskbar vẽ lại tức thì.

**Bước 3: Dựng Context Menu cho LangBar Item**

- Cài đặt phương thức `InitMenu`: Nạp danh sách các tùy chọn nhanh qua `ITfMenu`:
  - Chuyển chế độ V/E.
  - Kiểu đặt dấu thanh: Kiểu mới / Kiểu cũ.
  - Kiểu gõ: Telex / VNI / Simple Telex.
  - Bảng mã: Unicode dựng sẵn / Unicode tổ hợp / TCVN3.
  - Tùy chọn ngữ pháp thông minh: Auto-restore tiếng Anh, repeat-key undo, leading `w` → `ư`.
  - Lối tắt mở cửa sổ Cài đặt và Thông tin.
- Cài đặt `OnMenuSelect`: Bắt ID mục được chọn để đổi cấu hình runtime qua `SharedMemoryManager`.

**Bước 4: Thiết kế Contract cấu hình (`config.json`)**

- Lưu trữ tại `%AppData%\BambooMintKey\config.json`.

- Cấu trúc tối giản:

  ```json
  {
    "version": 2,
    "inputMethod": 0,
    "charset": 0,
    "toggleHotkey": 0,
    "hotkeyVKey": 16,
    "hotkeyModifiers": 514,
    "toneStyle": 0,
    "autoRestoreEnglishWords": true,
    "allowRepeatKeyUndo": true,
    "allowLeadingWAsU": false,
    "startWithWindows": true,
    "macroEnabled": false,
    "macros": {
      "vn": "Việt Nam",
      "bmk": "BambooMintKey",
      "f#": "F-Sharp"
    }
  }
  ```

- F# Core cung cấp hàm nạp lại cấu hình (reload state) không cần restart ứng dụng.
- NativeBridge và UI đồng bộ tức thì qua Named Shared Memory `Local\BambooMintKey_SharedConfig_v1` (64 bytes) và Manual-Reset Event `Local\BambooMintKey_StateChangedEvent_v1`.

**Bước 5: Cửa sổ Cài đặt & Tích hợp `ITfFnConfigure`**

- Cài đặt COM Interface `ITfFnConfigure` trong NativeBridge: Khi người dùng bấm "Options" trong Windows Settings, Windows sẽ gọi phương thức `Show()`.
- Phương thức mở GUI được triển khai qua `SettingsLauncher.LaunchSettingsGui(string? argument)` để khởi chạy binary `BambooMintKey.UI.exe` nằm cùng thư mục với DLL NativeBridge.
- Sau khi người dùng nhấn "Áp dụng & Đóng" trên giao diện, GUI ghi đè `config.json`, ghi trực tiếp vào Shared Memory, tăng `StateSequence` và bắn Event để NativeBridge trong mọi tiến trình tự động đồng bộ phím tắt, icon và Compartment.

> **Tóm tắt:** Toàn bộ Phase 3 đã được triển khai và chạy ổn định trên Windows 10/11. Các milestone M1–M5 đều đã có implementation tương ứng trong `src/BambooMintKey.NativeBridge` và `src/BambooMintKey.UI`. Chi tiết kỹ thuật từng bước xem trong các tài liệu `003_03` đến `003_07` và log giải quyết vấn đề `003_08`/`003_09`.