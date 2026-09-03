# 003_07_SettingsGUI_FnConfigure.md — Thiết kế Chi tiết Bảng Điều Khiển Cài Đặt (BambooMintKey.UI) & Tùy Biến Phím Tắt, Kiểu Gõ, Bảng Mã, Giao Diện About

> **Tài liệu liên quan:**  
> - Thiết kế Context Menu chuột phải: `docs/2.Design/Phase3/003_05_TaskbarContextMenu.md`  
> - Thiết kế Schema cấu hình dùng chung: `docs/2.Design/Phase3/003_06_SharedConfiguration_Schema.md`  
> - Thiết kế giải pháp đồng bộ nền tảng: `docs/2.Design/Phase3/003_09_IssuesSolution.md`  
> - Nền tảng mục tiêu: **Windows 10 và Windows 11 (64-bit)**. Ứng dụng GUI viết bằng **Avalonia UI (F#)** đa nền tảng.

---

## 1. Mục tiêu & Cơ sở Chuẩn hóa

### 1.1. Mục tiêu Cốt lõi
1. **Bảng điều khiển Cài đặt (`BambooMintKey.UI`):** Xây dựng ứng dụng giao diện đồ họa độc lập, khởi động tức thì (< 0.2s), phong cách **Bamboo Mint** sang trọng, cho phép người dùng tùy biến toàn bộ trải nghiệm gõ tiếng Việt.
2. **Tùy biến Phím tắt (Dynamic Hotkey Configuration):** Phím tắt chuyển đổi chế độ V/E (Ctrl + Shift, Alt + Z, Ctrl + Space,...) không còn bị fix cứng, mà người dùng có thể tự do lựa chọn trong giao diện cài đặt và TSF sẽ cập nhật đăng ký tức thì vào hệ thống.
3. **Quản lý Kiểu gõ & Bảng mã:** Cung cấp đầy đủ các lựa chọn Kiểu gõ (`Telex`, `VNI`, `Simple Telex`) và Bảng mã (`Unicode dựng sẵn`, `Unicode tổ hợp`, `TCVN3`) trên cả giao diện cài đặt lẫn Context Menu chuột phải, đồng bộ trạng thái qua `SharedMemoryManager` và `config.json`.
4. **Giao diện Thông tin Ứng dụng (About Dialog):** Tích hợp trực tiếp màn hình About trong ứng dụng (logo cây tre, phiên bản, tác giả, bản quyền, link tài liệu) thay vì chuyển hướng người dùng ra trình duyệt web bên ngoài.

### 1.2. Chuẩn hóa COM Windows SDK
Theo `msctf.idl` của Windows SDK:
- Nút **Options** trong `Windows Settings -> Time & Language -> Language -> Preferred Languages -> [Tiếng Việt] -> Options -> BambooMintKey` yêu cầu triển khai `ITfFunctionProvider` và `ITfFnConfigure`.
- Khi người dùng bấm nút Options (hoặc bấm "Bảng điều khiển & Cài đặt..." từ Context Menu Taskbar), Windows/NativeBridge sẽ gọi `ITfFnConfigure::Show()` để khởi chạy ứng dụng GUI.

| **Thành phần** | **File SDK gốc** | **GUID chuẩn xác** |
|---|---|---|
| `IID_ITfFunctionProvider` | `msctf.idl` | `101D9462-0E4E-41F1-B34B-E1EF37E02F0D` |
| `IID_ITfFunction` | `msctf.idl` | `DB593490-238F-11D8-9E28-0007E912B864` |
| `IID_ITfFnConfigure` | `msctf.idl` | `88F567C6-1757-49F8-A1B2-89234C1EEFF9` |

---

## 2. Hệ Thống Thẩm Mỹ & Màu Sắc "Bamboo Mint" (Visual Identity)

Giao diện cài đặt được thiết kế theo phong cách **Fluent Acrylic Glassmorphism** hiện đại trên nền tối sang trọng:

| Token | Mã Hex | Ứng dụng trong Giao diện Cài đặt |
|---|---|---|
| **Bamboo Primary** | `#16a34a` (Xanh tre) | Nút bấm chính (Save/Apply), tiêu đề tab đang chọn, header badge |
| **Mint Accent** | `#22c55e` (Xanh bạc hà) | Toggle switch khi bật (ON), viền focus, chấm radio active |
| **Mint Glow** | `#4ade80` (Xanh Mint neon) | Hiệu ứng phát sáng nhẹ quanh ô gõ thử nghiệm (Live Sandbox) |
| **Deep Forest Dark** | `#111613` (Đen ánh rêu) | Màu nền cửa sổ chính bán trong suốt (Acrylic Window) |
| **Card Surface** | `#18221c` (Thẻ Acrylic) | Màu nền các khối nhóm cài đặt (Container Cards) |
| **Card Border** | `#22c55e20` (Viền xanh mờ 12%) | Đường viền phân cách bo góc 8px - 12px tinh tế |
| **Text Bright** | `#f8fafc` (Trắng sáng) | Tiêu đề và nhãn chữ chính |
| **Text Muted** | `#94a3b8` (Xám bạc) | Chú thích hướng dẫn, phiên bản, thông tin phụ |

---

## 3. Cấu Trúc Bố Cục Giao Diện (`BambooMintKey.UI`)

Cửa sổ có kích thước tiêu chuẩn `520 x 480` pixel, căn giữa màn hình (`CenterScreen`), không cho phép resize méo giao diện, gồm thanh tiêu đề thương hiệu và **4 Tab chức năng**:

```
┌────────────────────────────────────────────────────────────────────────┐
│  🎍 BambooMintKey — Bảng Điều Khiển Cài Đặt                     [—][✕] │
├────────────────────────────────────────────────────────────────────────┤
│  [ Bàn phím & Phím tắt ]  [ Tùy chọn gõ ]  [ Gõ thử nghiệm ]  [ Thông tin ]  │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  [ TAB 1: BÀN PHÍM & PHÍM TẮT ]                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ ⌨ Kiểu gõ chính:                                                 │  │
│  │   (•) Telex         ( ) VNI         ( ) Simple Telex             │  │
│  ├──────────────────────────────────────────────────────────────────┤  │
│  │ 🔤 Bảng mã đầu ra:                                               │  │
│  │   [ Unicode dựng sẵn (Mặc định)                     ▼ ]          │  │
│  ├──────────────────────────────────────────────────────────────────┤  │
│  │ ⚡ Phím tắt chuyển đổi Việt / Anh:                                │  │
│  │   [ Ctrl + Shift (Mặc định)                         ▼ ]          │  │
│  │   (Tùy chọn: Ctrl+Shift, Alt+Z, Ctrl+Space, Không sử dụng)        │  │
│  ├──────────────────────────────────────────────────────────────────┤  │
│  │ 🚀 Hệ thống:                                                     │  │
│  │   [✓] Khởi động cùng Windows                                     │  │
│  │   [✓] Bật âm thanh click nhẹ khi chuyển đổi (Tùy chọn)           │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│                                     [ Mặc định ]  [ Áp dụng ]  [ Đóng ]│
└────────────────────────────────────────────────────────────────────────┘
```

### Chi tiết 4 Tab Chức năng:

#### Tab 1: Bàn phím & Phím tắt (Keyboard & Hotkeys)
- **Kiểu gõ chính:** 3 lựa chọn Radio Button:
  - `Telex` (mặc định)
  - `VNI`
  - `Simple Telex`
- **Bảng mã đầu ra:** Dropdown ComboBox:
  - `Unicode dựng sẵn` (Precomposed - chuẩn quốc tế)
  - `Unicode tổ hợp` (Decomposed)
  - `TCVN3 (ABC)` (Phục vụ văn phòng legacy)
- **Phím tắt chuyển đổi chế độ gõ (Toggle Hotkey):**
  - `Ctrl + Shift` (Phổ biến nhất tại Việt Nam)
  - `Alt + Z` (Tránh xung đột phím tắt đồ họa Adobe/Figma)
  - `Ctrl + Space` (Chuẩn macOS/Linux)
  - `Không sử dụng` (Chỉ chuyển đổi bằng click chuột trên Taskbar)
- **Khởi động cùng Windows:** Ghi vào Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

#### Tab 2: Tùy chọn gõ (Typing Options)
- **Quy chuẩn đặt vị trí dấu thanh:**
  - `Kiểu mới (hòa, thúy, xòe)` — Mặc định theo chuẩn ngôn ngữ học hiện đại.
  - `Kiểu cũ (hoá, thuý, xoè)` — Chuẩn truyền thống.
- **Tự động khôi phục từ tiếng Anh (Auto Restore):**
  - Toggle Switch bật/tắt `AutoRestoreEnglishWords`. Khi gõ từ sai ngữ pháp tiếng Việt (như `word`, `start`), bộ gõ tự trả về ký tự thô.
- **Gõ lặp dấu để khôi phục ký tự thô (Repeat Key Undo):**
  - Toggle Switch bật/tắt `AllowRepeatKeyUndo` (`ss -> s`, `aa -> a`).
- **Phím 'w' đứng đầu từ biến thành 'ư':**
  - Toggle Switch bật/tắt `AllowLeadingWAsU` (`w -> ư`).

#### Tab 3: Gõ thử nghiệm trực tiếp (Live Typing Sandbox)
- Khung văn bản soạn thảo trực tiếp nằm giữa cửa sổ với hiệu ứng viền xanh Mint phát sáng (`#22c55e33`).
- Cho phép người dùng vừa đổi kiểu gõ, bảng mã hay phím tắt là có thể **gõ thử nghiệm ngay lập tức** trong hộp thoại để kiểm tra mà không cần mở Notepad hay phần mềm khác.
- Có nút `[ Xóa trắng ]` để làm sạch khung test.

#### Tab 4: Thông tin BambooMintKey (About BambooMintKey)
- Màn hình thông tin chính thức của ứng dụng:
  - **Logo cây tre/búp măng xanh phát sáng:** Biểu tượng nhận diện của BambooMintKey.
  - **Tên phần mềm:** `BambooMintKey — Bộ Gõ Tiếng Việt Thế Hệ Mới`.
  - **Phiên bản:** `Phiên bản 1.0.0 (NativeAOT & Pure F# Core)`.
  - **Kiến trúc:** Windows Text Services Framework (TSF) TIP 64-bit.
  - **Tác giả:** Dương Gia Long & LMO contributors.
  - **Bản quyền:** Mã nguồn mở theo giấy phép MIT License.
  - **Các nút tương tác nội bộ:**
    - `[ 🌐 Trang chủ Dự án ]`: Mở liên kết trình duyệt tới repo GitHub.
    - `[ 📖 Hướng dẫn Sử dụng ]`: Mở tài liệu hướng dẫn gõ Telex & phím tắt.
    - `[ 🔄 Kiểm tra Cập nhật ]`: Kiểm tra phiên bản mới từ GitHub Releases.

---

## 4. Kiến Trúc Đồng Bộ Hai Chiều (Dual-Sync Architecture)

```
       [ Giao diện BambooMintKey.UI ] ◄──────► [ Menu Chuột Phải Taskbar ]
                      │                                    │
                      ▼                                    ▼
       ┌────────────────────────────────────────────────────────┐
       │             SharedMemoryManager (64 Bytes)             │
       │  [0] IsVietnameseMode     (1 = V, 0 = E)               │
       │  [1] ToneStyle            (0 = Mới, 1 = Cũ)            │
       │  [2] AutoRestoreEnglish   (1 = Bật, 0 = Tắt)           │
       │  [3] AllowRepeatKeyUndo   (1 = Bật, 0 = Tắt)           │
       │  [4] AllowLeadingWAsU     (1 = Bật, 0 = Tắt)           │
       │  [5] InputMethod          (0 = Telex, 1 = VNI, 2 = S.) │
       │  [6] Charset              (0 = Unicode, 1 = Tổ hợp,...)│
       │  [7] ToggleHotkey         (0 = CtrlShift, 1 = AltZ,...)│
       │  [8-11] StateSequence     (Bộ đếm phiên bản broadcast) │
       └───────────────────────────┬────────────────────────────┘
                                   │
                                   ▼
        ┌──────────────────────────────────────────────────────┐
        │        Tệp Cấu Hình Bền Vững (config.json)           │
        │   %AppData%\BambooMintKey\config.json                │
        └──────────────────────────────────────────────────────┘
                                   │
                                   ▼
        ┌──────────────────────────────────────────────────────┐
        │  Mọi Ứng Dụng Đang Mở (Notepad, Word, Chrome,...)    │
        │  - Tự động nạp cấu hình mới qua SignalStateChanged() │
        │  - Đăng ký lại PreservedKey theo ToggleHotkey mới    │
        │  - Cập nhật EngineConfig thời gian thực (0ms lag)!   │
        └──────────────────────────────────────────────────────┘
```

---

## 5. Hiện Thực Hóa Đăng Ký Phím Tắt Động (`KeyEventSinkHelper.cs`)

Khi `SharedMemoryManager.ToggleHotkey` thay đổi:
- `0`: Đăng ký `Ctrl + Shift` (`0x10`, `Control | Shift | OnKeyUp`).
- `1`: Đăng ký `Alt + Z` (`0x5A`, `Alt`).
- `2`: Đăng ký `Ctrl + Space` (`0x20`, `Control`).
- `3`: Không đăng ký phím tắt (vô hiệu hóa Preserved Key).

```csharp
public static void UpdatePreservedKeys(IntPtr pThreadMgr, uint clientId, byte hotkeyMode)
{
    // 1. Hủy toàn bộ phím tắt cũ
    UnregisterPreservedKeys(pThreadMgr, clientId);

    // 2. Đăng ký phím mới tương ứng với hotkeyMode
    if (hotkeyMode == 3) return; // 3 = None

    (uint vKey, uint modifiers, string desc) keyConfig = hotkeyMode switch
    {
        1 => (0x5A, TsfModFlags.Alt, "BambooMintKey Toggle (Alt+Z)"),
        2 => (0x20, TsfModFlags.Control, "BambooMintKey Toggle (Ctrl+Space)"),
        _ => (0x10, TsfModFlags.Control | TsfModFlags.OnKeyUp, "BambooMintKey Toggle (Ctrl+Shift)")
    };

    RegisterSinglePreservedKey(pThreadMgr, clientId, keyConfig.vKey, keyConfig.modifiers, keyConfig.desc);
}
```

---

## 6. Hiện Thực Hóa Mở Giao Diện Cài Đặt & Mở Thẳng Tab About

Trong `SettingsLauncher.cs`:

```csharp
public static class SettingsLauncher
{
    public static void LaunchSettingsGui(string? argument = null)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string uiPath = Path.Combine(baseDir, "BambooMintKey.UI.exe");

            if (!File.Exists(uiPath))
            {
                uiPath = Path.Combine(baseDir, "..", "..", "publish", "win-x64", "BambooMintKey.UI.exe");
            }

            if (File.Exists(uiPath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uiPath,
                    Arguments = argument ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                DebugLog.Write($"SettingsLauncher: Không tìm thấy file {uiPath}");
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"SettingsLauncher Exception: {ex.Message}");
        }
    }
}
```

Khi người dùng bấm từ Context Menu:
- Mục **"Bảng điều khiển & Cài đặt..."** $\rightarrow$ gọi `SettingsLauncher.LaunchSettingsGui()`.
- Mục **"Thông tin BambooMintKey"** $\rightarrow$ gọi `SettingsLauncher.LaunchSettingsGui("--about")` $\rightarrow$ Cửa sổ `BambooMintKey.UI` mở lên và **tự động chọn Tab 4 (Thông tin)**.

---

## 7. Quy Trình Kiểm Thử & Tiêu Chí Nghiệm Thu

1. **Khởi chạy Cài đặt từ Context Menu:**
   - Click chuột phải icon Taskbar $\rightarrow$ Chọn *Bảng điều khiển & Cài đặt...* $\rightarrow$ Cửa sổ Avalonia UI mở lên trong vòng 200ms với giao diện tông màu Bamboo Mint rực rỡ, nền Dark Acrylic.
2. **Khởi chạy Tab About:**
   - Click chuột phải icon Taskbar $\rightarrow$ Chọn *Thông tin BambooMintKey* $\rightarrow$ Cửa sổ mở lên tại đúng Tab Thông tin, hiển thị logo cây tre `🎍`, phiên bản `v1.0.0`, bản quyền MIT và tác giả (không bật trình duyệt web).
3. **Kiểm tra Tùy biến Phím tắt:**
   - Trong Tab Bàn phím: Đổi phím tắt từ `Ctrl + Shift` sang `Alt + Z` $\rightarrow$ bấm *Áp dụng*.
   - Mở Notepad $\rightarrow$ Bấm `Alt + Z` $\rightarrow$ Icon Taskbar chuyển đổi ngay lập tức giữa **V** và **E**. Bấm `Ctrl + Shift` không còn làm đảo icon nữa.
4. **Kiểm tra Chuyển Kiểu gõ & Bảng mã trên Context Menu:**
   - Click chuột phải vào icon Taskbar $\rightarrow$ Thấy 2 Submenu *Kiểu gõ* (Telex, VNI, Simple Telex) và *Bảng mã* (Unicode dựng sẵn, Tổ hợp, TCVN3).
   - Chọn mục bất kỳ $\rightarrow$ Dấu radio `(•)` di chuyển tương ứng và cấu hình được lưu lại vào hệ thống.
5. **Kiểm tra Live Typing Sandbox:**
   - Chuyển sang Tab *Gõ thử nghiệm* $\rightarrow$ Gõ trực tiếp câu `"Tiếng Việt mượt mà cùng BambooMintKey"` trong ô test để nghiệm thu.