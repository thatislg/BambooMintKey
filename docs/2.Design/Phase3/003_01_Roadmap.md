Lộ trình triển khai **Phase 3: User Interface & Context Management** cho BambooMintKey sau khi đã lược bỏ phần Candidate Window không cần thiết.

### Mục tiêu cốt lõi của Phase 3

- **Khả năng nhận diện trạng thái:** Cung cấp icon hiển thị động (**V** / **E**) tích hợp tự nhiên vào Windows Taskbar.
- **Tương tác nhanh:** Cho phép toggle chế độ gõ bằng click chuột trái, phím tắt nội bộ, hoặc menu chuột phải.
- **Cấu hình độc lập:** Xây dựng cửa sổ Settings GUI nhẹ, tách biệt, giao tiếp qua tệp cấu hình trung gian để sẵn sàng tái sử dụng khi đưa sang Linux.

### Lộ trình chi tiết Phase 3 (Roadmap)

| **Sprint / Milestone**                | **Hạng mục công việc**               | **Output kỹ thuật chính**                                    |
| ------------------------------------- | ------------------------------------ | ------------------------------------------------------------ |
| **M1: Taskbar Button COM Bridge**     | Cài đặt interface TSF LangBar        | `ITfLangBarItemButton`, `ITfLangBarItemSink`, struct VTable  |
| **M2: Icon Resource & State Binding** | Tạo icon động (V/E) và cơ chế toggle | Tài nguyên Icon Win32 GDI/`.ico`, cập nhật `BridgeStateManager` |
| **M3: Taskbar Context Menu**          | Menu chuột phải chuyển nhanh chế độ  | Cài đặt `InitMenu` & `OnMenuSelect` (Telex/VNI, Bảng mã, Mở Settings) |
| **M4: Shared Configuration Contract** | Chuẩn hóa schema cấu hình dùng chung | Tệp `config.json` và module parser trong F# Core             |
| **M5: Settings GUI (Standalone)**     | Cửa sổ cài đặt giao diện             | Binary GUI độc lập (Avalonia/WPF) + kết nối qua `ITfFnConfigure` |

### Kế hoạch hành động từng bước

**Bước 1: Triển khai `ITfLangBarItemButton` trên NativeBridge**

- Định nghĩa COM VTable cho `ITfLangBarItemButton` trong C# NativeAOT.
- Lấy `ITfLangBarItemMgr` từ `ITfThreadMgr` trong hàm `ActivateEx` để gọi `AddItem()`.
- Đăng ký hủy qua `RemoveItem()` trong hàm `Deactivate`.

**Bước 2: Xử lý Icon và sự kiện Click chuyển trạng thái**

- Tạo icon chữ **V** (màu đỏ) và **E** (màu xanh).
- Triển khai hàm `OnClick`: Khi click chuột trái vào icon, đảo cờ `IsVietnameseMode` trong engine.
- Gọi `ITfLangBarItemSink.OnUpdate(TF_LBI_ICON | TF_LBI_TOOLTIP)` để Windows Taskbar tự động vẽ lại icon tương ứng.

**Bước 3: Dựng Context Menu cho LangBar Item**

- Cài đặt phương thức `InitMenu`: Nạp danh sách các tùy chọn nhanh:
  - Kiểu gõ: Telex / VNI / Simple Telex.
  - Bảng mã: Unicode dựng sẵn / Unicode tổ hợp.
  - Lối tắt mở cửa sổ Cài đặt.
- Cài đặt `OnMenuSelect`: Bắt ID mục được chọn để đổi cấu hình runtime trong F# Core.

**Bước 4: Thiết kế Contract cấu hình (`config.json`)**

- Lưu trữ tại `%AppData%\BambooMintKey\config.json`.

- Cấu trúc tối giản:

  JSON

  ```
  {
    "inputMethod": "Telex",
    "charset": "Unicode",
    "hotkeyToggle": "CtrlShift",
    "spellCheck": true,
    "macroEnabled": true
  }
  ```

- F# Core cung cấp hàm nạp lại cấu hình (reload state) không cần restart ứng dụng.

**Bước 5: Cửa sổ Cài đặt & Tích hợp `ITfFnConfigure`**

- Cài đặt COM Interface `ITfFnConfigure` trong NativeBridge: Khi người dùng bấm "Options" trong Windows Settings, Windows sẽ gọi phương thức `Show()`.
- Phương thức `Show()` dùng `CreateProcess` để mở binary cấu hình độc lập (`BambooMintKey.Config.exe`).
- Sau khi người dùng nhấn "Lưu" trên giao diện, GUI ghi đè `config.json` và bắn thông điệp (hoặc file watcher) để NativeBridge tự động đồng bộ.

Bạn muốn bắt đầu ngay từ **Bước 1: Khai báo VTable và struct của `ITfLangBarItemButton` trong NativeBridge** hay chuẩn bị trước **schema cấu hình `config.json`**?