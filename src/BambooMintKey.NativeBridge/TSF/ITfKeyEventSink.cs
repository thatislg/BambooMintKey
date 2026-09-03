// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// VTable định nghĩa cho ITfKeyEventSink - đánh chặn phím hệ thống.
/// Theo thiết kế 002_03_KeyEventSink_and_Core_Interop.md.
/// Lưu ý: OnSetFocus chỉ có 2 tham số sau this (ITfContext* pic, BOOL fForeground).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfKeyEventSinkVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfKeyEventSink
    // OnSetFocus chỉ có 1 tham số sau this: BOOL fForeground
    public delegate* unmanaged[Stdcall]<IntPtr, int, int> OnSetFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnTestKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnTestKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, int*, int> OnPreservedKey;
}
