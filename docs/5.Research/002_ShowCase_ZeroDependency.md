 **Đề tài 3: Thiết kế tầng C-ABI từ .NET 10 NativeAOT để tích hợp vào Fcitx5 C++ Addon trên Linux và mở rộng đa nền tảng**.

---

# Technical Showcase: Building a Zero-Dependency, Cross-Platform IME with .NET 10 NativeAOT and C-ABI Interop

## 1. Introduction & Background

* **The Cross-Platform Input Challenge:**
* Brief overview of the fragmentation across desktop input subsystems: Windows Text Services Framework (TSF / COM), Linux Input Method Frameworks (Fcitx5 / D-Bus / C-ABI), and macOS InputMethodKit (IMK).


* Why running managed code (standard .NET Runtime / CLR) inside host input daemons or host client processes is historically impractical (runtime bootstrap lag, GC pauses, binary bloat, process crashes).




* **The Project (BambooMintKey):**
* Goal: A high-performance, deterministic Vietnamese IME built on a unified functional core.


* Tech Stack: **F# (Pure Syllable & Grammar Core)** + **C# NativeAOT (Interop Bridge)** + **Avalonia UI (Out-of-process Settings)**.




* **Purpose of this Showcase:**
* Share practical experiences and implementation patterns using **.NET 10 NativeAOT** to expose a lean C-ABI shared library (`.so`) consumed directly by native C++ daemons on Linux without a pre-installed .NET environment.





---

## 2. Architectural Blueprint: The Decoupled Adapter Model

* **Layer Separation:**
* *Layer 1 (Linguistic Engine):* Pure functional state transformations in F# (`BambooMintKey.Core`).


* *Layer 2 (Native Bridge):* C# project compiled ahead-of-time into native machine code (`libBambooMintKey.so`).


* *Layer 3 (OS Adapter):* Lightweight C++ Addon module (`bamboomintkey.so`) loaded directly by the Fcitx5 host process via dynamic linking / `dlopen`.




* **ASCII Architectural Diagram:**
* Visualizing the flow from key events in Fcitx5 $\rightarrow$ C++ Addon $\rightarrow$ C-ABI Entrypoints $\rightarrow$ F# Engine $\rightarrow$ Preedit/Commit return.





---

## 3. Engineering the C-ABI Surface with .NET 10 NativeAOT

* **SDK & Project Configuration:**
* Setting up `PublishAot=true` and `NativeLib=Shared` in `BambooMintKey.NativeBridge.csproj`.


* Targeting `linux-x64` with self-contained native generation.




* **Defining the Unmanaged Surface:**
* Exporting unmanaged entrypoints using `[UnmanagedCallersOnly(EntryPoint = "...", CallingConvention = CallingConvention.Cdecl)]`.
* Key lifecycle and transaction APIs:
* `bmk_init()`, `bmk_destroy()` (Context allocation & cleanup).


* `bmk_process_key(int key_code, int modifiers, ...)` (Fast-path evaluation).


* `bmk_get_preedit_text(...)`, `bmk_commit_text(...)` (Text retrieval).






* **Cross-Boundary Memory & String Marshaling:**
* Zero-leak memory contract: Managing UTF-8 string pointers across the managed unmanaged boundary.
* Allocating via `Marshal.StringToCoTaskMemUTF8` (or native memory allocator) and exposing an explicit `bmk_free_string()` function to ensure the host process frees memory safely.





---

## 4. Consuming the Shared Library in the Linux Fcitx5 C++ Addon

* **Addon Packaging & Integration:**
* Implementing the Fcitx5 module factory (`FCITX_ADDON_FACTORY` / `FCITX_ADDON_EXPORT`) in standard C++17.


* Dynamic linking vs `dlopen` strategy: Linking `libBambooMintKey.so` directly into the addon build pipeline via CMake (`target_link_libraries`).




* **Handling the Composition Loop:**
* Intercepting key events inside `keyEvent(const InputMethodEntry&, KeyEvent&)`.


* Updating composition state: Forwarding calculated output into `ic->inputPanel().setClientPreedit()` and controlling inline display without UI stutter.


* Distinguishing between active composition sessions and direct key forwarding.





---

## 5. Deployment, System Integration & Packaging Insights

* **Linux System Layout:**
* Library placement rules on modern distros: `/usr/lib64/` (Fedora/RHEL) vs `/usr/lib/x86_64-linux-gnu/` (Debian/Ubuntu).


* Dynamic linker caching (`ldconfig`) and metadata configuration files (`addon/*.conf` and `inputmethod/*.conf`).




* **Binary Footprint & Startup Latency:**
* Binary size evaluation of the `.so` artifact produced by .NET 10 NativeAOT.


* Key response latency: Achieving sub-millisecond execution times without GC pauses inside the Fcitx5 event loop.





---

## 6. Portability Horizon: From Linux (Fcitx5) to macOS (IMK)

* **Reusability of the C-ABI:**
* How this exact same C-ABI contract seamlessly targets macOS via `osx-arm64` NativeAOT compilation (`libBambooMintKey.dylib`).


* Replacing the C++ Fcitx5 wrapper with a thin Swift / Objective-C `IMKInputController` adapter while retaining 100% of the F# Core and NativeBridge logic.





---

## 7. Key Takeaways & Discussion Points

* **Practical Observations:**
* NativeAOT in .NET 10 is mature enough for direct, low-level OS extension integration (no runtime dependencies required on client machines).


* Functional programming in F# provides deterministic string transformation guarantees critical for complex tonal orthography.




* **Links & Community Feedback:**
* Open-source repository link on GitHub (`thatislg/BambooMintKey`).


* Inviting technical feedback on NativeAOT memory safety, optimization flags, and cross-platform ABI patterns from the .NET team and community.
