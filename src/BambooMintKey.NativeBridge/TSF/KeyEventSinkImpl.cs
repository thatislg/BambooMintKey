// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cài đặt ITfKeyEventSink để đánh chặn phím hệ thống.
/// Phân loại phím, nuốt phím khi cần và điều phối kết quả vào F# Telex Engine.
/// Theo thiết kế 002_03_KeyEventSink_and_Core_Interop.md.
/// </summary>
public static unsafe class KeyEventSinkImpl
{
    private static TfKeyEventSinkVTable* _vTable;

    /// <summary>
    /// Lấy (hoặc khởi tạo) con trỏ VTable của ITfKeyEventSink.
    /// </summary>
    public static IntPtr GetVTablePointer()
    {
        if (_vTable != null) return (IntPtr)_vTable;

        _vTable = (TfKeyEventSinkVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(KeyEventSinkImpl), sizeof(TfKeyEventSinkVTable));

        _vTable->QueryInterface = &QueryInterface;
        _vTable->AddRef = &AddRef;
        _vTable->Release = &Release;
        _vTable->OnSetFocus = &OnSetFocus;
        _vTable->OnTestKeyDown = &OnTestKeyDown;
        _vTable->OnTestKeyUp = &OnTestKeyUp;
        _vTable->OnKeyDown = &OnKeyDown;
        _vTable->OnKeyUp = &OnKeyUp;
        _vTable->OnPreservedKey = &OnPreservedKey;

        return (IntPtr)_vTable;
    }

    // =========================================================================
    // IUnknown Callbacks
    // =========================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        // Offset lùi lại về struct BambooMintKeyTextService chính
        var rootPtr = thisPtr - (sizeof(IntPtr) * 2);
        return BambooMintKeyTextService.QueryInterfaceImpl(rootPtr, riid, ppvObject);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
    {
        var rootPtr = thisPtr - (sizeof(IntPtr) * 2);
        return BambooMintKeyTextService.AddRefImpl(rootPtr);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
    {
        var rootPtr = thisPtr - (sizeof(IntPtr) * 2);
        return BambooMintKeyTextService.ReleaseImpl(rootPtr);
    }

    // =========================================================================
    // ITfKeyEventSink Callbacks
    // =========================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnSetFocus(IntPtr thisPtr, int fForeground)
    {
        DebugLog.Write($"KeyEventSink OnSetFocus fForeground={fForeground}");
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnTestKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        DebugLog.WriteAndFlush($"OnTestKeyDown ENTER vk={(uint)wParam}");
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        // 0. Kiểm tra phím tắt chuyển đổi chế độ V/E (Ctrl + Shift hoặc Alt + Z)
        if (KeyInputTranslator.IsToggleHotkeyPressed(wParam, lParam))
        {
            *pfEaten = 1;
            return HResult.Ok;
        }

        // 1. Không can thiệp nếu người dùng đang bấm tổ hợp phím tắt (Ctrl/Alt/Win)
        if (KeyInputTranslator.IsModifierModifierPressed())
        {
            DebugLog.Write("OnTestKeyDown modifier pressed, skip");
            return HResult.Ok;
        }

        // 1.1. Nếu đang ở chế độ tiếng Anh (E) -> Bỏ qua hoàn toàn, không nuốt phím
        if (!BridgeStateManager.IsVietnameseMode)
        {
            return HResult.Ok;
        }

        uint vkCode = (uint)wParam;

        // 2. Can thiệp nếu là Backspace khi đang có phiên Composition
        if (vkCode == KeyInputTranslator.VkBack && CompositionManager.HasActiveComposition())
        {
            *pfEaten = 1;
            return HResult.Ok;
        }

        // 3. Chuyển đổi mã phím sang ký tự để kiểm tra
        var inputChar = KeyInputTranslator.ConvertVirtualKeyToChar(wParam, lParam);
        if (inputChar.HasValue)
        {
            char c = inputChar.Value;
            DebugLog.WriteAndFlush($"OnTestKeyDown char={c}");
            // Nếu là ký tự bảng chữ cái hoặc đang có composition
            if (char.IsLetter(c) || CompositionManager.HasActiveComposition())
            {
                *pfEaten = 1;
                DebugLog.WriteAndFlush($"OnTestKeyDown EATEN char={c}");
            }
        }

        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnTestKeyUp(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        DebugLog.WriteAndFlush($"OnKeyDown ENTER vk={(uint)wParam}");
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        // 0. Bắt phím tắt chuyển đổi chế độ V/E (Ctrl + Shift hoặc Alt + Z)
        if (KeyInputTranslator.IsToggleHotkeyPressed(wParam, lParam))
        {
            bool newMode = BridgeStateManager.ToggleVietnameseMode();
            LangBarItemButton.NotifyStateChanged();
            DebugLog.Write($"OnKeyDown ToggleHotkey triggered! New IsVietnameseMode={newMode}");
            *pfEaten = 1;
            return HResult.Ok;
        }

        if (KeyInputTranslator.IsModifierModifierPressed())
        {
            return HResult.Ok;
        }

        // Nếu đang ở chế độ tiếng Anh (E) -> Bỏ qua hoàn toàn, không nuốt phím, không gõ dấu
        if (!BridgeStateManager.IsVietnameseMode)
        {
            if (CompositionManager.HasActiveComposition())
            {
                CompositionManager.EndComposition();
                BridgeStateManager.ResetState();
            }
            return HResult.Ok;
        }

        uint vkCode = (uint)wParam;
        var servicePtr = thisPtr - (sizeof(IntPtr) * 2);
        var service = BambooMintKeyTextService.GetTarget(servicePtr);

        // Trường hợp 1: Phím Backspace
        if (vkCode == KeyInputTranslator.VkBack)
        {
            if (CompositionManager.HasActiveComposition())
            {
                var (newState, action) = BridgeStateManager.ProcessBackspace();
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write($"OnKeyDown Backspace handled, text={newState.TransformedText}");
            }
            return HResult.Ok;
        }

        // Trường hợp 2: Phím Ký tự bình thường / Phím Ngắt
        var inputChar = KeyInputTranslator.ConvertVirtualKeyToChar(wParam, lParam);
        if (!inputChar.HasValue) return HResult.Ok;

        char c = inputChar.Value;

        if (KeyInputTranslator.IsWordBreakChar(c))
        {
            if (CompositionManager.HasActiveComposition())
            {
                var (newState, action) = BridgeStateManager.ProcessWordBreak(c);
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write($"OnKeyDown WordBreak handled, text={newState.TransformedText}");
            }
        }
        else
        {
            var (newState, action) = BridgeStateManager.ProcessKey(c);
            CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
            *pfEaten = 1;
            DebugLog.Write($"OnKeyDown ProcessKey char={c}, text={newState.TransformedText}");
        }

        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnKeyUp(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPreservedKey(IntPtr thisPtr, IntPtr pic, Guid* rguid, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;
        return HResult.Ok;
    }
}
