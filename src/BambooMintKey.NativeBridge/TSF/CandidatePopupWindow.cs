// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using System.Text;
using BambooMintKey.Core.Domain;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public struct Win32Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
public struct Win32Point
{
    public int X;
    public int Y;
}

/// <summary>
/// Cầu nối IPC nhẹ (SharedMemory + Named Event) gửi trạng thái ứng viên Symbol mode
/// từ BambooMintKey.dll (in-process TSF) sang tiến trình BambooMintKey.UI (Avalonia) để vẽ Popup dọc.
/// Tuyệt đối KHÔNG tạo cửa sổ Win32/GDI và KHÔNG gọi COM ITfContextView/GetTextExt trong tiến trình app chủ.
/// Thiết kế theo 013_03_Avalonia_Candidate_Popup_Design.md.
/// </summary>
public static unsafe class CandidatePopupWindow
{
    private const string MapName = @"Local\BambooMintKey_CandidatePopup_v1";
    private const string EventName = @"Local\BambooMintKey_CandidatePopupEvent_v1";
    private const string UiMutexName = @"Local\BambooMintKey_UI_SingleInstance_Mutex";
    private const string UniversalSddl = "D:(A;;GA;;;WD)(A;;GA;;;AC)(A;;GA;;;S-1-15-2-2)S:(ML;;NW;;;LW)";

    private const uint PageReadWrite = 0x04;
    private const uint FileMapWrite = 0x02;
    private const uint Synchronize = 0x00100000;
    private const int SharedSize = 1024;
    private const int PayloadOffset = 52;
    private const int MaxPayloadBytes = SharedSize - PayloadOffset;

    private static IntPtr _hMap = IntPtr.Zero;
    private static IntPtr _hEvent = IntPtr.Zero;
    private static byte* _pShared = null;
    private static int _sequence;
    private static readonly Lock InitLock = new();

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public Win32Rect rcCaret;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out IntPtr securityDescriptor,
        IntPtr securityDescriptorSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileMappingW(
        IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect,
        uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenFileMappingW(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, nuint dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEventW(IntPtr lpEventAttributes, [MarshalAs(UnmanagedType.Bool)] bool bManualReset, [MarshalAs(UnmanagedType.Bool)] bool bInitialState, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenMutexW(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo lpgui);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Win32Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    /// <summary>Kiểm tra tiến trình BambooMintKey.UI (Avalonia) có đang chạy để hiển thị popup hay không.</summary>
    public static bool IsUiHostRunning()
    {
        try
        {
            IntPtr hMutex = OpenMutexW(Synchronize, false, UiMutexName);
            if (hMutex != IntPtr.Zero)
            {
                CloseHandle(hMutex);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lấy tọa độ màn hình thuần Win32 (KHÔNG gọi COM MSCTF/WinUIEdit).
    /// Ưu tiên GetGUIThreadInfo caret; nếu app không báo caret thì fallback sang vị trí con trỏ chuột.
    /// </summary>
    public static bool TryGetWin32AnchorRect(out Win32Rect anchorRect, out bool fromCaret)
    {
        anchorRect = default;
        fromCaret = false;

        try
        {
            var gui = new GuiThreadInfo { cbSize = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            if (GetGUIThreadInfo(0, ref gui) && gui.hwndCaret != IntPtr.Zero)
            {
                var tl = new Win32Point { X = gui.rcCaret.Left, Y = gui.rcCaret.Top };
                var br = new Win32Point { X = gui.rcCaret.Right, Y = gui.rcCaret.Bottom };
                if (ClientToScreen(gui.hwndCaret, ref tl) && ClientToScreen(gui.hwndCaret, ref br))
                {
                    if (!(tl.X == 0 && tl.Y == 0 && br.X == 0 && br.Y == 0) && br.Y >= tl.Y)
                    {
                        anchorRect = new Win32Rect
                        {
                            Left = tl.X,
                            Top = tl.Y,
                            Right = Math.Max(tl.X + 2, br.X),
                            Bottom = Math.Max(tl.Y + 20, br.Y)
                        };
                        fromCaret = true;
                        return true;
                    }
                }
            }

            if (GetCursorPos(out var pt))
            {
                anchorRect = new Win32Rect
                {
                    Left = pt.X,
                    Top = pt.Y,
                    Right = pt.X + 2,
                    Bottom = pt.Y + 20
                };
                fromCaret = false;
                return true;
            }
        }
        catch { }

        return false;
    }

    public static bool ShowOrUpdate(CandidatePage page)
    {
        try
        {
            if (page.Items.Length == 0 || !EnsureInitialized())
            {
                Hide();
                return false;
            }

            TryGetWin32AnchorRect(out var anchor, out bool fromCaret);

            var sb = new StringBuilder(128);
            sb.Append(page.RawQuery);
            int itemCount = Math.Min(7, page.Items.Length);
            for (int i = 0; i < itemCount; i++)
            {
                sb.Append('\n');
                sb.Append(page.Items[i]);
            }

            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
            int payloadLen = Math.Min(payload.Length, MaxPayloadBytes);

            int* header = (int*)_pShared;
            header[1] = 1; // Visible = 1
            header[2] = anchor.Left;
            header[3] = anchor.Top;
            header[4] = anchor.Right;
            header[5] = anchor.Bottom;
            header[6] = page.SelectedInPage;
            header[7] = page.PageIndex;
            header[8] = page.TotalPages;
            header[9] = page.TotalCount;
            header[10] = itemCount;
            header[11] = fromCaret ? 1 : 0;
            header[12] = payloadLen;

            if (payloadLen > 0)
            {
                Marshal.Copy(payload, 0, (IntPtr)(_pShared + PayloadOffset), payloadLen);
            }

            // Ghi Sequence sau cùng để reader luôn đọc được bản nhất quán
            header[0] = Interlocked.Increment(ref _sequence);

            if (_hEvent != IntPtr.Zero)
            {
                SetEvent(_hEvent);
            }
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.WriteAndFlush($"CandidatePopupIpc.ShowOrUpdate exception: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static void Hide()
    {
        try
        {
            if (_pShared == null && !EnsureInitialized()) return;

            int* header = (int*)_pShared;
            if (header[1] == 0) return; // Đã ẩn sẵn

            header[1] = 0; // Visible = 0
            header[10] = 0;
            header[12] = 0;
            header[0] = Interlocked.Increment(ref _sequence);

            if (_hEvent != IntPtr.Zero)
            {
                SetEvent(_hEvent);
            }
        }
        catch { }
    }

    private static bool EnsureInitialized()
    {
        if (_pShared != null) return true;

        lock (InitLock)
        {
            if (_pShared != null) return true;

            SecurityAttributes sa = new()
            {
                nLength = Marshal.SizeOf<SecurityAttributes>(),
                bInheritHandle = false
            };

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
                    void* pView = MapViewOfFile(_hMap, FileMapWrite, 0, 0, SharedSize);
                    if (pView != null)
                    {
                        _pShared = (byte*)pView;
                    }
                }

                _hEvent = CreateEventW(pSaPtr, false, false, EventName);
            }
            finally
            {
                if (pSd != IntPtr.Zero)
                {
                    LocalFree(pSd);
                }
            }

            return _pShared != null;
        }
    }
}
