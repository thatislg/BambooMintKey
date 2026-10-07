<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 015: Lỗi preedit dở dang trên Linux — tự xóa chữ đầu (Antigravity) & tự copy-chèn khi bấm mũi tên (Chromium URL bar)

**Mã tài liệu:** `015_Linux_Preedit_Commit_Order_And_ArrowKey`

**Trạng thái:** 🔍 Đã điều tra nguyên nhân — đang chờ duyệt phương án trước khi sửa code

**Mức độ nghiêm trọng:** Nghiêm trọng (ảnh hưởng trải nghiệm gõ trên các app Chromium/Electron phổ biến)

**Nền tảng:** Linux (Fcitx 5). Chưa tái hiện trên Windows (TSF).

**Module liên quan:** `src/BambooMintKey.Fcitx5/engine.cpp`

**Ngày ghi nhận:** 07/10/2026

---

## 1. Mô tả hiện tượng

Hai lỗi riêng biệt nhưng cùng bản chất, đều mới phát hiện trên Linux:

### 1.1. Bug A — Antigravity IDE (khung chat với AI): chữ đầu tiên bị "xóa" khi space
- Khi gõ **chữ đầu tiên** rồi bấm `Space`, chữ đó lập tức **bị xóa** (biến mất) thay vì được chốt lại.
- Để gõ được, người dùng **bắt buộc phải bấm `Space` 1 lần trước** (lúc preedit còn trống), rồi mới gõ chữ → lúc đó mới gõ được.
- Không bấm space trước thì gõ bất kỳ chữ nào (có dấu hay không dấu) cũng bị xóa, chỉ cần **chưa thoát preedit**.
- **Chỉ xảy ra ở Antigravity IDE**; cùng hệ Electron như **VS Code thì không bị**.

### 1.2. Bug B — Thanh URL của trình duyệt nhân Chromium (Opera, Chrome): tự copy-chèn khi bấm mũi tên
- Khi đang ở **chế độ preedit chưa kết thúc bằng Space**, nếu bấm các phím di chuyển **lên/xuống/trái/phải**, bộ gõ **tự copy chữ và chèn vào một cách vô tội vạ** dù người dùng không gõ thêm.
- **Firefox thì không bị.**

### 1.3. Ghi chú nền tảng
- Cả 2 bug **mới chỉ lộ trên Linux**, chưa thử lại trên Windows.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

Cả hai lỗi đều xuất phát từ **cùng một lớp vấn đề** trong `src/BambooMintKey.Fcitx5/engine.cpp`: engine dùng **inline preedit** (`setClientPreedit`) nhưng **không commit/khép preedit đúng thời điểm** khi gặp phím không phải chữ, hoặc **commit sai thứ tự**. Trên các widget Chromium/Electron có cơ chế autocomplete/soạn thảo "thông minh" (omnibox, khung chat AI), preedit dở dang bị app xử lý sai → sinh ra hiện tượng tự chèn / tự xóa.

### 2.1. Bug B — phím mũi tên bị forward khi preedit còn dở (ĐÃ XÁC ĐỊNH)

Hàm `keyEvent` (engine.cpp dòng 209–212):

```cpp
const uint32_t unicode = fcitx::Key::keySymToUnicode(sym);
if (unicode == 0) {
    return; // Ký tự không in được -> bỏ qua.
}
```

- Các phím **mũi tên** (Left/Right/Up/Down) có `keySymToUnicode == 0`, nên engine **return sớm** mà:
  1. **Không commit** preedit đang dở (ví dụ `"vi"`).
  2. **Không `filterAndAccept()`** → phím mũi tên được fcitx5 **chuyển tiếp xuống app**.
- Hệ quả: preedit `"vi"` vẫn nằm trong `clientPreedit` (đang "soạn dở" trong ô URL), đồng thời phím mũi tên đi thẳng xuống Chromium. Omnibox nhận phím mũi tên → kích hoạt **điều hướng dropdown autocomplete** → Chromium **commit/điền lại** composition đang dở vào vị trí sai → hiện tượng "tự copy chữ và chèn vô tội vạ".

**Xác minh bằng mã nguồn fcitx5 5.1.7 (bản đang cài):**
- `Instance::postEvent` (instance.cpp) **không tự commit preedit** khi key không được engine chấp nhận — preedit cứ treo, key cứ được forward.
- Nhánh duy nhất có liên quan là `CapabilityFlag::KeyEventOrderFix`, nhưng nhánh đó chỉ chạy khi có `blockedEvents_` (không xảy ra khi gõ thường).

**Vì sao Firefox không bị:** thanh URL Firefox không có autocomplete-dropdown phản ứng với mũi tên kiểu omnibox, và xử lý IME composition cẩn thận hơn.

### 2.2. Bug A — thứ tự commit bị ngược trong `commitText` (GIẢ THUYẾT MẠNH, cần xác minh runtime)

Hàm `commitText` (engine.cpp dòng 321–330):

```cpp
const char *text = bmk_get_commit_text(state->handle());
ic->inputPanel().reset();       // (1) xóa preedit
ic->updatePreedit();            // (2) gửi preedit RỖNG xuống app
ic->updateUserInterface(...);
if (text && text[0] != '\0') {
    ic->commitString(text);     // (3) rồi MỚI commit "a"
}
```

Thứ tự ở đây là **ngược** so với chuẩn: gửi "preedit rỗng" (kết thúc composition) **trước**, rồi mới `commitString("a")` **sau**.

Chuẩn đúng (tham chiếu `fcitx5-bamboo`, đường gõ thường trong `bamboo.cpp`):

```cpp
if (commit) ic_->commitString(commit);  // commit trước
ic_->inputPanel().reset();               // rồi mới xóa panel
ic_->updatePreedit();                    // rồi mới cập nhật preedit
```

Với một editor Chromium/Electron "khó tính" (khung chat AI), nhận `preedit=""` (composition end) **rồi mới** nhận `commit "a"` khiến app tưởng composition đã kết thúc → xử lý lệnh commit lạc lối thành **xóa/hủy** chữ vừa gõ.

Điều này cũng lý giải **"bắt buộc space 1 lần trước"**: cú `Space` đầu (preedit trống → `ActionPassThrough`) được **forward** xuống app, giúp editor "khởi tạo" trạng thái IME; các lần commit sau mới chuẩn.

**Cơ chế bổ sung (khác biệt Linux/Windows):** trong `001_Init.md` có ghi hành vi chuẩn TSF (Windows) là sau khi commit từ, **space được nhả lại cho app** (`pfEaten = FALSE`) → kết quả `"việt "` (có dấu cách). Trên Linux, `commitText` gọi `keyEvent.filterAndAccept()` → **nuốt luôn space**. Đây là nguồn gốc khác biệt "chỉ lộ trên Linux".

### 2.3. Vì sao VS Code / Firefox không bị

| Ứng dụng | Lý do |
| :--- | :--- |
| VS Code (Electron) | Dùng Monaco — xử lý IME được kiểm chứng kỹ, chịu được preedit dở và thứ tự commit hơi lệch. |
| Firefox | Xử lý IME composition đúng chuẩn; thanh URL không autocomplete theo mũi tên. |
| Antigravity chat / Chromium omnibox | Widget "thông minh" (autocomplete/suggestion) → nhạy cảm với preedit dở và thứ tự commit sai. |

---

## 3. Phương án xử lý (đề xuất, chưa sửa code)

### Phương án 1 (khuyên dùng) — Commit preedit trước khi nhả phím không xử lý (fix Bug B)

Trong `keyEvent`, trước khi `return` ở nhánh `unicode == 0` (và cả nhánh `isSystemModifier`), chủ động commit preedit dở dang (tái dùng `flushPendingComposition`) để "khép" composition, rồi mới nhả phím xuống app.

```cpp
if (unicode == 0) {
    flushPendingComposition(ic, state); // commit phần dở trước khi forward mũi tên
    return;
}
```

### Phương án 2 (khuyên dùng) — Sửa thứ tự commit trong `commitText` (fix Bug A)

Đổi `commitText` thành **commit trước, reset/updatePreedit sau** (theo chuẩn fcitx5-bamboo):

```cpp
void BambooMintKeyEngine::commitText(fcitx::InputContext *ic,
                                     BambooMintKeyState *state) {
    const char *text = bmk_get_commit_text(state->handle());
    if (text && text[0] != '\0') {
        ic->commitString(text);   // commit trước
    }
    ic->inputPanel().reset();     // rồi mới xóa panel
    ic->updatePreedit();
    ic->updateUserInterface(fcitx::UserInterfaceComponent::InputPanel);
}
```

### Phương án 3 (tùy chọn) — Nhả `Space` cho app để khớp hành vi Windows

Cân nhắc **không `filterAndAccept()`** phím `Space` sau khi commit, để app nhận thêm dấu cách (khớp TSF Windows `pfEaten = FALSE`). Cần đánh giá kỹ vì có thể đổi hành vi hiện tại trên các app đã hoạt động đúng.

---

## 4. Môi trường tái hiện

- **Ứng dụng gặp lỗi:** Antigravity IDE (khung chat AI); Opera, Chrome (thanh URL / omnibox).
- **Bố cục bàn phím:** Telex (V/E).
- **Phiên bản BambooMintKey:** hiện tại (Linux Fcitx 5 addon).
- **Phiên bản Fcitx5:** 5.1.7.
- **Hệ điều hành:** Linux (chưa thử lại trên Windows).

## 5. Các bước tái hiện

**Bug A (Antigravity):**
1. Mở Antigravity IDE → khung chat với AI.
2. Bật BambooMintKey (V).
3. Gõ chữ đầu tiên (vd `a`), rồi bấm `Space`.
4. Quan sát: chữ `a` bị xóa thay vì được chốt.
5. Thử lại: bấm `Space` trước (preedit trống), rồi gõ `a` + `Space` → gõ được.

**Bug B (Chromium URL bar):**
1. Mở Opera/Chrome.
2. Click vào thanh URL.
3. Gõ vài chữ chưa kết thúc (vd `vi`) để preedit đang hiển thị.
4. Bấm phím mũi tên ↑/↓/←/→.
5. Quan sát: chữ bị copy-chèn vô tội vạ.

## 6. Action Items

- [ ] Xác minh giả thuyết Bug A bằng log runtime (`FCITX_DEBUG`, key_trace) trên Antigravity.
- [ ] Áp dụng Phương án 1 (commit trước khi forward phím mũi tên).
- [ ] Áp dụng Phương án 2 (sửa thứ tự commit trong `commitText`).
- [ ] Đánh giá Phương án 3 (nhả Space cho app) — chỉ làm nếu cần khớp Windows.
- [ ] Test hồi quy ma trận ứng dụng Linux: Kate, Firefox, LibreOffice, Konsole, VS Code, Chromium/Opera, Antigravity, Zed, Steam.
- [ ] Kiểm chứng lại trên Windows (TSF) để xác nhận không hồi quy.

---

## Lưu ý

| Trường | Tại sao cần |
|--------|-------------|
| **Chuỗi phím đã gõ** | Để tái hiện chính xác từng phím. |
| **Kết quả mong đợi** | Xác định hành vi đúng theo chuẩn IME. |
| **Kết quả thực tế** | Xác định lỗi preedit/commit đang trả về. |
| **Ứng dụng** | Một số lỗi chỉ xảy ra ở widget Chromium/Electron nhất định. |
| **Phiên bản** | Giúp xác định lỗi đã được sửa ở bản mới chưa. |
