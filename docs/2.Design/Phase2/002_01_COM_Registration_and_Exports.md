Dưới đây là tài liệu thiết kế chi tiết đầu tiên theo đúng danh mục: **`002_01_COM_Registration_and_Exports.md`**.  

# Thiết Kế Chi Tiết: Đăng Ký COM, Xuất Hàm C-ABI & TSF Category Manager

**Mã tài liệu:** `002_01_COM_Registration_and_Exports`

  

**Giai đoạn:** Phase 2 - Tích hợp Hệ Điều Hành (Windows TSF & NativeAOT)  

**Thuộc module:** `BambooMintKey.NativeBridge`

  

**Trạng thái:** Sẵn sàng thực thi (Ready for Implementation)

## 1. Mục Tiêu Kỹ Thuật

- Khai báo các định danh GUID chuẩn cho Text Service TIP (Text Input Processor) và cấu hình Registry COM in-process server (`InprocServer32`).  
- Xuất 4 hàm chuẩn C-ABI của một COM DLL (`DllGetClassObject`, `DllCanUnloadNow`, `DllRegisterServer`, `DllUnregisterServer`) bằng cú pháp `[UnmanagedCallersOnly]` của .NET 10 NativeAOT.  
- Cài đặt `IClassFactory` không phụ thuộc vào Windows Runtime COM Interop tự động để tương thích hoàn toàn với chế độ biên dịch NativeAOT AOT-safe.  
- Đăng ký và gỡ đăng ký TIP thông qua Windows TSF `ITfInputProcessorProfiles` và `ITfCategoryMgr` với Language ID tiếng Việt (`0x042A`).  

## 2. Định Nghĩa GUID & Hằng Số Hệ Thống

Toàn bộ hệ thống TIP sử dụng chung tập GUID cố định được định nghĩa tập trung:

C#

```c#
namespace BambooMintKey.NativeBridge.Common;

// Ở 1 file riêng
public static class Guids
{
    // CLSID của Text Service chính (BambooMintKey TIP)
    public static readonly Guid TextServiceClsid = new("B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1");

    // Profile GUID phân biệt phiên bản kiểu gõ (Telex Profile)
    public static readonly Guid ProfileGuid = new("C2F31A8E-92D0-4F81-9C3E-A52889211D44");

    // TSF Category GUIDs (Chuẩn Windows TSF)
    public static readonly Guid GuidTfCategoryTipKeyboard = new("34745C63-B2F0-4784-8B67-5E12E8701A31");
    public static readonly Guid GuidTfCategoryDisplayAttributeProvider = new("35E7A704-438C-4235-96BC-4A6361C31595");

    // COM Standard Interface GUIDs
    public static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");
    public static readonly Guid IidIClassFactory = new("00000001-0000-0000-C000-000000000046");

    // TSF Interface GUIDs
    public static readonly Guid IidITfTextInputProcessorEx = new("AABEC164-429C-4234-A75D-4E90B01D77D1");
}

// Ở 1 file riêng
public static class Constants
{
    public const ushort LangIdVietnamese = 0x042A; // Vietnamese (Vietnam)
    public const string TextServiceName = "BambooMintKey Vietnamese Input";
    public const string TextServiceDescription = "BambooMintKey TSF Telex Engine";
    public const string ThreadingModel = "Apartment";
}
```

## 3. Kiến Trúc C-ABI Exports & IClassFactory

### 3.1. Quản lý Vòng Đời Server và Lock Count

Để tránh bị hệ điều hành unload DLL khi đối tượng đang được ứng dụng mục tiêu sử dụng, thư viện duy trì một biến đếm thread-safe:

C#

```c#
namespace BambooMintKey.NativeBridge.COM;

public static class ComServerState
{
    private static int _lockCount;
    private static int _objectCount;

    public static void Lock() => Interlocked.Increment(ref _lockCount);
    public static void Unlock() => Interlocked.Decrement(ref _lockCount);
    public static void ObjectCreated() => Interlocked.Increment(ref _objectCount);
    public static void ObjectDestroyed() => Interlocked.Decrement(ref _objectCount);

    public static bool CanUnload => Volatile.Read(ref _lockCount) == 0 && Volatile.Read(ref _objectCount) == 0;
}
```

### 3.2. Cài Đặt `ClassFactory` Thủ Công (NativeAOT VTable)

Do NativeAOT vô hiệu hóa hoàn toàn cơ chế tạo runtime COM wrapper (`ComImport` cổ điển có thể gây lỗi), `ClassFactory` được biểu diễn dưới dạng một con trỏ VTable thuần túy:

C#

```c#
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.COM;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct IClassFactoryVTable
{
    // IUnknown
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // IClassFactory
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int> CreateInstance;
    public delegate* unmanaged[Stdcall]<IntPtr, int, int> LockServer;
}

public unsafe class TextServiceClassFactory
{
    private static IClassFactoryVTable* _vTable;
    private static IntPtr _singletonInstance;

    public static IntPtr GetInstance()
    {
        if (_singletonInstance != IntPtr.Zero) return _singletonInstance;

        _vTable = (IClassFactoryVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(TextServiceClassFactory), sizeof(IClassFactoryVTable));

        _vTable->QueryInterface = &QueryInterface;
        _vTable->AddRef = &AddRef;
        _vTable->Release = &Release;
        _vTable->CreateInstance = &CreateInstance;
        _vTable->LockServer = &LockServer;

        var objMem = (IntPtr*)Marshal.AllocHGlobal(sizeof(IntPtr));
        *objMem = (IntPtr)_vTable;
        _singletonInstance = (IntPtr)objMem;
        return _singletonInstance;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HRESULT.E_POINTER;

        if (*riid == Guids.IidIUnknown || *riid == Guids.IidIClassFactory)
        {
            *ppvObject = thisPtr;
            AddRef(thisPtr);
            return HRESULT.S_OK;
        }

        *ppvObject = IntPtr.Zero;
        return HRESULT.E_NOINTERFACE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
    {
        ComServerState.Lock();
        return 2; // Singleton static reference
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
    {
        ComServerState.Unlock();
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CreateInstance(IntPtr thisPtr, IntPtr pUnkOuter, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HRESULT.E_POINTER;
        *ppvObject = IntPtr.Zero;

        if (pUnkOuter != IntPtr.Zero) return HRESULT.CLASS_E_NOAGGREGATION;

        // Khởi tạo đối tượng BambooMintKeyTextService chính
        var textServicePtr = BambooMintKeyTextService.CreateNativeInstance();
        var punk = (IntPtr*)textServicePtr;
        var vtable = *(IClassFactoryVTable**)*punk; // Bóc tách IUnknown vtable

        return ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vtable->QueryInterface)(textServicePtr, riid, ppvObject);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int LockServer(IntPtr thisPtr, int fLock)
    {
        if (fLock != 0) ComServerState.Lock();
        else ComServerState.Unlock();
        return HRESULT.S_OK;
    }
}
```

## 4. Các Hàm Xuất C-ABI (Dll Exports)

Tập trung tại file `src/BambooMintKey.NativeBridge/Exports.cs`:  

C#

```c#
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge;

public static unsafe class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllGetClassObject(Guid* rclsid, Guid* riid, IntPtr* ppv)
    {
        if (rclsid == null || riid == null || ppv == null) return HRESULT.E_POINTER;
        *ppv = IntPtr.Zero;

        if (*rclsid != Guids.TextServiceClsid)
        {
            return HRESULT.CLASS_E_CLASSNOTAVAILABLE;
        }

        var factory = TextServiceClassFactory.GetInstance();
        var punk = *(IClassFactoryVTable**)factory;
        return punk->QueryInterface(factory, riid, ppv);
    }

    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllCanUnloadNow()
    {
        return ComServerState.CanUnload ? HRESULT.S_OK : HRESULT.S_FALSE;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllRegisterServer", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllRegisterServer()
    {
        return ServerRegistrar.RegisterServer();
    }

    [UnmanagedCallersOnly(EntryPoint = "DllUnregisterServer", CallConvs = [typeof(CallConvStdcall)])]
    public static int DllUnregisterServer()
    {
        return ServerRegistrar.UnregisterServer();
    }
}
```

## 5. Quy Trình Đăng Ký TSF và Registry (`ServerRegistrar.cs`)

Khi thực thi `regsvr32 BambooMintKey.dll`, `DllRegisterServer` sẽ hoàn thiện đồng thời 2 bước:  

```c#
                     ┌───────────────────────────┐
                     │ DllRegisterServer Gọi Vào │
                     └─────────────┬─────────────┘
                                   │
            ┌──────────────────────┴──────────────────────┐
            ▼                                             ▼
  [1. Windows Registry]                         [2. TSF System COM API]
  - Ghi vào HKCR\CLSID\{GUID}                   - CoCreate ITfInputProcessorProfiles
  - InprocServer32 = <PathToDll>                - Register Profile (0x042A - VIE)
  - ThreadingModel = "Apartment"                - EnableLanguageProfileByDefault
                                                - CoCreate ITfCategoryMgr
                                                - RegisterCategory(GUID_TFCAT_TIP_KEYBOARD)
                                                - RegisterCategory(GUID_TFCAT_DISPLAYATTRIBUTEPROVIDER)
```

### Mã nguồn `ServerRegistrar.cs`:

C#

```c#
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.COM;

public static class ServerRegistrar
{
    public static int RegisterServer()
    {
        var modulePath = Process.GetCurrentProcess().MainModule?.FileName;
        // Lấy đúng đường dẫn thực của DLL hiện tại
        var dllPath = NativeMethods.GetCurrentDllPath();

        if (string.IsNullOrEmpty(dllPath)) return HResult.Fail;

        // 1. Ghi Registry InprocServer32
        var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
        using (var key = Registry.ClassesRoot.CreateSubKey(clsidKeyPath))
        {
            key.SetValue(null, Constants.TextServiceName);
            using var inproc = key.CreateSubKey("InprocServer32");
            inproc.SetValue(null, dllPath);
            inproc.SetValue("ThreadingModel", Constants.ThreadingModel);
        }

        // 2. Gọi TSF COM API để đăng ký Category & Profile
        var hr = TsfRegistration.RegisterProfiles(dllPath);
        if (hr != HResult.Ok) return hr;

        return TsfRegistration.RegisterCategories();
    }

    public static int UnregisterServer()
    {
        // 1. Hủy Categories và Profile trong TSF
        TsfRegistration.UnregisterCategories();
        TsfRegistration.UnregisterProfiles();

        // 2. Xóa Registry CLSID
        var clsidKeyPath = $@"CLSID\{{{Guids.TextServiceClsid}}}";
        Registry.ClassesRoot.DeleteSubKeyTree(clsidKeyPath, throwOnMissingSubKey: false);

        return HResult.Ok;
    }
}
```

### Mã nguồn `NativeMethods.cs`:

`NativeMethods` cung cấp các P/Invoke cơ bản để COM server tự nhận diện đường dẫn DLL đang thực thi (`GetCurrentDllPath`) và tạo các đối tượng TSF COM (`CoCreateInstance`).

```c#
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop;

public static class NativeMethods
{
    private const uint GetModuleHandleExFlagFromAddress = 0x00000004;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetModuleHandleExW(
        uint dwFlags,
        IntPtr lpModuleName,
        out IntPtr phModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(
        IntPtr hModule,
        [Out] char[] lpFilename,
        uint nSize);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    /// <summary>
    /// Lấy đường dẫn tuyệt đối của file DLL hiện tại đang thực thi trong bộ nhớ.
    /// </summary>
    public static string GetCurrentDllPath()
    {
        // Dùng một con trỏ hàm trong chính assembly này để tìm Module Handle của DLL
        var dummyDelegate = (Action)DummyMethod;
        IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(dummyDelegate);
        if (!GetModuleHandleExW(GetModuleHandleExFlagFromAddress, functionPtr, out IntPtr hModule) || hModule == IntPtr.Zero)
        {
            return string.Empty;
        }

        char[] buffer = new char[260]; // MAX_PATH
        uint length = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);

        // Xử lý nếu đường dẫn dài hơn MAX_PATH
        if (length >= buffer.Length)
        {
            buffer = new char[32768];
            length = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);
        }

        GC.KeepAlive(dummyDelegate);
        return length > 0 ? new string(buffer, 0, (int)length) : string.Empty;
    }

    private static void DummyMethod() { }
}
```

> **Lưu ý khi sửa warning:**
> - Giữ delegate `dummyDelegate` trong biến local và gọi `GC.KeepAlive(dummyDelegate)` để tránh cảnh báo *"Value assigned is not used in any execution path"* do delegate bị GC thu hồi sớm trước khi Win32 sử dụng.
> - Không dùng `GCHandle.Alloc` rồi chỉ `Free` mà không sử dụng giá trị, vì analyzer sẽ báo biến không được dùng.

### Mã nguồn `TsfRegistration.cs`:

`TsfRegistration` thực hiện các cuộc gọi COM thủ công tới `ITfInputProcessorProfiles` và `ITfCategoryMgr` thông qua VTable. Theo quy ước đặt tên .NET, các struct VTable **bỏ tiền tố `I`** để tránh cảnh báo *"Name does not match rule 'Types and namespaces'"*.

```c#
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.Interop;

// VTable định nghĩa cho ITfInputProcessorProfiles
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfInputProcessorProfilesVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> Register;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> Unregister;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, char*, int, char*, int, uint, int> AddLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int> RemoveLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> EnableLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int*, int> IsEnabledLanguageProfile;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, ushort, Guid*, int, int> EnableLanguageProfileByDefault;
}

// VTable định nghĩa cho ITfCategoryMgr
[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCategoryMgrVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

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

public static unsafe class TsfRegistration
{
    // CLSIDs chuẩn của Windows TSF COM Manager
    private static readonly Guid ClsidTfInputProcessorProfiles = new("33C53824-660F-457B-8B3E-5F4A9D87AC47");
    private static readonly Guid IidITfInputProcessorProfiles = new("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA");

    private static readonly Guid ClsidTfCategoryMgr = new("A4B54FC0-ACAA-49FB-BB87-4EB0260080F6");
    private static readonly Guid IidITfCategoryMgr = new("C3ECEE2E-1C3D-4E3B-9A4D-0B86E03471AC");

    private const uint ClsCtxInprocServer = 0x1;

    public static int RegisterProfiles(string dllPath)
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

            hr = vtable->Register(pProfiles, &clsid);
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
            }

            if (!HResult.Succeeded(hr)) return hr;

            vtable->EnableLanguageProfileByDefault(
                pProfiles, &clsid, Constants.LangIdVietnamese, &profileGuid, 1);

            return HResult.Ok;
        }
        finally
        {
            vtable->Release(pProfiles);
        }
    }

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

    public static int RegisterCategories()
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

            vtable->RegisterCategory(pCatMgr, &clsid, &catTip, &clsid);
            vtable->RegisterCategory(pCatMgr, &clsid, &catDisplay, &clsid);

            return HResult.Ok;
        }
        finally
        {
            vtable->Release(pCatMgr);
        }
    }

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
```

## 6. Sơ Đồ Cấu Trúc Mã Nguồn Giai Đoạn 2.1

```bash
src/BambooMintKey.NativeBridge/
├── BambooMintKey.NativeBridge.csproj
├── Exports.cs                     # 4 điểm nhập chuẩn C-ABI ([UnmanagedCallersOnly])
├── Common/
│   ├── Guids.cs                   # Định nghĩa CLSID, Profile GUID, Category GUID
│   ├── Constants.cs               # Ngôn ngữ 0x042A, ThreadingModel, Tên hiển thị
│   └── HResult.cs                 # Mã lỗi chuẩn HRESULT (S_OK, E_POINTER,...)
├── COM/
│   ├── ComServerState.cs          # Quản lý lock count / unload condition
│   ├── TextServiceClassFactory.cs # Cài đặt IClassFactory vtable thủ công
│   └── ServerRegistrar.cs         # Đăng ký Registry & TSF Category
└── Interop/
    ├── NativeMethods.cs           # P/Invoke Win32 GetModuleFileName
    └── TsfRegistration.cs         # ITfInputProcessorProfiles & ITfCategoryMgr COM call
```

