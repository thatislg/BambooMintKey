// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Khởi chạy giao diện Cài đặt BambooMintKey.UI (bảo vệ Single-Instance).
/// </summary>
public static class SettingsLauncher
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? lpClassName, string lpWindowName);

    private const int SwRestore = 9;

    public static void LaunchSettingsGui(string? argument = null)
    {
        try
        {
            // 1. Kiểm tra xem BambooMintKey.UI đã chạy hay chưa. Nếu có, kích hoạt cửa sổ lên trước.
            var existingProcesses = Process.GetProcessesByName("BambooMintKey.UI");
            foreach (var proc in existingProcesses)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        IntPtr hWnd = proc.MainWindowHandle;
                        if (hWnd == IntPtr.Zero)
                        {
                            hWnd = FindWindowW(null, "BambooMintKey — Bảng Điều Khiển Cài Đặt");
                        }

                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindow(hWnd, SwRestore);
                            SetForegroundWindow(hWnd);
                            DebugLog.Write($"SettingsLauncher: Đã kích hoạt cửa sổ hiện tại (hWnd={hWnd})");
                            return;
                        }
                    }
                }
                catch { }
            }

            // 2. Nếu chưa chạy, tìm file thực thi và khởi chạy tiến trình mới.
            //    Ưu tiên file UI.exe nằm cạnh DLL đang được TSF load (thư mục cài đặt).
            //    Không được hardcode đường dẫn máy dev vào code production — việc này
            //    từng khiến UI mở nhầm bản cũ (vd. publish\ui\... vẫn là 1.1.2).
            string dllPath = NativeMethods.GetCurrentDllPath();
            string dir = !string.IsNullOrEmpty(dllPath) ? Path.GetDirectoryName(dllPath)! : AppDomain.CurrentDomain.BaseDirectory;
            string uiPath = Path.Combine(dir, "BambooMintKey.UI.exe");

            if (!File.Exists(uiPath))
            {
                // GetCurrentDllPath() có thể trả về sai/trống khi chạy in-proc trong
                // TSF (NativeAOT), nên dự phòng theo thư mục cài đặt chuẩn.
                string? programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                string[] candidates = [
                    Path.Combine(programFiles ?? "", "BambooMintKey", "BambooMintKey.UI.exe"),
                    Path.Combine(programFilesX86 ?? "", "BambooMintKey", "BambooMintKey.UI.exe")
                ];
                foreach (var cand in candidates)
                {
                    if (File.Exists(cand))
                    {
                        uiPath = cand;
                        break;
                    }
                }
            }

            if (File.Exists(uiPath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uiPath,
                    Arguments = argument ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                DebugLog.Write($"SettingsLauncher: Không tìm thấy file {uiPath}");
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"SettingsLauncher Exception: {ex.Message}");
        }
    }
}
