// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// VTable định nghĩa cho ITfCompartmentEventSink (msctf.h: 7434dd71-70e2-11d1-b656-0080c736b2d9).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCompartmentEventSinkVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfCompartmentEventSink
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> OnChange;
}
