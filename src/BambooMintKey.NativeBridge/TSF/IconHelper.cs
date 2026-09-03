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
    /// <returns>IntPtr trỏ tới HICON hợp lệ (cần được giải phóng bằng DestroyIcon khi tắt app)</returns>
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
