// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cài đặt IEnumTfDisplayAttributeInfo liệt kê các thuộc tính hiển thị do BambooMintKey cung cấp.
/// Liệt kê cả 2 thuộc tính: Stealth (ẩn gạch chân) và Preedit (nét đứt).
/// </summary>
public unsafe class EnumDisplayAttributeInfoImpl
{
    private static EnumTfDisplayAttributeInfoVTable* _vTable;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeLayout
    {
        public IntPtr VTable;
        public IntPtr GCHandle;
    }

    private int _refCount = 1;
    private uint _index;

    public static IntPtr CreateInstance()
    {
        if (_vTable == null)
        {
            _vTable = (EnumTfDisplayAttributeInfoVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(EnumDisplayAttributeInfoImpl), sizeof(EnumTfDisplayAttributeInfoVTable));

            _vTable->QueryInterface = &QueryInterface;
            _vTable->AddRef = &AddRef;
            _vTable->Release = &Release;
            _vTable->Clone = &Clone;
            _vTable->Next = &Next;
            _vTable->Reset = &Reset;
            _vTable->Skip = &Skip;
        }

        var instance = new EnumDisplayAttributeInfoImpl();
        var handle = GCHandle.Alloc(instance);

        var pMem = (NativeLayout*)Marshal.AllocHGlobal(sizeof(NativeLayout));
        pMem->VTable = (IntPtr)_vTable;
        pMem->GCHandle = GCHandle.ToIntPtr(handle);

        return (IntPtr)pMem;
    }

    private static EnumDisplayAttributeInfoImpl GetTarget(IntPtr thisPtr)
    {
        var layout = (NativeLayout*)thisPtr;
        var handle = GCHandle.FromIntPtr(layout->GCHandle);
        return (EnumDisplayAttributeInfoImpl)handle.Target!;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;
        *ppvObject = IntPtr.Zero;

        if (*riid == Guids.IidIUnknown || *riid == Guids.IidIEnumTfDisplayAttributeInfo)
        {
            *ppvObject = thisPtr;
            var vtable = *(EnumTfDisplayAttributeInfoVTable**)thisPtr;
            vtable->AddRef(thisPtr);
            return HResult.Ok;
        }

        return HResult.NoInterface;
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
        }
        return (uint)count;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Clone(IntPtr thisPtr, IntPtr* ppEnum)
    {
        if (ppEnum == null) return HResult.Pointer;
        *ppEnum = CreateInstance();
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Next(IntPtr thisPtr, uint ulCount, IntPtr* rgInfo, uint* pcFetched)
    {
        if (rgInfo == null) return HResult.Pointer;

        var target = GetTarget(thisPtr);
        uint fetched = 0;

        if (target._index == 0 && ulCount > 0)
        {
            var item = DisplayAttributeInfoImpl.GetStealthInstance();
            var itemVTable = *(TfDisplayAttributeInfoVTable**)item;
            itemVTable->AddRef(item);

            rgInfo[0] = item;
            target._index = 1;
            fetched = 1;

            if (ulCount > 1)
            {
                var item2 = DisplayAttributeInfoImpl.GetPreeditInstance();
                var item2VTable = *(TfDisplayAttributeInfoVTable**)item2;
                item2VTable->AddRef(item2);

                rgInfo[1] = item2;
                target._index = 2;
                fetched = 2;
            }
        }
        else if (target._index == 1 && ulCount > 0)
        {
            var item = DisplayAttributeInfoImpl.GetPreeditInstance();
            var itemVTable = *(TfDisplayAttributeInfoVTable**)item;
            itemVTable->AddRef(item);

            rgInfo[0] = item;
            target._index = 2;
            fetched = 1;
        }

        if (pcFetched != null) *pcFetched = fetched;
        if (ulCount == 0) return HResult.Ok;
        return fetched == ulCount ? HResult.Ok : HResult.False;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Reset(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        target._index = 0;
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Skip(IntPtr thisPtr, uint ulCount)
    {
        var target = GetTarget(thisPtr);
        target._index += ulCount;
        return (target._index <= 2) ? HResult.Ok : HResult.False;
    }
}
