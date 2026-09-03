// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using Microsoft.Win32;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.Interop;

// =========================================================================
// VTable định nghĩa cho ITfInputProcessorProfiles
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfInputProcessorProfilesVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfInputProcessorProfiles (3 - 20)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> Register;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> Unregister;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, char*, int, char*, int, uint, int> AddLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int> RemoveLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumInputProcessorInfo;
    public delegate* unmanaged[Stdcall]<IntPtr, ushort, Guid*, Guid*, Guid*, int> GetDefaultLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, ushort, Guid*, Guid*, Guid*, int> SetDefaultLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> ActivateLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort*, Guid*, int> GetActiveLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, char**, int> GetLanguageProfileDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, ushort*, int> GetCurrentLanguage;
    public delegate* unmanaged[Stdcall]<IntPtr, ushort, int> ChangeCurrentLanguage;
    public delegate* unmanaged[Stdcall]<IntPtr, ushort**, uint*, int> GetLanguageList;
    public delegate* unmanaged[Stdcall]<IntPtr, ushort, IntPtr*, int> EnumLanguageProfiles;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> EnableLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int*, int> IsEnabledLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> EnableLanguageProfileByDefault;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, IntPtr, int> SubstituteKeyboardLayout;
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
/// Đăng ký và gỡ đăng ký TSF Language Profile / Categories.
/// Thử dùng COM API (ITfInputProcessorProfiles / ITfCategoryMgr) trước;
/// nếu COM class chưa có sẵn, fallback sang ghi Registry trực tiếp.
/// </summary>
public static unsafe class TsfRegistration
{
    // CLSIDs chuẩn của Windows TSF COM Manager
    private static readonly Guid ClsidTfInputProcessorProfiles = new("E5895008-0C62-46A4-BC5B-244950D5ECB2");
    private static readonly Guid IidITfInputProcessorProfiles = new("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA");

    private static readonly Guid ClsidTfCategoryMgr = new("A4B54FC0-ACAA-49FB-BB87-4EB0260080F6");
    private static readonly Guid IidITfCategoryMgr = new("C3ECEE2E-1C3D-4E3B-9A4D-0B86E03471AC");

    private const uint ClsCtxAll = 0x17;

    private const string CtfTipRoot = @"SOFTWARE\Microsoft\CTF\TIP";

    private static readonly Guid[] SupportedCategories =
    [
        Guids.GuidTfCategoryTipKeyboard,
        Guids.GuidTfCategoryDisplayAttributeProvider,
        Guids.GuidTfCatTipCapImmersiveSupport,
        Guids.GuidTfCatTipCapSystraySupport,
        Guids.GuidTfCatTipCapInputModeCompartment,
        Guids.GuidTfCatTipCapUiElementEnabled
    ];

    private static void Log(string msg)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "BambooMintKey_Register.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] [TsfRegistration] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private static string TipKeyPath(Guid clsid) => $@"{CtfTipRoot}\{{{clsid}}}";
    private static string ProfileKeyPath(Guid clsid, ushort langId, Guid profileGuid) =>
        $@"{TipKeyPath(clsid)}\LanguageProfile\0x{langId:X8}\{{{profileGuid}}}";

    private static void WriteProfileKey(RegistryKey hive, string keyPath, string dllPath)
    {
        using var key = hive.CreateSubKey(keyPath);
        key.SetValue(null, Constants.TextServiceName);
        key.SetValue("Description", Constants.TextServiceName);
        key.SetValue("Display Description", Constants.TextServiceName);
        key.SetValue("Enable", 1, RegistryValueKind.DWord);
        key.SetValue("IconFile", dllPath);
        key.SetValue("IconIndex", 0, RegistryValueKind.DWord);
    }

    // =========================================================================
    // COM API Registration
    // =========================================================================

    /// <summary>
    /// Đăng ký Text Service và Language Profile tiếng Việt với TSF.
    /// </summary>
    public static int RegisterProfiles(string dllPath)
    {
        Log("RegisterProfiles started");

        int hr = TryRegisterProfilesCom(dllPath);
        if (HResult.Succeeded(hr))
        {
            Log("RegisterProfiles via COM succeeded");
            return hr;
        }

        Log($"RegisterProfiles via COM failed HR=0x{hr:X8}, falling back to Registry");
        return RegisterProfilesRegistry(dllPath);
    }

    private static int TryRegisterProfilesCom(string dllPath)
    {
        int hr = NativeMethods.TF_CreateInputProcessorProfiles(out IntPtr pProfiles);

        Log($"TF_CreateInputProcessorProfiles HR=0x{hr:X8}, pProfiles={(nint)pProfiles}");
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
        int hr = TryUnregisterProfilesCom();
        if (HResult.Succeeded(hr)) return hr;

        Log($"UnregisterProfiles via COM failed HR=0x{hr:X8}, using Registry cleanup");
        UnregisterProfilesRegistry();
        return HResult.Ok;
    }

    private static int TryUnregisterProfilesCom()
    {
        int hr = NativeMethods.TF_CreateInputProcessorProfiles(out IntPtr pProfiles);

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

        int hr = TryRegisterCategoriesCom();
        if (HResult.Succeeded(hr))
        {
            Log("RegisterCategories via COM succeeded");
            return hr;
        }

        Log($"RegisterCategories via COM failed HR=0x{hr:X8}, falling back to Registry");
        return RegisterCategoriesRegistry();
    }

    private static int TryRegisterCategoriesCom()
    {
        int hr = NativeMethods.TF_CreateCategoryMgr(out IntPtr pCatMgr);

        Log($"TF_CreateCategoryMgr HR=0x{hr:X8}, pCatMgr={(nint)pCatMgr}");
        if (!HResult.Succeeded(hr) || pCatMgr == IntPtr.Zero) return hr;

        var vtable = *(TfCategoryMgrVTable**)pCatMgr;
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            foreach (var cat in SupportedCategories)
            {
                var catCopy = cat;
                hr = vtable->RegisterCategory(pCatMgr, &clsid, &catCopy, &clsid);
                Log($"RegisterCategory({cat}) HR=0x{hr:X8}");
                if (!HResult.Succeeded(hr)) return hr;
            }
            return HResult.Ok;
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
        int hr = TryUnregisterCategoriesCom();
        if (HResult.Succeeded(hr)) return hr;

        Log($"UnregisterCategories via COM failed HR=0x{hr:X8}, using Registry cleanup");
        UnregisterCategoriesRegistry();
        return HResult.Ok;
    }

    private static int TryUnregisterCategoriesCom()
    {
        int hr = NativeMethods.TF_CreateCategoryMgr(out IntPtr pCatMgr);

        if (!HResult.Succeeded(hr) || pCatMgr == IntPtr.Zero) return hr;

        var vtable = *(TfCategoryMgrVTable**)pCatMgr;
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            foreach (var cat in SupportedCategories)
            {
                var catCopy = cat;
                vtable->UnregisterCategory(pCatMgr, &clsid, &catCopy, &clsid);
            }
            return HResult.Ok;
        }
        finally
        {
            vtable->Release(pCatMgr);
        }
    }

    // =========================================================================
    // Registry Fallback Registration
    // =========================================================================

    private static int RegisterProfilesRegistry(string dllPath)
    {
        try
        {
            Guid clsid = Guids.TextServiceClsid;
            Guid profileGuid = Guids.ProfileGuid;
            ushort langId = Constants.LangIdVietnamese;
            string tipKeyPath = TipKeyPath(clsid);
            string profileKeyPath = ProfileKeyPath(clsid, langId, profileGuid);

            // Machine-wide registration only; per-user enable must run in user's own context.
            using (var tipKey = Registry.LocalMachine.CreateSubKey(tipKeyPath))
            {
                tipKey.SetValue(null, Constants.TextServiceName);
            }
            WriteProfileKey(Registry.LocalMachine, profileKeyPath, dllPath);

            Log("RegisterProfiles via Registry succeeded");
            return HResult.Ok;
        }
        catch (Exception ex)
        {
            Log($"RegisterProfilesRegistry failed: {ex}");
            return HResult.Fail;
        }
    }

    private static void UnregisterProfilesRegistry()
    {
        try
        {
            string tipKeyPath = TipKeyPath(Guids.TextServiceClsid);
            Registry.LocalMachine.DeleteSubKeyTree(tipKeyPath, throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static int RegisterCategoriesRegistry()
    {
        try
        {
            Guid clsid = Guids.TextServiceClsid;

            foreach (var cat in SupportedCategories)
            {
                string catKeyPath = $@"{CtfTipRoot}\{{{clsid}}}\Category\Category\{{{cat}}}\{{{clsid}}}";
                using (var k = Registry.LocalMachine.CreateSubKey(catKeyPath))
                {
                    k.SetValue(null, "");
                }

                string itemKeyPath = $@"{CtfTipRoot}\{{{clsid}}}\Category\Item\{{{clsid}}}\{{{cat}}}";
                using (var k = Registry.LocalMachine.CreateSubKey(itemKeyPath))
                {
                    k.SetValue(null, "");
                }
            }

            Log("RegisterCategories via Registry succeeded");
            return HResult.Ok;
        }
        catch (Exception ex)
        {
            Log($"RegisterCategoriesRegistry failed: {ex}");
            return HResult.Fail;
        }
    }

    private static void UnregisterCategoriesRegistry()
    {
        try
        {
            string tipKeyPath = TipKeyPath(Guids.TextServiceClsid);
            Registry.LocalMachine.DeleteSubKeyTree(tipKeyPath, throwOnMissingSubKey: false);
        }
        catch { }
    }
}
