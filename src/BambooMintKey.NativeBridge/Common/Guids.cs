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
}
