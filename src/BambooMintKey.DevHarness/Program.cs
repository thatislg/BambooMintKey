using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.DevHarness;

public unsafe class Program
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    private delegate int DllGetClassObjectDelegate(Guid* rclsid, Guid* riid, IntPtr* ppv);

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BambooMintKey NativeAOT Dev Harness ===");

        string dllPath = args.Length > 0 ? args[0] : "BambooMintKey.dll";
        Console.WriteLine($"[1] Nạp thư viện: {dllPath}");

        IntPtr hModule = LoadLibraryW(dllPath);
        if (hModule == IntPtr.Zero)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] Không thể nạp DLL. Mã lỗi Win32: {Marshal.GetLastWin32Error()}");
            Console.ResetColor();
            return 1;
        }

        try
        {
            // 1. Lấy con trỏ hàm DllGetClassObject
            IntPtr pGetClassObject = GetProcAddress(hModule, "DllGetClassObject");
            if (pGetClassObject == IntPtr.Zero)
            {
                Console.WriteLine("[FAIL] Không tìm thấy export 'DllGetClassObject'.");
                return 1;
            }

            var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(pGetClassObject);

            // 2. Yêu cầu IClassFactory
            Console.WriteLine("[2] Khởi tạo COM ClassFactory...");
            IntPtr pClassFactory = IntPtr.Zero;
            Guid clsid = Guids.TextServiceClsid;
            Guid iidFactory = Guids.IidIClassFactory;

            int hr = dllGetClassObject(&clsid, &iidFactory, &pClassFactory);
            if (hr != HResult.Ok || pClassFactory == IntPtr.Zero)
            {
                Console.WriteLine($"[FAIL] DllGetClassObject thất bại với HRESULT: 0x{hr:X8}");
                return 1;
            }
            Console.WriteLine("[OK] Lấy thành công con trỏ IClassFactory.");

            // 3. Tạo instance ITfTextInputProcessorEx
            Console.WriteLine("[3] Tạo thực thể BambooMintKeyTextService...");
            IntPtr pTextService = IntPtr.Zero;
            Guid iidProcessorEx = Guids.IidITfTextInputProcessorEx;

            var factoryVTable = *(ClassFactoryVTable**)pClassFactory;
            hr = factoryVTable->CreateInstance(pClassFactory, IntPtr.Zero, &iidProcessorEx, &pTextService);

            if (hr != HResult.Ok || pTextService == IntPtr.Zero)
            {
                Console.WriteLine($"[FAIL] CreateInstance thất bại với HRESULT: 0x{hr:X8}");
                return 1;
            }
            Console.WriteLine("[OK] Khởi tạo đối tượng TIP thành công.");

            // 4. Test QueryInterface ITfKeyEventSink
            Console.WriteLine("[4] Kiểm tra QueryInterface cho ITfKeyEventSink...");
            IntPtr pKeyEventSink = IntPtr.Zero;
            Guid iidKeySink = Guids.IidITfKeyEventSink;

            var serviceVTable = *(TfTextInputProcessorExVTable**)pTextService;
            hr = serviceVTable->QueryInterface(pTextService, &iidKeySink, &pKeyEventSink);

            if (hr == HResult.Ok && pKeyEventSink != IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASS] Interface ITfKeyEventSink phản hồi chuẩn xác.");
                Console.ResetColor();

                var keySinkVTable = *(TfKeyEventSinkVTable**)pKeyEventSink;
                keySinkVTable->Release(pKeyEventSink);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Không thể QueryInterface ITfKeyEventSink. HRESULT: 0x{hr:X8}");
                Console.ResetColor();
                return 1;
            }

            // 5. Giải phóng COM Pointers
            serviceVTable->Release(pTextService);
            factoryVTable->Release(pClassFactory);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== TOÀN BỘ C-ABI VTABLE VÀ INTERFACE EXPORT ĐÃ VƯỢT QUA TEST ===");
            Console.ResetColor();
            return 0;
        }
        finally
        {
            FreeLibrary(hModule);
        }
    }
}
