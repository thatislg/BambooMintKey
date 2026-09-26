// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.UI.Linux

open System
open Avalonia

module Program =

    [<CompiledName "BuildAvaloniaApp">]
    let buildAvaloniaApp () =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(areas = Array.empty)

    [<EntryPoint>]
    let main argv =
        // Single instance: nếu đã có instance đang chạy, thoát ngay (đã gửi SHOW_WINDOW).
        if SingleInstance.tryNotifyExisting() then
            0
        else
            // Lắng nghe Unix socket; khi nhận SHOW_WINDOW thì đưa cửa sổ lên trước.
            use _listener =
                SingleInstance.startListening(fun () ->
                    App.Current |> Option.iter (fun w -> w.BringToFront()))

            buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
