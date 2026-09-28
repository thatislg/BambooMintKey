<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Hướng Dẫn Cài Đặt BambooMintKey (Flatpak Extension cho Fcitx5)

Tài liệu này dành cho **người dùng cuối**, đi kèm file `org.fcitx.Fcitx5.Addon.BambooMintKey.flatpak`.

---

## 1. File này là gì

- `org.fcitx.Fcitx5.Addon.BambooMintKey.flatpak` — bộ gõ tiếng Việt **BambooMintKey** (Telex) dạng **Flatpak extension** cho Fcitx5.

---

## 2. Yêu cầu

| Yêu cầu | Ghi chú |
| :--- | :--- |
| Flatpak đã cài | `flatpak --version` |
| Fcitx5 Flatpak | `org.fcitx.Fcitx5` (cài từ Flathub) |
| Hệ điều hành dùng app Flatpak | SteamOS, Bazzite, Fedora Silverblue/Kinoite, ChimeraOS... |

> ⚠️ **Quan trọng:** bản Flatpak chỉ phát huy tốt trên hệ điều hành mà **ứng dụng cũng cài dạng Flatpak** (Steam Deck / Bazzite...). Trên Linux Mint/Ubuntu thường (app native), hãy dùng bản Native (gói `.deb`/`.rpm`) thay vì Flatpak.

---

## 3. Các bước cài đặt

### Bước 1 — Cài Fcitx5 Flatpak (nếu chưa có)

```bash
flatpak install --user flathub org.fcitx.Fcitx5
```

### Bước 2 — Cài bundle BambooMintKey

```bash
flatpak install --user ./org.fcitx.Fcitx5.Addon.BambooMintKey.flatpak
```

### Bước 3 — Khởi động lại Fcitx5 để nạp addon

```bash
flatpak kill org.fcitx.Fcitx5 2>/dev/null || true
flatpak run org.fcitx.Fcitx5 -d &
```

### Bước 4 — Thêm bộ gõ BambooMintKey

```bash
flatpak run --command=fcitx5-configtool org.fcitx.Fcitx5
```

Trong cửa sổ cấu hình, tìm **BambooMintKey** (mục tiếng Việt, `LangCode=vi`) rồi **thêm vào danh sách Input Method** hiện tại.

---

## 4. Sử dụng

| Thao tác | Cách làm |
| :--- | :--- |
| Chuyển sang BambooMintKey | Nhấn phím tắt trigger của Fcitx5 (thường `Ctrl+Space` hoặc `Super+Space`) |
| Gõ tiếng Việt Telex | `duowngf` + `space` → **"đường"** |
| Đổi chế độ V/E | Bấm phím `` ` `` (grave, dưới Esc) để chuyển Việt ↔ Anh |

---

## 5. Gỡ cài đặt

```bash
flatpak uninstall --user org.fcitx.Fcitx5.Addon.BambooMintKey
```
