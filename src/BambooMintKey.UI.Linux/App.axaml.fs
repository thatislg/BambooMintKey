// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.UI.Linux

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    /// Tham chiếu cửa sổ chính (dùng cho single instance bring-to-front).
    static member val Current : MainWindow option = None with get, set

    override this.Initialize() =
        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            let window = MainWindow()
            desktop.MainWindow <- window
            App.Current <- Some window
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
