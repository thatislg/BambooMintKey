// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
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
    private static void Log(string msg)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "BambooMintKey_Register.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private static string GetDllPath()
    {
        unsafe
        {
            // Lấy địa chỉ hàm DllRegisterServer export; nó chắc chắn nằm trong BambooMintKey.dll.
            delegate* unmanaged[Stdcall]<int> fn = &Exports.DllRegisterServer;
            return NativeMethods.GetDllPathFromFunctionPointer((IntPtr)fn);
        }
    }

    /// <summary>
    /// Đăng ký COM server và TSF profile/category cho tiếng Việt.
    /// Trả về HResult.Ok nếu thành công.
    /// </summary>
    public static int RegisterServer()
    {
        Log("RegisterServer started");

        var dllPath = GetDllPath();
        Log($"DLL path: {dllPath}");

        if (string.IsNullOrEmpty(dllPath))
        {
            Log("GetDllPath returned empty");
            return HResult.Fail;
        }

        try
        {
            var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
            using (var key = Registry.ClassesRoot.CreateSubKey(clsidKeyPath))
            {
                key.SetValue(null, Constants.TextServiceName);
                using var inproc = key.CreateSubKey("InprocServer32");
                inproc.SetValue(null, dllPath);
                inproc.SetValue("ThreadingModel", Constants.ThreadingModel);
            }
            Log("Registry InprocServer32 written");
        }
        catch (Exception ex)
        {
            Log($"Registry write failed: {ex}");
            return HResult.Fail;
        }

        var hrProfiles = TsfRegistration.RegisterProfiles(dllPath);
        Log($"RegisterProfiles HRESULT: 0x{hrProfiles:X8}");
        if (!HResult.Succeeded(hrProfiles)) return hrProfiles;

        var hrCategories = TsfRegistration.RegisterCategories();
        Log($"RegisterCategories HRESULT: 0x{hrCategories:X8}");
        if (!HResult.Succeeded(hrCategories)) return hrCategories;

        Log("RegisterServer completed successfully");
        return HResult.Ok;
    }

    /// <summary>
    /// Gỡ đăng ký COM server và TSF profile/category.
    /// Trả về HResult.Ok nếu thành công.
    /// </summary>
    public static int UnregisterServer()
    {
        Log("UnregisterServer started");
        TsfRegistration.UnregisterCategories();
        TsfRegistration.UnregisterProfiles();

        var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
        Registry.ClassesRoot.DeleteSubKeyTree(clsidKeyPath, throwOnMissingSubKey: false);

        Log("UnregisterServer completed");
        return HResult.Ok;
    }
}
