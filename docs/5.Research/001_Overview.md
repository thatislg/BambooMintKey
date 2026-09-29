# Các chủ đề kỹ thuật trích xuất trực tiếp từ kiến trúc thực tế của **BambooMintKey**:

---

### 1. Chủ đề 1: Đưa .NET 10 NativeAOT vào In-Process COM Server của Windows TSF

*Đây là bài toán hiếm thấy trong hệ sinh thái .NET, đánh trúng sự quan tâm của nhóm phát triển NativeAOT.*

* **Nội dung trọng tâm:**
* Cách biên dịch **.NET 10 NativeAOT** thành một tệp DLL duy nhất (`BambooMintKey.dll`) hoạt động như một **In-Process COM Server** mà không cần máy đích cài sẵn .NET Runtime.
* Triển khai trực tiếp các giao diện COM của Text Services Framework (`ITfTextInputProcessorEx`, `ITfKeyEventSink`) bằng C# Unmanaged Exports (`[UnmanagedCallersOnly]`).
* Trải nghiệm thực tế về thời gian khởi động (cold start), độ trễ nhận phím (zero Garbage Collection stutter) và kiểm soát bộ nhớ khi DLL bị nạp vào không gian bộ nhớ của các tiến trình lạ (Notepad, trình duyệt, Word).
> *Case Study: Implementing a Zero-Dependency Windows TSF Text Input Processor using .NET 10 NativeAOT and In-Process COM*



---

### 2. Chủ đề 2: Mô hình kiến trúc lai: Functional Core (F#) kết hợp Imperative Bridge (C# NativeAOT)

*Chủ đề này đặc biệt hấp dẫn trên diễn đàn `dotnet/fsharp` và nhóm ngôn ngữ của Microsoft.*

* **Nội dung trọng tâm:**
* Tại sao lại chọn **F#** cho lõi xử lý vần tiếng Việt: Lợi thế của cấu trúc dữ liệu bất biến (immutability) và so khớp mẫu (pattern matching) trong việc xây dựng máy trạng thái hữu hạn (deterministic state machine) cho quy tắc Telex phức tạp.
* Cách liên kết tĩnh mã nguồn F# (`BambooMintKey.Core`) vào project C# NativeAOT (`BambooMintKey.NativeBridge`) mà không phát sinh overhead khi gọi hàm trong bộ nhớ.
* Bài học kinh nghiệm: Những hạn chế hoặc điều cần lưu ý khi AOT mã nguồn F# trong .NET 10.
> *Showcase: Designing a Deterministic Syllable Engine with F# and Statically Linking with C# NativeAOT*



---

### 3. Chủ đề 3: Xuất C-ABI từ .NET NativeAOT để tích hợp vào C++ Addon trên Linux (Fcitx5)

*Chủ đề chứng minh tính đa nền tảng và khả năng Interop cấp thấp của .NET trên Linux.*

* **Nội dung trọng tâm:**
* Cách cấu hình `dotnet publish` với cờ `NativeLib=Shared` để xuất ra tệp `libBambooMintKey.so` mang chuẩn C-ABI.
* Cơ chế kết nối giữa module C++ của **Fcitx5** (`bamboomintkey.so`) với thư viện C-ABI của .NET mà không cần qua tầng IPC cồng kềnh.
* Bài toán quản lý chuỗi UTF-8, con trỏ bộ nhớ native giữa C++ và .NET NativeAOT runtime trên các bản phân phối Linux (Ubuntu, Fedora).
> *Cross-Platform System Interop: Consuming .NET 10 NativeAOT Shared Libraries via C-ABI in Linux Fcitx5 C++ Addons*



---

### 4. Chủ đề 4: Chia sẻ bộ nhớ và IPC giữa Avalonia UI với Native IME Engine

*Chủ đề hấp dẫn cộng đồng làm ứng dụng Desktop UI hiện đại.*

* **Nội dung trọng tâm:**
* Kiến trúc tách biệt: Lõi gõ chạy In-Process (trong ứng dụng đích trên Windows hoặc trong Fcitx5 daemon trên Linux), trong khi cửa sổ Settings GUI chạy độc lập bằng **Avalonia UI**.
* Cơ chế đồng bộ cấu hình thời gian thực (Real-time synchronization) qua Shared Memory và tín hiệu thông báo đa tiến trình (Cross-process event / D-Bus) mà không cần khởi động lại ứng dụng.
> *Architecting Low-Latency IPC: Synchronizing Avalonia UI with an In-Process System Input Engine*
