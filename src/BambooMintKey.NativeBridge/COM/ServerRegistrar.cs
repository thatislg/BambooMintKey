using Microsoft.Win32;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.COM;

/// <summary>
/// Thực hiện đăng ký / gỡ đăng ký COM In-process Server và TSF Language Profile.
/// Được gọi bởi regsvr32 thông qua các export DllRegisterServer / DllUnregisterServer.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public static class ServerRegistrar
{
    /// <summary>
    /// Đăng ký COM server và TSF profile/category cho tiếng Việt.
    /// Trả về HResult.Ok nếu thành công.
    /// </summary>
    public static int RegisterServer()
    {
        // Lấy đúng đường dẫn thực của DLL hiện tại đang được load.
        // Không dùng Process.GetCurrentProcess().MainModule vì có thể trỏ đến EXE host.
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
        if (!HResult.Succeeded(hr)) return hr;

        return TsfRegistration.RegisterCategories();
    }

    /// <summary>
    /// Gỡ đăng ký COM server và TSF profile/category.
    /// Trả về HResult.Ok nếu thành công.
    /// </summary>
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
