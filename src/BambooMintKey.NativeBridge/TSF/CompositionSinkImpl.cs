// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

public static unsafe class CompositionSinkImpl
{
    private static TfCompositionSinkVTable* _vTable;
    private static IntPtr _singletonInstance = IntPtr.Zero;

    public static IntPtr GetOrCreateInstance()
    {
        if (_singletonInstance != IntPtr.Zero)
            return _singletonInstance;

        if (_vTable == null)
        {
            _vTable = (TfCompositionSinkVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(CompositionSinkImpl), sizeof(TfCompositionSinkVTable));

            _vTable->QueryInterface = &QueryInterface;
            _vTable->AddRef = &AddRef;
            _vTable->Release = &Release;
            _vTable->OnCompositionTerminated = &OnCompositionTerminated;
        }

        var objMem = (IntPtr*)Marshal.AllocHGlobal(sizeof(IntPtr));
        *objMem = (IntPtr)_vTable;
        _singletonInstance = (IntPtr)objMem;
        return _singletonInstance;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;
        *ppvObject = IntPtr.Zero;

        Guid iidCompositionSink = new("3D61BF11-ACFF-428F-A89F-9E59C70C1E1F");

        if (*riid == Guids.IidIUnknown || *riid == iidCompositionSink)
        {
            *ppvObject = thisPtr;
            // AddRef được gọi qua function pointer vì nó có [UnmanagedCallersOnly]
            var vtable = *(TfCompositionSinkVTable**)thisPtr;
            vtable->AddRef(thisPtr);
            return HResult.Ok;
        }

        return HResult.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr) => 2;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnCompositionTerminated(IntPtr thisPtr, uint ecWrite, IntPtr pComposition)
    {
        // Khi composition bị giải phóng từ phía hệ điều hành/app
        CompositionManager.TerminateActiveComposition();
        BridgeStateManager.ResetState();
        return HResult.Ok;
    }
}