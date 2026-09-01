using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

public enum EditActionType
{
    UpdateText,
    CommitText,
    CancelComposition
}

public unsafe class TextEditSession
{
    // TODO: Implement ITfEditSession DoEditSession for TF_ES_READWRITE per 002_04
    public IntPtr CreateNativeInstance() => throw new NotImplementedException();
}
