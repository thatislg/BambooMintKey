# Đặc Tả Kỹ Thuật Tầng 2: Kiểm Thử Tích Hợp Trên Ứng Dụng Thật (Tier 2 Real-App Benchmark)

---

## 1. Mục Đích & Nguyên Tắc Hoạt Động

Khác với Tầng 1 (chạy in-memory trong core engine), **Tầng 2 đo lường trải nghiệm gõ phím thực tế** khi bộ gõ tương tác với hệ điều hành và các ứng dụng đồ họa (GUI Applications).

### 1.1. Tính Tương Thích Chung Cho Mọi Bộ Gõ (Universal Compatibility)
> [!IMPORTANT]
> **Tầng 2 là bài kiểm thử hộp đen (Black-box Testing) 100% độc lập với bộ gõ.**
> Bất kỳ bộ gõ nào (BambooMintKey, UniKey, EVKey, OpenKey, GoTiengViet, Gboard desktop...) khi được kích hoạt trên hệ điều hành đều **dùng chung nguyên vẹn** runner Tầng 2 này mà không cần sửa đổi bất kỳ dòng mã nguồn nào của bộ gõ.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   TIER 2 UNIVERSAL RUNNER (SendInput)                  │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (Phát chuỗi Virtual Keys)
┌────────────────────────────────────────────────────────────────────────┐
│                        HỆ ĐIỀU HÀNH WINDOWS                            │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ BỘ GÕ BẤT KỲ ĐANG ACTIVE (BambooMintKey / UniKey / EVKey...)     │  │
│  │ (Hook bàn phím / TSF TIP xử lý và xuất văn bản tiếng Việt)       │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (Text Renderer)
┌────────────────────────────────────────────────────────────────────────┐
│                        ỨNG DỤNG ĐƯỢC KIỂM THỬ                          │
│        [App 1: Windows Notepad]      [App 2: VS Code / Chrome]         │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (Đọc lại qua Clipboard / UIA)
┌────────────────────────────────────────────────────────────────────────┐
│                     ĐỐI CHIẾU & ĐÁNH GIÁ CHỈ SỐ                        │
│   • Tỷ lệ đúng %        • Mất/nuốt phím      • Đảo thứ tự ký tự        │
│   • Tốc độ gõ tối đa    • Rò rỉ RAM/CPU      • Giật gạch chân preedit  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Ma Trận 2 Ứng Dụng Thử Nghiệm Chuẩn (1.000 Từ)

Để đại diện cho hai trường phái công nghệ giao diện phổ biến và khắc nghiệt nhất trên Windows, Tầng 2 chọn 2 ứng dụng:

### Ứng Dụng 1: Windows Notepad (`notepad.exe`)
- **Đại diện:** Native Win32 Desktop Application (Edit Control / WinUI RichEdit trên Windows 11).
- **Đặc thù:** Đơn giản, phản hồi tức thời, dùng để đo chuẩn mực "tốc độ tối đa" và độ ổn định cơ bản của bộ gõ khi không bị ảnh hưởng bởi web render pipeline.
- **Cách trích xuất kết quả:** `Ctrl + A` -> `Ctrl + C` (Clipboard) hoặc Win32 `WM_GETTEXT`.

### Ứng Dụng 2: Visual Studio Code (`code.exe`) hoặc Chrome / DevHarness
- **Đại diện:** Chromium / Electron / Monaco Editor Canvas.
- **Đặc thù:** Có vòng lặp sự kiện bất đồng bộ (Async Event Loop), xử lý composition string phức tạp, thường xuyên xảy ra lỗi **nuốt phím (swallowed keys)** hoặc **đảo ký tự (transposed keys)** khi gõ nhanh trên 15 phím/giây.
- **Cách trích xuất kết quả:** `Ctrl + A` -> `Ctrl + C` (Clipboard) hoặc file export.

---

## 3. Quy Trình Thực Thi 1.000 Từ (Corpus Typing Scenario)

1. **Khởi tạo môi trường:**
   - Kích hoạt bộ gõ mục tiêu (chọn bảng mã Unicode, kiểu gõ Telex).
   - Khởi động cửa sổ ứng dụng mục tiêu (Notepad hoặc VS Code).
   - Đưa con trỏ và tiêu điểm (focus) vào vùng nhập văn bản trống.
2. **Nạp 1.000 từ vựng:**
   - Lấy 1.000 từ phổ biến nhất từ `dicts/vi-corpus-freq.tsv`.
   - Sinh chuỗi phím Telex kèm phím cách `<SPACE>` ngắt từ:
     `nawm <SPACE> loaif <SPACE> thee <SPACE> cuar <SPACE> ...`
3. **Bắn phím mô phỏng (SendInput Cadence):**
   - Tốc độ gõ trung bình: 15 phím/giây (delay 66ms giữa các phím).
   - Tốc độ stress-test: 25 phím/giây (delay 40ms giữa các phím).
4. **Thu thập và phân tích kết quả:**
   - Đọc toàn bộ văn bản thực tế trong ứng dụng.
   - Tách thành 1.000 từ thực tế và so khớp với 1.000 từ gốc.

---

## 4. Các Chỉ Số Đánh Giá Định Lượng

| Chỉ Số | Đơn Vị | Ý Nghĩa Kỹ Thuật | Ngưỡng Kỳ Vọng |
| :--- | :---: | :--- | :---: |
| **Accuracy (Độ đúng)** | `%` | Số từ tiếng Việt xuất ra chính xác trên tổng 1.000 từ | $\ge 99.5\%$ |
| **Swallowed Chars (Nuốt ký tự)** | Ký tự | Số lần ký tự bị biến mất do bộ gõ nuốt nhầm hoặc app drop event | $0$ |
| **Transposed Chars (Đảo ký tự)** | Lần | Hiện tượng gõ nhanh bị nhảy lộn thứ tự (ví dụ: `được` -> `đựcơ`) | $0$ |
| **Memory Working Set** | MB | Mức tiêu thụ RAM của tiến trình bộ gõ sau 1.000 từ | $< 35\text{ MB}$ |
| **Crash Rate** | Lần | Số lần tiến trình IME hoặc ứng dụng mục tiêu bị crash | $0$ |
