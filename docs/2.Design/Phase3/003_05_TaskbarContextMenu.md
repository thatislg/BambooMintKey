# 003_03_TaskbarContextMenu.md

> Tài liệu kỹ thuật chi tiết về việc cài đặt context menu chuột phải cho nút Language Bar qua `ITfMenu`, xử lý các mục con (Submenu), đánh dấu trạng thái (Checked/Radio), và liên kết lệnh điều khiển tới F# Core.

## 1. Cơ sở chuẩn hóa từ Windows SDK

Toàn bộ hằng số và interface định nghĩa menu của TSF được trích xuất từ `C:\Program Files (x86)\Windows Kits\10\Include\<version>\um\ctfutb.idl` và `ctfutb.h`.

### 1.1. Bảng tra cứu GUID & Hằng số Menu

| **Thành phần**               | **File SDK gốc** | **Giá trị / GUID chuẩn**                    |
| ---------------------------- | ---------------- | ------------------------------------------- |
| `IID_ITfMenu`                | `ctfutb.idl`     | `6F46DC34-42A3-47A6-BEAE-263E4FF5E5D0`      |
| `TF_LBMENUID_APP`            | `ctfutb.h`       | `0x00010000` (Base ID cho app menu items)   |
| `TF_LBMENUFLAG_CHECKED`      | `ctfutb.h`       | `0x00000001` (Hiển thị dấu tích checkmark)  |
| `TF_LBMENUFLAG_SUBMENU`      | `ctfutb.h`       | `0x00000002` (Mục chứa menu con)            |
| `TF_LBMENUFLAG_SEPARATOR`    | `ctfutb.h`       | `0x00000004` (Đường kẻ phân cách ngang)     |
| `TF_LBMENUFLAG_RADIOCHECKED` | `ctfutb.h`       | `0x00000008` (Hiển thị dạng nút tròn radio) |

### 1.2. Định nghĩa Command ID nội bộ

Toàn bộ ID mục menu tùy chỉnh phải bắt đầu từ `TF_LBMENUID_APP` để tránh xung đột với các ID hệ thống của Windows:

C#

```c#
public static class MenuCommands
{
    public const uint Base = 0x00010000;

    public const uint ToggleMode          = Base + 1; // Bật/Tắt gõ tiếng Việt

    public const uint SubmenuInputMethod  = Base + 10; // Menu con: Kiểu gõ
    public const uint ModeTelex           = Base + 11;
    public const uint ModeVni             = Base + 12;
    public const uint ModeSimpleTelex     = Base + 13;

    public const uint SubmenuCharset      = Base + 20; // Menu con: Bảng mã
    public const uint CharsetUnicode      = Base + 21;
    public const uint CharsetCompound     = Base + 22;
    public const uint CharsetTcvn3        = Base + 23;

    public const uint OpenSettings        = Base + 30; // Mở cửa sổ Cài đặt...
}
```

## 2. Khai báo Interface `ITfMenu` VTable

Thêm struct VTable của `ITfMenu` vào `Interop/TsfLangBarTypes.cs`:

C#

```c#
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfMenuVTable
{
    // --- IUnknown ---
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // --- ITfMenu ---
    public delegate* unmanaged[Stdcall]<
        IntPtr,         // this
        uint,           // uId: Menu command ID
        uint,           // uFlags: TF_LBMENUFLAG_*
        IntPtr,         // hbmp: Handle ảnh icon menu (IntPtr.Zero nếu không dùng)
        IntPtr,         // hbmpMask: Handle mask icon
        char*,          // pch: Chuỗi text hiển thị (Unicode)
        uint,           // cch: Chiều dài chuỗi text
        IntPtr*,        // ppMenu: Pointer nhận ITfMenu con nếu uFlags chứa TF_LBMENUFLAG_SUBMENU
        int> AddMenuItem;
}
```

## 3. Cài đặt Hàm Dựng Menu (`InitMenu`)

Khi người dùng click chuột phải vào icon của bộ gõ trên Taskbar, Windows TSF sẽ gọi hàm `InitMenu(IntPtr thisPtr, IntPtr pMenu)`.

Cập nhật implementation trong `TSF/LangBarItemButton.cs`:

C#

```c#
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int InitMenu(IntPtr thisPtr, IntPtr pMenu)
{
    if (pMenu == IntPtr.Zero) return HResult.InvalidArg;

    var menu = **(ITfMenuVTable**)pMenu;

    // 1. Toggle Tiếng Việt
    uint toggleFlags = IsVietnamese ? Constants.TF_LBMENUFLAG_CHECKED : 0;
    AddMenuEntry(menu, pMenu, MenuCommands.ToggleMode, toggleFlags, "Gõ tiếng Việt (Ctrl + Shift)");

    // Dấu phân cách
    AddMenuSeparator(menu, pMenu);

    // 2. Submenu: Kiểu gõ (Telex, VNI, Simple Telex)
    IntPtr pSubInputMethod = IntPtr.Zero;
    menu.AddMenuItem(pMenu, MenuCommands.SubmenuInputMethod, 
        Constants.TF_LBMENUFLAG_SUBMENU, IntPtr.Zero, IntPtr.Zero, 
        null, 0, &pSubInputMethod);

    if (pSubInputMethod != IntPtr.Zero)
    {
        var subMenu = **(ITfMenuVTable**)pSubInputMethod;
        uint currentMethod = BridgeStateManager.CurrentInputMethod; // 0: Telex, 1: VNI, 2: SimpleTelex

        AddMenuEntry(subMenu, pSubInputMethod, MenuCommands.ModeTelex, 
            (currentMethod == 0 ? Constants.TF_LBMENUFLAG_RADIOCHECKED : 0), "Telex");
        AddMenuEntry(subMenu, pSubInputMethod, MenuCommands.ModeVni, 
            (currentMethod == 1 ? Constants.TF_LBMENUFLAG_RADIOCHECKED : 0), "VNI");
        AddMenuEntry(subMenu, pSubInputMethod, MenuCommands.ModeSimpleTelex, 
            (currentMethod == 2 ? Constants.TF_LBMENUFLAG_RADIOCHECKED : 0), "Simple Telex");

        var unk = **(IUnknownVTable**)pSubInputMethod;
        unk.Release(pSubInputMethod);
    }

    // 3. Submenu: Bảng mã
    IntPtr pSubCharset = IntPtr.Zero;
    menu.AddMenuItem(pMenu, MenuCommands.SubmenuCharset, 
        Constants.TF_LBMENUFLAG_SUBMENU, IntPtr.Zero, IntPtr.Zero, 
        null, 0, &pSubCharset);

    if (pSubCharset != IntPtr.Zero)
    {
        var subMenu = **(ITfMenuVTable**)pSubCharset;
        uint currentCharset = BridgeStateManager.CurrentCharset; // 0: Unicode, 1: Compound, 2: TCVN3

        AddMenuEntry(subMenu, pSubCharset, MenuCommands.CharsetUnicode, 
            (currentCharset == 0 ? Constants.TF_LBMENUFLAG_RADIOCHECKED : 0), "Unicode dựng sẵn");
        AddMenuEntry(subMenu, pSubCharset, MenuCommands.CharsetCompound, 
            (currentCharset == 1 ? Constants.TF_LBMENUFLAG_RADIOCHECKED : 0), "Unicode tổ hợp");
        AddMenuEntry(subMenu, pSubCharset, MenuCommands.CharsetTcvn3, 
            (currentCharset == 2 ? Constants.TF_LBMENUFLAG_RADIOCHECKED : 0), "TCVN3 (ABC)");

        var unk = **(IUnknownVTable**)pSubCharset;
        unk.Release(pSubCharset);
    }

    // Dấu phân cách
    AddMenuSeparator(menu, pMenu);

    // 4. Mở cửa sổ Cài đặt
    AddMenuEntry(menu, pMenu, MenuCommands.OpenSettings, 0, "Cài đặt tùy chọn...");

    return HResult.Ok;
}

// Hàm hỗ trợ add item text gọn gàng
private static void AddMenuEntry(ITfMenuVTable menu, IntPtr pMenu, uint id, uint flags, string text)
{
    fixed (char* pText = text)
    {
        menu.AddMenuItem(pMenu, id, flags, IntPtr.Zero, IntPtr.Zero, pText, (uint)text.Length, null);
    }
}

// Hàm hỗ trợ chèn đường kẻ ngăn cách
private static void AddMenuSeparator(ITfMenuVTable menu, IntPtr pMenu)
{
    menu.AddMenuItem(pMenu, 0, Constants.TF_LBMENUFLAG_SEPARATOR, IntPtr.Zero, IntPtr.Zero, null, 0, null);
}
```

## 4. Cài đặt Hàm Xử lý Sự kiện Chọn Menu (`OnMenuSelect`)

Khi người dùng nhấn chọn một dòng trong menu, Windows gọi hàm `OnMenuSelect(IntPtr thisPtr, uint uId)`.

C#

```c#
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnMenuSelect(IntPtr thisPtr, uint uId)
{
    switch (uId)
    {
        case MenuCommands.ToggleMode:
            IsVietnamese = !IsVietnamese;
            NotifyStateChanged();
            break;

        // --- Thay đổi kiểu gõ ---
        case MenuCommands.ModeTelex:
            BridgeStateManager.SetInputMethod(0);
            break;
        case MenuCommands.ModeVni:
            BridgeStateManager.SetInputMethod(1);
            break;
        case MenuCommands.ModeSimpleTelex:
            BridgeStateManager.SetInputMethod(2);
            break;

        // --- Thay đổi bảng mã ---
        case MenuCommands.CharsetUnicode:
            BridgeStateManager.SetCharset(0);
            break;
        case MenuCommands.CharsetCompound:
            BridgeStateManager.SetCharset(1);
            break;
        case MenuCommands.CharsetTcvn3:
            BridgeStateManager.SetCharset(2);
            break;

        // --- Mở Cài đặt ---
        case MenuCommands.OpenSettings:
            SettingsLauncher.LaunchSettingsGui();
            break;
    }

    return HResult.Ok;
}
```

## 5. Móc nối Dữ liệu với `BridgeStateManager` và F# Core

Trong `BridgeStateManager.cs`:

C#

```c#
public static class BridgeStateManager
{
    public static uint CurrentInputMethod { get; private set; } = 0;
    public static uint CurrentCharset { get; private set; } = 0;

    public static void SetInputMethod(uint method)
    {
        CurrentInputMethod = method;
        // Gọi F# Core C-ABI để đổi kiểu gõ sang Telex/VNI trong bộ máy trạng thái
        NativeCoreExports.SetInputMethod(method);
    }

    public static void SetCharset(uint charset)
    {
        CurrentCharset = charset;
        // Cập nhật bảng mã đầu ra cho Engine
        NativeCoreExports.SetCharset(charset);
    }
}
```

## 6. Quy trình Kiểm thử & Validation

1. **Biên dịch Native Bridge:** Chạy `scripts/build-native.ps1`.
2. **Kích hoạt TIP:** Chạy `scripts/enable-tip.ps1`.
3. **Kiểm tra Click Chuột Phải:**
   - Click chuột phải vào icon **V** hoặc **E** trên Taskbar.
   - Menu xuất hiện với đầy đủ tiếng Việt có dấu, các đường separator phân cách rõ ràng.
   - Submenu **Kiểu gõ** và **Bảng mã** bung ra mượt mà, hiển thị đúng dấu chấm radio tròn ở mục đang được kích hoạt.
4. **Kiểm tra Điều khiển Chế độ:**
   - Chọn `VNI` trong menu chuột phải $\rightarrow$ Gõ thử `a1`, `e1` trong Notepad xem ra `á`, `é`.
   - Click chọn `Gõ tiếng Việt (Ctrl + Shift)` $\rightarrow$ Icon trên Taskbar đổi tức thì sang chữ **E**.