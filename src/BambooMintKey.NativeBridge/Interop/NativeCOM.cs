// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.NativeBridge.Interop;

/// <summary>
/// Helper thực hiện thủ công các thao tác IUnknown::AddRef / IUnknown::Release
/// trên con trỏ COM thô. Dùng khi cần quản lý vòng đời COM object từ C# mà
/// không thông qua RCW tự động (NativeAOT-safe).
/// Theo thiết kế 002_02_TSF_TextInputProcessor_Lifecycle.md.
/// </summary>
public static unsafe class NativeCom
{
    /// <summary>
    /// Tăng tham chiếu đến COM object bằng cách gọi slot AddRef trong vtable.
    /// </summary>
    /// <param name="punk">Con trỏ đến interface COM (IUnknown*).</param>
    public static uint AddRef(IntPtr punk)
    {
        if (punk == IntPtr.Zero) return 0;

        // VTable nằm ở offset 0 của object; AddRef là slot thứ 2 sau QueryInterface.
        var vtable = *(IntPtr*)punk;
        var pfnAddRef = (delegate* unmanaged[Stdcall]<IntPtr, uint>)(*((IntPtr*)vtable + 1));
        return pfnAddRef(punk);
    }

    /// <summary>
    /// Giảm tham chiếu đến COM object bằng cách gọi slot Release trong vtable.
    /// </summary>
    /// <param name="punk">Con trỏ đến interface COM (IUnknown*).</param>
    public static uint Release(IntPtr punk)
    {
        if (punk == IntPtr.Zero) return 0;

        var vtable = *(IntPtr*)punk;
        var pfnRelease = (delegate* unmanaged[Stdcall]<IntPtr, uint>)(*((IntPtr*)vtable + 2));
        return pfnRelease(punk);
    }
}
