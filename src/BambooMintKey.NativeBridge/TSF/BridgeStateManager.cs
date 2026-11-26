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

    /// <summary>Cấu hình engine hiện tại (đồng bộ trạng thái và tùy chọn với SharedMemoryManager).</summary>
    public static EngineConfig.EngineConfig Config
    {
        get
        {
            bool isVn = IsVietnameseMode;
            bool repeatUndo = SharedMemoryManager.AllowRepeatKeyUndo;
            bool leadingW = SharedMemoryManager.AllowLeadingWAsU;
            byte toneStyleByte = SharedMemoryManager.ToneStyle;
            bool freeTone = SharedMemoryManager.AllowFreeTonePlacement;
            bool enableDict = SharedMemoryManager.EnableVietnameseDictionary;
            bool enableBacktrack = SharedMemoryManager.EnableEnglishBacktracking;
            var toneStyle = (toneStyleByte == 1) ? Types.TonePlacementStyle.Traditional : Types.TonePlacementStyle.Modern;

            if (_currentConfig.IsEnabled != isVn ||
                _currentConfig.AllowRepeatKeyUndo != repeatUndo ||
                _currentConfig.AllowLeadingWAsU != leadingW ||
                _currentConfig.ToneStyle != toneStyle ||
                _currentConfig.AllowFreeTonePlacement != freeTone ||
                _currentConfig.EnableVietnameseDictionary != enableDict ||
                _currentConfig.EnableEnglishBacktracking != enableBacktrack)
            {
                _currentConfig = new EngineConfig.EngineConfig(
                    isVn,
                    repeatUndo,
                    leadingW,
                    toneStyle,
                    freeTone,
                    enableDict,
                    enableBacktrack,
                    true
                );
            }
            return _currentConfig;
        }
    }

    /// <summary>
    /// Kiểm tra xem chế độ gõ tiếng Việt hiện đang bật (V) hay tắt (E).
    /// Single source of truth nằm trong GlobalVEState / SharedMemoryManager.
    /// </summary>
    public static bool IsVietnameseMode
    {
        get => GlobalVEState.IsVietnameseMode;
        set => GlobalVEState.SetVietnameseMode(value, GlobalVEState.SyncTarget.All);
    }

    /// <summary>
    /// Đảo trạng thái gõ tiếng Việt / tiếng Anh và trả về trạng thái mới.
    /// </summary>
    public static bool ToggleVietnameseMode()
    {
        return GlobalVEState.ToggleVietnameseMode(GlobalVEState.SyncTarget.All);
    }

    // =========================================================================
    // Lifecycle
    // =========================================================================

    /// <summary>Khởi tạo lại engine state về empty (KHÔNG đè trạng thái IsEnabled của người dùng).</summary>
    public static void InitializeEngine()
    {
        _currentState = Types.WordState.Empty;
    }

    /// <summary>Kiểm tra engine có đang ở trong chế độ gõ ký hiệu (Symbol mode) hay không.</summary>
    public static bool IsSymbolModeActive => !_currentState.SymbolState.IsIdle;

    /// <summary>Kiểm tra vị trí con trỏ hiện tại có hợp lệ để kích hoạt "::" hay không.</summary>
    public static bool CanStartSymbolTrigger => _currentState.CanStartSymbolTrigger;

    /// <summary>Reset state về empty (dùng khi chuyển focus hoặc composition kết thúc).</summary>
    public static void ResetState(bool preserveTriggerBoundary = false)
    {
        bool canStart = !preserveTriggerBoundary || _currentState.CanStartSymbolTrigger;
        _currentState = SymbolEngine.withTriggerBoundary(canStart, Types.WordState.Empty);
    }

    /// <summary>Cập nhật ranh giới từ khi một ký tự không bị bộ gõ nuốt đi qua (vd số, dấu câu, khoảng trắng).</summary>
    public static void NotifyNonEatenChar(char c)
    {
        if (!IsSymbolModeActive && _currentState.RawKeys.IsEmpty)
        {
            _currentState = SymbolEngine.withTriggerBoundary(SymbolEngine.isTriggerBoundaryChar(c), _currentState);
        }
    }

    /// <summary>Đặt lại cờ ranh giới từ về true (khi người dùng di chuyển con trỏ bằng phím điều hướng).</summary>
    public static void ResetTriggerBoundary()
    {
        if (!IsSymbolModeActive && _currentState.RawKeys.IsEmpty)
        {
            _currentState = SymbolEngine.withTriggerBoundary(true, _currentState);
        }
    }

    // =========================================================================
    // Process key inputs
    // =========================================================================

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

    /// <summary>Xử lý phím Tab / Shift+Tab (xoay ứng viên trong Symbol mode).</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessTab(bool isShift)
    {
        var input = Types.KeyInput.NewTab(isShift);
        var result = TelexEngine.processKey(_currentState, input, Config);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    /// <summary>Xử lý phím Escape (hủy Symbol mode trả về chuỗi thô).</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessEscape()
    {
        var input = Types.KeyInput.Escape;
        var result = TelexEngine.processKey(_currentState, input, Config);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }

    /// <summary>Xử lý phím điều hướng/không in được khi đang có phiên Symbol mode.</summary>
    public static (Types.WordState NewState, Types.EngineAction Action) ProcessNonCharacter()
    {
        var input = Types.KeyInput.NonCharacter;
        var result = TelexEngine.processKey(_currentState, input, Config);
        _currentState = result.Item1;
        return (result.Item1, result.Item2);
    }
}
