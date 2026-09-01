using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge;

public static unsafe class Exports
{
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

    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllCanUnloadNow()
    {
        return ComServerState.CanUnload ? HResult.Ok : HResult.False;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllRegisterServer", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllRegisterServer()
    {
        return ServerRegistrar.RegisterServer();
    }

    [UnmanagedCallersOnly(EntryPoint = "DllUnregisterServer", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllUnregisterServer()
    {
        return ServerRegistrar.UnregisterServer();
    }
}
