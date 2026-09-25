// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BambooMintKey.Core.Native;

/// <summary>
/// C-ABI exports của libBambooMintKeyCore.so.
///
/// M1.1 chỉ khởi tạo khung dự án và xác minh pipeline NativeAOT sinh ra `.so`.
/// Hàm bên dưới là export tối thiểu để xác nhận C-ABI hoạt động; toàn bộ giao diện
/// đầy đủ (context lifecycle, xử lý phím, trích xuất buffer, cấu hình) sẽ được
/// triển khai ở M1.2 -&gt; M1.6 theo 007_03_CoreNative_CABI_Design.md.
///
/// Quy ước gọi chuẩn C (cdecl) theo thiết kế.
/// </summary>
public static unsafe class Exports
{
    /// <summary>
    /// Trả về phiên bản ABI (đặt chỗ cho M1.1) để xác nhận thư viện được tải và gọi đúng.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_version", CallConvs = [typeof(CallConvCdecl)])]
    public static int Version()
    {
        return 1;
    }
}
