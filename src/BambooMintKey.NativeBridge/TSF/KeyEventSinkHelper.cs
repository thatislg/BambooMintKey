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
public struct TfPreservedkey
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
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, TfPreservedkey*, Guid*, int> GetPreservedKey;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, TfPreservedkey*, int*, int> IsPreservedKey;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, TfPreservedkey*, char*, uint, int> PreserveKey;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, TfPreservedkey*, int> UnpreserveKey;
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

    // Danh sách tất cả phím tắt được bảo lưu cố định (chuẩn Google IME / Mozc TSF)
    private static readonly (uint vKey, uint modifiers)[] AllPossibleKeys =
    [
        (0x19 /* VK_KANJI */, TsfModFlags.IgnoreAllModifier),
        (0xF3 /* VK_OEM_AUTO */, TsfModFlags.IgnoreAllModifier),
        (0xF4 /* VK_OEM_ENLW */, TsfModFlags.IgnoreAllModifier),
        (0xC0 /* VK_OEM_3 - Key dưới Esc */, TsfModFlags.Alt),
        (0xC0 /* VK_OEM_3 - Key dưới Esc */, TsfModFlags.Control),
        (0xC0 /* VK_OEM_3 - Key dưới Esc */, 0),
        (0x10 /* VK_SHIFT */, TsfModFlags.Control | TsfModFlags.OnKeyUp),
        (0x11 /* VK_CONTROL */, TsfModFlags.Shift | TsfModFlags.OnKeyUp),
        (0x5A /* 'Z' */, TsfModFlags.Alt),
        (0x20 /* Space */, TsfModFlags.Control)
    ];

    private static TfPreservedkey _lastCustomKey = new() { uVKey = 0, uModifiers = 0 };

    /// <summary>
    /// Lấy danh sách phím tắt chuyển đổi V/E mặc định cố định theo vị trí phím vật lý dưới Esc:
    /// - Phím `/~ (0xC0) trên bàn phím tiếng Anh (bấm trực tiếp hoặc Alt+~ / Ctrl+~)
    /// - Phím 半角/全角 / 漢字 (0x19, 0xF3, 0xF4) trên bàn phím tiếng Nhật
    /// Không cho phép thay đổi hay reset.
    /// </summary>
    private static (uint vKey, uint modifiers, string desc)[] GetActiveToggleKeys()
    {
        return
        [
            (0x19 /* VK_KANJI */, TsfModFlags.IgnoreAllModifier, "BambooMintKey Toggle (JP Hankaku/Zenkaku)"),
            (0xF3 /* VK_OEM_AUTO */, TsfModFlags.IgnoreAllModifier, "BambooMintKey Toggle (JP OEM Auto)"),
            (0xF4 /* VK_OEM_ENLW */, TsfModFlags.IgnoreAllModifier, "BambooMintKey Toggle (JP OEM Enlw)"),
            (0xC0 /* VK_OEM_3 */, TsfModFlags.Alt, "BambooMintKey Toggle (Alt+~ below Esc)"),
            (0xC0 /* VK_OEM_3 */, TsfModFlags.Control, "BambooMintKey Toggle (Ctrl+~ below Esc)"),
            (0xC0 /* VK_OEM_3 */, 0, "BambooMintKey Toggle (`/~ below Esc)")
        ];
    }

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

        var activeKeys = GetActiveToggleKeys();
        foreach (var (vKey, modifiers, desc) in activeKeys)
        {
            TfPreservedkey prekey = new() { uVKey = vKey, uModifiers = modifiers };
            _lastCustomKey = prekey;
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

        if (_lastCustomKey.uVKey != 0 || _lastCustomKey.uModifiers != 0)
        {
            TfPreservedkey lastKey = _lastCustomKey;
            pkmVTable->UnpreserveKey(pKeystrokeMgr, &guidToggle, &lastKey);
            _lastCustomKey = new() { uVKey = 0, uModifiers = 0 };
        }

        foreach (var (vKey, modifiers) in AllPossibleKeys)
        {
            TfPreservedkey prekey = new() { uVKey = vKey, uModifiers = modifiers };
            pkmVTable->UnpreserveKey(pKeystrokeMgr, &guidToggle, &prekey);
        }

        pkmVTable->Release(pKeystrokeMgr);
    }

    /// <summary>
    /// Cập nhật lại phím tắt khi cấu hình thay đổi (Gỡ phím cũ, nạp phím mới).
    /// </summary>
    public static void UpdatePreservedKeys(IntPtr pThreadMgr, uint clientId)
    {
        UnregisterPreservedKeys(pThreadMgr, clientId);
        RegisterPreservedKeys(pThreadMgr, clientId);
    }
}
