# 003_04_SharedConfiguration_Schema.md

> Tài liệu kỹ thuật chi tiết về thiết kế Schema cấu hình dùng chung (`config.json`), module phân tích cú pháp (Parser) trong F# Core, cơ chế đồng bộ nóng (Hot-Reload) không cần khởi động lại tiến trình, và cầu nối lưu trữ đa nền tảng (Windows/Linux).

## 1. Cơ sở chuẩn hóa & Phân tích kiến trúc

### 1.1. Nguyên tắc thiết kế hợp đồng cấu hình (Data Contract)

- **Phi nền tảng (Platform-Agnostic):** Schema sử dụng chuẩn JSON thuần túy (UTF-8 không BOM), không chứa bất kỳ định danh cụ thể nào của Windows Registry hay Linux GSettings.
- **Độc lập phụ thuộc (Zero Third-Party Dependency):** F# Core là tầng tính toán thuần túy (`BambooMintKey.Core`), không phụ thuộc vào các thư viện JSON bên ngoài (như `Newtonsoft.Json` hay `System.Text.Json` cồng kềnh) để đảm bảo biên dịch NativeAOT ra mã máy nhỏ gọn và giữ thời gian khởi động ở mức micro-second.
- **Bảo toàn dữ liệu (Safe Defaults & Fault-Tolerance):** Nếu tệp cấu hình bị lỗi cú pháp hoặc thiếu trường do người dùng chỉnh sửa tay, hệ thống tự động rơi về cấu hình mặc định an toàn (`Fallback Defaults`) mà không gây crash engine.

### 1.2. Vị trí lưu trữ tệp trên từng hệ điều hành

| **Hệ điều hành**   | **Đường dẫn lưu trữ tiêu chuẩn**                             |
| ------------------ | ------------------------------------------------------------ |
| **Windows**        | `%AppData%\BambooMintKey\config.json`  `C:\Users\<User>\AppData\Roaming\BambooMintKey\config.json` |
| **Linux (Fcitx5)** | `$XDG_CONFIG_HOME/bamboomintkey/config.json`  `~/.config/bamboomintkey/config.json` |

## 2. Đặc tả JSON Schema (`config.json`)

Tệp cấu hình được phiên bản hóa (`version`) để hỗ trợ nâng cấp (migration) trong tương lai.

JSON

```json
{
  "version": 1,
  "inputMethod": "Telex",
  "charset": "Unicode",
  "toggleHotkey": "CtrlShift",
  "spellCheck": true,
  "autoRestoreIfInvalid": true,
  "useModernOrthography": true,
  "macroEnabled": false,
  "macros": {
    "vn": "Việt Nam",
    "bmk": "BambooMintKey",
    "f#": "F-Sharp"
  }
}
```

### Chi tiết các trường dữ liệu:

- `version` (int): Phiên bản cấu trúc schema (mặc định: `1`).
- `inputMethod` (string): Kiểu gõ hợp lệ gồm `"Telex"`, `"Vni"`, `"SimpleTelex"`.
- `charset` (string): Bảng mã đầu ra gồm `"Unicode"`, `"CompoundUnicode"`, `"Tcvn3"`.
- `toggleHotkey` (string): Phím tắt toggle nhanh gồm `"CtrlShift"`, `"AltZ"`, `"None"`.
- `spellCheck` (bool): Bật/tắt kiểm tra từ điển âm tiết tiếng Việt hợp lệ.
- `autoRestoreIfInvalid` (bool): Tự động trả về ký tự thô nếu từ gõ không tuân theo quy tắc tiếng Việt.
- `useModernOrthography` (bool): Đặt dấu theo kiểu mới (`òa, úy` thay vì `oà, uý`).
- `macroEnabled` (bool): Kích hoạt bảng gõ tắt.
- `macros` (object map): Cặp khóa-giá trị `từ_viết_tắt: nội_dung_thay_thế`.

## 3. Cài đặt Module Cấu hình trong F# Core (`EngineConfig.fs`)

Tạo file mới tại `src/BambooMintKey.Core/EngineConfig.fs`.

F#

```c#
namespace BambooMintKey.Core

open System

[<RequireQualifiedAccess>]
type InputMethod =
    | Telex
    | Vni
    | SimpleTelex

[<RequireQualifiedAccess>]
type Charset =
    | Unicode
    | CompoundUnicode
    | Tcvn3

[<RequireQualifiedAccess>]
type ToggleHotkey =
    | CtrlShift
    | AltZ
    | None

type EngineConfig = {
    Version: int
    InputMethod: InputMethod
    Charset: Charset
    ToggleHotkey: ToggleHotkey
    SpellCheck: bool
    AutoRestoreIfInvalid: bool
    UseModernOrthography: bool
    MacroEnabled: bool
    Macros: Map<string, string>
}

module Configuration =

    let defaultConfig = {
        Version = 1
        InputMethod = InputMethod.Telex
        Charset = Charset.Unicode
        ToggleHotkey = ToggleHotkey.CtrlShift
        SpellCheck = true
        AutoRestoreIfInvalid = true
        UseModernOrthography = true
        MacroEnabled = false
        Macros = Map.empty
    }

    let parseInputMethod = function
        | "Vni" | "VNI" -> InputMethod.Vni
        | "SimpleTelex" -> InputMethod.SimpleTelex
        | _ -> InputMethod.Telex

    let parseCharset = function
        | "CompoundUnicode" -> Charset.CompoundUnicode
        | "Tcvn3" | "TCVN3" -> Charset.Tcvn3
        | _ -> Charset.Unicode

    let parseHotkey = function
        | "AltZ" -> ToggleHotkey.AltZ
        | "None" -> ToggleHotkey.None
        | _ -> ToggleHotkey.CtrlShift

    // Parser JSON tối giản, lightweight, không phân bổ GC nặng
    // Đọc các giá trị dạng phẳng và danh sách macro cơ bản
    let fromJson (jsonString: string) : EngineConfig =
        try
            if String.IsNullOrWhiteSpace(jsonString) then defaultConfig
            else
                let getVal (key: string) =
                    let pattern = $"\"{key}\""
                    let idx = jsonString.IndexOf(pattern)
                    if idx >= 0 then
                        let colonIdx = jsonString.IndexOf(':', idx + pattern.Length)
                        if colonIdx >= 0 then
                            let startVal = colonIdx + 1
                            let mutable endVal = jsonString.IndexOfAny([| ','; '}'; '\r'; '\n' |], startVal)
                            if endVal < 0 then endVal <- jsonString.Length
                            jsonString.Substring(startVal, endVal - startVal).Trim().Trim('"', ' ')
                        else ""
                    else ""

                let getBool (key: string) (defaultVal: bool) =
                    match getVal key with
                    | "true" -> true
                    | "false" -> false
                    | _ -> defaultVal

                let inputMethod = getVal "inputMethod" |> parseInputMethod
                let charset = getVal "charset" |> parseCharset
                let hotkey = getVal "toggleHotkey" |> parseHotkey
                let spell = getBool "spellCheck" defaultConfig.SpellCheck
                let autoRestore = getBool "autoRestoreIfInvalid" defaultConfig.AutoRestoreIfInvalid
                let modern = getBool "useModernOrthography" defaultConfig.UseModernOrthography
                let macroOn = getBool "macroEnabled" defaultConfig.MacroEnabled

                // Trích xuất cụm macros object {...}
                let mutable macroMap = Map.empty
                let macroIdx = jsonString.IndexOf("\"macros\"")
                if macroIdx >= 0 then
                    let openBrace = jsonString.IndexOf('{', macroIdx)
                    let closeBrace = if openBrace >= 0 then jsonString.IndexOf('}', openBrace) else -1
                    if openBrace >= 0 && closeBrace > openBrace then
                        let macroContent = jsonString.Substring(openBrace + 1, closeBrace - openBrace - 1)
                        let pairs = macroContent.Split([| ','; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                        for p in pairs do
                            let parts = p.Split(':')
                            if parts.Length = 2 then
                                let k = parts.[0].Trim().Trim('"', ' ')
                                let v = parts.[1].Trim().Trim('"', ' ')
                                if k.Length > 0 && v.Length > 0 then
                                    macroMap <- Map.add k v macroMap

                {
                    Version = 1
                    InputMethod = inputMethod
                    Charset = charset
                    ToggleHotkey = hotkey
                    SpellCheck = spell
                    AutoRestoreIfInvalid = autoRestore
                    UseModernOrthography = modern
                    MacroEnabled = macroOn
                    Macros = macroMap
                }
        with
        | _ -> defaultConfig

    let toJson (config: EngineConfig) : string =
        let inputMethodStr = match config.InputMethod with InputMethod.Telex -> "Telex" | InputMethod.Vni -> "Vni" | InputMethod.SimpleTelex -> "SimpleTelex"
        let charsetStr = match config.Charset with Charset.Unicode -> "Unicode" | Charset.CompoundUnicode -> "CompoundUnicode" | Charset.Tcvn3 -> "Tcvn3"
        let hotkeyStr = match config.ToggleHotkey with ToggleHotkey.CtrlShift -> "CtrlShift" | ToggleHotkey.AltZ -> "AltZ" | ToggleHotkey.None -> "None"
        
        let macrosEntries = 
            config.Macros 
            |> Map.toList 
            |> List.map (fun (k, v) -> $"    \"{k}\": \"{v}\"")
            |> String.concat ",\n"

        let sb = System.Text.StringBuilder()
        sb.AppendLine("{") |> ignore
        sb.AppendLine($"  \"version\": {config.Version},") |> ignore
        sb.AppendLine($"  \"inputMethod\": \"{inputMethodStr}\",") |> ignore
        sb.AppendLine($"  \"charset\": \"{charsetStr}\",") |> ignore
        sb.AppendLine($"  \"toggleHotkey\": \"{hotkeyStr}\",") |> ignore
        sb.AppendLine($"  \"spellCheck\": {config.SpellCheck.ToString().ToLower()},") |> ignore
        sb.AppendLine($"  \"autoRestoreIfInvalid\": {config.AutoRestoreIfInvalid.ToString().ToLower()},") |> ignore
        sb.AppendLine($"  \"useModernOrthography\": {config.UseModernOrthography.ToString().ToLower()},") |> ignore
        sb.AppendLine($"  \"macroEnabled\": {config.MacroEnabled.ToString().ToLower()},") |> ignore
        sb.AppendLine("  \"macros\": {") |> ignore
        if not (String.IsNullOrWhiteSpace(macrosEntries)) then
            sb.AppendLine(macrosEntries) |> ignore
        sb.AppendLine("  }") |> ignore
        sb.Append("}") |> ignore
        sb.ToString()
```

## 4. Cơ chế Giám sát Tệp & Đồng bộ Nóng (`ConfigWatcher.cs`)

Tạo file tại `src/BambooMintKey.NativeBridge/Common/ConfigWatcher.cs`.

Sử dụng `FileSystemWatcher` kèm cơ chế Debounce (tránh việc chương trình soạn thảo hoặc GUI khóa file khi đang ghi dở):

C#

```c#
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BambooMintKey.Core;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.NativeBridge.Common
{
    public static class ConfigManager
    {
        private static FileSystemWatcher? _watcher;
        private static readonly string ConfigDir;
        private static readonly string ConfigPath;
        private static DateTime _lastReadTime = DateTime.MinValue;
        private static readonly object SyncLock = new();

        static ConfigManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            ConfigDir = Path.Combine(appData, "BambooMintKey");
            ConfigPath = Path.Combine(ConfigDir, "config.json");
        }

        public static void Initialize()
        {
            EnsureConfigFileExists();
            ReloadConfiguration();
            SetupWatcher();
        }

        private static void EnsureConfigFileExists()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                if (!File.Exists(ConfigPath))
                {
                    string defaultJson = Configuration.toJson(Configuration.defaultConfig);
                    File.WriteAllText(ConfigPath, defaultJson);
                }
            }
            catch
            {
                // Fallback im lặng nếu thư mục bị khóa quyền
            }
        }

        private static void SetupWatcher()
        {
            try
            {
                _watcher = new FileSystemWatcher(ConfigDir, "config.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnConfigFileChanged;
                _watcher.Created += OnConfigFileChanged;
            }
            catch
            {
                // Xử lý an toàn nếu môi trường không cho phép tạo FileSystemWatcher
            }
        }

        private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (SyncLock)
            {
                // Debounce 100ms tránh event bắn đúp khi file stream vừa mở vừa đóng
                if ((DateTime.UtcNow - _lastReadTime).TotalMilliseconds < 100) return;
                _lastReadTime = DateTime.UtcNow;
            }

            // Chờ một khoảng nhỏ để ứng dụng ghi file giải phóng Handle lock
            Task.Delay(50).ContinueWith(_ => ReloadConfiguration());
        }

        public static void ReloadConfiguration()
        {
            lock (SyncLock)
            {
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        // Mở file với FileShare.ReadWrite để không xung đột với process đang lưu
                        using var fs = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(fs);
                        string json = reader.ReadToEnd();

                        var config = Configuration.fromJson(json);

                        // Đồng bộ sang Bridge State Manager
                        ApplyConfigToEngine(config);
                    }
                }
                catch
                {
                    // Giữ nguyên cấu hình hiện tại nếu quá trình đọc tệp bị gián đoạn
                }
            }
        }

        private static void ApplyConfigToEngine(EngineConfig config)
        {
            // 1. Áp dụng kiểu gõ
            uint methodCode = config.InputMethod switch
            {
                InputMethod.Vni => 1,
                InputMethod.SimpleTelex => 2,
                _ => 0 // Telex
            };
            BridgeStateManager.SetInputMethod(methodCode);

            // 2. Áp dụng bảng mã
            uint charsetCode = config.Charset switch
            {
                Charset.CompoundUnicode => 1,
                Charset.Tcvn3 => 2,
                _ => 0 // Unicode
            };
            BridgeStateManager.SetCharset(charsetCode);

            // 3. Cập nhật phím tắt toggle
            BridgeStateManager.ToggleHotkey = config.ToggleHotkey;

            // 4. Bắn cập nhật cho Language Bar cập nhật tooltip và menu checkmarks
            LangBarItemButton.NotifyStateChanged();
        }
    }
}
```

## 5. Tích hợp Khởi tạo và Dọn dẹp

Trong hàm `DllMain` hoặc `ActivateEx` của Text Service:

C#

```c#
// Khi Text Service được kích hoạt (ActivateEx)
ConfigManager.Initialize();
```

Khi người dùng chuyển đổi thiết lập từ Context Menu chuột phải (từ Bước 3):

- Cập nhật trạng thái bộ nhớ.
- Gọi `Configuration.toJson` và ghi đè lại vào file `config.json` để đồng bộ ngược ra ổ đĩa.

## 6. Quy trình Kiểm thử & Validation

1. **Kiểm tra Tạo File Tự Động:**
   - Xóa thư mục `%AppData%\BambooMintKey` nếu có sẵn.
   - Chạy `scripts/enable-tip.ps1` và bật bộ gõ.
   - Kiểm tra xem tệp `%AppData%\BambooMintKey\config.json` có tự động được sinh ra với nội dung chuẩn không.
2. **Kiểm tra Hot-Reload Tức thì:**
   - Mở Notepad gõ thử `as` $\rightarrow$ ra `á` (Telex).
   - Mở file `config.json`, sửa trường `"inputMethod": "Vni"`, bấm Save trong trình soạn thảo.
   - Quay lại Notepad gõ `a1` $\rightarrow$ ra `á` ngay lập tức mà không cần chuyển layout hay khởi động lại Windows.
3. **Kiểm tra Chống Crash (Fault Tolerance):**
   - Xóa sạch nội dung file `config.json` (thành file rỗng) hoặc điền JSON sai cú pháp (`{ "inputMethod": `).
   - Gõ thử bàn phím: Hệ thống vẫn hoạt động bình thường ở chế độ mặc định an toàn (`Telex` / `Unicode`), không phát sinh lỗi ngoại lệ unhandled.