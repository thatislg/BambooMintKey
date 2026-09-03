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
    private const uint PageReadWrite = 0x04;
    private const uint FileMapWrite = 0x02;
    private const int SharedSize = 64;

    private static IntPtr _hMap = IntPtr.Zero;
    private static IntPtr _hEvent = IntPtr.Zero;
    private static byte* _pShared = null;
    private static readonly object _initLock = new();

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
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_pShared != null) return;

        lock (_initLock)
        {
            if (_pShared != null) return;

            // IntPtr(-1) tương ứng INVALID_HANDLE_VALUE để cấp phát từ page file
            _hMap = CreateFileMappingW(new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, SharedSize, MapName);
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
                _hEvent = CreateEventW(IntPtr.Zero, false /* AutoReset */, false, EventName);
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
            return true; // Mặc định an toàn nếu không map được
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[0] = (byte)(value ? 1 : 0);
                SignalStateChanged();
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
        return true;
    }
}
