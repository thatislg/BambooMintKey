// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfPropertyVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfReadOnlyProperty (3 - 6)
    public new delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> GetType;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, IntPtr, int> EnumRanges;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> GetValue;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetContext;

    // ITfProperty (7 - 10) theo chuẩn Windows SDK msctf.h
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, uint, int> FindRange;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> SetValueStore;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> SetValue;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int> Clear;
}

public static unsafe class DisplayAttributeHelper
{
    private static readonly Guid GuidPropDisplayAttribute = new("57D4C09F-3462-4253-833B-8189D8B542F6");

    public static void ApplyCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        // Tạm thời chưa gán display attribute cho đến khi DisplayAttributeProvider được đăng ký hoàn chỉnh trong Phase 3.
        _ = pContext;
        _ = ec;
        _ = pRange;
    }

    public static void ClearCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &GuidPropDisplayAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok || pProp == IntPtr.Zero) return;
        }

        var propVTable = *(TfPropertyVTable**)pProp;
        propVTable->Clear(pProp, ec, pRange);
        propVTable->Release(pProp);
    }
}
