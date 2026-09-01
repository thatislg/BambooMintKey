using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

public static unsafe class KeyEventSinkImpl
{
    public static IntPtr GetVTablePointer()
    {
        // TODO: Allocate and return ITfKeyEventSink VTable pointer per 002_03
        throw new NotImplementedException();
    }
}
