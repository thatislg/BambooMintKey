# Thiết Kế Chi Tiết: Đánh Chặn Phím Hệ Thống & Cầu Nối F# TelexEngine

**Mã tài liệu:** `002_03_KeyEventSink_and_Core_Interop`

  

**Giai đoạn:** Phase 2 - Tích hợp Hệ Điều Hành (Windows TSF & NativeAOT)  

**Thuộc module:** `BambooMintKey.NativeBridge`

  

**Trạng thái:** Sẵn sàng thực thi (Ready for Implementation)

## 1. Mục Tiêu Kỹ Thuật

- Cài đặt giao diện COM `ITfKeyEventSink` (`OnTestKeyDown`, `OnTestKeyUp`, `OnKeyDown`, `OnKeyUp`, `OnPreservedKey`) thông qua con trỏ hàm Unmanaged VTable.  
- Phân loại và lọc các tổ hợp phím điều khiển hệ thống (`Ctrl`, `Alt`, `Win`) để tránh nuốt nhầm phím tắt ứng dụng (Shortcuts / Hotkeys).
- Chuyển đổi mã phím ảo Win32 Virtual Key (`VK_*`) cùng trạng thái bàn phím (`GetKeyboardState`, `ToUnicode`) thành ký tự Unicode (UTF-16) tương ứng.
- Đánh giá khả năng nuốt phím (`*pfEaten = 1` hoặc `*pfEaten = 0`) trong `OnTestKeyDown` trước khi chuyển vào xử lý thực tế tại `OnKeyDown`.  
- Gọi trực tiếp hàm F# `TelexEngine.processKey` trong bộ nhớ và chuyển tiếp kết quả đến `CompositionManager` để hiển thị.  

## 2. Luồng Xử Lý Sự Kiện Bàn Phím (Key Event Pipeline)

```bash
[Phím Nhấn từ Người Dùng]
          │
          ▼
┌────────────────────────────────────────────────────────┐
│ ITfKeyEventSink::OnTestKeyDown(pic, wParam, lParam)    │
│  - Kiểm tra trạng thái Ctrl / Alt / Win                │
│  - Nếu phím là tổ hợp phím tắt ──> *pfEaten = 0        │
│  - Nếu là phím ký tự / Backspace ──> *pfEaten = 1      │
└─────────────────────────┬──────────────────────────────┘
                          │ (Nếu *pfEaten = 1)
                          ▼
┌────────────────────────────────────────────────────────┐
│ ITfKeyEventSink::OnKeyDown(pic, wParam, lParam)        │
│  1. ToUnicode(wParam) ──> Lấy ký tự char c             │
│  2. Phân loại KeyInput:                                │
│     - Ký tự (a-z, A-Z) ──> KeyInput.Char c             │
│     - Backspace        ──> KeyInput.Backspace          │
│     - Break/Dấu câu    ──> KeyInput.WordBreak c        │
│  3. Gọi F# TelexEngine.processKey(...)                 │
│  4. Điều phối kết quả EngineAction:                    │
│     - UpdateComposition ──> ITfRange::SetText          │
│     - Commit            ──> ITfComposition::End        │
│     - PassThrough       ──> Bỏ qua (để TSF xử lý)      │
└────────────────────────────────────────────────────────┘
```

## 3. Khai Báo COM Structs & VTable `ITfKeyEventSink`

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/ITfKeyEventSink.cs`:

C#

```c#
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfKeyEventSinkVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfKeyEventSink
    // OnSetFocus chỉ có 2 tham số sau this: ITfContext* pic, BOOL fForeground
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, int> OnSetFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnTestKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnTestKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, UIntPtr, IntPtr, int*, int> OnKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, int*, int> OnPreservedKey;
}
```

## 4. Chuyển Đổi Phím Ảo Win32 Sang Ký Tự (`KeyInputTranslator.cs`)

Tập trung tại file `src/BambooMintKey.NativeBridge/Interop/KeyInputTranslator.cs`:

C#

```c#
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop;

public static class KeyInputTranslator
{
    // Virtual-key codes theo Win32 User Input API.
    // Đặt tên PascalCase + 'Vk' prefix để tuân thủ quy ước .NET/F# analyzer.
    public const uint VkBack = 0x08;
    public const uint VkReturn = 0x0D;
    public const uint VkSpace = 0x20;
    private const uint VkControl = 0x11;
    private const uint VkMenu = 0x12; // Alt
    private const uint VkLeftWin = 0x5B;
    private const uint VkRightWin = 0x5C;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 4)] System.Text.StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags);

    public static bool IsModifierModifierPressed()
    {
        // Kiểm tra xem Ctrl, Alt hoặc phím Win có đang được đè không
        bool isCtrl = (GetKeyState((int)VkControl) & 0x8000) != 0;
        bool isAlt = (GetKeyState((int)VkMenu) & 0x8000) != 0;
        bool isWin = ((GetKeyState((int)VkLeftWin) & 0x8000) != 0) || ((GetKeyState((int)VkRightWin) & 0x8000) != 0);

        return isCtrl || isAlt || isWin;
    }

    public static char? ConvertVirtualKeyToChar(UIntPtr wParam, IntPtr lParam)
    {
        uint vkCode = (uint)wParam;
        uint scanCode = ((uint)lParam >> 16) & 0xFF;

        byte[] keyState = new byte[256];
        if (!GetKeyboardState(keyState)) return null;

        var sb = new System.Text.StringBuilder(4);
        int result = ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);

        if (result > 0 && sb.Length > 0)
        {
            return sb[0];
        }

        return null;
    }

    public static bool IsWordBreakChar(char c)
    {
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
    }
}
```

## 5. Cài Đặt `KeyEventSinkImpl.cs`

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs`:

C#

```c#
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.Core.Domain;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

public static unsafe class KeyEventSinkImpl
{
    private static TfKeyEventSinkVTable* _vTable;

    public static IntPtr GetVTablePointer()
    {
        if (_vTable != null) return (IntPtr)_vTable;

        _vTable = (TfKeyEventSinkVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(KeyEventSinkImpl), sizeof(TfKeyEventSinkVTable));

        _vTable->QueryInterface = &QueryInterface;
        _vTable->AddRef = &AddRef;
        _vTable->Release = &Release;
        _vTable->OnSetFocus = &OnSetFocus;
        _vTable->OnTestKeyDown = &OnTestKeyDown;
        _vTable->OnTestKeyUp = &OnTestKeyUp;
        _vTable->OnKeyDown = &OnKeyDown;
        _vTable->OnKeyUp = &OnKeyUp;
        _vTable->OnPreservedKey = &OnPreservedKey;

        return (IntPtr)_vTable;
    }

    #region IUnknown Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        // Offset lùi lại về struct BambooMintKeyTextService chính
        var rootPtr = thisPtr - (sizeof(IntPtr) * 2);
        return BambooMintKeyTextService.QueryInterfaceImpl(rootPtr, riid, ppvObject);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
    {
        var rootPtr = thisPtr - (sizeof(IntPtr) * 2);
        return BambooMintKeyTextService.AddRefImpl(rootPtr);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
    {
        var rootPtr = thisPtr - (sizeof(IntPtr) * 2);
        return BambooMintKeyTextService.ReleaseImpl(rootPtr);
    }
    #endregion

    #region ITfKeyEventSink Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnSetFocus(IntPtr thisPtr, IntPtr pic, int fForeground) => HResult.Ok;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnTestKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        // 1. Không can thiệp nếu người dùng đang bấm tổ hợp phím tắt (Ctrl/Alt/Win)
        if (KeyInputTranslator.IsModifierModifierPressed())
        {
            return HResult.Ok;
        }

        uint vkCode = (uint)wParam;

        // 2. Can thiệp nếu là Backspace khi đang có phiên Composition
        if (vkCode == KeyInputTranslator.VkBack && CompositionManager.HasActiveComposition())
        {
            *pfEaten = 1;
            return HResult.Ok;
        }

        // 3. Chuyển đổi mã phím sang ký tự để kiểm tra
        var inputChar = KeyInputTranslator.ConvertVirtualKeyToChar(wParam, lParam);
        if (inputChar.HasValue)
        {
            char c = inputChar.Value;
            // Nếu là ký tự bảng chữ cái hoặc số hoặc phím ngắt khi buffer có từ
            if (char.IsLetter(c) || CompositionManager.HasActiveComposition())
            {
                *pfEaten = 1;
            }
        }

        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnTestKeyUp(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        if (KeyInputTranslator.IsModifierModifierPressed())
        {
            return HResult.Ok;
        }

        uint vkCode = (uint)wParam;
        var servicePtr = thisPtr - (sizeof(IntPtr) * 2);
        var service = BambooMintKeyTextService.GetTarget(servicePtr);

        // Trường hợp 1: Phím Backspace
        if (vkCode == KeyInputTranslator.VkBack)
        {
            if (CompositionManager.HasActiveComposition())
            {
                var (newState, action) = BridgeStateManager.ProcessBackspace();
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                return HResult.Ok;
            }
            return HResult.Ok;
        }

        // Trường hợp 2: Phím Ký tự bình thường / Phím Ngắt
        var inputChar = KeyInputTranslator.ConvertVirtualKeyToChar(wParam, lParam);
        if (!inputChar.HasValue) return HResult.Ok;

        char c = inputChar.Value;

        if (KeyInputTranslator.IsWordBreakChar(c))
        {
            if (CompositionManager.HasActiveComposition())
            {
                var (newState, action) = BridgeStateManager.ProcessWordBreak(c);
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                return HResult.Ok;
            }
        }
        else
        {
            var (newState, action) = BridgeStateManager.ProcessKey(c);
            CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
            *pfEaten = 1;
            return HResult.Ok;
        }

        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnKeyUp(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPreservedKey(IntPtr thisPtr, IntPtr pic, Guid* rguid, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;
        return HResult.Ok;
    }
    #endregion
}
```

## 6. Helper Đăng Ký KeyEventSink (`KeyEventSinkHelper.cs`)

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/KeyEventSinkHelper.cs`:

C#

```c#
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfKeystrokeMgrVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int, int> AdviseKeyEventSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseKeyEventSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int*, int> GetForeground;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> TestKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> TestKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> KeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int*, int> KeyUp;
}

public static unsafe class KeyEventSinkHelper
{
    private static readonly Guid IidITfKeystrokeMgr = new("AA80E806-2021-11D2-93E0-0060B067B86E");

    public static uint AdviseKeyEventSink(IntPtr pThreadMgr, uint clientId, IntPtr pKeyEventSink)
    {
        if (pThreadMgr == IntPtr.Zero || pKeyEventSink == IntPtr.Zero) return 0;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;
        
        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return 0;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        // fForeground = 1 (Nhận sự kiện bàn phím ưu tiên mức Foreground)
        int adviseHr = pkmVTable->AdviseKeyEventSink(pKeystrokeMgr, clientId, pKeyEventSink, 1);

        pkmVTable->Release(pKeystrokeMgr);
        return adviseHr == HResult.Ok ? 1u : 0u;
    }

    public static void UnadviseKeyEventSink(IntPtr pThreadMgr, uint clientId)
    {
        if (pThreadMgr == IntPtr.Zero) return;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;

        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        pkmVTable->UnadviseKeyEventSink(pKeystrokeMgr, clientId);
        pkmVTable->Release(pKeystrokeMgr);
    }
}
```

## 7. Sơ Đồ Cấu Trúc Mã Nguồn Hoàn Thiện (Phase 2.3)

```bash
src/BambooMintKey.NativeBridge/
├── TSF/
│   ├── ITfKeyEventSink.cs         # VTable định nghĩa ITfKeyEventSink
│   ├── KeyEventSinkImpl.cs        # Xử lý OnKeyDown, OnTestKeyDown & nuốt phím
│   └── KeyEventSinkHelper.cs      # Advise qua ITfKeystrokeMgr
└── Interop/
    └── KeyInputTranslator.cs      # Win32 ToUnicode, kiểm tra phím tắt Ctrl/Alt
```

