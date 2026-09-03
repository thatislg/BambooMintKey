#nowarn "9"
namespace BambooMintKey.UI

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open Microsoft.Win32
open FSharp.NativeInterop

type AppConfig = {
    mutable IsVietnameseMode: bool
    mutable ToneStyle: byte            // 0 = Modern (òa, xòe), 1 = Traditional (oà, xoè)
    mutable AutoRestoreEnglishWords: bool
    mutable AllowRepeatKeyUndo: bool
    mutable AllowLeadingWAsU: bool
    mutable InputMethod: byte          // 0 = Telex, 1 = VNI, 2 = Simple Telex
    mutable Charset: byte              // 0 = Unicode dựng sẵn, 1 = Unicode tổ hợp, 2 = TCVN3
    mutable ToggleHotkey: byte         // 0 = Ctrl+Shift, 1 = Alt+Z, 2 = Ctrl+Space, 3 = None/Custom
    mutable HotkeyVKey: uint32         // Virtual Key Code (0x10 = Shift, 0x5A = Z, etc.)
    mutable HotkeyModifiers: uint32    // TSF Modifiers (0x0202 = Ctrl+OnKeyUp, 0x0001 = Alt, etc.)
    mutable HotkeyDisplay: string      // Chuỗi hiển thị ("Ctrl + Shift", "Alt + Z", ...)
    mutable StartWithWindows: bool
} with
    static member Default = {
        IsVietnameseMode = true
        ToneStyle = 0uy
        AutoRestoreEnglishWords = true
        AllowRepeatKeyUndo = true
        AllowLeadingWAsU = false
        InputMethod = 0uy
        Charset = 0uy
        ToggleHotkey = 0uy
        HotkeyVKey = 0x10u
        HotkeyModifiers = 0x0202u
        HotkeyDisplay = "Ctrl + Shift"
        StartWithWindows = false
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

    let getHotkeyDisplayString (vKey: uint32) (modifiers: uint32) =
        if vKey = 0u && modifiers = 0u then "Không sử dụng phím tắt"
        elif vKey = 0x10u && (modifiers = 0x0202u || modifiers = 0x0002u) then "Ctrl + Shift"
        elif vKey = 0x10u && (modifiers = 0x0201u || modifiers = 0x0001u) then "Alt + Shift"
        elif vKey = 0x5Au && modifiers = 0x0001u then "Alt + Z"
        elif vKey = 0x20u && modifiers = 0x0002u then "Ctrl + Space"
        else
            let parts = System.Collections.Generic.List<string>()
            if modifiers &&& 0x0002u <> 0u then parts.Add("Ctrl")
            if modifiers &&& 0x0001u <> 0u then parts.Add("Alt")
            if modifiers &&& 0x0004u <> 0u then parts.Add("Shift")
            
            let keyName =
                match vKey with
                | 0x20u -> "Space"
                | 0x10u -> "Shift"
                | 0x11u -> "Ctrl"
                | 0x12u -> "Alt"
                | 0xC0u -> "~"
                | 0xDCu -> "\\"
                | 0xBFu -> "/"
                | 0xDBu -> "["
                | 0xDDu -> "]"
                | 0xBAu -> ";"
                | 0xDEu -> "'"
                | 0xBCu -> ","
                | 0xBEu -> "."
                | 0xBDu -> "-"
                | 0xBBu -> "="
                | 0x08u -> "Backspace"
                | 0x09u -> "Tab"
                | 0x0Du -> "Enter"
                | 0x14u -> "CapsLock"
                | 0x1Bu -> "Esc"
                | k when k >= 0x41u && k <= 0x5Au -> string (char (int k))
                | k when k >= 0x30u && k <= 0x39u -> string (char (int k))
                | k when k >= 0x60u && k <= 0x69u -> sprintf "Num%d" (int k - 0x60)
                | k when k >= 0x70u && k <= 0x7Bu -> sprintf "F%d" (int k - 0x70 + 1)
                | k -> sprintf "0x%X" k
            
            if not (parts.Contains(keyName)) then parts.Add(keyName)
            String.Join(" + ", parts)

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
                    key.SetValue(AppName, sprintf "\"%s\"" exePath)
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
                    cfg.IsVietnameseMode <- span.[0] <> 0uy
                    cfg.ToneStyle <- span.[1]
                    cfg.AutoRestoreEnglishWords <- span.[2] <> 0uy
                    cfg.AllowRepeatKeyUndo <- span.[3] <> 0uy
                    cfg.AllowLeadingWAsU <- span.[4] <> 0uy
                    cfg.InputMethod <- span.[5]
                    cfg.Charset <- span.[6]
                    cfg.ToggleHotkey <- span.[7]

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

        // 2. Nếu chưa có Shared Memory, nạp từ file JSON nếu có
        if not loadedFromMemory then
            let path = getConfigPath ()
            if File.Exists(path) then
                try
                    let json = File.ReadAllText(path)
                    let has (key: string) (v: string) = json.Contains(sprintf "\"%s\":%s" key v) || json.Contains(sprintf "\"%s\": %s" key v)
                    if has "toneStyle" "1" then cfg.ToneStyle <- 1uy
                    if has "autoRestoreEnglishWords" "false" then cfg.AutoRestoreEnglishWords <- false
                    if has "allowRepeatKeyUndo" "false" then cfg.AllowRepeatKeyUndo <- false
                    if has "allowLeadingWAsU" "true" then cfg.AllowLeadingWAsU <- true
                    if has "inputMethod" "1" then cfg.InputMethod <- 1uy
                    elif has "inputMethod" "2" then cfg.InputMethod <- 2uy
                    if has "charset" "1" then cfg.Charset <- 1uy
                    elif has "charset" "2" then cfg.Charset <- 2uy

                    let parseUint (key: string) =
                        let prefix = sprintf "\"%s\":" key
                        let idx = json.IndexOf(prefix)
                        if idx >= 0 then
                            let sub = json.Substring(idx + prefix.Length).Trim()
                            let endIdx = sub.IndexOfAny([|','; '\n'; '\r'; '}'|])
                            let token = if endIdx >= 0 then sub.Substring(0, endIdx).Trim() else sub
                            match UInt32.TryParse(token) with
                            | true, v -> Some v
                            | _ -> None
                        else None

                    match parseUint "hotkeyVKey" with
                    | Some v -> cfg.HotkeyVKey <- v
                    | None -> ()

                    match parseUint "hotkeyModifiers" with
                    | Some m -> cfg.HotkeyModifiers <- m
                    | None -> ()

                    cfg.HotkeyDisplay <- getHotkeyDisplayString cfg.HotkeyVKey cfg.HotkeyModifiers
                with _ -> ()

        cfg

    let saveConfig (cfg: AppConfig) =
        setStartWithWindows cfg.StartWithWindows

        // 1. Ghi file JSON để lưu bền vững trước tiên
        try
            let path = getConfigPath ()
            let json = sprintf "{\n  \"toneStyle\": %d,\n  \"autoRestoreEnglishWords\": %b,\n  \"allowRepeatKeyUndo\": %b,\n  \"allowLeadingWAsU\": %b,\n  \"inputMethod\": %d,\n  \"charset\": %d,\n  \"toggleHotkey\": %d,\n  \"hotkeyVKey\": %u,\n  \"hotkeyModifiers\": %u,\n  \"startWithWindows\": %b\n}"
                        cfg.ToneStyle
                        cfg.AutoRestoreEnglishWords
                        cfg.AllowRepeatKeyUndo
                        cfg.AllowLeadingWAsU
                        cfg.InputMethod
                        cfg.Charset
                        cfg.ToggleHotkey
                        cfg.HotkeyVKey
                        cfg.HotkeyModifiers
                        cfg.StartWithWindows
            File.WriteAllText(path, json)
        with _ -> ()

        // 2. Ghi trực tiếp vào Shared Memory và phát tín hiệu broadcast
        try
            let mutable hMap = OpenFileMappingW(FILE_MAP_READ ||| FILE_MAP_WRITE, false, MapName)
            if hMap = IntPtr.Zero then
                hMap <- CreateFileMappingW(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE, 0u, 64u, MapName)

            if hMap <> IntPtr.Zero then
                let pView = MapViewOfFile(hMap, FILE_MAP_READ ||| FILE_MAP_WRITE, 0u, 0u, 64n)
                if pView <> IntPtr.Zero then
                    let span = Span<byte>(pView.ToPointer(), 64)
                    span.[0] <- if cfg.IsVietnameseMode then 1uy else 0uy
                    span.[1] <- cfg.ToneStyle
                    span.[2] <- if cfg.AutoRestoreEnglishWords then 1uy else 0uy
                    span.[3] <- if cfg.AllowRepeatKeyUndo then 1uy else 0uy
                    span.[4] <- if cfg.AllowLeadingWAsU then 1uy else 0uy
                    span.[5] <- cfg.InputMethod
                    span.[6] <- cfg.Charset
                    span.[7] <- cfg.ToggleHotkey

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

        // 2. Ghi file JSON để lưu bền vững
        try
            let path = getConfigPath ()
            let json = sprintf "{\n  \"toneStyle\": %d,\n  \"autoRestoreEnglishWords\": %b,\n  \"allowRepeatKeyUndo\": %b,\n  \"allowLeadingWAsU\": %b,\n  \"inputMethod\": %d,\n  \"charset\": %d,\n  \"toggleHotkey\": %d,\n  \"hotkeyVKey\": %u,\n  \"hotkeyModifiers\": %u,\n  \"startWithWindows\": %b\n}"
                        cfg.ToneStyle
                        cfg.AutoRestoreEnglishWords
                        cfg.AllowRepeatKeyUndo
                        cfg.AllowLeadingWAsU
                        cfg.InputMethod
                        cfg.Charset
                        cfg.ToggleHotkey
                        cfg.HotkeyVKey
                        cfg.HotkeyModifiers
                        cfg.StartWithWindows
            File.WriteAllText(path, json)
        with _ -> ()
