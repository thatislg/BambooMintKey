using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.COM;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct IClassFactoryVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // IClassFactory
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int> CreateInstance;
    public delegate* unmanaged[Stdcall]<IntPtr, int, int> LockServer;
}

public unsafe class TextServiceClassFactory
{
    public static IntPtr GetInstance()
    {
        // TODO: Allocate singleton IClassFactory VTable per 002_01
        throw new NotImplementedException();
    }
}
