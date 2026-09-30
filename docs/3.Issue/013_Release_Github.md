# Hạng mục cần sửa trong `release.yml`

## Mức cao (nên sửa trước khi phát hành)

1. **Truyền version vào bước Compile Inno Setup.**
   - Lý do: `.iss` chỉ dùng version mặc định 1.1.2 khi không nhận tham số. Workflow hiện gọi ISCC không truyền gì, nên mọi tag đều ra bộ cài ghi 1.1.2. Việc này ảnh hưởng đến Installed Apps, nâng cấp và đối chiếu version với Store hoặc WinGet.
   - Kèm theo: thêm `VersionInfoVersion` vào `.iss` để file EXE mang version trong metadata.

2. **Xử lý bước `git-auto-commit-action`.**
   - Lý do: workflow chạy khi push tag, nên checkout ở trạng thái detached HEAD và không push commit ngược lên được. Bước này nằm trước bước upload Release, nên nó lỗi thì Release cũng không được tạo.
   - Hướng xử lý: checkout `main` riêng cho bước này, hoặc bỏ hẳn vì `komac` đã tự tạo PR manifest ở job sau.

3. **Không cho phép bỏ qua ký số trên tag phát hành.**
   - Lý do: khi thiếu secret chứng chỉ, các bước ký chỉ in "skipping" rồi chạy tiếp, dẫn đến phát hành bản chưa ký. Microsoft Store yêu cầu installer đã ký.
   - Hướng xử lý: nếu thiếu chứng chỉ thì cho workflow lỗi.

4. **Tách bước publish Release và job WinGet.**
   - Lý do: hiện `draft: false` và job WinGet chạy ngay sau đó, nên bản phát hành đã public và PR WinGet đã được gửi trước khi bạn test file do CI build ra. Bản draft thì link tải không công khai, WinGet không tải được nên không thể chỉ đặt draft.
   - Hướng xử lý: tách job WinGet ra workflow chạy tay hoặc thêm bước duyệt (environment), để bạn test xong mới publish và gửi WinGet.

## Mức trung bình

5. **Thêm bước Verify inputs trước khi compile.**
   - Lý do: kiểm tra `BambooMintKey.dll`, `BambooMintKey.UI.exe` và file `.ico` có đủ, để lỗi báo rõ ràng thay vì lỗi khó đọc từ ISCC.

6. **Thêm bước Smoke test sau khi ký installer.**
   - Lý do: cài im lặng lên runner, kiểm tra file đã được chép và `unins000.exe` có mặt, rồi gỡ cài đặt. Bước này đồng thời kiểm tra cờ silent mà Store yêu cầu. Nên thử khởi động UI vài giây, vì `PublishTrimmed` có thể làm app build được nhưng crash khi chạy.

7. **Kiểm tra lại cách cài `komac`.**
   - Lý do: `pip install komac` chưa chắc hoạt động, vì Komac thường được tải binary từ GitHub Releases hoặc dùng action `winget-releaser`. Ngoài ra cần xác nhận tên token và các tham số dòng lệnh vẫn đúng với phiên bản đang dùng.

8. **Ghim phiên bản runner và kiểm tra Inno Setup.**
   - Lý do: `windows-latest` có thể đổi image bất ngờ, làm thay đổi công cụ build và vị trí `ISCC.exe`. Nên dùng `windows-2022` hoặc `windows-2025`, chạy thử một lần để xác nhận ISCC có sẵn, nếu thiếu thì cài thêm bằng Chocolatey.

## Mức thấp

9. **Ghim version Pillow.**
   - Lý do: `pip install Pillow` luôn lấy bản mới nhất, nên icon sinh ra có thể khác giữa các lần build.

10. **Cân nhắc ký cả trình gỡ cài đặt (`unins000.exe`).**
    - Lý do: hiện chỉ ký file cài đặt chính. Muốn ký uninstaller cần thêm cấu hình `SignTool` trong `.iss`. Đây là tùy chọn, chưa chắc Store bắt buộc.

## Ngoài `release.yml`

- `build-native.ps1`: thêm kiểm tra `$LASTEXITCODE` sau `dotnet publish` để báo lỗi sớm và rõ hơn.
- `installer.iss`: thêm `VersionInfoVersion` (đã nêu ở mục 1).
- Repo: đảm bảo `src/media/rendered_v_64x64.png` đã được commit, nếu thiếu thì bước tạo icon sẽ lỗi.
