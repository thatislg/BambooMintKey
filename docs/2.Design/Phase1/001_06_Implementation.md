Trình tự triển khai trên JetBrains Rider sẽ đi theo quy tắc cốt lõi của F#: **file nào được định nghĩa trước phải nằm ở trên cùng**, vì compiler F# chỉ đọc code từ trên xuống dưới theo thứ tự khai báo trong file `.fsproj`.

### Bước 1: Cài đặt thư viện kiểm thử (xUnit) cho `BambooMintKey.Core.Tests`

Chạy lệnh cài các package test trong Terminal của Rider:

PowerShell

```
dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj package Microsoft.NET.Test.Sdk
dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj package xunit
dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj package xunit.runner.visualstudio
```

### Bước 2: Tạo và sắp xếp file trong `BambooMintKey.Core`

Tạo các thư mục con `Domain` và `Engine` trong `src/BambooMintKey.Core/`, sau đó tạo các file theo đúng thứ tự phụ thuộc sau:

1. **`Domain/Types.fs`** (Phần 1: Định nghĩa types, action, state)
2. **`Domain/EngineConfig.fs`** (Phần 1: Cấu hình bộ gõ)
3. **`Domain/UnicodeTables.fs`** (Phần 2: Bảng mã và phân loại ký tự)
4. **`Engine/SyllableParser.fs`** (Phần 3: Bộ bóc tách âm tiết)
5. **`Engine/ModifierRules.fs`** (Phần 3: Biến đổi mũ/móc/ngang)
6. **`Engine/ToneRules.fs`** (Phần 3: Quy tắc đặt dấu thanh)
7. **`Engine/WordBuffer.fs`** (Phần 4: Quản lý hoa/thường)
8. **`Engine/TelexEngine.fs`** (Phần 4: State machine chính)

*Mở file `src/BambooMintKey.Core/BambooMintKey.Core.fsproj` kiểm tra, danh sách `<Compile Include="..."/>` phải đúng thứ tự:*

XML

```
<ItemGroup>
  <Compile Include="Domain\Types.fs" />
  <Compile Include="Domain\EngineConfig.fs" />
  <Compile Include="Domain\UnicodeTables.fs" />
  <Compile Include="Engine\SyllableParser.fs" />
  <Compile Include="Engine\ModifierRules.fs" />
  <Compile Include="Engine\ToneRules.fs" />
  <Compile Include="Engine\WordBuffer.fs" />
  <Compile Include="Engine\TelexEngine.fs" />
</ItemGroup>
```

### Bước 3: Tạo và sắp xếp file trong `BambooMintKey.Core.Tests`

Tạo các file test trong `tests/BambooMintKey.Core.Tests/`:

1. **`SimpleTelexTests.fs`**
2. **`TonePlacementTests.fs`**
3. **`RestoreAndUndoTests.fs`**
4. **`EnglishFallbackTests.fs`**

*Kiểm tra file `tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj`:*

XML

```
<ItemGroup>
  <Compile Include="SimpleTelexTests.fs" />
  <Compile Include="TonePlacementTests.fs" />
  <Compile Include="RestoreAndUndoTests.fs" />
  <Compile Include="EnglishFallbackTests.fs" />
</ItemGroup>
```

### Bước 4: Build và Chạy Test trên Rider

1. Nhấn **Ctrl + Shift + B** để Build toàn bộ Solution.
2. Mở cửa sổ **Unit Tests** trong Rider (`Ctrl + Alt + U` hoặc `Alt + 8`).
3. Nhấn **Run All Tests** (hoặc chạy qua Terminal: `dotnet test`).

Khi toàn bộ các test case chuyển sang màu xanh (Passed), Phase 1 được nghiệm thu hoàn chỉnh và sẵn sàng để chuyển tiếp sang Phase 2 (NativeBridge & TSF Integration).

Bạn đã tạo xong các file trong `BambooMintKey.Core` chưa hay cần kiểm tra trước file `.fsproj`?