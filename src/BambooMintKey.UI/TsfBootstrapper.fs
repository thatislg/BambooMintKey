// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "3261"
#nowarn "3264"
namespace BambooMintKey.UI

open System
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.Security.AccessControl
open System.Security.Principal
open Microsoft.Win32

/// <summary>
/// Trình tự động triển khai và đăng ký Text Services Framework (TSF) COM DLL cho gói MSIX.
/// Giải quyết triệt để ranh giới bảo mật WindowsApps (ERROR_ACCESS_DENIED = 5).
/// Tự động sao chép BambooMintKey.dll sang C:\ProgramData\BambooMintKey và chạy regsvr32 nếu chưa đăng ký.
/// </summary>
module TsfBootstrapper =

    let private TextServiceClsid = "{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}"
    let private ProfileGuid = "{C2F31A8E-92D0-4F81-9C3E-A52889211D44}"
    let private LangIdHex = "0x0000042a"

    let getTargetDirectory () =
        let commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        Path.Combine(commonAppData, "BambooMintKey")

    let getTargetDllPath () =
        Path.Combine(getTargetDirectory(), "BambooMintKey.dll")

    let getTargetDll32Path () =
        Path.Combine(getTargetDirectory(), "BambooMintKey32.dll")

    /// Tìm kiếm file BambooMintKey.dll (64-bit) nguồn từ gói MSIX hoặc thư mục ứng dụng
    let findSourceDll () : string option =
        let baseDir = AppContext.BaseDirectory
        let candidates = [
            Path.Combine(baseDir, "BambooMintKey.dll")
            Path.Combine(baseDir, "..", "BambooMintKey.dll")
            Path.Combine(baseDir, @"..\publish\win-x64\BambooMintKey.dll")
            Path.Combine(baseDir, @"..\..\publish\win-x64\BambooMintKey.dll")
            Path.Combine(baseDir, @"..\..\..\publish\win-x64\BambooMintKey.dll")
        ]
        candidates |> List.tryFind File.Exists

    /// Tìm kiếm file BambooMintKey32.dll (32-bit WOW64 cho Zalo PC và các ứng dụng x86)
    let findSourceDll32 () : string option =
        let baseDir = AppContext.BaseDirectory
        let candidates = [
            Path.Combine(baseDir, "BambooMintKey32.dll")
            Path.Combine(baseDir, "..", "BambooMintKey32.dll")
            Path.Combine(baseDir, @"..\publish\win-x86\BambooMintKey32.dll")
            Path.Combine(baseDir, @"..\..\publish\win-x86\BambooMintKey32.dll")
            Path.Combine(baseDir, @"..\..\..\publish\win-x86\BambooMintKey32.dll")
        ]
        candidates |> List.tryFind File.Exists

    let private isFileUpToDate (sourcePath: string) (targetPath: string) : bool =
        if not (File.Exists(targetPath)) then false
        else
            let srcFi = FileInfo(sourcePath)
            let tgtFi = FileInfo(targetPath)
            srcFi.Length = tgtFi.Length && srcFi.LastWriteTimeUtc <= tgtFi.LastWriteTimeUtc.AddSeconds(2.0)

    let private isInprocRegistered (view: RegistryView) (expectedDllPath: string) : bool =
        use hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
        let inprocKeyPath = sprintf @"SOFTWARE\Classes\CLSID\%s\InprocServer32" TextServiceClsid
        use clsidKey = hklm.OpenSubKey(inprocKeyPath, false)
        if clsidKey = null then false
        else
            let registeredPath = clsidKey.GetValue(null) :?> string
            not (String.IsNullOrWhiteSpace(registeredPath)) &&
            registeredPath.Equals(expectedDllPath, StringComparison.OrdinalIgnoreCase)

    /// Kiểm tra xem BambooMintKey.dll (cả x64 và x86 nếu có) đã được đăng ký chuẩn trong Registry chưa
    let isAlreadyRegistered (sourceDllPath: string) (sourceDll32Opt: string option) : bool =
        try
            let targetDll = getTargetDllPath()
            if not (isFileUpToDate sourceDllPath targetDll) then false
            elif not (isInprocRegistered RegistryView.Registry64 targetDll) then false
            else
                // Kiểm tra bản 32-bit (WOW6432Node) nếu gói cài có kèm BambooMintKey32.dll
                let x86Ok =
                    match sourceDll32Opt with
                    | None -> true
                    | Some src32 ->
                        let targetDll32 = getTargetDll32Path()
                        isFileUpToDate src32 targetDll32 &&
                        isInprocRegistered RegistryView.Registry32 targetDll32

                if not x86Ok then false
                else
                    // Kiểm tra TSF Language Profile cho tiếng Việt (0x042A)
                    let tipKeyPath = sprintf @"SOFTWARE\Microsoft\CTF\TIP\%s\LanguageProfile\%s\%s" TextServiceClsid LangIdHex ProfileGuid
                    use tipKey = Registry.LocalMachine.OpenSubKey(tipKeyPath, false)
                    tipKey <> null
        with _ -> false

    /// Cấp quyền Read & Execute cho ALL APPLICATION PACKAGES và Users
    let private grantDirectoryPermissions (dirPath: string) =
        try
            // Sử dụng icacls với SID *S-1-15-2-1 (ALL APPLICATION PACKAGES) và *S-1-1-0 (Everyone)
            let psi = ProcessStartInfo("icacls.exe", sprintf "\"%s\" /grant \"*S-1-15-2-1:(OI)(CI)RX\" \"*S-1-1-0:(OI)(CI)RX\" /T /Q" dirPath)
            psi.CreateNoWindow <- true
            psi.UseShellExecute <- false
            use proc = Process.Start(psi)
            if proc <> null then
                proc.WaitForExit(4000) |> ignore
        with _ -> ()

    /// Khởi động lại hoặc thông báo cho ctfmon.exe nhận diện bộ gõ mới
    let private notifyCtfmon () =
        try
            let psi = ProcessStartInfo("ctfmon.exe")
            psi.UseShellExecute <- true
            Process.Start(psi) |> ignore
        with _ -> ()

    /// Đăng ký DLL với quyền Administrator thông qua regsvr32
    let private registerWithRegsvr32 (dllPath: string) : bool =
        try
            let psi = ProcessStartInfo()
            psi.FileName <- "regsvr32.exe"
            psi.Arguments <- sprintf "/s \"%s\"" dllPath
            psi.UseShellExecute <- true
            psi.Verb <- "runas"
            psi.WindowStyle <- ProcessWindowStyle.Hidden
            use proc = Process.Start(psi)
            if proc <> null then
                proc.WaitForExit(10000) |> ignore
                proc.ExitCode = 0
            else false
        with
        | :? Win32Exception as ex when ex.NativeErrorCode = 1223 ->
            // Người dùng từ chối hộp thoại UAC (ERROR_CANCELLED = 1223)
            Debug.WriteLine("TsfBootstrapper: User cancelled UAC prompt.")
            false
        | ex ->
            Debug.WriteLine(sprintf "TsfBootstrapper: regsvr32 failed: %s" ex.Message)
            false

    /// Sao chép DLL ra thư mục đích (hỗ trợ rename-and-replace nếu đang bị ứng dụng khác nạp giữ lock)
    let private copyDllSafe (sourcePath: string) (targetPath: string) =
        try
            File.Copy(sourcePath, targetPath, true)
        with _ ->
            try
                let targetDir = Path.GetDirectoryName(targetPath)
                let baseName = Path.GetFileName(targetPath)
                let oldDll = Path.Combine(targetDir, sprintf "%s.old.%s" baseName (Guid.NewGuid().ToString("N").Substring(0, 8)))
                if File.Exists(targetPath) then
                    File.Move(targetPath, oldDll)
                File.Copy(sourcePath, targetPath, true)
                try
                    Directory.GetFiles(targetDir, sprintf "%s.old.*" baseName)
                    |> Array.iter (fun f -> try File.Delete(f) with _ -> ())
                with _ -> ()
            with ex ->
                Debug.WriteLine(sprintf "TsfBootstrapper File.Copy warning (%s): %s" targetPath ex.Message)

    /// Thực hiện chu trình kiểm tra và tự động triển khai TSF DLL (x64 + x86 WOW64)
    let ensureRegistered () : bool =
        match findSourceDll () with
        | None ->
            Debug.WriteLine("TsfBootstrapper: source BambooMintKey.dll not found in package.")
            false
        | Some sourceDll ->
            let sourceDll32Opt = findSourceDll32 ()
            if isAlreadyRegistered sourceDll sourceDll32Opt then
                Debug.WriteLine("TsfBootstrapper: BambooMintKey (x64/x86) is already properly registered in TSF.")
                true
            else
                try
                    let targetDir = getTargetDirectory()
                    let targetDll = getTargetDllPath()

                    if not (Directory.Exists(targetDir)) then
                        Directory.CreateDirectory(targetDir) |> ignore

                    grantDirectoryPermissions targetDir

                    // 1. Sao chép bản 64-bit và 32-bit (nếu có) ra C:\ProgramData\BambooMintKey\
                    copyDllSafe sourceDll targetDll
                    match sourceDll32Opt with
                    | Some sourceDll32 ->
                        let targetDll32 = getTargetDll32Path()
                        copyDllSafe sourceDll32 targetDll32
                    | None -> ()

                    // 2. Thực thi regsvr32 (1 lần UAC duy nhất: DllRegisterServer x64 sẽ tự gọi SysWOW64\regsvr32 cho BambooMintKey32.dll)
                    let ok = registerWithRegsvr32 targetDll
                    if ok then
                        notifyCtfmon()
                    ok
                with ex ->
                    Debug.WriteLine(sprintf "TsfBootstrapper exception: %s" ex.Message)
                    false
