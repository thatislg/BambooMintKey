// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI.Linux

open System
open System.Diagnostics

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
