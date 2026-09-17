# 005.02 - Giải pháp sửa lỗi biểu tượng E/V trên Taskbar không phản ánh khi chuyển chế độ

## 1. Tóm tắt vấn đề & Kết quả điều tra thực nghiệm

- **Hiện tượng**: Phím tắt chuyển đổi E/V (`~` dưới Esc và phím Hankaku/Zenkaku) gõ rất mượt, phản hồi nhanh, không mất thời gian, không kẹt phím. Tuy nhiên biểu tượng E/V trên Taskbar của Windows không phản ánh thay đổi.
- **Bằng chứng từ `BambooMintKey_Runtime.log`**:
  - Khi luồng nền (Worker thread 3, 5, 7) trong `explorer.exe` nhận sự kiện đổi trạng thái và gọi `LangBarItemButton.NotifyStateChanged()` -> `sink->OnUpdate()`, Windows Taskbar hoàn toàn **không gọi lại `GetIcon`**.
  - Khi luồng giao diện UI/STA của Explorer (Thread 6) gọi `LangBarItemButton.NotifyStateChanged()`, chỉ **54 miligiây sau**, Explorer gọi ngay `GetIcon`, yêu cầu icon 'E' và vẽ lại thành công!

## 2. Nguyên nhân kỹ thuật

1. **Quy định của Windows TSF COM (`ITfLangBarItemSink::OnUpdate`)**:
   - `_pLangBarSink` là một interface COM thuộc căn hộ đơn luồng (STA - Single Threaded Apartment) được tạo trên UI Thread của Explorer/ứng dụng.
   - Tài liệu kỹ thuật Windows TSF quy định rõ: *The caller must call this method from the same thread that called ITfLangBarItemMgr::AddItem*.
   - Việc gọi trực tiếp qua con trỏ thô từ một worker thread (MTA) khiến Windows TSF bỏ qua thông báo cập nhật vì vi phạm quy tắc thread căn hộ.
2. **Cấu hình kiểu nút (`dwStyle`) và cờ thông báo (`OnUpdate`)**:
   - Hiện tại đang dùng `TfLbiStyleBtnToggle | TfLbiStyleShownInTray` và thiếu cờ `TfLbiStatus` trong `OnUpdate`.
   - Chuẩn Google IME / Mozc (`tip_lang_bar_menu.cc`) sử dụng `TF_LBI_STYLE_BTN_BUTTON | TF_LBI_STYLE_SHOWNINTRAY`, và gửi `TF_LBI_ICON | TF_LBI_STATUS | TF_LBI_TEXT` mỗi khi trạng thái thay đổi.
3. **Cập nhật Compartment cục bộ thay vì toàn cục (Global Compartment)**:
   - `TsfCompartmentHelper` hiện chỉ cập nhật Compartment của Thread hiện tại (`QueryInterface(IID_ITfCompartmentMgr)`).
   - Windows 10/11 Input Indicator trên Taskbar theo dõi **Global Compartment** (`ITfThreadMgr::GetGlobalCompartment`).

## 3. Thiết kế giải pháp

1. **Tạo Message-Only Window (`HWND_MESSAGE`) để điều phối về UI Thread**:
   - Khi `LangBarItemButton.Register()` được gọi trên UI Thread: tạo một cửa sổ `HWND_MESSAGE` ẩn.
   - Khi luồng nền phát hiện thay đổi trạng thái: gọi `PostMessageW(hMsgWnd, WM_STATE_CHANGED, 0, 0)`.
   - `WndProc` trên UI Thread nhận thông điệp và gọi `OnUpdate` trực tiếp trên UI Thread.
2. **Chuẩn hóa cấu hình nút theo chuẩn Google Mozc**:
   - `dwStyle = TsfLangBarFlags.TfLbiStyleBtnButton | TsfLangBarFlags.TfLbiStyleShownInTray;`
   - `GetStatus`: trả về `BridgeStateManager.IsVietnameseMode ? TsfLangBarFlags.TfLbiStatusBtnToggled : 0;`
   - `OnUpdate`: bổ sung cờ `TsfLangBarFlags.TfLbiStatus`.
3. **Bổ sung đồng bộ Global Compartment**:
   - Cài đặt gọi `ITfThreadMgr::GetGlobalCompartment` (vtable slot 11) để cập nhật `GUID_COMPARTMENT_KEYBOARD_OPENCLOSE` và `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` vào Global Compartment.
