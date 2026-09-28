# BambooMintKey - Flatpak Extension cho Fcitx 5 (`org.fcitx.Fcitx5.Addon.BambooMintKey`)

Thư mục này chứa toàn bộ định nghĩa manifest để phát hành bộ gõ **BambooMintKey** dưới dạng **Flatpak Extension** cho Fcitx 5 trên **Flathub**, phục vụ trực tiếp cho người dùng **Steam Deck (SteamOS)** và mọi hệ thống Linux sử dụng Fcitx5 Flatpak.

Thư mục này đóng vai trò tương tự như thư mục `manifests/l/LMO-LAB/` (dành cho WinGet).

---

## 1. Cấu trúc tệp tin

| Tệp tin | Vai trò |
|---|---|
| `org.fcitx.Fcitx5.Addon.BambooMintKey.yaml` | Manifest chính cho `flatpak-builder`, định nghĩa quy trình build NativeAOT & CMake. |
| `org.fcitx.Fcitx5.Addon.BambooMintKey.metainfo.xml` | AppStream metadata chuẩn XDG hiển thị thông tin, icon, tính năng trên **KDE Discover** của Steam Deck. |
| `flathub.json` | Cấu hình cho Flathub Build Bot. |
| `README.md` | Tài liệu hướng dẫn quy trình phát hành. |

*Lưu ý: Script thực thi đóng gói và kiểm thử được đặt tại `scripts/linux/package_flatpak.sh` (đồng bộ cùng `scripts/linux/package_linux.sh`).*

---

## 2. Tính độc lập & Không ảnh hưởng mã nguồn chính

- **Zero Source Impact:** Không sửa đổi bất kỳ tệp F#, C#, C++ hay CMake nào trong `src/`.
- **Độc lập nền tảng:** Quá trình build của Windows (TSF), Linux Native (.deb/.rpm) và macOS trong tương lai hoàn toàn không bị ảnh hưởng.
- **Mô hình Downstream Recipe:** Flatpak manifest chỉ đóng vai trò công thức kéo mã nguồn đã gắn tag release (`v1.1.0`, `v1.2.0`, ...) từ GitHub về và biên dịch trong môi trường sandbox của Flatpak.

---

## 3. Quy trình nộp duyệt lên Flathub (Flathub Submission)

**Bạn không cần tạo thêm repository riêng nào trên tài khoản cá nhân.** Flathub sẽ tự động tạo repository riêng cho bạn trên tổ chức của họ sau khi PR được duyệt:

### Bước 1: Mở PR tại kho Flathub
1. Fork kho [flathub/flathub](https://github.com/flathub/flathub).
2. Tạo một nhánh mới (ví dụ: `add-bamboomintkey-addon`).
3. Đưa các tệp trong thư mục `manifests/flatpak/` vào PR theo hướng dẫn tại [Flathub New App Submission](https://docs.flathub.org/docs/for-app-authors/submission/).
4. Flathub Bot sẽ tự động chạy linter và kiểm thử build.

### Bước 2: Nhận quyền quản trị tự động
Sau khi PR được duyệt và merge:
- Flathub sẽ tự động khởi tạo repository chính thức: `https://github.com/flathub/org.fcitx.Fcitx5.Addon.BambooMintKey`.
- Flathub tự động mời tài khoản GitHub của bạn (`thatislg`) làm Maintainer của repo đó.

---

## 4. Quy trình cập nhật phiên bản định kỳ (Release Maintenance)

Mỗi khi repository chính `thatislg/BambooMintKey` ra mắt phiên bản mới (ví dụ `v1.2.0`):
1. Cập nhật trường `tag` và `commit` trong `org.fcitx.Fcitx5.Addon.BambooMintKey.yaml`.
2. Bổ sung `<release version="1.2.0" ...>` vào `org.fcitx.Fcitx5.Addon.BambooMintKey.metainfo.xml`.
3. Đẩy commit lên repository của Flathub. Flathub build bot sẽ tự động biên dịch và phân phối tới tất cả người dùng Steam Deck qua Discover Store.

---

## 5. Kiểm thử cục bộ (Local Testing)

Để kiểm thử build Flatpak extension cục bộ trên máy phát triển:
```bash
# Build artifact vào delivery/flatpak/
./scripts/linux/package_flatpak.sh

# Hoặc build và cài thử nghiệm trực tiếp vào Fcitx5 Flatpak trên máy:
./scripts/linux/package_flatpak.sh --install
```
