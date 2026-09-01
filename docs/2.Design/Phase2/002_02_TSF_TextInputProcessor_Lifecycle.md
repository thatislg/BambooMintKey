# Thiết Kế Chi Tiết: Quản Lý Vòng Đời TSF Text Input Processor

**Mã tài liệu:** `002_02_TSF_TextInputProcessor_Lifecycle`

  

**Giai đoạn:** Phase 2 - Tích hợp Hệ Điều Hành (Windows TSF & NativeAOT)  

**Thuộc module:** `BambooMintKey.NativeBridge`

  

**Trạng thái:** Sẵn sàng thực thi (Ready for Implementation)

## 1. Mục Tiêu Kỹ Thuật

- Cài đặt giao diện COM `ITfTextInputProcessorEx` và `ITfTextInputProcessor` trên đối tượng chính `BambooMintKeyTextService` mà không dùng `ComImport` (NativeAOT VTable compliant).
- Xử lý phương thức `ActivateEx` để tiếp nhận `ITfThreadMgr`, `TfClientId` (client ID do TSF cấp), và cờ kích hoạt hệ thống (`TF_TMAE_*`).
- Đăng ký lắng nghe sự kiện thay đổi focus (`ITfThreadMgrEventSink`) và chuyển đổi context (`ITfDocumentMgr`).
- Quản lý cơ chế hủy tài nguyên trong `Deactivate`, đảm bảo unadvise toàn bộ event sink, giải phóng tham chiếu COM và reset `WordState` của F# Engine về trạng thái rỗng (`WordState.Empty`).

## 2. Kiến Trúc Vòng Đời (Lifecycle State Machine)

```bash
                       ┌────────────────────────┐
                       │ CoCreateInstance (TSF) │
                       └───────────┬────────────┘
                                   │
                                   ▼
                       ┌────────────────────────┐
                       │  BambooMintKey Service │
                       │       (Created)        │
                       └───────────┬────────────┘
                                   │
              ITfTextInputProcessorEx::ActivateEx(pThreadMgr, tfClientId, dwFlags)
                                   │
                                   ▼
                       ┌────────────────────────┐
                       │       ACTIVATED        │
                       │ - Lưu ThreadMgr & ID   │
                       │ - Advise KeyEventSink  │
                       │ - Advise ThreadMgrSink │
                       │ - Init F# WordState    │
                       └───────────┬────────────┘
                                   │
              ITfTextInputProcessor::Deactivate()
                                   │
                                   ▼
                       ┌────────────────────────┐
                       │      DEACTIVATED       │
                       │ - Unadvise Sinks       │
                       │ - Release COM Pointers │
                       │ - Reset Engine State   │
                       └───────────┬────────────┘
                                   │
                                   ▼
                       ┌────────────────────────┐
                       │    Release / Destroy   │
                       └────────────────────────┘
```

## 3. Khai Báo COM Structs & VTables

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/ITfTextInputProcessor.cs`:

C#

```c#
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

public static class TsfFlags
{
    public const uint TF_TMAE_NOACTIVATETIP = 0x00000001;
    public const uint TF_TMAE_SECUREMODE    = 0x00000002;
    public const uint TF_TMAE_UIELEMENTENABLEDONLY = 0x00000004;
    public const uint TF_INVALID_CLIENT_ID  = 0;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfTextInputProcessorExVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfTextInputProcessor
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int> Activate;
    public delegate* unmanaged[Stdcall]<IntPtr, int> Deactivate;

    // ITfTextInputProcessorEx
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, int> ActivateEx;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfThreadMgrEventSinkVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfThreadMgrEventSink
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int> OnInitDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> OnUninitDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int> OnSetFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> OnPushContext;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> OnPopContext;
}
```

## 4. Cài Đặt `BambooMintKeyTextService`

File trọng tâm điều phối toàn bộ phiên gõ tại `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs`:

C#

```c#
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

public unsafe class BambooMintKeyTextService
{
    private static ITfTextInputProcessorExVTable* _processorVTable;
    private static ITfThreadMgrEventSinkVTable* _threadMgrSinkVTable;

    // Instance native structure holding interfaces
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeLayout
    {
        public IntPtr VTableProcessor;       // Con trỏ vtable ITfTextInputProcessorEx
        public IntPtr VTableThreadMgrSink;   // Con trỏ vtable ITfThreadMgrEventSink
        public IntPtr VTableKeyEventSink;    // Con trỏ vtable ITfKeyEventSink (Chi tiết ở 002_03)
        public IntPtr GCHandle;              // GCHandle trỏ ngược lại instance C#
    }

    private int _refCount = 1;
    private IntPtr _pThreadMgr = IntPtr.Zero;
    private uint _clientId = TsfFlags.TF_INVALID_CLIENT_ID;
    private uint _threadMgrEventSinkCookie = 0;
    private uint _keyEventSinkCookie = 0;
    private bool _isActivated = false;

    // Properties cho các component khác truy cập
    public IntPtr ThreadMgr => _pThreadMgr;
    public uint ClientId => _clientId;
    public bool IsActivated => _isActivated;

    public static IntPtr CreateNativeInstance()
    {
        InitializeVTables();

        var service = new BambooMintKeyTextService();
        var gcHandle = GCHandle.Alloc(service, GCHandleType.Normal);

        var layout = (NativeLayout*)Marshal.AllocHGlobal(sizeof(NativeLayout));
        layout->VTableProcessor = (IntPtr)_processorVTable;
        layout->VTableThreadMgrSink = (IntPtr)_threadMgrSinkVTable;
        layout->VTableKeyEventSink = KeyEventSinkImpl.GetVTablePointer();
        layout->GCHandle = GCHandle.ToIntPtr(gcHandle);

        ComServerState.ObjectCreated();
        return (IntPtr)layout;
    }

    private static void InitializeVTables()
    {
        if (_processorVTable != null) return;

        _processorVTable = (ITfTextInputProcessorExVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(BambooMintKeyTextService), sizeof(ITfTextInputProcessorExVTable));
        _processorVTable->QueryInterface = &QueryInterface;
        _processorVTable->AddRef = &AddRef;
        _processorVTable->Release = &Release;
        _processorVTable->Activate = &Activate;
        _processorVTable->Deactivate = &Deactivate;
        _processorVTable->ActivateEx = &ActivateEx;

        _threadMgrSinkVTable = (ITfThreadMgrEventSinkVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(BambooMintKeyTextService), sizeof(ITfThreadMgrEventSinkVTable));
        _threadMgrSinkVTable->QueryInterface = &QueryInterface_ThreadMgrSink;
        _threadMgrSinkVTable->AddRef = &AddRef_ThreadMgrSink;
        _threadMgrSinkVTable->Release = &Release_ThreadMgrSink;
        _threadMgrSinkVTable->OnInitDocumentMgr = &OnInitDocumentMgr;
        _threadMgrSinkVTable->OnUninitDocumentMgr = &OnUninitDocumentMgr;
        _threadMgrSinkVTable->OnSetFocus = &OnSetFocus;
        _threadMgrSinkVTable->OnPushContext = &OnPushContext;
        _threadMgrSinkVTable->OnPopContext = &OnPopContext;
    }

    private static BambooMintKeyTextService GetTarget(IntPtr thisPtr)
    {
        var layout = (NativeLayout*)thisPtr;
        var handle = GCHandle.FromIntPtr(layout->GCHandle);
        return (BambooMintKeyTextService)handle.Target!;
    }

    #region IUnknown Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HRESULT.E_POINTER;
        *ppvObject = IntPtr.Zero;

        if (*riid == Guids.IidIUnknown ||
            *riid == Guids.IidITfTextInputProcessor ||
            *riid == Guids.IidITfTextInputProcessorEx)
        {
            *ppvObject = thisPtr;
            AddRef(thisPtr);
            return HRESULT.S_OK;
        }

        if (*riid == Guids.IidITfThreadMgrEventSink)
        {
            // Trỏ đến offset của ITfThreadMgrEventSink trong struct NativeLayout
            *ppvObject = thisPtr + sizeof(IntPtr);
            AddRef(thisPtr);
            return HRESULT.S_OK;
        }

        if (*riid == Guids.IidITfKeyEventSink)
        {
            // Trỏ đến offset của ITfKeyEventSink trong struct NativeLayout
            *ppvObject = thisPtr + (sizeof(IntPtr) * 2);
            AddRef(thisPtr);
            return HRESULT.S_OK;
        }

        return HRESULT.E_NOINTERFACE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        return (uint)Interlocked.Increment(ref target._refCount);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        var count = Interlocked.Decrement(ref target._refCount);
        if (count == 0)
        {
            var layout = (NativeLayout*)thisPtr;
            var handle = GCHandle.FromIntPtr(layout->GCHandle);
            handle.Free();
            Marshal.FreeHGlobal(thisPtr);
            ComServerState.ObjectDestroyed();
        }
        return (uint)count;
    }

    // Proxy Unknown cho Interface con thứ 2 (ThreadMgrSink)
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface_ThreadMgrSink(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
        => QueryInterface(thisPtr - sizeof(IntPtr), riid, ppvObject);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef_ThreadMgrSink(IntPtr thisPtr)
        => AddRef(thisPtr - sizeof(IntPtr));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release_ThreadMgrSink(IntPtr thisPtr)
        => Release(thisPtr - sizeof(IntPtr));
    #endregion

    #region ITfTextInputProcessorEx Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Activate(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId)
    {
        return ActivateEx(thisPtr, pThreadMgr, tfClientId, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ActivateEx(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId, uint dwFlags)
    {
        if (pThreadMgr == IntPtr.Zero) return HRESULT.E_INVALIDARG;

        var target = GetTarget(thisPtr);
        target._pThreadMgr = pThreadMgr;
        target._clientId = tfClientId;
        target._isActivated = true;

        // 1. Tăng RefCount cho ITfThreadMgr
        NativeCOM.AddRef(pThreadMgr);

        // 2. Advise ThreadMgrEventSink để theo dõi chuyển đổi cửa sổ / control
        var sinkPtr = thisPtr + sizeof(IntPtr);
        target._threadMgrEventSinkCookie = TsfEventSinkHelper.AdviseSink(
            pThreadMgr, Guids.IidITfThreadMgrEventSink, sinkPtr);

        // 3. Advise KeyEventSink để bắt đầu bắt phím
        var keySinkPtr = thisPtr + (sizeof(IntPtr) * 2);
        target._keyEventSinkCookie = KeyEventSinkHelper.AdviseKeyEventSink(
            pThreadMgr, tfClientId, keySinkPtr);

        // 4. Khởi tạo / Đồng bộ Engine State
        BridgeStateManager.InitializeEngine();

        return HRESULT.S_OK;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Deactivate(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        if (!target._isActivated) return HRESULT.S_OK;

        // 1. Unadvise KeyEventSink
        if (target._keyEventSinkCookie != 0)
        {
            KeyEventSinkHelper.UnadviseKeyEventSink(target._pThreadMgr, target._clientId);
            target._keyEventSinkCookie = 0;
        }

        // 2. Unadvise ThreadMgrEventSink
        if (target._threadMgrEventSinkCookie != 0)
        {
            TsfEventSinkHelper.UnadviseSink(target._pThreadMgr, target._threadMgrEventSinkCookie);
            target._threadMgrEventSinkCookie = 0;
        }

        // 3. Kết thúc mọi composition đang dang dở và reset buffer
        CompositionManager.TerminateActiveComposition();
        BridgeStateManager.ResetState();

        // 4. Giải phóng ITfThreadMgr
        if (target._pThreadMgr != IntPtr.Zero)
        {
            NativeCOM.Release(target._pThreadMgr);
            target._pThreadMgr = IntPtr.Zero;
        }

        target._clientId = TsfFlags.TF_INVALID_CLIENT_ID;
        target._isActivated = false;
        return HRESULT.S_OK;
    }
    #endregion

    #region ITfThreadMgrEventSink Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnInitDocumentMgr(IntPtr thisPtr, IntPtr pdimNew, IntPtr pdimPrev) => HRESULT.S_OK;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnUninitDocumentMgr(IntPtr thisPtr, IntPtr pdim) => HRESULT.S_OK;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnSetFocus(IntPtr thisPtr, IntPtr pdimFocus, IntPtr pdimPrevFocus)
    {
        // Khi chuyển sang ô nhập liệu khác -> Chốt từ đang gõ dở và làm sạch State
        CompositionManager.EndComposition();
        BridgeStateManager.ResetState();
        return HRESULT.S_OK;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPushContext(IntPtr thisPtr, IntPtr pic) => HRESULT.S_OK;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPopContext(IntPtr thisPtr, IntPtr pic) => HRESULT.S_OK;
    #endregion
}
```

## 5. Quản Lý Trạng Thái Cầu Nối F# (`BridgeStateManager.cs`)

Lớp điều phối in-memory gọi trực tiếp các kiểu dữ liệu và hàm của `BambooMintKey.Core`:

C#

```c#
using BambooMintKey.Core.Domain;
using BambooMintKey.Core.Engine;

namespace BambooMintKey.NativeBridge.TSF;

public static class BridgeStateManager
{
    private static Types.WordState _currentState = Types.WordState.Empty;
    private static EngineConfig.EngineConfig _currentConfig = EngineConfig.EngineConfig.Default;

    public static Types.WordState CurrentState => _currentState;
    public static EngineConfig.EngineConfig Config => _currentConfig;

    public static void InitializeEngine()
    {
        _currentState = Types.WordState.Empty;
        _currentConfig = EngineConfig.EngineConfig.Default;
    }

    public static (Types.WordState NewState, Types.EngineAction Action) ProcessKey(char c)
    {
        var input = Types.KeyInput.NewChar(c);
        var result = TelexEngine.processKey(_currentState, input, _currentConfig);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    public static (Types.WordState NewState, Types.EngineAction Action) ProcessBackspace()
    {
        var input = Types.KeyInput.Backspace;
        var result = TelexEngine.processKey(_currentState, input, _currentConfig);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    public static (Types.WordState NewState, Types.EngineAction Action) ProcessWordBreak(char breakChar)
    {
        var input = Types.KeyInput.NewWordBreak(breakChar);
        var result = TelexEngine.processKey(_currentState, input, _currentConfig);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    public static void ResetState()
    {
        _currentState = Types.WordState.Empty;
    }
}
```

## 6. Sơ Đồ Cấu Trúc Mã Nguồn Bổ Sung (Phase 2.2)

```bash
src/BambooMintKey.NativeBridge/
├── TSF/
│   ├── ITfTextInputProcessor.cs    # VTable định nghĩa ITfTextInputProcessor & Ex
│   ├── ITfThreadMgrEventSink.cs    # VTable định nghĩa sự kiện focus/context
│   ├── BambooMintKeyTextService.cs # Core Service quản lý vòng đời TIP
│   ├── BridgeStateManager.cs       # Cầu nối in-memory tới F# TelexEngine
│   └── TsfEventSinkHelper.cs       # Helper Advise/Unadvise ITfSource COM API
└── Interop/
    └── NativeCOM.cs                # P/Invoke IUnknown::AddRef / Release thủ công
```

