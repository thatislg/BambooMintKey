// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

public unsafe class BambooMintKeyTextService
{
    private static TfTextInputProcessorExVTable* _processorVTable;
    private static TfThreadMgrEventSinkVTable* _threadMgrSinkVTable;

    // Instance native structure holding interfaces
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeLayout
    {
        public IntPtr VTableProcessor;       // Con trỏ vtable ITfTextInputProcessorEx
        public IntPtr VTableThreadMgrSink;   // Con trỏ vtable ITfThreadMgrEventSink
        public IntPtr VTableKeyEventSink;    // Con trỏ vtable ITfKeyEventSink (Chi tiết ở 002_03)
        public IntPtr GCHandle;              // GCHandle trỏ ngược lại instance C#
    }

    private int _refCount = 1;
    private IntPtr _pThreadMgr = IntPtr.Zero;
    private uint _clientId = TsfFlags.TfInvalidClientId;
    private uint _threadMgrEventSinkCookie;
    private uint _keyEventSinkCookie;
    private bool _isActivated;

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

        _processorVTable = (TfTextInputProcessorExVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(BambooMintKeyTextService), sizeof(TfTextInputProcessorExVTable));
        _processorVTable->QueryInterface = &QueryInterface;
        _processorVTable->AddRef = &AddRef;
        _processorVTable->Release = &Release;
        _processorVTable->Activate = &Activate;
        _processorVTable->Deactivate = &Deactivate;
        _processorVTable->ActivateEx = &ActivateEx;

        _threadMgrSinkVTable = (TfThreadMgrEventSinkVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(BambooMintKeyTextService), sizeof(TfThreadMgrEventSinkVTable));
        _threadMgrSinkVTable->QueryInterface = &QueryInterface_ThreadMgrSink;
        _threadMgrSinkVTable->AddRef = &AddRef_ThreadMgrSink;
        _threadMgrSinkVTable->Release = &Release_ThreadMgrSink;
        _threadMgrSinkVTable->OnInitDocumentMgr = &OnInitDocumentMgr;
        _threadMgrSinkVTable->OnUninitDocumentMgr = &OnUninitDocumentMgr;
        _threadMgrSinkVTable->OnSetFocus = &OnSetFocus;
        _threadMgrSinkVTable->OnPushContext = &OnPushContext;
        _threadMgrSinkVTable->OnPopContext = &OnPopContext;
    }

    internal static BambooMintKeyTextService GetTarget(IntPtr thisPtr)
    {
        var layout = (NativeLayout*)thisPtr;
        var handle = GCHandle.FromIntPtr(layout->GCHandle);
        return (BambooMintKeyTextService)handle.Target!;
    }

    #region IUnknown Callbacks

    // Các phương thức Impl không có [UnmanagedCallersOnly] để các proxy interface
    // có thể gọi lẫn nhau mà không vi phạm lệnh cấm gọi trực tiếp giữa các hàm unmanaged.
    internal static int QueryInterfaceImpl(IntPtr rootPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;
        *ppvObject = IntPtr.Zero;

        if (*riid == Guids.IidIUnknown ||
            *riid == Guids.IidITfTextInputProcessor ||
            *riid == Guids.IidITfTextInputProcessorEx)
        {
            *ppvObject = rootPtr;
            var processorVTable = *(TfTextInputProcessorExVTable**)rootPtr;
            processorVTable->AddRef(rootPtr);
            return HResult.Ok;
        }

        if (*riid == Guids.IidITfThreadMgrEventSink)
        {
            *ppvObject = rootPtr + sizeof(IntPtr);
            var processorVTable = *(TfTextInputProcessorExVTable**)rootPtr;
            processorVTable->AddRef(rootPtr);
            return HResult.Ok;
        }

        if (*riid == Guids.IidITfKeyEventSink)
        {
            *ppvObject = rootPtr + (sizeof(IntPtr) * 2);
            var processorVTable = *(TfTextInputProcessorExVTable**)rootPtr;
            processorVTable->AddRef(rootPtr);
            return HResult.Ok;
        }

        return HResult.NoInterface;
    }

    internal static uint AddRefImpl(IntPtr rootPtr)
    {
        var target = GetTarget(rootPtr);
        return (uint)Interlocked.Increment(ref target._refCount);
    }

    internal static uint ReleaseImpl(IntPtr rootPtr)
    {
        var target = GetTarget(rootPtr);
        var count = Interlocked.Decrement(ref target._refCount);
        if (count == 0)
        {
            var layout = (NativeLayout*)rootPtr;
            var handle = GCHandle.FromIntPtr(layout->GCHandle);
            handle.Free();
            Marshal.FreeHGlobal(rootPtr);
            ComServerState.ObjectDestroyed();
        }
        return (uint)count;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
        => QueryInterfaceImpl(thisPtr, riid, ppvObject);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
        => AddRefImpl(thisPtr);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
        => ReleaseImpl(thisPtr);

    // Proxy Unknown cho Interface con thứ 2 (ThreadMgrSink)
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface_ThreadMgrSink(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
        => QueryInterfaceImpl(thisPtr - sizeof(IntPtr), riid, ppvObject);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef_ThreadMgrSink(IntPtr thisPtr)
        => AddRefImpl(thisPtr - sizeof(IntPtr));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release_ThreadMgrSink(IntPtr thisPtr)
        => ReleaseImpl(thisPtr - sizeof(IntPtr));
    #endregion

    #region ITfTextInputProcessorEx Callbacks

    internal static int ActivateExImpl(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId, uint dwFlags)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

        var target = GetTarget(thisPtr);
        target._pThreadMgr = pThreadMgr;
        target._clientId = tfClientId;
        target._isActivated = true;

        // 1. Tăng RefCount cho ITfThreadMgr
        NativeCom.AddRef(pThreadMgr);

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

        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Activate(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId)
    {
        return ActivateExImpl(thisPtr, pThreadMgr, tfClientId, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int ActivateEx(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId, uint dwFlags)
    {
        return ActivateExImpl(thisPtr, pThreadMgr, tfClientId, dwFlags);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Deactivate(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        if (!target._isActivated) return HResult.Ok;

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
            NativeCom.Release(target._pThreadMgr);
            target._pThreadMgr = IntPtr.Zero;
        }

        target._clientId = TsfFlags.TfInvalidClientId;
        target._isActivated = false;
        return HResult.Ok;
    }
    #endregion

    #region ITfThreadMgrEventSink Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnInitDocumentMgr(IntPtr thisPtr, IntPtr pdimNew, IntPtr pdimPrev) => HResult.Ok;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnUninitDocumentMgr(IntPtr thisPtr, IntPtr pdim) => HResult.Ok;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnSetFocus(IntPtr thisPtr, IntPtr pdimFocus, IntPtr pdimPrevFocus)
    {
        // Khi chuyển sang ô nhập liệu khác -> Chốt từ đang gõ dở và làm sạch State
        CompositionManager.EndComposition();
        BridgeStateManager.ResetState();
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPushContext(IntPtr thisPtr, IntPtr pic) => HResult.Ok;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPopContext(IntPtr thisPtr, IntPtr pic) => HResult.Ok;
    #endregion
}
