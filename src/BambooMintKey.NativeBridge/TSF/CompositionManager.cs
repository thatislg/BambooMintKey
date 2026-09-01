using System.Runtime.InteropServices;
using BambooMintKey.Core.Domain;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfContextCompositionVTable
{
    // TODO: Define ITfContextComposition VTable per 002_04
}

public static unsafe class CompositionManager
{
    public static bool HasActiveComposition() => false;

    public static IntPtr GetCompositionRange()
    {
        // TODO: Implement ITfComposition::GetRange wrapper per 002_04
        throw new NotImplementedException();
    }

    public static bool StartComposition(BambooMintKeyTextService service, IntPtr pContext, uint ec)
    {
        // TODO: Implement StartComposition via ITfContextComposition per 002_04
        throw new NotImplementedException();
    }

    public static void EndComposition()
    {
        // TODO: Implement EndComposition per 002_04
    }

    public static void TerminateActiveComposition()
    {
        // TODO: Implement forced cleanup per 002_04
    }

    public static void HandleEngineAction(BambooMintKeyTextService service, IntPtr pContext, Types.EngineAction action, string transformedText)
    {
        // TODO: Route Update/Commit/PassThrough to TextEditSession per 002_04
    }
}
