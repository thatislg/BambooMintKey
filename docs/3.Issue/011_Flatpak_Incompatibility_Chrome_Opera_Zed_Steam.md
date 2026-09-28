<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 011: Lỗi không kích hoạt bộ gõ trên Chrome, Opera, Zed và không nhận chữ trên Steam trong môi trường Fcitx 5 Flatpak

**Mã tài liệu:** `011_Flatpak_Incompatibility_Chrome_Opera_Zed_Steam`  
**Trạng thái:** 🟡 Đang điều tra & Thiết kế giải pháp kiến trúc theo chuẩn Mozc Addon  
**Mức độ nghiêm trọng:** Nghiêm trọng (Chặn mục tiêu phát hành Flatpak / Steam Deck)  
**Ngày ghi nhận:** 28/09/2026  

---

## 1. Mô tả hiện tượng

Trong quá trình nghiệm thu **Milestone 2 & 3 (Phase 9 - Đóng gói Flatpak)** trên hệ thống Linux (Linux Mint 22.3 chạy KDE Plasma Wayland), khi kích hoạt bộ gõ Fcitx 5 thông qua gói Flatpak (`org.fcitx.Fcitx5` cùng extension `org.fcitx.Fcitx5.Addon.BambooMintKey`):

1. **Nhóm ứng dụng hoạt động tốt:**
   - **Kate, Konsole, Firefox, LibreOffice:** Nhận diện bộ gõ bình thường, hiển thị icon [V]/[E], gõ tiếng Việt Telex chuẩn xác.

2. **Nhóm ứng dụng không thể kích hoạt (Không phản hồi):**
   - **Google Chrome (Flatpak) & Opera (Flatpak):** Bộ gõ hoàn toàn không nhận diện được tiêu điểm nhập liệu (Input Context). Người dùng bấm phím đổi chế độ hoặc gõ phím đều chỉ ra tiếng Anh thô, Fcitx 5 không kích hoạt.
   - **Zed Editor (Native binary trên Wayland):** Không kích hoạt được bộ gõ, không phản hồi phím gõ tiếng Việt.

3. **Nhóm ứng dụng kích hoạt được nhưng không gõ được tiếng Việt:**
   - **Steam (Native 32-bit chạy qua XWayland):** Trạng thái Fcitx 5 chuyển sang [V] khi bấm vào ô tìm kiếm hoặc khung chat, nhưng khi gõ các ký tự (ví dụ: `v`, `i`, `e`, `t`), màn hình không hiển thị bất kỳ ký tự nào đang soạn. Toàn bộ phím dường như bị nuốt chửng mà không sinh ra chữ.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. Nhóm trình duyệt nhân Chromium (Google Chrome & Opera Flatpak)
1. **Chromium trên Wayland mặc định tắt IME:**
   - Khi chạy với cờ `--ozone-platform=wayland`, nhân Chromium mặc định tắt giao thức nhập liệu Wayland (`zwp_text_input_v1`/`v3`). Trừ khi ứng dụng được truyền cờ `--enable-wayland-ime`, Chromium không gửi bất kỳ yêu cầu tạo InputContext nào tới Wayland Compositor (KWin). Do đó KWin không thể thông báo cho Fcitx 5 biết để kích hoạt.
2. **Cách ly D-Bus trong Sandbox của Flatpak (⚠️ ĐÃ ĐÍNH CHÍNH):**
   - ~~Cả Chrome và Opera đều chạy trong sandbox Flatpak riêng biệt (`com.google.Chrome`, `com.opera.Opera`). Chính sách quyền (`Session Bus Policy`) mặc định của gói trình duyệt không có quyền `--talk-name=org.fcitx.Fcitx5`. Vì vậy trình duyệt bị sandbox chặn hoàn toàn khi muốn giao tiếp D-Bus trực tiếp với tiến trình Fcitx 5.~~
   - **Đính chính:** Chromium **không** nói D-Bus trực tiếp tới Fcitx5; nó dùng Wayland `zwp_text_input_v3` (qua KWin) hoặc XIM. Vì vậy quyền `--talk-name=org.fcitx.Fcitx5` **không phải** nguyên nhân. Xem chi tiết tại [009_08_App_Compatibility_Fix_Matrix.md](../2.Design/Phase9/009_08_App_Compatibility_Fix_Matrix.md).

### 2.2. Nhóm trình soạn thảo Rust GPUI (Zed Editor)
1. **Kiến trúc đồ họa riêng biệt:**
   - Zed không sử dụng toolkit GTK hay Qt, do đó các module giao tiếp phổ biến như `GTK_IM_MODULE=fcitx` hay `QT_IM_MODULE=fcitx` không có hiệu lực.
2. **Giao thức Wayland text-input chưa hoàn thiện:**
   - Framework đồ họa GPUI của Zed trên Linux Wayland giao tiếp trực tiếp qua `wayland-client`. Triển khai giao thức `zwp_text_input_v3` của Zed hiện còn nhiều lỗi trong việc bắt tay (handshake) với bộ phối màn hình KWin khi Fcitx 5 chạy tách biệt.
   - Zed khi khởi chạy thiếu biến môi trường `XMODIFIERS=@im=fcitx` nên không thể tự động fallback về kênh XIM.

### 2.3. Nhóm ứng dụng XIM cổ điển (Steam Client) — ✅ ĐÃ GIẢI QUYẾT

> **Kết luận thực nghiệm (đã xác minh):** Steam **có** hỗ trợ input method, nhưng cần đúng biến môi trường. Nguyên nhân gốc: Steam là app **32-bit**, dùng **CEF (nền GTK)** cho ô nhập liệu. Kênh `GTK_IM_MODULE=fcitx` nạp `libfcitx5gclient.so` (64-bit) → Steam 32-bit không nạp được → không kết nối. Kênh `GTK_IM_MODULE=xim` dùng giao thức XIM thuần túy (chỉ cần libX11) → hoạt động.
>
> **Giải pháp đã xác minh hoạt động (cả native lẫn Flatpak):**
> ```bash
> env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" steam
> ```
> Wrapper tự động: `scripts/linux/steam-ime.sh` (hoặc `--install` để cài `~/.local/bin/steam`).

*(Lịch sử chẩn đoán: ban đầu suy đoán engine nuốt phím → sau đó suy đoán Steam không hỗ trợ XIM → cuối cùng xác định đúng nguyên nhân là `GTK_IM_MODULE=xim` cho app 32-bit.)*

1. **Nguyên nhân gốc thật — app 32-bit không nạp được IM module 64-bit:**
   - Steam (32-bit) dùng CEF/GTK cho khung chat và ô tìm kiếm.
   - `GTK_IM_MODULE=fcitx` yêu cầu `libfcitx5gclient.so` (64-bit) qua D-Bus → Steam không nạp được → im lặng không kết nối.
   - `GTK_IM_MODULE=xim` đi qua giao thức XIM (không cần thư viện client theo kiến trúc) → Steam kết nối XIM server của Fcitx5 thành công.
2. **Vai trò của tái cấu trúc engine (M3.6):**
   - Việc phân nhánh `CapabilityFlag::Preedit` + gọi `updateUserInterface(InputPanel)` vẫn đúng và cần thiết để Steam (thiếu client preedit) hiển thị popup ứng viên thay vì nuốt phím — bổ trợ cho fix môi trường ở trên, chứ không thay thế.

---

## 3. Đối chiếu kiến trúc chuẩn từ Mozc Addon (`fcitx5-mozc`)

> **Sơ đồ kiến trúc chuẩn đầy đủ (kim chỉ nam):** [009_07_Mozc_Reference_Architecture.md](../2.Design/Phase9/009_07_Mozc_Reference_Architecture.md)

Bộ gõ Mozc tiếng Nhật (`org.fcitx.Fcitx5.Addon.Mozc`) là addon chuẩn mực hoạt động bền bỉ trên Flatpak và SteamOS. Qua phân tích mã nguồn upstream (`src/unix/fcitx5/mozc_state.cc` và `mozc_response_parser.cc`), Mozc xử lý vấn đề này rất chuẩn xác:

### 3.1. Phân biệt rạch ròi giữa Client Preedit và InputPanel Preedit
Trong `MozcState::DrawAll()`:
```cpp
if (ic_->capabilityFlags().test(CapabilityFlag::Preedit)) {
    // Ứng dụng hỗ trợ inline preedit (Kate, Firefox, LibreOffice)
    ic_->inputPanel().setClientPreedit(preedit);
} else {
    // Ứng dụng KHÔNG hỗ trợ inline preedit (Steam, XIM legacy apps)
    // Tự động chuyển chuỗi dở dang sang vẽ ở cửa sổ popup InputPanel
    ic_->inputPanel().setPreedit(preedit);
}
ic_->updatePreedit();
// Luôn yêu cầu Fcitx5 làm mới giao diện UI InputPanel
ic_->updateUserInterface(UserInterfaceComponent::InputPanel);
```

### 3.2. Cơ chế cách ly Engine và UI
- Mozc tách rời tiến trình xử lý từ vựng (`mozc_server`) với addon Fcitx 5.
- Addon Fcitx 5 chỉ đóng vai trò là một client mỏng (Thin Client IPC), tuân thủ 100% vòng đời sự kiện của Fcitx 5 `InputContext`.

---

## 4. Đánh giá tác động toàn diện (Impact Assessment)

Khi triển khai giải pháp tái cấu trúc `BambooMintKeyEngine` theo kiến trúc chuẩn Mozc (`fcitx5-mozc`), nhóm phát triển đánh giá tác động chi tiết tới hai nền tảng cốt lõi của dự án:

### 4.1. Đánh giá tác động tới nền tảng Linux (Fcitx 5 Native & Flatpak)
- **Phạm vi tác động mã nguồn:** Tập trung hoàn toàn bên trong module C++ Addon tại [`src/BambooMintKey.Fcitx5/engine.cpp`](../../src/BambooMintKey.Fcitx5/engine.cpp) và [`engine.h`](../../src/BambooMintKey.Fcitx5/engine.h).
- **Tác động tích cực trực tiếp:**
  1. **Khắc phục triệt để lỗi mất chữ trên Steam:** Khi chạy Steam (hoặc bất kỳ ứng dụng XIM legacy nào thiếu `CapabilityFlag::Preedit`), cơ chế mới sẽ tự động chuyển preedit sang vẽ tại cửa sổ popup nổi của Fcitx 5 (`setPreedit`) và gọi `updateUserInterface(InputPanel)`. Người dùng sẽ nhìn thấy rõ ràng từ đang gõ dở dưới dạng candidate popup thay vì bị nuốt phím.
  2. **Nâng cao chất lượng cho cả 2 kênh phân phối Linux:** Cả gói cài đặt Native hệ thống (DEB trên Ubuntu/Debian/Linux Mint, RPM trên Fedora) lẫn gói Flathub Extension (SteamOS / Flatpak) đều dùng chung mã nguồn C++ Addon này. Do đó, toàn bộ người dùng Linux đều được thừa hưởng tính tương thích cao này mà không phải cấu hình thủ công.
- **Rủi ro tiềm ẩn & Biện pháp kiểm soát:**
  - *Rủi ro:* Cửa sổ popup InputPanel vô tình bật lên trên các ứng dụng vốn đã hỗ trợ inline preedit tốt (Kate, Firefox, LibreOffice), gây rối mắt.
  - *Kiểm soát:* Áp dụng chuẩn logic kiểm tra rẽ nhánh của Mozc:
    ```cpp
    if (ic_->capabilityFlags().test(CapabilityFlag::Preedit)) {
        ic_->inputPanel().setClientPreedit(preedit); // Chỉ vẽ inline
    } else {
        ic_->inputPanel().setPreedit(preedit);       // Chỉ bật popup khi app thiếu preedit
    }
    ```
- **Tác động tới lõi thuật toán Core F# và C-ABI:** **0% (Zero Source Intrusion)**. Không thay đổi bất kỳ logic gõ Telex nào trong `BambooMintKey.Core`, không thay đổi chữ ký hàm trong [`cabibridge.h`](../../src/BambooMintKey.Fcitx5/cabibridge.h).

### 4.2. Đánh giá tác động tới nền tảng Windows (TSF COM Server & Win32 UI)
- **Mức độ tác động:** **0% — HOÀN TOÀN KHÔNG BỊ ẢNH HƯỞNG (Zero Impact)**.
- **Lý do kỹ thuật và căn cứ kiến trúc:**
  1. **Kiến trúc phân ly độc lập (Decoupled Architecture):** Nền tảng Windows sử dụng Text Services Framework (TSF) COM Server thuần túy (`src/BambooMintKey.Tsf/`) và giao diện cài đặt WPF C# (`src/BambooMintKey.UI/`). Module `src/BambooMintKey.Fcitx5/` là thành phần dành riêng cho Linux, hoàn toàn không được liên kết hay biên dịch trên môi trường Windows.
  2. **Bảo toàn C-ABI NativeAOT:** Lõi xử lý từ vựng F# (`BambooMintKey.Core`) và bản xuất C-ABI qua C# NativeAOT (`BambooMintKey.Core.Native`) không bị chỉnh sửa. Toàn bộ các API xuất khẩu như `bmk_process_key`, `bmk_get_preedit_text`, `bmk_get_commit_text` giữ nguyên vẹn 100%. TSF trên Windows tiếp tục gọi các API này bình thường.
  3. **Độc lập tiến độ phát triển Windows:** Các hạng mục đang thử nghiệm của Phase 5 (Display Attribute, Preedit toggle, Global V/E mode) và kế hoạch đóng gói Win32 Inno Setup của Phase 6 trên Windows không bị gián đoạn hay chịu bất kỳ rủi ro hồi quy (regression) nào.

### 4.3. Bảng ma trận tổng hợp tác động

| Thành phần dự án | File liên quan | Mức độ tác động | Hệ quả dự kiến |
| :--- | :--- | :---: | :--- |
| **Linux C++ Addon** | `src/BambooMintKey.Fcitx5/engine.cpp` | **Cao** *(Trọng tâm thay đổi)* | Áp dụng `DrawAll()` của Mozc; sửa dứt điểm lỗi gõ chữ trên Steam XIM; giữ inline sạch cho Kate/Firefox. |
| **Linux Native Packages** | Gói DEB, RPM | **Thấp** *(Tự động thừa hưởng)* | Chỉ cần recompile module Addon C++; tăng độ ổn định trên toàn bộ các bản phân phối Linux. |
| **Flatpak Manifest / Sandbox** | `manifests/flatpak/` | **Không tác động** | Giữ nguyên manifest offline build đạt chuẩn M2; không cần thay đổi quyền sandbox. |
| **Core F# & C-ABI** | `src/BambooMintKey.Core*` | **0% (Không ảnh hưởng)** | Giữ nguyên 100% thuật toán gõ Telex và hợp đồng C-ABI. |
| **Windows TSF & UI** | `src/BambooMintKey.Tsf`, `src/BambooMintKey.UI` | **0% (Không ảnh hưởng)** | Hoàn toàn độc lập, không có bất kỳ rủi ro hồi quy nào. |

---

## 5. Kế hoạch hành động giải quyết (Action Plan)

> **Cập nhật tiến độ:** Tài liệu ma trận fix chi tiết tại [009_08_App_Compatibility_Fix_Matrix.md](../2.Design/Phase9/009_08_App_Compatibility_Fix_Matrix.md); script hỗ trợ cấu hình môi trường tại [`scripts/linux/setup_ime_compat.sh`](../../scripts/linux/setup_ime_compat.sh).

| STT | Nhiệm vụ | Phạm vi | Mục tiêu | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **1** | **Chuyển ngay hệ thống về Fcitx 5 Native** | Hệ thống | Trả lại môi trường gõ ổn định tức thì cho người dùng bằng script `switch_fcitx.sh native`. | ✅ |
| **2** | **Tái cấu trúc `engine.cpp` theo chuẩn Mozc** | `src/BambooMintKey.Fcitx5/` | Bổ sung kiểm tra `CapabilityFlag::Preedit`. Nếu app không có preedit (như Steam), tự động vẽ qua `InputPanel` popup và gọi `updateUserInterface(InputPanel)`. | ✅ (M3.6) |
| **3** | **Bổ sung cơ chế Fallback Commit cho XIM** | `src/BambooMintKey.Fcitx5/` | Xử lý kịch bản cho các ứng dụng hoàn toàn không nhận diện cả popup để đảm bảo phím không bị nuốt mất. | ✅ (`flushPendingComposition`) |
| **4** | **Nghiên cứu cơ chế tích hợp Wayland/Flatpak của SteamOS** | `manifests/flatpak/` | Khảo sát cách `org.fcitx.Fcitx5` tương tác với ứng dụng Game Mode / Desktop Mode; kết quả: fix nằm ở tầng môi trường (XMODIFIERS, cờ Chromium) không cần đổi manifest. | ✅ (ma trận fix) |

---

## 6. Kết luận

**Đã giải quyết triệt để Steam:**

1. **Steam — đã fix:** Nguyên nhân gốc là Steam (app 32-bit) dùng CEF/GTK nhưng `GTK_IM_MODULE=fcitx` không nạp được `libfcitx5gclient.so` (64-bit). Giải pháp: khởi chạy Steam với `GTK_IM_MODULE=xim` + `XMODIFIERS=@im=fcitx` (đã xác minh hoạt động cả native lẫn Flatpak):
   ```bash
   env XMODIFIERS="@im=fcitx" GTK_IM_MODULE="xim" QT_IM_MODULE="xim" steam
   ```
   Wrapper tự động: `scripts/linux/steam-ime.sh` (hoặc `--install`).

2. **Chrome/Opera/Zed (Flatpak):** nhóm thực sự bị ảnh hưởng bởi sandbox Flatpak / Wayland text-input (gõ tốt native nhưng fail trên Flatpak). Xem [009_08_App_Compatibility_Fix_Matrix.md](../2.Design/Phase9/009_08_App_Compatibility_Fix_Matrix.md).

Việc tái cấu trúc engine theo chuẩn Mozc (M3.6) được giữ lại như cải tiến kiến trúc đúng đắn (phân nhánh `CapabilityFlag::Preedit`, đồng bộ `updateUserInterface(InputPanel)`, flush khi mất focus) — giúp Steam (thiếu client preedit) hiển thị popup ứng viên đúng cách, bổ trợ cho fix môi trường ở trên.
