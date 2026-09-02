<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Tổng Quan Kiến Trúc: C# NativeBridge & Windows TSF Integration
**Mã tài liệu:** `002_00_Overview_Architecture`  
**Giai đoạn:** Phase 2 - Tích hợp Hệ Điều Hành (Windows TSF & NativeAOT)  
**Trạng thái:** ✅ Hoàn thành (Closed)

---

## 1. Mục Tiêu Giai Đoạn 2 (Phase 2 Objectives)

Theo lộ trình phát triển [002_Roadmap.md](file:///D:/Kojin/BambooMintKey/docs/1.Investigation/002_Roadmap.md):
- **Đóng gói In-Process COM Server:** Biên dịch thư viện C# `BambooMintKey.NativeBridge` cùng lõi F# `BambooMintKey.Core` thành **Native C ABI DLL (`BambooMintKey.dll`)** thông qua công nghệ **.NET 10 NativeAOT**.
- **Tích hợp sâu vào Windows TSF (Text Services Framework):**
  - Đăng ký thành công TIP (Text Input Processor) với danh mục bàn phím Windows (`GUID_TFCAT_TIP_KEYBOARD`) và ngôn ngữ tiếng Việt (`0x042A`).
  - Xuất hiện trên thanh chuyển đổi ngôn ngữ Windows (`Language Bar` / `Win + Space`).
- **Gõ trực tiếp không cần giả lập Backspace (Direct Inline Replacement):**
  - Chặn phím mức hệ thống qua `ITfKeyEventSink`.
  - Gọi trực tiếp vào F# Pure Engine trong cùng không gian bộ nhớ tiến trình (In-Memory Fast Call ≈ 0ms).
  - Quản lý phiên gõ tạm `ITfComposition` và cập nhật văn bản nguyên tử qua `ITfRange::SetText`.

---

## 2. Kiến Trúc Phân Tầng In-Process (In-Process Layering)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Target Application Process (Word, Chrome, Discord, Notepad, Games...)  │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Windows TSF Subsystem (msctf.dll)                                │  │
│  └──────────────────────────────────┬───────────────────────────────┘  │
│                                     │ COM Interface Calls              │
│  ┌──────────────────────────────────▼───────────────────────────────┐  │
│  │ [C# NativeAOT] BambooMintKey.NativeBridge (BambooMintKey.dll)    │  │
│  │                                                                  │  │
│  │  1. COM Entry Points:                                            │  │
│  │     - DllGetClassObject, DllCanUnloadNow                         │  │
│  │     - DllRegisterServer, DllUnregisterServer                     │  │
│  │                                                                  │  │
│  │  2. TSF Interfaces Implementation:                               │  │
│  │     - ITfTextInputProcessorEx (Lifecycle & ActivateEx)           │  │
│  │     - ITfThreadMgrEventSink   (Focus Changed)                    │  │
│  │     - ITfKeyEventSink         (OnTestKeyDown, OnKeyDown, OnKeyUp)│  │
│  │     - ITfCompositionSink      (OnCompositionTerminated)          │  │
│  │     - ITfDisplayAttributeProvider (Inline Composition Underline) │  │
│  │                                                                  │  │
│  │  3. In-Memory Bridge:                                            │  │
│  │     - State Management (WordState mapping)                       │  │
│  │     - Direct Call -> BambooMintKey.Core.Engine.TelexEngine       │  │
│  └──────────────────────────────────┬───────────────────────────────┘  │
│                                     │ Fast In-Memory Function Call     │
│  ┌──────────────────────────────────▼───────────────────────────────┐  │
│  │ [F#] BambooMintKey.Core (Statically Linked via NativeAOT)        │  │
│  │  - Telex State Machine                                           │  │
│  │  - Pure Domain & Unicode Tables                                  │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Danh Mục Các Tài Liệu Thiết Kế Chi Tiết (Phase 2 Design Index)

| Mã Tài Liệu | Tên Tài Liệu | Nội Dung Trọng Tâm | Trạng Thái |
| :--- | :--- | :--- | :--- |
| **002_01** | `002_01_COM_Registration_and_Exports.md` | Xuất hàm C-ABI, tạo ClassFactory, đăng ký Registry và TSF Category/Language Profile | ✅ |
| **002_02** | `002_02_TSF_TextInputProcessor_Lifecycle.md` | Vòng đời Text Service (`ActivateEx`, `Deactivate`), quản lý ThreadMgr & Client ID | ✅ |
| **002_03** | `002_03_KeyEventSink_and_Core_Interop.md` | Đánh chặn phím (`ITfKeyEventSink`), nuốt phím (`pfEaten`), gọi vào F# `TelexEngine` | ✅ |
| **002_04** | `002_04_Composition_and_TextRange.md` | Khởi tạo/kết thúc `ITfComposition`, thao tác `ITfRange`, vẽ gạch chân `DisplayAttribute` | ✅ |
| **002_05** | `002_05_DevHarness_and_RegistrationScript.md` | Chương trình Console Dev Harness kiểm thử trước khi inject & Script PowerShell đăng ký TIP | ✅ |
| **002_06** | `002_06_Closure.md` | Tổng kết Phase 2: các vấn đề đã gặp, cách khắc phục, trạng thái cuối cùng | ✅ |

---

## 4. Tiêu Chuẩn Hoàn Thành Giai Đoạn 2 (Definition of Done - DoD)

1. **Build NativeAOT thành công:** `dotnet publish -c Release -r win-x64` sinh ra file `BambooMintKey.dll` độc lập.
2. **Đăng ký TSF thành công:** Chạy script đăng ký xuất hiện biểu tượng **BambooMintKey Vietnamese Input (VIE)** trên Taskbar / thanh chuyển đổi `Win + Space`.
3. **Gõ thực tế hoàn chỉnh:** Kích hoạt bộ gõ và gõ được đầy đủ tiếng Việt có dấu (`việt`, `tiếng`, `trường`, `hoàng`) trên **Notepad**, **Microsoft Word**, và **Google Chrome** mà không bị lỗi nuốt chữ, mất dấu hay xung đột con trỏ.

**Trạng thái DoD:** Đã đạt trong quá trình phát triển giai đoạn 2. Mọi lỗi đã được ghi nhận và khắc phục trong `002_06_Closure.md`.
