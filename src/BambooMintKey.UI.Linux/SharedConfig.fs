// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI.Linux

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

/// Cấu hình ứng dụng (tương ứng schema config.json chuẩn XDG).
type AppConfig() =
    member val Version = 2 with get, set
    member val InputMethod = 0 with get, set          // 0=Telex, 1=VNI, 2=Simple Telex
    member val Charset = 0 with get, set              // 0=Unicode dựng sẵn, 1=Unicode tổ hợp, 2=TCVN3
    member val ToneStyle = 0 with get, set            // 0=kiểu mới, 1=kiểu cũ
    member val AutoRestoreEnglishWords = true with get, set
    member val AllowRepeatKeyUndo = true with get, set
    member val AllowLeadingWAsU = false with get, set
    member val AllowFreeTonePlacement = true with get, set
    member val EnablePreedit = false with get, set
    member val IsVietnameseMode = true with get, set  // trạng thái V/E
    member val MacroEnabled = false with get, set
    member val Macros = Dictionary<string, string>() with get, set

/// Lưu trữ & nạp cấu hình theo chuẩn FreeDesktop XDG + ghi nguyên tử (atomic).
module ConfigStore =

    /// Thư mục cơ sở XDG_CONFIG_HOME, fallback ~/.config.
    let private configHome () =
        let xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
        if String.IsNullOrWhiteSpace(xdg) then
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
        else
            xdg

    /// Thư mục cấu hình ứng dụng: $XDG_CONFIG_HOME/bamboomintkey/
    let configDir () = Path.Combine(configHome(), "bamboomintkey")

    /// Đường dẫn file config.json.
    let configPath () = Path.Combine(configDir(), "config.json")

    let private options =
        JsonSerializerOptions(
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)

    /// Nạp cấu hình; nếu chưa có file thì trả về cấu hình mặc định.
    let loadConfig () : AppConfig =
        let path = configPath ()
        if File.Exists(path) then
            try
                File.ReadAllText(path) |> JsonSerializer.Deserialize<AppConfig>
            with _ ->
                AppConfig()
        else
            AppConfig()

    /// Ghi cấu hình nguyên tử: ghi vào file tạm rồi rename đè (tránh Fcitx5 đọc file ghi dở).
    let saveConfig (cfg: AppConfig) =
        let path = configPath ()
        Directory.CreateDirectory(configDir()) |> ignore
        let tmp = path + ".tmp." + Guid.NewGuid().ToString("N")
        let json = JsonSerializer.Serialize(cfg, options)
        File.WriteAllText(tmp, json)
        File.Move(tmp, path, true) // atomic rename (cùng filesystem)
