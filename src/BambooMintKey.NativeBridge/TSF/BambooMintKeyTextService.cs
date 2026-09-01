using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

public unsafe class BambooMintKeyTextService
{
    // TODO: Implement NativeAOT TSF Text Service lifecycle per 002_02
    public static IntPtr CreateNativeInstance() => throw new NotImplementedException();
    public static uint AddRef(IntPtr thisPtr) => throw new NotImplementedException();
    public static uint Release(IntPtr thisPtr) => throw new NotImplementedException();
    public static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject) => throw new NotImplementedException();

    public IntPtr ThreadMgr => throw new NotImplementedException();
    public uint ClientId => throw new NotImplementedException();
    public bool IsActivated => throw new NotImplementedException();
}
