// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#nowarn "9"
#nowarn "3261"
namespace BambooMintKey.UI

open System
open System.Runtime.InteropServices
open System.Threading
open FSharp.NativeInterop

/// <summary>
/// Khôi phục đúng bộ gõ BambooMintKey sau khi người dùng mở khóa màn hình Windows (Lock/Unlock).
/// Giải quyết triệt để hiện tượng Windows LogonUI chuyển về en-US trên gói MSIX mà không
/// can thiệp khi người dùng chủ động chọn English hoặc Japanese trước khi khóa máy.
/// Thiết kế theo 013_02_Symbol_Vertical_Popup_and_SessionRestore_Design.md (Phần B).
/// </summary>
module SessionRestoreManager =

    [<DllImport("wtsapi32.dll", SetLastError = true)>]
    extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint32 dwFlags)

    [<DllImport("wtsapi32.dll", SetLastError = true)>]
    extern bool WTSUnRegisterSessionNotification(IntPtr hWnd)

    [<DllImport("ole32.dll")>]
    extern int CoInitializeEx(IntPtr pvReserved, uint32 dwCoInit)

    [<DllImport("ole32.dll")>]
    extern void CoUninitialize()

    [<DllImport("ole32.dll")>]
    extern int CoCreateInstance(Guid& rclsid, IntPtr pUnkOuter, uint32 dwClsContext, Guid& riid, IntPtr& ppv)

    let WM_WTSSESSION_CHANGE = 0x02B1u
    let WTS_SESSION_LOCK = 0x7u
    let WTS_SESSION_UNLOCK = 0x8u
    let NOTIFY_FOR_THIS_SESSION = 0u

    let private COINIT_APARTMENTTHREADED = 0x2u
    let private CLSCTX_INPROC_SERVER = 0x1u

    let private TF_PROFILETYPE_INPUTPROCESSOR = 1u
    let private LANGID_VIETNAMESE = 0x042Aus
    let private TF_IPPMF_FORSESSION = 0x20000000u
    let private TF_IPPMF_ENABLEPROFILE = 0x00000001u

    let private ClsidTfInputProcessorProfiles = Guid("E5895008-0C62-46A4-BC5B-244950D5ECB2")
    let private IidITfInputProcessorProfileMgr = Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")
    let private TextServiceClsid = Guid("B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1")
    let private ProfileGuid = Guid("C2F31A8E-92D0-4F81-9C3E-A52889211D44")

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type private ReleaseDelegate = delegate of IntPtr -> uint32

    [<UnmanagedFunctionPointer(CallingConvention.StdCall)>]
    type private ActivateProfileDelegate =
        delegate of IntPtr * uint32 * uint16 * byref<Guid> * byref<Guid> * IntPtr * uint32 -> int

    /// Kích hoạt lại profile TSF BambooMintKey cho phiên người dùng hiện tại qua ITfInputProcessorProfileMgr::ActivateProfile.
    let activateBambooMintKeyProfile () : bool =
        let hrInit = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED)
        let shouldUninit = (hrInit = 0 || hrInit = 1)
        try
            let mutable clsidMgr = ClsidTfInputProcessorProfiles
            let mutable iidMgr = IidITfInputProcessorProfileMgr
            let mutable pMgr = IntPtr.Zero
            let hrCreate = CoCreateInstance(&clsidMgr, IntPtr.Zero, CLSCTX_INPROC_SERVER, &iidMgr, &pMgr)
            if hrCreate = 0 && pMgr <> IntPtr.Zero then
                try
                    let vtablePtr : IntPtr = NativePtr.read (NativePtr.ofNativeInt<IntPtr> pMgr)
                    // Slot 3 trong ITfInputProcessorProfileMgr là ActivateProfile
                    let activateFnPtr : IntPtr = NativePtr.read (NativePtr.ofNativeInt<IntPtr> (vtablePtr + nativeint (3 * sizeof<IntPtr>)))
                    let activateFn = Marshal.GetDelegateForFunctionPointer<ActivateProfileDelegate>(activateFnPtr)
                    let mutable tipClsid = TextServiceClsid
                    let mutable profileGuid = ProfileGuid
                    let hrAct =
                        activateFn.Invoke(
                            pMgr,
                            TF_PROFILETYPE_INPUTPROCESSOR,
                            LANGID_VIETNAMESE,
                            &tipClsid,
                            &profileGuid,
                            IntPtr.Zero,
                            TF_IPPMF_FORSESSION ||| TF_IPPMF_ENABLEPROFILE)
                    hrAct = 0
                finally
                    let vtablePtr : IntPtr = NativePtr.read (NativePtr.ofNativeInt<IntPtr> pMgr)
                    let releaseFnPtr : IntPtr = NativePtr.read (NativePtr.ofNativeInt<IntPtr> (vtablePtr + nativeint (2 * sizeof<IntPtr>)))
                    let releaseFn = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releaseFnPtr)
                    releaseFn.Invoke(pMgr) |> ignore
            else
                false
        with _ -> false
        |> fun ok ->
            if shouldUninit then CoUninitialize()
            ok

    /// Chạy khôi phục bất đồng bộ trên STA thread riêng sau khi mở khóa màn hình,
    /// chờ Windows hoàn tất chuyển từ Secure Desktop (Winlogon) về Default Desktop.
    let scheduleRestoreAfterUnlock (isCurrentlyBambooActive: unit -> bool) =
        let t = Thread(fun () ->
            try
                Thread.Sleep(450)
                if not (isCurrentlyBambooActive()) then
                    activateBambooMintKeyProfile() |> ignore
                Thread.Sleep(800)
                if not (isCurrentlyBambooActive()) then
                    activateBambooMintKeyProfile() |> ignore
            with _ -> ()
        )
        t.IsBackground <- true
        t.Name <- "BambooMintKey_SessionRestore"
        t.Start()
