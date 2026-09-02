// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop;

/// <summary>
/// Các P/Invoke Win32 cơ bản dùng cho COM server tự nhận diện DLL path
/// và tạo các đối tượng TSF COM.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public static class NativeMethods
{
    // =========================================================================
    // GetModuleHandleEx flags
    // =========================================================================

    /// <summary>
    /// GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS - Lấy module handle từ địa chỉ hàm.
    /// </summary>
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

    // =========================================================================
    // kernel32 P/Invokes
    // =========================================================================

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetModuleHandleExW(
        uint dwFlags,
        IntPtr lpModuleName,
        out IntPtr phModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(
        IntPtr hModule,
        [Out] char[] lpFilename,
        uint nSize);

    // =========================================================================
    // ole32 P/Invokes
    // =========================================================================

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static unsafe extern int CoCreateInstance(
        Guid* rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        Guid* riid,
        out IntPtr ppv);

    [DllImport("msctf.dll", ExactSpelling = true)]
    public static extern int TF_CreateInputProcessorProfiles(out IntPtr ppipProfile);

    [DllImport("msctf.dll", ExactSpelling = true)]
    public static extern int TF_CreateCategoryMgr(out IntPtr ppcat);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern void CoUninitialize();

    public const uint CoinitApartmentthreaded = 0x2;
    public const uint CoinitMultithreaded = 0x0;

    public const uint ClsCtxInprocServer = 0x1;
    public const uint ClsCtxAll = 0x17; // 23 decimal

    // =========================================================================
    // Helper lấy đường dẫn DLL hiện tại
    // =========================================================================

    /// <summary>
    /// Lấy đường dẫn tuyệt đối của file DLL hiện tại đang thực thi trong bộ nhớ
    /// từ một con trỏ hàm cụ thể nằm trong DLL đó.
    /// </summary>
    /// <param name="functionPtr">Con trỏ đến hàm nằm trong DLL cần lấy đường dẫn.</param>
    public static string GetDllPathFromFunctionPointer(IntPtr functionPtr)
    {
        if (functionPtr == IntPtr.Zero) return string.Empty;

        if (!GetModuleHandleExW(GetModuleHandleExFlagFromAddress, functionPtr, out IntPtr hModule) || hModule == IntPtr.Zero)
        {
            return string.Empty;
        }

        char[] buffer = new char[260]; // MAX_PATH
        uint length = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);

        if (length >= buffer.Length)
        {
            buffer = new char[32768];
            length = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);
        }

        return length > 0 ? new string(buffer, 0, (int)length) : string.Empty;
    }

    /// <summary>
    /// Lấy đường dẫn tuyệt đối của file DLL hiện tại đang thực thi trong bộ nhớ.
    /// Dùng một delegate trỏ đến hàm trong chính assembly này để tìm Module Handle.
    /// </summary>
    public static string GetCurrentDllPath()
    {
        var dummyDelegate = (Action)DummyMethod;
        IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(dummyDelegate);
        var path = GetDllPathFromFunctionPointer(functionPtr);
        GC.KeepAlive(dummyDelegate);
        return path;
    }

    /// <summary>
    /// Hàm dummy chỉ dùng để lấy con trỏ hàm nằm trong assembly này.
    /// </summary>
    private static void DummyMethod() { }
}
