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
    public static readonly Guid GuidTfCategoryTipKeyboard = new("34745C63-B2F0-4784-8B67-5E12E8701A31");

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
    public static readonly Guid IidITfTextInputProcessorEx = new("AABEC164-429C-4234-A75D-4E90B01D77D1");

    /// <summary>IID_ITfTextInputProcessor - Interface cơ bản của TIP.</summary>
    public static readonly Guid IidITfTextInputProcessor = new("AA80E7D5-2021-11D2-93E0-0060B067B86E");

    /// <summary>IID_ITfThreadMgrEventSink - Sự kiện thay đổi focus/context (002_02).</summary>
    public static readonly Guid IidITfThreadMgrEventSink = new("30B573D0-CCFA-11D2-9A86-00AA006EFD5E");

    /// <summary>IID_ITfKeyEventSink - Sự kiện bàn phím (002_03).</summary>
    public static readonly Guid IidITfKeyEventSink = new("AA80E7F5-2021-11D2-93E0-0060B067B86E");

    /// <summary>IID_ITfEditSession - Thao tác văn bản trong edit cookie (002_04).</summary>
    public static readonly Guid IidITfEditSession = new("AA80E7FD-2021-11D2-93E0-0060B067B86E");
}
