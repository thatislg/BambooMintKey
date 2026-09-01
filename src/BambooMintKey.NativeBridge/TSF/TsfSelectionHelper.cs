using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfContextViewVTable
{
    // TODO: Define required ITfContextView / selection VTable helpers per 002_04
}

public static class TsfSelectionHelper
{
    public static IntPtr GetSelectionRange(IntPtr pContext, uint ec)
    {
        // TODO: Get current insertion point range via ITfContext::GetSelection per 002_04
        throw new NotImplementedException();
    }

    public static void SetSelectionToEnd(IntPtr pContext, uint ec, IntPtr pRange)
    {
        // TODO: Move caret to end of given range per 002_04
    }
}
