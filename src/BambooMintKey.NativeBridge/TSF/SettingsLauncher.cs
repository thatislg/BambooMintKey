// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System;
using System.Diagnostics;
using System.IO;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Khởi chạy giao diện Cài đặt BambooMintKey.UI.
/// </summary>
public static class SettingsLauncher
{
    public static void LaunchSettingsGui(string? argument = null)
    {
        try
        {
            string dllPath = NativeMethods.GetCurrentDllPath();
            string dir = !string.IsNullOrEmpty(dllPath) ? Path.GetDirectoryName(dllPath)! : AppDomain.CurrentDomain.BaseDirectory;
            string uiPath = Path.Combine(dir, "BambooMintKey.UI.exe");

            if (!File.Exists(uiPath))
            {
                // Fallback nếu chạy trong dev
                uiPath = @"D:\Kojin\BambooMintKey\publish\win-x64\BambooMintKey.UI.exe";
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
