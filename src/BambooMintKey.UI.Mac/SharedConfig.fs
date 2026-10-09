// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
namespace BambooMintKey.UI.Mac

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

/// Cấu hình ứng dụng (tương ứng schema config.json, camelCase khớp C-ABI `bmk_load_config_json`).
type AppConfig() =
    member val Version = 2 with get, set
    member val InputMethod = 0 with get, set          // 0=Telex, 1=VNI, 2=Simple Telex
    member val Charset = 0 with get, set              // 0=Unicode dựng sẵn, 1=Unicode tổ hợp, 2=TCVN3
    member val ToneStyle = 0 with get, set            // 0=kiểu mới, 1=kiểu cũ
    member val AllowRepeatKeyUndo = true with get, set
    member val AllowLeadingWAsU = false with get, set
    member val AllowFreeTonePlacement = true with get, set
    member val EnableVietnameseDictionary = true with get, set
    member val EnableEnglishBacktracking = true with get, set
    member val EnablePreedit = false with get, set
    member val IsVietnameseMode = true with get, set  // trạng thái V/E
    member val MacroEnabled = false with get, set
    member val Macros = Dictionary<string, string>() with get, set

/// Lưu trữ & nạp cấu hình theo chuẩn macOS (Application Support) + ghi nguyên tử (atomic).
module ConfigStore =

    /// Thư mục Application Support của người dùng:
    /// ~/Library/Application Support/BambooMintKey/
    let private configDir () =
        let appSupport =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Path.Combine(appSupport, "BambooMintKey")

    /// Đường dẫn file config.json.
    let configPath () = Path.Combine(configDir (), "config.json")

    let private options =
        JsonSerializerOptions(
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)

    /// Nạp cấu hình; nếu chưa có file thì trả về cấu hình mặc định.
    let loadConfig () : AppConfig =
        let path = configPath ()
        if File.Exists(path) then
            try
                JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options)
            with _ ->
                AppConfig()
        else
            AppConfig()

    /// Ghi cấu hình nguyên tử: ghi vào file tạm rồi rename đè (tránh bộ gõ đọc file ghi dở).
    let saveConfig (cfg: AppConfig) =
        let path = configPath ()
        Directory.CreateDirectory(configDir ()) |> ignore
        let tmp = path + ".tmp." + Guid.NewGuid().ToString("N")
        let json = JsonSerializer.Serialize(cfg, options)
        File.WriteAllText(tmp, json)
        File.Move(tmp, path, true) // atomic rename (cùng filesystem)
