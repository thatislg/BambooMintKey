// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
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
    private static TfLangBarItemButtonVTable* _buttonVTable;
    private static TfSourceVTable* _sourceVTable;
    private static readonly IntPtr ComInstance;

    // Con trỏ tới ITfLangBarItemSink mà Windows cung cấp qua ITfSource::AdviseSink
    private static volatile IntPtr _pLangBarSink = IntPtr.Zero;
    private static uint _sinkCookie = 0;
    private static IntPtr _langBarMgr = IntPtr.Zero;
    private static readonly Lock SinkLock = new();
    private static IntPtr _pThreadMgr = IntPtr.Zero;
    private static uint _clientId = 0;


    // Win32 Menu APIs cho Context Menu chuột phải
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, nuint uIdNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    private static extern int SetPreferredAppMode(int appMode);

    static LangBarItemButton()
    {
        try { SetPreferredAppMode(1 /* AllowDark */); } catch { }
        InitializeVTables();

        // Cấp phát vùng nhớ Native Layout kép (Slot 0: Button, Slot 1: Source)
        var layout = (LangBarButtonNativeLayout*)NativeMemory.Alloc((nuint)sizeof(LangBarButtonNativeLayout));
        layout->VTableButton = (IntPtr)_buttonVTable;
        layout->VTableSource = (IntPtr)_sourceVTable;
        ComInstance = (IntPtr)layout;
    }

    private static void InitializeVTables()
    {
        // 1. VTable cho ITfLangBarItemButton
        _buttonVTable = (TfLangBarItemButtonVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(LangBarItemButton), sizeof(TfLangBarItemButtonVTable));

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
    public static IntPtr Instance => ComInstance;

    // =====================================================================
    // IUnknown Implementation (Dual-Interface Routing)
    // =====================================================================
    private static uint AddRefImpl() => 2;
    private static uint ReleaseImpl() => 1;

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
            AddRefImpl();
            return HResult.Ok;
        }

        // [WinSDK: QueryInterface cho ITfSource]
        if (*riid == Guids.IidITfSource)
        {
            *ppv = rootPtr + sizeof(IntPtr);
            AddRefImpl();
            return HResult.Ok;
        }

        return HResult.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        => QueryInterfaceImpl(thisPtr, riid, ppv);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr) => AddRefImpl();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr) => ReleaseImpl();

    // Proxy IUnknown cho Slot 1 (ITfSource)
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface_Source(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        => QueryInterfaceImpl(thisPtr - sizeof(IntPtr), riid, ppv);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef_Source(IntPtr thisPtr) => AddRefImpl();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release_Source(IntPtr thisPtr) => ReleaseImpl();

    // =====================================================================
    // ITfLangBarItem Implementation
    // =====================================================================

    /// <summary>[WinSDK: ITfLangBarItem::GetInfo] - Cung cấp thông tin cấu hình nút cho Windows.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetInfo(IntPtr thisPtr, TfLangbariteminfo* pInfo)
    {
        if (pInfo == null) return HResult.InvalidArgument;

        pInfo->clsidService = Guids.TextServiceClsid;
        pInfo->guidItem = Guids.GuidLbiInputMode;
        // Dùng TfLbiStyleBtnToggle | TfLbiStyleShownInTray để Taskbar xử lý đảo trạng thái hai chiều tức thì
        pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnToggle |
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
    private static int OnClick(IntPtr thisPtr, uint click, Point pt, Rect* prcArea)
    {
        DebugLog.Write($"LangBarItemButton OnClick ENTER click={click}, pt=({pt.X}, {pt.Y}), thread={Environment.CurrentManagedThreadId}");
        if (click == TsfLangBarFlags.TfLbiClkRight)
        {
            // Click chuột phải: Mở Context Menu trực tiếp tại tọa độ chuột
            ShowNativeContextMenu(pt);
        }
        else
        {
            // Click chuột trái: Đảo chế độ V/E tức thì
            bool newMode = BridgeStateManager.ToggleVietnameseMode();

            // 1. Gửi thông báo OnUpdate tới Sink để vẽ lại Icon ngay
            NotifyStateChanged();

            // 2. Đồng bộ lập tức tới TSF Input Mode Compartment của Windows 10/11 Shell
            if (_pThreadMgr != IntPtr.Zero)
            {
                TsfCompartmentHelper.SetConversionMode(_pThreadMgr, _clientId, newMode);
            }

            DebugLog.Write($"LangBarItemButton OnClick toggled IsVietnameseMode={newMode} (Sink + Compartment synchronized)");
        }
        DebugLog.Write($"LangBarItemButton OnClick EXIT click={click}");
        return HResult.Ok;
    }

    /// <summary>
    /// Lấy chuỗi nhãn hiển thị cho mục menu chuyển đổi chế độ gõ, kèm phím tắt động (VD: "Gõ tiếng Việt (Ctrl + Shift)").
    /// </summary>
    private static string GetToggleHotkeyDisplayText()
    {
        uint vKey = SharedMemoryManager.HotkeyVKey;
        uint mods = SharedMemoryManager.HotkeyModifiers;

        if (vKey == 0 && mods == 0) return "Gõ tiếng Việt";

        if (vKey == 0x10 && (mods == 0x0202 || mods == 0x0002)) return "Gõ tiếng Việt (Ctrl + Shift)";
        if (vKey == 0x10 && (mods == 0x0201 || mods == 0x0001)) return "Gõ tiếng Việt (Alt + Shift)";
        if (vKey == 0x5A && mods == 0x0001) return "Gõ tiếng Việt (Alt + Z)";
        if (vKey == 0x20 && mods == 0x0002) return "Gõ tiếng Việt (Ctrl + Space)";

        var parts = new List<string>();
        if ((mods & 0x0002) != 0) parts.Add("Ctrl");
        if ((mods & 0x0001) != 0) parts.Add("Alt");
        if ((mods & 0x0004) != 0) parts.Add("Shift");

        string keyName = vKey switch
        {
            0x20 => "Space",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0xC0 => "~",
            0xDC => "\\",
            0xBF => "/",
            0xDB => "[",
            0xDD => "]",
            0xBA => ";",
            0xDE => "'",
            0xBC => ",",
            0xBE => ".",
            0xBD => "-",
            0xBB => "=",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x14 => "CapsLock",
            0x1B => "Esc",
            >= 0x41 and <= 0x5A => ((char)vKey).ToString(),
            >= 0x30 and <= 0x39 => ((char)vKey).ToString(),
            >= 0x60 and <= 0x69 => $"Num{vKey - 0x60}",
            >= 0x70 and <= 0x7B => $"F{vKey - 0x70 + 1}",
            _ => $"0x{vKey:X}"
        };

        if (!parts.Contains(keyName)) parts.Add(keyName);
        return $"Gõ tiếng Việt ({string.Join(" + ", parts)})";
    }

    /// <summary>[WinSDK: ITfLangBarItemButton::InitMenu] - Khởi tạo menu ngữ cảnh qua ITfMenu.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int InitMenu(IntPtr thisPtr, IntPtr pMenu)
    {
        DebugLog.Write($"LangBarItemButton InitMenu ENTER pMenu={pMenu}");
        if (pMenu == IntPtr.Zero) return HResult.InvalidArgument;

        var menuVTable = *(TfMenuVTable**)pMenu;

        // 1. Chế độ gõ tiếng Việt
        bool isVn = BridgeStateManager.IsVietnameseMode;
        uint vFlag = isVn ? TsfMenuFlags.TfLbMenuFlagChecked : 0;
        AddMenuItemText(menuVTable, pMenu, MenuCommands.ToggleVietnameseMode, vFlag, GetToggleHotkeyDisplayText());

        AddMenuSeparator(menuVTable, pMenu);

        // 2. Submenu: Kiểu đặt dấu thanh
        IntPtr pSubTone = IntPtr.Zero;
        fixed (char* pText = "Kiểu đặt dấu thanh")
        {
            menuVTable->AddMenuItem(pMenu, MenuCommands.SubmenuToneStyle,
                TsfMenuFlags.TfLbMenuFlagSubMenu, IntPtr.Zero, IntPtr.Zero, pText, (uint)"Kiểu đặt dấu thanh".Length, &pSubTone);
        }
        if (pSubTone != IntPtr.Zero)
        {
            var subVTable = *(TfMenuVTable**)pSubTone;
            byte toneStyle = SharedMemoryManager.ToneStyle; // 0 = Modern, 1 = Classic
            AddMenuItemText(subVTable, pSubTone, MenuCommands.ToneStyleModern,
                toneStyle == 0 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "Kiểu mới (òa, xòe, thủy)");
            AddMenuItemText(subVTable, pSubTone, MenuCommands.ToneStyleClassic,
                toneStyle == 1 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "Kiểu cũ (oà, xoè, thuỷ)");
            
            NativeCom.Release(pSubTone);
        }

        // 3. Tùy chọn ngữ pháp thông minh
        uint autoRestoreFlag = SharedMemoryManager.AutoRestoreEnglishWords ? TsfMenuFlags.TfLbMenuFlagChecked : 0;
        AddMenuItemText(menuVTable, pMenu, MenuCommands.ToggleAutoRestoreEnglish, autoRestoreFlag, "Tự động khôi phục từ tiếng Anh");

        uint repeatUndoFlag = SharedMemoryManager.AllowRepeatKeyUndo ? TsfMenuFlags.TfLbMenuFlagChecked : 0;
        AddMenuItemText(menuVTable, pMenu, MenuCommands.ToggleRepeatKeyUndo, repeatUndoFlag, "Gõ lặp dấu để khôi phục (ss -> s)");

        uint leadingWFlag = SharedMemoryManager.AllowLeadingWAsU ? TsfMenuFlags.TfLbMenuFlagChecked : 0;
        AddMenuItemText(menuVTable, pMenu, MenuCommands.ToggleLeadingWAsU, leadingWFlag, "Phím 'w' đầu từ thành 'ư' (w -> ư)");

        AddMenuSeparator(menuVTable, pMenu);

        // 4. Submenu: Kiểu gõ
        IntPtr pSubMethod = IntPtr.Zero;
        fixed (char* pText = "Kiểu gõ")
        {
            menuVTable->AddMenuItem(pMenu, MenuCommands.SubmenuInputMethod,
                TsfMenuFlags.TfLbMenuFlagSubMenu, IntPtr.Zero, IntPtr.Zero, pText, (uint)"Kiểu gõ".Length, &pSubMethod);
        }
        if (pSubMethod != IntPtr.Zero)
        {
            var subVTable = *(TfMenuVTable**)pSubMethod;
            byte curMethod = SharedMemoryManager.InputMethod;
            AddMenuItemText(subVTable, pSubMethod, MenuCommands.MethodTelex,
                curMethod == 0 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "Telex");
            AddMenuItemText(subVTable, pSubMethod, MenuCommands.MethodVni,
                curMethod == 1 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "VNI");
            AddMenuItemText(subVTable, pSubMethod, MenuCommands.MethodSimpleTelex,
                curMethod == 2 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "Simple Telex");
            NativeCom.Release(pSubMethod);
        }

        // 5. Submenu: Bảng mã
        IntPtr pSubCharset = IntPtr.Zero;
        fixed (char* pText = "Bảng mã")
        {
            menuVTable->AddMenuItem(pMenu, MenuCommands.SubmenuCharset,
                TsfMenuFlags.TfLbMenuFlagSubMenu, IntPtr.Zero, IntPtr.Zero, pText, (uint)"Bảng mã".Length, &pSubCharset);
        }
        if (pSubCharset != IntPtr.Zero)
        {
            var subVTable = *(TfMenuVTable**)pSubCharset;
            byte curCharset = SharedMemoryManager.Charset;
            AddMenuItemText(subVTable, pSubCharset, MenuCommands.CharsetUnicodePrecomposed,
                curCharset == 0 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "Unicode dựng sẵn");
            AddMenuItemText(subVTable, pSubCharset, MenuCommands.CharsetUnicodeDecomposed,
                curCharset == 1 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "Unicode tổ hợp");
            AddMenuItemText(subVTable, pSubCharset, MenuCommands.CharsetTcvn3,
                curCharset == 2 ? TsfMenuFlags.TfLbMenuFlagRadioChecked : 0, "TCVN3 (ABC)");
            NativeCom.Release(pSubCharset);
        }

        AddMenuSeparator(menuVTable, pMenu);

        // 6. Cài đặt & Thông tin
        AddMenuItemText(menuVTable, pMenu, MenuCommands.OpenSettings, 0, "Bảng điều khiển & Cài đặt...");
        AddMenuItemText(menuVTable, pMenu, MenuCommands.AboutApp, 0, "Thông tin BambooMintKey");

        return HResult.Ok;
    }

    private static void AddMenuItemText(TfMenuVTable* vtable, IntPtr pMenu, uint id, uint flags, string text)
    {
        fixed (char* pText = text)
        {
            vtable->AddMenuItem(pMenu, id, flags, IntPtr.Zero, IntPtr.Zero, pText, (uint)text.Length, null);
        }
    }

    private static void AddMenuSeparator(TfMenuVTable* vtable, IntPtr pMenu)
    {
        vtable->AddMenuItem(pMenu, 0, TsfMenuFlags.TfLbMenuFlagSeparator, IntPtr.Zero, IntPtr.Zero, null, 0, null);
    }

    /// <summary>[WinSDK: ITfLangBarItemButton::OnMenuSelect] - Bắt sự kiện mục menu được chọn từ TSF.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnMenuSelect(IntPtr thisPtr, uint uId)
    {
        DebugLog.Write($"LangBarItemButton OnMenuSelect uId={uId}");
        ExecuteMenuCommand(uId);
        return HResult.Ok;
    }

    /// <summary>Hiển thị menu ngữ cảnh chuột phải native (Win32 TrackPopupMenuEx) tại vị trí chuột.</summary>
    private static void ShowNativeContextMenu(Point pt)
    {
        if (pt is { X: 0, Y: 0 })
        {
            GetCursorPos(out pt);
        }

        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            const uint mfString       = 0x00000000;
            const uint mfSeparator    = 0x00000800;
            const uint mfChecked      = 0x00000008;
            const uint mfPopup        = 0x00000010;
            const uint tpmReturncmd   = 0x0100;
            const uint tpmRightbutton = 0x0002;

            // 1. Chế độ gõ tiếng Việt
            uint vFlag = BridgeStateManager.IsVietnameseMode ? mfChecked : 0;
            AppendMenuW(hMenu, mfString | vFlag, MenuCommands.ToggleVietnameseMode, GetToggleHotkeyDisplayText());
            AppendMenuW(hMenu, mfSeparator, 0, string.Empty);

            // 2. Submenu Kiểu đặt dấu thanh
            IntPtr hSubTone = CreatePopupMenu();
            byte toneStyle = SharedMemoryManager.ToneStyle;
            AppendMenuW(hSubTone, mfString | (toneStyle == 0 ? mfChecked : 0), MenuCommands.ToneStyleModern, "Kiểu mới (òa, xòe, thủy)");
            AppendMenuW(hSubTone, mfString | (toneStyle == 1 ? mfChecked : 0), MenuCommands.ToneStyleClassic, "Kiểu cũ (oà, xoè, thuỷ)");
            AppendMenuW(hMenu, mfPopup, (nuint)hSubTone, "Kiểu đặt dấu thanh");

            // 3. Tùy chọn ngữ pháp thông minh
            uint autoRestore = SharedMemoryManager.AutoRestoreEnglishWords ? mfChecked : 0;
            AppendMenuW(hMenu, mfString | autoRestore, MenuCommands.ToggleAutoRestoreEnglish, "Tự động khôi phục từ tiếng Anh");

            uint repeatUndo = SharedMemoryManager.AllowRepeatKeyUndo ? mfChecked : 0;
            AppendMenuW(hMenu, mfString | repeatUndo, MenuCommands.ToggleRepeatKeyUndo, "Gõ lặp dấu để khôi phục (ss -> s)");

            uint leadingW = SharedMemoryManager.AllowLeadingWAsU ? mfChecked : 0;
            AppendMenuW(hMenu, mfString | leadingW, MenuCommands.ToggleLeadingWAsU, "Phím 'w' đầu từ thành 'ư' (w -> ư)");

            AppendMenuW(hMenu, mfSeparator, 0, string.Empty);

            // 4. Submenu Kiểu gõ
            IntPtr hSubMethod = CreatePopupMenu();
            byte curMethod = SharedMemoryManager.InputMethod;
            AppendMenuW(hSubMethod, mfString | (curMethod == 0 ? mfChecked : 0), MenuCommands.MethodTelex, "Telex");
            AppendMenuW(hSubMethod, mfString | (curMethod == 1 ? mfChecked : 0), MenuCommands.MethodVni, "VNI");
            AppendMenuW(hSubMethod, mfString | (curMethod == 2 ? mfChecked : 0), MenuCommands.MethodSimpleTelex, "Simple Telex");
            AppendMenuW(hMenu, mfPopup, (nuint)hSubMethod, "Kiểu gõ");

            // 5. Submenu Bảng mã
            IntPtr hSubCharset = CreatePopupMenu();
            byte curCharset = SharedMemoryManager.Charset;
            AppendMenuW(hSubCharset, mfString | (curCharset == 0 ? mfChecked : 0), MenuCommands.CharsetUnicodePrecomposed, "Unicode dựng sẵn");
            AppendMenuW(hSubCharset, mfString | (curCharset == 1 ? mfChecked : 0), MenuCommands.CharsetUnicodeDecomposed, "Unicode tổ hợp");
            AppendMenuW(hSubCharset, mfString | (curCharset == 2 ? mfChecked : 0), MenuCommands.CharsetTcvn3, "TCVN3 (ABC)");
            AppendMenuW(hMenu, mfPopup, (nuint)hSubCharset, "Bảng mã");

            AppendMenuW(hMenu, mfSeparator, 0, string.Empty);

            // 6. Cài đặt & Thông tin
            AppendMenuW(hMenu, mfString, MenuCommands.OpenSettings, "Bảng điều khiển & Cài đặt...");
            AppendMenuW(hMenu, mfString, MenuCommands.AboutApp, "Thông tin BambooMintKey");

            // Đặt Foreground window để menu tự đóng khi người dùng click ra ngoài
            IntPtr hWndFore = GetForegroundWindow();
            if (hWndFore != IntPtr.Zero) SetForegroundWindow(hWndFore);

            uint selectedCmd = TrackPopupMenuEx(hMenu, tpmReturncmd | tpmRightbutton, pt.X, pt.Y, hWndFore, IntPtr.Zero);
            
            if (selectedCmd != 0)
            {
                ExecuteMenuCommand(selectedCmd);
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    /// <summary>Xử lý tập trung các mã lệnh từ Menu (dùng chung cho cả TSF và Win32 Popup).</summary>
    private static void ExecuteMenuCommand(uint cmdId)
    {
        DebugLog.Write($"LangBarItemButton ExecuteMenuCommand cmdId={cmdId}");
        switch (cmdId)
        {
            case MenuCommands.ToggleVietnameseMode:
                bool newMode = BridgeStateManager.ToggleVietnameseMode();
                NotifyStateChanged();
                if (_pThreadMgr != IntPtr.Zero)
                {
                    TsfCompartmentHelper.SetConversionMode(_pThreadMgr, _clientId, newMode);
                }
                break;

            case MenuCommands.ToneStyleModern:
                SharedMemoryManager.ToneStyle = 0; // 0 = Modern
                break;

            case MenuCommands.ToneStyleClassic:
                SharedMemoryManager.ToneStyle = 1; // 1 = Classic
                break;

            case MenuCommands.ToggleAutoRestoreEnglish:
                SharedMemoryManager.AutoRestoreEnglishWords = !SharedMemoryManager.AutoRestoreEnglishWords;
                break;

            case MenuCommands.ToggleRepeatKeyUndo:
                SharedMemoryManager.AllowRepeatKeyUndo = !SharedMemoryManager.AllowRepeatKeyUndo;
                break;

            case MenuCommands.ToggleLeadingWAsU:
                SharedMemoryManager.AllowLeadingWAsU = !SharedMemoryManager.AllowLeadingWAsU;
                break;

            case MenuCommands.MethodTelex:
                SharedMemoryManager.InputMethod = 0;
                break;

            case MenuCommands.MethodVni:
                SharedMemoryManager.InputMethod = 1;
                break;

            case MenuCommands.MethodSimpleTelex:
                SharedMemoryManager.InputMethod = 2;
                break;

            case MenuCommands.CharsetUnicodePrecomposed:
                SharedMemoryManager.Charset = 0;
                break;

            case MenuCommands.CharsetUnicodeDecomposed:
                SharedMemoryManager.Charset = 1;
                break;

            case MenuCommands.CharsetTcvn3:
                SharedMemoryManager.Charset = 2;
                break;

            case MenuCommands.OpenSettings:
                SettingsLauncher.LaunchSettingsGui();
                break;

            case MenuCommands.AboutApp:
                SettingsLauncher.LaunchSettingsGui("--about");
                break;
        }
    }

    /// <summary>[WinSDK: ITfLangBarItemButton::GetIcon] - Cung cấp con trỏ HICON để Windows vẽ icon Taskbar.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
    {
        if (phIcon == null) return HResult.InvalidArgument;

        // Cung cấp bản sao HICON độc lập từ cache tĩnh qua IconHelper.GetBambooIconHandle
        // Windows Taskbar Shell sẽ tự động gọi DestroyIcon sau khi vẽ.
        string text = BridgeStateManager.IsVietnameseMode ? "V" : "E";
        DebugLog.Write($"LangBarItemButton.GetIcon ENTER requested='{text}', IsVietnameseMode={BridgeStateManager.IsVietnameseMode}, thread={Environment.CurrentManagedThreadId}");
        *phIcon = IconHelper.GetBambooIconHandle(text);
        DebugLog.Write($"LangBarItemButton.GetIcon EXIT text='{text}' -> {*phIcon}");

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
        DebugLog.Write($"LangBarItemButton.AdviseSink ENTER thisPtr={thisPtr}, punk={punk}, thread={Environment.CurrentManagedThreadId}");
        if (riid == null || punk == IntPtr.Zero || pdwCookie == null)
        {
            DebugLog.Write("LangBarItemButton.AdviseSink invalid args");
            return HResult.InvalidArgument;
        }

        DebugLog.Write($"LangBarItemButton.AdviseSink riid={*riid}");

        if (*riid == Guids.IidITfLangBarItemSink)
        {
            Guid iidSink = Guids.IidITfLangBarItemSink;
            IntPtr pSink = IntPtr.Zero;
            var unk = *(TfSourceVTable**)punk;
            int hrQi = unk->QueryInterface(punk, &iidSink, &pSink);
            DebugLog.Write($"LangBarItemButton.AdviseSink QI ITfLangBarItemSink hr=0x{hrQi:X8}, pSink={pSink}");

            if (hrQi == HResult.Ok && pSink != IntPtr.Zero)
            {
                lock (SinkLock)
                {
                    if (_pLangBarSink != IntPtr.Zero)
                    {
                        NativeCom.Release(_pLangBarSink);
                    }
                    _pLangBarSink = pSink;
                    _sinkCookie = 1;
                    *pdwCookie = _sinkCookie;
                }
                DebugLog.Write($"LangBarItemButton.AdviseSink: ITfLangBarItemSink connected via QI pSink={pSink}");
                return HResult.Ok;
            }

            *pdwCookie = 0;
            DebugLog.Write($"LangBarItemButton.AdviseSink: QI ITfLangBarItemSink failed (0x{hrQi:X8})");
            return hrQi != HResult.Ok ? hrQi : HResult.NoInterface;
        }

        *pdwCookie = 0;
        DebugLog.Write($"LangBarItemButton.AdviseSink: unsupported riid={*riid}, returning E_INVALIDARG");
        return HResult.InvalidArgument;
    }

    /// <summary>[WinSDK: ITfSource::UnadviseSink] - Windows gọi để hủy đăng ký Sink khi tắt ứng dụng hoặc gỡ nút.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int UnadviseSink(IntPtr thisPtr, uint dwCookie)
    {
        DebugLog.Write($"LangBarItemButton.UnadviseSink ENTER dwCookie={dwCookie}, _sinkCookie={_sinkCookie}, _pLangBarSink={_pLangBarSink}");
        lock (SinkLock)
        {
            if (dwCookie == _sinkCookie && _pLangBarSink != IntPtr.Zero)
            {
                NativeCom.Release(_pLangBarSink);
                _pLangBarSink = IntPtr.Zero;
                _sinkCookie = 0;
                DebugLog.Write("LangBarItemButton.UnadviseSink: ITfLangBarItemSink disconnected");
                return HResult.Ok;
            }
        }
        DebugLog.Write("LangBarItemButton.UnadviseSink: cookie mismatch or no sink");
        return HResult.InvalidArgument;
    }

    // =====================================================================
    // Lifecycle & State Notification Binding
    // =====================================================================

    private static bool _listenerStarted = false;

    private static void StartEventListener()
    {
        var thread = new Thread(() =>
        {
            IntPtr hEv = SharedMemoryManager.StateChangedEventHandle;
            uint localSeq = SharedMemoryManager.StateSequence;
            bool lastMode = BridgeStateManager.IsVietnameseMode;
            DebugLog.Write($"StartEventListener thread started. hEv={hEv}, initialMode={lastMode}, initialSeq={localSeq}, thread={Environment.CurrentManagedThreadId}");

            while (true)
            {
                // Chờ event Manual-Reset broadcast (timeout 250ms phòng trường hợp trễ)
                if (hEv != IntPtr.Zero)
                {
                    uint wr = SharedMemoryManager.WaitForSingleObject(hEv, 250);
                    if (wr != 0 /* WAIT_OBJECT_0 */ && wr != 258 /* WAIT_TIMEOUT */)
                    {
                        DebugLog.Write($"StartEventListener WaitForSingleObject returned unexpected {wr}, exiting loop");
                        break;
                    }
                }
                else
                {
                    Thread.Sleep(250);
                }

                // Kiểm tra StateSequence để phát hiện mọi thay đổi từ bất kỳ tiến trình nào
                uint currentSeq = SharedMemoryManager.StateSequence;
                bool currentMode = BridgeStateManager.IsVietnameseMode;

                if (currentSeq != localSeq || currentMode != lastMode)
                {
                    DebugLog.Write($"StartEventListener detected change: seq {localSeq}->{currentSeq}, mode {lastMode}->{currentMode}");
                    localSeq = currentSeq;
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
    public static void Register(IntPtr pThreadMgr, uint clientId = 0)
    {
        if (pThreadMgr == IntPtr.Zero)
        {
            DebugLog.Write("LangBarItemButton.Register: pThreadMgr is NULL");
            return;
        }

        _pThreadMgr = pThreadMgr;
        _clientId = clientId;

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
            const uint clsctxInprocServer = 1;
            hrQi = NativeCom.CoCreateInstance(&clsidMgr, IntPtr.Zero, clsctxInprocServer, &iidMgr, &pMgr);
            DebugLog.Write($"LangBarItemButton.Register CoCreateInstance ITfLangBarItemMgr hr=0x{hrQi:X8}, pMgr={pMgr}");
        }

        if (pMgr != IntPtr.Zero)
        {
            _langBarMgr = pMgr;
            var mgrVTable = *(TfLangBarItemMgrVTable**)_langBarMgr;
            
            // [WinSDK: ITfLangBarItemMgr::AddItem]
            // Windows sẽ tự gọi QI(ITfSource) -> AdviseSink trên _comInstance để trao Sink
            int hr = mgrVTable->AddItem(_langBarMgr, ComInstance);
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
            var mgrVTable = *(TfLangBarItemMgrVTable**)_langBarMgr;
            // [WinSDK: ITfLangBarItemMgr::RemoveItem]
            int hr = mgrVTable->RemoveItem(_langBarMgr, ComInstance);
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

        _pThreadMgr = IntPtr.Zero;
        _clientId = 0;
    }

    /// <summary>
    /// Báo cho Windows vẽ lại Icon, Text và Tooltip qua ITfLangBarItemSink::OnUpdate.
    /// Được gọi khi người dùng click chuột trái vào nút hoặc nhấn phím tắt chuyển chế độ (Ctrl+Shift+Q).
    /// </summary>
    public static void NotifyStateChanged()
    {
        IntPtr sink = _pLangBarSink;
        DebugLog.Write($"LangBarItemButton.NotifyStateChanged ENTER _pLangBarSink={sink}, thread={Environment.CurrentManagedThreadId}");
        if (sink != IntPtr.Zero)
        {
            var sinkVTable = *(TfLangBarItemSinkVTable**)sink;
            // [WinSDK: ITfLangBarItemSink::OnUpdate]
            int hr = sinkVTable->OnUpdate(
                sink,
                TsfLangBarFlags.TfLbiIcon | TsfLangBarFlags.TfLbiText | TsfLangBarFlags.TfLbiTooltip);
            DebugLog.Write($"LangBarItemButton.NotifyStateChanged: OnUpdate sent to Windows Taskbar hr=0x{hr:X8}");
        }
        else
        {
            DebugLog.Write("LangBarItemButton.NotifyStateChanged: _pLangBarSink is NULL in this process");
        }
    }
}
