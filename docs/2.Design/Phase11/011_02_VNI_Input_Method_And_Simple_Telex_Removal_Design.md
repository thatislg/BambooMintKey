<!--
  BambooMintKey - Vietnamese Telex & VNI Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# 011_02 — Thiết Kế Kỹ Thuật: Tích Hợp Kiểu Gõ VNI & Xóa Bỏ Hoàn Toàn Simple Telex Khỏi Hệ Thống

**Mã tài liệu:** `011_02_VNI_Input_Method_And_Simple_Telex_Removal_Design`

**Giai đoạn:** Phase 11 — Mở rộng kiểu gõ & Chuẩn hóa trải nghiệm người dùng

**Thuộc module:**
- `BambooMintKey.Core` (`Domain/Types.fs`, `Domain/EngineConfig.fs`, `Engine/*`)
- `BambooMintKey.Core.Native` (`Exports.cs`, `EngineContext.cs`)
- `BambooMintKey.NativeBridge` (`TSF/BridgeStateManager.cs`, `TSF/LangBarItemButton.cs`, `TSF/MenuCommands.cs`, `Common/SharedMemoryManager.cs`)
- `BambooMintKey.UI`, `BambooMintKey.UI.Mac`, `BambooMintKey.UI.Linux` (`MainWindow.axaml`, `MainWindow.axaml.fs`, `TrayIconManager.fs`, `SharedConfig.fs`)
- `BambooMintKey.Mac.IMK` & `BambooMintKey.Fcitx5`

**Trạng thái:** 📋 Đang nghiên cứu & Lên kế hoạch (Investigation & Technical Outline)

**Tài liệu tham chiếu:**
- Kiến trúc hệ thống: [SYSTEM_ARCHITECTURE.md](../../SYSTEM_ARCHITECTURE.md)
- Đặc tả cấu hình dùng chung: [003_06_SharedConfiguration_Schema.md](../Phase3/003_06_SharedConfiguration_Schema.md)
- Thiết kế C-ABI Native Core: [007_03_CoreNative_CABI_Design.md](../Phase7/007_03_CoreNative_CABI_Design.md)
- Cơ chế Bỏ dấu tự do: [005_00_Free_Tone_Placement_Design.md](../Phase5/005_00_Free_Tone_Placement_Design.md)

---

## 1. Bối Cảnh Và Mục Tiêu

### 1.1. Bối cảnh
1. **Sự ổn định của các phiên bản hiện tại:** Bộ gõ BambooMintKey hiện đã vận hành ổn định trên cả 3 nền tảng chính:
   - **Windows:** TSF COM In-process Provider + Avalonia Settings UI + Win32 Notification Area Tray.
   - **macOS:** InputMethodKit (IMK) + Menu Bar Status Item + Avalonia UI.
   - **Linux:** Fcitx5 C++ Addon + Core.Native C-ABI + Avalonia UI.
2. **Nhu cầu gõ VNI:** Mặc dù tác giả chủ yếu sử dụng Telex, VNI vẫn là kiểu gõ phổ biến thứ hai tại Việt Nam (đặc biệt trong môi trường văn phòng, kế toán, người dùng miền Nam hoặc người quen gõ bàn phím số). Việc bổ sung VNI giúp BambooMintKey tiếp cận đầy đủ đối tượng người dùng mà không làm giảm hiệu năng của engine.
3. **Vấn đề của "Simple Telex":**
   - Hiện tại trên UI (Windows, macOS, Linux) và Taskbar Menu đều có tùy chọn RadioButton/MenuItem "Simple Telex".
   - Tuy nhiên, trong engine F# Core (`TelexEngine.fs`), lựa chọn này chưa từng có thuật toán riêng, thực chất chỉ là một placeholder nhận giá trị `inputMethod = 2` nhưng engine vẫn chạy Telex chuẩn.
   - Tuỳ chọn này gây dư thừa, khó hiểu cho người dùng và không mang lại giá trị thực tế. Cần xóa bỏ triệt để.

### 1.2. Mục tiêu
1. **Xóa hoàn toàn Simple Telex:** Loại bỏ khỏi toàn bộ giao diện Avalonia (3 OS), Taskbar Context Menu (Windows), TSF Language Bar Menu (Windows) và tài liệu. Tự động chuyển đổi người dùng cũ từ Simple Telex về Telex tiêu chuẩn.
2. **Triển khai kiểu gõ VNI vào F# Core Engine:**
   - Hỗ trợ đầy đủ 5 dấu thanh (`1`–`5`), phím xóa dấu thanh (`0`).
   - Hỗ trợ các dấu phụ bằng số: `6` (mũ: â, ê, ô), `7` (móc: ơ, ư), `8` (trăng: ă), `9` (gạch đ).
   - Hỗ trợ bỏ dấu tự do kiểu VNI (`viet61` $\rightarrow$ `việt`, `duong971` $\rightarrow$ `dưỡng`).
   - Xử lý an toàn phím số trong nhập liệu thông thường (năm 2026, địa chỉ, số lượng...) mà không bị nuốt phím sai ngữ cảnh.
   - Không bị biến đổi bởi các phím Telex (`s, f, r, x, j, w` vẫn là chữ cái bình thường khi đang ở VNI).
3. **Đồng bộ xuyên suốt đa nền tảng:** Kết nối thông suốt từ UI $\rightarrow$ Shared Memory/Config JSON $\rightarrow$ Native Bridge (TSF / C-ABI) $\rightarrow$ F# Engine.
4. **Không làm suy giảm hiệu năng & Không ảnh hưởng Telex:** Giữ nguyên tính chất zero-allocation, thread-safe per-context, và độ trễ gõ phím cực thấp (< 0.05ms) của F# Core.

---

## 2. Kết Quả Điều Tra Chi Tiết (Investigation Findings)

### 2.1. Tầng Domain & Engine lõi (`BambooMintKey.Core`)
- **[EngineConfig.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.Core/Domain/EngineConfig.fs):** Chưa có trường `InputMethod`. Hiện chỉ có `IsEnabled`, `AllowRepeatKeyUndo`, `AllowLeadingWAsU`, `ToneStyle`, `AllowFreeTonePlacement`, `EnableVietnameseDictionary`, `EnableEnglishBacktracking`.
- **[Types.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.Core/Domain/Types.fs):** Chưa có kiểu enum định nghĩa `InputMethod`.
- **[ToneRules.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.Core/Engine/ToneRules.fs):** Hàm `keyToTone` hiện chỉ nhận các ký tự `'s', 'f', 'r', 'x', 'j'`. Chưa có ánh xạ số cho VNI (`1`–`5`, `0`).
- **[ModifierRules.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.Core/Engine/ModifierRules.fs):**
  - Hàm `resolveInlineModifiers` và `applyModifier` chỉ nhận diện các modifier Telex: `'a', 'w', 'e', 'o', 'd'`.
  - Chưa hỗ trợ modifier số (`6`–`9`).
- **[FreeTonePlacement.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.Core/Engine/FreeTonePlacement.fs):**
  - Hàm `isToneKeyChar` chỉ nhận diện `s, f, r, x, j`. Cần phân biệt theo kiểu gõ để nhận diện chữ số khi ở VNI.
- **[TelexEngine.fs](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.Core/Engine/TelexEngine.fs):**
  - Hàm `handleCharInput` và `processKey` xử lý trực tiếp các luật Telex cố định. Khi lặp phím hoàn tác, đang xét lặp các chữ cái Telex.

### 2.2. Tầng Cầu nối Native (`NativeBridge` & `Core.Native`)
- **Windows TSF (`BridgeStateManager.cs`):** Đã đọc các cờ cấu hình từ `SharedMemoryManager`, nhưng chưa đọc trường `SharedMemoryManager.InputMethod` để gán vào `EngineConfig`.
- **C-ABI (`Exports.cs` & `EngineContext.cs`):**
  - `LoadConfigJson`: Đã đọc hầu hết các trường từ JSON (`isVietnameseMode`, `toneStyle`, `allowRepeatKeyUndo`...) nhưng bỏ qua trường `"inputMethod"`.
  - `SetOptions` / `bmk_set_options`: Danh sách tham số chưa có `inputMethod`.

### 2.3. Tầng Giao diện & Cấu hình (UI / Tray / LangBar)
- **Windows UI (`BambooMintKey.UI`):**
  - `MainWindow.axaml`: Có 3 RadioButton `RbTelex`, `RbVni`, `RbSimpleTelex`.
  - `TrayIconManager.fs`: Menu chuột phải có 3 mục: `Telex`, `VNI`, `Simple Telex` (command ID: `menuCmdMethodSimple`).
- **macOS UI (`BambooMintKey.UI.Mac`):** Có `RbSimpleTelex`.
- **Linux UI (`BambooMintKey.UI.Linux`):** Có `RbSimpleTelex`.
- **TSF Language Bar (`LangBarItemButton.cs`):** Có menu item và hằng số lệnh `MethodSimpleTelex`.
- **Schema cấu hình (`SharedConfig.fs` & `SharedMemoryManager.cs`):**
  - Byte `[5]` trong Shared Memory đang ghi nhận: `0: Telex, 1: VNI, 2: Simple Telex`.
  - JSON schema có `"inputMethod": 0 | 1 | 2`.

---

## 3. Thiết Kế Chi Tiết

### 3.1. Hạng mục A: Xóa bỏ hoàn toàn "Simple Telex"

#### A.1. Loại bỏ các thành phần UI & Menu
1. **Avalonia XAML & Code-behind:**
   - Xóa thẻ `<RadioButton Name="RbSimpleTelex" Content="Simple Telex" GroupName="InputMethod"/>` trên cả 3 dự án:
     - `src/BambooMintKey.UI/MainWindow.axaml`
     - `src/BambooMintKey.UI.Mac/MainWindow.axaml`
     - `src/BambooMintKey.UI.Linux/MainWindow.axaml`
   - Trong code-behind `MainWindow.axaml.fs`:
     - Xóa khai báo biến `rbSimpleTelex`.
     - Xóa nhánh điều kiện `elif rbSimpleTelex.IsChecked = Nullable true then cfg.InputMethod <- 2uy`.
     - Chỉ còn 2 lựa chọn: `RbTelex` (giá trị `0`) và `RbVni` (giá trị `1`).
2. **Context Menu khay hệ thống (Windows Tray):**
   - File `TrayIconManager.fs`:
     - Xóa biến `mSimple`, xóa dòng `TrayWin32.AppendMenuW(hSubMethod, ..., "Simple Telex")`.
     - Xóa nhánh `menuCmdMethodSimple` trong `HandleMenuCommand`.
3. **TSF Language Bar (Windows):**
   - File `MenuCommands.cs`: Xóa `MethodSimpleTelex = Base + 33;`.
   - File `LangBarItemButton.cs`:
     - Xóa mục `"Simple Telex"` trong phương thức `ShowContextMenu` (ITfMenu) và Win32 `TrackPopupMenuEx`.
     - Xóa nhánh `case MenuCommands.MethodSimpleTelex:`.

#### A.2. Chiến lược Tương thích ngược & Migration (Backward Compatibility)
- **Quy tắc chuẩn hóa `InputMethod`:**
  - `0`: **Telex** (mặc định)
  - `1`: **VNI**
- **Cơ chế nạp cấu hình (`SharedConfig.fs` & `SharedMemoryManager.cs`):**
  - Khi nạp từ `config.json` hoặc Shared Memory, nếu đọc được giá trị `2` (từ cấu hình của bản phát hành trước), hệ thống tự động gán về `0` (Telex):
    ```fsharp
    // SharedConfig.fs
    if has "inputMethod" "1" then cfg.InputMethod <- 1uy
    else cfg.InputMethod <- 0uy // Nếu là 2 hoặc không xác định -> quy về Telex (0)
    ```
  - Trong `SharedMemoryManager.cs`:
    ```csharp
    var method = ParseUint("inputMethod", 0);
    pShared[5] = (byte)(method == 1 ? 1 : 0); // Loại bỏ hoàn toàn giá trị 2
    ```

---

### 3.2. Hạng mục B: Triển khai Kiểu gõ VNI vào Core Engine

#### B.1. Đặc tả bảng gõ VNI chuẩn (VNI Specification)

1. **Quy tắc Dấu thanh (Tone Keys):**
   | Phím số | Dấu thanh (`Tone`) | Ví dụ sau nguyên âm | Ví dụ bỏ dấu cuối từ |
   |:---:|:---|:---|:---|
   | `1` | Sắc (`Acute`) | `a1` $\rightarrow$ `á` | `toan1` $\rightarrow$ `toán` |
   | `2` | Huyền (`Grave`) | `a2` $\rightarrow$ `à` | `meo2` $\rightarrow$ `mèo` |
   | `3` | Hỏi (`Hook`) | `a3` $\rightarrow$ `ả` | `kho3` $\rightarrow$ `khỏ` |
   | `4` | Ngã (`Tilde`) | `a4` $\rightarrow$ `ã` | `sua4` $\rightarrow$ `sữa` |
   | `5` | Nặng (`Dot`) | `a5` $\rightarrow$ `ạ` | `nang5` $\rightarrow$ `nặng` |
   | `0` | Xóa dấu thanh (`None`) | `á0` $\rightarrow$ `a` | `toán0` $\rightarrow$ `toan` |

2. **Quy tắc Dấu phụ (Modifier Keys):**
   | Phím số | Dấu phụ (`Modifier`) | Nguyên âm / Phụ âm áp dụng | Ví dụ |
   |:---:|:---|:---|:---|
   | `6` | Mũ (`Hat`) | `a` $\rightarrow$ `â`, `e` $\rightarrow$ `ê`, `o` $\rightarrow$ `ô` | `a6` $\rightarrow$ `â`, `thao6` $\rightarrow$ `thôa` (hoặc `tho6` $\rightarrow$ `thô`) |
   | `7` | Móc (`Horn`) | `o` $\rightarrow$ `ơ`, `u` $\rightarrow$ `ư`, `uo` $\rightarrow$ `ươ` | `u7` $\rightarrow$ `ư`, `o7` $\rightarrow$ `ơ`, `uo7` $\rightarrow$ `ươ`, `uowng7` |
   | `8` | Trăng (`Breve`) | `a` $\rightarrow$ `ă` | `a8` $\rightarrow$ `ă`, `an8` $\rightarrow$ `ăn` |
   | `9` | Gạch ngang (`DBar`) | `d` $\rightarrow$ `đ` | `d9` $\rightarrow$ `đ`, `da9` $\rightarrow$ `đa`, `d9a` $\rightarrow$ `đa` |

3. **Nguyên tắc cô lập phím chữ cái (Letter Isolation):**
   - Khi ở chế độ VNI, **tất cả các phím chữ cái `s, f, r, x, j, w, a, e, o, d` giữ nguyên giá trị chữ cái**, hoàn toàn không tạo dấu.
   - `as` $\rightarrow$ `as` (không thành `á`).
   - `dd` $\rightarrow$ `dd` (không thành `đ`).
   - `aw` $\rightarrow$ `aw` (không thành `ă`).

4. **Nguyên tắc gõ số thông thường (Numeric Typing Safety):**
   - Trong thực tế, người dùng gõ số rất thường xuyên: `nam 2026`, `tang 3`, `lop 12`, `iPhone 16`, `H2O`...
   - **Quy tắc kích hoạt dấu:** Phím số `0`–`9` chỉ kích hoạt biến đổi tiếng Việt khi:
     - Đứng ngay sau nguyên âm hoặc phụ âm có thể áp dụng (vd `a1`, `d9`), HOẶC:
     - Nằm trong ngữ cảnh từ hợp lệ đang gõ dở dang và có thể đặt dấu thanh/dấu phụ theo quy chuẩn ngữ âm học.
   - **Trường hợp từ chối biến đổi (Commit số):**
     - Đứng đầu từ (buffer rỗng): `1` $\rightarrow$ hiển thị số `1`, pass-through.
     - Sau phụ âm không nhận dấu (vd `t1`, `m2`... mà không có nguyên âm): pass-through số.
     - Đã nhận dấu rồi mà gõ số không liên quan: pass-through số.
     - Lặp phím số (Repeat Key Undo): `a1` $\rightarrow$ `á`, gõ tiếp `1` $\rightarrow$ `a1` (trả lại chữ `a` và số `1`).

#### B.2. Thiết kế kiến trúc mã nguồn trong `BambooMintKey.Core`

1. **Cập nhật Domain Types (`Types.fs` & `EngineConfig.fs`):**
   ```fsharp
   // Types.fs
   [<RequireQualifiedAccess>]
   type InputMethod =
       | Telex = 0
       | Vni = 1

   // EngineConfig.fs
   type EngineConfig = {
       IsEnabled: bool
       InputMethod: InputMethod // Mới thêm: Kiểu gõ hiện tại
       AllowRepeatKeyUndo: bool
       AllowLeadingWAsU: bool
       ToneStyle: TonePlacementStyle
       AllowFreeTonePlacement: bool
       EnableVietnameseDictionary: bool
       EnableEnglishBacktracking: bool
   }
   with
       static member Default = {
           IsEnabled = true
           InputMethod = InputMethod.Telex
           AllowRepeatKeyUndo = true
           AllowLeadingWAsU = false
           ToneStyle = TonePlacementStyle.Modern
           AllowFreeTonePlacement = true
           EnableVietnameseDictionary = true
           EnableEnglishBacktracking = true
       }
   ```

2. **Mở rộng Module Dấu thanh (`ToneRules.fs`):**
   ```fsharp
   module ToneRules =
       /// Ánh xạ phím sang dấu thanh dựa trên kiểu gõ
       let keyToToneWithMethod (method: InputMethod) (c: char) : Tone option =
           match method with
           | InputMethod.Telex ->
               match Char.ToLowerInvariant c with
               | 's' -> Some Tone.Acute
               | 'f' -> Some Tone.Grave
               | 'r' -> Some Tone.Hook
               | 'x' -> Some Tone.Tilde
               | 'j' -> Some Tone.Dot
               | _ -> None
           | InputMethod.Vni ->
               match c with
               | '1' -> Some Tone.Acute
               | '2' -> Some Tone.Grave
               | '3' -> Some Tone.Hook
               | '4' -> Some Tone.Tilde
               | '5' -> Some Tone.Dot
               | '0' -> Some Tone.None // Phím 0: xóa dấu thanh về thanh ngang
               | _ -> None
   ```

3. **Mở rộng Module Dấu phụ (`ModifierRules.fs`):**
   ```fsharp
   module ModifierRules =
       /// Kiểm tra ký tự có phải là phím modifier hợp lệ
       let isModifierKey (method: InputMethod) (c: char) : bool =
           match method with
           | InputMethod.Telex -> "aweod".Contains(Char.ToLowerInvariant c)
           | InputMethod.Vni -> "6789".Contains c

       /// Áp dụng biến đổi modifier cho âm tiết
       let applyModifierWithMethod (method: InputMethod) (c: char) (syllable: Syllable) : Syllable option =
           match method with
           | InputMethod.Telex ->
               // Logic Telex hiện tại ('w', 'a', 'e', 'o', 'd')
               applyModifier c syllable
           | InputMethod.Vni ->
               match c with
               | '9' when syllable.InitialConsonant.ToLowerInvariant() = "d" ->
                   let newInit = if Char.IsUpper syllable.InitialConsonant[0] then "Đ" else "đ"
                   Some { syllable with InitialConsonant = newInit }
               | '6' -> // Biến đổi mũ: a -> â, e -> ê, o -> ô
                   transformVniHat syllable
               | '7' -> // Biến đổi móc: u -> ư, o -> ơ, uo -> ươ
                   transformVniHorn syllable
               | '8' -> // Biến đổi trăng: a -> ă
                   transformVniBreve syllable
               | _ -> None
   ```

4. **Mở rộng Module Bỏ dấu tự do (`FreeTonePlacement.fs`):**
   - Khi ở VNI, danh sách phím dấu thanh là `'1', '2', '3', '4', '5'`.
   - Phím dấu phụ là `'6', '7', '8', '9'`.
   - Bộ trích xuất `extractTokens` cần nhận tham số `InputMethod` để:
     - Nếu Telex: nhận diện `s, f, r, x, j`.
     - Nếu VNI: nhận diện các chữ số `0`–`9` sau nguyên âm.

---

### 3.3. Hạng mục C: Tích hợp Pipeline Đa nền tảng (Integration)

```mermaid
flowchart TD
    subgraph UI_Layer [Giao diện & Cấu hình]
        UI_Win[BambooMintKey.UI - Windows]
        UI_Mac[BambooMintKey.UI.Mac - macOS]
        UI_Linux[BambooMintKey.UI.Linux - Linux]
        ConfigJSON[config.json\ninputMethod: 0 | 1]
        SharedMem[SharedMemoryManager\npShared 5 = 0 | 1]
    end

    subgraph Native_Bridge [Native Bridges]
        TSF_Bridge[Windows TSF BridgeStateManager.cs]
        CABI_CoreNative[Core.Native Exports.cs / EngineContext.cs]
        Fcitx5_Bridge[Linux Fcitx5 engine.cpp]
        IMK_Bridge[macOS IMK BambooMintKeyController.swift]
    end

    subgraph Core_Engine [F# Pure Core Engine]
        EngineConfig[EngineConfig.InputMethod]
        ToneRules[ToneRules.keyToToneWithMethod]
        ModRules[ModifierRules.applyModifierWithMethod]
        TelexEngine[TelexEngine / VniEngine Engine Dispatcher]
    end

    UI_Win -->|Lưu| SharedMem
    UI_Win -->|Lưu| ConfigJSON
    UI_Mac -->|Lưu| ConfigJSON
    UI_Linux -->|Lưu| ConfigJSON

    SharedMem -->|Đọc byte 5| TSF_Bridge
    ConfigJSON -->|bmk_load_config_json| CABI_CoreNative

    TSF_Bridge -->|Config.InputMethod| TelexEngine
    CABI_CoreNative -->|EngineContext.Config| TelexEngine
    Fcitx5_Bridge -->|C-ABI| CABI_CoreNative
    IMK_Bridge -->|C-ABI| CABI_CoreNative

    TelexEngine --> EngineConfig
    EngineConfig --> ToneRules
    EngineConfig --> ModRules
```

1. **Windows TSF (`BridgeStateManager.cs`):**
   - Đọc `SharedMemoryManager.InputMethod`:
     ```csharp
     byte methodByte = SharedMemoryManager.InputMethod;
     var inputMethod = (methodByte == 1) ? Types.InputMethod.Vni : Types.InputMethod.Telex;
     ```
   - Truyền `inputMethod` vào cấu trúc `EngineConfig.EngineConfig`.
2. **C-ABI (`Exports.cs` & `EngineContext.cs`):**
   - Trong `LoadConfigJson`: Đọc `int inputMethod = JsonGetInt(json, "inputMethod", 0);` và cập nhật context.
   - Trong `bmk_set_options`: Bổ sung tham số `int inputMethod` để Fcitx5 và macOS IMK truyền trực tiếp khi có sự kiện đổi cấu hình on-the-fly.
3. **Fcitx5 C++ (`engine.cpp`) & macOS IMK (`BambooMintKeyController.swift`):**
   - Khi quan sát thấy file `config.json` thay đổi qua `inotify` hoặc `FileWatcher`, tự động gọi `bmk_load_config_json` hoặc `bmk_set_options` để cập nhật context tức thì mà không cần khởi động lại tiến trình.

---

## 4. Kế Hoạch Kiểm Thử Toàn Diện (Test Matrix & Verification Plan)

### 4.1. Unit Test Matrix cho Kiểu gõ VNI (`VniEngineTests.fs`)

| Nhóm test | Dữ liệu gõ vào | Kết quả mong đợi | Mục tiêu kiểm tra |
| :--- | :--- | :--- | :--- |
| **1. Dấu thanh cơ bản** | `a1`, `a2`, `a3`, `a4`, `a5` | `á`, `à`, `ả`, `ã`, `ạ` | 5 dấu thanh với nguyên âm đơn |
| **2. Xóa dấu thanh bằng phím 0** | `a10`, `toan10` | `a`, `toan` | Phím `0` trả về thanh ngang |
| **3. Dấu mũ số 6** | `a6`, `e6`, `o6` | `â`, `ê`, `ô` | Mũ cho a, e, o |
| **4. Dấu móc số 7** | `u7`, `o7`, `uo7` | `ư`, `ơ`, `ươ` | Móc cho u, o và cụm uo |
| **5. Dấu trăng số 8** | `a8`, `an8` | `ă`, `ăn` | Trăng cho a |
| **6. Gạch ngang số 9** | `d9`, `d9a`, `da9` | `đ`, `đa`, `đa` | Biến đổi d thành đ |
| **7. Bỏ dấu tự do cuối từ** | `toan1`, `viet65`, `duong971` | `toán`, `việt`, `dưỡng` | Free tone placement & Free modifier |
| **8. Hoàn tác lặp phím (Undo)** | `a11`, `a66`, `d99` | `a1`, `a6`, `d9` | Khôi phục số thô khi lặp phím |
| **9. Nhập số thông thường** | `2026`, `hang1`, `lop12` | `2026`, `hang1`, `lop12` | Không nuốt số ở ngữ cảnh số thuần túy |
| **10. Cô lập phím chữ Telex** | `as`, `aw`, `dd`, `oo` | `as`, `aw`, `dd`, `oo` | Tuyệt đối không biến đổi theo luật Telex khi ở VNI |

### 4.2. Regression Test Matrix cho Kiểu gõ Telex
- Chạy toàn bộ test suites hiện hữu:
  - `RuleTests.fs`
  - `FreeTonePlacementTests.fs`
  - `EnglishProtectionTests.fs`
  - `UppercaseModifierTests.fs`
  - `BasicTelexTests.fs` (đổi tên từ `SimpleTelexTests.fs`)
- **Yêu cầu nghiệm thu:** 100% các ca test Telex hiện tại phải PASS mà không có bất kỳ sai lệch nào.

---

## 5. Lộ Trình Triển Khai Đề Xuất (Execution Roadmap)

```
[Giai đoạn 1: Dọn dẹp] ───> [Giai đoạn 2: VNI Core] ───> [Giai đoạn 3: Integration] ───> [Giai đoạn 4: QA & Release]
- Xóa Simple Telex UI      - Types.fs & EngineConfig     - Windows TSF Bridge            - Chạy trọn bộ Unit Tests
- Xóa Menu Tray/LangBar    - VNI Tone & Modifier Rules   - C-ABI Core.Native             - Test tay trên Win, Mac, Linux
- Migration config.json    - Unit Test VniEngineTests     - Fcitx5 & macOS IMK sync       - Cập nhật tài liệu người dùng
```

- **Bước 1 — Dọn dẹp Simple Telex & Migration (Nhanh, an toàn):**
  - Xóa RadioButton "Simple Telex" trên 3 file XAML của UI (Windows, Mac, Linux).
  - Dọn dẹp code-behind và menu chuột phải Taskbar / Language Bar.
  - Cập nhật hàm nạp config để migrate giá trị `2` thành `0`.
  - Đổi tên file test `SimpleTelexTests.fs` thành `BasicTelexTests.fs`.
- **Bước 2 — Mở rộng Core Engine cho VNI & Unit Test:**
  - Cập nhật `Types.fs` và `EngineConfig.fs`.
  - Viết module xử lý luật VNI và tích hợp vào engine dispatcher.
  - Viết bộ test `VniEngineTests.fs` và chạy xác minh độ phủ.
- **Bước 3 — Kết nối Pipeline Đa nền tảng:**
  - Đồng bộ `InputMethod` trong Windows TSF `BridgeStateManager.cs`.
  - Cập nhật `Exports.cs` và `EngineContext.cs` trong `BambooMintKey.Core.Native`.
  - Xác nhận Fcitx5 và macOS IMK đồng bộ cấu hình thông suốt.
- **Bước 4 — Nghiệm thu & Kiểm thử hồi quy:**
  - Chạy toàn bộ test suite. Kiểm thử gõ thực tế trên Notepad, trình duyệt, Word và Terminal.
