// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Diagnostics;
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
    /// Khi chạy ở tiến trình 64-bit, nếu có BambooMintKey32.dll nằm cùng thư mục thì tự động
    /// gọi SysWOW64\regsvr32.exe trước (kế thừa quyền Admin, không bật UAC lần 2) để các ứng
    /// dụng 32-bit (như Zalo PC) nạp được TSF in-proc server.
    /// </summary>
    public static int RegisterServer()
    {
        string arch = Environment.Is64BitProcess ? "x64" : "x86";
        Log($"RegisterServer ({arch}) started");

        var dllPath = GetDllPath();
        Log($"DLL path ({arch}): {dllPath}");

        if (string.IsNullOrEmpty(dllPath))
        {
            Log("GetDllPath returned empty");
            return HResult.Fail;
        }

        if (Environment.Is64BitProcess)
        {
            TryRunWow64Regsvr32(dllPath, unregister: false);
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
            Log($"Registry InprocServer32 ({arch}) written");
        }
        catch (Exception ex)
        {
            Log($"Registry write ({arch}) failed: {ex}");
            return HResult.Fail;
        }

        var hrProfiles = TsfRegistration.RegisterProfiles(dllPath);
        Log($"RegisterProfiles ({arch}) HRESULT: 0x{hrProfiles:X8}");
        if (!HResult.Succeeded(hrProfiles)) return hrProfiles;

        var hrCategories = TsfRegistration.RegisterCategories();
        Log($"RegisterCategories ({arch}) HRESULT: 0x{hrCategories:X8}");
        if (!HResult.Succeeded(hrCategories)) return hrCategories;

        Log($"RegisterServer ({arch}) completed successfully");
        return HResult.Ok;
    }

    /// <summary>
    /// Gỡ đăng ký COM server và TSF profile/category.
    /// Trả về HResult.Ok nếu thành công.
    /// </summary>
    public static int UnregisterServer()
    {
        string arch = Environment.Is64BitProcess ? "x64" : "x86";
        Log($"UnregisterServer ({arch}) started");

        if (Environment.Is64BitProcess)
        {
            var dllPath = GetDllPath();
            if (!string.IsNullOrEmpty(dllPath))
            {
                TryRunWow64Regsvr32(dllPath, unregister: true);
            }

            try
            {
                using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                hklm32.DeleteSubKeyTree($@"SOFTWARE\Classes\CLSID\{{{Guids.TextServiceClsid}}}", throwOnMissingSubKey: false);
            }
            catch { }
        }

        TsfRegistration.UnregisterCategories();
        TsfRegistration.UnregisterProfiles();

        var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
        Registry.ClassesRoot.DeleteSubKeyTree(clsidKeyPath, throwOnMissingSubKey: false);

        Log($"UnregisterServer ({arch}) completed");
        return HResult.Ok;
    }

    private static void TryRunWow64Regsvr32(string dll64Path, bool unregister)
    {
        try
        {
            string? dir = Path.GetDirectoryName(dll64Path);
            if (string.IsNullOrEmpty(dir)) return;

            string dll32Path = Path.Combine(dir, "BambooMintKey32.dll");
            if (!File.Exists(dll32Path))
            {
                Log($"WOW64 companion DLL not found at {dll32Path}, skipping x86 registration");
                return;
            }

            string sysWow64Regsvr = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SysWOW64",
                "regsvr32.exe");
            if (!File.Exists(sysWow64Regsvr))
            {
                Log($"SysWOW64 regsvr32 not found at {sysWow64Regsvr}");
                return;
            }

            string args = unregister ? $"/u /s \"{dll32Path}\"" : $"/s \"{dll32Path}\"";
            var psi = new ProcessStartInfo
            {
                FileName = sysWow64Regsvr,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit(10000);
                Log($"WOW64 regsvr32 ({(unregister ? "unregister" : "register")}) exitCode={proc.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            Log($"TryRunWow64Regsvr32 failed: {ex.Message}");
        }
    }
}
