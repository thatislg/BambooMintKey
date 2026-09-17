// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cấu trúc VARIANT Win32 dùng cho ITfCompartment::SetValue/GetValue.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct Variant
{
    [FieldOffset(0)]
    public ushort vt;
    [FieldOffset(2)]
    public ushort wReserved1;
    [FieldOffset(4)]
    public ushort wReserved2;
    [FieldOffset(6)]
    public ushort wReserved3;
    [FieldOffset(8)]
    public int lVal;
    [FieldOffset(8)]
    public IntPtr byref;
}

/// <summary>VTable cho ITfCompartmentMgr (msctf.h)</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCompartmentMgrVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetCompartment;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> ClearCompartment;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumCompartments;
}

/// <summary>VTable cho ITfCompartment (msctf.h)</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCompartmentVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, Variant*, int> SetValue;
    public delegate* unmanaged[Stdcall]<IntPtr, Variant*, int> GetValue;
}

/// <summary>VTable cho ITfThreadMgr (msctf.h)</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfThreadMgrVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> Activate;
    public delegate* unmanaged[Stdcall]<IntPtr, int> Deactivate;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> CreateDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumDocumentMgrs;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> SetFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr*, int> AssociateFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, int> IsAssocBaseWnd;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetGlobalCompartment;
}

/// <summary>
/// Trợ thủ đồng bộ trạng thái Input Mode Compartment với Windows 10/11 Taskbar Input Indicator.
/// </summary>
public static unsafe class TsfCompartmentHelper
{
    private const ushort VtI4 = 3;

    /// <summary>
    /// Đồng bộ chế độ gõ V (Conversion On = 1) hoặc E (Conversion Off = 0) vào Global Compartment.
    /// </summary>
    public static int SetGlobalConversionMode(IntPtr pThreadMgr, uint clientId, bool isVietnamese)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

        var tmVTable = *(TfThreadMgrVTable**)pThreadMgr;
        IntPtr pGlobalCompMgr = IntPtr.Zero;
        int hr = tmVTable->GetGlobalCompartment(pThreadMgr, &pGlobalCompMgr);
        if (hr != HResult.Ok || pGlobalCompMgr == IntPtr.Zero) return hr;

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pGlobalCompMgr;
            Guid guidConversion = Guids.GuidCompartmentKeyboardInputModeConversion;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pGlobalCompMgr, &guidConversion, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero) return hr;

            try
            {
                var compVTable = *(TfCompartmentVTable**)pComp;
                Variant varVal = new()
                {
                    vt = VtI4,
                    lVal = isVietnamese ? 1 : 0
                };
                int setHr = compVTable->SetValue(pComp, clientId, &varVal);
                DebugLog.Write($"TsfCompartmentHelper.SetGlobalConversionMode isVietnamese={isVietnamese}, hr=0x{setHr:X8}");
                return setHr;
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pGlobalCompMgr);
        }
    }

    /// <summary>
    /// Đồng bộ chế độ gõ V (Conversion On = 1) hoặc E (Conversion Off = 0) vào Thread Manager Compartment và Global Compartment.
    /// </summary>
    public static int SetConversionMode(IntPtr pThreadMgr, uint clientId, bool isVietnamese)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

        // Đồng bộ vào Global Compartment để toàn bộ hệ thống (kể cả Taskbar Shell) nhận biết
        SetGlobalConversionMode(pThreadMgr, clientId, isVietnamese);

        Guid iidCompMgr = Guids.IidITfCompartmentMgr;
        IntPtr pCompMgr = IntPtr.Zero;

        var unk = *(TfCompartmentMgrVTable**)pThreadMgr;
        int hr = unk->QueryInterface(pThreadMgr, &iidCompMgr, &pCompMgr);
        if (hr != HResult.Ok || pCompMgr == IntPtr.Zero)
        {
            return hr;
        }

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pCompMgr;
            Guid guidConversion = Guids.GuidCompartmentKeyboardInputModeConversion;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pCompMgr, &guidConversion, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero)
            {
                return hr;
            }

            try
            {
                var compVTable = *(TfCompartmentVTable**)pComp;
                Variant varVal = new()
                {
                    vt = VtI4,
                    lVal = isVietnamese ? 1 : 0
                };
                int setHr = compVTable->SetValue(pComp, clientId, &varVal);
                DebugLog.Write($"TsfCompartmentHelper.SetConversionMode isVietnamese={isVietnamese}, hr=0x{setHr:X8}");
                return setHr;
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pCompMgr);
        }
    }

    /// <summary>
    /// Đọc trạng thái Open/Close (1 = Vietnamese, 0 = English) từ TSF Compartment.
    /// </summary>
    public static bool GetOpenClose(IntPtr pThreadMgr, uint clientId, out bool isOpen)
    {
        isOpen = true; // Mặc định mở
        if (pThreadMgr == IntPtr.Zero) return false;

        Guid iidCompMgr = Guids.IidITfCompartmentMgr;
        IntPtr pCompMgr = IntPtr.Zero;

        var unk = *(TfCompartmentMgrVTable**)pThreadMgr;
        int hr = unk->QueryInterface(pThreadMgr, &iidCompMgr, &pCompMgr);
        if (hr != HResult.Ok || pCompMgr == IntPtr.Zero) return false;

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pCompMgr;
            Guid guidOpenClose = Guids.GuidCompartmentKeyboardOpenClose;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pCompMgr, &guidOpenClose, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero) return false;

            try
            {
                var compVTable = *(TfCompartmentVTable**)pComp;
                Variant varVal = new();
                hr = compVTable->GetValue(pComp, &varVal);
                if (hr == HResult.Ok && varVal.vt == VtI4)
                {
                    isOpen = varVal.lVal != 0;
                    return true;
                }
                return false;
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pCompMgr);
        }
    }

    /// <summary>
    /// Ghi trạng thái Open/Close (1 = Vietnamese, 0 = English) vào Global Compartment.
    /// </summary>
    public static int SetGlobalOpenClose(IntPtr pThreadMgr, uint clientId, bool isOpen)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

        var tmVTable = *(TfThreadMgrVTable**)pThreadMgr;
        IntPtr pGlobalCompMgr = IntPtr.Zero;
        int hr = tmVTable->GetGlobalCompartment(pThreadMgr, &pGlobalCompMgr);
        if (hr != HResult.Ok || pGlobalCompMgr == IntPtr.Zero) return hr;

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pGlobalCompMgr;
            Guid guidOpenClose = Guids.GuidCompartmentKeyboardOpenClose;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pGlobalCompMgr, &guidOpenClose, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero) return hr;

            try
            {
                var compVTable = *(TfCompartmentVTable**)pComp;
                Variant varVal = new()
                {
                    vt = VtI4,
                    lVal = isOpen ? 1 : 0
                };
                int setHr = compVTable->SetValue(pComp, clientId, &varVal);
                DebugLog.Write($"TsfCompartmentHelper.SetGlobalOpenClose isOpen={isOpen}, hr=0x{setHr:X8}");
                return setHr;
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pGlobalCompMgr);
        }
    }

    /// <summary>
    /// Ghi trạng thái Open/Close (1 = Vietnamese, 0 = English) vào TSF Compartment và Global Compartment.
    /// </summary>
    public static int SetOpenClose(IntPtr pThreadMgr, uint clientId, bool isOpen)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

        // Đồng bộ vào Global Compartment để toàn bộ hệ thống (kể cả Taskbar Shell) nhận biết
        SetGlobalOpenClose(pThreadMgr, clientId, isOpen);

        Guid iidCompMgr = Guids.IidITfCompartmentMgr;
        IntPtr pCompMgr = IntPtr.Zero;

        var unk = *(TfCompartmentMgrVTable**)pThreadMgr;
        int hr = unk->QueryInterface(pThreadMgr, &iidCompMgr, &pCompMgr);
        if (hr != HResult.Ok || pCompMgr == IntPtr.Zero) return hr;

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pCompMgr;
            Guid guidOpenClose = Guids.GuidCompartmentKeyboardOpenClose;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pCompMgr, &guidOpenClose, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero) return hr;

            try
            {
                var compVTable = *(TfCompartmentVTable**)pComp;
                Variant varVal = new()
                {
                    vt = VtI4,
                    lVal = isOpen ? 1 : 0
                };
                int setHr = compVTable->SetValue(pComp, clientId, &varVal);
                DebugLog.Write($"TsfCompartmentHelper.SetOpenClose isOpen={isOpen}, hr=0x{setHr:X8}");
                return setHr;
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pCompMgr);
        }
    }

    /// <summary>
    /// Đảo trạng thái Open/Close và đồng bộ cả ConversionMode.
    /// </summary>
    public static bool ToggleOpenClose(IntPtr pThreadMgr, uint clientId)
    {
        bool isOpen = true;
        GetOpenClose(pThreadMgr, clientId, out isOpen);
        bool newOpen = !isOpen;
        SetOpenClose(pThreadMgr, clientId, newOpen);
        SetConversionMode(pThreadMgr, clientId, newOpen);
        return newOpen;
    }

    /// <summary>
    /// Đăng ký lắng nghe sự kiện thay đổi của một Compartment qua ITfCompartmentEventSink.
    /// </summary>
    public static uint AdviseCompartmentEventSink(IntPtr pThreadMgr, Guid* pGuidCompartment, IntPtr pSink)
    {
        if (pThreadMgr == IntPtr.Zero || pSink == IntPtr.Zero || pGuidCompartment == null) return 0;

        Guid iidCompMgr = Guids.IidITfCompartmentMgr;
        IntPtr pCompMgr = IntPtr.Zero;

        var unk = *(TfCompartmentMgrVTable**)pThreadMgr;
        int hr = unk->QueryInterface(pThreadMgr, &iidCompMgr, &pCompMgr);
        if (hr != HResult.Ok || pCompMgr == IntPtr.Zero) return 0;

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pCompMgr;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pCompMgr, pGuidCompartment, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero) return 0;

            try
            {
                return TsfEventSinkHelper.AdviseSink(pComp, Guids.IidITfCompartmentEventSink, pSink);
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pCompMgr);
        }
    }

    /// <summary>
    /// Gỡ đăng ký lắng nghe sự kiện Compartment.
    /// </summary>
    public static void UnadviseCompartmentEventSink(IntPtr pThreadMgr, Guid* pGuidCompartment, uint cookie)
    {
        if (pThreadMgr == IntPtr.Zero || cookie == 0 || pGuidCompartment == null) return;

        Guid iidCompMgr = Guids.IidITfCompartmentMgr;
        IntPtr pCompMgr = IntPtr.Zero;

        var unk = *(TfCompartmentMgrVTable**)pThreadMgr;
        int hr = unk->QueryInterface(pThreadMgr, &iidCompMgr, &pCompMgr);
        if (hr != HResult.Ok || pCompMgr == IntPtr.Zero) return;

        try
        {
            var compMgrVTable = *(TfCompartmentMgrVTable**)pCompMgr;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pCompMgr, pGuidCompartment, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero) return;

            try
            {
                TsfEventSinkHelper.UnadviseSink(pComp, cookie);
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pCompMgr);
        }
    }
}

