// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

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
    private static uint _displayAttrAtomStealth = 0;
    private static uint _displayAttrAtomPreedit = 0;

    private static uint GetOrRegisterAtom(Guid guid)
    {
        int hr = NativeMethods.TF_CreateCategoryMgr(out IntPtr pCatMgr);
        if (HResult.Succeeded(hr) && pCatMgr != IntPtr.Zero)
        {
            var vtable = *(TfCategoryMgrVTable**)pCatMgr;
            try
            {
                Guid* pguid = stackalloc Guid[1];
                *pguid = guid;
                uint atom = 0;
                int regHr = vtable->RegisterGUID(pCatMgr, pguid, &atom);
                if (regHr == HResult.Ok)
                {
                    DebugLog.Write($"DisplayAttribute registered guid={guid} atom={atom}");
                }
                else
                {
                    DebugLog.Write($"DisplayAttribute RegisterGUID failed guid={guid} HR=0x{regHr:X8}");
                }
                return atom;
            }
            finally
            {
                vtable->Release(pCatMgr);
            }
        }
        return 0;
    }

    private static uint GetStealthAtom()
    {
        if (_displayAttrAtomStealth == 0)
            _displayAttrAtomStealth = GetOrRegisterAtom(Guids.GuidDisplayAttributeInput);
        return _displayAttrAtomStealth;
    }

    private static uint GetPreeditAtom()
    {
        if (_displayAttrAtomPreedit == 0)
            _displayAttrAtomPreedit = GetOrRegisterAtom(Guids.GuidDisplayAttributeInputPreedit);
        return _displayAttrAtomPreedit;
    }

    private static uint GetCurrentAtom()
    {
        bool enablePreedit = SharedMemoryManager.EnablePreedit;
        uint atom = enablePreedit ? GetPreeditAtom() : GetStealthAtom();
        DebugLog.Write($"DisplayAttribute current atom: enablePreedit={enablePreedit}, atom={atom}");
        return atom;
    }

    public static void ApplyCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        uint atom = GetCurrentAtom();
        if (atom == 0) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &Guids.GuidPropAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok || pProp == IntPtr.Zero) return;
        }

        var propVTable = *(TfPropertyVTable**)pProp;
        try
        {
            // Windows TSF quy định GUID_PROP_ATTRIBUTE nhận VARIANT kiểu VT_I4 (3) chứa TfGuidAtom
            Variant varValue = default;
            varValue.vt = 3; // VT_I4
            varValue.lVal = (int)atom;

            int hr = propVTable->SetValue(pProp, ec, pRange, (IntPtr)(&varValue));
            DebugLog.Write($"ApplyCompositionAttribute SetValue HR=0x{hr:X8}, atom={atom}");
        }
        finally
        {
            propVTable->Release(pProp);
        }
    }

    public static void ClearCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &Guids.GuidPropAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok || pProp == IntPtr.Zero) return;
        }

        var propVTable = *(TfPropertyVTable**)pProp;
        try
        {
            propVTable->Clear(pProp, ec, pRange);
        }
        finally
        {
            propVTable->Release(pProp);
        }
    }
}
