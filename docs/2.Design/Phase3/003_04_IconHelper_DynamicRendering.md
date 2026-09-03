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
    /// <param name="width">Chiều rộng tùy chọn (mặc định 0 để tự lấy theo DPI)</param>
    /// <param name="height">Chiều cao tùy chọn (mặc định 0 để tự lấy theo DPI)</param>
    /// <returns>IntPtr trỏ tới HICON hợp lệ</returns>
    public static IntPtr CreateBambooIcon(string text, int width = 0, int height = 0)
    {
        if (width <= 0 || height <= 0)
        {
            var metrics = GetTrayIconMetrics();
            width = metrics.width;
            height = metrics.height;
        }

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

        // Tạo font chữ nét đậm Segoe UI
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

## 3. Quản lý Hiển thị Icon & Đồng bộ trong `LangBarItemButton.cs`

Mã nguồn tại [`src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs):

### 3.1. Cung cấp Icon qua `ITfLangBarItemButton::GetIcon`

```csharp
/// <summary>[WinSDK: ITfLangBarItemButton::GetIcon] - Cung cấp con trỏ HICON để Windows vẽ icon Taskbar.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
{
    if (phIcon == null) return HResult.InvalidArgument;

    // Theo đặc tả Microsoft WinSDK cho ITfLangBarItemButton::GetIcon:
    // "The caller is responsible for destroying this icon when it is no longer required."
    // Windows Taskbar Shell sẽ tự động gọi DestroyIcon sau khi vẽ.
    // Bắt buộc phải tạo HICON mới mỗi lần để tránh cung cấp handle đã bị hủy.
    string text = BridgeStateManager.IsVietnameseMode ? "V" : "E";
    *phIcon = IconHelper.CreateBambooIcon(text);
    DebugLog.Write($"LangBarItemButton.GetIcon: Created fresh HICON for '{text}' -> {*phIcon}");

    return HResult.Ok;
}
```

### 3.2. Xử lý Sự kiện Click Chuột Trái (`OnClick`)

```csharp
/// <summary>[WinSDK: ITfLangBarItemButton::OnClick] - Xử lý sự kiện click chuột từ người dùng.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnClick(IntPtr thisPtr, uint click, POINT pt, RECT* prcArea)
{
    DebugLog.Write($"LangBarItemButton OnClick received click={click}");
    // Chỉ xử lý khi đúng là click chuột trái (TF_LBI_CLK_LEFT = 2)
    if (click == TsfLangBarFlags.TfLbiClkLeft)
    {
        bool newMode = BridgeStateManager.ToggleVietnameseMode();
        NotifyStateChanged();
        DebugLog.Write($"LangBarItemButton OnClick toggled IsVietnameseMode={newMode}");
    }
    return HResult.Ok;
}
```

### 3.3. Đồng bộ Liên tiến trình qua `StartEventListener`

Để khi một tiến trình ứng dụng thay đổi trạng thái (hoặc phím tắt được bấm ở bất kỳ ứng dụng nào), Taskbar Language Bar nhận diện và vẽ lại:

```csharp
private static bool _listenerStarted = false;

private static void StartEventListener()
{
    var thread = new System.Threading.Thread(() =>
    {
        IntPtr hEv = SharedMemoryManager.StateChangedEventHandle;
        bool lastMode = BridgeStateManager.IsVietnameseMode;

        while (true)
        {
            // Chờ event tối đa 100ms
            if (hEv != IntPtr.Zero)
            {
                SharedMemoryManager.WaitForSingleObject(hEv, 100);
            }
            else
            {
                System.Threading.Thread.Sleep(100);
            }

            // Kiểm tra trạng thái thực tế trong Shared Memory để luôn đồng bộ Taskbar
            bool currentMode = BridgeStateManager.IsVietnameseMode;
            if (currentMode != lastMode)
            {
                lastMode = currentMode;
                NotifyStateChanged();
            }
        }
    })
    {
        IsBackground = true,
        Name = "BambooMintKey_StateEventListener"
    };
    thread.Start();
}
```

---

## 4. Tích hợp Phím tắt Chuyển chế độ vào `KeyInputTranslator.cs` và `KeyEventSinkImpl.cs`

Mã nguồn hiện tại đã cập nhật phím tắt chính là **`Ctrl + Shift + Q`** (thay vì `Ctrl + Shift` để tránh trùng lặp với phím tắt mặc định chuyển ngôn ngữ của Windows), kèm phím tắt phụ **`Alt + Z`**.

### 4.1. Mã nguồn Kiểm tra Phím tắt trong `KeyInputTranslator.cs`

Mã nguồn tại [`src/BambooMintKey.NativeBridge/Interop/KeyInputTranslator.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Interop/KeyInputTranslator.cs):

```csharp
[DllImport("user32.dll")]
private static extern short GetAsyncKeyState(int vKey);

private static bool IsKeyDown(int vKey)
{
    return ((GetKeyState(vKey) & 0x8000) != 0) || ((GetAsyncKeyState(vKey) & 0x8000) != 0);
}

// =========================================================================
// Hotkey detection
// =========================================================================

public const uint VkShift = 0x10;
public const uint VkQ = 0x51;
public const uint VkZ = 0x5A;

/// <summary>
/// Kiểm tra xem sự kiện bàn phím hiện tại có phải là phím tắt chuyển đổi chế độ V/E hay không.
/// Theo yêu cầu người dùng: Ctrl + Shift + Q.
/// </summary>
public static bool IsToggleHotkeyPressed(UIntPtr wParam, IntPtr lParam)
{
    uint vk = (uint)wParam;

    // Phím tắt chính: Ctrl + Shift + Q
    if (vk == VkQ)
    {
        bool isCtrl = IsKeyDown((int)VkControl) || IsKeyDown(0xA2) || IsKeyDown(0xA3);
        bool isShift = IsKeyDown((int)VkShift) || IsKeyDown(0xA0) || IsKeyDown(0xA1);
        if (isCtrl && isShift)
        {
            return true;
        }
    }

    // Phím tắt dự phòng: Alt + Z
    if (vk == VkZ && (IsKeyDown((int)VkMenu) || IsKeyDown(0xA4) || IsKeyDown(0xA5)))
    {
        return true;
    }

    return false;
}
```

### 4.2. Mã nguồn Bắt Phím trong `KeyEventSinkImpl.cs`

Mã nguồn tại [`src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs):

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnTestKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
{
    if (pfEaten == null) return HResult.Pointer;
    *pfEaten = 0;

    // 0. Kiểm tra phím tắt chuyển đổi chế độ V/E (Ctrl + Shift + Q hoặc Alt + Z)
    if (KeyInputTranslator.IsToggleHotkeyPressed(wParam, lParam))
    {
        *pfEaten = 1;
        return HResult.Ok;
    }
    ...
}

[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
{
    if (pfEaten == null) return HResult.Pointer;
    *pfEaten = 0;

    // 0. Bắt phím tắt chuyển đổi chế độ V/E (Ctrl + Shift + Q hoặc Alt + Z)
    if (KeyInputTranslator.IsToggleHotkeyPressed(wParam, lParam))
    {
        bool newMode = BridgeStateManager.ToggleVietnameseMode();
        LangBarItemButton.NotifyStateChanged();
        DebugLog.Write($"OnKeyDown ToggleHotkey triggered! New IsVietnameseMode={newMode}");
        *pfEaten = 1;
        return HResult.Ok;
    }
    ...
}
```

### 4.3. Bắt Phím tắt Chuẩn TSF qua `PreserveKey` và `OnPreservedKey`

Để đảm bảo phím tắt hoạt động độc lập 100% không phụ thuộc vào trạng thái hàng đợi thông điệp của cửa sổ ứng dụng:
1. Trong `KeyEventSinkHelper.cs`: Đăng ký các phím tắt (`Ctrl + Shift + Q`, `Alt + Z`, `Ctrl + Space`, `Ctrl + Shift` KeyUp) với `ITfKeystrokeMgr::PreserveKey`.
2. Trong `KeyEventSinkImpl.cs`: Callback `OnPreservedKey` được Windows TSF gọi trực tiếp khi người dùng bấm phím tắt:

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnPreservedKey(IntPtr thisPtr, IntPtr pic, Guid* rguid, int* pfEaten)
{
    DebugLog.Write($"OnPreservedKey ENTER rguid={(rguid != null ? (*rguid).ToString() : "null")}");
    if (pfEaten == null) return HResult.Pointer;
    *pfEaten = 0;

    if (rguid != null && *rguid == Guids.GuidPreservedKeyToggle)
    {
        bool newMode = BridgeStateManager.ToggleVietnameseMode();
        LangBarItemButton.NotifyStateChanged();
        DebugLog.Write($"OnPreservedKey Toggle triggered! New IsVietnameseMode={newMode}");
        *pfEaten = 1;
        return HResult.Ok;
    }

    return HResult.Ok;
}
```

---

## 5. Phân tích Nguyên nhân & Cơ chế Lỗi Thực tế (Root Cause Analysis)

Qua quá trình chạy kiểm thử thực tế và phân tích chi tiết log thời gian thực (`BambooMintKey_Runtime.log`), phát hiện hai nguyên nhân kỹ thuật cốt lõi sau:

### 5.1. Nguyên nhân Lỗi 1: Click chuột trực tiếp vào Icon E/V thường chỉ đổi được đúng 1 lần

#### Cơ chế của Windows TSF đối với `HICON`:
* Theo quy định kỹ thuật của Microsoft Windows SDK cho interface `ITfLangBarItemButton::GetIcon`:
  > *"phIcon: [out] Pointer to an HICON value that receives the icon handle. **The caller is responsible for destroying this icon when it is no longer required**."*
* Shell của Windows (Taskbar Explorer) là bên gọi (`caller`). Sau khi nhận `HICON` và vẽ icon lên khay hệ thống, **Windows tự động gọi `DestroyIcon(hIcon)`** để giải phóng tài nguyên GDI.

#### Nguyên nhân gây lỗi:
1. Ban đầu ở trạng thái **V**: `GetIcon` cấp phát `_hIconV` (ví dụ con trỏ `0x1000`). Windows nhận `0x1000`, vẽ chữ V, sau đó Windows gọi `DestroyIcon(0x1000)`. Con trỏ `0x1000` **đã bị hủy hoàn toàn trong bảng GDI của Windows**!
2. Người dùng click lần 1 (đổi sang **E**): `GetIcon` cấp phát `_hIconE` (ví dụ `0x2000`). Windows nhận `0x2000`, vẽ chữ E, sau đó gọi `DestroyIcon(0x2000)`.
3. Người dùng click lần 2 (muốn đổi lại **V**): `GetIcon` thấy `_hIconV != IntPtr.Zero`, nên **trả lại con trỏ cũ `0x1000`**!
4. Do `0x1000` đã bị Windows hủy ở bước 1, Windows gặp lỗi GDI Handle không hợp lệ (`ERROR_INVALID_HANDLE`) nên **không thể vẽ lại icon, icon bị đơ/đứng hình hoặc biến mất**!
5. **Giải pháp khắc phục:** Không lưu cache vĩnh viễn con trỏ `HICON`. Mỗi lần Windows gọi `GetIcon`, hàm phải tạo mới một `HICON` tươi (`IconHelper.CreateBambooIcon(...)`) để trao quyền sở hữu cho Windows giải phóng, hoặc tạo bản sao độc lập.

---

### 5.2. Nguyên nhân Lỗi 2: Phím tắt `Ctrl + Shift + Q` không đổi được E/V

#### Phân tích luồng bắt phím từ Runtime Log:
Log thực tế ghi nhận khi người dùng bấm `Ctrl + Shift + Q`:
```text
[15:10:31.221] OnKeyDown ENTER vk=17  (VK_CONTROL)
[15:10:31.781] OnKeyDown ENTER vk=16  (VK_SHIFT)
[15:10:31.956] OnKeyDown ENTER vk=81  (VK_Q)
[15:10:31.957] RequestEdit: action=UpdateText, text=Q
[15:10:31.964] OnKeyDown ProcessKey char=Q, text=Q
```
Khi mã phím `vk=81` ('Q') vào `OnKeyDown`, hệ thống gõ ra chữ **'Q'** thay vì kích hoạt phím tắt vì những lý do sau:

1. **Trạng thái Modifier trong TSF Message Pump không đồng bộ:**
   * `KeyInputTranslator` dùng `GetKeyState` và `GetAsyncKeyState`.
   * `GetKeyState` kiểm tra trạng thái phím trong hàng đợi thông điệp (`message queue`) của thread hiện tại.
   * `ToUnicode` tại thời điểm `vk=81` trả về ký tự in hoa `'Q'`. Điều này chứng minh tại thời điểm phím `Q` được gửi đến `OnKeyDown`, cờ `VK_CONTROL` trong thread message queue đã là `0` (không còn được tính là đang giữ `Ctrl`).
2. **Xung đột phím hệ thống Windows (`Ctrl + Shift`):**
   * Trong Windows 10 & 11, phím tắt mặc định toàn hệ thống để chuyển đổi ngôn ngữ nhập liệu (Input Language Switching) chính là **`Ctrl + Shift`** (hoặc `Left Alt + Shift`).
   * Khi người dùng ấn giữ `Ctrl` rồi ấn tiếp `Shift`, Windows Shell lập tức đánh chặn tổ hợp này trước khi người dùng kịp ấn đến `Q`, khiến chuỗi trạng thái phím bị nuốt hoặc làm mất trạng thái modifier của thread.
3. **Quy tắc nuốt phím của TSF (`OnTestKeyDown` -> `OnKeyDown`):**
   * Trong Windows TSF, nếu `ITfKeyEventSink::OnTestKeyDown` trả về `*pfEaten = 0`, Windows coi như IME không xử lý phím đó và **sẽ không gọi `OnKeyDown`**, mà gửi thẳng `WM_KEYDOWN` đến ứng dụng.
   * Khi đang ở chế độ Tiếng Anh (`E`), nếu `IsToggleHotkeyPressed` trả về `false` trong `OnTestKeyDown`, `*pfEaten` bị gán bằng `0`. Sau đó `OnKeyDown` không bao giờ được gọi nữa, khiến người dùng **không thể chuyển từ E ngược lại V**.
4. **Giải pháp chuẩn TSF (Preserved Key):**
   * Trong TSF, chuẩn để đăng ký phím tắt IME là dùng API **`ITfKeystrokeMgr::PreserveKey`** và callback **`ITfKeyEventSink::OnPreservedKey`**.
   * Khi đăng ký qua `PreserveKey`, Windows TSF sẽ chịu trách nhiệm giám sát toàn cục tổ hợp phím và kích hoạt trực tiếp `OnPreservedKey`, khắc phục triệt để việc sai lệch trạng thái phím modifier do hàng đợi thông điệp.

---

### 5.3. Nguyên nhân Lỗi 3: Icon hiển thị E nhưng vẫn gõ ra dấu tiếng Việt (Lệch pha giữa Icon và Bộ gõ)

#### Cơ chế bảo mật Sandbox của Windows (Low Integrity & AppContainer):
* Các ứng dụng hiện đại như **Google Chrome, Microsoft Edge, Electron (Antigravity IDE, VS Code, Discord, Slack)** và **UWP/XAML Input Host** chạy ở mức đặc quyền **Low Integrity** hoặc bên trong môi trường cách ly **AppContainer**.
* Khi tạo Named File Mapping (`Local\BambooMintKey_SharedConfig_v1`) và Named Event với `lpFileMappingAttributes = IntPtr.Zero`, Windows gán DACL mặc định của tiến trình tạo (Medium Integrity).
* Hậu quả: Khi ứng dụng sandbox/AppContainer cố gắng mở vùng nhớ dùng chung, Windows trả về mã lỗi **`ERROR_ACCESS_DENIED` (5)**. Con trỏ `_pShared` bị `null`, khiến ứng dụng đó luôn fallback về giá trị mặc định (`IsVietnameseMode = true`), hoàn toàn không thấy được lệnh chuyển sang `E` từ Taskbar!
* Ngoài ra, trong `BridgeStateManager.ProcessKey`, tham số truyền vào hàm `TelexEngine.processKey` trước đây sử dụng trường tĩnh `_currentConfig` thay vì property `Config` có đồng bộ trạng thái.

#### Giải pháp khắc phục:
1. **Thiết lập Universal SDDL:** Sử dụng chuỗi SDDL chuẩn Win32:
   ```csharp
   "D:(A;;GA;;;WD)(A;;GA;;;AC)S:(ML;;NW;;;LW)"
   ```
   Cấp quyền `Generic All` cho Everyone (`WD`), ALL APPLICATION PACKAGES (`AC` - AppContainer) và gán nhãn toàn vẹn Low Integrity (`LW`). Nhờ đó, 100% ứng dụng sandbox đều có thể đọc/ghi vùng nhớ chia sẻ mà không bị Access Denied.
2. **Fallback an toàn:** Khi `_pShared == null`, sử dụng biến tĩnh `_fallbackVietnameseMode` cho phép đảo trạng thái cục bộ thay vì cố định luôn là `true`.
3. **Đồng bộ Config trong Engine:** Đổi tất cả các lệnh gọi `ProcessKey`, `ProcessBackspace`, `ProcessWordBreak` sang sử dụng trực tiếp `Config`.

#### Mã nguồn tham chiếu `SharedMemoryManager.cs`

```csharp
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.Common;

/// <summary>
/// Quản lý vùng nhớ dùng chung liên tiến trình (Cross-Process Shared Memory) qua Win32 Named File Mapping.
/// Đảm bảo trạng thái gõ tiếng Việt (V/E) và cấu hình engine đồng bộ tức thì (0 microseconds)
/// giữa taskbar (ctfmon/explorer) và tất cả ứng dụng đang gõ (Notepad, Word, Browser, VS Code,...).
/// </summary>
public static unsafe class SharedMemoryManager
{
    private const string MapName = @"Local\BambooMintKey_SharedConfig_v1";
    private const string EventName = @"Local\BambooMintKey_StateChangedEvent_v1";
    // Universal SDDL cho phép Everyone (WD), ALL APPLICATION PACKAGES/AppContainer (AC) và Low Integrity (LW)
    private const string UniversalSddl = "D:(A;;GA;;;WD)(A;;GA;;;AC)S:(ML;;NW;;;LW)";
    private const uint PageReadWrite = 0x04;
    private const uint FileMapWrite = 0x02;
    private const int SharedSize = 64;

    private static IntPtr _hMap = IntPtr.Zero;
    private static IntPtr _hEvent = IntPtr.Zero;
    private static byte* _pShared = null;
    private static bool _fallbackVietnameseMode = true;
    private static readonly object _initLock = new();

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bInheritHandle;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string StringSecurityDescriptor,
        uint StringSDRevision,
        out IntPtr SecurityDescriptor,
        IntPtr SecurityDescriptorSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenFileMappingW(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        string lpName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEventW(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileMappingW(
        IntPtr hFile,
        IntPtr lpFileMappingAttributes,
        uint flProtect,
        uint dwMaximumSizeHigh,
        uint dwMaximumSizeLow,
        string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* MapViewOfFile(
        IntPtr hFileMappingObject,
        uint dwDesiredAccess,
        uint dwFileOffsetHigh,
        uint dwFileOffsetLow,
        nuint dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(void* lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    static SharedMemoryManager()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Khởi tạo hoặc kết nối vào vùng nhớ FileMapping chung của phiên người dùng.
    /// Hỗ trợ cả ứng dụng thường, Chromium sandbox (Low Integrity) và UWP (AppContainer).
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_pShared != null) return;

        lock (_initLock)
        {
            if (_pShared != null) return;

            SECURITY_ATTRIBUTES sa = new();
            sa.nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
            sa.bInheritHandle = false;

            IntPtr pSd = IntPtr.Zero;
            bool hasSd = ConvertStringSecurityDescriptorToSecurityDescriptorW(UniversalSddl, 1, out pSd, IntPtr.Zero);
            if (hasSd && pSd != IntPtr.Zero)
            {
                sa.lpSecurityDescriptor = pSd;
            }

            try
            {
                IntPtr pSaPtr = (hasSd && pSd != IntPtr.Zero) ? (IntPtr)(&sa) : IntPtr.Zero;
                _hMap = CreateFileMappingW(new IntPtr(-1), pSaPtr, PageReadWrite, 0, SharedSize, MapName);

                if (_hMap == IntPtr.Zero)
                {
                    _hMap = OpenFileMappingW(FileMapWrite, false, MapName);
                }

                if (_hMap != IntPtr.Zero)
                {
                    bool isCreator = (Marshal.GetLastWin32Error() == 0);
                    void* pView = MapViewOfFile(_hMap, FileMapWrite, 0, 0, SharedSize);
                    if (pView != null)
                    {
                        _pShared = (byte*)pView;

                        // Nếu là tiến trình đầu tiên tạo ra map, khởi tạo giá trị mặc định (Bật Tiếng Việt)
                        if (isCreator)
                        {
                            _pShared[0] = 1; // 1 = IsVietnameseMode On (V)
                            _pShared[1] = 0; // 0 = ToneStyle New
                            _pShared[2] = 1; // AutoRestoreEnglishWords
                            _pShared[3] = 1; // AllowRepeatKeyUndo
                            _pShared[4] = 0; // AllowLeadingWAsU
                        }
                    }
                }

                if (_hEvent == IntPtr.Zero)
                {
                    _hEvent = CreateEventW(pSaPtr, false /* AutoReset */, false, EventName);
                }
            }
            finally
            {
                if (pSd != IntPtr.Zero)
                {
                    LocalFree(pSd);
                }
            }
        }
    }

    /// <summary>Handle của Win32 Event đồng bộ trạng thái V/E.</summary>
    public static IntPtr StateChangedEventHandle
    {
        get
        {
            EnsureInitialized();
            return _hEvent;
        }
    }

    /// <summary>Phát tín hiệu cho tất cả tiến trình khác biết cấu hình đã thay đổi.</summary>
    public static void SignalStateChanged()
    {
        if (_hEvent != IntPtr.Zero)
        {
            SetEvent(_hEvent);
        }
    }

    /// <summary>
    /// Trạng thái bật/tắt gõ tiếng Việt đồng bộ xuyên suốt mọi tiến trình người dùng.
    /// true = V (Tiếng Việt), false = E (Tiếng Anh).
    /// </summary>
    public static bool IsVietnameseMode
    {
        get
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                return _pShared[0] != 0;
            }
            return _fallbackVietnameseMode;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[0] = (byte)(value ? 1 : 0);
                SignalStateChanged();
            }
            else
            {
                _fallbackVietnameseMode = value;
            }
        }
    }

    /// <summary>
    /// Đảo trạng thái V/E và trả về giá trị mới.
    /// </summary>
    public static bool ToggleVietnameseMode()
    {
        EnsureInitialized();
        if (_pShared != null)
        {
            byte current = _pShared[0];
            byte next = (byte)(current == 0 ? 1 : 0);
            _pShared[0] = next;
            SignalStateChanged();
            return next != 0;
        }
        _fallbackVietnameseMode = !_fallbackVietnameseMode;
        return _fallbackVietnameseMode;
    }
}
```

#### Mã nguồn tham chiếu `BridgeStateManager.cs`

```csharp
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using BambooMintKey.Core.Domain;
using BambooMintKey.Core.Engine;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cầu nối in-memory giữa TSF COM server và F# Pure Telex Engine.
/// Duy trì WordState hiện tại và điều phối các lệnh gọi đến TelexEngine.processKey.
/// </summary>
public static class BridgeStateManager
{
    private static Types.WordState _currentState = Types.WordState.Empty;
    private static EngineConfig.EngineConfig _currentConfig = EngineConfig.EngineConfig.Default;

    /// <summary>Trạng thái word hiện tại của engine.</summary>
    public static Types.WordState CurrentState => _currentState;

    /// <summary>Cấu hình engine hiện tại (đồng bộ trạng thái IsEnabled với SharedMemoryManager).</summary>
    public static EngineConfig.EngineConfig Config
    {
        get
        {
            bool isVn = SharedMemoryManager.IsVietnameseMode;
            if (_currentConfig.IsEnabled != isVn)
            {
                _currentConfig = new EngineConfig.EngineConfig(
                    isVn,
                    _currentConfig.AutoRestoreEnglishWords,
                    _currentConfig.AllowRepeatKeyUndo,
                    _currentConfig.AllowLeadingWAsU,
                    _currentConfig.ToneStyle
                );
            }
            return _currentConfig;
        }
    }

    /// <summary>Kiểm tra xem chế độ gõ tiếng Việt hiện đang bật (V) hay tắt (E) qua Shared Memory.</summary>
    public static bool IsVietnameseMode
    {
        get => SharedMemoryManager.IsVietnameseMode;
        set => SharedMemoryManager.IsVietnameseMode = value;
    }

    /// <summary>Đảo trạng thái gõ tiếng Việt / tiếng Anh trong Shared Memory và trả về trạng thái mới.</summary>
    public static bool ToggleVietnameseMode()
    {
        bool newMode = SharedMemoryManager.ToggleVietnameseMode();
        _currentConfig = new EngineConfig.EngineConfig(
            newMode,
            _currentConfig.AutoRestoreEnglishWords,
            _currentConfig.AllowRepeatKeyUndo,
            _currentConfig.AllowLeadingWAsU,
            _currentConfig.ToneStyle
        );
        return newMode;
    }

    /// <summary>Khởi tạo lại engine state về empty (KHÔNG đè trạng thái IsEnabled của người dùng).</summary>
    public static void InitializeEngine()
    {
        _currentState = Types.WordState.Empty;
    }

    /// <summary>Reset state về empty (dùng khi chuyển focus hoặc composition kết thúc).</summary>
    public static void ResetState()
    {
        _currentState = Types.WordState.Empty;
    }

    /// <summary>Xử lý một ký tự bàn phím thông thường.</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessKey(char c)
    {
        var input = Types.KeyInput.NewChar(c);
        var result = TelexEngine.processKey(_currentState, input, Config);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    /// <summary>Xử lý phím Backspace.</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessBackspace()
    {
        var input = Types.KeyInput.Backspace;
        var result = TelexEngine.processKey(_currentState, input, Config);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    /// <summary>Xử lý ký tự ngắt từ (space, dấu câu, ...).</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessWordBreak(char breakChar)
    {
        var input = Types.KeyInput.NewWordBreak(breakChar);
        var result = TelexEngine.processKey(_currentState, input, Config);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }
}
```

---

### 5.4. Nguyên nhân Lỗi 4: Sau vài lần tắt bật thì không đổi được chữ (Kẹt Event Đồng bộ Đa tiến trình)

#### Cơ chế tranh chấp Win32 AutoReset Event:
* Ban đầu, `SharedMemoryManager` sử dụng một Win32 Named Event kiểu **AutoReset** (`bManualReset = false`).
* Khi có nhiều tiến trình cùng chạy (Explorer, ctfmon, Notepad, Chrome, VS Code...), mỗi tiến trình đều có một thread chạy `WaitForSingleObject(_hEvent, INFINITE)`.
* Khi một tiến trình gọi `SetEvent(_hEvent)`, Win32 AutoReset Event **chỉ đánh thức duy nhất 1 thread của 1 tiến trình bất kỳ** rồi tự động chuyển về trạng thái `non-signaled`.
* Nếu một tiến trình nền (ví dụ Notepad) "nhặt" mất tín hiệu này, thì tiến trình Taskbar của Windows (`explorer.exe`) sẽ **vẫn tiếp tục ngủ (block vĩnh viễn)**, khiến Taskbar không hề biết cấu hình đã đổi và không gọi `OnUpdate` để vẽ lại icon!

#### Giải pháp khắc phục:
* Trong `LangBarItemButton.StartEventListener`, chuyển sang cơ chế kiểm tra định kỳ (polling) với timeout 100ms:
  ```csharp
  private static void StartEventListener()
  {
      var thread = new System.Threading.Thread(() =>
      {
          IntPtr hEv = SharedMemoryManager.StateChangedEventHandle;
          bool lastMode = BridgeStateManager.IsVietnameseMode;

          while (true)
          {
              // Chờ event tối đa 100ms
              if (hEv != IntPtr.Zero)
              {
                  SharedMemoryManager.WaitForSingleObject(hEv, 100);
              }
              else
              {
                  System.Threading.Thread.Sleep(100);
              }

              // Kiểm tra trạng thái thực tế trong Shared Memory để luôn đồng bộ Taskbar
              bool currentMode = BridgeStateManager.IsVietnameseMode;
              if (currentMode != lastMode)
              {
                  lastMode = currentMode;
                  NotifyStateChanged();
              }
          }
      })
      {
          IsBackground = true,
          Name = "BambooMintKey_StateEventListener"
      };
      thread.Start();
  }
  ```
* Dù Win32 Event có bị tiến trình khác nhận mất, thread của Taskbar vẫn thức dậy sau tối đa 100ms, đọc trực tiếp byte trạng thái từ RAM trong Shared Memory và cập nhật icon ngay lập tức. Đảm bảo icon Taskbar không bao giờ bị kẹt hay mất đồng bộ.

---

## 6. Kế hoạch Kiểm thử & Nghiệm thu (Verification Matrix)

| Bước kiểm thử | Thao tác thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- |
| **1. DevHarness Test** | Chạy `dotnet run --project src/BambooMintKey.DevHarness` | `CreateBambooIcon("V")` và `"E"` trả về `HICON != IntPtr.Zero`. `GetIcon` phản hồi đúng icon theo trạng thái. Không rò rỉ bộ nhớ. |
| **2. NativeAOT Build** | Chạy `pwsh scripts/build-native.ps1` | Biên dịch ra DLL `publish/win-x64/BambooMintKey.dll` thành công không cảnh báo calling convention hay AOT trim. |
| **3. Hiển thị Icon Taskbar** | Đăng ký TIP, chuyển sang BambooMintKey bằng `Win + Space` | Khay Taskbar xuất hiện ngay lập tức icon hình vuông nền xanh lá cây bo góc có chữ **V** màu trắng ngà cạnh chữ `VIE`. |
| **4. Click Chuột Đổi Icon** | Click chuột trái vào icon chữ **V** | Icon chuyển tức thì sang chữ **E** (nền xanh lá, chữ E màu trắng), tooltip chuyển thành `"BambooMintKey: English"`. Click tiếp đổi lại chữ **V** liên tục không bị đơ. |
| **5. Phím tắt Toggle** | Bấm `Ctrl + Shift + Q` hoặc `Alt + Z` | Icon trên Taskbar tự động đổi giữa **V** $\leftrightarrow$ **E**. Chế độ gõ tiếng Việt tắt/bật đồng bộ. |
| **6. GDI Leak Check** | Dùng Task Manager (cột GDI Objects) click toggle 50 lần | Số lượng GDI Objects của tiến trình không tăng lũy tiến (đạt tiêu chuẩn 0 GDI Leak). |