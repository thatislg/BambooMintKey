// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cờ kích hoạt TSF Text Service và giá trị đặc biệt.
/// Theo thiết kế 002_02_TSF_TextInputProcessor_Lifecycle.md.
/// </summary>
public static class TsfFlags
{
    /// <summary>TF_TMAE_NOACTIVATETIP - Không tự động kích hoạt TIP.</summary>
    public const uint TfTmaeNoactivatetip = 0x00000001;

    /// <summary>TF_TMAE_SECUREMODE - Chế độ bảo mật ( không UI ).</summary>
    public const uint TfTmaeSecuremode = 0x00000002;

    /// <summary>TF_TMAE_UIELEMENTENABLEDONLY - Chỉ bật UI element.</summary>
    public const uint TfTmaeUielementenabledonly = 0x00000004;

    /// <summary>TF_INVALID_CLIENT_ID - Client ID không hợp lệ.</summary>
    public const uint TfInvalidClientId = 0;
}

// =========================================================================
// VTable định nghĩa cho ITfTextInputProcessor / ITfTextInputProcessorEx
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfTextInputProcessorExVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfTextInputProcessor
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int> Activate;
    public delegate* unmanaged[Stdcall]<IntPtr, int> Deactivate;

    // ITfTextInputProcessorEx
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, int> ActivateEx;
}

// =========================================================================
// VTable định nghĩa cho ITfThreadMgrEventSink
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfThreadMgrEventSinkVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfThreadMgrEventSink
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int> OnInitDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> OnUninitDocumentMgr;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int> OnSetFocus;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> OnPushContext;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> OnPopContext;
}
