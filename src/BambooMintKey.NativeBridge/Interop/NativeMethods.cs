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
    public static extern int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    // =========================================================================
    // Helper lấy đường dẫn DLL hiện tại
    // =========================================================================

    /// <summary>
    /// Lấy đường dẫn tuyệt đối của file DLL hiện tại đang thực thi trong bộ nhớ.
    /// Dùng một delegate trỏ đến hàm trong chính assembly này để tìm Module Handle.
    /// </summary>
    public static string GetCurrentDllPath()
    {
        var dummyDelegate = (Action)DummyMethod;
        IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(dummyDelegate);
        if (!GetModuleHandleExW(GetModuleHandleExFlagFromAddress, functionPtr, out IntPtr hModule) || hModule == IntPtr.Zero)
        {
            return string.Empty;
        }

        char[] buffer = new char[260]; // MAX_PATH
        uint length = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);

        // Xử lý nếu đường dẫn dài hơn MAX_PATH
        if (length >= buffer.Length)
        {
            buffer = new char[32768];
            length = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);
        }

        GC.KeepAlive(dummyDelegate);
        return length > 0 ? new string(buffer, 0, (int)length) : string.Empty;
    }

    /// <summary>
    /// Hàm dummy chỉ dùng để lấy con trỏ hàm nằm trong assembly này.
    /// </summary>
    private static void DummyMethod() { }
}
