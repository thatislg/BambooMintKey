using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.Interop;

// =========================================================================
// VTable định nghĩa cho ITfInputProcessorProfiles
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfInputProcessorProfilesVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfInputProcessorProfiles
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> Register;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> Unregister;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, char*, int, char*, int, uint, int> AddLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int> RemoveLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> EnableLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int*, int> IsEnabledLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> EnableLanguageProfileByDefault;
}

// =========================================================================
// VTable định nghĩa cho ITfCategoryMgr
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCategoryMgrVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfCategoryMgr
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, Guid*, int> RegisterCategory;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, Guid*, int> UnregisterCategory;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> EnumCategoriesInItem;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> EnumItemsInCategory;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, IntPtr*, int> FindClosestCategory;
    public delegate* unmanaged[Stdcall]<IntPtr, char*, uint*, int> RegisterGUIDDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> UnregisterGUIDDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, char**, int> GetGUIDDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint*, int> RegisterGUID;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, int> GetGUID;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int> RegisterGUIDDWORD;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint*, int> GetGUIDDWORD;
}

// =========================================================================
// TsfRegistration - Đăng ký / gỡ đăng ký TSF Profile & Categories
// =========================================================================

/// <summary>
/// Đăng ký và gỡ đăng ký TSF Language Profile / Categories thông qua COM API.
/// Theo thiết kế 002_01_COM_Registration_and_Exports.md.
/// </summary>
public static unsafe class TsfRegistration
{
    private static readonly Guid ClsidTfInputProcessorProfiles = new("33C53824-660F-457B-8B3E-5F4A9D87AC47");
    private static readonly Guid IidITfInputProcessorProfiles = new("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA");

    private static readonly Guid ClsidTfCategoryMgr = new("A4B54FC0-ACAA-49FB-BB87-4EB0260080F6");
    private static readonly Guid IidITfCategoryMgr = new("C3ECEE2E-1C3D-4E3B-9A4D-0B86E03471AC");

    private const uint ClsCtxInprocServer = 0x1;

    private static void Log(string msg)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "BambooMintKey_Register.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] [TsfRegistration] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>
    /// Đăng ký Text Service và Language Profile tiếng Việt với TSF.
    /// </summary>
    public static int RegisterProfiles(string dllPath)
    {
        Log("RegisterProfiles started");

        int hr = NativeMethods.CoCreateInstance(
            ClsidTfInputProcessorProfiles,
            IntPtr.Zero,
            ClsCtxInprocServer,
            IidITfInputProcessorProfiles,
            out IntPtr pProfiles);

        Log($"CoCreateInstance(ITfInputProcessorProfiles) HR=0x{hr:X8}, pProfiles={(nint)pProfiles}");
        if (!HResult.Succeeded(hr) || pProfiles == IntPtr.Zero) return hr;

        var vtable = *(TfInputProcessorProfilesVTable**)pProfiles;
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            Guid profileGuid = Guids.ProfileGuid;

            hr = vtable->Register(pProfiles, &clsid);
            Log($"Register TIP HR=0x{hr:X8}");
            if (!HResult.Succeeded(hr)) return hr;

            fixed (char* pDesc = Constants.TextServiceName)
            fixed (char* pIconFile = dllPath)
            {
                hr = vtable->AddLanguageProfile(
                    pProfiles,
                    &clsid,
                    Constants.LangIdVietnamese,
                    &profileGuid,
                    pDesc,
                    Constants.TextServiceName.Length,
                    pIconFile,
                    dllPath.Length,
                    0);
                Log($"AddLanguageProfile HR=0x{hr:X8}");
                if (!HResult.Succeeded(hr)) return hr;
            }

            hr = vtable->EnableLanguageProfileByDefault(
                pProfiles, &clsid, Constants.LangIdVietnamese, &profileGuid, 1);
            Log($"EnableLanguageProfileByDefault HR=0x{hr:X8}");

            return hr;
        }
        finally
        {
            vtable->Release(pProfiles);
        }
    }

    /// <summary>
    /// Gỡ đăng ký Language Profile và Text Service khỏi TSF.
    /// </summary>
    public static int UnregisterProfiles()
    {
        int hr = NativeMethods.CoCreateInstance(
            ClsidTfInputProcessorProfiles,
            IntPtr.Zero,
            ClsCtxInprocServer,
            IidITfInputProcessorProfiles,
            out IntPtr pProfiles);

        if (!HResult.Succeeded(hr) || pProfiles == IntPtr.Zero) return hr;

        var vtable = *(TfInputProcessorProfilesVTable**)pProfiles;
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            Guid profileGuid = Guids.ProfileGuid;

            vtable->RemoveLanguageProfile(pProfiles, &clsid, Constants.LangIdVietnamese, &profileGuid);
            vtable->Unregister(pProfiles, &clsid);
            return HResult.Ok;
        }
        finally
        {
            vtable->Release(pProfiles);
        }
    }

    /// <summary>
    /// Đăng ký Categories với TSF Category Manager.
    /// </summary>
    public static int RegisterCategories()
    {
        Log("RegisterCategories started");

        int hr = NativeMethods.CoCreateInstance(
            ClsidTfCategoryMgr,
            IntPtr.Zero,
            ClsCtxInprocServer,
            IidITfCategoryMgr,
            out IntPtr pCatMgr);

        Log($"CoCreateInstance(ITfCategoryMgr) HR=0x{hr:X8}, pCatMgr={(nint)pCatMgr}");
        if (!HResult.Succeeded(hr) || pCatMgr == IntPtr.Zero) return hr;

        var vtable = *(TfCategoryMgrVTable**)pCatMgr;
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            Guid catTip = Guids.GuidTfCategoryTipKeyboard;
            Guid catDisplay = Guids.GuidTfCategoryDisplayAttributeProvider;

            hr = vtable->RegisterCategory(pCatMgr, &clsid, &catTip, &clsid);
            Log($"RegisterCategory(TIP_KEYBOARD) HR=0x{hr:X8}");
            if (!HResult.Succeeded(hr)) return hr;

            hr = vtable->RegisterCategory(pCatMgr, &clsid, &catDisplay, &clsid);
            Log($"RegisterCategory(DISPLAY_ATTRIBUTE) HR=0x{hr:X8}");

            return hr;
        }
        finally
        {
            vtable->Release(pCatMgr);
        }
    }

    /// <summary>
    /// Gỡ bỏ Categories khỏi TSF Category Manager.
    /// </summary>
    public static int UnregisterCategories()
    {
        int hr = NativeMethods.CoCreateInstance(
            ClsidTfCategoryMgr,
            IntPtr.Zero,
            ClsCtxInprocServer,
            IidITfCategoryMgr,
            out IntPtr pCatMgr);

        if (!HResult.Succeeded(hr) || pCatMgr == IntPtr.Zero) return hr;

        var vtable = *(TfCategoryMgrVTable**)pCatMgr;
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            Guid catTip = Guids.GuidTfCategoryTipKeyboard;
            Guid catDisplay = Guids.GuidTfCategoryDisplayAttributeProvider;

            vtable->UnregisterCategory(pCatMgr, &clsid, &catTip, &clsid);
            vtable->UnregisterCategory(pCatMgr, &clsid, &catDisplay, &clsid);
            return HResult.Ok;
        }
        finally
        {
            vtable->Release(pCatMgr);
        }
    }
}
