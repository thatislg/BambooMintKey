// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI.Linux

open System
open System.Diagnostics
open System.Threading

/// D-Bus client điều khiển & đồng bộ trạng thái V/E với Fcitx5 addon.
/// Dùng lệnh `dbus-send` (tránh phụ thuộc thư viện D-Bus bên thứ ba).
module DbusClient =
    let private service = "org.fcitx.Fcitx5.BambooMintKey"
    let private path = "/org/fcitx/Fcitx5/BambooMintKey"
    let private iface = "org.fcitx.Fcitx5.BambooMintKey1"

    /// Chạy lệnh dbus-send và trả về stdout.
    let private run (args: string list) : string =
        try
            let psi = ProcessStartInfo("dbus-send", String.concat " " args)
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true
            use p = Process.Start(psi)
            let output = p.StandardOutput.ReadToEnd()
            p.WaitForExit()
            output
        with _ ->
            ""

    /// Gọi method D-Bus không tham số, trả về bool.
    let private callBool (method: string) : bool =
        run [ "--session"; "--print-reply"; sprintf "--dest=%s" service; path; sprintf "%s.%s" iface method ]
        |> fun o -> o.Contains("true")

    /// Gọi method D-Bus có tham số bool, không trả về.
    let private callVoid (method: string) (value: bool) =
        run [ "--session"; "--print-reply"; sprintf "--dest=%s" service; path; sprintf "%s.%s" iface method; sprintf "boolean:%b" value ]
        |> ignore

    /// Truy vấn trạng thái V/E hiện tại.
    let getVietnameseMode () : bool = callBool "GetVietnameseMode"

    /// Thiết lập trạng thái V/E.
    let setVietnameseMode (enabled: bool) = callVoid "SetVietnameseMode" enabled

    /// Đảo trạng thái V/E, trả về trạng thái mới.
    let toggleVietnameseMode () : bool = callBool "ToggleVietnameseMode"

    /// Lắng nghe signal `ModeChanged` từ addon qua `dbus-monitor` (không thêm dependency).
    /// Gọi `onChanged` mỗi khi chế độ V/E đổi từ bên ngoài (vd: bấm phím ` bên app khác).
    /// Trả về IDisposable để dừng tiến trình khi đóng ứng dụng.
    let startModeChangedListener (onChanged: bool -> unit) : IDisposable =
        let filter =
            sprintf "type='signal',interface='%s',member='ModeChanged'" iface
        let psi = ProcessStartInfo("dbus-monitor", filter)
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        let p =
            try
                Some(Process.Start(psi))
            with _ ->
                None
        match p with
        | None ->
            { new IDisposable with
                member _.Dispose() = () }
        | Some p ->
            let cts = new CancellationTokenSource()
            let worker =
                async {
                    while not cts.IsCancellationRequested do
                        try
                            let line = p.StandardOutput.ReadLine()
                            match line with
                            | null -> do! Async.Sleep 100
                            | s ->
                                let t = s.Trim()
                                if t.StartsWith("boolean") then
                                    onChanged (t.Contains("true"))
                        with _ ->
                            do! Async.Sleep 100
                }
            Async.Start(worker, cts.Token)
            { new IDisposable with
                member _.Dispose() =
                    cts.Cancel()
                    try
                        if not p.HasExited then p.Kill()
                    with _ ->
                        ()
                    try
                        p.Dispose()
                    with _ ->
                        () }
