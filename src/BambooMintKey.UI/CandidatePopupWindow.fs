// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "9"
#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Runtime.InteropServices
open System.Text
open System.Threading
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open FSharp.NativeInterop

/// Dữ liệu trang ứng viên đọc từ SharedMemory Local\BambooMintKey_CandidatePopup_v1.
type CandidatePopupSnapshot = {
    Sequence: int
    Visible: bool
    AnchorLeft: int
    AnchorTop: int
    AnchorRight: int
    AnchorBottom: int
    SelectedInPage: int
    PageIndex: int
    TotalPages: int
    TotalCount: int
    RawQuery: string
    Items: string array
}

module private CandidatePopupWin32 =
    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type SECURITY_ATTRIBUTES =
        struct
            val mutable nLength: int
            val mutable lpSecurityDescriptor: IntPtr
            [<MarshalAs(UnmanagedType.Bool)>]
            val mutable bInheritHandle: bool
        end

    [<DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern [<MarshalAs(UnmanagedType.Bool)>] bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor,
        uint32 stringSdRevision,
        IntPtr& securityDescriptor,
        IntPtr securityDescriptorSize)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern IntPtr LocalFree(IntPtr hMem)

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr CreateFileMappingW(
        IntPtr hFile, IntPtr lpFileMappingAttributes, uint32 flProtect,
        uint32 dwMaximumSizeHigh, uint32 dwMaximumSizeLow, string lpName)

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr OpenFileMappingW(uint32 dwDesiredAccess, [<MarshalAs(UnmanagedType.Bool)>] bool bInheritHandle, string lpName)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint32 dwDesiredAccess, uint32 dwFileOffsetHigh, uint32 dwFileOffsetLow, nativeint dwNumberOfBytesToMap)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool UnmapViewOfFile(IntPtr lpBaseAddress)

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr CreateEventW(IntPtr lpEventAttributes, [<MarshalAs(UnmanagedType.Bool)>] bool bManualReset, [<MarshalAs(UnmanagedType.Bool)>] bool bInitialState, string lpName)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern uint32 WaitForSingleObject(IntPtr hHandle, uint32 dwMilliseconds)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool CloseHandle(IntPtr hObject)

    [<DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)>]
    extern nativeint GetWindowLongPtrW(IntPtr hWnd, int nIndex)

    [<DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)>]
    extern nativeint SetWindowLongPtrW(IntPtr hWnd, int nIndex, nativeint dwNewLong)

    let GWL_EXSTYLE = -20
    let WS_EX_TOPMOST = 0x00000008n
    let WS_EX_TOOLWINDOW = 0x00000080n
    let WS_EX_NOACTIVATE = 0x08000000n

    let PAGE_READWRITE = 0x04u
    let FILE_MAP_READ = 0x04u
    let FILE_MAP_ALL_ACCESS = 0xF001Fu
    let UniversalSddl = "D:(A;;GA;;;WD)(A;;GA;;;AC)(A;;GA;;;S-1-15-2-2)S:(ML;;NW;;;LW)"

    let applyNoActivateStyle (hWnd: IntPtr) =
        if hWnd <> IntPtr.Zero then
            try
                let current = GetWindowLongPtrW(hWnd, GWL_EXSTYLE)
                let updated = current ||| WS_EX_NOACTIVATE ||| WS_EX_TOOLWINDOW ||| WS_EX_TOPMOST
                SetWindowLongPtrW(hWnd, GWL_EXSTYLE, updated) |> ignore
            with _ -> ()

/// <summary>
/// Cửa sổ Avalonia hiển thị danh sách ứng viên ký hiệu theo chiều dọc (tối đa 7 mục/trang, phong cách IME Nhật).
/// Chạy hoàn toàn trong tiến trình BambooMintKey.UI (out-of-process), không bao giờ cướp focus
/// và không thể gây crash ứng dụng đang gõ.
/// Thiết kế theo 013_03_Avalonia_Candidate_Popup_Design.md.
/// </summary>
[<AllowNullLiteral>]
type CandidatePopupWindow() as this =
    inherit Window()

    let headerQueryText = TextBlock(
        FontSize = 12.0,
        FontWeight = FontWeight.SemiBold,
        Foreground = SolidColorBrush(Color.Parse("#1B8A5A")),
        VerticalAlignment = VerticalAlignment.Center)

    let headerCountText = TextBlock(
        FontSize = 11.0,
        Foreground = SolidColorBrush(Color.Parse("#6E7781")),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right)

    let itemsPanel = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)

    let footerHintText = TextBlock(
        FontSize = 11.0,
        Foreground = SolidColorBrush(Color.Parse("#6E7781")),
        VerticalAlignment = VerticalAlignment.Center)

    let footerPageText = TextBlock(
        FontSize = 11.0,
        FontWeight = FontWeight.SemiBold,
        Foreground = SolidColorBrush(Color.Parse("#1B8A5A")),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right)

    let selBgBrush = SolidColorBrush(Color.Parse("#E8F5EE"))
    let accentBrush = SolidColorBrush(Color.Parse("#1B8A5A"))
    let mutedBrush = SolidColorBrush(Color.Parse("#656D76"))
    let textBrush = SolidColorBrush(Color.Parse("#1F2328"))

    do
        this.WindowDecorations <- WindowDecorations.None
        this.CanResize <- false
        this.Topmost <- true
        this.ShowInTaskbar <- false
        this.ShowActivated <- false
        this.Focusable <- false
        this.Background <- Brushes.Transparent
        this.TransparencyLevelHint <- [ WindowTransparencyLevel.Transparent ]
        this.SizeToContent <- SizeToContent.WidthAndHeight

        // Header
        let queryPill = Border(
            Background = SolidColorBrush(Color.Parse("#E8F5EE")),
            CornerRadius = CornerRadius(5.0),
            Padding = Thickness(6.0, 2.0),
            Child = headerQueryText)

        let headerGrid = Grid(Margin = Thickness(2.0, 0.0, 2.0, 4.0))
        headerGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength.Star))
        headerGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength.Auto))
        Grid.SetColumn(queryPill, 0)
        Grid.SetColumn(headerCountText, 1)
        headerGrid.Children.Add(queryPill)
        headerGrid.Children.Add(headerCountText)

        // Divider
        let divider = Border(
            Height = 1.0,
            Background = SolidColorBrush(Color.Parse("#EAECEF")),
            Margin = Thickness(2.0, 4.0, 2.0, 4.0))

        // Footer
        let footerGrid = Grid(Margin = Thickness(4.0, 0.0, 4.0, 0.0))
        footerGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength.Star))
        footerGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength.Auto))
        Grid.SetColumn(footerHintText, 0)
        Grid.SetColumn(footerPageText, 1)
        footerGrid.Children.Add(footerHintText)
        footerGrid.Children.Add(footerPageText)

        let rootStack = StackPanel(Orientation = Orientation.Vertical)
        rootStack.Children.Add(headerGrid)
        rootStack.Children.Add(itemsPanel)
        rootStack.Children.Add(divider)
        rootStack.Children.Add(footerGrid)

        let card = Border(
            Margin = Thickness(8.0),
            MinWidth = 204.0,
            Padding = Thickness(8.0, 7.0),
            CornerRadius = CornerRadius(10.0),
            Background = SolidColorBrush(Color.Parse("#FCFCFD")),
            BorderBrush = SolidColorBrush(Color.Parse("#D0D7DE")),
            BorderThickness = Thickness(1.0),
            BoxShadow = BoxShadows.Parse("0 6 18 0 #28000000"),
            Child = rootStack)

        this.Content <- card

        this.Opened.Add(fun _ ->
            match this.TryGetPlatformHandle() with
            | null -> ()
            | handle -> CandidatePopupWin32.applyNoActivateStyle handle.Handle)

    let buildRow (indexInPage: int) (symbol: string) (isSelected: bool) : Control =
        let rowGrid = Grid()
        rowGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength(6.0)))
        rowGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength(22.0)))
        rowGrid.ColumnDefinitions.Add(ColumnDefinition(GridLength.Star))

        if isSelected then
            let accentBar = Border(
                Width = 3.0,
                Height = 16.0,
                CornerRadius = CornerRadius(1.5),
                Background = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center)
            Grid.SetColumn(accentBar, 0)
            rowGrid.Children.Add(accentBar)

        let idxText = TextBlock(
            Text = string (indexInPage + 1),
            FontSize = 12.0,
            FontWeight = FontWeight.SemiBold,
            Foreground = (if isSelected then accentBrush else mutedBrush),
            VerticalAlignment = VerticalAlignment.Center)
        Grid.SetColumn(idxText, 1)
        rowGrid.Children.Add(idxText)

        let symText = TextBlock(
            Text = symbol,
            FontSize = 16.0,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center)
        Grid.SetColumn(symText, 2)
        rowGrid.Children.Add(symText)

        Border(
            CornerRadius = CornerRadius(6.0),
            Padding = Thickness(4.0, 5.0, 8.0, 5.0),
            Background = (if isSelected then selBgBrush :> IBrush else Brushes.Transparent :> IBrush),
            Child = rowGrid)

    let positionNearAnchor (snap: CandidatePopupSnapshot) =
        let scaling = if this.RenderScaling > 0.0 then this.RenderScaling else 1.0
        let estWidth = int (Math.Round(224.0 * scaling))
        let estHeight = int (Math.Round((82.0 + float snap.Items.Length * 32.0) * scaling))
        let gap = int (Math.Round(6.0 * scaling))

        let mutable x = snap.AnchorLeft
        let mutable y = snap.AnchorBottom + gap

        let anchorPt = PixelPoint(snap.AnchorLeft, snap.AnchorBottom)
        let screen =
            match this.Screens.ScreenFromPoint(anchorPt) with
            | null -> this.Screens.Primary
            | s -> s

        if screen <> null then
            let work = screen.WorkingArea
            if y + estHeight > work.Bottom then
                let aboveY = snap.AnchorTop - estHeight - gap
                y <- if aboveY >= work.Y then aboveY else Math.Max(work.Y, work.Bottom - estHeight)
            if x + estWidth > work.Right then
                x <- Math.Max(work.X, work.Right - estWidth)
            if x < work.X then
                x <- work.X

        this.Position <- PixelPoint(x, y)

    member this.ApplySnapshot(snap: CandidatePopupSnapshot) =
        if not snap.Visible || snap.Items.Length = 0 then
            if this.IsVisible then
                this.Hide()
        else
            headerQueryText.Text <- "::" + snap.RawQuery
            headerCountText.Text <- string snap.TotalCount + " mục"

            itemsPanel.Children.Clear()
            for i = 0 to snap.Items.Length - 1 do
                itemsPanel.Children.Add(buildRow i snap.Items.[i] (i = snap.SelectedInPage))

            footerHintText.Text <- if snap.TotalPages > 1 then "↑↓ · ←→ · 1-7" else "↑↓/Tab · 1-7"
            footerPageText.Text <-
                if snap.TotalPages > 1 then string (snap.PageIndex + 1) + "/" + string snap.TotalPages
                else ""

            positionNearAnchor snap
            if not this.IsVisible then
                this.Show()
                match this.TryGetPlatformHandle() with
                | null -> ()
                | handle -> CandidatePopupWin32.applyNoActivateStyle handle.Handle

/// <summary>
/// Quản lý vòng đời lắng nghe SharedMemory Local\BambooMintKey_CandidatePopup_v1
/// và cập nhật CandidatePopupWindow (Avalonia) tức thì khi NativeBridge phát sự kiện.
/// </summary>
[<AllowNullLiteral>]
type CandidatePopupHost() =
    let mapName = @"Local\BambooMintKey_CandidatePopup_v1"
    let eventName = @"Local\BambooMintKey_CandidatePopupEvent_v1"
    let sharedSize = 1024
    let payloadOffset = 52

    let mutable isRunning = false
    let mutable popupWindow: CandidatePopupWindow = null
    let mutable lastSequence = 0

    let readSnapshot (pView: IntPtr) : CandidatePopupSnapshot option =
        try
            let seq = NativePtr.read (NativePtr.ofNativeInt<int> pView)
            if seq = lastSequence then None
            else
                lastSequence <- seq
                let visible = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 4n)) <> 0
                let left = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 8n))
                let top = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 12n))
                let right = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 16n))
                let bottom = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 20n))
                let selInPage = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 24n))
                let pageIdx = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 28n))
                let totalPages = max 1 (NativePtr.read (NativePtr.ofNativeInt<int> (pView + 32n)))
                let totalCount = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 36n))
                let itemCount = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 40n))
                let payloadLen = NativePtr.read (NativePtr.ofNativeInt<int> (pView + 48n))

                let clampedLen = max 0 (min payloadLen (sharedSize - payloadOffset))
                let rawQuery, items =
                    if visible && clampedLen > 0 then
                        let buf = Array.zeroCreate<byte> clampedLen
                        Marshal.Copy(pView + nativeint payloadOffset, buf, 0, clampedLen)
                        let text = Encoding.UTF8.GetString(buf)
                        let parts = text.Split('\n')
                        let q = if parts.Length > 0 then parts.[0] else ""
                        let count = min (max 0 itemCount) (max 0 (parts.Length - 1))
                        let arr = if count > 0 then Array.sub parts 1 count else [||]
                        q, arr
                    else
                        "", [||]

                Some {
                    Sequence = seq
                    Visible = visible
                    AnchorLeft = left
                    AnchorTop = top
                    AnchorRight = right
                    AnchorBottom = bottom
                    SelectedInPage = selInPage
                    PageIndex = pageIdx
                    TotalPages = totalPages
                    TotalCount = totalCount
                    RawQuery = rawQuery
                    Items = items
                }
        with _ -> None

    member _.Start() =
        if isRunning then ()
        else
            isRunning <- true
            let t = Thread(fun () ->
                let mutable sa = CandidatePopupWin32.SECURITY_ATTRIBUTES()
                sa.nLength <- Marshal.SizeOf<CandidatePopupWin32.SECURITY_ATTRIBUTES>()
                sa.bInheritHandle <- false
                let mutable pSd = IntPtr.Zero
                let hasSd = CandidatePopupWin32.ConvertStringSecurityDescriptorToSecurityDescriptorW(CandidatePopupWin32.UniversalSddl, 1u, &pSd, IntPtr.Zero)
                if hasSd && pSd <> IntPtr.Zero then
                    sa.lpSecurityDescriptor <- pSd

                let pSa = Marshal.AllocHGlobal(Marshal.SizeOf<CandidatePopupWin32.SECURITY_ATTRIBUTES>())
                Marshal.StructureToPtr(sa, pSa, false)

                let mutable hMap = IntPtr.Zero
                let mutable pView = IntPtr.Zero
                let mutable hEvent = IntPtr.Zero

                try
                    let secPtr = if hasSd && pSd <> IntPtr.Zero then pSa else IntPtr.Zero
                    hMap <- CandidatePopupWin32.CreateFileMappingW(nativeint -1, secPtr, CandidatePopupWin32.PAGE_READWRITE, 0u, uint32 sharedSize, mapName)
                    if hMap = IntPtr.Zero then
                        hMap <- CandidatePopupWin32.OpenFileMappingW(CandidatePopupWin32.FILE_MAP_READ, false, mapName)
                    if hMap <> IntPtr.Zero then
                        pView <- CandidatePopupWin32.MapViewOfFile(hMap, CandidatePopupWin32.FILE_MAP_READ, 0u, 0u, nativeint sharedSize)

                    hEvent <- CandidatePopupWin32.CreateEventW(secPtr, false, false, eventName)

                    while isRunning do
                        try
                            if hEvent <> IntPtr.Zero then
                                CandidatePopupWin32.WaitForSingleObject(hEvent, 500u) |> ignore
                            else
                                Thread.Sleep(100)

                            if isRunning && pView <> IntPtr.Zero then
                                match readSnapshot pView with
                                | Some snap ->
                                    Dispatcher.UIThread.Post(Action(fun () ->
                                        try
                                            if popupWindow = null then
                                                popupWindow <- CandidatePopupWindow()
                                            popupWindow.ApplySnapshot(snap)
                                        with _ -> ()))
                                | None -> ()
                        with _ -> Thread.Sleep(200)
                finally
                    if pView <> IntPtr.Zero then CandidatePopupWin32.UnmapViewOfFile(pView) |> ignore
                    if hMap <> IntPtr.Zero then CandidatePopupWin32.CloseHandle(hMap) |> ignore
                    if hEvent <> IntPtr.Zero then CandidatePopupWin32.CloseHandle(hEvent) |> ignore
                    Marshal.FreeHGlobal(pSa)
                    if pSd <> IntPtr.Zero then CandidatePopupWin32.LocalFree(pSd) |> ignore
            )
            t.IsBackground <- true
            t.Name <- "BambooMintKey_CandidatePopupWatcher"
            t.Start()

    member _.Stop() =
        isRunning <- false
        Dispatcher.UIThread.Post(Action(fun () ->
            try
                if popupWindow <> null then
                    popupWindow.Close()
                    popupWindow <- null
            with _ -> ()))
