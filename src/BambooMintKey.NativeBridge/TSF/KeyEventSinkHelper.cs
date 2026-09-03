// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

// =========================================================================
// =========================================================================
// Cấu trúc & Hằng số cho ITfKeystrokeMgr::PreserveKey (Windows SDK msctf.h)
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public struct TF_PRESERVEDKEY
{
    public uint uVKey;
    public uint uModifiers;
}

public static class TsfModFlags
{
    public const uint Alt = 0x0001;
    public const uint Control = 0x0002;
    public const uint Shift = 0x0004;
    public const uint RAlt = 0x0008;
    public const uint RControl = 0x0010;
    public const uint RShift = 0x0020;
    public const uint LAlt = 0x0040;
    public const uint LControl = 0x0080;
    public const uint LShift = 0x0100;
    public const uint OnKeyUp = 0x0200;
    public const uint IgnoreAllModifier = 0x0400;
}

// =========================================================================
// VTable định nghĩa cho ITfKeystrokeMgr (Chuẩn 17 phương thức từ msctf.h)
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfKeystrokeMgrVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfKeystrokeMgr
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int, int> AdviseKeyEventSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseKeyEventSink;
    public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> GetForeground;
    public delegate* unmanaged[Stdcall]<IntPtr, UIntPtr, IntPtr, int*, int> TestKeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, UIntPtr, IntPtr, int*, int> TestKeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, UIntPtr, IntPtr, int*, int> KeyDown;
    public delegate* unmanaged[Stdcall]<IntPtr, UIntPtr, IntPtr, int*, int> KeyUp;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, TF_PRESERVEDKEY*, Guid*, int> GetPreservedKey;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, TF_PRESERVEDKEY*, int*, int> IsPreservedKey;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, TF_PRESERVEDKEY*, char*, uint, int> PreserveKey;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, TF_PRESERVEDKEY*, int> UnpreserveKey;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, char*, uint, int> SetPreservedKeyDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetPreservedKeyDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, int*, int> SimulatePreservedKey;
}

// =========================================================================
// KeyEventSinkHelper - Advise/Unadvise ITfKeyEventSink qua ITfKeystrokeMgr
// =========================================================================

/// <summary>
/// Helper đăng ký / gỡ đăng ký KeyEventSink và Preserved Keys với TSF Keystroke Manager.
/// </summary>
public static unsafe class KeyEventSinkHelper
{
    /// <summary>IID_ITfKeystrokeMgr. Lấy từ Windows SDK msctf.idl: uuid(aa80e7f0-2021-11d2-93e0-0060b067b86e).</summary>
    private static readonly Guid IidITfKeystrokeMgr = new("AA80E7F0-2021-11D2-93E0-0060B067B86E");

    /// <summary>
    /// Đăng ký KeyEventSink với TSF để nhận sự kiện bàn phím.
    /// Trả về cookie nếu thành công, 0 nếu thất bại.
    /// </summary>
    public static uint AdviseKeyEventSink(IntPtr pThreadMgr, uint clientId, IntPtr pKeyEventSink)
    {
        if (pThreadMgr == IntPtr.Zero || pKeyEventSink == IntPtr.Zero) return 0;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;
        
        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            DebugLog.Write($"QueryInterface(ITfKeystrokeMgr) HR=0x{hr:X8}, pKeystrokeMgr={pKeystrokeMgr}");
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return 0;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        // fForeground = 1 (Nhận sự kiện bàn phím ưu tiên mức Foreground)
        int adviseHr = pkmVTable->AdviseKeyEventSink(pKeystrokeMgr, clientId, pKeyEventSink, 1);
        DebugLog.Write($"AdviseKeyEventSink HR=0x{adviseHr:X8}");

        pkmVTable->Release(pKeystrokeMgr);
        return adviseHr == HResult.Ok ? 1u : 0u;
    }

    /// <summary>
    /// Gỡ đăng ký KeyEventSink khỏi TSF Keystroke Manager.
    /// </summary>
    public static void UnadviseKeyEventSink(IntPtr pThreadMgr, uint clientId)
    {
        if (pThreadMgr == IntPtr.Zero) return;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;

        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        pkmVTable->UnadviseKeyEventSink(pKeystrokeMgr, clientId);
        pkmVTable->Release(pKeystrokeMgr);
    }

    // Danh sách phím tắt chuyển đổi chế độ V/E cần đăng ký với Windows TSF
    private static readonly (uint vKey, uint modifiers, string desc)[] ToggleKeys =
    [
        (0x51 /* 'Q' */, TsfModFlags.Control | TsfModFlags.Shift, "BambooMintKey Toggle (Ctrl+Shift+Q)"),
        (0x5A /* 'Z' */, TsfModFlags.Alt, "BambooMintKey Toggle (Alt+Z)"),
        (0x20 /* Space */, TsfModFlags.Control, "BambooMintKey Toggle (Ctrl+Space)"),
        (0x10 /* Shift */, TsfModFlags.Control | TsfModFlags.OnKeyUp, "BambooMintKey Toggle (Ctrl+Shift)")
    ];

    /// <summary>
    /// Đăng ký các tổ hợp phím tắt chuẩn Preserved Key vào TSF Keystroke Manager.
    /// Khi người dùng bấm tổ hợp phím này, Windows TSF sẽ đánh chặn tự động và gọi OnPreservedKey.
    /// </summary>
    public static void RegisterPreservedKeys(IntPtr pThreadMgr, uint clientId)
    {
        if (pThreadMgr == IntPtr.Zero || clientId == TsfFlags.TfInvalidClientId) return;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;

        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        Guid guidToggle = Guids.GuidPreservedKeyToggle;

        foreach (var (vKey, modifiers, desc) in ToggleKeys)
        {
            TF_PRESERVEDKEY prekey = new() { uVKey = vKey, uModifiers = modifiers };
            fixed (char* pDesc = desc)
            {
                int hr = pkmVTable->PreserveKey(
                    pKeystrokeMgr,
                    clientId,
                    &guidToggle,
                    &prekey,
                    pDesc,
                    (uint)desc.Length);
                DebugLog.Write($"PreserveKey ({desc}) hr=0x{hr:X8}");
            }
        }

        pkmVTable->Release(pKeystrokeMgr);
    }

    /// <summary>
    /// Gỡ đăng ký các tổ hợp phím tắt Preserved Key khỏi TSF Keystroke Manager.
    /// </summary>
    public static void UnregisterPreservedKeys(IntPtr pThreadMgr, uint clientId)
    {
        if (pThreadMgr == IntPtr.Zero || clientId == TsfFlags.TfInvalidClientId) return;

        IntPtr pKeystrokeMgr = IntPtr.Zero;
        var punk = *(TfKeystrokeMgrVTable**)pThreadMgr;

        fixed (Guid* riid = &IidITfKeystrokeMgr)
        {
            int hr = punk->QueryInterface(pThreadMgr, riid, &pKeystrokeMgr);
            if (hr != HResult.Ok || pKeystrokeMgr == IntPtr.Zero) return;
        }

        var pkmVTable = *(TfKeystrokeMgrVTable**)pKeystrokeMgr;
        Guid guidToggle = Guids.GuidPreservedKeyToggle;

        foreach (var (vKey, modifiers, _) in ToggleKeys)
        {
            TF_PRESERVEDKEY prekey = new() { uVKey = vKey, uModifiers = modifiers };
            pkmVTable->UnpreserveKey(pKeystrokeMgr, &guidToggle, &prekey);
        }

        pkmVTable->Release(pKeystrokeMgr);
    }
}
