<!--
  BambooMintKey - Vietnamese Telex Input Method Editor
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue 010: Khắc phục lỗi gói cài đặt Linux (RPM/DEB) bị Not Available do hardcoded DT_NEEDED và sai thư mục /usr/lib64

**Mã tài liệu:** `010_LinuxPackaging_NotAvailable_Fix`

**Trạng thái:** ✅ Đã giải quyết — Đã đóng gói và xác nhận hoạt động trên Fedora VM (Commit `e666af7`)

**Mức độ nghiêm trọng:** Cao (Bộ gõ không thể khởi động trên bất kỳ máy nào khác ngoài máy build ban đầu).

---

## 1. Mô tả hiện tượng

Khi người dùng build gói cài đặt Linux (`.rpm` hoặc `.deb`) bằng script `package_linux.sh` trên máy host (Ubuntu/Debian) rồi chuyển sang cài đặt trên máy ảo hoặc máy khách (Fedora 64-bit hoặc bản phân phối Ubuntu/Debian khác):

1. **Trạng thái cài đặt:** Trình quản lý gói (`dnf` hoặc `dpkg`) báo cài đặt thành công 100%.
2. **Trạng thái cấu hình:** Mở `fcitx5-configtool` thấy bộ gõ `BambooMintKey` xuất hiện trong danh sách.
3. **Hiện tượng lỗi:** Khi thêm vào danh sách bộ gõ hoạt động và chuyển sang gõ, bộ gõ rơi vào trạng thái **"Not Available"** (Không khả dụng). Người dùng không thể kích hoạt để gõ tiếng Việt.
4. **Log lỗi verbose:** Khi kiểm tra bằng lệnh `fcitx5 -v` hoặc `ldd -r /usr/lib64/fcitx5/libbamboomintkey.so`, hệ thống xuất hiện thông báo lỗi:
   ```text
   /home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/publish/linux-x64/BambooMintKeyCore.so: not found
   ```
   Toàn bộ đường dẫn tuyệt đối của máy host build bị lộ vào binary và dynamic linker đòi nạp file từ đường dẫn đó thay vì thư mục hệ thống.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Cause Analysis)

### 2.1. GNU Linker tự động hardcode absolute path do thiếu `SONAME`
- **Cơ chế:** Khi .NET NativeAOT biên dịch thư viện C# thành `BambooMintKeyCore.so`, mặc định NativeAOT trên Linux không gán cờ `SONAME` vào header ELF.
- **Điểm phát sinh lỗi trong CMake:** Trong `src/BambooMintKey.Fcitx5/CMakeLists.txt`, target imported `bamboomintkey_core` chỉ nhận thuộc tính `IMPORTED_LOCATION "${BAMBOOMINTKEY_CORE_SO}"` (đường dẫn tuyệt đối trong thư mục `publish/`).
- **Quy tắc của GNU Linker (`ld`):** Khi liên kết `libbamboomintkey.so` với một shared library thiếu `SONAME`, linker của Linux sẽ tự động **nhúng toàn bộ đường dẫn tuyệt đối lúc build** vào bảng phụ thuộc `DT_NEEDED` của file gọi nó.
- **Hệ quả:**
  - File `libbamboomintkey.so` có `DT_NEEDED: /home/lmo1720/.../BambooMintKeyCore.so`.
  - Trên chính máy build, file này tồn tại nên gói `.deb` test trên máy build vẫn chạy được.
  - Nhưng trên máy ảo Fedora hoặc bất kỳ máy khách nào khác, đường dẫn trên không tồn tại, khiến `dlopen()` thất bại hoàn toàn.

### 2.2. Lệch đường dẫn thư viện 64-bit giữa Debian và Fedora (`/usr/lib` vs `/usr/lib64`)
- **Cơ chế:** Khi chạy `rpmbuild` trên môi trường Ubuntu/Debian, bộ macro của RPM trên Debian mặc định gán `%{_libdir}` thành `/usr/lib` (Debian không dùng `/usr/lib64`).
- **Hệ quả:** Gói `.rpm` đóng gói file addon vào `/usr/lib/fcitx5/`. Tuy nhiên, trên Fedora x86_64, Fcitx5 64-bit chỉ tìm kiếm plugin tại `/usr/lib64/fcitx5/` (thư mục `/usr/lib` dành cho ứng dụng 32-bit). Người dùng kiểm tra `/usr/lib64/fcitx5/` không thấy file `.so`.

### 2.3. Ràng buộc phiên bản Fcitx5 cứng (`0=core:5.1.7`)
- **Cơ chế:** File cấu hình `bamboomintkey-addon.conf` có dòng `0=core:5.1.7` (phiên bản trên Ubuntu 24.04).
- **Hệ quả:** Fcitx5 so sánh phiên bản core của máy khách, nếu thấp hơn 5.1.7 (như trên Debian 12, Ubuntu 22.04 LTS, hoặc một số bản Fedora), Fcitx5 sẽ từ chối nạp addon và đánh dấu "Not Available".

### 2.4. Thiếu icon ứng dụng Settings trong spec RPM
- File `bamboomintkey-settings.desktop` khai báo `Icon=bamboomintkey`, nhưng `bamboomintkey.spec` trước đó chỉ cài 2 icon trạng thái Fcitx (`fcitx_bamboomintkey*.svg`) mà bỏ sót `bamboomintkey.svg`.

---

## 3. Các giải pháp đã triển khai (Implemented Solutions)

| Thành phần | File mã nguồn | Thay đổi kỹ thuật |
| :--- | :--- | :--- |
| **NativeAOT C#** | [BambooMintKey.Core.Native.csproj](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Core.Native/BambooMintKey.Core.Native.csproj) | Thêm `<LinkerArg Include="-Wl,-soname,BambooMintKeyCore.so" />` cho platform Linux để NativeAOT nhúng đúng SONAME. |
| **CMake Build** | [src/BambooMintKey.Fcitx5/CMakeLists.txt](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Fcitx5/CMakeLists.txt) | Gán `IMPORTED_SONAME "BambooMintKeyCore.so"` cho `bamboomintkey_core`. Linker chỉ ghi tên file và tìm qua `INSTALL_RPATH "$ORIGIN"`. |
| **Addon Conf** | [src/BambooMintKey.Fcitx5/bamboomintkey-addon.conf.in](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/src/BambooMintKey.Fcitx5/bamboomintkey-addon.conf.in) | Đổi `0=core:5.1.7` thành `0=core` để tương thích mọi phiên bản Fcitx5 trên các bản phân phối Linux. |
| **Packaging RPM** | [scripts/package_linux.sh](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/scripts/package_linux.sh) | Thêm `--define "_lib lib64" --define "_libdir /usr/lib64"` khi gọi `rpmbuild`; copy bổ sung `bamboomintkey.svg` vào SOURCES. |
| **RPM Spec** | [scripts/bamboomintkey.spec](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/scripts/bamboomintkey.spec) | Cài đặt và đóng gói `bamboomintkey.svg`; bổ sung cập nhật cache icon và desktop trong `%post` và `%postun`. |
| **Gỡ cài đặt** | [scripts/uninstall_linux.sh](file:///home/lmo1720/Fcitx-Bamboo-Mint/BambooMintKey/scripts/uninstall_linux.sh) | Bổ sung dọn dẹp sạch sẽ các đường dẫn `/usr/lib64/` và `/usr/bin/` cho Fedora/RHEL. |

---

## 4. Kết quả kiểm chứng (Verification)

1. **Xác thực ELF Header:**
   ```bash
   $ readelf -d publish/linux-x64/BambooMintKeyCore.so | grep -i soname
    0x000000000000000e (SONAME)             Library soname: [BambooMintKeyCore.so]

   $ readelf -d delivery/linux/stage/usr/lib/*/fcitx5/libbamboomintkey.so | grep -i BambooMintKeyCore
    0x0000000000000001 (NEEDED)             Shared library: [BambooMintKeyCore.so]
   ```
   *Kết quả:* Không còn bất kỳ đường dẫn tuyệt đối nào bị ghi vào binary. Khi chạy, `libbamboomintkey.so` tự động tìm `BambooMintKeyCore.so` cùng thư mục thông qua `$ORIGIN`.

2. **Xác thực trên máy ảo Fedora:**
   * Cài đặt gói `bamboomintkey-1.1.0-1.x86_64.rpm` vào hệ thống.
   * Tất cả thư viện nằm đúng tại `/usr/lib64/fcitx5/`.
   * Khởi động lại Fcitx5 (`fcitx5 -r -d`), bộ gõ `BambooMintKey` lập tức chuyển sang trạng thái **Available**.
   * Bộ gõ hoạt động ổn định, gõ tiếng Việt Telex chính xác.
   * Icon của BambooMintKey Settings hiển thị đầy đủ trên Application Menu.
