using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

public static class TsfFlags
{
    // TODO: Define TF_TMAE_* flags and TF_INVALID_CLIENT_ID per 002_02
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfTextInputProcessorExVTable
{
    // TODO: Define IUnknown + ITfTextInputProcessor + ITfTextInputProcessorEx VTable per 002_02
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfThreadMgrEventSinkVTable
{
    // TODO: Define IUnknown + ITfThreadMgrEventSink VTable per 002_02
}
