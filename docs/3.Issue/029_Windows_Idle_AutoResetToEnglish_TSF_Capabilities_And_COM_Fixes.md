<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 029: Bộ gõ vẫn tự động chuyển về ENG sau một thời gian không gõ (Idle / SecureMode / COMLESS / LPAC / COM Unload)

**Mã tài liệu:** `029_Windows_Idle_AutoResetToEnglish_TSF_Capabilities_And_COM_Fixes`  
**Trạng thái:** ✅ Đã khắc phục (Sẵn sàng build & xác minh thực tế trên Visual Studio)  
**Mức độ nghiêm trọng:** Cao (Ảnh hưởng trực tiếp trải nghiệm giữ trạng thái Input Method toàn cục trên Windows 10/11)  
**Ngày ghi nhận & khắc phục:** 26/11/2026  
**Nền tảng ảnh hưởng:** Windows 10 / Windows 11 64-bit (chế độ Global Input Method)  
**Mô-đun liên quan:**
- `src/BambooMintKey.NativeBridge/Common/Guids.cs` (bổ sung `GUID_TFCAT_TIPCAP_SECUREMODE` và `GUID_TFCAT_TIPCAP_COMLESS`)
- `src/BambooMintKey.NativeBridge/Interop/TsfRegistration.cs` (đăng ký thêm 2 category vào `SupportedCategories`)
- `src/BambooMintKey.NativeBridge/Exports.cs` (`DllCanUnloadNow()` luôn trả về `S_FALSE` cho NativeAOT DLL)
- `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs` (xử lý `TF_TMAE_SECUREMODE`, bỏ ghi thừa `BridgeStateManager.IsVietnameseMode` trong `ActivateExImpl`, loại bỏ background thread gọi COM STA `ITfThreadMgr` sai luồng)
- `src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs` (pass-through an toàn trong `IsSecureMode`, đồng bộ compartment trên STA UI thread)
- `src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs` (bổ sung SID `S-1-15-2-2` vào `UniversalSddl`)
- `src/BambooMintKey.UI/TsfBootstrapper.fs` (bổ sung ACL `*S-1-15-2-2` cho `C:\ProgramData\BambooMintKey` và kiểm tra 2 category mới trong `isAlreadyRegistered`)

---

## 1. Mô tả hiện tượng & Yêu cầu chuẩn

- **Hiện tượng:** Sau khi đã áp dụng bản vá Issue 028 (bổ sung `BambooMintKey32.dll` cho các ứng dụng 32-bit WOW64 như Zalo PC), tần suất tự rơi về bộ gõ `ENG` (`0409:00000409`) có giảm và khoảng thời gian kéo dài hơn, nhưng **sau một thời gian không gõ (idle)**, bộ gõ toàn hệ thống vẫn tự động bị chuyển về `ENG`.
- **Yêu cầu chuẩn:** Input Method **chỉ được chuyển khi người dùng chủ động bấm `Win + Space`** và trạng thái Input Method được **chia sẻ chung cho mọi ứng dụng**.

---

## 2. Quá trình điều tra & Bằng chứng thực nghiệm trên máy

### 2.1. Kiểm tra thiết lập Windows (đều đã đúng chuẩn)
- `SystemParametersInfo(SPI_GETTHREADLOCALINPUTSETTINGS)` = **`False`** → Windows đang dùng Global Input Method (dùng chung toàn hệ thống).
- `HKCU\Keyboard Layout\Toggle\Layout Hotkey` = **`3`** → Đã tắt phím tắt đổi ngôn ngữ `Alt+Shift` / `Ctrl+Shift`.
- `Get-WinUserLanguageList`: `en-US` (`0409:00000409`), `ja` (`0411:MS IME`), `vi` (`042A:{B8A5A29D-...}{C2F31A8E-...}`).
- Cả 2 nhánh COM `HKLM\SOFTWARE\Classes\CLSID\{B8A5A29D-...}` và `WOW6432Node` đều trỏ đúng `C:\ProgramData\BambooMintKey\BambooMintKey.dll` và `BambooMintKey32.dll`.

### 2.2. Đối chiếu TSF Categories trên Registry với bộ gõ hệ thống
Đối chiếu trực tiếp `HKLM\SOFTWARE\Microsoft\CTF\TIP`:
- **Microsoft Vietnamese Telex** (`{C2CB2CF0-AF47-413E-9780-8BC3A3C16068}`) và **Microsoft Japanese IME** (`{03B5835F-F03C-411B-9CE2-AA23E1171E36}`) đều đăng ký đầy đủ:
  - `{49D2F9CE-1F5E-11D7-A6D3-00065B84435C}` (`GUID_TFCAT_TIPCAP_SECUREMODE`)
  - `{364215D9-75BC-11D7-A6EF-00065B84435C}` (`GUID_TFCAT_TIPCAP_COMLESS`)
- **BambooMintKey** (`{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}`) **thiếu cả hai category trên**.
- Thiết lập nguồn/màn hình trên máy: `ScreenSaveActive = 1`, `VIDEOIDLE = 300s` (AC) / `180s` (DC).

### 2.3. Kiểm tra ACL thư mục DLL & Event Viewer
- `icacls C:\ProgramData\BambooMintKey`: chỉ có `Everyone:(OI)(CI)(RX)` và `ALL APPLICATION PACKAGES (S-1-15-2-1):(OI)(CI)(RX)`, **thiếu `ALL RESTRICTED APPLICATION PACKAGES (S-1-15-2-2)`**.
- **Windows Event Viewer (Application Log):**
  - Ghi nhận `Claude.exe` và `DB Browser for SQLite.exe` cùng crash với mã `0xc0000409` (`__fastfail`) tại module `unknown`, cùng offset `0x00007fff3a8fc28f` (dấu hiệu đặc trưng của NativeAOT DLL bị unload/lỗi).
  - Ghi nhận `Explorer.EXE` crash với mã `0xc0000374` (Heap Corruption).

---

## 3. Nguyên nhân gốc rễ (Root Causes)

Bản chất lỗi ở chế độ Global Input Method là: **mỗi khi Windows CTF cố kích hoạt TIP `042A` trên một thread/ngữ cảnh mà thất bại hoặc bị từ chối, Windows sẽ fallback toàn phiên về layout đầu tiên trong `HKCU\Keyboard Layout\Preload` (`1 = 00000409` — ENG)**. Issue 028 đã xử lý nhánh app 32-bit WOW64, nhưng vẫn còn **4 nguyên nhân kỹ thuật** sau:

1. **Thiếu `GUID_TFCAT_TIPCAP_SECUREMODE` và `GUID_TFCAT_TIPCAP_COMLESS`:**
   - Khi máy idle dẫn đến tắt màn hình / kích hoạt Lock Screen (`LockApp.exe`), UAC, hoặc focus vào ô nhập mật khẩu (`TF_TMAE_SECUREMODE`), TSF từ chối kích hoạt TIP thiếu `SECUREMODE` → fallback toàn cục về `ENG`.
   - Khi một thread hệ thống/shell/popup kích hoạt TSF với `TF_TMAE_COMLESS` (không gọi `CoInitialize`), TSF không thể khởi tạo TIP thiếu `COMLESS` → fallback toàn cục về `ENG`.
2. **`Exports.DllCanUnloadNow()` trả về `S_OK` trên NativeAOT DLL:**
   - COM trên Windows chạy `CoFreeUnusedLibraries` định kỳ khi **idle** (mặc định ~10 phút). Nếu `DllCanUnloadNow()` trả về `S_OK`, Windows gọi `FreeLibrary` lên `BambooMintKey.dll`.
   - `.NET NativeAOT` không hỗ trợ unload DLL qua `FreeLibrary`, dẫn tới crash hoặc hỏng quá trình nạp lại COM server.
3. **Thiếu quyền `ALL RESTRICTED APPLICATION PACKAGES` (`S-1-15-2-2` / LPAC):**
   - Các tiến trình LPAC của Windows 11 (WebView2, Widgets, bề mặt Shell/LockApp hạn chế) không có quyền đọc `C:\ProgramData\BambooMintKey\BambooMintKey.dll` và SharedMemory → `LoadLibrary` thất bại (`ERROR_ACCESS_DENIED`) → TSF fallback về `ENG`.
4. **Vi phạm COM STA threading trong `StartStateWatcher()`:**
   - Thread nền `BambooMintKey_StateWatcher` (MTA) gọi thẳng `UpdatePreservedKeys`, `SetOpenClose`, `SetConversionMode` lên `_pThreadMgr` (`ITfThreadMgr` là COM **STA** thuộc UI thread) mỗi khi có `SignalStateChanged()` (xảy ra liên tục mỗi lần `OnSetFocus` đổi `IsBambooMintKeyActive`). Việc gọi COM STA sai luồng có thể gây race condition / heap corruption cho `msctf.dll` và `Explorer.EXE` (khi Shell/CTF lỗi hoặc restart, input method tự trả về `ENG`).

---

## 4. Giải pháp kỹ thuật đã triển khai

1. **Đăng ký `GUID_TFCAT_TIPCAP_SECUREMODE` & `GUID_TFCAT_TIPCAP_COMLESS`:**
   - Khai báo 2 GUID chuẩn Windows SDK trong `src/BambooMintKey.NativeBridge/Common/Guids.cs` và thêm vào `SupportedCategories` trong `src/BambooMintKey.NativeBridge/Interop/TsfRegistration.cs`.
2. **Xử lý chuẩn `TF_TMAE_SECUREMODE` (`dwFlags & 0x2`):**
   - Trong `BambooMintKeyTextService.ActivateExImpl`, ghi nhận `_isSecureMode = (dwFlags & TsfFlags.TfTmaeSecuremode) != 0`.
   - Khi ở Secure Mode: không đăng ký preserved key, không gọi `TrayUiLauncher.EnsureRunning()`, không ghi disk/SharedMemory, và pass-through toàn bộ phím trong `KeyEventSinkImpl` — giúp TSF vẫn giữ nguyên profile BambooMintKey trên Lock Screen / ô mật khẩu mà không đẩy về `ENG`.
   - Đồng thời loại bỏ lời gọi thừa `BridgeStateManager.IsVietnameseMode = initialMode` trong `ActivateExImpl` (vốn gây ghi `config.json` xuống disk và broadcast event thừa mỗi lần kích hoạt).
3. **Chặn COM unload NativeAOT DLL khi idle:**
   - Sửa `Exports.DllCanUnloadNow()` luôn trả về `HResult.False` (`S_FALSE`) để `CoFreeUnusedLibraries` không bao giờ gọi `FreeLibrary` lên `BambooMintKey.dll` / `BambooMintKey32.dll`.
4. **Bổ sung quyền LPAC (`S-1-15-2-2`):**
   - Thêm `*S-1-15-2-2:(OI)(CI)RX` vào lệnh `icacls` trong `src/BambooMintKey.UI/TsfBootstrapper.fs` và thêm `(A;;GA;;;S-1-15-2-2)` vào `UniversalSddl` trong `src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs`.
   - Cập nhật `TsfBootstrapper.isAlreadyRegistered` kiểm tra sự tồn tại của 2 category `SECUREMODE` và `COMLESS` để bản cài mới tự động cập nhật ACL và đăng ký lại TSF qua `regsvr32`.
5. **Loại bỏ vi phạm COM STA trong `StartStateWatcher()`:**
   - Gỡ bỏ thread nền `BambooMintKey_StateWatcher` (không còn gọi `_pThreadMgr` từ MTA thread và không còn block `Thread.Join(600)` khi `Deactivate`).
   - Thay bằng `SyncCompartmentIfStaleOnStaThread()` chạy trực tiếp trên STA UI thread (kiểm tra nhanh `SharedMemoryManager.StateSequence` ở `OnTestKeyDown` và `OnSetFocus`), đảm bảo đồng bộ compartment an toàn tuyệt đối.

---

## 5. Lưu ý khi build & cài đặt thử nghiệm qua Visual Studio

- Khi cài bản mới và mở ứng dụng lần đầu, `TsfBootstrapper` sẽ phát hiện DLL mới + 2 category mới chưa có trong Registry, tự động cập nhật ACL (thêm `S-1-15-2-2`) và bật 1 lần UAC (`regsvr32`) để đăng ký `GUID_TFCAT_TIPCAP_SECUREMODE` và `GUID_TFCAT_TIPCAP_COMLESS`.
- Sau khi cài xong, nên **Sign out / Sign in** (hoặc restart máy) một lần để `ctfmon.exe`, `Explorer.EXE` và `LockApp.exe` nạp hoàn toàn bản DLL mới và đọc lại bộ TSF Category mới.
