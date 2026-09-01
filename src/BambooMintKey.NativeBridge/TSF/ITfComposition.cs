using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

public static class TsfEditFlags
{
    // TODO: Define TF_ES_* and TF_ANCHOR_* constants per 002_04
}

[StructLayout(LayoutKind.Sequential)]
public struct TF_SELECTION
{
    // TODO: Define TF_SELECTION struct per 002_04
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfEditSessionVTable
{
    // TODO: Define IUnknown + ITfEditSession VTable per 002_04
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfCompositionSinkVTable
{
    // TODO: Define IUnknown + ITfCompositionSink VTable per 002_04
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfCompositionVTable
{
    // TODO: Define IUnknown + ITfComposition VTable per 002_04
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfRangeVTable
{
    // TODO: Define IUnknown + ITfRange VTable per 002_04
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfContextVTable
{
    // TODO: Define IUnknown + ITfContext VTable per 002_04
}
