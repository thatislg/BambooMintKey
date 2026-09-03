# 003_04_IconHelper_DynamicRendering.md

> Tài liệu kỹ thuật chi tiết về cơ chế vẽ `HICON` động qua Win32 GDI trong bộ nhớ, tạo icon nền xanh lá bo góc mang nhận diện thương hiệu BambooMintKey, xử lý trong suốt (transparency mask), quản lý bộ nhớ đệm cache, đồng bộ trạng thái Taskbar qua TSF Compartment và phím tắt chuyển chế độ (`Ctrl + Shift + Q` / `Alt + Z`).
> 
> Đã triển khai theo: `003_09_IssuesSolution.md`, `009_10_DelayOnMouseChangeDelaySolution.md`.

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
* **Màu nền (Background):** Xanh lá cây Bamboo `#16a34a` (RGB: 22, 163, 74 → Win32 COLORREF: `0x004AA316`).
* **Màu viền (Border):** Xanh mint nhạt `#86efac` (RGB: 134, 239, 172 → Win32 COLORREF: `0x00ACEF86`).
* **Màu chữ (Text/Glyph):** Trắng ngà `#fbf8f9` (RGB: 251, 248, 249 → Win32 COLORREF: `0x00F9F8FB`).
* **Hình dạng:** Khung hình chữ nhật bo tròn 4 góc (Rounded Rectangle).

### 1.3. Cơ chế Khử Răng cưa & Độ trong suốt (Win32 Masking Architecture)
Một Win32 `HICON` chuẩn được tạo thông qua `CreateIconIndirect` với cấu trúc `ICONINFO`:
* `hbmColor`: Bitmap 32-bit màu RGB chứa hình vẽ nút (nền xanh `#16a34a`, viền mint `#86efac`, chữ trắng đậm).
* `hbmMask`: Bitmap đơn sắc 1-bit monochrome:
  * Bit = `0` (Màu đen trong mask): **Opaque** → Điểm ảnh tương ứng trong `hbmColor` được vẽ đè lên màn hình.
  * Bit = `1` (Màu trắng trong mask): **Transparent** → Điểm ảnh trên màn hình được giữ nguyên (trong suốt).
* **Xử lý 4 góc bo tròn:**
  * Trong `hbmMask`, ta quét toàn bộ nền bằng màu trắng (`1` - trong suốt), sau đó vẽ một hình chữ nhật bo góc bằng màu đen (`0` - đục).
  * Nhờ vậy, 4 góc bên ngoài hình bo tròn sẽ hoàn toàn trong suốt, hiển thị liền mạch trên cả giao diện Taskbar Dark Theme lẫn Light Theme của Windows.

### 1.4. Hỗ trợ DPI Động (`SM_CXSMICON` / `SM_CYSMICON`)
Thay vì cố định cứng 16x16 pixel (sẽ bị mờ hoặc vỡ nét trên màn hình 125%, 150%, 200%), `IconHelper` truy vấn kích thước icon khay hệ thống thời gian thực:
* `GetSystemMetrics(SmCxSmIcon = 49)` và `GetSystemMetrics(SmCySmIcon = 50)`.
* Tự động tính toán kích cỡ font chữ và bán kính bo góc tỷ lệ theo kích thước DPI.

### 1.5. Chiến lược Cache tĩnh + `CopyIcon`
Thay vì tạo mới toàn bộ HICON mỗi lần Windows gọi `GetIcon` (gây áp lực GDI handle pool và flicker), giải pháp hiện tại:
* Giữ sẵn **2 HICON mẫu** `_cachedIconV` và `_cachedIconE` theo DPI hiện tại.
* Khi Windows gọi `GetIcon`, trả về `CopyIcon(_cachedIconX)` — bản sao độc lập mà Windows tự do `DestroyIcon`.
* Cache chỉ được vẽ lại khi DPI thay đổi hoặc lần đầu gọi.

---

## 2. Đặc tả Chi tiết `IconHelper.cs`

Mã nguồn tại [`src/BambooMintKey.NativeBridge/TSF/IconHelper.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/IconHelper.cs):

```csharp
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
using System.Text;
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

    /// <summary>Đếm số lần tạo HICON để debug leak / race condition.</summary>
    public static long CreationCount = 0;

    /// <summary>Số lần tạo HICON thất bại (trả về NULL).</summary>
    public static long FailureCount = 0;

    /// <summary>Lỗi Win32 lần thất bại gần nhất.</summary>
    public static int LastWin32Error = 0;

    // =========================================================================
    // Win32 GDI & User32 P/Invoke
    // =========================================================================

    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(uint dwErrCode);

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

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern IntPtr CopyIcon(IntPtr hIcon);

    private static IntPtr _cachedIconV = IntPtr.Zero;
    private static IntPtr _cachedIconE = IntPtr.Zero;
    private static int _cachedWidth = 0;
    private static int _cachedHeight = 0;
    private static readonly object _cacheLock = new();

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
    /// Cung cấp một bản sao HICON độc lập cho Windows Taskbar từ cache tĩnh.
    /// Tránh cấp phát GDI liên tục và triệt tiêu flicker.
    /// </summary>
    public static IntPtr GetBambooIconHandle(string text)
    {
        var (w, h) = GetTrayIconMetrics();
        lock (_cacheLock)
        {
            if (_cachedIconV == IntPtr.Zero || _cachedIconE == IntPtr.Zero || _cachedWidth != w || _cachedHeight != h)
            {
                if (_cachedIconV != IntPtr.Zero) DestroyIcon(_cachedIconV);
                if (_cachedIconE != IntPtr.Zero) DestroyIcon(_cachedIconE);

                _cachedIconV = CreateBambooIcon("V", w, h);
                _cachedIconE = CreateBambooIcon("E", w, h);
                _cachedWidth = w;
                _cachedHeight = h;
            }

            IntPtr source = (text == "V") ? _cachedIconV : _cachedIconE;
            if (source != IntPtr.Zero)
            {
                IntPtr copy = CopyIcon(source);
                if (copy != IntPtr.Zero)
                {
                    return copy;
                }
            }
        }

        return CreateBambooIcon(text, w, h);
    }

    /// <summary>
    /// Tạo một HICON động chứa ký tự text ("V" hoặc "E") với nền xanh lá bo góc BambooMintKey.
    /// </summary>
    /// <param name="text">Ký tự cần vẽ ("V" hoặc "E")</param>
    /// <param name="width">Chiều rộng icon (0 = tự nhận theo DPI)</param>
    /// <param name="height">Chiều cao icon (0 = tự nhận theo DPI)</param>
    /// <returns>IntPtr trỏ tới HICON hợp lệ (cần được giải phóng bằng DestroyIcon khi tắt app)</returns>
    public static IntPtr CreateBambooIcon(string text, int width = 0, int height = 0)
    {
        long seq = System.Threading.Interlocked.Increment(ref CreationCount);
        var sb = new StringBuilder();
        sb.AppendLine($"[ICON {seq}] CreateBambooIcon ENTER text='{text}', requested={width}x{height}");

        if (width <= 0 || height <= 0)
        {
            var metrics = GetTrayIconMetrics();
            width = metrics.width;
            height = metrics.height;
        }

        int cornerRadius = Math.Max(4, width / 4);
        sb.AppendLine($"[ICON {seq}] Will draw at {width}x{height}, cornerRadius={cornerRadius}");

        IntPtr hScreenDC = GetDC(IntPtr.Zero);
        sb.AppendLine($"[ICON {seq}] GetDC(0)={hScreenDC}");

        // ---------------------------------------------------------------------
        // 1. Tạo Color Bitmap (Nền xanh lá #16a34a, viền mint #86efac, chữ trắng)
        // ---------------------------------------------------------------------
        IntPtr hColorDC = CreateCompatibleDC(hScreenDC);
        IntPtr hColorBmp = CreateCompatibleBitmap(hScreenDC, width, height);
        IntPtr hOldColorBmp = SelectObject(hColorDC, hColorBmp);
        sb.AppendLine($"[ICON {seq}] Color DC={hColorDC}, BMP={hColorBmp}, OldBMP={hOldColorBmp}");

        // Tạo Brush nền xanh và Pen viền mint
        IntPtr hBrushBg = CreateSolidBrush(ColorBackground);
        IntPtr hPenBorder = CreatePen(0 /* PS_SOLID */, 1, ColorBorder);
        IntPtr hOldBrush = SelectObject(hColorDC, hBrushBg);
        IntPtr hOldPen = SelectObject(hColorDC, hPenBorder);
        sb.AppendLine($"[ICON {seq}] BrushBg={hBrushBg}, PenBorder={hPenBorder}, OldBrush={hOldBrush}, OldPen={hOldPen}");

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
        sb.AppendLine($"[ICON {seq}] Font={hFont}, OldFont={hOldFont}, fontHeight={fontHeight}");

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
        sb.AppendLine($"[ICON {seq}] Mask DC={hMaskDC}, BMP={hMaskBmp}, OldBMP={hOldMaskBmp}");

        // Phủ toàn bộ Mask màu trắng (0x00FFFFFF -> Trong suốt hoàn toàn)
        IntPtr hBrushWhite = CreateSolidBrush(0x00FFFFFF);
        IntPtr hPenWhite = CreatePen(0, 1, 0x00FFFFFF);
        IntPtr hOldMaskBrush = SelectObject(hMaskDC, hBrushWhite);
        IntPtr hOldMaskPen = SelectObject(hMaskDC, hPenWhite);
        sb.AppendLine($"[ICON {seq}] BrushWhite={hBrushWhite}, PenWhite={hPenWhite}, OldBrush={hOldMaskBrush}, OldPen={hOldMaskPen}");
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

        SetLastError(0);
        IntPtr hIcon = CreateIconIndirect(ref iconInfo);
        uint err = GetLastError();
        sb.AppendLine($"[ICON {seq}] CreateIconIndirect hIcon={hIcon}, GetLastError={err}");

        if (hIcon == IntPtr.Zero)
        {
            System.Threading.Interlocked.Increment(ref FailureCount);
            LastWin32Error = (int)err;
        }

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

        sb.AppendLine($"[ICON {seq}] CreateBambooIcon EXIT hIcon={hIcon}");
        DebugLog.Write(sb.ToString());

        return hIcon;
    }
}
```

---

## 3. Quản lý Hiển thị Icon & Đồng bộ trong `LangBarItemButton.cs`

Mã nguồn tại [`src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs):

### 3.1. Trường dữ liệu & Khởi tạo

```csharp
public static unsafe class LangBarItemButton
{
    private static ITfLangBarItemButtonVTable* _buttonVTable;
    private static TfSourceVTable* _sourceVTable;
    private static IntPtr _comInstance;

    // Con trỏ tới ITfLangBarItemSink mà Windows cung cấp qua ITfSource::AdviseSink
    private static volatile IntPtr _pLangBarSink = IntPtr.Zero;
    private static uint _sinkCookie = 0;
    private static IntPtr _langBarMgr = IntPtr.Zero;
    private static readonly object _sinkLock = new();
    private static IntPtr _pThreadMgr = IntPtr.Zero;
    private static uint _clientId = 0;

    private static bool _listenerStarted = false;

    static LangBarItemButton()
    {
        InitializeVTables();

        // Cấp phát vùng nhớ Native Layout kép (Slot 0: Button, Slot 1: Source)
        var layout = (LangBarButtonNativeLayout*)NativeMemory.Alloc((nuint)sizeof(LangBarButtonNativeLayout));
        layout->VTableButton = (IntPtr)_buttonVTable;
        layout->VTableSource = (IntPtr)_sourceVTable;
        _comInstance = (IntPtr)layout;
    }

    /// <summary>Con trỏ COM Instance của LangBarItemButton.</summary>
    public static IntPtr Instance => _comInstance;
}
```

### 3.2. Cung cấp Icon qua `ITfLangBarItemButton::GetIcon`

```csharp
/// <summary>[WinSDK: ITfLangBarItemButton::GetIcon] - Cung cấp con trỏ HICON để Windows vẽ icon Taskbar.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetIcon(IntPtr thisPtr, IntPtr* phIcon)
{
    if (phIcon == null) return HResult.InvalidArgument;

    // Cung cấp bản sao HICON độc lập từ cache tĩnh qua IconHelper.GetBambooIconHandle.
    // Windows Taskbar Shell sẽ tự động gọi DestroyIcon sau khi vẽ.
    string text = BridgeStateManager.IsVietnameseMode ? "V" : "E";
    DebugLog.Write($"LangBarItemButton.GetIcon ENTER requested='{text}', IsVietnameseMode={BridgeStateManager.IsVietnameseMode}, thread={Environment.CurrentManagedThreadId}");
    *phIcon = IconHelper.GetBambooIconHandle(text);
    DebugLog.Write($"LangBarItemButton.GetIcon EXIT text='{text}' -> {*phIcon}");

    return HResult.Ok;
}
```

### 3.3. Cấu hình Nút (`GetInfo`) dùng Toggle Button

```csharp
/// <summary>[WinSDK: ITfLangBarItem::GetInfo] - Cung cấp thông tin cấu hình nút cho Windows.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int GetInfo(IntPtr thisPtr, TF_LANGBARITEMINFO* pInfo)
{
    if (pInfo == null) return HResult.InvalidArgument;

    pInfo->clsidService = Guids.TextServiceClsid;
    pInfo->guidItem = Guids.GuidLbiInputMode;
    // Dùng TfLbiStyleBtnToggle | TfLbiStyleShownInTray để Taskbar xử lý đảo trạng thái hai chiều tức thì
    pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnToggle |
                     TsfLangBarFlags.TfLbiStyleShownInTray;
    pInfo->ulSort = 0;

    string desc = "BambooMintKey Mode";
    fixed (char* src = desc)
    {
        for (int i = 0; i < desc.Length && i < 31; i++)
        {
            pInfo->szDescription[i] = src[i];
        }
        pInfo->szDescription[Math.Min(desc.Length, 31)] = '\0';
    }

    return HResult.Ok;
}
```

### 3.4. Xử lý Sự kiện Click Chuột (`OnClick`)

```csharp
/// <summary>[WinSDK: ITfLangBarItemButton::OnClick] - Xử lý sự kiện click chuột từ người dùng.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnClick(IntPtr thisPtr, uint click, POINT pt, RECT* prcArea)
{
    DebugLog.Write($"LangBarItemButton OnClick ENTER click={click}, thread={Environment.CurrentManagedThreadId}");
    // Bắt mọi click chuột trái (hoặc bất kỳ click nào không phải chuột phải)
    if (click != TsfLangBarFlags.TfLbiClkRight)
    {
        bool newMode = BridgeStateManager.ToggleVietnameseMode();

        // 1. Gửi thông báo OnUpdate tới Sink để vẽ lại Icon ngay
        NotifyStateChanged();

        // 2. Đồng bộ lập tức tới TSF Input Mode Compartment của Windows 10/11 Shell
        if (_pThreadMgr != IntPtr.Zero)
        {
            TsfCompartmentHelper.SetConversionMode(_pThreadMgr, _clientId, newMode);
        }

        DebugLog.Write($"LangBarItemButton OnClick toggled IsVietnameseMode={newMode} (Sink + Compartment synchronized)");
    }
    DebugLog.Write($"LangBarItemButton OnClick EXIT click={click}");
    return HResult.Ok;
}
```

### 3.5. Quản lý Sink An toàn (`AdviseSink` / `UnadviseSink`)

```csharp
/// <summary>[WinSDK: ITfSource::AdviseSink] - Windows gọi để trao con trỏ ITfLangBarItemSink cho bộ gõ.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int AdviseSink(IntPtr thisPtr, Guid* riid, IntPtr punk, uint* pdwCookie)
{
    DebugLog.Write($"LangBarItemButton.AdviseSink ENTER thisPtr={thisPtr}, punk={punk}, thread={Environment.CurrentManagedThreadId}");
    if (riid == null || punk == IntPtr.Zero || pdwCookie == null)
    {
        DebugLog.Write("LangBarItemButton.AdviseSink invalid args");
        return HResult.InvalidArgument;
    }

    DebugLog.Write($"LangBarItemButton.AdviseSink riid={*riid}");

    if (*riid == Guids.IidITfLangBarItemSink)
    {
        Guid iidSink = Guids.IidITfLangBarItemSink;
        IntPtr pSink = IntPtr.Zero;
        var unk = *(TfSourceVTable**)punk;
        int hrQi = unk->QueryInterface(punk, &iidSink, &pSink);
        DebugLog.Write($"LangBarItemButton.AdviseSink QI ITfLangBarItemSink hr=0x{hrQi:X8}, pSink={pSink}");

        if (hrQi == HResult.Ok && pSink != IntPtr.Zero)
        {
            lock (_sinkLock)
            {
                if (_pLangBarSink != IntPtr.Zero)
                {
                    NativeCom.Release(_pLangBarSink);
                }
                _pLangBarSink = pSink;
                _sinkCookie = 1;
                *pdwCookie = _sinkCookie;
            }
            DebugLog.Write($"LangBarItemButton.AdviseSink: ITfLangBarItemSink connected via QI pSink={pSink}");
            return HResult.Ok;
        }

        *pdwCookie = 0;
        DebugLog.Write($"LangBarItemButton.AdviseSink: QI ITfLangBarItemSink failed (0x{hrQi:X8})");
        return hrQi != HResult.Ok ? hrQi : HResult.NoInterface;
    }

    *pdwCookie = 0;
    DebugLog.Write($"LangBarItemButton.AdviseSink: unsupported riid={*riid}, returning E_INVALIDARG");
    return HResult.InvalidArgument;
}

/// <summary>[WinSDK: ITfSource::UnadviseSink] - Windows gọi để hủy đăng ký Sink khi tắt ứng dụng hoặc gỡ nút.</summary>
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int UnadviseSink(IntPtr thisPtr, uint dwCookie)
{
    DebugLog.Write($"LangBarItemButton.UnadviseSink ENTER dwCookie={dwCookie}, _sinkCookie={_sinkCookie}, _pLangBarSink={_pLangBarSink}");
    lock (_sinkLock)
    {
        if (dwCookie == _sinkCookie && _pLangBarSink != IntPtr.Zero)
        {
            NativeCom.Release(_pLangBarSink);
            _pLangBarSink = IntPtr.Zero;
            _sinkCookie = 0;
            DebugLog.Write("LangBarItemButton.UnadviseSink: ITfLangBarItemSink disconnected");
            return HResult.Ok;
        }
    }
    DebugLog.Write("LangBarItemButton.UnadviseSink: cookie mismatch or no sink");
    return HResult.InvalidArgument;
}
```

### 3.6. Gửi Thông báo Cập nhật (`NotifyStateChanged`)

```csharp
public static void NotifyStateChanged()
{
    DebugLog.Write($"LangBarItemButton.NotifyStateChanged ENTER _pLangBarSink={_pLangBarSink}, thread={Environment.CurrentManagedThreadId}");
    if (_pLangBarSink != IntPtr.Zero)
    {
        var sinkVTable = *(ITfLangBarItemSinkVTable**)_pLangBarSink;
        // [WinSDK: ITfLangBarItemSink::OnUpdate]
        int hr = sinkVTable->OnUpdate(
            _pLangBarSink,
            TsfLangBarFlags.TfLbiIcon | TsfLangBarFlags.TfLbiText | TsfLangBarFlags.TfLbiTooltip);
        DebugLog.Write($"LangBarItemButton.NotifyStateChanged: OnUpdate sent to Windows Taskbar hr=0x{hr:X8}");
    }
    else
    {
        DebugLog.Write("LangBarItemButton.NotifyStateChanged: _pLangBarSink is NULL, cannot notify");
    }
}
```

### 3.7. Đồng bộ Liên tiến trình qua `StartEventListener`

Background thread theo dõi `StateSequence` trong shared memory. Khi sequence tăng (bất kỳ tiến trình nào đổi mode), tiến trình hiện tại gọi `NotifyStateChanged()` nếu đang nắm Sink.

```csharp
private static void StartEventListener()
{
    var thread = new System.Threading.Thread(() =>
    {
        IntPtr hEv = SharedMemoryManager.StateChangedEventHandle;
        uint localSeq = SharedMemoryManager.StateSequence;
        bool lastMode = BridgeStateManager.IsVietnameseMode;
        DebugLog.Write($"StartEventListener thread started. hEv={hEv}, initialMode={lastMode}, initialSeq={localSeq}, thread={Environment.CurrentManagedThreadId}");

        while (true)
        {
            // Chờ event Manual-Reset broadcast (timeout 250ms phòng trường hợp trễ)
            if (hEv != IntPtr.Zero)
            {
                uint wr = SharedMemoryManager.WaitForSingleObject(hEv, 250);
                if (wr != 0 /* WAIT_OBJECT_0 */ && wr != 258 /* WAIT_TIMEOUT */)
                {
                    DebugLog.Write($"StartEventListener WaitForSingleObject returned unexpected {wr}, exiting loop");
                    break;
                }
            }
            else
            {
                System.Threading.Thread.Sleep(250);
            }

            // Kiểm tra StateSequence để phát hiện mọi thay đổi từ bất kỳ tiến trình nào
            uint currentSeq = SharedMemoryManager.StateSequence;
            bool currentMode = BridgeStateManager.IsVietnameseMode;

            if (currentSeq != localSeq || currentMode != lastMode)
            {
                DebugLog.Write($"StartEventListener detected change: seq {localSeq}->{currentSeq}, mode {lastMode}->{currentMode}");
                localSeq = currentSeq;
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

### 3.8. Đăng ký Nút Taskbar (`Register`)

```csharp
/// <summary>
/// Đăng ký nút Language Bar vào hệ thống thông qua ITfLangBarItemMgr.
/// </summary>
public static void Register(IntPtr pThreadMgr, uint clientId = 0)
{
    if (pThreadMgr == IntPtr.Zero)
    {
        DebugLog.Write("LangBarItemButton.Register: pThreadMgr is NULL");
        return;
    }

    _pThreadMgr = pThreadMgr;
    _clientId = clientId;

    if (!_listenerStarted)
    {
        _listenerStarted = true;
        StartEventListener();
    }

    Guid iidMgr = Guids.IidITfLangBarItemMgr;
    IntPtr pMgr = IntPtr.Zero;

    var unk = *(TfSourceVTable**)pThreadMgr;
    int hrQi = unk->QueryInterface(pThreadMgr, &iidMgr, &pMgr);
    DebugLog.Write($"LangBarItemButton.Register QI ITfLangBarItemMgr hr=0x{hrQi:X8}, pMgr={pMgr}");

    if (hrQi != HResult.Ok || pMgr == IntPtr.Zero)
    {
        // Fallback sang CoCreateInstance với CLSID_TF_LangBarItemMgr nếu pThreadMgr không hỗ trợ QI trực tiếp
        Guid clsidMgr = Guids.ClsidTfLangBarItemMgr;
        const uint CLSCTX_INPROC_SERVER = 1;
        hrQi = NativeCom.CoCreateInstance(&clsidMgr, IntPtr.Zero, CLSCTX_INPROC_SERVER, &iidMgr, &pMgr);
        DebugLog.Write($"LangBarItemButton.Register CoCreateInstance ITfLangBarItemMgr hr=0x{hrQi:X8}, pMgr={pMgr}");
    }

    if (pMgr != IntPtr.Zero)
    {
        _langBarMgr = pMgr;
        var mgrVTable = *(ITfLangBarItemMgrVTable**)_langBarMgr;

        // [WinSDK: ITfLangBarItemMgr::AddItem]
        // Windows sẽ tự gọi QI(ITfSource) -> AdviseSink trên _comInstance để trao Sink
        int hr = mgrVTable->AddItem(_langBarMgr, _comInstance);
        DebugLog.Write($"LangBarItemButton.Register AddItem result=0x{hr:X8}");
        NotifyStateChanged();
    }
    else
    {
        DebugLog.Write("LangBarItemButton.Register: Failed to obtain ITfLangBarItemMgr");
    }
}
```

---

## 4. Đồng bộ TSF Input Mode Compartment (`TsfCompartmentHelper.cs`)

Mã nguồn tại [`src/BambooMintKey.NativeBridge/TSF/TsfCompartmentHelper.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/TsfCompartmentHelper.cs):

```csharp
// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cấu trúc VARIANT Win32 dùng cho ITfCompartment::SetValue/GetValue.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct VARIANT
{
    [FieldOffset(0)]
    public ushort vt;
    [FieldOffset(2)]
    public ushort wReserved1;
    [FieldOffset(4)]
    public ushort wReserved2;
    [FieldOffset(6)]
    public ushort wReserved3;
    [FieldOffset(8)]
    public int lVal;
    [FieldOffset(8)]
    public IntPtr byref;
}

/// <summary>VTable cho ITfCompartmentMgr (msctf.h)</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfCompartmentMgrVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetCompartment;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> ClearCompartment;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumCompartments;
}

/// <summary>VTable cho ITfCompartment (msctf.h)</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfCompartmentVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, VARIANT*, int> SetValue;
    public delegate* unmanaged[Stdcall]<IntPtr, VARIANT*, int> GetValue;
}

/// <summary>
/// Trợ thủ đồng bộ trạng thái Input Mode Compartment với Windows 10/11 Taskbar Input Indicator.
/// </summary>
public static unsafe class TsfCompartmentHelper
{
    public const ushort VtI4 = 3;

    /// <summary>
    /// Đồng bộ chế độ gõ V (Conversion On = 1) hoặc E (Conversion Off = 0) vào Thread Manager Compartment.
    /// </summary>
    public static int SetConversionMode(IntPtr pThreadMgr, uint clientId, bool isVietnamese)
    {
        if (pThreadMgr == IntPtr.Zero) return HResult.InvalidArgument;

        Guid iidCompMgr = Guids.IidITfCompartmentMgr;
        IntPtr pCompMgr = IntPtr.Zero;

        var unk = *(ITfCompartmentMgrVTable**)pThreadMgr;
        int hr = unk->QueryInterface(pThreadMgr, &iidCompMgr, &pCompMgr);
        if (hr != HResult.Ok || pCompMgr == IntPtr.Zero)
        {
            return hr;
        }

        try
        {
            var compMgrVTable = *(ITfCompartmentMgrVTable**)pCompMgr;
            Guid guidConversion = Guids.GuidCompartmentKeyboardInputModeConversion;
            IntPtr pComp = IntPtr.Zero;

            hr = compMgrVTable->GetCompartment(pCompMgr, &guidConversion, &pComp);
            if (hr != HResult.Ok || pComp == IntPtr.Zero)
            {
                return hr;
            }

            try
            {
                var compVTable = *(ITfCompartmentVTable**)pComp;
                VARIANT varVal = new()
                {
                    vt = VtI4,
                    lVal = isVietnamese ? 1 : 0
                };
                int setHr = compVTable->SetValue(pComp, clientId, &varVal);
                DebugLog.Write($"TsfCompartmentHelper.SetConversionMode isVietnamese={isVietnamese}, hr=0x{setHr:X8}");
                return setHr;
            }
            finally
            {
                NativeCom.Release(pComp);
            }
        }
        finally
        {
            NativeCom.Release(pCompMgr);
        }
    }
}
```

`GuidCompartmentKeyboardInputModeConversion` được khai báo trong `Guids.cs`:

```csharp
/// <summary>GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION - Trạng thái Conversion mode (V/E).</summary>
public static readonly Guid GuidCompartmentKeyboardInputModeConversion = new("CCF05DD7-4A87-11D7-A6E2-00065B84435C");
```

---

## 5. Tích hợp Phím tắt Chuyển chế độ vào `KeyInputTranslator.cs` và `KeyEventSinkImpl.cs`

Mã nguồn hiện tại đã cập nhật phím tắt chính là **`Ctrl + Shift + Q`** (thay vì `Ctrl + Shift` để tránh trùng lặp với phím tắt mặc định chuyển ngôn ngữ của Windows), kèm phím tắt phụ **`Alt + Z`**.

### 5.1. Mã nguồn Kiểm tra Phím tắt trong `KeyInputTranslator.cs`

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

### 5.2. Mã nguồn Bắt Phím trong `KeyEventSinkImpl.cs`

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
        var target = BambooMintKeyTextService.GetTarget(thisPtr - (sizeof(IntPtr) * 2));
        if (target != null && target.ThreadMgr != IntPtr.Zero)
        {
            TsfCompartmentHelper.SetConversionMode(target.ThreadMgr, target.ClientId, newMode);
        }
        DebugLog.Write($"OnKeyDown ToggleHotkey triggered! New IsVietnameseMode={newMode}");
        *pfEaten = 1;
        return HResult.Ok;
    }
    ...
}
```

### 5.3. Bắt Phím tắt Chuẩn TSF qua `PreserveKey` và `OnPreservedKey`

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
        var target = BambooMintKeyTextService.GetTarget(thisPtr - (sizeof(IntPtr) * 2));
        if (target != null && target.ThreadMgr != IntPtr.Zero)
        {
            TsfCompartmentHelper.SetConversionMode(target.ThreadMgr, target.ClientId, newMode);
        }
        DebugLog.Write($"OnPreservedKey Toggle triggered! New IsVietnameseMode={newMode}");
        *pfEaten = 1;
        return HResult.Ok;
    }

    return HResult.Ok;
}
```

### 5.4. Tích hợp vào Vòng đời TIP

Trong `BambooMintKeyTextService.ActivateExImpl`, sau khi đăng ký Language Bar Item, đồng bộ Compartment ngay lập tức:

```csharp
// 5. Đăng ký Language Bar Item Button vào Taskbar
LangBarItemButton.Register(pThreadMgr, tfClientId);

// 6. Đồng bộ trạng thái Input Mode Compartment với Windows Shell Taskbar
TsfCompartmentHelper.SetConversionMode(pThreadMgr, tfClientId, BridgeStateManager.IsVietnameseMode);
```

---

## 6. Quản lý Shared Memory (`SharedMemoryManager.cs`)

Mã nguồn tại [`src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs):

### 6.1. Layout bộ nhớ dùng chung (64 bytes)

| Offset | Kích thước | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|---|
| `0` | 1 byte | `byte` | `IsVietnameseMode` (1 = V, 0 = E) |
| `1` | 1 byte | `byte` | `ToneStyle` (0 = Mới, 1 = Cũ) |
| `2` | 1 byte | `byte` | `AutoRestoreEnglishWords` |
| `3` | 1 byte | `byte` | `AllowRepeatKeyUndo` |
| `4` | 1 byte | `byte` | `AllowLeadingWAsU` |
| `5 - 7` | 3 bytes | - | Reserved / Padding |
| `8 - 11` | 4 bytes | `uint` | `StateSequence`: Số đếm phiên bản trạng thái |
| `12 - 63` | 52 bytes | - | Reserved cho cấu hình mở rộng |

### 6.2. Khởi tạo Manual-Reset Event & StateSequence

```csharp
public static unsafe class SharedMemoryManager
{
    private const string MapName = @"Local\BambooMintKey_SharedConfig_v1";
    private const string EventName = @"Local\BambooMintKey_StateChangedEvent_v1";
    private const string UniversalSddl = "D:(A;;GA;;;WD)(A;;GA;;;AC)S:(ML;;NW;;;LW)";
    private const uint PageReadWrite = 0x04;
    private const uint FileMapWrite = 0x02;
    private const int SharedSize = 64;

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
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ResetEvent(IntPtr hEvent);

    // ... (các P/Invoke khác giữ nguyên)

    public static void EnsureInitialized()
    {
        // ... (tạo FileMapping)

        if (_hMap != IntPtr.Zero)
        {
            bool isCreator = (Marshal.GetLastWin32Error() == 0);
            void* pView = MapViewOfFile(_hMap, FileMapWrite, 0, 0, SharedSize);
            if (pView != null)
            {
                _pShared = (byte*)pView;

                if (isCreator)
                {
                    _pShared[0] = 1; // IsVietnameseMode On (V)
                    _pShared[1] = 0; // ToneStyle New
                    _pShared[2] = 1; // AutoRestoreEnglishWords
                    _pShared[3] = 1; // AllowRepeatKeyUndo
                    _pShared[4] = 0; // AllowLeadingWAsU
                    *(uint*)(_pShared + 8) = 1; // StateSequence ban đầu
                }
            }
        }

        if (_hEvent == IntPtr.Zero)
        {
            _hEvent = CreateEventW(pSaPtr, true /* ManualReset */, false, EventName);
        }
    }

    /// <summary>Handle của Win32 Event đồng bộ trạng thái V/E.</summary>
    public static IntPtr StateChangedEventHandle => EnsureAndReturn(ref _hEvent);

    /// <summary>Số đếm phiên bản trạng thái (Sequence Number) để các tiến trình phát hiện thay đổi.</summary>
    public static uint StateSequence
    {
        get
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                return *(uint*)(_pShared + 8);
            }
            return 0;
        }
    }

    /// <summary>Phát tín hiệu cho tất cả tiến trình khác biết cấu hình đã thay đổi.</summary>
    public static void SignalStateChanged()
    {
        if (_pShared != null)
        {
            System.Threading.Interlocked.Increment(ref *(int*)(_pShared + 8));
        }
        if (_hEvent != IntPtr.Zero)
        {
            // Đánh thức TẤT CẢ các tiến trình đang chờ đợi (Manual-Reset Broadcast)
            SetEvent(_hEvent);
            ResetEvent(_hEvent);
        }
    }

    public static bool IsVietnameseMode { /* ... */ }

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

---

## 7. Phân tích Nguyên nhân & Cơ chế Lỗi Thực tế (Root Cause Analysis)

### 7.1. Lỗi 1: Click chuột trực tiếp vào Icon E/V thường chỉ đổi được đúng 1 lần

**Cơ chế của Windows TSF đối với `HICON`:**
Theo quy định kỹ thuật của Microsoft Windows SDK cho interface `ITfLangBarItemButton::GetIcon`:
> *"phIcon: [out] Pointer to an HICON value that receives the icon handle. **The caller is responsible for destroying this icon when it is no longer required**."*

Shell của Windows (Taskbar Explorer) là bên gọi (`caller`). Sau khi nhận `HICON` và vẽ icon lên khay hệ thống, **Windows tự động gọi `DestroyIcon(hIcon)`** để giải phóng tài nguyên GDI.

**Nguyên nhân gây lỗi trong bản cũ:**
1. Ban đầu ở trạng thái **V**: `GetIcon` cấp phát `_hIconV` (ví dụ con trỏ `0x1000`). Windows nhận `0x1000`, vẽ chữ V, sau đó Windows gọi `DestroyIcon(0x1000)`. Con trỏ `0x1000` **đã bị hủy hoàn toàn trong bảng GDI của Windows**!
2. Người dùng click lần 1 (đổi sang **E**): `GetIcon` cấp phát `_hIconE` mới.
3. Người dùng click lần 2 (muốn đổi lại **V**): nếu code cache `_hIconV`, nó sẽ trả lại con trỏ cũ đã bị hủy.
4. Do handle đã hủy, Windows gặp lỗi GDI (`ERROR_INVALID_HANDLE`) nên **không thể vẽ lại icon, icon bị đơ hoặc biến mất**.

**Giải pháp đã triển khai:**
* Không lưu cache vĩnh viễn con trỏ `HICON` mà Windows đã hủy.
* Dùng **2 HICON mẫu tĩnh** `_cachedIconV` / `_cachedIconE` do bộ gõ tự quản lý.
* Mỗi lần Windows gọi `GetIcon`, trả về `CopyIcon(_cachedIconX)` — bản sao độc lập mà Windows tự do `DestroyIcon` mà không ảnh hưởng cache.

### 7.2. Lỗi 2: Độ trễ 500–1000ms khi click chuột đổi icon

**Nguyên nhân:**
* Bản cũ chỉ gọi `NotifyStateChanged()` (tức `ITfLangBarItemSink::OnUpdate`) trong `OnClick`.
* Windows 10/11 Taskbar Input Indicator ưu tiên giám sát **TSF Compartment** `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`.
* Khi compartment không được cập nhật ngay, Taskbar phải đợi chu kỳ polling định kỳ (500–1000ms) mới vẽ lại.

**Giải pháp đã triển khai:**
* Trong `OnClick`, sau `NotifyStateChanged()`, gọi thêm `TsfCompartmentHelper.SetConversionMode(_pThreadMgr, _clientId, newMode)`.
* Kết hợp với style `TF_LBI_STYLE_BTN_TOGGLE` trong `GetInfo`, giúp Taskbar coi nút là công tắc hai chiều, sẵn sàng nhận click tiếp theo ngay.
* Nới lỏng điều kiện click: `if (click != TsfLangBarFlags.TfLbiClkRight)` thay vì `== TfLbiClkLeft`, tránh bỏ qua các cú click liên tiếp nhanh.

### 7.3. Lỗi 3: Phím tắt `Ctrl + Shift + Q` không đổi được E/V

**Phân tích luồng bắt phím từ Runtime Log:**
```text
[15:10:31.221] OnKeyDown ENTER vk=17  (VK_CONTROL)
[15:10:31.781] OnKeyDown ENTER vk=16  (VK_SHIFT)
[15:10:31.956] OnKeyDown ENTER vk=81  (VK_Q)
[15:10:31.957] RequestEdit: action=UpdateText, text=Q
[15:10:31.964] OnKeyDown ProcessKey char=Q, text=Q
```

**Nguyên nhân:**
1. `GetKeyState` / `GetAsyncKeyState` trong thread message pump của TSF có thể không đồng bộ với cờ modifier khi Windows Shell đã chặn `Ctrl + Shift` trước.
2. Nếu `OnTestKeyDown` trả về `*pfEaten = 0`, Windows sẽ không gọi `OnKeyDown` nữa.

**Giải pháp đã triển khai:**
* Đăng ký phím tắt qua `ITfKeystrokeMgr::PreserveKey` trong `KeyEventSinkHelper.RegisterPreservedKeys`.
* Callback `OnPreservedKey` được Windows TSF gọi trực tiếp toàn cục, không phụ thuộc message queue của ứng dụng.
* Cả `OnKeyDown` và `OnPreservedKey` đều cập nhật `BridgeStateManager`, `NotifyStateChanged()` và `TsfCompartmentHelper.SetConversionMode`.

### 7.4. Lỗi 4: Icon hiển thị E nhưng vẫn gõ ra dấu tiếng Việt (Lệch pha giữa Icon và Bộ gõ)

**Nguyên nhân:**
* Ứng dụng sandbox (Chrome, Edge, VS Code, UWP) chạy ở Low Integrity / AppContainer.
* Named File Mapping mặc định của tiến trình Medium Integrity chặn access từ Low Integrity (`ERROR_ACCESS_DENIED`).
* Con trỏ `_pShared` bị `null`, ứng dụng sandbox fallback về `_fallbackVietnameseMode = true` cố định, không thấy lệnh chuyển `E`.

**Giải pháp đã triển khai:**
1. **Universal SDDL:** `"D:(A;;GA;;;WD)(A;;GA;;;AC)S:(ML;;NW;;;LW)"` — cấp quyền Generic All cho Everyone (`WD`), ALL APPLICATION PACKAGES (`AC`) và nhãn toàn vẹn Low Integrity (`LW`).
2. **Fallback an toàn:** Khi `_pShared == null`, dùng `_fallbackVietnameseMode` cho phép đảo trạng thái cục bộ.
3. **Đồng bộ Config trong Engine:** `BridgeStateManager.Config` luôn đọc từ `SharedMemoryManager.IsVietnameseMode` trước khi truyền vào `TelexEngine.processKey`.

### 7.5. Lỗi 5: Sau vài lần tắt bật thì không đổi được chữ (Kẹt Event Đồng bộ Đa tiến trình)

**Nguyên nhân:**
* Bản cũ dùng Win32 **AutoReset** Event (`bManualReset = false`).
* `SetEvent` chỉ đánh thức đúng **1 thread của 1 tiến trình bất kỳ** rồi tự reset.
* Nếu tiến trình nền (Notepad/Chrome) "nhặt" mất tín hiệu, tiến trình Taskbar (`explorer.exe`) sẽ tiếp tục block, không gọi `OnUpdate`.

**Giải pháp đã triển khai:**
* Chuyển sang **Manual-Reset Event** (`bManualReset = true`) kết hợp `StateSequence` trong shared memory.
* `SignalStateChanged()` tăng `StateSequence` an toàn đa luồng (`Interlocked.Increment`), sau đó `SetEvent` + `ResetEvent` để broadcast đến tất cả tiến trình.
* `StartEventListener` không chỉ dựa vào event mà còn so sánh `StateSequence` mỗi 250ms. Dù event bị trễ/mất, thread vẫn đồng bộ đúng phiên bản trạng thái mới nhất.

---

## 8. Kế hoạch Kiểm thử & Nghiệm thu (Verification Matrix)

| Bước kiểm thử | Thao tác thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- |
| **1. DevHarness Test** | Chạy `dotnet run --project src/BambooMintKey.DevHarness` | `CreateBambooIcon("V")` và `"E"` trả về `HICON != IntPtr.Zero`. `GetIcon` phản hồi đúng icon theo trạng thái. Không rò rỉ bộ nhớ. |
| **2. NativeAOT Build** | Chạy `pwsh scripts/build-native.ps1` | Biên dịch ra DLL `publish/win-x64/BambooMintKey.dll` thành công không cảnh báo calling convention hay AOT trim. |
| **3. Hiển thị Icon Taskbar** | Đăng ký TIP, chuyển sang BambooMintKey bằng `Win + Space` | Khay Taskbar xuất hiện ngay lập tức icon hình vuông nền xanh lá cây bo góc có chữ **V** màu trắng ngà cạnh chữ `VIE`. |
| **4. Click Chuột Đổi Icon Tức thì** | Click chuột trái vào icon chữ **V** | Icon chuyển tức thì sang chữ **E** (nền xanh lá, chữ E màu trắng), tooltip chuyển thành `"BambooMintKey: English"`. Click tiếp đổi lại chữ **V** liên tục không bị đơ, độ trễ < 30ms. |
| **5. Click Chuột Liên tiếp Nhanh** | Nhấp chuột liên tục 5–10 lần nhanh | Mỗi cú click đều đổi icon tương ứng, không có cú click nào bị trôi hoặc phải đợi 1 giây. |
| **6. Phím tắt Toggle** | Bấm `Ctrl + Shift + Q` hoặc `Alt + Z` | Icon trên Taskbar tự động đổi giữa **V** ↔ **E**. Chế độ gõ tiếng Việt tắt/bật đồng bộ. |
| **7. Đồng bộ Xuyên tiến trình** | Mở Notepad 1 → Mở Notepad 2 → Đóng Notepad 1 → Đổi mode trên Notepad 2 | Icon Taskbar vẫn đồng bộ, không biến mất, không đơ. |
| **8. GDI Leak Check** | Dùng Task Manager (cột GDI Objects) click toggle 500 lần | Số lượng GDI Objects của tiến trình không tăng lũy tiến (đạt tiêu chuẩn 0 GDI Leak nhờ CopyIcon + cache). |
| **9. Sandbox AppContainer** | Gõ trong Chrome/Edge/VS Code (Electron) | Trạng thái V/E đồng bộ với Taskbar, không bị Access Denied dẫn đến lệch pha. |
