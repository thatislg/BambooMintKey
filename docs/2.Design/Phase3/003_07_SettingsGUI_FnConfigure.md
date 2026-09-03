# 003_07_SettingsGUI_FnConfigure.md

> Tài liệu kỹ thuật chi tiết về việc cài đặt COM Interface `ITfFunctionProvider` / `ITfFnConfigure`, móc nối nút "Options" trong Windows Settings, kiến trúc ứng dụng giao diện cấu hình độc lập (`BambooMintKey.Config`) và luồng đồng bộ dữ liệu hai chiều.

## 1. Cơ sở chuẩn hóa từ Windows SDK

Toàn bộ interface định nghĩa cấu hình hệ thống được trích xuất từ file gốc: `C:\Program Files (x86)\Windows Kits\10\Include\<version>\um\msctf.idl`.

### 1.1. Bảng tra cứu GUID chuẩn

| **Thành phần**            | **File SDK gốc** | **GUID chuẩn xác**                     |
| ------------------------- | ---------------- | -------------------------------------- |
| `IID_ITfFunctionProvider` | `msctf.idl`      | `101D9462-0E4E-41F1-B34B-E1EF37E02F0D` |
| `IID_ITfFunction`         | `msctf.idl`      | `DB593490-238F-11D8-9E28-0007E912B864` |
| `IID_ITfFnConfigure`      | `msctf.idl`      | `88F567C6-1757-49F8-A1B2-89234C1EEFF9` |

### 1.2. Vai trò của `ITfFnConfigure`

- Khi cài đặt một Text Input Processor (TIP), nếu không triển khai `ITfFnConfigure`, nút **Options** trong mục `Windows Settings -> Time & Language -> Language -> Preferred Languages -> [Ngôn ngữ] -> Options -> BambooMintKey` sẽ bị **xám mờ (disabled)**.
- Khi người dùng bấm nút Options (hoặc chọn "Cài đặt tùy chọn..." từ Context Menu chuột phải trên Taskbar), Windows sẽ truy vấn interface này và gọi hàm `Show()`.

## 2. Thiết kế VTable & Struct COM C# NativeAOT

Giao diện `ITfFnConfigure` kế thừa từ `ITfFunction` $\rightarrow$ `IUnknown`.

### 2.1. Khai báo Struct VTable (`Interop/TsfConfigureTypes.cs`)

C#

```
using System;
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ITfFunctionProviderVTable
    {
        // --- IUnknown ---
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // --- ITfFunctionProvider ---
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> GetType;
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetDescription;
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, IntPtr*, int> GetFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ITfFnConfigureVTable
    {
        // --- IUnknown ---
        public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
        public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

        // --- ITfFunction ---
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetDisplayName;

        // --- ITfFnConfigure ---
        public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ushort, Guid*, int> Show;
    }
}
```

## 3. Cài đặt `FunctionProvider` & `FnConfigureImpl`

### 3.1. Cài đặt Implementation (`TSF/ConfigureService.cs`)

C#

```
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF
{
    public static unsafe class ConfigureService
    {
        private static ITfFunctionProviderVTable* _providerVTable;
        private static ITfFnConfigureVTable* _fnConfigureVTable;

        private static IntPtr _providerInstance;
        private static IntPtr _fnConfigureInstance;

        static ConfigureService()
        {
            // 1. Dựng VTable cho ITfFunctionProvider
            _providerVTable = (ITfFunctionProviderVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(ConfigureService), sizeof(ITfFunctionProviderVTable));
            _providerVTable->QueryInterface = &Provider_QueryInterface;
            _providerVTable->AddRef = &Provider_AddRef;
            _providerVTable->Release = &Provider_Release;
            _providerVTable->GetType = &Provider_GetType;
            _providerVTable->GetDescription = &Provider_GetDescription;
            _providerVTable->GetFunction = &Provider_GetFunction;

            IntPtr* pMemProvider = (IntPtr*)NativeMemory.Alloc((nuint)sizeof(IntPtr));
            *pMemProvider = (IntPtr)_providerVTable;
            _providerInstance = (IntPtr)pMemProvider;

            // 2. Dựng VTable cho ITfFnConfigure
            _fnConfigureVTable = (ITfFnConfigureVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(ConfigureService), sizeof(ITfFnConfigureVTable));
            _fnConfigureVTable->QueryInterface = &Configure_QueryInterface;
            _fnConfigureVTable->AddRef = &Configure_AddRef;
            _fnConfigureVTable->Release = &Configure_Release;
            _fnConfigureVTable->GetDisplayName = &Configure_GetDisplayName;
            _fnConfigureVTable->Show = &Configure_Show;

            IntPtr* pMemConfigure = (IntPtr*)NativeMemory.Alloc((nuint)sizeof(IntPtr));
            *pMemConfigure = (IntPtr)_fnConfigureVTable;
            _fnConfigureInstance = (IntPtr)pMemConfigure;
        }

        public static IntPtr ProviderInstance => _providerInstance;

        // --- ITfFunctionProvider Callbacks ---
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Provider_QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        {
            if (ppv == null || riid == null) return HResult.InvalidArg;

            if (*riid == Guids.IidIUnknown || *riid == Guids.IidITfFunctionProvider)
            {
                *ppv = thisPtr;
                return HResult.Ok;
            }
            *ppv = IntPtr.Zero;
            return HResult.NoInterface;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Provider_AddRef(IntPtr thisPtr) => 2;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Provider_Release(IntPtr thisPtr) => 1;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Provider_GetType(IntPtr thisPtr, Guid* pguid)
        {
            if (pguid == null) return HResult.InvalidArg;
            *pguid = Guids.ClsidBambooMintKey;
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Provider_GetDescription(IntPtr thisPtr, IntPtr* pbstrDesc)
        {
            if (pbstrDesc == null) return HResult.InvalidArg;
            *pbstrDesc = Marshal.StringToBSTR("BambooMintKey Configuration Provider");
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Provider_GetFunction(IntPtr thisPtr, Guid* rguid, Guid* riid, IntPtr* ppunk)
        {
            if (ppunk == null || rguid == null || riid == null) return HResult.InvalidArg;

            if (*rguid == Guid.Empty || *rguid == Guids.IidITfFnConfigure)
            {
                if (*riid == Guids.IidIUnknown || *riid == Guids.IidITfFunction || *riid == Guids.IidITfFnConfigure)
                {
                    *ppunk = _fnConfigureInstance;
                    return HResult.Ok;
                }
            }

            *ppunk = IntPtr.Zero;
            return HResult.NoInterface;
        }

        // --- ITfFnConfigure Callbacks ---
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Configure_QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppv)
        {
            if (ppv == null || riid == null) return HResult.InvalidArg;

            if (*riid == Guids.IidIUnknown || *riid == Guids.IidITfFunction || *riid == Guids.IidITfFnConfigure)
            {
                *ppv = thisPtr;
                return HResult.Ok;
            }
            *ppv = IntPtr.Zero;
            return HResult.NoInterface;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Configure_AddRef(IntPtr thisPtr) => 2;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static uint Configure_Release(IntPtr thisPtr) => 1;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Configure_GetDisplayName(IntPtr thisPtr, IntPtr* pbstrName)
        {
            if (pbstrName == null) return HResult.InvalidArg;
            *pbstrName = Marshal.StringToBSTR("BambooMintKey Settings");
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int Configure_Show(IntPtr thisPtr, IntPtr hwndParent, ushort langid, Guid* rguidProfile)
        {
            LaunchSettingsGui(hwndParent);
            return HResult.Ok;
        }

        public static void LaunchSettingsGui(IntPtr hwndParent)
        {
            try
            {
                // Ưu tiên tìm binary GUI nằm cùng thư mục chứa DLL hoặc đường dẫn cố định
                string baseDir = AppContext.BaseDirectory;
                string exePath = Path.Combine(baseDir, "BambooMintKey.Config.exe");

                if (!File.Exists(exePath))
                {
                    // Fallback sang thư mục Program Files chuẩn
                    string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    exePath = Path.Combine(pf, "BambooMintKey", "BambooMintKey.Config.exe");
                }

                if (File.Exists(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"--parent-hwnd {hwndParent}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(startInfo);
                }
            }
            catch
            {
                // Tránh throw ngoại lệ gây crash tiến trình cha (Settings hoặc Explorer)
            }
        }
    }
}
```

## 4. Đăng ký Function Provider với `ITfSourceSingle`

Để Windows nhận biết TIP có hỗ trợ cấu hình, provider phải được đăng ký vào `ThreadMgr` khi kích hoạt.

Cập nhật vào luồng `ActivateEx` và `Deactivate`:

C#

```
// Trong ActivateEx
Guid iidSourceSingle = new("4EA48A35-60AE-446F-8FD6-E6A8D8825E5C"); // ITfSourceSingle
IntPtr pSourceSingle = IntPtr.Zero;

var unk = **(IUnknownVTable**)pThreadMgr;
if (unk.QueryInterface(pThreadMgr, &iidSourceSingle, &pSourceSingle) == HResult.Ok && pSourceSingle != IntPtr.Zero)
{
    var sourceVTable = **(ITfSourceSingleVTable**)pSourceSingle;
    Guid iidProvider = Guids.IidITfFunctionProvider;

    // Đăng ký Provider
    sourceVTable.AdviseSingleSink(pSourceSingle, tfClientId, &iidProvider, ConfigureService.ProviderInstance);

    var unkSource = **(IUnknownVTable**)pSourceSingle;
    unkSource.Release(pSourceSingle);
}

// Trong Deactivate
// Thực hiện UnadviseSingleSink tương tự để giải phóng
```

## 5. Kiến trúc Ứng dụng GUI Độc Lập (`BambooMintKey.Config`)

Ứng dụng Cài đặt được tổ chức thành một sub-project riêng: `src/BambooMintKey.Config/`.

### 5.1. Định hướng Công nghệ

- **Framework:** **Avalonia UI** (sử dụng .NET NativeAOT).
- **Lợi thế:**
  - Khởi động tức thì (< 0.2s), chiếm dụng RAM cực thấp (~15MB).
  - Chạy trực tiếp trên Windows (Win32 API) và **tái sử dụng 100% mã nguồn XAML/C# khi chuyển sang Linux (X11/Wayland)** mà không cần viết lại giao diện.
- **Giao tiếp dữ liệu:** Không gọi IPC hay Socket phức tạp. GUI chỉ thao tác đọc/ghi duy nhất tệp `%AppData%\BambooMintKey\config.json`. Bộ giám sát `ConfigManager` (Bước 4) sẽ tự động nạp cấu hình mới vào Core State Machine.

### 5.2. Cấu trúc Giao diện XAML (`MainWindow.axaml`)

XML

```
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="BambooMintKey.Config.MainWindow"
        Title="BambooMintKey - Bảng Điều Khiển"
        Width="480" Height="420"
        CanResize="False"
        WindowStartupLocation="CenterScreen">

    <TabControl Margin="10">
        <!-- Tab 1: Thiết lập cơ bản -->
        <TabItem Header="Chung">
            <StackPanel Spacing="12" Margin="10">
                <TextBlock Text="Kiểu gõ chính:" FontWeight="Bold"/>
                <StackPanel Orientation="Horizontal" Spacing="20">
                    <RadioButton Name="RbTelex" Content="Telex" GroupName="InputMethod"/>
                    <RadioButton Name="RbVni" Content="VNI" GroupName="InputMethod"/>
                    <RadioButton Name="RbSimpleTelex" Content="Simple Telex" GroupName="InputMethod"/>
                </StackPanel>

                <TextBlock Text="Bảng mã đầu ra:" FontWeight="Bold" Margin="0,10,0,0"/>
                <ComboBox Name="CbCharset" HorizontalAlignment="Stretch">
                    <ComboBoxItem Content="Unicode dựng sẵn"/>
                    <ComboBoxItem Content="Unicode tổ hợp"/>
                    <ComboBoxItem Content="TCVN3 (ABC)"/>
                </ComboBox>

                <TextBlock Text="Phím tắt chuyển Việt/Anh:" FontWeight="Bold" Margin="0,10,0,0"/>
                <ComboBox Name="CbHotkey" HorizontalAlignment="Stretch">
                    <ComboBoxItem Content="Ctrl + Shift"/>
                    <ComboBoxItem Content="Alt + Z"/>
                    <ComboBoxItem Content="Không sử dụng"/>
                </ComboBox>
            </StackPanel>
        </TabItem>

        <!-- Tab 2: Nâng cao & Chính tả -->
        <TabItem Header="Tùy chọn">
            <StackPanel Spacing="10" Margin="10">
                <CheckBox Name="ChkSpell" Content="Kiểm tra chính tả theo từ điển âm tiết"/>
                <CheckBox Name="ChkAutoRestore" Content="Tự động khôi phục từ nếu gõ sai quy tắc"/>
                <CheckBox Name="ChkModern" Content="Đặt dấu theo chuẩn mới (òa, úy thay vì oà, uý)"/>
                <CheckBox Name="ChkMacro" Content="Bật tính năng gõ tắt (Macro)"/>
            </StackPanel>
        </TabItem>

        <!-- Tab 3: Bảng gõ tắt -->
        <TabItem Header="Gõ tắt">
            <Grid RowDefinitions="*, Auto" Margin="10">
                <DataGrid Name="DgMacros" Grid.Row="0" AutoGenerateColumns="False">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Từ tắt" Binding="{Binding Key}" Width="100"/>
                        <DataGridTextColumn Header="Cụm từ thay thế" Binding="{Binding Value}" Width="*"/>
                    </DataGrid.Columns>
                </DataGrid>
                
                <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="10" Margin="0,10,0,0">
                    <Button Name="BtnAddMacro" Content="Thêm"/>
                    <Button Name="BtnDeleteMacro" Content="Xóa"/>
                </StackPanel>
            </Grid>
        </TabItem>
    </TabControl>
</Window>
```

### 5.3. Code-behind Xử lý Lưu Cấu hình (`MainWindow.axaml.cs`)

C#

```
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BambooMintKey.Core;

namespace BambooMintKey.Config
{
    public partial class MainWindow : Window
    {
        private readonly string _configPath;
        private EngineConfig _currentConfig;

        public MainWindow()
        {
            InitializeComponent();

            string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            _configPath = Path.Combine(appData, "BambooMintKey", "config.json");

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                _currentConfig = Configuration.fromJson(json);
            }
            else
            {
                _currentConfig = Configuration.defaultConfig;
            }

            // Gán dữ liệu lên form
            RbTelex.IsChecked = _currentConfig.InputMethod == InputMethod.Telex;
            RbVni.IsChecked = _currentConfig.InputMethod == InputMethod.Vni;
            RbSimpleTelex.IsChecked = _currentConfig.InputMethod == InputMethod.SimpleTelex;

            CbCharset.SelectedIndex = (int)_currentConfig.Charset;
            CbHotkey.SelectedIndex = (int)_currentConfig.ToggleHotkey;

            ChkSpell.IsChecked = _currentConfig.SpellCheck;
            ChkAutoRestore.IsChecked = _currentConfig.AutoRestoreIfInvalid;
            ChkModern.IsChecked = _currentConfig.UseModernOrthography;
            ChkMacro.IsChecked = _currentConfig.MacroEnabled;
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            var updatedConfig = new EngineConfig(
                version: 1,
                inputMethod: RbVni.IsChecked == true ? InputMethod.Vni :
                             RbSimpleTelex.IsChecked == true ? InputMethod.SimpleTelex : InputMethod.Telex,
                charset: (Charset)CbCharset.SelectedIndex,
                toggleHotkey: (ToggleHotkey)CbHotkey.SelectedIndex,
                spellCheck: ChkSpell.IsChecked ?? true,
                autoRestoreIfInvalid: ChkAutoRestore.IsChecked ?? true,
                useModernOrthography: ChkModern.IsChecked ?? true,
                macroEnabled: ChkMacro.IsChecked ?? false,
                macros: _currentConfig.Macros
            );

            // Ghi đè file config.json (Trigger Hot-Reload bên NativeBridge)
            string json = Configuration.toJson(updatedConfig);
            File.WriteAllText(_configPath, json);

            Close();
        }
    }
}
```

## 6. Quy trình Kiểm thử & Validation

1. **Biên dịch Hệ thống:**
   - Build DLL Native Bridge: `dotnet publish src/BambooMintKey.NativeBridge -c Release -r win-x64`.
   - Build GUI Cài đặt: `dotnet publish src/BambooMintKey.Config -c Release -r win-x64`.
   - Copy `BambooMintKey.Config.exe` vào cùng thư mục với DLL runtime.
2. **Kiểm tra Windows Settings Integration:**
   - Mở `Settings` trên Windows $\rightarrow$ `Time & Language` $\rightarrow$ `Language & Region`.
   - Bấm vào mục ngôn ngữ `Vietnamese` $\rightarrow$ `Language options`.
   - Cuộn xuống phần Keyboards: Xác nhận nút **Options** cạnh BambooMintKey đã **sáng lên (enabled)** và bấm được.
3. **Kiểm tra Kích hoạt Cửa sổ:**
   - Bấm nút **Options** $\rightarrow$ Cửa sổ Avalonia UI hiển thị tức thì tại vị trí trung tâm màn hình.
   - Thử chuyển sang kiểu gõ `VNI`, tick bỏ `Kiểm tra chính tả`, bấm nút **Lưu**.
   - Mở Notepad gõ thử phím số: xác nhận engine đã chuyển sang VNI ngay lập tức mà không cần khởi động lại tiến trình nào.