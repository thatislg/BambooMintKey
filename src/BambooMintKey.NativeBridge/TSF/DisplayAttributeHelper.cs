// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfPropertyVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfProperty::GetType trùng tên với object.GetType(), dùng 'new' để suppress warning
    public new delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> GetType;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, IntPtr, int> EnumRanges;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> GetValue;
    // ITfProperty::Clear chỉ có 2 tham số sau this: ec và pRange
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int> Clear;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> SetValueStore;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> SetValue;
}

public static unsafe class DisplayAttributeHelper
{
    private static readonly Guid GuidPropDisplayAttribute = new("57D4C09F-3462-4253-833B-8189D8B542F6");
    private static readonly Guid GuidDisplayAttributeInput = new("E6A93F52-7B42-4F18-A4D2-E6B39218F12D");

    public static void ApplyCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &GuidPropDisplayAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok) return;
        }

        // Gán Display Attribute GUID (gạch chân nét chấm/nét liền mờ)
        var propVTable = *(TfPropertyVTable**)pProp;
        
        // Gán giá trị Variant kiểu VT_I4 / GUID
        IntPtr pVar = CreateGuidVariant();
        propVTable->SetValue(pProp, ec, pRange, pVar);
        Marshal.FreeHGlobal(pVar);

        propVTable->Release(pProp);
    }

    public static void ClearCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &GuidPropDisplayAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok) return;
        }

        var propVTable = *(TfPropertyVTable**)pProp;
        propVTable->Clear(pProp, ec, pRange);
        propVTable->Release(pProp);
    }

    private static IntPtr CreateGuidVariant()
    {
        // VARIANT structure: VT_UNKNOWN hoặc VT_I4 chứa Atom ID.
        // GuidDisplayAttributeInput được giữ lại để gán vào pVar khi implement đầy đủ.
        _ = GuidDisplayAttributeInput;

        var mem = Marshal.AllocHGlobal(24);
        Marshal.WriteInt16(mem, 0, 13); // VT_UNKNOWN
        return mem;
    }
}
