# 003_04_IconHelper_DynamicRendering.md

> Tài liệu kỹ thuật chi tiết về cơ chế vẽ `HICON` động qua Win32 GDI trong bộ nhớ, tạo icon nền xanh lá bo góc mang nhận diện thương hiệu BambooMintKey, xử lý trong suốt (transparency mask), quản lý bộ nhớ đệm cache và tích hợp phím tắt chuyển chế độ (`Ctrl + Shift` / `Alt + Z`).

---

## 1. Phân tích Kỹ thuật & Yêu cầu Thực tế

### 1.1. Tại sao Taskbar Windows 10/11 bắt buộc cần `HICON`?
* **Thực tế trên Windows 10 & 11:** Khay thông báo Taskbar (Notification Area / System Tray) không hỗ trợ nút bấm chỉ chứa thuần văn bản (`GetText`). Nếu `ITfLangBarItemButton::GetIcon` trả về `IntPtr.Zero` (`NULL`), shell Windows sẽ coi như nút không có nội dung trực quan và **ẩn toàn bộ nút**, chỉ hiển thị icon bàn phím mặc định của Windows (`VIE`).
* **Yêu cầu:** Bắt buộc `ITfLangBarItemButton::GetIcon` phải trả về con trỏ `HICON` hợp lệ để Windows Taskbar vẽ biểu tượng nút bấm.

### 1.2. Nhận diện Thương hiệu BambooMintKey
Theo yêu cầu thiết kế và quy chuẩn từ các tài nguyên:
* File gốc chữ **V**: [`src/media/bamboo_mint_key_ico.svg`](file:///D:/Kojin/BambooMintKey/src/media/bamboo_mint_key_ico.svg)
* File gốc chữ **E**: [`src/media/bamboo_mint_key_ico_e.svg`](file:///D:/Kojin/BambooMintKey/src/media/bamboo_mint_key_ico_e.svg)

Cả hai icon đều tuân theo hệ màu thương hiệu:
* **Màu nền (Background):** Xanh lá cây Bamboo `#16a34a` (RGB: 22, 163, 74 $\rightarrow$ Win32 COLORREF: `0x004AA316`).
* **Màu viền (Border):** Xanh mint nhạt `#86efac` (RGB: 134, 239, 172 $\rightarrow$ Win32 COLORREF: `0x00ACEF86`).
* **Màu chữ (Text/Glyph):** Trắng ngà `#fbf8f9` (RGB: 251, 248, 249 $\rightarrow$ Win32 COLORREF: `0x00F9F8FB`).
* **Hình dạng:** Khung hình chữ nhật bo tròn 4 góc (Rounded Rectangle).

### 1.3. Cơ chế Khử Răng cưa & Độ trong suốt (Win32 Masking Architecture)
Một Win32 `HICON` chuẩn được tạo thông qua `CreateIconIndirect` với cấu trúc `ICONINFO`:
* `hbmColor`: Bitmap 32-bit màu RGB chứa hình vẽ nút (nền xanh `#16a34a`, viền mint `#86efac`, chữ trắng đậm).
* `hbmMask`: Bitmap đơn sắc 1-bit monochrome:
  * Bit = `0` (Màu đen trong mask): **Opaque** $\rightarrow$ Điểm ảnh tương ứng trong `hbmColor` được vẽ đè lên màn hình.
  * Bit = `1` (Màu trắng trong mask): **Transparent** $\rightarrow$ Điểm ảnh trên màn hình được giữ nguyên (trong suốt).
* **Xử lý 4 góc bo tròn:** 
  * Trong `hbmMask`, ta quét toàn bộ nền bằng màu trắng (`1` - trong suốt), sau đó vẽ một hình chữ nhật bo góc bằng màu đen (`0` - đục).
  * Nhờ vậy, 4 góc bên ngoài hình bo tròn sẽ hoàn toàn trong suốt, hiển thị liền mạch trên cả giao diện Taskbar Dark Theme lẫn Light Theme của Windows.

### 1.4. Hỗ trợ DPI Động (`SM_CXSMICON` / `SM_CYSMICON`)
Thay vì cố định cứng 16x16 pixel (sẽ bị mờ hoặc vỡ nét trên màn hình 125%, 150%, 200%), `IconHelper` truy vấn kích thước icon khay hệ thống thời gian thực:
* `GetSystemMetrics(SmCxSmIcon = 49)` và `GetSystemMetrics(SmCySmIcon = 50)`.
* Tự động tính toán kích cỡ font chữ và bán kính bo góc tỷ lệ theo kích thước DPI.

---

## 2. Đặc tả Chi tiết `IconHelper.cs`

Tạo file mới tại `src/BambooMintKey.NativeBridge/TSF/IconHelper.cs`:

```csharp
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Trợ thủ vẽ HICON động trong bộ nhớ qua Win32 GDI cho Taskbar Language Bar.
/// Tạo icon bo góc màu xanh lá BambooMintKey (#16a34a) với chữ trắng ngà (#fbf8f9).
/// </summary>
public static class IconHelper
{
    // =========================================================================
    // Win32 Constants (.NET PascalCase Style kèm chú thích WinSDK gốc)
    // =========================================================================

    /// <summary>SM_CXSMICON (49) - Chiều rộng icon nhỏ theo DPI hệ thống.</summary>
    private const int SmCxSmIcon = 49;

    /// <summary>SM_CYSMICON (50) - Chiều cao icon nhỏ theo DPI hệ thống.</summary>
    private const int SmCySmIcon = 50;

    /// <summary>TRANSPARENT (1) - Nền chữ trong suốt trong GDI.</summary>
    private const int BkModeTransparent = 1;

    /// <summary>FW_BOLD (700) - Độ đậm nét chữ Bold.</summary>
    private const int FwBold = 700;

    /// <summary>FW_HEAVY (900) - Độ đậm nét chữ Heavy (hiển thị rõ ở kích thước nhỏ).</summary>
    private const int FwHeavy = 900;

    /// <summary>DT_CENTER (0x00000001) - Canh giữa theo chiều ngang.</summary>
    private const uint DtCenter = 0x00000001;

    /// <summary>DT_VCENTER (0x00000004) - Canh giữa theo chiều dọc.</summary>
    private const uint DtVcenter = 0x00000004;

    /// <summary>DT_SINGLELINE (0x00000020) - Vẽ trên một dòng đơn.</summary>
    private const uint DtSingleline = 0x00000020;

    // =========================================================================
    // BambooMintKey Brand Palette (COLORREF format: 0x00BBGGRR)
    // =========================================================================

    /// <summary>Nền xanh lá Bamboo (#16a34a -> RGB 22, 163, 74 -> BGR 0x004AA316).</summary>
    public const uint ColorBackground = 0x004AA316;

    /// <summary>Viền xanh mint nhạt (#86efac -> RGB 134, 239, 172 -> BGR 0x00ACEF86).</summary>
    public const uint ColorBorder = 0x00ACEF86;

    /// <summary>Chữ trắng ngà (#fbf8f9 -> RGB 251, 248, 249 -> BGR 0x00F9F8FB).</summary>
    public const uint ColorText = 0x00F9F8FB;

    // =========================================================================
    // Win32 GDI & User32 P/Invoke
    // =========================================================================

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetrics(int nIndex);

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
    private static extern IntPtr CreateSolidBrush(uint crColor);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr CreatePen(int iStyle, int cWidth, uint color);

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

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern bool RoundRect(IntPtr hdc, int left, int top, int right, int bottom, int width, int height);

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

    /// <summary>
    /// Lấy kích thước chuẩn icon khay hệ thống theo DPI hiện tại.
    /// </summary>
    public static (int width, int height) GetTrayIconMetrics()
    {
        int cx = GetSystemMetrics(SmCxSmIcon);
        int cy = GetSystemMetrics(SmCySmIcon);
        if (cx <= 0) cx = 16;
        if (cy <= 0) cy = 16;
        return (cx, cy);
    }

    /// <summary>
    /// Tạo một HICON động chứa ký tự text ("V" hoặc "E") với nền xanh lá bo góc BambooMintKey.
    /// </summary>
    /// <param name="text">Ký tự cần vẽ ("V" hoặc "E")</param>
    /// <returns>IntPtr trỏ tới HICON hợp lệ (cần được giải phóng bằng DestroyIcon khi tắt app)</returns>
    public static IntPtr CreateBambooIcon(string text)
    {
        var (width, height) = GetTrayIconMetrics();
        int cornerRadius = Math.Max(4, width / 4);

        IntPtr hScreenDC = GetDC(IntPtr.Zero);

        // ---------------------------------------------------------------------
        // 1. Tạo Color Bitmap (Nền xanh lá #16a34a, viền mint #86efac, chữ trắng)
        // ---------------------------------------------------------------------
        IntPtr hColorDC = CreateCompatibleDC(hScreenDC);
        IntPtr hColorBmp = CreateCompatibleBitmap(hScreenDC, width, height);
        IntPtr hOldColorBmp = SelectObject(hColorDC, hColorBmp);

        // Tạo Brush nền xanh và Pen viền mint
        IntPtr hBrushBg = CreateSolidBrush(ColorBackground);
        IntPtr hPenBorder = CreatePen(0 /* PS_SOLID */, 1, ColorBorder);
        IntPtr hOldBrush = SelectObject(hColorDC, hBrushBg);
        IntPtr hOldPen = SelectObject(hColorDC, hPenBorder);

        // Vẽ hình chữ nhật bo góc
        RoundRect(hColorDC, 0, 0, width, height, cornerRadius, cornerRadius);

        // Tạo font chữ nét đậm Segoe UI / Arial
        int fontHeight = -((height * 7) / 10);
        IntPtr hFont = CreateFontW(
            fontHeight, 0, 0, 0, FwHeavy,
            0, 0, 0, 1 /* DEFAULT_CHARSET */,
            0, 0, 5 /* CLEARTYPE_QUALITY */,
            0, "Segoe UI");
        IntPtr hOldFont = SelectObject(hColorDC, hFont);

        SetBkMode(hColorDC, BkModeTransparent);
        SetTextColor(hColorDC, ColorText);

        RECT textRect = new() { Left = 0, Top = 0, Right = width, Bottom = height };
        DrawTextW(hColorDC, text, text.Length, ref textRect, DtCenter | DtVcenter | DtSingleline);

        // ---------------------------------------------------------------------
        // 2. Tạo Mask Bitmap (Monochrome 1-bit: 0 = đục, 1 = trong suốt)
        // ---------------------------------------------------------------------
        IntPtr hMaskDC = CreateCompatibleDC(hScreenDC);
        IntPtr hMaskBmp = CreateBitmap(width, height, 1, 1, IntPtr.Zero);
        IntPtr hOldMaskBmp = SelectObject(hMaskDC, hMaskBmp);

        // Phủ toàn bộ Mask màu trắng (0x00FFFFFF -> Trong suốt hoàn toàn)
        IntPtr hBrushWhite = CreateSolidBrush(0x00FFFFFF);
        IntPtr hPenWhite = CreatePen(0, 1, 0x00FFFFFF);
        IntPtr hOldMaskBrush = SelectObject(hMaskDC, hBrushWhite);
        IntPtr hOldMaskPen = SelectObject(hMaskDC, hPenWhite);
        RoundRect(hMaskDC, -1, -1, width + 1, height + 1, 0, 0);

        // Vẽ hình chữ nhật bo góc màu đen (0x00000000 -> Đục/Hiển thị Color)
        IntPtr hBrushBlack = CreateSolidBrush(0x00000000);
        IntPtr hPenBlack = CreatePen(0, 1, 0x00000000);
        SelectObject(hMaskDC, hBrushBlack);
        SelectObject(hMaskDC, hPenBlack);
        RoundRect(hMaskDC, 0, 0, width, height, cornerRadius, cornerRadius);

        // ---------------------------------------------------------------------
        // 3. Đóng gói vào ICONINFO và sinh HICON
        // ---------------------------------------------------------------------
        ICONINFO iconInfo = new()
        {
            fIcon = true,
            xHotspot = 0,
            yHotspot = 0,
            hbmMask = hMaskBmp,
            hbmColor = hColorBmp
        };

        IntPtr hIcon = CreateIconIndirect(ref iconInfo);

        // ---------------------------------------------------------------------
        // 4. Dọn dẹp tài nguyên trung gian (Tránh GDI Leak)
        // ---------------------------------------------------------------------
        SelectObject(hColorDC, hOldFont);
        SelectObject(hColorDC, hOldBrush);
        SelectObject(hColorDC, hOldPen);
        SelectObject(hColorDC, hOldColorBmp);
        DeleteObject(hFont);
        DeleteObject(hBrushBg);
        DeleteObject(hPenBorder);
        DeleteObject(hColorBmp);
        DeleteDC(hColorDC);

        SelectObject(hMaskDC, hOldMaskBrush);
        SelectObject(hMaskDC, hOldMaskPen);
        SelectObject(hMaskDC, hOldMaskBmp);
        DeleteObject(hBrushWhite);
        DeleteObject(hPenWhite);
        DeleteObject(hBrushBlack);
        DeleteObject(hPenBlack);
        DeleteObject(hMaskBmp);
        DeleteDC(hMaskDC);

        ReleaseDC(IntPtr.Zero, hScreenDC);

        return hIcon;
    }
}
```

---

## 3. Tích hợp Quản lý Cache Icon vào `LangBarItemButton.cs`

Trong [LangBarItemButton.cs](file:///D:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs):

### 3.1. Caching Handle `_hIconV` và `_hIconE`
Windows TSF gọi `GetIcon` liên tục khi người dùng di chuột qua thanh Taskbar. Để tránh tạo và hủy GDI bitmap hàng trăm lần mỗi giây, hai handle `_hIconV` và `_hIconE` được tạo theo cơ chế Lazy Initialization và lưu vào bộ nhớ cache:

```csharp
private static IntPtr _hIconV = IntPtr.Zero;
private static IntPtr _hIconE = IntPtr.Zero;

/// <summary>[WinSDK: ITfLangBarItemButton::GetIcon] - Cung cấp con trỏ HICON để Windows vẽ icon Taskbar.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
{
    if (phIcon == null) return HResult.InvalidArgument;

    if (BridgeStateManager.IsVietnameseMode)
    {
        if (_hIconV == IntPtr.Zero)
        {
            _hIconV = IconHelper.CreateBambooIcon("V");
        }
        *phIcon = _hIconV;
    }
    else
    {
        if (_hIconE == IntPtr.Zero)
        {
            _hIconE = IconHelper.CreateBambooIcon("E");
        }
        *phIcon = _hIconE;
    }

    return HResult.Ok;
}
```

### 3.2. Giải phóng Cache trong `Unregister`
Khi bộ gõ bị hủy kích hoạt hoặc gỡ cài đặt, bắt buộc phải giải phóng hai handle icon để tránh rò rỉ tài nguyên hệ thống (GDI Handle Leak):

```csharp
public static void Unregister()
{
    // 1. Gỡ Item khỏi Language Bar Manager
    if (_langBarMgr != IntPtr.Zero)
    {
        var mgrVTable = **(ITfLangBarItemMgrVTable**)_langBarMgr;
        mgrVTable.RemoveItem(_langBarMgr, _comInstance);

        NativeCom.Release(_langBarMgr);
        _langBarMgr = IntPtr.Zero;
    }

    // 2. Giải phóng con trỏ Sink kết nối từ Windows
    if (_pLangBarSink != IntPtr.Zero)
    {
        NativeCom.Release(_pLangBarSink);
        _pLangBarSink = IntPtr.Zero;
        _sinkCookie = 0;
    }

    // 3. Giải phóng bộ đệm HICON (Tránh rò rỉ GDI Objects)
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
}
```

---

## 4. Tích hợp Phím tắt Chuyển chế độ vào `KeyEventSinkImpl.cs`

Để người dùng có thể chuyển đổi nhanh giữa tiếng Việt và tiếng Anh bằng bàn phím (không cần dùng chuột click vào khay hệ thống), `KeyEventSinkImpl` cài đặt bộ kiểm tra phím tắt.

### 4.1. Định nghĩa Tổ hợp Phím tắt
* **Tổ hợp 1 (`Ctrl + Shift`):** Nhấn phím `Shift` khi phím `Ctrl` đang được giữ (hoặc ngược lại).
* **Tổ hợp 2 (`Alt + Z`):** Nhấn phím `Z` khi phím `Alt` đang được giữ.

### 4.2. Mã nguồn Xử lý trong `KeyEventSinkImpl.cs`
Trong `OnTestKeyDown` và `OnKeyDown`:

```csharp
// Kiểm tra tổ hợp phím tắt Toggle V/E
if (KeyInputTranslator.IsToggleHotkeyPressed(wParam, lParam))
{
    *pfEaten = 1; // Nuốt phím để không phát ký tự ra ứng dụng
    return HResult.Ok;
}
```

Trong `OnKeyDown`:
```csharp
if (KeyInputTranslator.IsToggleHotkeyPressed(wParam, lParam))
{
    // 1. Đảo trạng thái bộ gõ trong BridgeStateManager
    BridgeStateManager.ToggleVietnameseMode();

    // 2. Thông báo cho Windows Taskbar vẽ lại icon V/E và cập nhật Tooltip
    LangBarItemButton.NotifyStateChanged();

    *pfEaten = 1;
    return HResult.Ok;
}
```

Trong `KeyInputTranslator.cs`:
```csharp
/// <summary>
/// Kiểm tra xem sự kiện bàn phím hiện tại có phải là phím tắt chuyển đổi chế độ V/E hay không.
/// Hỗ trợ: Ctrl + Shift hoặc Alt + Z.
/// </summary>
public static bool IsToggleHotkeyPressed(UIntPtr wParam, IntPtr lParam)
{
    uint vk = (uint)wParam;

    // Trường hợp 1: Alt + Z (vk == 0x5A và phím Alt đang giữ)
    if (vk == 0x5A /* 'Z' */ && (GetKeyState((int)VkMenu) & 0x8000) != 0)
    {
        return true;
    }

    // Trường hợp 2: Ctrl + Shift (bấm Shift khi Ctrl đang giữ, hoặc bấm Ctrl khi Shift đang giữ)
    bool isCtrl = (vk == 0x11 /* VK_CONTROL */ || vk == 0xA2 /* VK_LCONTROL */ || vk == 0xA3 /* VK_RCONTROL */);
    bool isShift = (vk == 0x10 /* VK_SHIFT */ || vk == 0xA0 /* VK_LSHIFT */ || vk == 0xA1 /* VK_RSHIFT */);

    if (isShift && (GetKeyState((int)VkControl) & 0x8000) != 0)
    {
        return true;
    }

    if (isCtrl && (GetKeyState(0x10 /* VK_SHIFT */) & 0x8000) != 0)
    {
        return true;
    }

    return false;
}
```

---

## 5. Kế hoạch Kiểm thử & Nghiệm thu (Verification Matrix)

| Bước kiểm thử | Thao tác thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- |
| **1. DevHarness Test** | Chạy `dotnet run --project src/BambooMintKey.DevHarness` | `CreateBambooIcon("V")` và `"E"` trả về `HICON != IntPtr.Zero`. `GetIcon` phản hồi đúng icon theo trạng thái. Không rò rỉ bộ nhớ. |
| **2. NativeAOT Build** | Chạy `pwsh scripts/build-native.ps1` | Biên dịch ra DLL `publish/win-x64/BambooMintKey.dll` thành công không cảnh báo calling convention hay AOT trim. |
| **3. Hiển thị Icon Taskbar** | Đăng ký TIP, chuyển sang BambooMintKey bằng `Win + Space` | Khay Taskbar xuất hiện ngay lập tức icon hình vuông nền xanh lá cây bo góc có chữ **V** màu trắng ngà cạnh chữ `VIE`. |
| **4. Click Chuột Đổi Icon** | Click chuột trái vào icon chữ **V** | Icon chuyển tức thì sang chữ **E** (nền xanh lá, chữ E màu trắng), tooltip chuyển thành `"BambooMintKey: English"`. Click tiếp đổi lại chữ **V**. |
| **5. Phím tắt Toggle** | Bấm `Ctrl + Shift` hoặc `Alt + Z` | Icon trên Taskbar tự động đổi giữa **V** $\leftrightarrow$ **E**. Chế độ gõ tiếng Việt tắt/bật đồng bộ. |
| **6. GDI Leak Check** | Dùng Task Manager (cột GDI Objects) click toggle 50 lần | Số lượng GDI Objects của tiến trình không tăng lũy tiến (đạt tiêu chuẩn 0 GDI Leak). |