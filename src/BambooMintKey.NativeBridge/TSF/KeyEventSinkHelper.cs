using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfKeystrokeMgrVTable
{
    // TODO: Define ITfKeystrokeMgr VTable per 002_03
}

public static unsafe class KeyEventSinkHelper
{
    public static uint AdviseKeyEventSink(IntPtr pThreadMgr, uint clientId, IntPtr pKeyEventSink)
    {
        // TODO: Implement ITfKeystrokeMgr.AdviseKeyEventSink wrapper per 002_03
        throw new NotImplementedException();
    }

    public static void UnadviseKeyEventSink(IntPtr pThreadMgr, uint clientId)
    {
        // TODO: Implement ITfKeystrokeMgr.UnadviseKeyEventSink wrapper per 002_03
    }
}
