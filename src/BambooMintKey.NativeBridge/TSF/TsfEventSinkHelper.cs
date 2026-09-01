using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

// =========================================================================
// VTable định nghĩa cho ITfSource
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfSourceVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfSource
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr, uint*, int> AdviseSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseSink;
}

// =========================================================================
// TsfEventSinkHelper - Advise/Unadvise các event sink qua ITfSource
// =========================================================================

/// <summary>
/// Helper đăng ký / gỡ đăng ký các event sink khác với ITfSource COM API.
/// Theo thiết kế 002_02_TSF_TextInputProcessor_Lifecycle.md.
/// </summary>
public static unsafe class TsfEventSinkHelper
{
    /// <summary>IID_ITfSource.</summary>
    private static readonly Guid IidITfSource = new("4EA48A35-60AE-446F-8BC6-0B0B6E49E0C0");

    /// <summary>
    /// Đăng ký một event sink với TSF source object.
    /// Trả về cookie nếu thành công, 0 nếu thất bại.
    /// </summary>
    public static uint AdviseSink(IntPtr pSource, Guid riid, IntPtr pSink)
    {
        if (pSource == IntPtr.Zero || pSink == IntPtr.Zero) return 0;

        IntPtr pTfSource = IntPtr.Zero;
        var punk = *(TfSourceVTable**)pSource;

        Guid riidCopy = riid;
        var pRiid = &riidCopy;

        int hr = punk->QueryInterface(pSource, pRiid, &pTfSource);
        if (hr != HResult.Ok || pTfSource == IntPtr.Zero) return 0;

        var sourceVTable = *(TfSourceVTable**)pTfSource;
        uint cookie = 0;
        int adviseHr = sourceVTable->AdviseSink(pTfSource, pRiid, pSink, &cookie);
        sourceVTable->Release(pTfSource);

        return adviseHr == HResult.Ok ? cookie : 0;
    }

    /// <summary>
    /// Gỡ đăng ký event sink khỏi TSF source object bằng cookie.
    /// </summary>
    public static void UnadviseSink(IntPtr pSource, uint cookie)
    {
        if (pSource == IntPtr.Zero || cookie == 0) return;

        IntPtr pTfSource = IntPtr.Zero;
        var punk = *(TfSourceVTable**)pSource;

        Guid iidSource = IidITfSource;
        var pRiidSource = &iidSource;

        int hr = punk->QueryInterface(pSource, pRiidSource, &pTfSource);
        if (hr != HResult.Ok || pTfSource == IntPtr.Zero) return;

        var sourceVTable = *(TfSourceVTable**)pTfSource;
        sourceVTable->UnadviseSink(pTfSource, cookie);
        sourceVTable->Release(pTfSource);
    }
}
