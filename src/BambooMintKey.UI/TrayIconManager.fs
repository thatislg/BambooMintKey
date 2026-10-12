// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "9"
#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Threading
open Avalonia.Threading

module TrayWin32 =

    [<Struct; StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)>]
    type NOTIFYICONDATAW =
        struct
            val mutable cbSize: uint32
            val mutable hWnd: IntPtr
            val mutable uID: uint32
            val mutable uFlags: uint32
            val mutable uCallbackMessage: uint32
            val mutable hIcon: IntPtr
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)>]
            val mutable szTip: string
            val mutable dwState: uint32
            val mutable dwStateMask: uint32
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)>]
            val mutable szInfo: string
            val mutable uTimeoutOrVersion: uint32
            [<MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)>]
            val mutable szInfoTitle: string
            val mutable dwInfoFlags: uint32
            val mutable guidItem: Guid
            val mutable hBalloonIcon: IntPtr
        end

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type POINT =
        struct
            val mutable X: int
            val mutable Y: int
        end

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RECT =
        struct
            val mutable Left: int
            val mutable Top: int
            val mutable Right: int
            val mutable Bottom: int
        end

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type ICONINFO =
        struct
            val mutable fIcon: bool
            val mutable xHotspot: uint32
            val mutable yHotspot: uint32
            val mutable hbmMask: IntPtr
            val mutable hbmColor: IntPtr
        end

    type WNDPROC = delegate of IntPtr * uint32 * UIntPtr * IntPtr -> IntPtr

    [<Struct; StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)>]
    type WNDCLASSEXW =
        struct
            val mutable cbSize: uint32
            val mutable style: uint32
            val mutable lpfnWndProc: WNDPROC
            val mutable cbClsExtra: int
            val mutable cbWndExtra: int
            val mutable hInstance: IntPtr
            val mutable hIcon: IntPtr
            val mutable hCursor: IntPtr
            val mutable hbrBackground: IntPtr
            val mutable lpszMenuName: string
            val mutable lpszClassName: string
            val mutable hIconSm: IntPtr
        end

    let NIM_ADD = 0x00000000u
    let NIM_MODIFY = 0x00000001u
    let NIM_DELETE = 0x00000002u

    let NIF_MESSAGE = 0x00000001u
    let NIF_ICON = 0x00000002u
    let NIF_TIP = 0x00000004u

    let WM_USER = 0x0400u
    let WM_TRAYCALLBACK = WM_USER + 201u
    let WM_LBUTTONUP = 0x0202u
    let WM_RBUTTONUP = 0x0205u

    let MF_STRING = 0x00000000u
    let MF_SEPARATOR = 0x00000800u
    let MF_CHECKED = 0x00000008u
    let MF_POPUP = 0x00000010u
    let TPM_RETURNCMD = 0x0100u
    let TPM_RIGHTBUTTON = 0x0002u

    let SM_CXSMICON = 49
    let SM_CYSMICON = 50

    let SYNCHRONIZE = 0x00100000u

    [<DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern bool Shell_NotifyIconW(uint32 dwMessage, NOTIFYICONDATAW& lpData)

    [<DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern uint16 RegisterClassExW(WNDCLASSEXW& lpwcx)

    [<DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr CreateWindowExW(
        uint32 dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint32 dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam)

    [<DllImport("user32.dll")>]
    extern IntPtr DefWindowProcW(IntPtr hWnd, uint32 uMsg, UIntPtr wParam, IntPtr lParam)

    [<DllImport("user32.dll")>]
    extern bool DestroyWindow(IntPtr hWnd)

    [<DllImport("user32.dll")>]
    extern IntPtr CreatePopupMenu()

    [<DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern bool AppendMenuW(IntPtr hMenu, uint32 uFlags, UIntPtr uIDNewItem, string lpNewItem)

    [<DllImport("user32.dll")>]
    extern uint32 TrackPopupMenuEx(IntPtr hMenu, uint32 uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm)

    [<DllImport("user32.dll")>]
    extern bool DestroyMenu(IntPtr hMenu)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern int GetClassNameW(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern uint32 RegisterWindowMessageW(string lpString)

    [<DllImport("user32.dll")>]
    extern bool GetCursorPos(POINT& lpPoint)

    [<DllImport("user32.dll")>]
    extern IntPtr GetForegroundWindow()

    [<DllImport("user32.dll")>]
    extern bool SetForegroundWindow(IntPtr hWnd)

    [<DllImport("user32.dll")>]
    extern uint32 GetWindowThreadProcessId(IntPtr hWnd, uint32& lpdwProcessId)

    [<DllImport("user32.dll")>]
    extern IntPtr GetKeyboardLayout(uint32 idThread)

    [<DllImport("user32.dll")>]
    extern int GetSystemMetrics(int nIndex)

    [<DllImport("user32.dll")>]
    extern IntPtr GetDC(IntPtr hWnd)

    [<DllImport("user32.dll")>]
    extern int ReleaseDC(IntPtr hWnd, IntPtr hDC)

    [<DllImport("gdi32.dll")>]
    extern IntPtr CreateCompatibleDC(IntPtr hdc)

    [<DllImport("gdi32.dll")>]
    extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy)

    [<DllImport("gdi32.dll")>]
    extern IntPtr CreateSolidBrush(uint32 crColor)

    [<DllImport("gdi32.dll")>]
    extern IntPtr CreatePen(int iStyle, int cWidth, uint32 color)

    [<DllImport("gdi32.dll")>]
    extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj)

    [<DllImport("gdi32.dll")>]
    extern bool DeleteObject(IntPtr hObject)

    [<DllImport("gdi32.dll")>]
    extern bool DeleteDC(IntPtr hdc)

    [<DllImport("gdi32.dll")>]
    extern bool RoundRect(IntPtr hdc, int left, int top, int right, int bottom, int width, int height)

    [<DllImport("gdi32.dll")>]
    extern int SetBkMode(IntPtr hdc, int iBkMode)

    [<DllImport("gdi32.dll")>]
    extern uint32 SetTextColor(IntPtr hdc, uint32 crColor)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern int DrawTextW(IntPtr hdc, string lpchText, int cchText, RECT& lprc, uint32 format)

    [<DllImport("gdi32.dll", CharSet = CharSet.Unicode)>]
    extern IntPtr CreateFontW(
        int cHeight, int cWidth, int cEscapement, int cOrientation,
        int cWeight, uint32 bItalic, uint32 bUnderline, uint32 bStrikeOut,
        uint32 iCharSet, uint32 iOutPrecision, uint32 iClipPrecision,
        uint32 iQuality, uint32 iPitchAndFamily, string pszFaceName)

    [<DllImport("gdi32.dll")>]
    extern IntPtr CreateBitmap(int nWidth, int nHeight, uint32 nPlanes, uint32 nBitCount, IntPtr lpBits)

    [<DllImport("user32.dll")>]
    extern IntPtr CreateIconIndirect(ICONINFO& piconinfo)

    [<DllImport("user32.dll")>]
    extern bool DestroyIcon(IntPtr hIcon)

    [<DllImport("kernel32.dll")>]
    extern uint32 WaitForSingleObject(IntPtr hHandle, uint32 dwMilliseconds)

    [<DllImport("kernel32.dll")>]
    extern bool CloseHandle(IntPtr hObject)

/// <summary>
/// Trợ thủ vẽ HICON Win32 động cho chữ V và E với nền xanh lá #16a34a,
/// đồng bộ tuyệt đối với hình ảnh thương hiệu BambooMintKey trên macOS và Linux.
/// </summary>
module TrayIconGdi =
    open TrayWin32

    let private ColorBackground = 0x004AA316u // #16a34a (BGR)
    let private ColorBorder = 0x00ACEF86u     // #86efac (BGR)
    let private ColorText = 0x00F9F8FBu       // #fbf8f9 (BGR)

    let createLetterIcon (letter: string) : IntPtr =
        let mutable w = GetSystemMetrics(SM_CXSMICON)
        let mutable h = GetSystemMetrics(SM_CYSMICON)
        if w <= 0 then w <- 16
        if h <= 0 then h <- 16

        let hScreenDC = GetDC(IntPtr.Zero)
        let hMemDC = CreateCompatibleDC(hScreenDC)
        let hBmpColor = CreateCompatibleBitmap(hScreenDC, w, h)
        let hOldBmp = SelectObject(hMemDC, hBmpColor)

        let hBrush = CreateSolidBrush(ColorBackground)
        let hPen = CreatePen(0, 1, ColorBorder)
        let hOldBrush = SelectObject(hMemDC, hBrush)
        let hOldPen = SelectObject(hMemDC, hPen)

        let corner = Math.Max(2, w / 4)
        RoundRect(hMemDC, 0, 0, w, h, corner, corner) |> ignore

        SetBkMode(hMemDC, 1) |> ignore
        SetTextColor(hMemDC, ColorText) |> ignore

        let fontHeight = -(int (float h * 0.75))
        let hFont = CreateFontW(fontHeight, 0, 0, 0, 900, 0u, 0u, 0u, 1u, 0u, 0u, 5u, 0u, "Segoe UI")
        let hOldFont = SelectObject(hMemDC, hFont)

        let mutable rc = RECT(Left = 0, Top = 0, Right = w, Bottom = h)
        DrawTextW(hMemDC, letter, letter.Length, &rc, 0x00000001u ||| 0x00000004u ||| 0x00000020u) |> ignore

        SelectObject(hMemDC, hOldFont) |> ignore
        SelectObject(hMemDC, hOldPen) |> ignore
        SelectObject(hMemDC, hOldBrush) |> ignore
        SelectObject(hMemDC, hOldBmp) |> ignore

        DeleteObject(hFont) |> ignore
        DeleteObject(hPen) |> ignore
        DeleteObject(hBrush) |> ignore
        DeleteDC(hMemDC) |> ignore
        ReleaseDC(IntPtr.Zero, hScreenDC) |> ignore

        let hBmpMask = CreateBitmap(w, h, 1u, 1u, IntPtr.Zero)
        let mutable iconInfo = ICONINFO(
            fIcon = true,
            xHotspot = 0u,
            yHotspot = 0u,
            hbmMask = hBmpMask,
            hbmColor = hBmpColor
        )

        let hIcon = CreateIconIndirect(&iconInfo)
        DeleteObject(hBmpColor) |> ignore
        DeleteObject(hBmpMask) |> ignore
        hIcon

/// <summary>
/// Quản trị viên Tray Icon độc lập cho Windows (kiến trúc tương đương BambooMintKey.Mac.StatusBar).
/// Tự động hiển thị khi chọn BambooMintKey và ẩn khi chuyển sang IME khác.
/// </summary>
[<AllowNullLiteral>]
type TrayIconManager(onOpenSettings: unit -> unit, onOpenAbout: unit -> unit) =

    let mutable hMsgWnd: IntPtr = IntPtr.Zero
    let mutable nid = TrayWin32.NOTIFYICONDATAW()
    let mutable isIconAdded = false
    let mutable currentModeVn = true
    let mutable hIconV = IntPtr.Zero
    let mutable hIconE = IntPtr.Zero
    let mutable isRunning = true
    let mutable visibilityTimer: DispatcherTimer = null
    let mutable wndProcDelegate: TrayWin32.WNDPROC = null
    let mutable msgTaskbarCreated = 0u
    let mutable lastKnownActive = false

    let menuCmdToggleVn = 101u
    let menuCmdMethodTelex = 201u
    let menuCmdMethodVni = 202u
    let menuCmdMethodSimple = 203u
    let menuCmdCharsetPrecomposed = 301u
    let menuCmdCharsetDecomposed = 302u
    let menuCmdCharsetTcvn3 = 303u
    let menuCmdToneModern = 401u
    let menuCmdToneClassic = 402u
    let menuCmdFreeTone = 501u
    let menuCmdRepeatUndo = 502u
    let menuCmdLeadingW = 503u
    let menuCmdEnglishBacktrack = 504u
    let menuCmdOpenSettings = 601u
    let menuCmdOpenAbout = 602u

    let isBambooMintKeyActive () : bool =
        try
            let hwnd = TrayWin32.GetForegroundWindow()
            if hwnd <> IntPtr.Zero then
                let mutable pid = 0u
                let threadId = TrayWin32.GetWindowThreadProcessId(hwnd, &pid)
                let currentPid = uint32 (Process.GetCurrentProcess().Id)

                // 1. Nếu cửa sổ tiền cảnh là của chính BambooMintKey UI -> giữ nguyên trạng thái
                if pid = currentPid then
                    lastKnownActive
                else
                    // 2. Kiểm tra xem có phải là Taskbar / Shell (đang click chuột vào Tray Icon)
                    let sb = System.Text.StringBuilder(256)
                    let len = TrayWin32.GetClassNameW(hwnd, sb, sb.Capacity)
                    let className = if len > 0 then sb.ToString() else ""
                    if className = "Shell_TrayWnd" || className = "Shell_SecondaryTrayWnd" || className = "Windows.UI.Core.CoreWindow" then
                        lastKnownActive
                    else
                        // 3. Lấy Language Layout của ứng dụng tiền cảnh
                        let hkl = TrayWin32.GetKeyboardLayout(threadId)
                        let langId = (uint16 (hkl.ToInt64())) &&& 0xFFFFus
                        if langId <> 0us then
                            // 0x042A = Vietnamese language ID.
                            // Nếu Language Bar đang chọn Vietnamese -> BambooMintKey -> Chắc chắn SHOW (true)
                            // Nếu chọn bộ gõ khác (0x0411 Google Japanese, 0x0409 US English,...) -> Chắc chắn HIDE (false)
                            let isActive = (langId = 0x042Aus)
                            lastKnownActive <- isActive
                            isActive
                        else
                            // Fallback khi không lấy được HKL (ví dụ cửa sổ đặc biệt)
                            let active = ConfigStore.getIsActive()
                            lastKnownActive <- active
                            active
            else
                let active = ConfigStore.getIsActive()
                lastKnownActive <- active
                active
        with _ ->
            let active = ConfigStore.getIsActive()
            lastKnownActive <- active
            active

    let showNativeContextMenu () =
        let mutable pt = TrayWin32.POINT()
        TrayWin32.GetCursorPos(&pt) |> ignore

        let hMenu = TrayWin32.CreatePopupMenu()
        if hMenu = IntPtr.Zero then ()
        else
            try
                let cfg = ConfigStore.loadConfig()
                let isVn = cfg.IsVietnameseMode

                let vFlag = if isVn then TrayWin32.MF_CHECKED else 0u
                let eFlag = if not isVn then TrayWin32.MF_CHECKED else 0u
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING ||| vFlag, UIntPtr(uint64 menuCmdToggleVn), "Tiếng Việt (V)") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING ||| eFlag, UIntPtr(uint64 menuCmdToggleVn), "Tiếng Anh (E)") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_SEPARATOR, UIntPtr.Zero, String.Empty) |> ignore

                // Submenu: Kiểu gõ
                let hSubMethod = TrayWin32.CreatePopupMenu()
                let mTelex = if cfg.InputMethod = 0uy then TrayWin32.MF_CHECKED else 0u
                let mVni = if cfg.InputMethod = 1uy then TrayWin32.MF_CHECKED else 0u
                let mSimple = if cfg.InputMethod = 2uy then TrayWin32.MF_CHECKED else 0u
                TrayWin32.AppendMenuW(hSubMethod, TrayWin32.MF_STRING ||| mTelex, UIntPtr(uint64 menuCmdMethodTelex), "Telex") |> ignore
                TrayWin32.AppendMenuW(hSubMethod, TrayWin32.MF_STRING ||| mVni, UIntPtr(uint64 menuCmdMethodVni), "VNI") |> ignore
                TrayWin32.AppendMenuW(hSubMethod, TrayWin32.MF_STRING ||| mSimple, UIntPtr(uint64 menuCmdMethodSimple), "Simple Telex") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_POPUP, UIntPtr(uint64 (hSubMethod.ToInt64())), "Kiểu gõ") |> ignore

                // Submenu: Bảng mã
                let hSubCharset = TrayWin32.CreatePopupMenu()
                let cPre = if cfg.Charset = 0uy then TrayWin32.MF_CHECKED else 0u
                let cDec = if cfg.Charset = 1uy then TrayWin32.MF_CHECKED else 0u
                let cTcvn = if cfg.Charset = 2uy then TrayWin32.MF_CHECKED else 0u
                TrayWin32.AppendMenuW(hSubCharset, TrayWin32.MF_STRING ||| cPre, UIntPtr(uint64 menuCmdCharsetPrecomposed), "Unicode dựng sẵn") |> ignore
                TrayWin32.AppendMenuW(hSubCharset, TrayWin32.MF_STRING ||| cDec, UIntPtr(uint64 menuCmdCharsetDecomposed), "Unicode tổ hợp") |> ignore
                TrayWin32.AppendMenuW(hSubCharset, TrayWin32.MF_STRING ||| cTcvn, UIntPtr(uint64 menuCmdCharsetTcvn3), "TCVN3 (ABC)") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_POPUP, UIntPtr(uint64 (hSubCharset.ToInt64())), "Bảng mã") |> ignore

                // Submenu: Kiểu đặt dấu thanh
                let hSubTone = TrayWin32.CreatePopupMenu()
                let tModern = if cfg.ToneStyle = 0uy then TrayWin32.MF_CHECKED else 0u
                let tClassic = if cfg.ToneStyle = 1uy then TrayWin32.MF_CHECKED else 0u
                TrayWin32.AppendMenuW(hSubTone, TrayWin32.MF_STRING ||| tModern, UIntPtr(uint64 menuCmdToneModern), "Kiểu mới (òa, xòe, thủy)") |> ignore
                TrayWin32.AppendMenuW(hSubTone, TrayWin32.MF_STRING ||| tClassic, UIntPtr(uint64 menuCmdToneClassic), "Kiểu cũ (oà, xoè, thuỷ)") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_POPUP, UIntPtr(uint64 (hSubTone.ToInt64())), "Kiểu đặt dấu thanh") |> ignore

                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_SEPARATOR, UIntPtr.Zero, String.Empty) |> ignore

                // Tùy chọn gõ thông minh
                let freeFlag = if cfg.AllowFreeTonePlacement then TrayWin32.MF_CHECKED else 0u
                let undoFlag = if cfg.AllowRepeatKeyUndo then TrayWin32.MF_CHECKED else 0u
                let wFlag = if cfg.AllowLeadingWAsU then TrayWin32.MF_CHECKED else 0u
                let enFlag = if cfg.EnableEnglishBacktracking then TrayWin32.MF_CHECKED else 0u
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING ||| freeFlag, UIntPtr(uint64 menuCmdFreeTone), "Cho phép bỏ dấu tự do") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING ||| undoFlag, UIntPtr(uint64 menuCmdRepeatUndo), "Gõ lặp dấu để khôi phục (ss -> s)") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING ||| wFlag, UIntPtr(uint64 menuCmdLeadingW), "Phím 'w' đầu từ thành 'ư' (w -> ư)") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING ||| enFlag, UIntPtr(uint64 menuCmdEnglishBacktrack), "Tự động nhận diện từ tiếng Anh") |> ignore

                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_SEPARATOR, UIntPtr.Zero, String.Empty) |> ignore

                // Điều hướng
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING, UIntPtr(uint64 menuCmdOpenSettings), "Bảng điều khiển & Cài đặt...") |> ignore
                TrayWin32.AppendMenuW(hMenu, TrayWin32.MF_STRING, UIntPtr(uint64 menuCmdOpenAbout), "Thông tin BambooMintKey...") |> ignore

                let hWndFore = TrayWin32.GetForegroundWindow()
                if hWndFore <> IntPtr.Zero then TrayWin32.SetForegroundWindow(hWndFore) |> ignore

                let cmd = TrayWin32.TrackPopupMenuEx(hMenu, TrayWin32.TPM_RETURNCMD ||| TrayWin32.TPM_RIGHTBUTTON, pt.X, pt.Y, hMsgWnd, IntPtr.Zero)
                if cmd <> 0u then
                    match cmd with
                    | c when c = menuCmdToggleVn ->
                        cfg.IsVietnameseMode <- not cfg.IsVietnameseMode
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdMethodTelex ->
                        cfg.InputMethod <- 0uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdMethodVni ->
                        cfg.InputMethod <- 1uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdMethodSimple ->
                        cfg.InputMethod <- 2uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdCharsetPrecomposed ->
                        cfg.Charset <- 0uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdCharsetDecomposed ->
                        cfg.Charset <- 1uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdCharsetTcvn3 ->
                        cfg.Charset <- 2uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdToneModern ->
                        cfg.ToneStyle <- 0uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdToneClassic ->
                        cfg.ToneStyle <- 1uy
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdFreeTone ->
                        cfg.AllowFreeTonePlacement <- not cfg.AllowFreeTonePlacement
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdRepeatUndo ->
                        cfg.AllowRepeatKeyUndo <- not cfg.AllowRepeatKeyUndo
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdLeadingW ->
                        cfg.AllowLeadingWAsU <- not cfg.AllowLeadingWAsU
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdEnglishBacktrack ->
                        cfg.EnableEnglishBacktracking <- not cfg.EnableEnglishBacktracking
                        ConfigStore.saveConfig cfg
                    | c when c = menuCmdOpenSettings ->
                        Dispatcher.UIThread.Post(Action(onOpenSettings))
                    | c when c = menuCmdOpenAbout ->
                        Dispatcher.UIThread.Post(Action(onOpenAbout))
                    | _ -> ()
            finally
                TrayWin32.DestroyMenu(hMenu) |> ignore

    let updateTrayIconImage (isVn: bool) =
        currentModeVn <- isVn
        nid.hIcon <- if isVn then hIconV else hIconE
        nid.szTip <- if isVn then "BambooMintKey: Tiếng Việt (V)" else "BambooMintKey: English (E)"
        if isIconAdded then
            TrayWin32.Shell_NotifyIconW(TrayWin32.NIM_MODIFY, &nid) |> ignore

    let showTrayIcon () =
        if not isIconAdded then
            nid.hIcon <- if currentModeVn then hIconV else hIconE
            nid.szTip <- if currentModeVn then "BambooMintKey: Tiếng Việt (V)" else "BambooMintKey: English (E)"
            let ok = TrayWin32.Shell_NotifyIconW(TrayWin32.NIM_ADD, &nid)
            if ok then isIconAdded <- true

    let hideTrayIcon () =
        if isIconAdded then
            TrayWin32.Shell_NotifyIconW(TrayWin32.NIM_DELETE, &nid) |> ignore
            isIconAdded <- false

    let wndProc (hWnd: IntPtr) (msg: uint32) (wParam: UIntPtr) (lParam: IntPtr) : IntPtr =
        if msg = TrayWin32.WM_TRAYCALLBACK then
            let mouseMsg = uint32 (lParam.ToInt64() &&& 0xFFFFL)
            if mouseMsg = TrayWin32.WM_LBUTTONUP then
                // Click chuột trái: Đảo nhanh V/E
                let cfg = ConfigStore.loadConfig()
                cfg.IsVietnameseMode <- not cfg.IsVietnameseMode
                ConfigStore.saveConfig cfg
                updateTrayIconImage cfg.IsVietnameseMode
                IntPtr.Zero
            elif mouseMsg = TrayWin32.WM_RBUTTONUP then
                // Click chuột phải: Mở Context Menu
                showNativeContextMenu()
                IntPtr.Zero
            else
                TrayWin32.DefWindowProcW(hWnd, msg, wParam, lParam)
        elif msg = msgTaskbarCreated && msgTaskbarCreated <> 0u then
            // Khi Explorer restart, tạo lại icon nếu đang active
            isIconAdded <- false
            if lastKnownActive then
                showTrayIcon()
            IntPtr.Zero
        else
            TrayWin32.DefWindowProcW(hWnd, msg, wParam, lParam)

    let checkVisibilityAndState () =
        let active = isBambooMintKeyActive()
        let isVn = ConfigStore.getIsVietnameseMode()

        if active then
            if not isIconAdded then
                showTrayIcon()
            if isVn <> currentModeVn then
                updateTrayIconImage isVn
        else
            if isIconAdded then
                hideTrayIcon()

    member this.Start() =
        msgTaskbarCreated <- TrayWin32.RegisterWindowMessageW("TaskbarCreated")
        hIconV <- TrayIconGdi.createLetterIcon("V")
        hIconE <- TrayIconGdi.createLetterIcon("E")
        currentModeVn <- ConfigStore.getIsVietnameseMode()

        // 1. Tạo Message-Only Window
        let className = "BambooMintKey_Tray_MsgWnd_" + Guid.NewGuid().ToString("N")
        wndProcDelegate <- TrayWin32.WNDPROC(wndProc)

        let mutable wc = TrayWin32.WNDCLASSEXW()
        wc.cbSize <- uint32 (Marshal.SizeOf<TrayWin32.WNDCLASSEXW>())
        wc.style <- 0u
        wc.lpfnWndProc <- wndProcDelegate
        wc.hInstance <- Process.GetCurrentProcess().Handle
        wc.lpszClassName <- className

        let atom = TrayWin32.RegisterClassExW(&wc)
        if atom <> 0us then
            // HWND_MESSAGE = -3
            hMsgWnd <- TrayWin32.CreateWindowExW(0u, className, "BambooMintKey_Tray_MsgWnd", 0u, 0, 0, 0, 0, IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)

        // 2. Cấu hình NOTIFYICONDATAW
        nid.cbSize <- uint32 (Marshal.SizeOf<TrayWin32.NOTIFYICONDATAW>())
        nid.hWnd <- hMsgWnd
        nid.uID <- 1001u
        nid.uFlags <- TrayWin32.NIF_MESSAGE ||| TrayWin32.NIF_ICON ||| TrayWin32.NIF_TIP
        nid.uCallbackMessage <- TrayWin32.WM_TRAYCALLBACK
        nid.hIcon <- if currentModeVn then hIconV else hIconE
        nid.szTip <- if currentModeVn then "BambooMintKey: Tiếng Việt (V)" else "BambooMintKey: English (E)"

        // 3. Khởi tạo trạng thái ban đầu
        checkVisibilityAndState()

        // 4. Timer định kỳ 100ms kiểm tra đổi cửa sổ / đổi bộ gõ
        visibilityTimer <- DispatcherTimer(TimeSpan.FromMilliseconds(100.0), DispatcherPriority.Background, EventHandler(fun _ _ -> checkVisibilityAndState()))
        visibilityTimer.Start()

        // 5. Thread lắng nghe sự kiện đồng bộ StateChangedEvent (0ms response khi ấn phím tắt V/E)
        let eventThread = Thread(fun () ->
            let mutable hEvent = IntPtr.Zero
            while isRunning do
                try
                    if hEvent = IntPtr.Zero then
                        hEvent <- ConfigStore.OpenEventW(TrayWin32.SYNCHRONIZE, false, @"Local\BambooMintKey_StateChangedEvent_v1")

                    if hEvent <> IntPtr.Zero then
                        let wr = TrayWin32.WaitForSingleObject(hEvent, 500u)
                        if wr = 0u && isRunning then
                            Dispatcher.UIThread.Post(Action(fun () -> checkVisibilityAndState()))
                    else
                        Thread.Sleep(500)
                with _ -> Thread.Sleep(500)
            if hEvent <> IntPtr.Zero then
                TrayWin32.CloseHandle(hEvent) |> ignore
        )
        eventThread.IsBackground <- true
        eventThread.Name <- "BambooMintKey_Tray_StateEventWatcher"
        eventThread.Start()

    member this.Stop() =
        isRunning <- false
        if visibilityTimer <> null then
            visibilityTimer.Stop()
            visibilityTimer <- null
        hideTrayIcon()
        if hIconV <> IntPtr.Zero then
            TrayWin32.DestroyIcon(hIconV) |> ignore
            hIconV <- IntPtr.Zero
        if hIconE <> IntPtr.Zero then
            TrayWin32.DestroyIcon(hIconE) |> ignore
            hIconE <- IntPtr.Zero
        if hMsgWnd <> IntPtr.Zero then
            TrayWin32.DestroyWindow(hMsgWnd) |> ignore
            hMsgWnd <- IntPtr.Zero
