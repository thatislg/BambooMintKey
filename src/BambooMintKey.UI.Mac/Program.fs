// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.UI.Mac

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
        // Parse argument tùy chọn: --tab <name> (vd --tab about) để mở thẳng một tab.
        let args = List.ofArray argv
        let rec findTab = function
            | "--tab" :: name :: _ -> Some name
            | _ :: rest -> findTab rest
            | [] -> None
        App.RequestedTab <- findTab args

        // Single instance: nếu đã có instance đang chạy, thoát ngay (đã gửi SHOW_WINDOW).
        if SingleInstance.tryNotifyExisting() then
            0
        else
            // Lắng nghe Unix socket; khi nhận SHOW_WINDOW thì đưa cửa sổ lên trước.
            use _listener =
                SingleInstance.startListening(fun () ->
                    App.Current |> Option.iter (fun w -> w.BringToFront()))

            buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
