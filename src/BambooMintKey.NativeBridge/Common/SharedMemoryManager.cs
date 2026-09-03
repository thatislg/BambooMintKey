// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.TSF;

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
    private static readonly Lock InitLock = new();

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bInheritHandle;
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
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ResetEvent(IntPtr hEvent);

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

        lock (InitLock)
        {
            if (_pShared != null) return;

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
                            _pShared[5] = 0; // 0 = Telex, 1 = VNI, 2 = Simple Telex
                            _pShared[6] = 0; // 0 = Unicode, 1 = Compound, 2 = TCVN3
                            _pShared[7] = 0; // 0 = Ctrl+Shift, 1 = Alt+Z, 2 = Ctrl+Space, 3 = None
                            *(uint*)(_pShared + 8) = 1; // StateSequence ban đầu
                            *(uint*)(_pShared + 12) = 0x10; // HotkeyVKey: VK_SHIFT (0x10) mặc định
                            *(uint*)(_pShared + 16) = 0x0202; // HotkeyModifiers: Control | OnKeyUp (0x0202) mặc định

                            // Đọc cấu hình người dùng đã lưu trong file config.json nếu có
                            LoadInitialConfigFromDisk(_pShared);
                        }
                    }
                }

                if (_hEvent == IntPtr.Zero)
                {
                    _hEvent = CreateEventW(pSaPtr, true /* ManualReset */, false, EventName);
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

    private static void LoadInitialConfigFromDisk(byte* pShared)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(appData, "BambooMintKey", "config.json");
            if (!File.Exists(path))
            {
                string dir = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string defaultJson = """
                {
                  "version": 2,
                  "inputMethod": 0,
                  "charset": 0,
                  "toggleHotkey": 0,
                  "hotkeyVKey": 16,
                  "hotkeyModifiers": 514,
                  "toneStyle": 0,
                  "autoRestoreEnglishWords": true,
                  "allowRepeatKeyUndo": true,
                  "allowLeadingWAsU": false,
                  "startWithWindows": true,
                  "macroEnabled": false,
                  "macros": {
                    "vn": "Việt Nam",
                    "bmk": "BambooMintKey",
                    "f#": "F-Sharp"
                  }
                }
                """;
                File.WriteAllText(path, defaultJson);
                return;
            }

            string json = File.ReadAllText(path);

            uint ParseUint(string key, uint defaultVal)
            {
                string prefix = $"\"{key}\":";
                int idx = json.IndexOf(prefix);
                if (idx < 0) return defaultVal;
                string sub = json.Substring(idx + prefix.Length).Trim();
                int endIdx = sub.IndexOfAny([',', '\n', '\r', '}']);
                string token = endIdx >= 0 ? sub.Substring(0, endIdx).Trim() : sub;
                return uint.TryParse(token, out uint v) ? v : defaultVal;
            }

            bool ParseBool(string key, bool defaultVal)
            {
                string prefix = $"\"{key}\":";
                int idx = json.IndexOf(prefix);
                if (idx < 0) return defaultVal;
                string sub = json.Substring(idx + prefix.Length).Trim();
                if (sub.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (sub.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
                return defaultVal;
            }

            pShared[1] = (byte)ParseUint("toneStyle", 0);
            pShared[2] = (byte)(ParseBool("autoRestoreEnglishWords", true) ? 1 : 0);
            pShared[3] = (byte)(ParseBool("allowRepeatKeyUndo", true) ? 1 : 0);
            pShared[4] = (byte)(ParseBool("allowLeadingWAsU", false) ? 1 : 0);
            pShared[5] = (byte)ParseUint("inputMethod", 0);
            pShared[6] = (byte)ParseUint("charset", 0);
            pShared[7] = (byte)ParseUint("toggleHotkey", 0);
            *(uint*)(pShared + 12) = ParseUint("hotkeyVKey", 0x10);
            *(uint*)(pShared + 16) = ParseUint("hotkeyModifiers", 0x0202);

            DebugLog.Write($"Loaded config from disk: vKey={*(uint*)(pShared + 12)}, mods={*(uint*)(pShared + 16)}");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LoadInitialConfigFromDisk error: {ex.Message}");
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

    /// <summary>Con trỏ handle của sự kiện StateChangedEvent để các tiến trình chờ lắng nghe.</summary>
    public static IntPtr EventHandle
    {
        get
        {
            EnsureInitialized();
            return _hEvent;
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

    /// <summary>
    /// Quy chuẩn đặt vị trí dấu thanh (0 = Mới: òa, xòe, thủy / 1 = Cũ: oà, xoè, thuỷ).
    /// </summary>
    public static byte ToneStyle
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? _pShared[1] : (byte)0;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[1] = value;
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Tự động phục hồi từ gốc khi gõ từ sai ngữ pháp tiếng Việt (Fallback tiếng Anh).
    /// </summary>
    public static bool AutoRestoreEnglishWords
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? (_pShared[2] != 0) : true;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[2] = (byte)(value ? 1 : 0);
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Cho phép gõ lặp dấu để khôi phục ký tự thô (ví dụ: 'ss' -> 's', 'aa' -> 'a').
    /// </summary>
    public static bool AllowRepeatKeyUndo
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? (_pShared[3] != 0) : true;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[3] = (byte)(value ? 1 : 0);
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Cho phép phím 'w' đứng đầu từ biến thành 'ư' (True: w -> ư, False: w -> w).
    /// </summary>
    public static bool AllowLeadingWAsU
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? (_pShared[4] != 0) : false;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[4] = (byte)(value ? 1 : 0);
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Kiểu gõ hiện tại (0: Telex, 1: VNI, 2: Simple Telex).
    /// </summary>
    public static byte InputMethod
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? _pShared[5] : (byte)0;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[5] = value;
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Bảng mã đầu ra (0: Unicode dựng sẵn, 1: Unicode tổ hợp, 2: TCVN3).
    /// </summary>
    public static byte Charset
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? _pShared[6] : (byte)0;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[6] = value;
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Phím tắt chuyển đổi chế độ V/E (0: Ctrl+Shift, 1: Alt+Z, 2: Ctrl+Space, 3: Không dùng).
    /// </summary>
    public static byte ToggleHotkey
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? _pShared[7] : (byte)0;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                _pShared[7] = value;
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// Virtual Key Code của phím tắt chuyển đổi V/E tự chọn (ví dụ: 0x10 cho Shift, 0x5A cho 'Z', 0x20 cho Space).
    /// </summary>
    public static uint HotkeyVKey
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? *(uint*)(_pShared + 12) : 0x10;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                *(uint*)(_pShared + 12) = value;
                SignalStateChanged();
            }
        }
    }

    /// <summary>
    /// TSF Modifiers của phím tắt chuyển đổi V/E tự chọn (ví dụ: Control | OnKeyUp = 0x0202, Alt = 0x0001, Control = 0x0002).
    /// </summary>
    public static uint HotkeyModifiers
    {
        get
        {
            EnsureInitialized();
            return _pShared != null ? *(uint*)(_pShared + 16) : 0x0202;
        }
        set
        {
            EnsureInitialized();
            if (_pShared != null)
            {
                *(uint*)(_pShared + 16) = value;
                SignalStateChanged();
            }
        }
    }
}
