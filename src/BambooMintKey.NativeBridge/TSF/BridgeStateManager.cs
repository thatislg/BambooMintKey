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
/// Theo thiết kế 002_02_TSF_TextInputProcessor_Lifecycle.md.
/// </summary>
public static class BridgeStateManager
{
    // =========================================================================
    // Internal engine state
    // =========================================================================

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

    // =========================================================================
    // Lifecycle
    // =========================================================================

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

    // =========================================================================
    // Process key inputs
    // =========================================================================

    /// <summary>Xử lý một ký tự bàn phím thông thường.</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessKey(char c)
    {
        var input = Types.KeyInput.NewChar(c);
        var result = TelexEngine.processKey(_currentState, input, _currentConfig);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    /// <summary>Xử lý phím Backspace.</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessBackspace()
    {
        var input = Types.KeyInput.Backspace;
        var result = TelexEngine.processKey(_currentState, input, _currentConfig);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    /// <summary>Xử lý ký tự ngắt từ (space, dấu câu, ...).</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessWordBreak(char breakChar)
    {
        var input = Types.KeyInput.NewWordBreak(breakChar);
        var result = TelexEngine.processKey(_currentState, input, _currentConfig);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }
}
