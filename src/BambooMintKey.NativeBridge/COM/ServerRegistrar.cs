using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.COM;

public static class ServerRegistrar
{
    public static int RegisterServer()
    {
        var modulePath = Process.GetCurrentProcess().MainModule?.FileName;
        // Lấy đúng đường dẫn thực của DLL hiện tại
        var dllPath = NativeMethods.GetCurrentDllPath();

        if (string.IsNullOrEmpty(dllPath)) return HResult.Fail;

        // 1. Ghi Registry InprocServer32
        var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
        using (var key = Registry.ClassesRoot.CreateSubKey(clsidKeyPath))
        {
            key.SetValue(null, Constants.TextServiceName);
            using var inproc = key.CreateSubKey("InprocServer32");
            inproc.SetValue(null, dllPath);
            inproc.SetValue("ThreadingModel", Constants.ThreadingModel);
        }

        // 2. Gọi TSF COM API để đăng ký Category & Profile
        var hr = TsfRegistration.RegisterProfiles(dllPath);
        if (hr != HResult.Ok) return hr;

        return TsfRegistration.RegisterCategories();
    }

    public static int UnregisterServer()
    {
        // 1. Hủy Categories và Profile trong TSF
        TsfRegistration.UnregisterCategories();
        TsfRegistration.UnregisterProfiles();

        // 2. Xóa Registry CLSID
        var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
        Registry.ClassesRoot.DeleteSubKeyTree(clsidKeyPath, throwOnMissingSubKey: false);

        return HResult.Ok;
    }
}
