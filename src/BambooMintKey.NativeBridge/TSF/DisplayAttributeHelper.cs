using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfPropertyVTable
{
    // TODO: Define ITfProperty VTable per 002_04
}

public static unsafe class DisplayAttributeHelper
{
    public static void ApplyCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        // TODO: Apply TSF display attribute for inline composition underline per 002_04
    }

    public static void ClearCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        // TODO: Clear display attribute on commit per 002_04
    }
}
