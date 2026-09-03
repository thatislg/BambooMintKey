# 003_03_TaskbarButton_COM.md

> Tài liệu kỹ thuật chi tiết về việc cài đặt COM Interface `ITfLangBarItemButton` & `ITfSource`, kết nối khay hệ thống (Taskbar Language Bar) và quản lý vòng đời nút bấm cho BambooMintKey.
> Đã được chuẩn hóa theo phong cách .NET (PascalCase) cho các hằng số, ghi chú thích đầy đủ tên hàm/hằng gốc từ Windows 10/11 SDK (`ctfutb.idl`, `ctfutb.h`).

---

## 1. Cơ sở chuẩn hóa từ Windows SDK

Toàn bộ định nghĩa interface và cấu trúc dữ liệu được trích xuất trực tiếp từ file Windows SDK gốc: `C:\Program Files (x86)\Windows Kits\10\Include\<version>\um\ctfutb.idl` và `ctfutb.h`.

### 1.1. Bảng tra cứu GUID chuẩn

| Tên biến C# (.NET Style)    | Định nghĩa Windows SDK | GUID chuẩn xác                         | Ghi chú / Mục đích |
| --------------------------- | ---------------------- | -------------------------------------- | ------------------ |
| `Guids.IidIUnknown`         | `IID_IUnknown`         | `00000000-0000-0000-C000-000000000046` | Interface COM cơ sở |
| `Guids.IidITfLangBarItem`   | `IID_ITfLangBarItem`   | `73540D69-EDEB-4EE9-96C9-23AA30B25916` | Interface item thanh ngôn ngữ cơ bản (ctfutb.h) |
| `Guids.IidITfLangBarItemButton` | `IID_ITfLangBarItemButton` | `28C7F1D0-DE25-11D2-AFDD-00105A2799B5` | Nút bấm Language Bar (V/E) (ctfutb.h) |
| `Guids.IidITfSource`        | `IID_ITfSource`        | `4EA48A35-60AE-446F-8FD6-E6A8D82459F7` | Điểm kết nối Sink từ Windows |
| `Guids.IidITfLangBarItemSink` | `IID_ITfLangBarItemSink` | `57DBE1A0-DE25-11D2-AFDD-00105A2799B5` | Interface thông báo cập nhật cho Windows (ctfutb.h) |
| `Guids.IidITfLangBarItemMgr` | `IID_ITfLangBarItemMgr` | `BA468C55-9956-4FB1-A59D-52A7DD7CC6AA` | Quản lý item Language Bar của Windows (ctfutb.h) |
| `Guids.ClsidTfLangBarItemMgr` | `CLSID_TF_LangBarItemMgr` | `B9931692-A2B3-4FAB-BF33-9EC6F9FB96AC` | COM CoCreateInstance fallback (ctfutb.h) |
| `Guids.GuidLbiInputMode`    | `GUID_LBI_INPUTMODE`   | `2C77A81E-41CC-4178-A3A7-5F8A987568E6` | Bắt buộc trên Win 8/10/11 để hiển thị icon IME (ctffunc.h) |

### 1.2. Hằng số Flag & Enum theo chuẩn .NET (`TsfLangBarFlags`)

Các hằng số được định danh theo quy ước .NET (PascalCase), kèm chú thích tên hằng số gốc trong Windows SDK:

```csharp
namespace BambooMintKey.NativeBridge.Common;

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
```

---

## 2. Thiết kế cấu trúc VTable & Native Layout C# NativeAOT

Nút bấm `LangBarItemButton` hoạt động như một COM Object hỗ trợ 2 giao diện song song:
1. `ITfLangBarItemButton` (kế thừa `ITfLangBarItem` $\rightarrow$ `IUnknown`): Xử lý hiển thị và tương tác người dùng.
2. `ITfSource`: Cho phép Windows gắn `ITfLangBarItemSink` để nhận tín hiệu cập nhật icon/tooltip khi chế độ gõ thay đổi.

### 2.1. Cấu trúc Native Layout kép (`LangBarButtonNativeLayout`)

Tương tự thiết kế `BambooMintKeyTextService`, đối tượng COM được cấp phát vùng nhớ Native chứa mảng con trỏ VTable và GCHandle:

```
+------------------------------------+ <--- Con trỏ _comInstance (trả về cho Windows)
| IntPtr VTableButton                | ---> Trỏ tới ITfLangBarItemButtonVTable
+------------------------------------+ <--- Offset +sizeof(IntPtr) (khi QI IID_ITfSource)
| IntPtr VTableSource                | ---> Trỏ tới TfSourceVTable
+------------------------------------+
```

### 2.2. Khai báo Cấu trúc Struct & VTable (`Interop/TsfLangBarTypes.cs`)

Tên hàm gốc của WinSDK được chú thích chi tiết tại từng delegate:

```csharp
using System;
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop
{
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
}
```

---

## 3. Cài đặt Implementation `LangBarItemButton`

Lớp quản lý nút bấm tích hợp sẵn `ITfSource`, kết nối trực tiếp với `BridgeStateManager` theo chuẩn đặt tên .NET (`TSF/LangBarItemButton.cs`).

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF
{
    public static unsafe class LangBarItemButton
    {
        private static ITfLangBarItemButtonVTable* _buttonVTable;
        private static TfSourceVTable* _sourceVTable;
        private static IntPtr _comInstance;

        // Con trỏ tới ITfLangBarItemSink mà Windows cung cấp qua ITfSource::AdviseSink
        private static IntPtr _pLangBarSink = IntPtr.Zero;
        private static uint _sinkCookie = 0;
        private static IntPtr _langBarMgr = IntPtr.Zero;

        static LangBarItemButton()
        {
            InitializeVTables();

            // Cấp phát vùng nhớ Native Layout kép (Slot 0: Button, Slot 1: Source)
            var layout = (LangBarButtonNativeLayout*)NativeMemory.Alloc((nuint)sizeof(LangBarButtonNativeLayout));
            layout->VTableButton = (IntPtr)_buttonVTable;
            layout->VTableSource = (IntPtr)_sourceVTable;
            _comInstance = (IntPtr)layout;
        }

        private static void InitializeVTables()
        {
            // 1. VTable cho ITfLangBarItemButton
            _buttonVTable = (ITfLangBarItemButtonVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(LangBarItemButton), sizeof(ITfLangBarItemButtonVTable));

            _buttonVTable->QueryInterface = &QueryInterface;
            _buttonVTable->AddRef = &AddRef;
            _buttonVTable->Release = &Release;

            _buttonVTable->GetInfo = &GetInfo;
            _buttonVTable->GetStatus = &GetStatus;
            _buttonVTable->Show = &Show;
            _buttonVTable->GetTooltipString = &GetTooltipString;

            _buttonVTable->OnClick = &OnClick;
            _buttonVTable->InitMenu = &InitMenu;
            _buttonVTable->OnMenuSelect = &OnMenuSelect;
            _buttonVTable->GetIcon = &GetIcon;
            _buttonVTable->GetText = &GetText;

            // 2. VTable cho ITfSource
            _sourceVTable = (TfSourceVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(LangBarItemButton), sizeof(TfSourceVTable));

            _sourceVTable->QueryInterface = &QueryInterface_Source;
            _sourceVTable->AddRef = &AddRef_Source;
            _sourceVTable->Release = &Release_Source;
            _sourceVTable->AdviseSink = &AdviseSink;
            _sourceVTable->UnadviseSink = &UnadviseSink;
        }

        public static IntPtr Instance => _comInstance;

        // =====================================================================
        // IUnknown Implementation (Dual-Interface Routing)
        // =====================================================================
        private static int QueryInterfaceImpl(IntPtr rootPtr, Guid* riid, IntPtr* ppv)
        {
            if (ppv == null || riid == null) return HResult.Pointer;
            *ppv = IntPtr.Zero;

            // [WinSDK: QueryInterface cho ITfLangBarItem & ITfLangBarItemButton]
            if (*riid == Guids.IidIUnknown ||
                *riid == Guids.IidITfLangBarItem ||
                *riid == Guids.IidITfLangBarItemButton)
            {
                *ppv = rootPtr;
                AddRef(rootPtr);
                return HResult.Ok;
            }

            // [WinSDK: QueryInterface cho ITfSource]
            if (*riid == Guids.IidITfSource)
            {
                *ppv = rootPtr + sizeof(IntPtr);
                AddRef(rootPtr);
                return HResult.Ok;
            }

            return HResult.NoInterface;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
            => QueryInterfaceImpl(thisPtr, riid, ppv);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint AddRef(IntPtr thisPtr) => 2;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Release(IntPtr thisPtr) => 1;

        // Proxy IUnknown cho Slot 1 (ITfSource)
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int QueryInterface_Source(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
            => QueryInterfaceImpl(thisPtr - sizeof(IntPtr), riid, ppv);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint AddRef_Source(IntPtr thisPtr) => 2;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Release_Source(IntPtr thisPtr) => 1;

        // =====================================================================
        // ITfLangBarItem Implementation
        // =====================================================================

        /// <summary>[WinSDK: ITfLangBarItem::GetInfo] - Cung cấp thông tin cấu hình nút cho Windows.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetInfo(IntPtr thisPtr, TF_LANGBARITEMINFO* pInfo)
        {
            if (pInfo == null) return HResult.InvalidArgument;

            pInfo->clsidService = Guids.TextServiceClsid;
            pInfo->guidItem = Guids.GuidLbiBambooMintKeyMode;
            pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnButton |
                             TsfLangBarFlags.TfLbiStyleBtnMenu |
                             TsfLangBarFlags.TfLbiStyleShownInTray;
            pInfo->ulSort = 0;

            string desc = "BambooMintKey Mode";
            fixed (char* src = desc)
            {
                for (int i = 0; i < desc.Length && i < 31; i++)
                {
                    pInfo->szDescription[i] = src[i];
                }
                pInfo->szDescription[Math.Min(desc.Length, 31)] = '\0';
            }

            return HResult.Ok;
        }

        /// <summary>[WinSDK: ITfLangBarItem::GetStatus] - Trả về trạng thái hiện tại (Enabled/Disabled/Hidden).</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetStatus(IntPtr thisPtr, uint* pdwStatus)
        {
            if (pdwStatus == null) return HResult.InvalidArgument;
            *pdwStatus = 0; // Nút luôn enabled và hiển thị bình thường
            return HResult.Ok;
        }

        /// <summary>[WinSDK: ITfLangBarItem::Show] - Yêu cầu ẩn/hiện nút từ Windows.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Show(IntPtr thisPtr, int fShow) => HResult.Ok;

        /// <summary>[WinSDK: ITfLangBarItem::GetTooltipString] - Cung cấp chuỗi tooltip khi hover chuột vào nút.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetTooltipString(IntPtr thisPtr, IntPtr* pbstrToolTip)
        {
            if (pbstrToolTip == null) return HResult.InvalidArgument;
            bool isVn = BridgeStateManager.IsVietnameseMode;
            string tip = isVn ? "BambooMintKey: Tiếng Việt" : "BambooMintKey: English";
            *pbstrToolTip = Marshal.StringToBSTR(tip);
            return HResult.Ok;
        }

        // =====================================================================
        // ITfLangBarItemButton Implementation
        // =====================================================================

        /// <summary>[WinSDK: ITfLangBarItemButton::OnClick] - Xử lý sự kiện click chuột từ người dùng.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int OnClick(IntPtr thisPtr, uint click, POINT pt, RECT* prcArea)
        {
            // SDK: TF_LBI_CLK_LEFT = 2 (Click chuột trái -> đảo chế độ gõ V/E)
            if (click == TsfLangBarFlags.TfLbiClkLeft)
            {
                BridgeStateManager.ToggleVietnameseMode();
                NotifyStateChanged();
            }
            return HResult.Ok;
        }

        /// <summary>[WinSDK: ITfLangBarItemButton::InitMenu] - Khởi tạo menu ngữ cảnh khi click chuột phải.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int InitMenu(IntPtr thisPtr, IntPtr pMenu)
        {
            // Được hiện thực hóa chi tiết tại 003_05_TaskbarContextMenu.md
            return HResult.Ok;
        }

        /// <summary>[WinSDK: ITfLangBarItemButton::OnMenuSelect] - Bắt sự kiện mục menu được chọn.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int OnMenuSelect(IntPtr thisPtr, uint uId)
        {
            // Được hiện thực hóa chi tiết tại 003_05_TaskbarContextMenu.md
            return HResult.Ok;
        }

        /// <summary>[WinSDK: ITfLangBarItemButton::GetIcon] - Cung cấp con trỏ HICON để Windows vẽ icon Taskbar.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
        {
            if (phIcon == null) return HResult.InvalidArgument;
            // Trả về HICON động từ Win32 GDI (chi tiết tại 003_04_IconHelper_DynamicRendering.md)
            *phIcon = IntPtr.Zero;
            return HResult.Ok;
        }

        /// <summary>[WinSDK: ITfLangBarItemButton::GetText] - Cung cấp chuỗi nhãn hiển thị nút ("V" hoặc "E").</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetText(IntPtr thisPtr, IntPtr* pbstrText)
        {
            if (pbstrText == null) return HResult.InvalidArgument;
            bool isVn = BridgeStateManager.IsVietnameseMode;
            string text = isVn ? "V" : "E";
            *pbstrText = Marshal.StringToBSTR(text);
            return HResult.Ok;
        }

        // =====================================================================
        // ITfSource Implementation (Nhận ITfLangBarItemSink từ Windows)
        // =====================================================================

        /// <summary>[WinSDK: ITfSource::AdviseSink] - Windows gọi để trao con trỏ ITfLangBarItemSink cho bộ gõ.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int AdviseSink(IntPtr thisPtr, Guid* riid, IntPtr punk, uint* pdwCookie)
        {
            if (riid == null || punk == IntPtr.Zero || pdwCookie == null) return HResult.InvalidArgument;

            if (*riid == Guids.IidITfLangBarItemSink)
            {
                _pLangBarSink = punk;
                NativeCom.AddRef(punk);
                _sinkCookie = 1;
                *pdwCookie = _sinkCookie;
                return HResult.Ok;
            }

            *pdwCookie = 0;
            return HResult.InvalidArgument;
        }

        /// <summary>[WinSDK: ITfSource::UnadviseSink] - Windows gọi để hủy đăng ký Sink khi tắt ứng dụng hoặc gỡ nút.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int UnadviseSink(IntPtr thisPtr, uint dwCookie)
        {
            if (dwCookie == _sinkCookie && _pLangBarSink != IntPtr.Zero)
            {
                NativeCom.Release(_pLangBarSink);
                _pLangBarSink = IntPtr.Zero;
                _sinkCookie = 0;
                return HResult.Ok;
            }
            return HResult.InvalidArgument;
        }

        // =====================================================================
        // Lifecycle & State Notification Binding
        // =====================================================================

        /// <summary>
        /// Đăng ký nút Language Bar vào hệ thống thông qua ITfLangBarItemMgr.
        /// </summary>
        public static void Register(IntPtr pThreadMgr)
        {
            if (pThreadMgr == IntPtr.Zero) return;

            Guid iidMgr = Guids.IidITfLangBarItemMgr;
            IntPtr pMgr = IntPtr.Zero;

            var unk = **(TfSourceVTable**)pThreadMgr;
            if (unk.QueryInterface(pThreadMgr, &iidMgr, &pMgr) == HResult.Ok && pMgr != IntPtr.Zero)
            {
                _langBarMgr = pMgr;
                var mgrVTable = **(ITfLangBarItemMgrVTable**)_langBarMgr;
                
                // [WinSDK: ITfLangBarItemMgr::AddItem]
                // Windows sẽ tự gọi QI(ITfSource) -> AdviseSink trên _comInstance để trao Sink
                mgrVTable.AddItem(_langBarMgr, _comInstance);
            }
        }

        /// <summary>
        /// Gỡ nút khỏi Language Bar và giải phóng tài nguyên.
        /// </summary>
        public static void Unregister()
        {
            if (_langBarMgr != IntPtr.Zero)
            {
                var mgrVTable = **(ITfLangBarItemMgrVTable**)_langBarMgr;
                // [WinSDK: ITfLangBarItemMgr::RemoveItem]
                mgrVTable.RemoveItem(_langBarMgr, _comInstance);

                NativeCom.Release(_langBarMgr);
                _langBarMgr = IntPtr.Zero;
            }

            if (_pLangBarSink != IntPtr.Zero)
            {
                NativeCom.Release(_pLangBarSink);
                _pLangBarSink = IntPtr.Zero;
                _sinkCookie = 0;
            }
        }

        /// <summary>
        /// Báo cho Windows vẽ lại Icon, Text và Tooltip qua ITfLangBarItemSink::OnUpdate.
        /// Được gọi khi người dùng click chuột trái vào nút hoặc nhấn phím tắt chuyển chế độ (Ctrl+Shift).
        /// </summary>
        public static void NotifyStateChanged()
        {
            if (_pLangBarSink != IntPtr.Zero)
            {
                var sinkVTable = **(ITfLangBarItemSinkVTable**)_pLangBarSink;
                // [WinSDK: ITfLangBarItemSink::OnUpdate]
                sinkVTable.OnUpdate(
                    _pLangBarSink,
                    TsfLangBarFlags.TfLbiIcon | TsfLangBarFlags.TfLbiText | TsfLangBarFlags.TfLbiTooltip);
            }
        }
    }
}
```

---

## 4. Tích hợp vào Vòng đời Bộ gõ (`BambooMintKeyTextService` & `KeyEventSink`)

### 4.1. Tích hợp tại `BambooMintKeyTextService.cs`

Trong hàm `ActivateExImpl` và `DeactivateImpl`:

```csharp
// Trong BambooMintKeyTextService::ActivateExImpl
internal static int ActivateExImpl(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId, uint dwFlags)
{
    // ... (Các bước 1, 2, 3: Advise ThreadMgrSink, KeyEventSink)

    // 4. Khởi tạo / Đồng bộ Engine State
    BridgeStateManager.InitializeEngine();

    // 5. Đăng ký Language Bar Item Button vào Taskbar
    LangBarItemButton.Register(pThreadMgr);

    DebugLog.Write("ActivateExImpl completed with LangBarItemButton registered");
    return HResult.Ok;
}

// Trong BambooMintKeyTextService::DeactivateImpl
private static int DeactivateImpl(IntPtr thisPtr)
{
    var target = GetTarget(thisPtr);
    if (!target._isActivated) return HResult.Ok;

    // 1. Gỡ nút khỏi Language Bar Taskbar
    LangBarItemButton.Unregister();

    // ... (Các bước 2, 3, 4: Unadvise KeyEventSink, ThreadMgrSink, Terminate Composition)
    return HResult.Ok;
}
```

### 4.2. Tích hợp cập nhật Icon khi bấm Phím tắt chuyển chế độ (`KeyEventSinkImpl.cs`)

Khi người dùng nhấn tổ hợp phím tắt chuyển chế độ (`Ctrl + Shift` hoặc `Alt + Z`):

```csharp
// Khi bắt được phím tắt toggle V/E trong KeyEventSink:
BridgeStateManager.ToggleVietnameseMode();

// Bắn thông báo ngay để nút trên Taskbar tự động đổi giữa V và E
LangBarItemButton.NotifyStateChanged();
```

---

## 5. Bổ sung hỗ trợ tại `BridgeStateManager.cs`

Để đảm bảo nguyên lý Single Source of Truth (SSOT), `BridgeStateManager` bổ sung các hàm hỗ trợ:

```csharp
/// <summary>Kiểm tra xem chế độ gõ tiếng Việt hiện đang bật hay tắt.</summary>
public static bool IsVietnameseMode => _currentConfig.IsEnabled;

/// <summary>Đảo trạng thái gõ tiếng Việt / tiếng Anh và trả về trạng thái mới.</summary>
public static bool ToggleVietnameseMode()
{
    var newConfig = new EngineConfig.EngineConfig(
        !_currentConfig.IsEnabled,
        _currentConfig.AutoRestoreEnglishWords,
        _currentConfig.AllowRepeatKeyUndo,
        _currentConfig.AllowLeadingWAsU,
        _currentConfig.ToneStyle
    );
    _currentConfig = newConfig;
    return _currentConfig.IsEnabled;
}
```

---

## 6. Quy trình Kiểm thử & Validation

1. **Biên dịch Native DLL:** Chạy script `scripts/build-native.ps1` kiểm tra không có lỗi build AOT, không có cảnh báo calling convention.
2. **Kiểm tra VTable Slot Alignment:** Dùng PowerShell harness gọi `QueryInterface` với `IID_ITfLangBarItemButton` và `IID_ITfSource`, kiểm tra con trỏ VTable trả về khác `IntPtr.Zero`.
3. **Đăng ký TSF:** Chạy `test-register.ps1` (Admin) và `enable-tip.ps1` (User).
4. **Kiểm tra Runtime:**
   - Nhấn `Win + Space` chuyển sang BambooMintKey.
   - Quan sát góc Taskbar xuất hiện icon hiển thị chữ **V**.
   - Hover chuột: Tooltip hiển thị `"BambooMintKey: Tiếng Việt"`.
   - Click chuột trái: Nút lập tức chuyển sang chữ **E**, Tooltip chuyển thành `"BambooMintKey: English"`.
   - Nhấn phím tắt toggle (hoặc gõ thử phím): Xác nhận engine tuân thủ đúng trạng thái bật/tắt gõ tiếng Việt mà không gây crash `ctfmon.exe` hay `explorer.exe`.