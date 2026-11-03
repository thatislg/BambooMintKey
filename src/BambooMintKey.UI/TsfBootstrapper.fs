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

    /// Tìm kiếm file BambooMintKey.dll nguồn từ gói MSIX hoặc thư mục ứng dụng
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

    /// Kiểm tra xem BambooMintKey.dll đã được đăng ký chuẩn trong Registry chưa
    let isAlreadyRegistered (sourceDllPath: string) : bool =
        try
            let targetDll = getTargetDllPath()
            if not (File.Exists(targetDll)) then false
            else
                let srcFi = FileInfo(sourceDllPath)
                let tgtFi = FileInfo(targetDll)
                // Nếu kích thước file khác nhau -> phiên bản đã được cập nhật, cần đăng ký lại
                if srcFi.Length <> tgtFi.Length then false
                else
                    // 1. Kiểm tra COM InprocServer32
                    let inprocKeyPath = sprintf @"CLSID\%s\InprocServer32" TextServiceClsid
                    use clsidKey = Registry.ClassesRoot.OpenSubKey(inprocKeyPath, false)
                    if clsidKey = null then false
                    else
                        let registeredPath = clsidKey.GetValue(null) :?> string
                        if String.IsNullOrWhiteSpace(registeredPath) ||
                           not (registeredPath.Equals(targetDll, StringComparison.OrdinalIgnoreCase)) then false
                        else
                            // 2. Kiểm tra TSF Language Profile cho tiếng Việt (0x042A)
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

    /// Thực hiện chu trình kiểm tra và tự động triển khai TSF DLL
    let ensureRegistered () : bool =
        match findSourceDll () with
        | None ->
            Debug.WriteLine("TsfBootstrapper: source BambooMintKey.dll not found in package.")
            false
        | Some sourceDll ->
            if isAlreadyRegistered sourceDll then
                Debug.WriteLine("TsfBootstrapper: BambooMintKey.dll is already properly registered in TSF.")
                true
            else
                try
                    let targetDir = getTargetDirectory()
                    let targetDll = getTargetDllPath()

                    if not (Directory.Exists(targetDir)) then
                        Directory.CreateDirectory(targetDir) |> ignore

                    grantDirectoryPermissions targetDir

                    // Sao chép file DLL ra C:\ProgramData\BambooMintKey\
                    try
                        File.Copy(sourceDll, targetDll, true)
                    with ex ->
                        Debug.WriteLine(sprintf "TsfBootstrapper File.Copy warning: %s" ex.Message)

                    // Thực thi regsvr32
                    let ok = registerWithRegsvr32 targetDll
                    if ok then
                        notifyCtfmon()
                    ok
                with ex ->
                    Debug.WriteLine(sprintf "TsfBootstrapper exception: %s" ex.Message)
                    false
