# Methodology (Draft — Tiếng Việt, tạm thời)

> **LƯU Ý:** Đây là bản nháp tiếng Việt, tạm lưu để khỏi quên. Sau này sẽ convert sang tiếng Anh và rút gọn đưa vào chương 1 của báo cáo, hoặc xóa nếu thấy không cần. Chưa phải nội dung chính thức.

---

## 1. Loại nghiên cứu

Đây là **Design Science Research (DSR)** dưới dạng **case study**. Nghĩa là:

- Sản phẩm nghiên cứu là một **artifact phần mềm** (BambooMintKey), không phải một mô hình lý thuyết.
- Đóng góp nằm ở **cách xây dựng artifact + đánh giá nó**, không phải phát hiện một quy luật tự nhiên.

Không phải Experimental/Quantitative (không chạy kiểm định thống kê, không p-value).

## 2. Artifact

BambooMintKey — lõi F# thuần hàm + cầu nối NativeAOT phơi qua C-ABI, được tiêu thụ bởi các adapter hệ thống (TSF trên Windows, Fcitx5 trên Linux, IMK trên macOS).

## 3. Objectives (2 RQ)

- **RQ1:** Một lõi F# duy nhất, biên dịch NativeAOT và phơi qua C-ABI, có thể làm input engine đa nền tảng mà không cần managed runtime trong hot path?
- **RQ2:** F# có đủ sức làm ngôn ngữ hạng nhất cho phần mềm desktop (một IME), phản bác định kiến "F# chỉ dành cho tài chính/backend"?

## 4. Quy trình (6 bước DSR → ánh xạ vào chương)

| Bước DSR | Chương tương ứng |
|---|---|
| 1. Problem identification | 1 (Introduction) + 2 (Related Work) |
| 2. Objectives | 1.4 (Purpose & Contributions — RQ1/RQ2) |
| 3. Design & development | 3–8 (kiến trúc, C-ABI, bộ nhớ, addon, đa nền tảng, đóng gói) |
| 4. Demonstration | Chạy thật trên Windows + Linux |
| 5. Evaluation | 9 (đo footprint/latency/correctness) |
| 6. Communication | Toàn bộ báo cáo 002 |

## 5. Phương pháp đánh giá

Hai công cụ đánh giá, tương ứng hai bản chất bằng chứng:

1. **Benchmark định lượng** (`[MEASURE]`): đo binary footprint, startup/latency, correctness, leak bằng `scripts/tests/bench-cabi.py` — tái lập được bằng lệnh.
2. **So sánh định tính** (`survey`): bảng khảo sát các bộ gõ hiện có (mục 2.1) để định vị novelty.

Không dùng kiểm định thống kê (đây là case study, cỡ mẫu nhỏ, mục tiêu là chứng minh thuộc tính chứ không suy luận quần thể).

## 6. Tiêu chí truy vết (traceability)

Mọi claim gắn bằng chứng theo `006_Report_Writing_Rules.md`:
- Code → file + symbol/dòng.
- Ngoài → citation `[n]` vào `12-references`.
- Số liệu → `[MEASURE: x — lệnh/môi trường]`.

---

## 7. TODO (điều cần quyết định sau)

- [ ] Có cần nêu "threats to validity" (đe dọa đến giá trị kết luận) không? — ví dụ: đánh giá trên một máy cụ thể, chưa có corpus chuẩn.
- [ ] Có nên đưa hẳn DSR vào báo cáo, hay chỉ nói gọn "case study + benchmark"?
- [ ] Xác định môi trường benchmark chuẩn (OS, CPU, phiên bản .NET) để số đo tái lập được.
