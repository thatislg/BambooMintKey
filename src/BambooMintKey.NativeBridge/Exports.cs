// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge;

/// <summary>
/// Các điểm nhập C-ABI chuẩn của một COM DLL.
/// Được Windows TSF gọi khi load/unload và bởi regsvr32 khi đăng ký TIP.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public static unsafe class Exports
{
    /// <summary>
    /// DLL entry point: Tạo đối tượng COM từ CLSID của BambooMintKey TIP.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllGetClassObject(Guid* rclsid, Guid* riid, IntPtr* ppv)
    {
        if (rclsid == null || riid == null || ppv == null) return HResult.Pointer;
        *ppv = IntPtr.Zero;

        if (*rclsid != Guids.TextServiceClsid)
        {
            return HResult.ClassNotAvailable;
        }

        var factory = TextServiceClassFactory.GetInstance();
        var punk = *(ClassFactoryVTable**)factory;
        return punk->QueryInterface(factory, riid, ppv);
    }

    /// <summary>
    /// DLL entry point: Cho phép Windows kiểm tra xem DLL có thể unload hay chưa.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllCanUnloadNow()
    {
        return ComServerState.CanUnload ? HResult.Ok : HResult.False;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllRegisterServer", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllRegisterServer()
    {
        // TSF COM classes (ITfInputProcessorProfiles, ITfCategoryMgr) require STA apartment.
        NativeMethods.CoInitializeEx(IntPtr.Zero, NativeMethods.CoinitApartmentthreaded);
        try
        {
            return ServerRegistrar.RegisterServer();
        }
        finally
        {
            NativeMethods.CoUninitialize();
        }
    }

    /// <summary>
    /// DLL entry point: Gỡ đăng ký COM server + TSF profile/category.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "DllUnregisterServer", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllUnregisterServer()
    {
        NativeMethods.CoInitializeEx(IntPtr.Zero, NativeMethods.CoinitApartmentthreaded);
        try
        {
            return ServerRegistrar.UnregisterServer();
        }
        finally
        {
            NativeMethods.CoUninitialize();
        }
    }
}
