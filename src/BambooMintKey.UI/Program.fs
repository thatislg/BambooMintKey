// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.Threading
open Avalonia

module NativeWin32 =
    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool ShowWindow(IntPtr hWnd, int nCmdShow)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool SetForegroundWindow(IntPtr hWnd)

    [<DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr FindWindowW(string lpClassName, string lpWindowName)

    let SW_RESTORE = 9

module Program =

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace(areas = Array.empty)

    let private activateExistingInstance () =
        try
            // Phát tín hiệu qua EventWaitHandle để tiến trình Avalonia đang chạy
            // tự gọi mainWindow.BringToFront() trên UI thread (KHÔNG gọi Win32 ShowWindow
            // trực tiếp lên HWND đang bị Avalonia Hide(), vì sẽ gây cửa sổ xám không render được).
            let mutable showEvent: EventWaitHandle = null
            if EventWaitHandle.TryOpenExisting(@"Local\BambooMintKey_ShowMainWindow_Event_v1", &showEvent) then
                use ev = showEvent
                ev.Set() |> ignore
            else
                // Fallback chỉ đưa cửa sổ đang hiện sẵn lên foreground (không ép ShowWindow)
                let handle = NativeWin32.FindWindowW(null, "BambooMintKey — Bảng Điều Khiển Cài Đặt")
                if handle <> IntPtr.Zero then
                    NativeWin32.SetForegroundWindow(handle) |> ignore
        with _ -> ()

    [<EntryPoint; STAThread>]
    let main argv =
        let mutable createdNew = false
        use mutex = new Mutex(true, @"Local\BambooMintKey_UI_SingleInstance_Mutex", &createdNew)
        if not createdNew then
            activateExistingInstance()
            0
        else
            buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)

