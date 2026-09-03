// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.NativeBridge.Common;

/// <summary>
/// Tập hợp toàn bộ GUID cố định dùng cho COM Class, TSF Profile, TSF Categories
/// và các interface IID theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// Các giá trị này phải khớp với Registry COM/TSF đã đăng ký.
/// </summary>
public static class Guids
{
    // =========================================================================
    // CLSID của Text Service chính (BambooMintKey TIP)
    // =========================================================================

    /// <summary>CLSID của Text Service chính BambooMintKey TIP.</summary>
    public static readonly Guid TextServiceClsid = new("B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1");

    /// <summary>Profile GUID phân biệt phiên bản kiểu gõ Telex.</summary>
    public static readonly Guid ProfileGuid = new("C2F31A8E-92D0-4F81-9C3E-A52889211D44");

    // =========================================================================
    // TSF Category GUIDs (Chuẩn Windows TSF)
    // =========================================================================

    /// <summary>GUID_TFCAT_TIP_KEYBOARD - Đăng ký bộ gõ bàn phím.</summary>
    public static readonly Guid GuidTfCategoryTipKeyboard = new("34745C63-B2F0-4784-8B67-5E12C8701A31");

    /// <summary>GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER - Cung cấp hiển thị gạch chân composition.</summary>
    public static readonly Guid GuidTfCategoryDisplayAttributeProvider = new("35E7A704-438C-4235-96BC-4A6361C31595");

    /// <summary>GUID_TFCAT_TIPCAP_IMMERSIVESUPPORT - Khai báo tương thích Windows 8/10/11 shell & UWP/XAML Input Indicator.</summary>
    public static readonly Guid GuidTfCatTipCapImmersiveSupport = new("13A016DF-560B-46CD-947A-4C3AF1E0E35D");

    /// <summary>GUID_TFCAT_TIPCAP_SYSTRAYSUPPORT - Khai báo hiển thị icon mode ngay trên System Tray / Taskbar Input Indicator.</summary>
    public static readonly Guid GuidTfCatTipCapSystraySupport = new("25504FB4-7BAB-4BC1-9C69-CF81890F0EF5");

    /// <summary>GUID_TFCAT_TIPCAP_INPUTMODECOMPARTMENT - Khai báo hỗ trợ Input Mode Compartment.</summary>
    public static readonly Guid GuidTfCatTipCapInputModeCompartment = new("CCF05DD7-4A87-11D7-A6E2-00065B84435C");

    /// <summary>GUID_TFCAT_TIPCAP_UIELEMENTENABLED - Khai báo hỗ trợ UIElements.</summary>
    public static readonly Guid GuidTfCatTipCapUiElementEnabled = new("49D2F9CF-1F5E-11D7-A6D3-00065B84435C");

    // =========================================================================
    // COM Standard Interface GUIDs
    // =========================================================================

    /// <summary>IID_IUnknown (00000000-0000-0000-C000-000000000046).</summary>
    public static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");

    /// <summary>IID_IClassFactory (00000001-0000-0000-C000-000000000046).</summary>
    public static readonly Guid IidIClassFactory = new("00000001-0000-0000-C000-000000000046");

    // =========================================================================
    // TSF Interface GUIDs
    // =========================================================================

    /// <summary>IID_ITfTextInputProcessorEx - Vòng đời TIP chính (002_02).</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(6e4e2102-f9cd-433d-b496-303ce03a6507).</remarks>
    public static readonly Guid IidITfTextInputProcessorEx = new("6E4E2102-F9CD-433D-B496-303CE03A6507");

    /// <summary>IID_ITfTextInputProcessor - Interface cơ bản của TIP.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(aa80e7f7-2021-11d2-93e0-0060b067b86e).</remarks>
    public static readonly Guid IidITfTextInputProcessor = new("AA80E7F7-2021-11D2-93E0-0060B067B86E");

    /// <summary>IID_ITfThreadMgrEventSink - Sự kiện thay đổi focus/context (002_02).</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(aa80e80e-2021-11d2-93e0-0060b067b86e).</remarks>
    public static readonly Guid IidITfThreadMgrEventSink = new("AA80E80E-2021-11D2-93E0-0060B067B86E");

    /// <summary>IID_ITfKeyEventSink - Sự kiện bàn phím (002_03).</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(aa80e7f5-2021-11d2-93e0-0060b067b86e).</remarks>
    public static readonly Guid IidITfKeyEventSink = new("AA80E7F5-2021-11D2-93E0-0060B067B86E");

    /// <summary>IID_ITfEditSession - Thao tác văn bản trong edit cookie (002_04).</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(aa80e803-2021-11d2-93e0-0060b067b86e).</remarks>
    public static readonly Guid IidITfEditSession = new("AA80E803-2021-11D2-93E0-0060B067B86E");

    /// <summary>IID_ITfContextComposition - Quản lý composition trên ITfContext.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(d40c8aae-ac92-4fc7-9a11-0ee0e23aa39b).</remarks>
    public static readonly Guid IidITfContextComposition = new("D40C8AAE-AC92-4FC7-9A11-0EE0E23AA39B");

    /// <summary>IID_ITfComposition - Phiên làm việc composition.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(20168d64-5a8f-4a5a-b7bd-cfa29f4d0fd9).</remarks>
    public static readonly Guid IidITfComposition = new("20168D64-5A8F-4A5A-B7BD-CFA29F4D0FD9");

    /// <summary>IID_ITfCompositionSink - Nhận thông báo kết thúc composition.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(a781718c-579a-4b15-a280-32b8577acc5e).</remarks>
    public static readonly Guid IidITfCompositionSink = new("A781718C-579A-4B15-A280-32B8577ACC5E");

    /// <summary>IID_ITfSource - Đăng ký và gỡ đăng ký event sink chung.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(4ea48a35-60ae-446f-8fd6-e6a8d82459f7).</remarks>
    public static readonly Guid IidITfSource = new("4EA48A35-60AE-446F-8FD6-E6A8D82459F7");

    // =========================================================================
    // TSF Language Bar GUIDs (Windows SDK ctfutb.h & ctffunc.h - 100% chuẩn xác)
    // =========================================================================

    /// <summary>IID_ITfLangBarItem - Interface cơ bản của một item trên Language Bar.</summary>
    /// <remarks>Lấy từ Windows SDK ctfutb.h: uuid(73540d69-edeb-4ee9-96c9-23aa30b25916).</remarks>
    public static readonly Guid IidITfLangBarItem = new("73540D69-EDEB-4EE9-96C9-23AA30B25916");

    /// <summary>IID_ITfLangBarItemButton - Interface nút bấm trên Language Bar.</summary>
    /// <remarks>Lấy từ Windows SDK ctfutb.h: uuid(28c7f1d0-de25-11d2-afdd-00105a2799b5).</remarks>
    public static readonly Guid IidITfLangBarItemButton = new("28C7F1D0-DE25-11D2-AFDD-00105A2799B5");

    /// <summary>IID_ITfLangBarItemSink - Nhận thông báo cập nhật icon/text/tooltip từ item.</summary>
    /// <remarks>Lấy từ Windows SDK ctfutb.h: uuid(57dbe1a0-de25-11d2-afdd-00105a2799b5).</remarks>
    public static readonly Guid IidITfLangBarItemSink = new("57DBE1A0-DE25-11D2-AFDD-00105A2799B5");

    /// <summary>IID_ITfLangBarItemMgr - Quản lý cài đặt/gỡ bỏ item trên Language Bar.</summary>
    /// <remarks>Lấy từ Windows SDK ctfutb.h: uuid(ba468c55-9956-4fb1-a59d-52a7dd7cc6aa).</remarks>
    public static readonly Guid IidITfLangBarItemMgr = new("BA468C55-9956-4FB1-A59D-52A7DD7CC6AA");

    /// <summary>CLSID_TF_LangBarItemMgr - Class ID khởi tạo Language Bar Item Manager.</summary>
    /// <remarks>Lấy từ Windows SDK ctfutb.h: uuid(b9931692-a2b3-4fab-bf33-9ec6f9fb96ac).</remarks>
    public static readonly Guid ClsidTfLangBarItemMgr = new("B9931692-A2B3-4FAB-BF33-9EC6F9FB96AC");

    /// <summary>GUID_LBI_INPUTMODE - Bắt buộc trên Windows 8/10/11 để Taskbar hiển thị icon của IME trong system tray.</summary>
    /// <remarks>Lấy từ Windows SDK ctffunc.h: uuid(2c77a81e-41cc-4178-a3a7-5f8a987568e6).</remarks>
    public static readonly Guid GuidLbiInputMode = new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");

    /// <summary>GUID định danh cho Preserved Key chuyển đổi chế độ V/E trong TSF.</summary>
    public static readonly Guid GuidPreservedKeyToggle = new("E58A4372-B147-49D6-8C45-76DF53E65B01");

    // =========================================================================
    // TSF Compartment GUIDs (Windows SDK msctf.h & ctffunc.h)
    // =========================================================================

    /// <summary>IID_ITfCompartmentMgr - Quản lý các compartment trong TSF.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(7dcf57ac-18ad-438b-824d-979bffb74b7c).</remarks>
    public static readonly Guid IidITfCompartmentMgr = new("7DCF57AC-18AD-438B-824D-979BFFB74B7C");

    /// <summary>IID_ITfCompartment - Truy cập và gán giá trị một compartment cụ thể.</summary>
    /// <remarks>Lấy từ Windows SDK msctf.idl: uuid(bb80d7d3-0144-42b3-8419-5a90924c7823).</remarks>
    public static readonly Guid IidITfCompartment = new("BB80D7D3-0144-42B3-8419-5A90924C7823");

    /// <summary>GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION - Trạng thái Conversion mode (V/E).</summary>
    /// <remarks>Lấy từ Windows SDK ctffunc.h: uuid(ccf05dd7-4a87-11d7-a6e2-00065b84435c).</remarks>
    public static readonly Guid GuidCompartmentKeyboardInputModeConversion = new("CCF05DD7-4A87-11D7-A6E2-00065B84435C");

    /// <summary>GUID_COMPARTMENT_KEYBOARD_OPENCLOSE - Trạng thái Open/Close của bộ gõ.</summary>
    /// <remarks>Lấy từ Windows SDK ctffunc.h: uuid(a3ce0321-4a8c-11d7-a6e2-00065b84435c).</remarks>
    public static readonly Guid GuidCompartmentKeyboardOpenClose = new("A3CE0321-4A8C-11D7-A6E2-00065B84435C");
}
