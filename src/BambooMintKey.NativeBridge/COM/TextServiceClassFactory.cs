using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.NativeBridge.COM;

// =========================================================================
// VTable định nghĩa cho IClassFactory (bỏ tiền tố 'I' theo quy ước .NET analyzer)
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ClassFactoryVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // IClassFactory
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int> CreateInstance;
    public delegate* unmanaged[Stdcall]<IntPtr, int, int> LockServer;
}

// =========================================================================
// TextServiceClassFactory - Tạo instance BambooMintKeyTextService cho TSF
// =========================================================================

/// <summary>
/// Cài đặt IClassFactory thủ công cho NativeAOT, trả về singleton factory.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public unsafe class TextServiceClassFactory
{
    private static ClassFactoryVTable* _vTable;
    private static IntPtr _singletonInstance;

    /// <summary>
    /// Lấy (hoặc khởi tạo) singleton instance của ClassFactory để Windows TSF query.
    /// </summary>
    public static IntPtr GetInstance()
    {
        if (_singletonInstance != IntPtr.Zero) return _singletonInstance;

        _vTable = (ClassFactoryVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(TextServiceClassFactory), sizeof(ClassFactoryVTable));

        _vTable->QueryInterface = &QueryInterface;
        _vTable->AddRef = &AddRef;
        _vTable->Release = &Release;
        _vTable->CreateInstance = &CreateInstance;
        _vTable->LockServer = &LockServer;

        var objMem = (IntPtr*)Marshal.AllocHGlobal(sizeof(IntPtr));
        *objMem = (IntPtr)_vTable;
        _singletonInstance = (IntPtr)objMem;
        return _singletonInstance;
    }

    // =========================================================================
    // IUnknown Callbacks
    // =========================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;

        if (*riid == Guids.IidIUnknown || *riid == Guids.IidIClassFactory)
        {
            *ppvObject = thisPtr;
            // AddRef được gọi qua function pointer vì nó có [UnmanagedCallersOnly]
            var vtable = *(ClassFactoryVTable**)thisPtr;
            vtable->AddRef(thisPtr);
            return HResult.Ok;
        }

        *ppvObject = IntPtr.Zero;
        return HResult.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
    {
        ComServerState.Lock();
        return 2; // Singleton static reference
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
    {
        ComServerState.Unlock();
        return 1;
    }

    // =========================================================================
    // IClassFactory Callbacks
    // =========================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CreateInstance(IntPtr thisPtr, IntPtr pUnkOuter, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;
        *ppvObject = IntPtr.Zero;

        if (pUnkOuter != IntPtr.Zero) return HResult.ClassNoAggregation;

        // Khởi tạo đối tượng BambooMintKeyTextService chính
        var textServicePtr = BambooMintKeyTextService.CreateNativeInstance();
        var punk = (IntPtr*)textServicePtr;
        var vtable = *(ClassFactoryVTable**)*punk; // Bóc tách IUnknown vtable

        return ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vtable->QueryInterface)(textServicePtr, riid, ppvObject);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int LockServer(IntPtr thisPtr, int fLock)
    {
        if (fLock != 0) ComServerState.Lock();
        else ComServerState.Unlock();
        return HResult.Ok;
    }
}
