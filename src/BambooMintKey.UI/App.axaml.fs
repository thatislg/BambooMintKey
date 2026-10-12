#nowarn "3261"
namespace BambooMintKey.UI

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    let mutable trayManager: TrayIconManager = null

    override this.Initialize() =
        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.ShutdownMode <- ShutdownMode.OnExplicitShutdown
            let args = match desktop.Args with null -> [||] | a -> a
            let mainWindow = MainWindow(args)
            desktop.MainWindow <- mainWindow

            let openSettings () =
                mainWindow.SelectMainTab()
                mainWindow.ReloadSettings()
                mainWindow.Show()
                mainWindow.Activate()

            let openAbout () =
                mainWindow.SelectAboutTab()
                mainWindow.ReloadSettings()
                mainWindow.Show()
                mainWindow.Activate()

            trayManager <- TrayIconManager(openSettings, openAbout)
            trayManager.Start()

            let isTrayMode =
                args |> Array.exists (fun a ->
                    a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--background", StringComparison.OrdinalIgnoreCase))

            if not isTrayMode then
                mainWindow.Show()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
