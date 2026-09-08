# Hướng dẫn Submit BambooMintKey lên WinGet (Thủ công)

Tài liệu này hướng dẫn cách đưa bộ cài đặt `BambooMintKey-Setup.exe` lên kho gói chính thức `microsoft/winget-pkgs` bằng cách tạo Pull Request thủ công.

> **Mục đích:** Khi PR được merge, người dùng trên toàn thế giới có thể cài BambooMintKey bằng một dòng lệnh:
> ```powershell
> winget install BambooMintKey.BambooMintKey
> ```

---

## 0. Điều kiện tiên quyết

Trước khi bắt đầu, bạn cần có:

1. **GitHub account** (ví dụ: `thatislg`).
2. **Git** đã cài trên máy.
3. **WinGet (Windows Package Manager)** đã cài trên Windows 10/11 để kiểm thử.
4. **Bản Release trên GitHub** đã public với asset `BambooMintKey-Setup.exe`.
5. **SHA-256 của asset** khớp với giá trị trong file `BambooMintKey.BambooMintKey.installer.yaml`.

> **Tại sao cần SHA-256 khớp?** WinGet dùng SHA-256 để xác minh file tải về không bị sửa đổi. Nếu sai, lệnh `winget install` sẽ báo lỗi bảo mật và bot của Microsoft sẽ từ chối PR.

---

## 1. Tại sao phải fork `microsoft/winget-pkgs`?

`microsoft/winget-pkgs` là kho lưu trữ **chỉ đọc đối với người ngoài**. Microsoft không cho phép push trực tiếp. Thay vào đó, cơ chế GitHub Pull Request yêu cầu:

- Bạn **fork** repo về tài khoản của mình.
- Bạn chỉnh sửa trên fork.
- Bạn tạo PR từ fork về repo gốc.
- Microsoft review và merge.

> **Tương tự như:** Bạn không thể sửa trực tiếp bài viết trên Wikipedia, mà phải đề xuất sửa đổi để admin duyệt.

### Thao tác

Truy cập: https://github.com/microsoft/winget-pkgs

Nhấn nút **Fork** ở góc phải trên. Giữ mặc định, tạo fork về tài khoản `thatislg/winget-pkgs`.

---

## 2. Clone fork về máy local

Sau khi fork xong, bạn cần clone fork để chỉnh sửa file.

```powershell
git clone https://github.com/thatislg/winget-pkgs.git
cd winget-pkgs
```

> **Tại sao không upload file trực tiếp trên web?** Có thể, nhưng việc clone về local cho phép bạn chạy `winget validate` để kiểm tra lỗi trước khi push. Nếu đẩy lên web trước, bot sẽ báo lỗi sau, tốn thời gian chờ.

---

## 3. Tạo nhánh mới

Bạn không nên chỉnh sửa trên nhánh `master` của fork vì:

- Nhánh `master` của fork cần đồng bộ với `microsoft/winget-pkgs` để sau này dùng tiếp.
- Mỗi PR nên đến từ một nhánh riêng, rõ ràng.

```powershell
git checkout -b BambooMintKey-1.0.0
```

> **Quy ước đặt tên:** Microsoft khuyến khích tên nhánh mô tả nội dung PR, ví dụ `BambooMintKey-1.0.0` hoặc `New-Package-BambooMintKey`.

---

## 4. Tạo cấu trúc thư mục manifest

WinGet tổ chức manifest theo quy tắc phân cấp dựa trên `PackageIdentifier`:

```text
manifests/
└── b/                              # Chữ cái đầu của Publisher
    └── BambooMintKey/              # Tên Publisher
        └── BambooMintKey/          # Tên sản phẩm
            └── 1.0.0/              # Phiên bản
                ├── BambooMintKey.BambooMintKey.yaml
                ├── BambooMintKey.BambooMintKey.installer.yaml
                └── BambooMintKey.BambooMintKey.locale.en-US.yaml
```

> **Tại sao phải đúng cấu trúc này?** Bot của Microsoft tự động quét thư mục theo đúng quy tắc trên. Nếu sai vị trí, PR sẽ bị đóng ngay lập tức vì lỗi `Manifest-Metadata-Consistency`.

### Thao tác

```powershell
# Tạo thư mục theo quy tắc của WinGet
New-Item -ItemType Directory -Path "manifests\b\BambooMintKey\BambooMintKey\1.0.0" -Force

# Copy 3 file manifest từ repo BambooMintKey sang
Copy-Item -Path "D:\Kojin\BambooMintKey\delivery\winget\1.0.0\*" `
          -Destination "manifests\b\BambooMintKey\BambooMintKey\1.0.0\" `
          -Force
```

---

## 5. Kiểm tra manifest trước khi submit

Đây là bước **quan trọng nhất**. Nếu manifest có lỗi, bot của Microsoft sẽ báo và bạn phải sửa, push lại, chờ lại.

```powershell
winget validate manifests\b\BambooMintKey\BambooMintKey\1.0.0\
```

Kết quả mong đợi:
```text
Manifest validation succeeded.
```

> **Tại sao cần validate?** `winget validate` kiểm tra:
> - Schema YAML có đúng không.
> - `PackageIdentifier` có khớp với đường dẫn thư mục không.
> - `InstallerSha256` có đúng định dạng 64 ký tự hex không.
> - Các giá trị bắt buộc có đầy đủ không.
> Nếu không validate, bot sẽ từ chối PR ngay.

Ngoài ra, bạn nên kiểm tra URL tải về hoạt động:

```powershell
# Kiểm tra file có tồn tại trên Release không
$uri = "https://github.com/thatislg/BambooMintKey/releases/download/v1.0.0/BambooMintKey-Setup.exe"
try {
    $response = Invoke-WebRequest -Uri $uri -Method Head -UseBasicParsing -ErrorAction Stop
    Write-Host "URL OK - Status $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "URL FAILED: $_" -ForegroundColor Red
}
```

> **Lưu ý:** Nếu Release đang ở chế độ `draft`, URL sẽ trả về 404 đối với người dùng không đăng nhập. WinGet bot cũng sẽ không tải được.

---

## 6. Commit và push lên fork

Sau khi validate OK, bạn commit các file manifest.

```powershell
git add manifests/b/BambooMintKey/BambooMintKey/1.0.0/
git commit -m "Add BambooMintKey.BambooMintKey version 1.0.0"
git push origin BambooMintKey-1.0.0
```

> **Tại sao chỉ commit thư mục manifest?** PR chỉ nên chứa đúng các file cần thiết. Không commit file cá nhân, log, hoặc thay đổi không liên quan.

---

## 7. Tạo Pull Request trên GitHub

1. Truy cập fork: `https://github.com/thatislg/winget-pkgs`
2. GitHub sẽ hiển thị banner "Compare & pull request" cho nhánh `BambooMintKey-1.0.0`. Nhấn vào đó.
3. Base repository chọn `microsoft/winget-pkgs`, base chọn `master`.
4. Điền tiêu đề PR theo mẫu:
   ```
   New version: BambooMintKey.BambooMintKey version 1.0.0
   ```
5. Trong phần mô tả, có thể ghi ngắn gọn:
   ```
   - Add BambooMintKey.BambooMintKey version 1.0.0
   - Installer type: Inno Setup (x64)
   - Silent switches: /VERYSILENT /NORESTART
   ```
6. Nhấn **Create pull request**.

> **Tại sao tiêu đề phải theo mẫu?** Bot `wingetbot` của Microsoft tự động phân loại PR dựa trên tiêu đề. Sai mẫu có thể bị đóng hoặc yêu cầu sửa.

---

## 8. Chờ bot kiểm tra và xử lý lỗi

Sau khi tạo PR, các bot sau sẽ chạy:

| Bot/Công cụ | Công việc |
|---|---|
| `wingetbot` | Kiểm tra metadata, hash, URL, license |
| `Azure Pipelines` | Build và test cài đặt thực tế trong máy ảo |
| `microsoft/winget-pkgs` moderators | Review thủ công nếu có cảnh báo |

Quy trình kiểm tra tự động gồm:

1. **Kiểm tra URL và SHA-256:** Bot tải file từ `InstallerUrl` và đối chiếu hash.
2. **Kiểm tra cài đặt ngầm:** Chạy `BambooMintKey-Setup.exe /VERYSILENT /NORESTART` trong sandbox. Nếu treo hoặc hiện dialog, PR fail.
3. **Kiểm tra ProductCode:** Sau khi cài, bot kiểm tra Registry xem có `{D8A27E4B-4E3F-4A92-805F-294FCE314D01}_is1` không.
4. **Kiểm tra gỡ cài đặt:** Chạy `unins000.exe /SILENT`, xác nhận không còn file trong `Program Files`.
5. **Quét bảo mật:** Windows Defender / SmartScreen / VirusTotal.

> **Tại sao quá trình này mất 24–48 giờ?** Số lượng PR rất lớn, bot chạy tuần tự, và có thể cần moderator duyệt thủ công.

---

## 9. Xử lý SmartScreen warning (trường hợp installer chưa ký số)

Nếu BambooMintKey-Setup.exe **chưa được ký số** bằng chứng chỉ hợp lệ, bot sẽ báo cảnh báo SmartScreen.

Biểu hiện:
- PR bị gắn nhãn `Validation-Installation-Error` hoặc `SmartScreen`.
- Moderator yêu cầu giải thích.

Cách xử lý:

1. **Giải thích trên PR:** Nêu rõ đây là phần mềm open-source MIT, link về repo GitHub, và cam kết sẽ ký số ở các phiên bản sau.
2. **Cung cấp bằng chứng:** Link đến source code, release note, giấy phép MIT.
3. **Mua chứng chỉ code signing (nếu muốn nhanh):**
   - Chứng chỉ cá nhân từ các nhà cung cấp như Sectigo, DigiCert, SSL.com.
   - Chi phí khoảng $80–$300/năm.
   - Sau khi có chứng chỉ, cập nhật workflow GitHub Actions để ký installer trước khi release.

> **Tại sao SmartScreen lại quan trọng?** WinGet là kênh phân phối chính thức của Microsoft. Microsoft phải đảm bảo các gói không phải malware. File EXE không ký số dễ bị nghi ngờ vì kẻ xấu cũng có thể đóng gói phần mềm độc hại.

---

## 10. Sau khi PR được merge

Khi PR merge, gói sẽ xuất hiện trong WinGet sau vài giờ. Bạn có thể kiểm tra:

```powershell
winget search BambooMintKey
```

Kết quả mong đợi:
```text
Name           Id                        Version Source
---------------------------------------------------------
BambooMintKey  BambooMintKey.BambooMintKey 1.0.0   winget
```

Người dùng cài đặt bằng:
```powershell
winget install BambooMintKey.BambooMintKey
```

---

## 11. Cập nhật phiên bản sau này

Khi có phiên bản mới (ví dụ `v1.1.0`):

1. Build installer mới.
2. Tạo GitHub Release `v1.1.0` với asset `BambooMintKey-Setup.exe`.
3. Lấy SHA-256 mới.
4. Chạy script trong repo BambooMintKey:
   ```powershell
   .\scripts\update-winget-manifest.ps1 `
     -Version "1.1.0" `
     -InstallerUrl "https://github.com/thatislg/BambooMintKey/releases/download/v1.1.0/BambooMintKey-Setup.exe" `
     -InstallerSha256 "<sha256-mới>"
   ```
5. Copy các file manifest mới từ `delivery/winget/1.1.0/` (hoặc `manifests/...`) sang fork `winget-pkgs`.
6. Tạo PR mới với tiêu đề: `New version: BambooMintKey.BambooMintKey version 1.1.0`.

---

## Tài liệu tham khảo

- [WinGet Package Manifest Schema](https://aka.ms/winget-manifest.schema)
- [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs)
- [Komac – WinGet manifest updater](https://github.com/russellbanks/Komac)
