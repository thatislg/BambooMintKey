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

/// <summary>
/// Trợ thủ đồng bộ trạng thái Input Mode Compartment với Windows 10/11 Taskbar Input Indicator.
/// </summary>
public static unsafe class TsfCompartmentHelper
{
    private const ushort VtI4 = 3;

    /// <summary>
    /// Đồng bộ chế độ gõ V (Conversion On = 1) hoặc E (Conversion Off = 0) vào Thread Manager Compartment.
    /// </summary>
    public static int SetConversionMode(IntPtr pThreadMgr, uint clientId, bool isVietnamese)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

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
}
