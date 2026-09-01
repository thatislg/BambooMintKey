# 1. Khởi tạo dự án 

- Truy cập vào Powershell di chuyển tới thư mục BambooMintKey

```powershell
# 1. Tạo Solution
dotnet new sln -n BambooMintKey

# 2. Tạo các Project trong src/ và tests/
dotnet new classlib -lang "F#" -o src/BambooMintKey.Core -n BambooMintKey.Core
dotnet new classlib -lang "C#" -o src/BambooMintKey.NativeBridge -n BambooMintKey.NativeBridge
dotnet new classlib -lang "F#" -o src/BambooMintKey.Shared -n BambooMintKey.Shared
dotnet new avalonia.app -lang "F#" -o src/BambooMintKey.UI -n BambooMintKey.UI
dotnet new classlib -lang "F#" -o tests/BambooMintKey.Core.Tests -n BambooMintKey.Core.Tests

# 3. Thêm các project vào Solution
dotnet sln add src/BambooMintKey.Core/BambooMintKey.Core.fsproj
dotnet sln add src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj
dotnet sln add src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj
dotnet sln add src/BambooMintKey.UI/BambooMintKey.UI.fsproj
dotnet sln add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj

# 4. Thiết lập liên kết tham chiếu (References)
dotnet add src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj reference src/BambooMintKey.Core/BambooMintKey.Core.fsproj

dotnet add src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj

dotnet add src/BambooMintKey.Core/BambooMintKey.Core.fsproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj

dotnet add src/BambooMintKey.UI/BambooMintKey.UI.fsproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj

dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj reference src/BambooMintKey.Core/BambooMintKey.Core.fsproj

dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj
```

