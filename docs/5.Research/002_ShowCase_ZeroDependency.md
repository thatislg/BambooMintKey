<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Technical Showcase: Building a Zero-Dependency, Cross-Platform IME with .NET 10 NativeAOT and C-ABI Interop

> **Status:** Outline (draft). Sections are expanded with concrete writing points, technical facts, and placeholders for measurements to be collected.
> **Companion documents:** `docs/SYSTEM_ARCHITECTURE.md`, `docs/5.Research/001_Overview.md`.

---

## Table of Contents

- [Abstract](#abstract)
- [1. Introduction & Background](#1-introduction--background)
  - [1.1. The Cross-Platform Input Method Problem](#11-the-cross-platform-input-method-problem)
  - [1.2. Vietnamese Orthography as a Hard Test Case](#12-vietnamese-orthography-as-a-hard-test-case)
  - [1.3. The Project: BambooMintKey](#13-the-project-bamboomintkey)
  - [1.4. Purpose & Contributions of This Showcase](#14-purpose--contributions-of-this-showcase)
- [2. Related Work](#2-related-work)
- [3. System Architecture: The Decoupled Adapter Model](#3-system-architecture-the-decoupled-adapter-model)
  - [3.1. Layer Separation](#31-layer-separation)
  - [3.2. Source Tree Mapping](#32-source-tree-mapping)
  - [3.3. Architecture Diagram](#33-architecture-diagram)
  - [3.4. Runtime Boundary & the Honest Scope of "Zero-Dependency"](#34-runtime-boundary--the-honest-scope-of-zero-dependency)
- [4. Engineering the C-ABI Surface with .NET 10 NativeAOT](#4-engineering-the-c-abi-surface-with-net-10-nativeaot)
  - [4.1. Project & SDK Configuration](#41-project--sdk-configuration)
  - [4.2. The Unmanaged Surface (exact signatures)](#42-the-unmanaged-surface-exact-signatures)
  - [4.3. Action Code Contract](#43-action-code-contract)
  - [4.4. Exporting Entrypoints](#44-exporting-entrypoints)
- [5. Memory Management & Zero-Allocation Buffers](#5-memory-management--zero-allocation-buffers)
  - [5.1. Ownership Contract](#51-ownership-contract)
  - [5.2. Fixed-Size Native Buffers](#52-fixed-size-native-buffers)
  - [5.3. Why This Design (the "so what")](#53-why-this-design-the-so-what)
  - [5.4. Thread Safety](#54-thread-safety)
- [6. Consuming the Library in the Fcitx5 C++ Addon](#6-consuming-the-library-in-the-fcitx5-c-addon)
  - [6.1. Build & Link Strategy](#61-build--link-strategy)
  - [6.2. Addon Factory & Lifecycle](#62-addon-factory--lifecycle)
  - [6.3. The Composition Loop](#63-the-composition-loop)
  - [6.4. Preedit vs. Direct Commit](#64-preedit-vs-direct-commit)
  - [6.5. D-Bus & Config Hot-Reload](#65-d-bus--config-hot-reload)
- [7. Cross-Platform Portability: Windows TSF & macOS IMK](#7-cross-platform-portability-windows-tsf--macos-imk)
  - [7.1. The Windows TSF Bridge (already implemented)](#71-the-windows-tsf-bridge-already-implemented)
  - [7.2. Reusing the Same C-ABI for macOS (future)](#72-reusing-the-same-c-abi-for-macos-future)
- [8. Deployment, Packaging & System Integration](#8-deployment-packaging--system-integration)
  - [8.1. Linux System Layout](#81-linux-system-layout)
  - [8.2. Flatpak / SteamOS](#82-flatpak--steamos)
  - [8.3. Windows Packaging](#83-windows-packaging)
- [9. Evaluation & Measurements](#9-evaluation--measurements)
  - [9.1. Binary Footprint](#91-binary-footprint)
  - [9.2. Startup & Latency](#92-startup--latency)
  - [9.3. Correctness](#93-correctness)
  - [9.4. Memory Safety & Leak Checks](#94-memory-safety--leak-checks)
- [10. Limitations & Future Work](#10-limitations--future-work)
- [11. Conclusion](#11-conclusion)
- [12. References](#12-references)

---

## Abstract

> **TODO (VN):** Tóm tắt toàn bài: vấn đề cần giải, cách tiếp cận (lõi F# thuần hàm + cầu nối NativeAOT C-ABI), các luận điểm chính sẽ chứng minh, và kết quả chính. Phần này viết cuối cùng, sau khi các chương đã hoàn tất.

* Summarize the problem: shipping a high-performance Vietnamese IME across heterogeneous desktop input subsystems without requiring a managed runtime on the target machine.
* Summarize the approach: a pure-functional F# linguistic core exposed through a lean C-ABI produced by **.NET 10 NativeAOT**, consumed by thin native adapters (Windows TSF COM, Linux Fcitx5 C++ addon).
* State the key claims to be evidenced in the body:
  * Zero dependency on the .NET runtime / CLR for the **in-daemon input engine path** (the out-of-process settings GUI is currently framework-dependent — see the Runtime Boundary section).
  * Zero heap allocation on the hot key-processing path (fixed 256-byte buffers).
  * Deterministic, side-effect-free string transformation from the F# core.
  * A single ABI contract portable from Linux (`BambooMintKeyCore.so`) toward macOS (`libBambooMintKey.dylib`).
* Scope note (honesty): "zero-dependency" is claimed for the *input engine*, not the whole product.

---

## 1. Introduction & Background

> **TODO (VN):** Đặt vấn đề và dẫn nhập. Giải thích sự phân mảnh của các hệ thống nhập liệu (Windows TSF, Linux Fcitx5, macOS IMK) và vì sao nhúng managed code (CLR/JIT) vào daemon hay process host là bất khả thi thực tế (latency khởi động, GC pause, phình binary, bán kính crash). Giới thiệu dự án, tech stack, mục đích và đóng góp của bài báo — làm nền để người đọc hiểu vì sao cần NativeAOT + C-ABI.

### 1.1. The Cross-Platform Input Method Problem
* Fragmentation of desktop input subsystems:
  * **Windows** — Text Services Framework (TSF), COM-based, in-process TIP.
  * **Linux** — Input Method frameworks (Fcitx5), daemon-based, C++ addon model.
  * **macOS** — InputMethodKit (IMK), Objective-C/Swift controller.
* Why embedding managed code (CLR/JIT) inside host input daemons or host client processes is historically impractical:
  * Runtime bootstrap latency.
  * GC pause jitter inside the input event loop.
  * Binary bloat and per-process footprint.
  * Crash blast radius (a runtime failure takes down the host process).

### 1.2. Vietnamese Orthography as a Hard Test Case
* Tonal + diacritic composition requirements (e.g., `aa` → `â`, `as` → `á`, `dd` → `đ`).
* Tone placement rules — the modern vs. traditional distinction (`hòa` vs. `hoà`).
* English-word backtracking / auto-restore behavior.
* Why correctness must be **deterministic and total** — no partial/invalid syllable states.

### 1.3. The Project: BambooMintKey
* Goal: a high-performance, deterministic Vietnamese IME on a unified functional core.
* Tech stack:
  * **F#** — pure functional syllable & grammar core (`BambooMintKey.Core`).
  * **C# .NET 10 NativeAOT** — interop bridges (`BambooMintKey.Core.Native` for Linux, `BambooMintKey.NativeBridge` for Windows TSF).
  * **C++17** — thin Fcitx5 adapter (`BambooMintKey.Fcitx5`).
  * **Avalonia 12** — out-of-process settings UI (`BambooMintKey.UI`, `BambooMintKey.UI.Linux`).
* Repository: `thatislg/BambooMintKey` (MIT, SPDX-tagged).

### 1.4. Purpose & Contributions of This Showcase
* Practical patterns for exposing a C-ABI shared library from .NET 10 NativeAOT consumed by native daemons with no pre-installed .NET.
* A concrete, reusable memory-ownership contract for cross-boundary UTF-8 strings.
* Evidence for the maturity of NativeAOT in low-level OS extension integration.

---

## 2. Related Work

> **TODO (VN):** So sánh với các bộ gõ tiếng Việt đã có (Unikey, ibus-bamboo, fcitx5-bamboo) về kiến trúc và cách triển khai. Làm nổi bật điểm khác biệt của BambooMintKey: lõi F# thuần hàm dùng chung đa nền tảng, cầu nối AOT, buffer zero-allocation. Định vị đóng góp của bài là kỹ thuật interop managed→native, không phải một thuật toán gõ mới.

* Existing Vietnamese IMEs and their architecture:
  * **Unikey** (Windows) — native C++, keyboard-hook / TSF approaches.
  * **ibus-bamboo** / **fcitx5-bamboo** — native C/C++ Fcitx5/IBus engines, JavaScript-based core in some variants.
* Contrast with BambooMintKey:
  * Unified F# functional core shared across all platforms.
  * AOT-compiled bridge (no interpreter, no runtime).
  * Zero-allocation composition buffers vs. dynamic string churn.
* Position the novelty: not a new input algorithm, but a **delivery & interop engineering** contribution for managed → native boundary design.

---

## 3. System Architecture: The Decoupled Adapter Model

> **TODO (VN):** Mô tả kiến trúc phân tầng: F# core → NativeAOT bridge → OS adapter → Settings UI, kèm sơ đồ tổng thể và cây thư mục nguồn thực tế. Đặc biệt nhấn mạnh mục Runtime Boundary để trung thực về phạm vi "zero-dependency" (engine thì không cần runtime, còn settings GUI thì vẫn framework-dependent — và lập luận mọi UI framework đều cần một runtime).

### 3.1. Layer Separation
* **Layer 1 — Linguistic Engine** (`BambooMintKey.Core`, F#):
  * Pure state transformations, no OS API dependencies, no side effects.
  * Modules: 
    * `Types`: 
    * `EngineConfig`: 
    * `UnicodeTables`: 
    * `HotkeyFormatter`: 
    * `IDictionaryService`: 
    * `FrozenDictionaryService`: 
    * `ModifierRules`: 
    * `SyllableParser`: 
    * `ToneRules`: 
    * `WordBuffer`: 
    * `EnglishProtection`: 
    * `FreeTonePlacement`: 
    * `TelexEngine`:
    * `NativeApi`:
* **Layer 2 — Native Bridge** (C# NativeAOT):
  * Linux: `BambooMintKey.Core.Native` → `BambooMintKeyCore.so`.
  * Windows: `BambooMintKey.NativeBridge` → `BambooMintKey.dll` (in-process COM TIP).
* **Layer 3 — OS Adapter**:
  * Linux: `BambooMintKey.Fcitx5` (C++17) → `libbamboomintkey.so`, loaded by the Fcitx5 daemon.
  * Windows: TSF registration via `DllGetClassObject` / `DllRegisterServer`.
  * macOS (future): `IMKInputController` in Swift/Objective-C.
* **Layer 4 — Settings UI** (out-of-process):
  * `BambooMintKey.UI` (Windows), `BambooMintKey.UI.Linux` (Linux), Avalonia 12.

### 3.2. Source Tree Mapping
```
src/
├── BambooMintKey.Core/          # F# functional engine (shared 100%)
├── BambooMintKey.Core.Native/   # C# NativeAOT → BambooMintKeyCore.so (Linux C-ABI)
├── BambooMintKey.NativeBridge/  # C# NativeAOT → BambooMintKey.dll (Windows TSF COM)
├── BambooMintKey.Fcitx5/        # C++17 Fcitx5 addon → libbamboomintkey.so
├── BambooMintKey.Shared/        # shared F# config/IPC types
├── BambooMintKey.UI/            # Avalonia settings (Windows)
├── BambooMintKey.UI.Linux/      # Avalonia settings (Linux)
└── BambooMintKey.DevHarness/    # dev/test harness
```

### 3.3. Architecture Diagram
* **Figure 3.1** — high-level flowchart (reuse the overview diagram in `SYSTEM_ARCHITECTURE.md` §2):
  * Key event → OS adapter → C-ABI entrypoint → F# engine → preedit/commit return.

### 3.4. Runtime Boundary & the Honest Scope of "Zero-Dependency"

> **Why this section exists:** the project is advertised as "zero-dependency", but the
> settings GUI (Avalonia) is currently published **framework-dependent** and therefore
> requires the .NET 10 runtime on the target machine. This section scopes the claim
> precisely so the report is not overstating, and frames the trade-off correctly.

#### 3.4.1. The Two Runtime Zones
| Component | Build | Requires .NET runtime? | Runs inside the input daemon? |
|---|---|---|---|
| `BambooMintKeyCore.so` (engine bridge) | NativeAOT | No | Yes |
| `libbamboomintkey.so` (Fcitx5 addon) | C++17 | No | Yes |
| `BambooMintKey.dll` (Windows TSF) | NativeAOT | No | Yes |
| `BambooMintKey.UI` / `UI.Linux` (settings) | Avalonia, framework-dependent | **Yes** | No (out-of-process) |

#### 3.4.2. What "Zero-Dependency" Actually Means Here
* The claim applies **only to the input engine path** — the components loaded into the host input daemon (Fcitx5) or the TSF host process.
* This is the only place where a runtime dependency would be *unacceptable*: embedding the CLR inside a daemon introduces bootstrap latency, GC pauses in the input loop, and a larger crash blast radius.
* The settings GUI is a **separate process**, decoupled from the daemon. Its runtime dependency does not affect input latency, stability, or memory profile.

#### 3.4.3. Every UI Framework Needs *A* Runtime
* This is the key honest framing: **Qt and GTK are also runtimes** — they are native runtimes, whereas .NET is a managed runtime. A truly runtime-free desktop UI does not exist; choosing Avalonia is not an architectural regression.
* The meaningful question is therefore **not** "runtime or no runtime", but **"where does the runtime live"** — it must never live in the input daemon.
* A user demanding a literal 100% zero-runtime product can have it: a native (Qt/GTK or plain C++) settings UI would still bundle *a* runtime, just a native one. The current design deliberately keeps the managed runtime isolated to the UI process.

#### 3.4.4. Evidence in the Repository
* `scripts/linux/install_linux.sh` (lines ~92–93): the settings UI "needs the .NET 10 runtime", with `sudo apt install dotnet-runtime-10.0` as guidance.
* `scripts/linux/install-ui-linux.sh` (lines ~9, ~41): publishes Avalonia **framework-dependent** (no `PublishAot`, no `--self-contained`).
* The engine (`BambooMintKey.Core.Native.csproj`) uses `PublishAot=true`, `NativeLib=Shared`,`StripSymbols=true`, `InvariantGlobalization=true` — confirming the AOT zone is genuinely runtime-free.

---

## 4. Engineering the C-ABI Surface with .NET 10 NativeAOT

> **TODO (VN):** Trình bày chi tiết kỹ thuật bề mặt C-ABI: cấu hình csproj (PublishAot, NativeLib=Shared, StripSymbols, InvariantGlobalization...), danh sách đầy đủ các hàm xuất khẩu, hợp đồng ActionCode, và cách export bằng `[UnmanagedCallersOnly]`. Lấy `cabibridge.h` làm nguồn chân lý để chép chữ ký hàm chính xác.

### 4.1. Project & SDK Configuration
* `PublishAot=true`, `NativeLib=Shared`, `AssemblyName=BambooMintKeyCore`.
* `StripSymbols=true`, `InvariantGlobalization=true`, `AllowUnsafeBlocks=true`.
* `TargetFramework=net10.0`.
* Rationale for each flag (size reduction, ICU removal, pointer arithmetic for buffers).

### 4.2. The Unmanaged Surface (exact signatures)
> Reproduce the `extern "C"` block from `cabibridge.h` verbatim.

* **Version & diagnostics**
  * `int bmk_version(void)`
  * `void bmk_gc_collect(void)`
  * `int bmk_get_live_context_count(void)`
* **Context lifecycle** (GCHandle-backed)
  * `void *bmk_context_create(void)`
  * `void bmk_context_free(void *handle)`
  * `void bmk_context_reset(void *handle)`
* **Key processing** (returns an `ActionCode`)
  * `int bmk_process_key(void *handle, uint32_t unicodeChar)`
  * `int bmk_process_backspace(void *handle)`
  * `int bmk_process_wordbreak(void *handle, uint32_t breakChar)`
* **Text retrieval** (read-only, null-terminated)
  * `const char *bmk_get_preedit_text(void *handle)`
  * `const char *bmk_get_commit_text(void *handle)`
  * `int bmk_get_preedit_length(void *handle)`
* **Runtime configuration**
  * `void bmk_set_options(void *handle, int isEnabled, int toneStyle, int autoRestoreEnglish, int allowRepeatUndo, int allowLeadingW, int allowFreeTone, int enableVietnameseDictionary, int enableEnglishBacktracking)`
  * `int bmk_load_config_json(void *handle, const char *jsonUtf8)`

### 4.3. Action Code Contract
* `ActionPassThrough = 0`, `ActionConsume = 1`, `ActionUpdatePreedit = 2`, `ActionCommitString = 3`.
* Explain the semantics of each and why `ActionConsume` is currently reserved/unused.

### 4.4. Exporting Entrypoints
* `[UnmanagedCallersOnly(EntryPoint = "...", CallConvs = [typeof(CallConvCdecl)])]`.
* Why `CallConvCdecl` on Linux vs. `CallConvStdcall` on Windows COM (platform calling convention).
* AOT-safe constraints: no generics, no exceptions across the boundary, `unsafe` context for raw pointers.

---

## 5. Memory Management & Zero-Allocation Buffers

> **TODO (VN):** Đây là đóng góp kỹ thuật chính của bài. Trình bày mô hình bộ nhớ: hai buffer cố định 256 byte thuộc context, trả con trỏ read-only, caller không free → zero heap allocation trên đường xử lý phím nóng, không GC pause. Giải thích ownership contract, vì sao con trỏ ổn định (NativeMemory tránh GC relocate), và mô hình thread-safety theo từng context.

> **This is the report's key technical contribution — emphasize it.**

### 5.1. Ownership Contract
* Buffers are **owned by the context**, not the caller.
* Caller only reads, never frees. No `bmk_free_string` exists.
* The contract is documented in `EngineContext` XML docs and `cabibridge.h`.

### 5.2. Fixed-Size Native Buffers
* Two fixed 256-byte buffers per context: preedit and commit.
* Allocated with `NativeMemory.AllocZeroed(256)`, written with `Encoding.UTF8.GetBytes(...)`.
* Null-terminated, `byte*` returned to the caller.

### 5.3. Why This Design (the "so what")
* **Zero heap allocation** on the hot key path — no GC pressure, no jitter in the Fcitx5 event loop.
* **Stable pointers** — `NativeMemory` avoids GC relocation; the C++ caller never observes a dangling pointer.
* **Deterministic memory bound** — per-context footprint is constant.
* Contrast with the naive alternative (alloc-per-keystroke + explicit free) that the draft previously assumed.

### 5.4. Thread Safety
* Per-context lock (`context.SyncRoot`) around all key processing.
* Why state is per-handle (not `static`): the Fcitx5 daemon is a single shared process across all windows — unlike Windows TSF where each app hosts its own DLL copy.

---

## 6. Consuming the Library in the Fcitx5 C++ Addon

> **TODO (VN):** Trình bày cách tích hợp `.so` vào addon C++ Fcitx5: chiến lược link CMake (target_link_libraries + IMPORTED target + rpath $ORIGIN, không dùng dlopen), factory & lifecycle, vòng composition (keyEvent → bmk_process_key → setClientPreedit/commitString), phân biệt preedit vs direct commit, và cơ chế D-Bus + inotify hot-reload.

### 6.1. Build & Link Strategy
* CMake: `find_package(Fcitx5Core/Config/Utils REQUIRED)`, C++17.
* **Direct linking** of `BambooMintKeyCore.so` via an `IMPORTED` target and `target_link_libraries` — *not* `dlopen`/`dlsym` (clarify the discrepancy with the old code comment).
* `INSTALL_RPATH` / `BUILD_RPATH` = `$ORIGIN` so the core `.so` is discovered next to the addon.
* `add_library(bamboomintkey SHARED engine.cpp)` → `libbamboomintkey.so`.

### 6.2. Addon Factory & Lifecycle
* `FCITX_ADDON_FACTORY(BambooMintKeyFactory)` registration.
* `BambooMintKeyEngine : fcitx::AddonInstance` construction, property registration, config load, `inotify` watcher setup.

### 6.3. The Composition Loop
* Intercepting keys in `keyEvent(const fcitx::InputMethodEntry &, fcitx::KeyEvent &)`.
* Hotkey handling (grave key `` ` `` toggles V/E before any engine call).
* Forwarding into `bmk_process_key` / `bmk_process_backspace` / `bmk_process_wordbreak`.
* Mapping `ActionCode` → `setClientPreedit` / `commitString` / `filterAndAccept`.

### 6.4. Preedit vs. Direct Commit
* Level 1 (preedit) used for all apps by default.
* Why Level 2 (`deleteSurroundingText`) was avoided — concrete evidence from `engine.cpp` comments (some apps, e.g. Zed, advertise SurroundingText support but mishandle deletion → duplicated text).

### 6.5. D-Bus & Config Hot-Reload
* D-Bus service `org.fcitx.Fcitx5.BambooMintKey`, object `/org/fcitx/Fcitx5/BambooMintKey`, interface `org.fcitx.Fcitx5.BambooMintKey1`.
* `setVietnameseMode` / `getVietnameseMode` / `toggleVietnameseMode` + `ModeChanged` signal.
* `inotify` watching `~/.config/bamboomintkey/config.json` (`IN_CLOSE_WRITE | IN_MOVED_TO | IN_CREATE`).

---

## 7. Cross-Platform Portability: Windows TSF & macOS IMK

> **TODO (VN):** Chứng minh tính đa nền tảng: Windows TSF bridge (đã triển khai, cùng lõi F# nhưng adapter khác) và khả năng tái dùng nguyên C-ABI cho macOS IMK trong tương lai. Củng cố luận điểm "cross-platform" ngay trong tiêu đề bài báo.

### 7.1. The Windows TSF Bridge (already implemented)
* `BambooMintKey.NativeBridge` → `BambooMintKey.dll`, in-process COM TIP.
* COM exports: `DllGetClassObject`, `DllRegisterServer`, `DllUnregisterServer`.
* TSF interfaces implemented: `ITfTextInputProcessorEx`, `ITfKeyEventSink`, `ITfThreadMgrEventSink`, `ITfDisplayAttributeProvider`, `ITfCompartmentEventSink`, `ITfLangBarItemButton`.
* Cross-process state via 64-byte shared memory (`Local\BambooMintKey_SharedConfig_v1`) + broadcast event.

### 7.2. Reusing the Same C-ABI for macOS (future)
* Recompile the bridge for `osx-arm64` → `libBambooMintKey.dylib`.
* Replace the C++ Fcitx5 wrapper with a Swift/Objective-C `IMKInputController` adapter.
* Retain 100% of the F# core and the C-ABI contract unchanged.

---

## 8. Deployment, Packaging & System Integration

> **TODO (VN):** Mô tả bố cục cài đặt và đóng gói: vị trí thư viện theo từng distro Linux, metadata addon/inputmethod, Flatpak/SteamOS (extension trên rootfs bất biến), và Windows (Inno Setup / Microsoft Store / WinGet).

### 8.1. Linux System Layout
* Library placement per distro:
  * Fedora/RHEL: `/usr/lib64/…`
  * Debian/Ubuntu: `/usr/lib/x86_64-linux-gnu/…`
* Addon metadata: `addon/*.conf`, `inputmethod/*.conf`, icon SVG in `hicolor`.
* Packaging scripts: `scripts/linux/package_linux.sh` (`.deb`, `.rpm`, tarball), `scripts/linux/package_flatpak.sh`.

### 8.2. Flatpak / SteamOS
* Fcitx5 Addon Extension `org.fcitx.Fcitx5.Addon.BambooMintKey`.
* Mount point `/app/addons/BambooMintKey` — works on the immutable read-only SteamOS rootfs.

### 8.3. Windows Packaging
* Inno Setup installer, Microsoft Store, WinGet manifest.

---

## 9. Evaluation & Measurements

> **TODO (VN):** Nơi đặt số liệu đo thực tế: kích thước binary, latency/throughput (dùng script `bench-cabi.py` đã viết), độ chính xác (accuracy), và kiểm tra rò rỉ bộ nhớ. Điền các placeholder `[MEASURE: ...]` bằng số đo thật khi đã build và chạy benchmark.

> **All numbers below must be collected from actual builds. Replace `[MEASURE: …]` placeholders with real data before publication.**

### 9.1. Binary Footprint
* `BambooMintKeyCore.so` size: `[MEASURE: bytes]`.
* `libbamboomintkey.so` size: `[MEASURE: bytes]`.
* `BambooMintKey.dll` size: `[MEASURE: bytes]`.
* Effect of `StripSymbols` and `InvariantGlobalization`: `[MEASURE: before/after]`.

### 9.2. Startup & Latency
* Context creation time: `[MEASURE: µs]`.
* Per-keystroke latency in the Fcitx5 event loop: `[MEASURE: µs]`.
* Demonstration of no GC pause (zero allocation on hot path).

### 9.3. Correctness
* Unit-test pass rate (currently documented as 119/119 in `SYSTEM_ARCHITECTURE.md`; verify current count).
* C-ABI integration tests (`scripts/tests/test-cabi.py`, 9 scenarios).

### 9.4. Memory Safety & Leak Checks
* `bmk_get_live_context_count` / `bmk_gc_collect` diagnostics.
* Results of leak-testing runs: `[MEASURE]`.

---

## 10. Limitations & Future Work

> **TODO (VN):** Liệt kê hạn chế hiện tại (ActionConsume chưa dùng, macOS chưa triển khai, settings UI còn framework-dependent) và hướng khắc phục (AOT cho UI hoặc thay bằng UI native). Thể hiện sự trung thực khoa học.

* `ActionConsume` path not yet exercised.
* macOS adapter not yet implemented.
* Potential ABI-versioning strategy as the surface grows.
* Code-signing maturity (Windows Authenticode, Linux distro signing).
* **Settings UI is still framework-dependent.** Two possible directions:
  * Publish the Avalonia UI self-contained, or adopt NativeAOT for the UI (subject to
    Avalonia trimming/AOT constraints around reflection and XAML resource lookup).
  * For a literal "100% zero-runtime" distribution, replace the settings UI with a native
    (Qt/GTK or plain C++) implementation — noting this still bundles *a* (native) runtime;
    it only removes the *managed* runtime.

---

## 11. Conclusion

> **TODO (VN):** Tổng kết 3 trụ cột (lõi F# thuần hàm, cầu nối NativeAOT C-ABI, buffer zero-allocation), nhắc lại scope trung thực (zero-dependency chỉ cho engine, UI ngoài process vẫn framework-dependent), và kêu gọi phản hồi từ cộng đồng .NET/Fcitx5.

* Recap the three pillars: pure F# core, NativeAOT C-ABI bridge, zero-allocation buffer contract.
* Restate that .NET 10 NativeAOT is production-viable for native OS extension integration —
  specifically for the in-daemon input engine path.
* Reiterate the honest scope: "zero-dependency" holds for the input engine; the settings
  GUI runs out-of-process and remains framework-dependent, a deliberate and acceptable
  trade-off since every UI framework (Qt/GTK included) carries a runtime of some kind.
* Call to action: invite feedback from the .NET and Fcitx5 communities on ABI patterns and memory safety.

---

## 12. References

> **TODO (VN):** Liệt kê tài liệu tham khảo: docs .NET NativeAOT, `UnmanagedCallersOnlyAttribute`, tài liệu phát triển addon Fcitx5, TSF, và các bộ gõ tiền nhiệm (Unikey, ibus-bamboo, fcitx5-bamboo). Điền URL thật thay cho `[TODO: fill ...]`.

* `.NET NativeAOT` documentation.
* `UnmanagedCallersOnlyAttribute` API reference.
* Fcitx5 addon development docs.
* TSF (Text Services Framework) documentation.
* Prior art: Unikey, ibus-bamboo, fcitx5-bamboo.
* `[TODO: fill with actual URLs / citations]`
