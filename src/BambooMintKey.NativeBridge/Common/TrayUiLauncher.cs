// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.TSF;
using Microsoft.Win32;

namespace BambooMintKey.NativeBridge.Common;

/// <summary>
/// Quản lý vòng đời và tự động khởi chạy BambooMintKey.UI.exe (icon EV khay hệ thống).
/// Tương đương kiến trúc StatusBarLauncher.swift trên macOS:
/// Đảm bảo mỗi khi BambooMintKey TextService được kích hoạt phiên gõ hoặc nhận focus,
/// ứng dụng khay hệ thống luôn chạy ngầm để phục vụ người dùng. Kể cả người dùng có End Task
/// từ Task Manager, UI sẽ tự động được hồi sinh ngay khi gõ phím tiếp theo.
/// </summary>
public static class TrayUiLauncher
{
    private const string MutexName = @"Local\BambooMintKey_UI_Process_Mutex_v1";
    private const uint SynchronizeAccess = 0x00100000;
    private static long _lastLaunchTick = 0;
    private static readonly object LaunchLock = new();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenMutexW(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(IntPtr hModule, [Out] char[] lpFilename, uint nSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetModuleHandleExW(uint dwFlags, IntPtr lpModuleName, out IntPtr phModule);

    /// <summary>
    /// Kiểm tra và tự động phục hồi tiến trình UI nếu đang không chạy.
    /// </summary>
    public static void EnsureRunning()
    {
        long now = Environment.TickCount64;
        // Debounce: Chỉ kiểm tra và khởi chạy tối đa 1 lần mỗi 3 giây để tránh overhead
        if (now - _lastLaunchTick < 3000) return;

        lock (LaunchLock)
        {
            now = Environment.TickCount64;
            if (now - _lastLaunchTick < 3000) return;

            // 1. Kiểm tra nhanh sự tồn tại của UI thông qua Named Mutex (0.001ms)
            IntPtr hMutex = OpenMutexW(SynchronizeAccess, false, MutexName);
            if (hMutex != IntPtr.Zero)
            {
                CloseHandle(hMutex);
                return; // UI đang hoạt động hoàn toàn bình thường
            }

            // 2. UI chưa chạy hoặc vừa bị End Task từ Process Manager -> Hồi sinh UI
            _lastLaunchTick = now;
            string? exePath = FindUiExecutable();
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    ProcessStartInfo psi = new(exePath, "--tray")
                    {
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psi);
                    DebugLog.Write($"TrayUiLauncher: Successfully launched UI watchdog from {exePath}");
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"TrayUiLauncher: Failed to launch UI: {ex.Message}");
                }
            }
            else
            {
                DebugLog.Write("TrayUiLauncher: BambooMintKey.UI.exe not found in candidates");
            }
        }
    }

    /// <summary>
    /// Tìm kiếm đường dẫn tệp thực thi BambooMintKey.UI.exe theo thứ tự ưu tiên.
    /// </summary>
    private static string? FindUiExecutable()
    {
        // Ưu tiên 1: Cùng thư mục với BambooMintKey.dll hiện tại
        try
        {
            char[] buffer = new char[1024];
            // GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004
            if (GetModuleHandleExW(0x00000004, (IntPtr)typeof(TrayUiLauncher).TypeHandle.Value, out IntPtr hModule) && hModule != IntPtr.Zero)
            {
                uint len = GetModuleFileNameW(hModule, buffer, (uint)buffer.Length);
                if (len > 0)
                {
                    string dllPath = new(buffer, 0, (int)len);
                    string? dir = Path.GetDirectoryName(dllPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        string candidate = Path.Combine(dir, "BambooMintKey.UI.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
        }
        catch { }

        // Ưu tiên 2: Thư mục Program Files chuẩn
        try
        {
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string candidate = Path.Combine(progFiles, "BambooMintKey", "BambooMintKey.UI.exe");
            if (File.Exists(candidate)) return candidate;
        }
        catch { }

        // Ưu tiên 3: Đọc từ Registry Run
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (key?.GetValue("BambooMintKey") is string runCmd && !string.IsNullOrEmpty(runCmd))
            {
                string clean = runCmd.Replace("\"", "").Trim();
                int idx = clean.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string path = clean.Substring(0, idx + 4);
                    if (File.Exists(path)) return path;
                }
            }
        }
        catch { }

        // Ưu tiên 4: Thư mục publish/ui (môi trường dev)
        string devCandidate = @"D:\Kojin\BambooMintKey\publish\ui\BambooMintKey.UI.exe";
        if (File.Exists(devCandidate)) return devCandidate;

        return null;
    }
}
