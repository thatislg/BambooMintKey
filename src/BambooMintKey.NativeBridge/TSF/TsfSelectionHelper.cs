// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfContextVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfContext (3 - 17) theo chuẩn Windows SDK msctf.h
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, uint, int*, int> RequestEditSession;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int*, int> InWriteSession;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, TfSelection*, uint*, int> GetSelection;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, TfSelection*, int> SetSelection;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> GetStart;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> GetEnd;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetActiveView;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumViews;
    public delegate* unmanaged[Stdcall]<IntPtr, void*, int> GetStatus;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetProperty;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetAppProperty;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid**, uint, Guid**, uint, IntPtr*, int> TrackProperties;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumProperties;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> CreateRangeBackup;
}

public static unsafe class TsfSelectionHelper
{
    public static IntPtr GetSelectionRange(IntPtr pContext, uint ec)
    {
        DebugLog.WriteAndFlush($"GetSelectionRange BEGIN pContext={pContext}, ec={ec}");
        if (pContext == IntPtr.Zero) return IntPtr.Zero;

        var contextVTable = *(TfContextVTable**)pContext;
        TfSelection selection = default;
        uint fetched = 0;

        // TF_DEFAULT_SELECTION = 0, ulCount = 1
        int hr = contextVTable->GetSelection(pContext, ec, 0, 1, &selection, &fetched);
        DebugLog.WriteAndFlush($"GetSelection HR=0x{hr:X8}, fetched={fetched}, range={selection.range}");
        if (HResult.Succeeded(hr) && fetched > 0 && selection.range != IntPtr.Zero)
        {
            return selection.range;
        }

        return IntPtr.Zero;
    }

    public static void SetSelectionToEnd(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        // 1. Thu hẹp Range về cuối từ (TF_ANCHOR_END)
        var rangeVTable = *(TfRangeVTable**)pRange;
        rangeVTable->Collapse(pRange, ec, TsfEditFlags.TfAnchorEnd);

        // 2. Gán vị trí con trỏ bàn phím vào vị trí này
        var contextVTable = *(TfContextVTable**)pContext;
        TfSelection selection = new()
        {
            range = pRange,
            styleAse = 0,
            styleFse = 0
        };

        contextVTable->SetSelection(pContext, ec, 1, &selection);
    }
}