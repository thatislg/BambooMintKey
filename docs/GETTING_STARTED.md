<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Hướng Dẫn Khởi Tạo Dự Án BambooMintKey

Tài liệu này hướng dẫn cách tạo lại cấu trúc dự án từ đầu bằng `dotnet` CLI. Thông thường bạn không cần chạy lại các lệnh này trừ khi đang setup repository mới.

---

## Yêu Cầu

- .NET SDK 10.0+
- PowerShell 7.6+ (để chạy các script hỗ trợ)

---

## Bước 1: Tạo Solution

```powershell
dotnet new sln -n BambooMintKey
```

## Bước 2: Tạo Các Project

```powershell
# Lõi F#: engine Telex và xử lý ngôn ngữ
dotnet new classlib -lang "F#" -o src/BambooMintKey.Core -n BambooMintKey.Core

# Native Bridge C#: COM server + TSF integration (NativeAOT)
dotnet new classlib -lang "C#" -o src/BambooMintKey.NativeBridge -n BambooMintKey.NativeBridge

# Thư viện dùng chung
dotnet new classlib -lang "F#" -o src/BambooMintKey.Shared -n BambooMintKey.Shared

# UI Avalonia cho cấu hình bộ gõ
dotnet new avalonia.app -lang "F#" -o src/BambooMintKey.UI -n BambooMintKey.UI

# Unit tests cho Core
dotnet new classlib -lang "F#" -o tests/BambooMintKey.Core.Tests -n BambooMintKey.Core.Tests
```

## Bước 3: Thêm Project Vào Solution

```powershell
dotnet sln add src/BambooMintKey.Core/BambooMintKey.Core.fsproj
dotnet sln add src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj
dotnet sln add src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj
dotnet sln add src/BambooMintKey.UI/BambooMintKey.UI.fsproj
dotnet sln add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj
```

## Bước 4: Thiết Lập Tham Chiếu

```powershell
# NativeBridge dùng Core và Shared
dotnet add src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj reference src/BambooMintKey.Core/BambooMintKey.Core.fsproj
dotnet add src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj

# Core dùng Shared
dotnet add src/BambooMintKey.Core/BambooMintKey.Core.fsproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj

# UI dùng Shared
dotnet add src/BambooMintKey.UI/BambooMintKey.UI.fsproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj

# Tests dùng Core và Shared
dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj reference src/BambooMintKey.Core/BambooMintKey.Core.fsproj
dotnet add tests/BambooMintKey.Core.Tests/BambooMintKey.Core.Tests.fsproj reference src/BambooMintKey.Shared/BambooMintKey.Shared.fsproj
```

## Bước 5: Cấu Hình NativeAOT

Trong `src/BambooMintKey.NativeBridge/BambooMintKey.NativeBridge.csproj`, thêm cấu hình để publish thành shared native library:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <NativeLib>Shared</NativeLib>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

## Bước 6: Build Lần Đầu

```powershell
dotnet build BambooMintKey.slnx -c Release
```

---

## Xem Thêm

- [README.md](../README.md) — tổng quan dự án.
- [docs/2.Design/Phase2/](2.Design/Phase2/) — thiết kế chi tiết Phase 2 (TSF/COM).
