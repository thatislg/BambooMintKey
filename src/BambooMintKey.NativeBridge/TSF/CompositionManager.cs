// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.Core.Domain;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfContextCompositionVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, IntPtr*, int> StartComposition;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumCompositions;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> FindComposition;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int> TakeOwnership;
}

public static unsafe class CompositionManager
{
    private static readonly Guid IidITfContextComposition = new("D40C8A3B-DA93-4B21-9E58-53E7135B47F0");
    private static IntPtr _pActiveComposition = IntPtr.Zero;

    public static bool HasActiveComposition() => _pActiveComposition != IntPtr.Zero;

    public static IntPtr GetCompositionRange()
    {
        if (_pActiveComposition == IntPtr.Zero) return IntPtr.Zero;

        IntPtr pRange = IntPtr.Zero;
        var compVTable = *(TfCompositionVTable**)_pActiveComposition;
        compVTable->GetRange(_pActiveComposition, &pRange);
        return pRange;
    }

    public static bool StartComposition(BambooMintKeyTextService service, IntPtr pContext, uint ec)
    {
        if (_pActiveComposition != IntPtr.Zero) return true;

        IntPtr pContextComp = IntPtr.Zero;
        var contextVTable = *(TfContextCompositionVTable**)pContext;

        fixed (Guid* riid = &IidITfContextComposition)
        {
            if (contextVTable->QueryInterface(pContext, riid, &pContextComp) != HResult.Ok)
                return false;
        }

        // Lấy selection range hiện tại của ô nhập liệu
        var pRange = TsfSelectionHelper.GetSelectionRange(pContext, ec);
        if (pRange == IntPtr.Zero)
        {
            var ccVTable = *(TfContextCompositionVTable**)pContextComp;
            ccVTable->Release(pContextComp);
            return false;
        }

        IntPtr pCompSink = CompositionSinkImpl.GetOrCreateInstance();
        IntPtr pComposition = IntPtr.Zero;

        var ccVTableFinal = *(TfContextCompositionVTable**)pContextComp;
        int hr = ccVTableFinal->StartComposition(pContextComp, ec, pRange, pCompSink, &pComposition);

        // Giải phóng COM tạm
        var rangeVTable = *(TfRangeVTable**)pRange;
        rangeVTable->Release(pRange);
        ccVTableFinal->Release(pContextComp);

        if (hr == HResult.Ok && pComposition != IntPtr.Zero)
        {
            _pActiveComposition = pComposition;
            return true;
        }

        return false;
    }

    public static void EndComposition()
    {
        if (_pActiveComposition == IntPtr.Zero) return;

        var compVTable = *(TfCompositionVTable**)_pActiveComposition;
        compVTable->EndComposition(_pActiveComposition, 0);
        compVTable->Release(_pActiveComposition);
        _pActiveComposition = IntPtr.Zero;
    }

    public static void TerminateActiveComposition()
    {
        if (_pActiveComposition != IntPtr.Zero)
        {
            var compVTable = *(TfCompositionVTable**)_pActiveComposition;
            compVTable->Release(_pActiveComposition);
            _pActiveComposition = IntPtr.Zero;
        }
    }

    public static void HandleEngineAction(BambooMintKeyTextService service, IntPtr pContext, Types.EngineAction action, string transformedText)
    {
        if (action.IsUpdateComposition)
        {
            var updateAction = (Types.EngineAction.UpdateComposition)action;
            RequestEdit(service, pContext, EditActionType.UpdateText, updateAction.newText);
        }
        else if (action.IsCommit)
        {
            var commitAction = (Types.EngineAction.Commit)action;
            RequestEdit(service, pContext, EditActionType.CommitText, commitAction.committedText);
            BridgeStateManager.ResetState();
        }
        else // PassThrough
        {
            if (HasActiveComposition())
            {
                RequestEdit(service, pContext, EditActionType.CommitText, transformedText);
                BridgeStateManager.ResetState();
            }
        }
    }

    private static void RequestEdit(BambooMintKeyTextService service, IntPtr pContext, EditActionType actionType, string text)
    {
        var session = new TextEditSession(service, pContext, actionType, text);
        var pSession = session.CreateNativeInstance();

        int hrSession = 0;
        var contextVTable = *(TfContextVTable**)pContext;
        contextVTable->RequestEditSession(pContext, service.ClientId, pSession, TsfEditFlags.TfEsSync | TsfEditFlags.TfEsReadWrite, &hrSession);

        var sessionPunk = *(TfEditSessionVTable**)pSession;
        sessionPunk->Release(pSession);
    }
}