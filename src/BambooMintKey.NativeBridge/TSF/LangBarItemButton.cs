// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Quản lý nút bấm Language Bar trên Taskbar Windows cho BambooMintKey.
/// Hỗ trợ ITfLangBarItemButton và ITfSource để nhận kết nối Sink từ Windows.
/// Theo thiết kế 003_03_TaskbarButton_COM.md.
/// </summary>
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

    /// <summary>Con trỏ COM Instance của LangBarItemButton.</summary>
    public static IntPtr Instance => _comInstance;

    // =====================================================================
    // IUnknown Implementation (Dual-Interface Routing)
    // =====================================================================
    internal static uint AddRefImpl(IntPtr thisPtr) => 2;
    internal static uint ReleaseImpl(IntPtr thisPtr) => 1;

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
            AddRefImpl(rootPtr);
            return HResult.Ok;
        }

        // [WinSDK: QueryInterface cho ITfSource]
        if (*riid == Guids.IidITfSource)
        {
            *ppv = rootPtr + sizeof(IntPtr);
            AddRefImpl(rootPtr);
            return HResult.Ok;
        }

        return HResult.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        => QueryInterfaceImpl(thisPtr, riid, ppv);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr) => AddRefImpl(thisPtr);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr) => ReleaseImpl(thisPtr);

    // Proxy IUnknown cho Slot 1 (ITfSource)
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface_Source(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        => QueryInterfaceImpl(thisPtr - sizeof(IntPtr), riid, ppv);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef_Source(IntPtr thisPtr) => AddRefImpl(thisPtr - sizeof(IntPtr));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release_Source(IntPtr thisPtr) => ReleaseImpl(thisPtr - sizeof(IntPtr));

    // =====================================================================
    // ITfLangBarItem Implementation
    // =====================================================================

    /// <summary>[WinSDK: ITfLangBarItem::GetInfo] - Cung cấp thông tin cấu hình nút cho Windows.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetInfo(IntPtr thisPtr, TF_LANGBARITEMINFO* pInfo)
    {
        if (pInfo == null) return HResult.InvalidArgument;

        pInfo->clsidService = Guids.TextServiceClsid;
        pInfo->guidItem = Guids.GuidLbiInputMode;
        // Chỉ dùng TfLbiStyleBtnButton | TfLbiStyleShownInTray (KHÔNG dùng TfLbiStyleBtnMenu để Windows xử lý click chuột trái làm nút toggle)
        pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnButton |
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
        DebugLog.Write($"LangBarItemButton OnClick received click={click}");
        // Chỉ xử lý khi đúng là click chuột trái (TF_LBI_CLK_LEFT = 2)
        if (click == TsfLangBarFlags.TfLbiClkLeft)
        {
            bool newMode = BridgeStateManager.ToggleVietnameseMode();
            NotifyStateChanged();
            DebugLog.Write($"LangBarItemButton OnClick toggled IsVietnameseMode={newMode}");
        }
        return HResult.Ok;
    }

    /// <summary>[WinSDK: ITfLangBarItemButton::InitMenu] - Khởi tạo menu ngữ cảnh khi click chuột phải.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InitMenu(IntPtr thisPtr, IntPtr pMenu)
    {
        // Sẽ hiện thực hóa chi tiết tại 003_05_TaskbarContextMenu.md
        return HResult.Ok;
    }

    /// <summary>[WinSDK: ITfLangBarItemButton::OnMenuSelect] - Bắt sự kiện mục menu được chọn.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnMenuSelect(IntPtr thisPtr, uint uId)
    {
        // Sẽ hiện thực hóa chi tiết tại 003_05_TaskbarContextMenu.md
        return HResult.Ok;
    }

    /// <summary>[WinSDK: ITfLangBarItemButton::GetIcon] - Cung cấp con trỏ HICON để Windows vẽ icon Taskbar.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
    {
        if (phIcon == null) return HResult.InvalidArgument;

        // Theo đặc tả Microsoft WinSDK cho ITfLangBarItemButton::GetIcon:
        // "The caller is responsible for destroying this icon when it is no longer required."
        // Windows Taskbar Shell sẽ tự động gọi DestroyIcon sau khi vẽ.
        // Bắt buộc phải tạo HICON mới mỗi lần để tránh cung cấp handle đã bị hủy.
        string text = BridgeStateManager.IsVietnameseMode ? "V" : "E";
        *phIcon = IconHelper.CreateBambooIcon(text);
        DebugLog.Write($"LangBarItemButton.GetIcon: Created fresh HICON for '{text}' -> {*phIcon}");

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
            Guid iidSink = Guids.IidITfLangBarItemSink;
            IntPtr pSink = IntPtr.Zero;
            var unk = *(TfSourceVTable**)punk;
            int hrQi = unk->QueryInterface(punk, &iidSink, &pSink);

            if (hrQi == HResult.Ok && pSink != IntPtr.Zero)
            {
                if (_pLangBarSink != IntPtr.Zero)
                {
                    NativeCom.Release(_pLangBarSink);
                }
                _pLangBarSink = pSink;
                _sinkCookie = 1;
                *pdwCookie = _sinkCookie;
                DebugLog.Write($"LangBarItemButton AdviseSink: ITfLangBarItemSink connected via QI pSink={pSink}");
                return HResult.Ok;
            }

            if (_pLangBarSink != IntPtr.Zero)
            {
                NativeCom.Release(_pLangBarSink);
            }

            _pLangBarSink = punk;
            NativeCom.AddRef(punk);
            _sinkCookie = 1;
            *pdwCookie = _sinkCookie;
            DebugLog.Write("LangBarItemButton AdviseSink: ITfLangBarItemSink connected directly to punk");
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
            DebugLog.Write("LangBarItemButton UnadviseSink: ITfLangBarItemSink disconnected");
            return HResult.Ok;
        }
        return HResult.InvalidArgument;
    }

    // =====================================================================
    // Lifecycle & State Notification Binding
    // =====================================================================

    private static bool _listenerStarted = false;

    private static void StartEventListener()
    {
        var thread = new System.Threading.Thread(() =>
        {
            IntPtr hEv = SharedMemoryManager.StateChangedEventHandle;
            bool lastMode = BridgeStateManager.IsVietnameseMode;

            while (true)
            {
                // Chờ event tối đa 100ms
                if (hEv != IntPtr.Zero)
                {
                    SharedMemoryManager.WaitForSingleObject(hEv, 100);
                }
                else
                {
                    System.Threading.Thread.Sleep(100);
                }

                // Kiểm tra trạng thái thực tế trong Shared Memory để luôn đồng bộ Taskbar
                bool currentMode = BridgeStateManager.IsVietnameseMode;
                if (currentMode != lastMode)
                {
                    lastMode = currentMode;
                    NotifyStateChanged();
                }
            }
        })
        {
            IsBackground = true,
            Name = "BambooMintKey_StateEventListener"
        };
        thread.Start();
    }

    /// <summary>
    /// Đăng ký nút Language Bar vào hệ thống thông qua ITfLangBarItemMgr.
    /// </summary>
    public static void Register(IntPtr pThreadMgr)
    {
        if (pThreadMgr == IntPtr.Zero)
        {
            DebugLog.Write("LangBarItemButton.Register: pThreadMgr is NULL");
            return;
        }

        if (!_listenerStarted)
        {
            _listenerStarted = true;
            StartEventListener();
        }

        Guid iidMgr = Guids.IidITfLangBarItemMgr;
        IntPtr pMgr = IntPtr.Zero;

        var unk = *(TfSourceVTable**)pThreadMgr;
        int hrQi = unk->QueryInterface(pThreadMgr, &iidMgr, &pMgr);
        DebugLog.Write($"LangBarItemButton.Register QI ITfLangBarItemMgr hr=0x{hrQi:X8}, pMgr={pMgr}");

        if (hrQi != HResult.Ok || pMgr == IntPtr.Zero)
        {
            // Fallback sang CoCreateInstance với CLSID_TF_LangBarItemMgr nếu pThreadMgr không hỗ trợ QI trực tiếp
            Guid clsidMgr = Guids.ClsidTfLangBarItemMgr;
            const uint CLSCTX_INPROC_SERVER = 1;
            hrQi = NativeCom.CoCreateInstance(&clsidMgr, IntPtr.Zero, CLSCTX_INPROC_SERVER, &iidMgr, &pMgr);
            DebugLog.Write($"LangBarItemButton.Register CoCreateInstance ITfLangBarItemMgr hr=0x{hrQi:X8}, pMgr={pMgr}");
        }

        if (pMgr != IntPtr.Zero)
        {
            _langBarMgr = pMgr;
            var mgrVTable = *(ITfLangBarItemMgrVTable**)_langBarMgr;
            
            // [WinSDK: ITfLangBarItemMgr::AddItem]
            // Windows sẽ tự gọi QI(ITfSource) -> AdviseSink trên _comInstance để trao Sink
            int hr = mgrVTable->AddItem(_langBarMgr, _comInstance);
            DebugLog.Write($"LangBarItemButton.Register AddItem result=0x{hr:X8}");
            NotifyStateChanged();
        }
        else
        {
            DebugLog.Write("LangBarItemButton.Register: Failed to obtain ITfLangBarItemMgr");
        }
    }

    /// <summary>
    /// Gỡ nút khỏi Language Bar và giải phóng tài nguyên.
    /// </summary>
    public static void Unregister()
    {
        if (_langBarMgr != IntPtr.Zero)
        {
            var mgrVTable = *(ITfLangBarItemMgrVTable**)_langBarMgr;
            // [WinSDK: ITfLangBarItemMgr::RemoveItem]
            int hr = mgrVTable->RemoveItem(_langBarMgr, _comInstance);
            DebugLog.Write($"LangBarItemButton.Unregister RemoveItem hr=0x{hr:X8}");

            NativeCom.Release(_langBarMgr);
            _langBarMgr = IntPtr.Zero;
            DebugLog.Write("LangBarItemButton.Unregister: _langBarMgr released");
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
    /// Được gọi khi người dùng click chuột trái vào nút hoặc nhấn phím tắt chuyển chế độ (Ctrl+Shift+Q).
    /// </summary>
    public static void NotifyStateChanged()
    {
        if (_pLangBarSink != IntPtr.Zero)
        {
            var sinkVTable = *(ITfLangBarItemSinkVTable**)_pLangBarSink;
            // [WinSDK: ITfLangBarItemSink::OnUpdate]
            int hr = sinkVTable->OnUpdate(
                _pLangBarSink,
                TsfLangBarFlags.TfLbiIcon | TsfLangBarFlags.TfLbiText | TsfLangBarFlags.TfLbiTooltip);
            DebugLog.Write($"LangBarItemButton.NotifyStateChanged: OnUpdate sent to Windows Taskbar hr=0x{hr:X8}");
        }
    }
}
