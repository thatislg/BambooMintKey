// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.Core.Domain;
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
        if (fForeground == 0)
        {
            CandidatePopupWindow.Hide();
        }
        DebugLog.Write($"KeyEventSink OnSetFocus fForeground={fForeground}");
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnTestKeyDown(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        try
        {
            var servicePtr = thisPtr - (sizeof(IntPtr) * 2);
            var service = BambooMintKeyTextService.GetTarget(servicePtr);
            if (service != null)
            {
                if (service.IsSecureMode) return HResult.Ok;
                service.SyncCompartmentIfStaleOnStaThread();
            }

            // 1. Nếu đang ở chế độ tiếng Anh (E / Closed) -> Pass-through ngay lập tức, không nuốt phím
            if (!BridgeStateManager.IsVietnameseMode)
            {
                return HResult.Ok;
            }

            // 2. Không can thiệp nếu người dùng đang bấm tổ hợp phím tắt (Ctrl/Alt/Win)
            if (KeyInputTranslator.IsModifierModifierPressed())
            {
                return HResult.Ok;
            }

            uint vkCode = (uint)wParam;
            bool hasComp = CompositionManager.HasActiveComposition();
            bool inSymbol = BridgeStateManager.IsSymbolModeActive;

            // 3. Can thiệp nếu là Backspace khi đang có phiên Composition
            if (vkCode == KeyInputTranslator.VkBack && hasComp)
            {
                *pfEaten = 1;
                return HResult.Ok;
            }

            // 3b. Khi đang ở Symbol mode: nuốt Esc, Tab (kể cả Shift+Tab) và 4 phím mũi tên
            if (inSymbol && (vkCode == KeyInputTranslator.VkEscape ||
                             vkCode == KeyInputTranslator.VkTab ||
                             vkCode == KeyInputTranslator.VkUp ||
                             vkCode == KeyInputTranslator.VkDown ||
                             vkCode == KeyInputTranslator.VkLeft ||
                             vkCode == KeyInputTranslator.VkRight))
            {
                *pfEaten = 1;
                return HResult.Ok;
            }

            // 4. Chuyển đổi mã phím sang ký tự để kiểm tra
            var inputChar = KeyInputTranslator.ConvertVirtualKeyToChar(wParam, lParam);
            if (inputChar.HasValue)
            {
                char c = inputChar.Value;
                // Nếu là ký tự bảng chữ cái, đang có composition, hoặc ':' hợp lệ ở đầu từ (kích hoạt "::")
                if (char.IsLetter(c) || hasComp ||
                    (c == ':' && BridgeStateManager.CanStartSymbolTrigger && BridgeStateManager.Config.EnableSymbolShortcode))
                {
                    *pfEaten = 1;
                }
            }
        }
        catch (Exception ex)
        {
            *pfEaten = 0;
            DebugLog.WriteAndFlush($"OnTestKeyDown exception: {ex.GetType().Name}: {ex.Message}");
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
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        try
        {
            return OnKeyDownCore(thisPtr, pic, wParam, lParam, pfEaten);
        }
        catch (Exception ex)
        {
            *pfEaten = 0;
            CandidatePopupWindow.Hide();
            BridgeStateManager.ResetState();
            DebugLog.WriteAndFlush($"OnKeyDown exception: {ex.GetType().Name}: {ex.Message}");
            return HResult.Ok;
        }
    }

    private static int OnKeyDownCore(IntPtr thisPtr, IntPtr pic, UIntPtr wParam, IntPtr lParam, int* pfEaten)
    {
        uint vkCode = (uint)wParam;
        var servicePtr = thisPtr - (sizeof(IntPtr) * 2);
        var service = BambooMintKeyTextService.GetTarget(servicePtr);
        if (service != null && service.IsSecureMode)
        {
            return HResult.Ok;
        }

        // 1. Phím tắt hệ thống có modifier (Ctrl/Alt/Win)
        if (KeyInputTranslator.IsModifierModifierPressed())
        {
            // Đang có tổ hợp phím tắt (Ctrl+A, Ctrl+C...):
            // Chốt từ dở dang để giải phóng composition, tránh treo trạng thái (Issue 020)
            if (CompositionManager.HasActiveComposition() && service != null)
            {
                if (BridgeStateManager.IsSymbolModeActive)
                {
                    var (newState, action) = BridgeStateManager.ProcessNonCharacter();
                    CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                }
                else
                {
                    var curText = BridgeStateManager.CurrentState.TransformedText;
                    CompositionManager.HandleEngineAction(service, pic, Types.EngineAction.NewCommit(curText), curText);
                }
                DebugLog.Write("OnKeyDown Modifier pressed with active composition -> committed");
            }
            return HResult.Ok;
        }

        // 2. Nếu đang ở chế độ tiếng Anh (E) -> Bỏ qua hoàn toàn, không nuốt phím, không gõ dấu
        if (!BridgeStateManager.IsVietnameseMode)
        {
            CandidatePopupWindow.Hide();
            if (CompositionManager.HasActiveComposition())
            {
                CompositionManager.EndComposition();
                BridgeStateManager.ResetState();
            }
            return HResult.Ok;
        }

        // 3. Trường hợp 1: Phím Backspace
        if (vkCode == KeyInputTranslator.VkBack)
        {
            if (CompositionManager.HasActiveComposition() && service != null)
            {
                var (newState, action) = BridgeStateManager.ProcessBackspace();
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write($"OnKeyDown Backspace handled, text={newState.TransformedText}");
            }
            return HResult.Ok;
        }

        // 3b. Phím Escape, Tab và 4 phím mũi tên khi đang ở Symbol mode (xử lý trước ConvertVirtualKeyToChar)
        if (BridgeStateManager.IsSymbolModeActive && service != null)
        {
            if (vkCode == KeyInputTranslator.VkEscape)
            {
                var (newState, action) = BridgeStateManager.ProcessEscape();
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write("OnKeyDown Symbol Escape handled");
                return HResult.Ok;
            }
            if (vkCode == KeyInputTranslator.VkTab)
            {
                bool isShift = KeyInputTranslator.IsShiftPressed();
                var (newState, action) = BridgeStateManager.ProcessTab(isShift);
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write($"OnKeyDown Symbol Tab (shift={isShift}) handled, text={newState.TransformedText}");
                return HResult.Ok;
            }
            if (vkCode == KeyInputTranslator.VkUp || vkCode == KeyInputTranslator.VkDown ||
                vkCode == KeyInputTranslator.VkLeft || vkCode == KeyInputTranslator.VkRight)
            {
                var arrow = vkCode switch
                {
                    KeyInputTranslator.VkUp => Types.KeyInput.ArrowUp,
                    KeyInputTranslator.VkDown => Types.KeyInput.ArrowDown,
                    KeyInputTranslator.VkLeft => Types.KeyInput.ArrowLeft,
                    _ => Types.KeyInput.ArrowRight
                };
                var (newState, action) = BridgeStateManager.ProcessArrow(arrow);
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write($"OnKeyDown Symbol Arrow vk=0x{vkCode:X2} handled");
                return HResult.Ok;
            }
        }

        // 4. Trường hợp 2: Phím Ký tự bình thường / Phím Ngắt
        var inputChar = KeyInputTranslator.ConvertVirtualKeyToChar(wParam, lParam);
        if (!inputChar.HasValue)
        {
            // Bỏ qua các modifier thuần (Shift, CapsLock...) để người dùng tiếp tục gõ hoa
            if (KeyInputTranslator.IsPureModifierKey(vkCode))
            {
                return HResult.Ok;
            }

            // Với phím điều hướng / không in được (Mũi tên, Home, End, Esc, Tab...):
            // Chốt từ dở dang trước khi nhả phím cho ứng dụng (Issue 021)
            if (CompositionManager.HasActiveComposition() && service != null)
            {
                if (BridgeStateManager.IsSymbolModeActive)
                {
                    var (newState, action) = BridgeStateManager.ProcessNonCharacter();
                    CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                }
                else
                {
                    var curText = BridgeStateManager.CurrentState.TransformedText;
                    CompositionManager.HandleEngineAction(service, pic, Types.EngineAction.NewCommit(curText), curText);
                }
                DebugLog.Write($"OnKeyDown Nav/Non-printable vkCode=0x{vkCode:X2} with active composition -> committed");
            }
            else
            {
                BridgeStateManager.ResetTriggerBoundary();
            }
            return HResult.Ok;
        }

        char c = inputChar.Value;

        if (KeyInputTranslator.IsWordBreakChar(c))
        {
            bool canStartColon = c == ':' && BridgeStateManager.CanStartSymbolTrigger && BridgeStateManager.Config.EnableSymbolShortcode;
            if ((CompositionManager.HasActiveComposition() || canStartColon) && service != null)
            {
                var (newState, action) = BridgeStateManager.ProcessWordBreak(c);
                CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
                *pfEaten = 1;
                DebugLog.Write($"OnKeyDown WordBreak handled, text={newState.TransformedText}");
            }
            else
            {
                BridgeStateManager.NotifyNonEatenChar(c);
            }
        }
        else if ((char.IsLetter(c) || CompositionManager.HasActiveComposition()) && service != null)
        {
            var (newState, action) = BridgeStateManager.ProcessKey(c);
            CompositionManager.HandleEngineAction(service, pic, action, newState.TransformedText);
            *pfEaten = 1;
            DebugLog.Write($"OnKeyDown ProcessKey char={c}, text={newState.TransformedText}");
        }
        else
        {
            BridgeStateManager.NotifyNonEatenChar(c);
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
        DebugLog.Write($"OnPreservedKey ENTER rguid={(rguid != null ? (*rguid).ToString() : "null")}");
        if (pfEaten == null) return HResult.Pointer;
        *pfEaten = 0;

        if (rguid != null && *rguid == Guids.GuidPreservedKeyToggle)
        {
            var target = BambooMintKeyTextService.GetTarget(thisPtr - (sizeof(IntPtr) * 2));
            if (target != null && target.IsSecureMode)
            {
                return HResult.Ok;
            }
            if (target != null && target.ThreadMgr != IntPtr.Zero)
            {
                bool newMode = GlobalVEState.ToggleVietnameseMode(
                    GlobalVEState.SyncTarget.All,
                    target.ThreadMgr,
                    target.ClientId);
                DebugLog.Write($"OnPreservedKey Toggle executed! New IsVietnameseMode={newMode}");
            }
            *pfEaten = 1;
            return HResult.Ok;
        }

        return HResult.Ok;
    }
}
