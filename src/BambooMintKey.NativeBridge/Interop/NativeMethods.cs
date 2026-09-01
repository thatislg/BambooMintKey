using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop;

public static class NativeMethods
{
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

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

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    /// <summary>
    /// Lấy đường dẫn tuyệt đối của file DLL hiện tại đang thực thi trong bộ nhớ.
    /// </summary>
    public static string GetCurrentDllPath()
    {
        // Dùng một con trỏ hàm trong chính assembly này để tìm Module Handle của DLL
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

    private static void DummyMethod() { }
}
