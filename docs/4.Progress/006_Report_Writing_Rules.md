# Bộ Luật Viết & Tiêu Chí Kiểm Chứng — Báo Cáo Khoa Học 002

**Mục đích:** Đảm bảo mọi nội dung trong `docs/5.Research/002/` đều **truy vết được** (traceable) và **kiểm chứng được** (verifiable) — không suy diễn kết quả khi chưa đo/đối chiếu nguồn.

**Áp dụng:** Tất cả file `.mdx` trong `docs/5.Research/002/`.

---

## 1. Hai Trục Mục Tiêu (Research Questions)

Mọi chương phải đóng góp trả lời ít nhất một trong hai RQ. Đây là "mục tiêu" của toàn báo cáo:

- **RQ1 — Cross-platform với .NET:** Liệu một lõi F# duy nhất, biên dịch NativeAOT và phơi qua C-ABI, có thể làm input engine cho nhiều framework IME desktop (TSF / Fcitx5 / IMK) mà **không cần managed runtime trong hot path**?
- **RQ2 — F# trong desktop:** F# có đủ sức làm ngôn ngữ *hạng nhất* cho phần mềm hệ thống desktop (một IME), phản bác định kiến "F# chỉ dành cho tài chính/backend"?

> RQ2 không nằm gọn ở một chương mà là luận điểm xuyên suốt: mỗi chương kỹ thuật là một bằng chứng "đây là F# thuần hàm đang chạy thật trong IME desktop".

---

## 2. Quy Tắc Truy Vết (Traceability)

1. **Không có phát biểu trần (naked assertion).** Mỗi mệnh đề sự kiện phải gắn bằng chứng.
2. **Phát biểu về code** → trích dẫn đường dẫn file + tên symbol / dòng cụ thể.
   - Ví dụ: `src/BambooMintKey.Core.Native/Exports.cs` → `bmk_process_key`.
3. **Phát biểu về bên ngoài** → trích dẫn nguồn (repo / paper / URL), gán số `[n]` vào `12-references`.
4. **Số liệu** → luôn kèm cách đo (lệnh, môi trường, phiên bản).

---

## 3. Marker Bằng Chứng (chống suy diễn kết quả)

Dùng đúng marker để phân biệt "đo được" với "ước lượng" với "cần kiểm chứng":

| Marker | Ý nghĩa | Khi nào dùng |
|---|---|---|
| `[MEASURE: x — lệnh/môi trường]` | Giá trị đo thật | Đã chạy và ghi nhận số liệu |
| `[ESTIMATE: ~x — cơ sở]` | Giá trị ước lượng | Chưa đo, nêu rõ cơ sở ước lượng |
| `[CLAIM: cần nguồn]` | Phát biểu chưa kiểm chứng | Đã viết nhưng chưa có nguồn |
| `[TODO (VN): ...]` | Ghi chú draft tiếng Việt | Chưa viết, sẽ convert sang EN sau |

> **Nguyên tắc vàng:** Nếu chưa đo, **không** ghi số liệu "có vẻ đúng". Ghi `[ESTIMATE]` hoặc `[MEASURE]` sau khi thực sự chạy.

---

## 4. Mỗi Chương Phải Có Đánh Giá

- Mỗi chương kết thúc bằng mục con **`X.Y Evaluation`**.
- Nội dung đánh giá phải liên hệ ngược lại RQ (xem bảng ánh xạ trong `005_Report_Progress.md`).

---

## 5. Quy Tắc Trung Thực (Honesty / Scope Boundary)

- Không phóng đại claim. Ví dụ: "zero-dependency" chỉ đúng cho **đường xử lý gõ (daemon engine)**, UI cấu hình Avalonia vẫn cần runtime .NET — phải nói rõ phạm vi.
- Khi có giới hạn, ghi rõ giới hạn thay vì né tránh.

---

## 6. Quy Ước Ngôn Ngữ & Định Dạng

- **Nội dung cuối** viết tiếng Anh.
- **Ghi chú draft** (`TODO (VN)`) viết tiếng Việt, sẽ convert dần sang EN khi hoàn thiện.
- File dùng `.mdx` (Astro/Starlight), có frontmatter `title` + `description`.
- Bảng: căn cột rõ, đặt nguồn ở cuối bảng hoặc footnote.

---

## 7. Định Nghĩa "Xong" Một Chương (Definition of Done)

Một chương chỉ được đánh dấu ✅ khi:

1. Đủ các file `.mdx` của chương (đúng danh sách trong `005_Report_Progress.md`).
2. Không còn `[CLAIM]` chưa có nguồn, không còn `[TODO (VN)]` ở nội dung EN chính thức.
3. Có mục `X.Y Evaluation` liên hệ RQ.
4. Mọi số liệu đều là `[MEASURE]` (hoặc `[ESTIMATE]` được ghi rõ cơ sở).
5. Đã đối chiếu với mã nguồn thực tế (nếu chương kỹ thuật).

---

## 8. Quy Trình Kiểm Chứng Trước Khi Commit

1. Chạy `diagnostics` / kiểm tra nội dung file đã viết.
2. So khớp từng `[MEASURE]` với lệnh đã chạy (giữ lại lệnh trong chương 9 hoặc `12-references`).
3. Cập nhật bảng tiến độ + Progress Log trong `005_Report_Progress.md`.
4. Commit theo từng chương (không gom quá nhiều chương trong 1 commit).
