// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

// =========================================================================
// VTable định nghĩa cho ITfKeystrokeMgr
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfKeystrokeMgrVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfKeystrokeMgr
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int, int> AdviseKeyEventSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseKeyEventSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int*, int> GetForeground;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> TestKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> TestKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> KeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> KeyUp;
}

// =========================================================================
// KeyEventSinkHelper - Advise/Unadvise ITfKeyEventSink qua ITfKeystrokeMgr
// =========================================================================

/// <summary>
/// Helper đăng ký / gỡ đăng ký KeyEventSink với TSF Keystroke Manager.
/// Theo thiết kế 002_03_KeyEventSink_and_Core_Interop.md.
/// </summary>
public static unsafe class KeyEventSinkHelper
{
    /// <summary>IID_ITfKeystrokeMgr.</summary>
    private static readonly Guid IidITfKeystrokeMgr = new("AA80E806-2021-11D2-93E0-0060B067B86E");

    /// <summary>
    /// Đăng ký KeyEventSink với TSF để nhận sự kiện bàn phím.
    /// Trả về cookie nếu thành công, 0 nếu thất bại.
    /// </summary>
    public static uint AdviseKeyEventSink(IntPtr pThreadMgr, uint clientId, IntPtr pKeyEventSink)
    {
        if (pThreadMgr == IntPtr.Zero || pKeyEventSink == IntPtr.Zero) return 0;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;
        
        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return 0;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        // fForeground = 1 (Nhận sự kiện bàn phím ưu tiên mức Foreground)
        int adviseHr = pkmVTable->AdviseKeyEventSink(pKeystrokeMgr, clientId, pKeyEventSink, 1);

        pkmVTable->Release(pKeystrokeMgr);
        return adviseHr == HResult.Ok ? 1u : 0u;
    }

    /// <summary>
    /// Gỡ đăng ký KeyEventSink khỏi TSF Keystroke Manager.
    /// </summary>
    public static void UnadviseKeyEventSink(IntPtr pThreadMgr, uint clientId)
    {
        if (pThreadMgr == IntPtr.Zero) return;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;

        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        pkmVTable->UnadviseKeyEventSink(pKeystrokeMgr, clientId);
        pkmVTable->Release(pKeystrokeMgr);
    }
}
