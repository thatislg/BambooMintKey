// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop;

/// <summary>
/// Các hằng số TSF Language Bar theo chuẩn đặt tên .NET (PascalCase).
/// Tên gốc Windows SDK được ghi chú thích chi tiết tương ứng.
/// </summary>
public static class TsfLangBarFlags
{
    // =========================================================================
    // TF_LANGBARITEMINFO.dwStyle (Kiểu hiển thị của Item)
    // =========================================================================

    /// <summary>[WinSDK: TF_LBI_STYLE_HIDDENSTATUSCONTROL (0x00000001)]</summary>
    public const uint TfLbiStyleHiddenStatusControl = 0x00000001;

    /// <summary>[WinSDK: TF_LBI_STYLE_SHOWNINTRAY (0x00000002)] - Buộc hiển thị trong khay hệ thống (Notification area / Language band).</summary>
    public const uint TfLbiStyleShownInTray = 0x00000002;

    /// <summary>[WinSDK: TF_LBI_STYLE_HIDEONNOOTHERITEMS (0x00000004)]</summary>
    public const uint TfLbiStyleHideOnNoOtherItems = 0x00000004;

    /// <summary>[WinSDK: TF_LBI_STYLE_SHOWNINTRAYONLY (0x00000008)]</summary>
    public const uint TfLbiStyleShownInTrayOnly = 0x00000008;

    /// <summary>[WinSDK: TF_LBI_STYLE_HIDDENBYDEFAULT (0x00000010)]</summary>
    public const uint TfLbiStyleHiddenByDefault = 0x00000010;

    /// <summary>[WinSDK: TF_LBI_STYLE_TEXTCOLORICON (0x00000020)]</summary>
    public const uint TfLbiStyleTextColorIcon = 0x00000020;

    /// <summary>[WinSDK: TF_LBI_STYLE_BTN_BUTTON (0x00010000)] - Nút bấm tiêu chuẩn (kích hoạt OnClick khi click).</summary>
    public const uint TfLbiStyleBtnButton = 0x00010000;

    /// <summary>[WinSDK: TF_LBI_STYLE_BTN_MENU (0x00020000)] - Nút hỗ trợ menu ngữ cảnh (kích hoạt InitMenu).</summary>
    public const uint TfLbiStyleBtnMenu = 0x00020000;

    /// <summary>[WinSDK: TF_LBI_STYLE_BTN_TOGGLE (0x00040000)]</summary>
    public const uint TfLbiStyleBtnToggle = 0x00040000;

    // =========================================================================
    // ITfLangBarItem::GetStatus pdwStatus (Trạng thái nút)
    // =========================================================================

    /// <summary>[WinSDK: TF_LBI_STATUS_HIDDEN (0x00000001)] - Ẩn nút khỏi Taskbar.</summary>
    public const uint TfLbiStatusHidden = 0x00000001;

    /// <summary>[WinSDK: TF_LBI_STATUS_DISABLED (0x00000002)] - Vô hiệu hóa tương tác chuột.</summary>
    public const uint TfLbiStatusDisabled = 0x00000002;

    /// <summary>[WinSDK: TF_LBI_STATUS_BTN_TOGGLED (0x00010000)]</summary>
    public const uint TfLbiStatusBtnToggled = 0x00010000;

    // =========================================================================
    // ITfLangBarItemSink::OnUpdate dwFlags (Cờ thông báo cập nhật)
    // =========================================================================

    /// <summary>[WinSDK: TF_LBI_ICON (0x00000001)] - Yêu cầu Windows gọi lại GetIcon để vẽ lại biểu tượng.</summary>
    public const uint TfLbiIcon = 0x00000001;

    /// <summary>[WinSDK: TF_LBI_TEXT (0x00000002)] - Yêu cầu Windows gọi lại GetText để cập nhật chữ hiển thị.</summary>
    public const uint TfLbiText = 0x00000002;

    /// <summary>[WinSDK: TF_LBI_TOOLTIP (0x00000004)] - Yêu cầu Windows gọi lại GetTooltipString để cập nhật tooltip text.</summary>
    public const uint TfLbiTooltip = 0x00000004;

    /// <summary>[WinSDK: TF_LBI_STATUS (0x00000008)] - Yêu cầu Windows gọi lại GetStatus.</summary>
    public const uint TfLbiStatus = 0x00000008;

    // =========================================================================
    // TfLBIClick (Enum loại click chuột trong OnClick)
    // =========================================================================

    /// <summary>[WinSDK: TF_LBI_CLK_RIGHT = 1] - Người dùng click chuột phải vào nút.</summary>
    public const uint TfLbiClkRight = 1;

    /// <summary>[WinSDK: TF_LBI_CLK_LEFT = 2] - Người dùng click chuột trái vào nút (chuyển chế độ V/E).</summary>
    public const uint TfLbiClkLeft = 2;
}

/// <summary>[WinSDK: struct TF_LANGBARITEMINFO]</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct TF_LANGBARITEMINFO
{
    public Guid clsidService;
    public Guid guidItem;
    public uint dwStyle;
    public uint ulSort;
    public fixed char szDescription[32];
}

/// <summary>[WinSDK: struct POINT (8 bytes)]</summary>
[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

/// <summary>[WinSDK: struct RECT]</summary>
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

/// <summary>Cấu trúc vùng nhớ Native kép chứa 2 VTable: Button và Source.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LangBarButtonNativeLayout
{
    public IntPtr VTableButton;
    public IntPtr VTableSource;
}

/// <summary>
/// VTable cho ITfLangBarItemButton (kế thừa ITfLangBarItem -> IUnknown).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfLangBarItemButtonVTable
{
    // --- IUnknown (Slot 0 - 2) ---
    /// <summary>[WinSDK: IUnknown::QueryInterface]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    /// <summary>[WinSDK: IUnknown::AddRef]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    /// <summary>[WinSDK: IUnknown::Release]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // --- ITfLangBarItem (Slot 3 - 6) ---
    /// <summary>[WinSDK: ITfLangBarItem::GetInfo]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, TF_LANGBARITEMINFO*, int> GetInfo;
    /// <summary>[WinSDK: ITfLangBarItem::GetStatus]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> GetStatus;
    /// <summary>[WinSDK: ITfLangBarItem::Show]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, int, int> Show;
    /// <summary>[WinSDK: ITfLangBarItem::GetTooltipString]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetTooltipString;

    // --- ITfLangBarItemButton (Slot 7 - 11) ---
    /// <summary>[WinSDK: ITfLangBarItemButton::OnClick (Slot 7)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, POINT, RECT*, int> OnClick;
    /// <summary>[WinSDK: ITfLangBarItemButton::InitMenu (Slot 8)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> InitMenu;
    /// <summary>[WinSDK: ITfLangBarItemButton::OnMenuSelect (Slot 9)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> OnMenuSelect;
    /// <summary>[WinSDK: ITfLangBarItemButton::GetIcon (Slot 10)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetIcon;
    /// <summary>[WinSDK: ITfLangBarItemButton::GetText (Slot 11)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetText;
}

/// <summary>
/// VTable cho ITfSource (nhận kết nối Sink từ Windows Language Bar Manager).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfSourceVTable
{
    // --- IUnknown (Slot 0 - 2) ---
    /// <summary>[WinSDK: IUnknown::QueryInterface]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    /// <summary>[WinSDK: IUnknown::AddRef]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    /// <summary>[WinSDK: IUnknown::Release]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // --- ITfSource (Slot 3 - 4) ---
    /// <summary>[WinSDK: ITfSource::AdviseSink (Slot 3)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr, uint*, int> AdviseSink;
    /// <summary>[WinSDK: ITfSource::UnadviseSink (Slot 4)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseSink;
}

/// <summary>
/// VTable cho ITfLangBarItemSink (do Windows triển khai, chúng ta gọi OnUpdate).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfLangBarItemSinkVTable
{
    // --- IUnknown (Slot 0 - 2) ---
    /// <summary>[WinSDK: IUnknown::QueryInterface]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    /// <summary>[WinSDK: IUnknown::AddRef]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    /// <summary>[WinSDK: IUnknown::Release]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // --- ITfLangBarItemSink (Slot 3) ---
    /// <summary>[WinSDK: ITfLangBarItemSink::OnUpdate (Slot 3)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> OnUpdate;
}

/// <summary>
/// VTable cho ITfLangBarItemMgr (chuẩn xác 100% theo thứ tự 15 slot trong ctfutb.h).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfLangBarItemMgrVTable
{
    // --- IUnknown (Slot 0 - 2) ---
    /// <summary>[WinSDK: IUnknown::QueryInterface (Slot 0)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    /// <summary>[WinSDK: IUnknown::AddRef (Slot 1)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    /// <summary>[WinSDK: IUnknown::Release (Slot 2)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // --- ITfLangBarItemMgr (Slot 3 - 14) ---
    /// <summary>[WinSDK: ITfLangBarItemMgr::EnumItems (Slot 3)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumItems;
    /// <summary>[WinSDK: ITfLangBarItemMgr::GetItem (Slot 4)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetItem;
    /// <summary>[WinSDK: ITfLangBarItemMgr::AddItem (Slot 5)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> AddItem;
    /// <summary>[WinSDK: ITfLangBarItemMgr::RemoveItem (Slot 6)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> RemoveItem;
    /// <summary>[WinSDK: ITfLangBarItemMgr::AdviseItemSink (Slot 7)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint*, Guid*, int> AdviseItemSink;
    /// <summary>[WinSDK: ITfLangBarItemMgr::UnadviseItemSink (Slot 8)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseItemSink;
    /// <summary>[WinSDK: ITfLangBarItemMgr::GetItemFloatingRect (Slot 9)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, RECT*, int> GetItemFloatingRect;
    /// <summary>[WinSDK: ITfLangBarItemMgr::GetItemsStatus (Slot 10)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, uint*, int> GetItemsStatus;
    /// <summary>[WinSDK: ITfLangBarItemMgr::GetItemNum (Slot 11)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> GetItemNum;
    /// <summary>[WinSDK: ITfLangBarItemMgr::GetItems (Slot 12)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, TF_LANGBARITEMINFO*, uint*, uint*, int> GetItems;
    /// <summary>[WinSDK: ITfLangBarItemMgr::AdviseItemsSink (Slot 13)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, Guid*, uint*, int> AdviseItemsSink;
    /// <summary>[WinSDK: ITfLangBarItemMgr::UnadviseItemsSink (Slot 14)]</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint*, int> UnadviseItemsSink;
}
