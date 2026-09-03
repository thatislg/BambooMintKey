# 003_02_IconHelper_DynamicRendering.md

> Tài liệu kỹ thuật chi tiết về cơ chế vẽ `HICON` động qua Win32 GDI trong bộ nhớ, xử lý trong suốt (transparency), quản lý vòng đời tài nguyên đồ họa và cơ chế kích hoạt cập nhật Taskbar.

## 1. Cơ sở chuẩn hóa & Phân tích kỹ thuật

### 1.1. Tại sao phải vẽ Icon động thay vì dùng file `.ico` tĩnh?

- **Cách ly Sandbox/AppContainer:** Các ứng dụng Universal Windows Platform (UWP), Microsoft Edge hoặc ứng dụng chạy dưới AppContainer có chính sách bảo mật cô lập ổ đĩa, thường không thể đọc file icon từ đường dẫn tuyệt đối hoặc tương đối.
- **Độc lập đóng gói:** Không phụ thuộc vào đường dẫn cài đặt ngoài ổ đĩa; toàn bộ tài nguyên hiển thị được tạo tức thì trong bộ nhớ từ chính DLL NativeAOT.
- **Tối ưu DPI:** Cho phép co giãn và căn chỉnh phông chữ theo kích thước icon chuẩn hệ thống (`SM_CXSMICON`) một cách linh hoạt.

### 1.2. Cấu trúc Win32 Icon (`ICONINFO`)

Một icon chuẩn của Windows được ghép bởi 2 thành phần bitmap:

- `hbmColor`: DIB/Bitmap chứa dữ liệu màu 32-bit (ARGB hoặc RGB kết hợp nền).
- `hbmMask`: Bitmap đơn sắc (1-bit monochrome mask) quyết định điểm ảnh nào trong suốt (màu trắng `1` = trong suốt / giữ nguyên nền, màu đen `0` = vẽ đè màu từ `hbmColor`).

## 2. Mã nguồn `IconHelper.cs`

Tạo mới file tại `src/BambooMintKey.NativeBridge/TSF/IconHelper.cs` để đóng gói toàn bộ lời gọi P/Invoke GDI32 và User32.

C#

```c#
using System;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF
{
    public static class IconHelper
    {
        // --- Win32 GDI & User32 P/Invoke ---
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, IntPtr lpBits);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int SetBkMode(IntPtr hdc, int iBkMode);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern uint SetTextColor(IntPtr hdc, uint crColor);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFontW(
            int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
            uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
            uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
            uint fdwPitchAndFamily, string lpszFace);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DrawTextW(IntPtr hDC, string lpchText, int nCount, ref RECT lpRect, uint uFormat);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        private const int TRANSPARENT = 1;
        private const int FW_BOLD = 700;
        private const uint DT_CENTER = 0x00000001;
        private const uint DT_VCENTER = 0x00000004;
        private const uint DT_SINGLELINE = 0x00000020;

        /// <summary>
        /// Tạo một HICON động chứa ký tự text (V hoặc E) với màu sắc xác định.
        /// </summary>
        /// <param name="text">Ký tự cần vẽ ("V" hoặc "E")</param>
        /// <param name="rgbColor">Mã màu Win32 COLORREF: 0x00bbggrr</param>
        /// <returns>IntPtr trỏ tới HICON hợp lệ</returns>
        public static IntPtr CreateTextIcon(string text, uint rgbColor)
        {
            const int width = 16;
            const int height = 16;

            IntPtr hScreenDC = GetDC(IntPtr.Zero);
            IntPtr hColorDC = CreateCompatibleDC(hScreenDC);
            IntPtr hColorBmp = CreateCompatibleBitmap(hScreenDC, width, height);
            IntPtr hOldColorBmp = SelectObject(hColorDC, hColorBmp);

            // 1. Tạo Font đậm nét để hiển thị rõ ở kích thước nhỏ (16x16)
            IntPtr hFont = CreateFontW(
                -14, 0, 0, 0, FW_BOLD,
                0, 0, 0, 1 /* DEFAULT_CHARSET */,
                0, 0, 5 /* CLEARTYPE_QUALITY */,
                0, "Segoe UI");
            IntPtr hOldFont = SelectObject(hColorDC, hFont);

            // 2. Thiết lập vẽ chữ trên Color DC
            SetBkMode(hColorDC, TRANSPARENT);
            SetTextColor(hColorDC, rgbColor);

            RECT rect = new RECT { Left = 0, Top = 0, Right = width, Bottom = height };
            DrawTextW(hColorDC, text, text.Length, ref rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

            // 3. Tạo Mask Bitmap (Monochrome) cho vùng trong suốt
            // Đối với chữ V/E đơn giản, ta dùng mask đen toàn bộ để nền hòa trộn màu hệ thống
            IntPtr hMaskBmp = CreateBitmap(width, height, 1, 1, IntPtr.Zero);

            ICONINFO iconInfo = new ICONINFO
            {
                fIcon = true,
                xHotspot = 0,
                yHotspot = 0,
                hbmMask = hMaskBmp,
                hbmColor = hColorBmp
            };

            IntPtr hIcon = CreateIconIndirect(ref iconInfo);

            // 4. Giải phóng triệt để tài nguyên GDI trung gian tránh GDI Leak
            SelectObject(hColorDC, hOldFont);
            SelectObject(hColorDC, hOldColorBmp);
            DeleteObject(hFont);
            DeleteObject(hColorBmp);
            DeleteObject(hMaskBmp);
            DeleteDC(hColorDC);
            ReleaseDC(IntPtr.Zero, hScreenDC);

            return hIcon;
        }
    }
}
```

## 3. Cập nhật `LangBarItemButton.cs` để liên kết Icon & Sink

Tích hợp hàm vẽ icon vào luồng gọi của Windows TSF và quản lý bộ đệm cache icon (`src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs`).

### 3.1. Quản lý Cache `HICON`

Vì Windows TSF có thể gọi `GetIcon` liên tục khi người dùng di chuột qua lại Taskbar, việc gọi hàm vẽ mới liên tục sẽ lãng phí tài nguyên. Ta cache lại 2 handle `_hIconV` và `_hIconE`.

C#

```c#
private static IntPtr _hIconV = IntPtr.Zero;
private static IntPtr _hIconE = IntPtr.Zero;

// Đỏ cờ: RGB(220, 20, 60) -> Win32 COLORREF (0x003C14DC)
private const uint COLOR_V = 0x003C14DC; 

// Xanh dương: RGB(30, 144, 255) -> Win32 COLORREF (0x00FF901E)
private const uint COLOR_E = 0x00FF901E;

[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
{
    if (phIcon == null) return HResult.InvalidArg;

    if (IsVietnamese)
    {
        if (_hIconV == IntPtr.Zero)
        {
            _hIconV = IconHelper.CreateTextIcon("V", COLOR_V);
        }
        *phIcon = _hIconV;
    }
    else
    {
        if (_hIconE == IntPtr.Zero)
        {
            _hIconE = IconHelper.CreateTextIcon("E", COLOR_E);
        }
        *phIcon = _hIconE;
    }

    return HResult.Ok;
}
```

### 3.2. Giải phóng Cache khi Hủy Đăng ký (`Unregister`)

Bổ sung vào hàm `Unregister()` của `LangBarItemButton`:

C#

```c#
if (_hIconV != IntPtr.Zero)
{
    IconHelper.DestroyIcon(_hIconV);
    _hIconV = IntPtr.Zero;
}

if (_hIconE != IntPtr.Zero)
{
    IconHelper.DestroyIcon(_hIconE);
    _hIconE = IntPtr.Zero;
}
```

## 4. Cơ chế Kích hoạt Cập nhật (Notification Mechanism)

Khi trạng thái thay đổi qua click chuột trái hoặc phím tắt, ta gửi thông báo tới Windows thông qua `ITfLangBarItemSink`.

### 4.1. Cài đặt Sink Callback

Trong `LangBarItemButton.cs`:

C#

```c#
public static void NotifyStateChanged()
{
    // Cập nhật trạng thái logic vào F# Engine Core
    BridgeStateManager.SetVietnameseMode(IsVietnamese);

    // Bắn tín hiệu cho Windows Taskbar vẽ lại UI
    if (_langBarMgr != IntPtr.Zero && _sinkCookie != 0)
    {
        // Khi AdviseItemSink được đăng ký thành công, Windows sẽ gọi lại GetIcon và GetTooltip
        // thông qua sink interface của Item.
        // Ngoài ra ta có thể gọi trực tiếp OnUpdate nếu giữ reference của ITfLangBarItemSink:
        if (_itemSink != IntPtr.Zero)
        {
            var sinkVTable = **(ITfLangBarItemSinkVTable**)_itemSink;
            sinkVTable.OnUpdate(_itemSink, Constants.TF_LBI_ICON | Constants.TF_LBI_TOOLTIP | Constants.TF_LBI_TEXT);
        }
    }
}
```

### 4.2. Móc nối với Phím tắt (`ITfKeyEventSink`)

Trong phương thức xử lý phím `OnKeyDown` của bộ gõ:

C#

```
// Kiểm tra tổ hợp phím Ctrl + Shift hoặc Alt + Z
if (IsToggleHotkeyPressed(wParam, lParam))
{
    LangBarItemButton.IsVietnamese = !LangBarItemButton.IsVietnamese;
    LangBarItemButton.NotifyStateChanged();
    *pfEaten = 1; // Nuốt phím không đưa vào ứng dụng
    return HResult.Ok;
}
```

## 5. Quy trình Kiểm thử & Validation

1. **Build mã nguồn:** Chạy `scripts/build-native.ps1` để biên dịch DLL NativeAOT.  
2. **Kích hoạt TIP:** Chạy `scripts/enable-tip.ps1`.  
3. **Kiểm tra hiển thị Icon:**
   - Chuyển sang BambooMintKey bằng `Win + Space`.
   - Quan sát khay Taskbar: Biểu tượng chữ **V** màu đỏ xuất hiện rõ nét cạnh Language Bar.
4. **Kiểm tra Click Toggle:**
   - Dùng chuột trái click vào icon trên Taskbar.
   - Icon đổi tức thì sang chữ **E** màu xanh; tooltip khi hover chuột đổi từ `"BambooMintKey: Tiếng Việt"` sang `"BambooMintKey: English"`.
5. **Kiểm tra GDI Leak:**
   - Bật tiện ích `Task Manager` (thêm cột GDI Objects) hoặc `GDIView`.
   - Click liên tục vào icon 50 lần. Số lượng GDI Objects của tiến trình chứa TIP (`ctfmon.exe` hoặc `explorer.exe`) phải giữ nguyên không tăng tịnh tiến.  