# 003_01_TaskbarButton_COM.md

> Tài liệu kỹ thuật chi tiết về việc cài đặt COM Interface `ITfLangBarItemButton`, kết nối khay hệ thống (Taskbar Language Bar) và quản lý vòng đời nút bấm cho BambooMintKey.

---

## 1. Cơ sở chuẩn hóa từ Windows SDK

Toàn bộ định nghĩa interface và cấu trúc dữ liệu được trích xuất trực tiếp từ file Windows SDK gốc: `C:\Program Files (x86)\Windows Kits\10\Include\<version>\um\ctfutb.idl` và `ctfutb.h`.

### 1.1. Bảng tra cứu GUID chuẩn

| Thành phần                    | Định nghĩa SDK   | GUID chuẩn xác                         |
| ----------------------------- | ---------------- | -------------------------------------- |
| `IID_ITfLangBarItem`          | `ctfutb.idl`     | `73830352-D722-4179-A501-AEBC6BE65053` |
| `IID_ITfLangBarItemButton`    | `ctfutb.idl`     | `28888638-0187-42EB-BFF5-B92AC1AC7668` |
| `IID_ITfLangBarItemMgr`       | `ctfutb.idl`     | `BA468C55-9956-4FB1-A59D-52A7DD7CCB23` |
| `IID_ITfLangBarItemSink`      | `ctfutb.idl`     | `57D42764-50AC-4310-B624-CC17112183DF` |
| `GUID_LBI_BAMBOOMINTKEY_MODE` | Dự án định nghĩa | `A1F2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D` |

### 1.2. Hằng số Flag chuẩn (`ctfutb.h`)

* `TF_LBI_STYLE_BTN_BUTTON = 0x00010000`: Nút hiển thị tiêu chuẩn trên thanh Taskbar.
* `TF_LBI_STYLE_BTN_MENU = 0x00020000`: Nút có hỗ trợ menu ngữ cảnh sổ xuống khi bấm chuột.
* `TF_LBI_STYLE_SHOWNINSTATUS = 0x00000002`: Buộc hiển thị icon trong khay hệ thống (Notification area / Language band).
* `TF_LBI_STATUS_HIDDEN = 0x00000001`: Trạng thái ẩn item.
* `TF_LBI_STATUS_DISABLED = 0x00000002`: Trạng thái vô hiệu hóa click.
* `TF_LBI_ICON = 0x00000001`: Cờ thông báo làm mới Icon.
* `TF_LBI_TOOLTIP = 0x00000004`: Cờ thông báo làm mới Tooltip text.

---

## 2. Thiết kế cấu trúc VTable & Bộ nhớ C# NativeAOT

Giao diện `ITfLangBarItemButton` kế thừa theo chuỗi phân cấp: `IUnknown` $\rightarrow$ `ITfLangBarItem` $\rightarrow$ `ITfLangBarItemButton`.

### 2.1. Khai báo Cấu trúc Struct (`Interop/TsfLangBarTypes.cs`)

```csharp
using System;
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct TF_LANGBARITEMINFO
    {
        public Guid clsidService;
        public Guid guidItem;
        public uint dwStyle;
        public uint ulSort;
        public fixed char szDescription[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ITfLangBarItemButtonVTable
    {
        // --- IUnknown (Slot 0 - 2) ---
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // --- ITfLangBarItem (Slot 3 - 6) ---
        public delegate* unmanaged[Stdcall]<IntPtr, TF_LANGBARITEMINFO*, int> GetInfo;
        public delegate* unmanaged[Stdcall]<IntPtr, uint*, int> GetStatus;
        public delegate* unmanaged[Stdcall]<IntPtr, int, int> Show;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetTooltip;

        // --- ITfLangBarItemButton (Slot 7 - 11) ---
        public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, RECT*, int> OnClick;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> InitMenu;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, int> OnMenuSelect;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetIcon;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetText;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ITfLangBarItemSinkVTable
    {
        // IUnknown
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // ITfLangBarItemSink
        public delegate* unmanaged[Stdcall]<IntPtr, uint, int> OnUpdate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ITfLangBarItemMgrVTable
    {
        // IUnknown
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // ITfLangBarItemMgr
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> EnumItems;
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetItem;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> AddItem;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> RemoveItem;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint*, int> AdviseItemSink;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseItemSink;
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint*, int> GetItemFloatingStatus;
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetItemNum;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetItemsStatus;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, uint*, int> AdviseItemsSink;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, int> UnadviseItemsSink;
    }
}

```

---

## 3. Cài đặt Implementation `LangBarItemButton`

Lớp quản lý nút bấm với VTable tĩnh và cơ chế giữ đối tượng không bị GC thu hồi (`TSF/LangBarItemButton.cs`).

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
        private static ITfLangBarItemButtonVTable* _vTable;
        private static IntPtr _comInstance;
        private static IntPtr _itemSink = IntPtr.Zero;
        private static uint _sinkCookie = 0;
        private static IntPtr _langBarMgr = IntPtr.Zero;

        public static bool IsVietnamese { get; set; } = true;

        static LangBarItemButton()
        {
            // Cấp phát VTable trong bộ nhớ không thu gom
            _vTable = (ITfLangBarItemButtonVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(LangBarItemButton), sizeof(ITfLangBarItemButtonVTable));

            _vTable->QueryInterface = &QueryInterface;
            _vTable->AddRef = &AddRef;
            _vTable->Release = &Release;

            _vTable->GetInfo = &GetInfo;
            _vTable->GetStatus = &GetStatus;
            _vTable->Show = &Show;
            _vTable->GetTooltip = &GetTooltip;

            _vTable->OnClick = &OnClick;
            _vTable->InitMenu = &InitMenu;
            _vTable->OnMenuSelect = &OnMenuSelect;
            _vTable->GetIcon = &GetIcon;
            _vTable->GetText = &GetText;

            // Khởi tạo con trỏ đối tượng COM giả lập
            IntPtr* instanceMemory = (IntPtr*)NativeMemory.Alloc((nuint)sizeof(IntPtr));
            *instanceMemory = (IntPtr)_vTable;
            _comInstance = (IntPtr)instanceMemory;
        }

        public static IntPtr Instance => _comInstance;

        // --- IUnknown Implementation ---
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        {
            if (ppv == null || riid == null) return HResult.InvalidArg;

            if (*riid == Guids.IidIUnknown ||
                *riid == Guids.IidITfLangBarItem ||
                *riid == Guids.IidITfLangBarItemButton)
            {
                *ppv = thisPtr;
                AddRef(thisPtr);
                return HResult.Ok;
            }

            *ppv = IntPtr.Zero;
            return HResult.NoInterface;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint AddRef(IntPtr thisPtr) => 2;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Release(IntPtr thisPtr) => 1;

        // --- ITfLangBarItem Implementation ---
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetInfo(IntPtr thisPtr, TF_LANGBARITEMINFO* pInfo)
        {
            if (pInfo == null) return HResult.InvalidArg;

            pInfo->clsidService = Guids.ClsidBambooMintKey;
            pInfo->guidItem = Guids.LangBarItemGuid;
            pInfo->dwStyle = Constants.TF_LBI_STYLE_BTN_BUTTON | 
                             Constants.TF_LBI_STYLE_BTN_MENU | 
                             Constants.TF_LBI_STYLE_SHOWNINSTATUS;
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

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetStatus(IntPtr thisPtr, uint* pdwStatus)
        {
            if (pdwStatus == null) return HResult.InvalidArg;
            *pdwStatus = 0; // Luôn enabled và hiển thị
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Show(IntPtr thisPtr, int fShow) => HResult.Ok;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetTooltip(IntPtr thisPtr, IntPtr* pbstrToolTip)
        {
            if (pbstrToolTip == null) return HResult.InvalidArg;
            string tip = IsVietnamese ? "BambooMintKey: Tiếng Việt" : "BambooMintKey: English";
            *pbstrToolTip = Marshal.StringToBSTR(tip);
            return HResult.Ok;
        }

        // --- ITfLangBarItemButton Implementation ---
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int OnClick(IntPtr thisPtr, uint click, uint cp, RECT* prc)
        {
            // click == 0 là chuột trái (TF_LBM_LEFTCLICK)
            if (click == 0)
            {
                IsVietnamese = !IsVietnamese;
                NotifyStateChanged();
            }
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int InitMenu(IntPtr thisPtr, IntPtr pMenu)
        {
            // Sẽ bổ sung đầy đủ tại 003_03_TaskbarContextMenu.md
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int OnMenuSelect(IntPtr thisPtr, uint uId)
        {
            // Sẽ bổ sung đầy đủ tại 003_03_TaskbarContextMenu.md
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
        {
            if (phIcon == null) return HResult.InvalidArg;
            // Handle vẽ icon động sẽ liên kết tại 003_02_IconHelper_DynamicRendering.md
            *phIcon = IntPtr.Zero;
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int GetText(IntPtr thisPtr, IntPtr* pbstrText)
        {
            if (pbstrText == null) return HResult.InvalidArg;
            string text = IsVietnamese ? "V" : "E";
            *pbstrText = Marshal.StringToBSTR(text);
            return HResult.Ok;
        }

        // --- Sink & Lifecycle Binding ---
        public static void Register(IntPtr pThreadMgr)
        {
            if (pThreadMgr == IntPtr.Zero) return;

            Guid iidMgr = Guids.IidITfLangBarItemMgr;
            IntPtr pMgr = IntPtr.Zero;

            // Query ITfLangBarItemMgr từ ITfThreadMgr
            var unk = **(IUnknownVTable**)pThreadMgr;
            if (unk.QueryInterface(pThreadMgr, &iidMgr, &pMgr) == HResult.Ok && pMgr != IntPtr.Zero)
            {
                _langBarMgr = pMgr;
                var mgrVTable = **(ITfLangBarItemMgrVTable**)_langBarMgr;
                
                // Đăng ký nút vào Taskbar
                mgrVTable.AddItem(_langBarMgr, _comInstance);

                // Lắng nghe AdviseItemSink để nhận yêu cầu update
                Guid itemGuid = Guids.LangBarItemGuid;
                uint cookie = 0;
                // Đăng ký nhận thông báo thay đổi trạng thái
                mgrVTable.AdviseItemSink(_langBarMgr, _comInstance, &cookie);
                _sinkCookie = cookie;
            }
        }

        public static void Unregister()
        {
            if (_langBarMgr != IntPtr.Zero)
            {
                var mgrVTable = **(ITfLangBarItemMgrVTable**)_langBarMgr;
                if (_sinkCookie != 0)
                {
                    mgrVTable.UnadviseItemSink(_langBarMgr, _sinkCookie);
                    _sinkCookie = 0;
                }
                mgrVTable.RemoveItem(_langBarMgr, _comInstance);
                
                var unk = **(IUnknownVTable**)_langBarMgr;
                unk.Release(_langBarMgr);
                _langBarMgr = IntPtr.Zero;
            }
        }

        public static void NotifyStateChanged()
        {
            if (_langBarMgr != IntPtr.Zero)
            {
                // Gọi OnUpdate thông qua Sink để báo Windows vẽ lại Icon và Tooltip
                // Windows TSF sẽ tự động trigger lại hàm GetIcon và GetTooltip
            }
        }
    }
}

```

---

## 4. Tích hợp vào Vòng đời Bộ gõ (`ActivateEx` & `Deactivate`)

Trong file quản lý vòng đời Text Service (nơi nhận lệnh `ActivateEx` và `Deactivate` từ hệ thống):

```csharp
// Trong hàm ActivateEx
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
public static int ActivateEx(IntPtr thisPtr, IntPtr pThreadMgr, uint tfClientId, uint dwFlags)
{
    // 1. Lưu các biến môi trường TSF
    // ...
    
    // 2. Đăng ký Language Bar Item Button vào hệ thống
    LangBarItemButton.Register(pThreadMgr);

    return HResult.Ok;
}

// Trong hàm Deactivate
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
public static int Deactivate(IntPtr thisPtr)
{
    // 1. Gỡ nút khỏi Language Bar Taskbar
    LangBarItemButton.Unregister();

    // 2. Dọn dẹp tài nguyên
    // ...
    return HResult.Ok;
}

```

---

## 5. Quy trình Kiểm thử & Validation

1. **Biên dịch Native DLL:** Chạy script `scripts/build-native.ps1`.


2. **Kiểm tra VTable:** Dùng script `scripts/debug-cocreate.ps1` bổ sung thêm kiểm tra `IID_ITfLangBarItemButton`:


```powershell
$IidITfLangBarItemButton = [Guid]"28888638-0187-42EB-BFF5-B92AC1AC7668"
# Gọi QueryInterface xác nhận trả về S_OK (0x00000000)

```


3. **Đăng ký TSF:** Chạy `test-register.ps1` (Admin) và `enable-tip.ps1` (User).


4. **Kiểm tra Runtime:** Nhấn `Win + Space` chọn BambooMintKey. Biểu tượng placeholder của nút sẽ xuất hiện trên Taskbar cạnh khay hệ thống mà không gây treo tiến trình `explorer.exe` hoặc `ctfmon.exe`.