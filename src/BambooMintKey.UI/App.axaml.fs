#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Threading
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open Avalonia.Threading

type App() =
    inherit Application()

    let mutable trayManager: TrayIconManager = null
    let mutable uiProcessMutex: Mutex = null

    override this.Initialize() =
        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.ShutdownMode <- ShutdownMode.OnExplicitShutdown

            let mutable createdNew = false
            try
                uiProcessMutex <- new Mutex(true, @"Local\BambooMintKey_UI_Process_Mutex_v1", &createdNew)
            with _ -> createdNew <- true

            let args = match desktop.Args with null -> [||] | a -> a
            let mainWindow = MainWindow(args)
            desktop.MainWindow <- mainWindow

            let openSettings () =
                mainWindow.SelectMainTab()
                mainWindow.ReloadSettings()
                mainWindow.BringToFront()

            let openAbout () =
                mainWindow.SelectAboutTab()
                mainWindow.ReloadSettings()
                mainWindow.BringToFront()

            let onExit () =
                if trayManager <> null then
                    trayManager.Stop()
                mainWindow.ForceClose()
                desktop.Shutdown(0)

            if not createdNew then
                // Đã có một phiên bản BambooMintKey đang chạy trong cùng user session
                // Phát tín hiệu để phiên bản trước mở Bảng điều khiển và thoát tiến trình trùng lặp
                try
                    use showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BambooMintKey_ShowMainWindow_Event_v1")
                    showEvent.Set() |> ignore
                with _ -> ()
                desktop.Shutdown(0)
            else
                // 1. Tự động kiểm tra và đăng ký TSF COM DLL vào C:\ProgramData\BambooMintKey
                try
                    TsfBootstrapper.ensureRegistered() |> ignore
                with _ -> ()

                // 2. Lắng nghe tín hiệu từ các lần mở tiếp theo để hiện Bảng điều khiển
                let listenThread = Thread(fun () ->
                    try
                        use showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BambooMintKey_ShowMainWindow_Event_v1")
                        while true do
                            showEvent.WaitOne() |> ignore
                            Dispatcher.UIThread.Post(Action(fun () -> openSettings()))
                    with _ -> ()
                )
                listenThread.IsBackground <- true
                listenThread.Name <- "BambooMintKey_SingleInstanceListener"
                listenThread.Start()

                // 3. Khởi chạy Tray Icon luôn luôn hiển thị trên Taskbar
                trayManager <- TrayIconManager(openSettings, openAbout, onExit)
                trayManager.Start()

                let isTrayMode =
                    args |> Array.exists (fun a ->
                        a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--background", StringComparison.OrdinalIgnoreCase))

                if not isTrayMode then
                    mainWindow.Show()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
