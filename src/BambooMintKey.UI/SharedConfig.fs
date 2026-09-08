#nowarn "9"
namespace BambooMintKey.UI

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open Microsoft.Win32
open FSharp.NativeInterop
open BambooMintKey.Core.Domain

type AppConfig = {
    mutable Version: int
    mutable IsVietnameseMode: bool
    mutable ToneStyle: byte            // 0 = Modern (òa, xòe), 1 = Traditional (oà, xoè)
    mutable AutoRestoreEnglishWords: bool
    mutable AllowRepeatKeyUndo: bool
    mutable AllowLeadingWAsU: bool
    mutable AllowFreeTonePlacement: bool
    mutable InputMethod: byte          // 0 = Telex, 1 = VNI, 2 = Simple Telex
    mutable Charset: byte              // 0 = Unicode dựng sẵn, 1 = Unicode tổ hợp, 2 = TCVN3
    mutable ToggleHotkey: byte         // 0 = Ctrl+Shift, 1 = Alt+Z, 2 = Ctrl+Space, 3 = None, 4 = Custom
    mutable HotkeyVKey: uint32         // Virtual Key Code (0x10 = Shift, 0x5A = Z, etc.)
    mutable HotkeyModifiers: uint32    // TSF Modifiers (0x0202 = Ctrl+OnKeyUp, 0x0001 = Alt, etc.)
    mutable HotkeyDisplay: string      // Chuỗi hiển thị ("Ctrl + Shift", "Alt + Z", ...)
    mutable StartWithWindows: bool
    mutable MacroEnabled: bool
    mutable Macros: Map<string, string>
} with
    static member Default = {
        Version = 2
        IsVietnameseMode = true
        ToneStyle = 0uy
        AutoRestoreEnglishWords = true
        AllowRepeatKeyUndo = true
        AllowLeadingWAsU = false
        AllowFreeTonePlacement = true
        InputMethod = 0uy
        Charset = 0uy
        ToggleHotkey = 0uy
        HotkeyVKey = 0x10u
        HotkeyModifiers = 0x0202u
        HotkeyDisplay = "Ctrl + Shift"
        StartWithWindows = false
        MacroEnabled = false
        Macros = Map.ofList [ ("vn", "Việt Nam"); ("bmk", "BambooMintKey"); ("f#", "F-Sharp") ]
    }

module ConfigStore =

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr OpenFileMappingW(uint32 dwDesiredAccess, bool bInheritHandle, string lpName)

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr CreateFileMappingW(IntPtr hFile, IntPtr lpFileMappingAttributes, uint32 flProtect, uint32 dwMaximumSizeHigh, uint32 dwMaximumSizeLow, string lpName)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint32 dwDesiredAccess, uint32 dwFileOffsetHigh, uint32 dwFileOffsetLow, nativeint dwNumberOfBytesToMap)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool UnmapViewOfFile(IntPtr lpBaseAddress)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool CloseHandle(IntPtr hObject)

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)>]
    extern IntPtr OpenEventW(uint32 dwDesiredAccess, bool bInheritHandle, string lpName)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool SetEvent(IntPtr hEvent)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool ResetEvent(IntPtr hEvent)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern void Sleep(uint32 dwMilliseconds)

    let private FILE_MAP_WRITE = 0x0002u
    let private FILE_MAP_READ = 0x0004u
    let private PAGE_READWRITE = 0x04u
    let private EVENT_MODIFY_STATE = 0x0002u
    let private MapName = @"Local\BambooMintKey_SharedConfig_v1"
    let private EventName = @"Local\BambooMintKey_StateChangedEvent_v1"
    let private RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run"
    let private AppName = "BambooMintKey"

    /// Đọc số đếm vòng lặp StateSequence (offset 8) từ Shared Memory để kiểm tra thay đổi từ Language Bar
    let getStateSequence () : uint32 =
        try
            let hMap = OpenFileMappingW(FILE_MAP_READ, false, MapName)
            if hMap <> IntPtr.Zero then
                let pView = MapViewOfFile(hMap, FILE_MAP_READ, 0u, 0u, 64n)
                if pView <> IntPtr.Zero then
                    let seqPtr : nativeptr<uint32> = NativePtr.ofNativeInt (pView + 8n)
                    let seq = NativePtr.read seqPtr
                    UnmapViewOfFile(pView) |> ignore
                    CloseHandle(hMap) |> ignore
                    seq
                else
                    CloseHandle(hMap) |> ignore
                    0u
            else 0u
        with _ -> 0u

    let getHotkeyDisplayString (vKey: uint32) (modifiers: uint32) =
        HotkeyFormatter.getHotkeyDisplayString vKey modifiers

    let private getConfigPath () =
        let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        let dir = Path.Combine(appData, "BambooMintKey")
        if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore
        Path.Combine(dir, "config.json")

    let checkStartWithWindows () =
        try
            use key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false)
            if key <> null then
                let value = key.GetValue(AppName)
                value <> null
            else false
        with _ -> false

    let setStartWithWindows (enable: bool) =
        try
            use key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true)
            if key <> null then
                if enable then
                    let exePath = Process.GetCurrentProcess().MainModule.FileName
                    key.SetValue(AppName, $"\"%s{exePath}\"")
                else
                    key.DeleteValue(AppName, false)
        with _ -> ()

    let loadConfig () : AppConfig =
        let cfg = AppConfig.Default
        cfg.StartWithWindows <- checkStartWithWindows ()

        // 1. Thử đọc trực tiếp từ Shared Memory
        let mutable loadedFromMemory = false
        try
            let hMap = OpenFileMappingW(FILE_MAP_READ, false, MapName)
            if hMap <> IntPtr.Zero then
                let pView = MapViewOfFile(hMap, FILE_MAP_READ, 0u, 0u, 64n)
                if pView <> IntPtr.Zero then
                    let span = Span<byte>(pView.ToPointer(), 64)
                    cfg.IsVietnameseMode <- span[0] <> 0uy
                    cfg.ToneStyle <- span[1]
                    cfg.AutoRestoreEnglishWords <- span[2] <> 0uy
                    cfg.AllowRepeatKeyUndo <- span[3] <> 0uy
                    cfg.AllowLeadingWAsU <- span[4] <> 0uy
                    cfg.AllowFreeTonePlacement <- if span.Length > 20 then span[20] <> 0uy else true
                    cfg.InputMethod <- span[5]
                    cfg.Charset <- span[6]
                    cfg.ToggleHotkey <- span[7]

                    let vKeyPtr : nativeptr<uint32> = NativePtr.ofNativeInt (pView + 12n)
                    let modPtr : nativeptr<uint32> = NativePtr.ofNativeInt (pView + 16n)
                    let vKey = NativePtr.read vKeyPtr
                    let mods = NativePtr.read modPtr
                    if vKey <> 0u || mods <> 0u then
                        cfg.HotkeyVKey <- vKey
                        cfg.HotkeyModifiers <- mods
                    elif cfg.ToggleHotkey = 1uy then
                        cfg.HotkeyVKey <- 0x5Au
                        cfg.HotkeyModifiers <- 0x0001u
                    elif cfg.ToggleHotkey = 2uy then
                        cfg.HotkeyVKey <- 0x20u
                        cfg.HotkeyModifiers <- 0x0002u

                    cfg.HotkeyDisplay <- getHotkeyDisplayString cfg.HotkeyVKey cfg.HotkeyModifiers

                    UnmapViewOfFile(pView) |> ignore
                    loadedFromMemory <- true
                CloseHandle(hMap) |> ignore
        with _ -> ()

        // 2. Đọc file JSON để nạp bền vững và lấy bảng gõ tắt (Macros)
        let path = getConfigPath ()
        if File.Exists(path) then
            try
                let json = File.ReadAllText(path)
                let has (key: string) (v: string) = json.Contains $"\"%s{key}\":%s{v}" || json.Contains $"\"%s{key}\": %s{v}"

                let parseUint (key: string) =
                    let prefix = $"\"%s{key}\":"
                    let idx = json.IndexOf(prefix)
                    if idx >= 0 then
                        let sub = json.Substring(idx + prefix.Length).Trim()
                        let endIdx = sub.IndexOfAny([|','; '\n'; '\r'; '}'|])
                        let token = if endIdx >= 0 then sub.Substring(0, endIdx).Trim() else sub
                        match UInt32.TryParse(token) with
                        | true, v -> Some v
                        | _ -> None
                    else None

                match parseUint "version" with
                | Some ver -> cfg.Version <- int ver
                | None -> ()

                if has "macroEnabled" "true" then cfg.MacroEnabled <- true
                elif has "macroEnabled" "false" then cfg.MacroEnabled <- false

                // Nếu chưa nạp từ Shared Memory thì nạp các thuộc tính chính từ JSON
                if not loadedFromMemory then
                    if has "toneStyle" "1" then cfg.ToneStyle <- 1uy
                    if has "autoRestoreEnglishWords" "false" then cfg.AutoRestoreEnglishWords <- false
                    if has "allowRepeatKeyUndo" "false" then cfg.AllowRepeatKeyUndo <- false
                    if has "allowLeadingWAsU" "true" then cfg.AllowLeadingWAsU <- true
                    if has "allowFreeTonePlacement" "false" then cfg.AllowFreeTonePlacement <- false
                    if has "inputMethod" "1" then cfg.InputMethod <- 1uy
                    elif has "inputMethod" "2" then cfg.InputMethod <- 2uy
                    if has "charset" "1" then cfg.Charset <- 1uy
                    elif has "charset" "2" then cfg.Charset <- 2uy

                    match parseUint "hotkeyVKey" with
                    | Some v -> cfg.HotkeyVKey <- v
                    | None -> ()

                    match parseUint "hotkeyModifiers" with
                    | Some m -> cfg.HotkeyModifiers <- m
                    | None -> ()

                    cfg.HotkeyDisplay <- getHotkeyDisplayString cfg.HotkeyVKey cfg.HotkeyModifiers

                // Nạp bảng macros
                let macroIdx = json.IndexOf("\"macros\"")
                if macroIdx >= 0 then
                    let openBrace = json.IndexOf('{', macroIdx)
                    let closeBrace = if openBrace >= 0 then json.IndexOf('}', openBrace) else -1
                    if openBrace >= 0 && closeBrace > openBrace then
                        let macroContent = json.Substring(openBrace + 1, closeBrace - openBrace - 1)
                        let pairs = macroContent.Split([| ','; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                        let mutable map = Map.empty
                        for p in pairs do
                            let parts = p.Split(':')
                            if parts.Length = 2 then
                                let k = parts[0].Trim().Trim('"', ' ')
                                let v = parts[1].Trim().Trim('"', ' ')
                                if k.Length > 0 && v.Length > 0 then
                                    map <- Map.add k v map
                        if not map.IsEmpty then cfg.Macros <- map
            with _ -> ()

        cfg

    let saveConfig (cfg: AppConfig) =
        setStartWithWindows cfg.StartWithWindows

        // 1. Ghi file JSON để lưu bền vững theo Schema Version 2
        try
            let path = getConfigPath ()
            let macroEntries =
                cfg.Macros
                |> Map.toList
                |> List.map (fun (k, v) -> sprintf "    \"%s\": \"%s\"" k v)
                |> String.concat ",\n"

            let macrosBlock =
                if String.IsNullOrWhiteSpace(macroEntries) then "  \"macros\": {}"
                else sprintf "  \"macros\": {\n%s\n  }" macroEntries

            let json = sprintf "{\n  \"version\": %d,\n  \"inputMethod\": %d,\n  \"charset\": %d,\n  \"toggleHotkey\": %d,\n  \"hotkeyVKey\": %u,\n  \"hotkeyModifiers\": %u,\n  \"toneStyle\": %d,\n  \"autoRestoreEnglishWords\": %b,\n  \"allowRepeatKeyUndo\": %b,\n  \"allowLeadingWAsU\": %b,\n  \"allowFreeTonePlacement\": %b,\n  \"startWithWindows\": %b,\n  \"macroEnabled\": %b,\n%s\n}"
                        cfg.Version
                        (int cfg.InputMethod)
                        (int cfg.Charset)
                        (int cfg.ToggleHotkey)
                        cfg.HotkeyVKey
                        cfg.HotkeyModifiers
                        (int cfg.ToneStyle)
                        cfg.AutoRestoreEnglishWords
                        cfg.AllowRepeatKeyUndo
                        cfg.AllowLeadingWAsU
                        cfg.AllowFreeTonePlacement
                        cfg.StartWithWindows
                        cfg.MacroEnabled
                        macrosBlock

            File.WriteAllText(path, json)
        with ex ->
            Debug.WriteLine($"BambooMintKey saveConfig error: {ex.Message}")

        // 2. Ghi trực tiếp vào Shared Memory và phát tín hiệu broadcast
        try
            let mutable hMap = OpenFileMappingW(FILE_MAP_READ ||| FILE_MAP_WRITE, false, MapName)
            if hMap = IntPtr.Zero then
                hMap <- CreateFileMappingW(IntPtr(-1), IntPtr.Zero, PAGE_READWRITE, 0u, 64u, MapName)

            if hMap <> IntPtr.Zero then
                let pView = MapViewOfFile(hMap, FILE_MAP_READ ||| FILE_MAP_WRITE, 0u, 0u, 64n)
                if pView <> IntPtr.Zero then
                    let span = Span<byte>(pView.ToPointer(), 64)
                    span[0] <- if cfg.IsVietnameseMode then 1uy else 0uy
                    span[1] <- cfg.ToneStyle
                    span[2] <- if cfg.AutoRestoreEnglishWords then 1uy else 0uy
                    span[3] <- if cfg.AllowRepeatKeyUndo then 1uy else 0uy
                    span[4] <- if cfg.AllowLeadingWAsU then 1uy else 0uy
                    if span.Length > 20 then span[20] <- if cfg.AllowFreeTonePlacement then 1uy else 0uy
                    span[5] <- cfg.InputMethod
                    span[6] <- cfg.Charset
                    span[7] <- cfg.ToggleHotkey

                    let vKeyPtr : nativeptr<uint32> = NativePtr.ofNativeInt (pView + 12n)
                    NativePtr.write vKeyPtr cfg.HotkeyVKey

                    let modPtr : nativeptr<uint32> = NativePtr.ofNativeInt (pView + 16n)
                    NativePtr.write modPtr cfg.HotkeyModifiers

                    // Tăng StateSequence tại offset 8
                    let seqPtr : nativeptr<uint32> = NativePtr.ofNativeInt (pView + 8n)
                    let currentSeq = NativePtr.read seqPtr
                    NativePtr.write seqPtr (currentSeq + 1u)

                    UnmapViewOfFile(pView) |> ignore

                    // Phát tín hiệu Event broadcast cho các tiến trình đang lắng nghe
                    let hEvent = OpenEventW(EVENT_MODIFY_STATE, false, EventName)
                    if hEvent <> IntPtr.Zero then
                        SetEvent(hEvent) |> ignore
                        Sleep(20u)
                        ResetEvent(hEvent) |> ignore
                        CloseHandle(hEvent) |> ignore

                CloseHandle(hMap) |> ignore
        with _ -> ()
