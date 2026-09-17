// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Interop;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.NativeBridge.Common;

/// <summary>
/// API trung tâm (single source of truth) cho trạng thái V/E toàn hệ thống.
/// Đảm bảo icon hiển thị, TSF compartment, và khả năng gõ tiếng Việt của engine
/// luôn nhất quán với SharedMemoryManager.IsVietnameseMode (byte offset 0).
/// </summary>
public static unsafe class GlobalVEState
{
    [Flags]
    public enum SyncTarget
    {
        None = 0,
        SharedMemory = 1,
        Compartment = 2,
        Icon = 4,
        All = SharedMemory | Compartment | Icon
    }

    /// <summary>
    /// Đọc trạng thái V/E hiện tại từ shared memory.
    /// Đây là single source of truth duy nhất.
    /// </summary>
    public static bool IsVietnameseMode => SharedMemoryManager.IsVietnameseMode;

    /// <summary>
    /// Đặt trạng thái V/E mới và đồng bộ toàn bộ các thành phần.
    /// Đây là cổng duy nhất được phép thay đổi state từ UI / menu / click icon.
    /// </summary>
    public static bool SetVietnameseMode(bool value, SyncTarget target, IntPtr pThreadMgr = default, uint clientId = 0)
    {
        bool actual = SharedMemoryManager.AtomicSetVietnameseMode(value);
        Synchronize(target, pThreadMgr, clientId);
        DebugLog.Write($"GlobalVEState.SetVietnameseMode: requested={value}, actual={actual}, target={target}");
        return actual;
    }

    /// <summary>
    /// Đảo trạng thái V/E một cách atomic. Nếu phát hiện state đã bị đổi bởi
    /// process/thread khác trong khoảng đọc-ghi, hủy bỏ toggle và trả về state thực tế.
    /// </summary>
    public static bool ToggleVietnameseMode(SyncTarget target, IntPtr pThreadMgr = default, uint clientId = 0)
    {
        bool actual = SharedMemoryManager.AtomicToggleVietnameseMode();
        Synchronize(target, pThreadMgr, clientId);
        DebugLog.Write($"GlobalVEState.ToggleVietnameseMode: actual={actual}, target={target}");
        return actual;
    }

    /// <summary>
    /// Buộc đồng bộ compartment và icon với shared memory.
    /// Dùng khi nhận event từ process khác, hoặc khi nghi ngờ lệch pha.
    /// </summary>
    public static void ResyncFromSharedMemory(IntPtr pThreadMgr, uint clientId)
    {
        Synchronize(SyncTarget.Compartment | SyncTarget.Icon, pThreadMgr, clientId);
        DebugLog.Write($"GlobalVEState.ResyncFromSharedMemory: IsVietnameseMode={IsVietnameseMode}");
    }

    private static void Synchronize(SyncTarget target, IntPtr pThreadMgr, uint clientId)
    {
        if ((target & SyncTarget.Compartment) != 0 && pThreadMgr != IntPtr.Zero)
        {
            TsfCompartmentHelper.SetConversionMode(pThreadMgr, clientId, IsVietnameseMode);
        }

        if ((target & SyncTarget.Icon) != 0)
        {
            LangBarItemButton.NotifyStateChanged();
        }
    }
}
