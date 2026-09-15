<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Issue : UI vẫn tự khởi động cùng Windows sau khi gỡ cài đặt

**Mã tài liệu:** `007_UIAutostartAfterUninstall`

**Trạng thái:** ✅ Đã sửa (chờ build installer mới)

**Liên quan:** `delivery/installer/installer.iss`

---

## 1. Mô tả lỗi

Sau khi người dùng **gỡ cài đặt** BambooMintKey qua uninstaller, ứng dụng UI vẫn tự động chạy mỗi khi đăng nhập Windows. Mặc dù folder cài đặt đã bị xóa, Windows vẫn thực thi command trong registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BambooMintKey`, dẫn đến lỗi/thông báo không tìm thấy file hoặc UI cũ vẫn khởi động nếu file còn sót.

## 2. Nguyên nhân gốc rễ

`BambooMintKey.UI` ghi registry `HKCU\...\Run\BambooMintKey` khi người dùng bật "Khởi động cùng Windows". Tuy nhiên, file `installer.iss` **không xóa key này trong quá trình uninstall**, nên entry khởi động vẫn còn lại sau khi gỡ cài đặt.

## 3. Môi trường

- **Phiên bản BambooMintKey:** trước commit `fa1a2e2`
- **Phiên bản Windows:** Windows 10/11
- **Cách gỡ:** Uninstaller của Inno Setup

## 4. Các bước tái hiện

1. Cài đặt BambooMintKey.
2. Mở UI → bật "Khởi động cùng Windows".
3. Khởi động lại Windows để xác nhận UI tự chạy.
4. Gỡ cài đặt BambooMintKey qua Settings → Apps → Uninstall.
5. Khởi động lại Windows.
6. Quan sát UI BambooMintKey vẫn cố gắng chạy / hiện lỗi.

## 5. Fix đã thực hiện

Trong `delivery/installer/installer.iss`, thêm section:

```iss
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#MyAppName}"; Flags: uninsdeletevalue
```

Điều này đảm bảo Inno Setup xóa key `Run\BambooMintKey` khi gỡ cài đặt.

## 6. Action Items

- [x] Thêm `[Registry]` section vào `installer.iss` để xóa key Run khi uninstall.
- [ ] Build bộ cài mới và test uninstall trên máy sạch.
- [ ] Xác nhận key `HKCU\...\Run\BambooMintKey` bị xóa sau uninstall.
- [ ] Hướng dẫn người dùng xóa key cũ thủ công nếu đã gỡ bản cũ trước đó:
  ```powershell
  Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "BambooMintKey" -ErrorAction SilentlyContinue
  ```

## 7. Thông tin thêm

- Đây là lỗi phổ biến của các ứng dụng tự ghi `HKCU\Run` nhưng không dọn dẹp khi uninstall.
- Fix này không ảnh hưởng đến chức năng "Khởi động cùng Windows" khi app còn được cài.
