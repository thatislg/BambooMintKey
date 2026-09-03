// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;
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

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(Guid* rclsid, IntPtr pUnkOuter, uint dwClsContext, Guid* riid, IntPtr* ppv);

    [DllImport("ole32.dll")]
    private static extern int CoInitialize(IntPtr pvReserved);

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

            // 5. Test LangBarItemButton (Milestone M1)
            Console.WriteLine("[5] Kiểm tra LangBarItemButton (ITfLangBarItemButton, ITfSource & State Toggle)...");
            IntPtr pLangBar = LangBarItemButton.Instance;
            if (pLangBar == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAIL] LangBarItemButton.Instance là NULL.");
                Console.ResetColor();
                return 1;
            }

            var buttonUnk = *(ITfLangBarItemButtonVTable**)pLangBar;
            IntPtr pButton = IntPtr.Zero;
            Guid iidButton = Guids.IidITfLangBarItemButton;
            hr = buttonUnk->QueryInterface(pLangBar, &iidButton, &pButton);
            if (hr != HResult.Ok || pButton == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Không thể QI ITfLangBarItemButton. HRESULT: 0x{hr:X8}");
                Console.ResetColor();
                return 1;
            }
            Console.WriteLine("  [OK] QI ITfLangBarItemButton thành công.");

            IntPtr pSource = IntPtr.Zero;
            Guid iidSource = Guids.IidITfSource;
            hr = buttonUnk->QueryInterface(pLangBar, &iidSource, &pSource);
            if (hr != HResult.Ok || pSource == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Không thể QI ITfSource từ LangBarItemButton. HRESULT: 0x{hr:X8}");
                Console.ResetColor();
                return 1;
            }
            Console.WriteLine("  [OK] QI ITfSource từ LangBarItemButton thành công.");

            // Kiểm tra GetInfo
            TF_LANGBARITEMINFO info;
            buttonUnk->GetInfo(pLangBar, &info);
            char* descPtr = info.szDescription;
            string desc = new string(descPtr);
            Console.WriteLine($"  [OK] GetInfo szDescription: '{desc}', dwStyle: 0x{info.dwStyle:X8}");

            // Kiểm tra GetText ban đầu (phải là V)
            IntPtr bstrText = IntPtr.Zero;
            buttonUnk->GetText(pLangBar, &bstrText);
            string initialText = Marshal.PtrToStringBSTR(bstrText);
            Marshal.FreeBSTR(bstrText);
            Console.WriteLine($"  [OK] Trạng thái ban đầu GetText: '{initialText}'");
            if (initialText != "V")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Trạng thái ban đầu mong đợi là 'V', nhận được '{initialText}'");
                Console.ResetColor();
                return 1;
            }

            // Giả lập người dùng click chuột trái (TfLbiClkLeft = 2)
            POINT pt = new() { X = 100, Y = 100 };
            RECT rc = new() { Left = 90, Top = 90, Right = 110, Bottom = 110 };
            buttonUnk->OnClick(pLangBar, TsfLangBarFlags.TfLbiClkLeft, pt, &rc);

            // Kiểm tra GetText sau click (phải là E)
            bstrText = IntPtr.Zero;
            buttonUnk->GetText(pLangBar, &bstrText);
            string toggledText = Marshal.PtrToStringBSTR(bstrText);
            Marshal.FreeBSTR(bstrText);
            Console.WriteLine($"  [OK] Trạng thái sau khi click chuột trái GetText: '{toggledText}'");
            if (toggledText != "E")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Sau khi toggle mong đợi là 'E', nhận được '{toggledText}'");
                Console.ResetColor();
                return 1;
            }

            // Click lần 2 để đổi lại về V
            buttonUnk->OnClick(pLangBar, TsfLangBarFlags.TfLbiClkLeft, pt, &rc);
            bstrText = IntPtr.Zero;
            buttonUnk->GetText(pLangBar, &bstrText);
            string restoredText = Marshal.PtrToStringBSTR(bstrText);
            Marshal.FreeBSTR(bstrText);
            Console.WriteLine($"  [OK] Trạng thái sau khi click lần 2 GetText: '{restoredText}'");
            if (restoredText != "V")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Sau khi toggle lần 2 mong đợi là 'V', nhận được '{restoredText}'");
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PASS] LangBarItemButton & ITfSource hoạt động hoàn hảo!");
            Console.ResetColor();

            // 5.1 Test IconHelper (Milestone M2)
            Console.WriteLine("[5.1] Kiểm tra IconHelper tạo HICON nền xanh lá (#16a34a)...");
            var (trayW, trayH) = IconHelper.GetTrayIconMetrics();
            Console.WriteLine($"  [OK] Tray Icon Metrics: {trayW}x{trayH}");

            IntPtr testIconV = IconHelper.CreateBambooIcon("V");
            IntPtr testIconE = IconHelper.CreateBambooIcon("E");
            if (testIconV == IntPtr.Zero || testIconE == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAIL] Không thể tạo HICON từ IconHelper.");
                Console.ResetColor();
                return 1;
            }
            Console.WriteLine($"  [OK] Tạo thành công HICON V={testIconV}, E={testIconE}");

            // Xuất file PNG để người dùng kiểm tra trực quan
            string mediaDir = @"D:\Kojin\BambooMintKey\src\media";
            try
            {
                // Xuất cỡ khay hệ thống (16x16 hoặc theo DPI)
                using (var icon = System.Drawing.Icon.FromHandle(testIconV))
                using (var bmp = icon.ToBitmap())
                {
                    string pathV = Path.Combine(mediaDir, "rendered_v_tray.png");
                    bmp.Save(pathV, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"  [EXPORT] Đã xuất ảnh icon 'V' kích thước khay ({trayW}x{trayH}): {pathV}");
                }

                using (var icon = System.Drawing.Icon.FromHandle(testIconE))
                using (var bmp = icon.ToBitmap())
                {
                    string pathE = Path.Combine(mediaDir, "rendered_e_tray.png");
                    bmp.Save(pathE, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"  [EXPORT] Đã xuất ảnh icon 'E' kích thước khay ({trayW}x{trayH}): {pathE}");
                }

                // Xuất cỡ lớn 64x64 nét căng để thẩm định chất lượng vẽ Win32 GDI
                IntPtr hIconV64 = IconHelper.CreateBambooIcon("V", 64, 64);
                IntPtr hIconE64 = IconHelper.CreateBambooIcon("E", 64, 64);
                if (hIconV64 != IntPtr.Zero)
                {
                    using var icon = System.Drawing.Icon.FromHandle(hIconV64);
                    using var bmp = icon.ToBitmap();
                    string pathV64 = Path.Combine(mediaDir, "rendered_v_64x64.png");
                    bmp.Save(pathV64, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"  [EXPORT] Đã xuất ảnh icon 'V' nét cao (64x64): {pathV64}");
                    IconHelper.DestroyIcon(hIconV64);
                }
                if (hIconE64 != IntPtr.Zero)
                {
                    using var icon = System.Drawing.Icon.FromHandle(hIconE64);
                    using var bmp = icon.ToBitmap();
                    string pathE64 = Path.Combine(mediaDir, "rendered_e_64x64.png");
                    bmp.Save(pathE64, System.Drawing.Imaging.ImageFormat.Png);
                    Console.WriteLine($"  [EXPORT] Đã xuất ảnh icon 'E' nét cao (64x64): {pathE64}");
                    IconHelper.DestroyIcon(hIconE64);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Không thể xuất PNG: {ex.Message}");
            }

            IconHelper.DestroyIcon(testIconV);
            IconHelper.DestroyIcon(testIconE);

            // Kiểm tra LangBarItemButton.GetIcon
            IntPtr hIconFromButton = IntPtr.Zero;
            hr = buttonUnk->GetIcon(pLangBar, &hIconFromButton);
            if (hr != HResult.Ok || hIconFromButton == IntPtr.Zero)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] GetIcon thất bại hoặc trả về NULL: HR=0x{hr:X8}, hIcon={hIconFromButton}");
                Console.ResetColor();
                return 1;
            }
            Console.WriteLine($"  [OK] GetIcon phản hồi HICON hợp lệ: {hIconFromButton}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PASS] Milestone M2 (IconHelper & GetIcon) đạt tiêu chuẩn!");
            Console.ResetColor();

            // 6. Giải phóng COM Pointers
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
