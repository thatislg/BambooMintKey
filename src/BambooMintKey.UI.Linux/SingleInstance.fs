// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI.Linux

open System
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading

/// Cơ chế single-instance chuẩn POSIX: dùng Unix Domain Socket thay cho Win32.
module SingleInstance =

    /// Đường dẫn socket: $XDG_RUNTIME_DIR/bamboomintkey-ui.sock (fallback /tmp).
    let socketPath () =
        let runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
        let dir = if String.IsNullOrWhiteSpace(runtime) then Path.GetTempPath() else runtime
        Path.Combine(dir, "bamboomintkey-ui.sock")

    /// Thử kết nối tới socket của instance đang chạy.
    /// Trả về true nếu đã có instance (đã gửi lệnh SHOW_WINDOW).
    let tryNotifyExisting () : bool =
        let path = socketPath ()
        if not (File.Exists(path)) then false
        else
            try
                use client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                client.Connect(UnixDomainSocketEndPoint(path))
                client.Send(Encoding.UTF8.GetBytes("SHOW_WINDOW")) |> ignore
                true
            with _ ->
                false

    /// Tạo socket lắng nghe; gọi onShow khi nhận lệnh SHOW_WINDOW từ instance mới.
    /// Trả về IDisposable để dọn dẹp socket khi đóng ứng dụng.
    let startListening (onShow: unit -> unit) : IDisposable =
        let path = socketPath ()
        if File.Exists(path) then
            try File.Delete(path) with _ -> ()

        let server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
        server.Bind(UnixDomainSocketEndPoint(path))
        server.Listen(1)

        let cts = new CancellationTokenSource()
        let worker =
            async {
                while not cts.IsCancellationRequested do
                    try
                        use client = server.Accept()
                        let buffer = Array.zeroCreate<byte> 1024
                        let n = client.Receive(buffer)
                        if n > 0 then onShow ()
                    with _ ->
                        ()
            }
        Async.Start(worker, cts.Token)

        { new IDisposable with
            member _.Dispose() =
                cts.Cancel()
                try server.Dispose() with _ -> ()
                try File.Delete(path) with _ -> () }
