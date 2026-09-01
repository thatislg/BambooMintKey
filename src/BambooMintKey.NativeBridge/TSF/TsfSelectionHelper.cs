using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfContextVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, uint, int*, int> RequestEditSession;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> InWriteSession;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, TfSelection*, uint*, int> GetSelection;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, TfSelection*, int> SetSelection;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> GetStart;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> GetEnd;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int> GetActiveView;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetProperty;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetAppProperty;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> TrackProperties;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumProperties;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int> GetSelectionStyle;
}

public static unsafe class TsfSelectionHelper
{
    public static IntPtr GetSelectionRange(IntPtr pContext, uint ec)
    {
        if (pContext == IntPtr.Zero) return IntPtr.Zero;

        var contextVTable = *(TfContextVTable**)pContext;
        TfSelection selection = default;
        uint fetched = 0;

        int hr = contextVTable->GetSelection(pContext, ec, 1, &selection, &fetched);
        if (HResult.Succeeded(hr) && fetched > 0 && selection.range != IntPtr.Zero)
        {
            return selection.range;
        }

        return IntPtr.Zero;
    }

    public static void SetSelectionToEnd(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        // 1. Thu hẹp Range về cuối từ (TF_ANCHOR_END)
        var rangeVTable = *(TfRangeVTable**)pRange;
        rangeVTable->Collapse(pRange, ec, TsfEditFlags.TfAnchorEnd);

        // 2. Gán vị trí con trỏ bàn phím vào vị trí này
        var contextVTable = *(TfContextVTable**)pContext;
        TfSelection selection = new()
        {
            range = pRange,
            styleAse = 0,
            styleFse = 0
        };

        contextVTable->SetSelection(pContext, ec, 1, &selection);
    }
}