// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

/// <summary>
/// Cài đặt ITfDisplayAttributeInfo định nghĩa thuộc tính hiển thị composition của BambooMintKey.
/// Cung cấp 2 display attribute cố định:
///   - Stealth (không gạch chân): GuidDisplayAttributeInput
///   - Preedit (gạch chân nét đứt): GuidDisplayAttributeInputPreedit
/// Dùng 2 GUID riêng biệt để tránh các ứng dụng/Chromium cache sai khi người dùng đổi cấu hình.
/// </summary>
public static unsafe class DisplayAttributeInfoImpl
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeLayout
    {
        public IntPtr VTable;
        public byte IsPreedit; // 0 = stealth (no underline), 1 = preedit (dotted underline)
    }

    private static TfDisplayAttributeInfoVTable* _vTable;
    private static IntPtr _stealthInstance = IntPtr.Zero;
    private static IntPtr _preeditInstance = IntPtr.Zero;

    public static IntPtr GetStealthInstance()
    {
        if (_stealthInstance != IntPtr.Zero) return _stealthInstance;
        return GetOrCreateInstanceInternal(false);
    }

    public static IntPtr GetPreeditInstance()
    {
        if (_preeditInstance != IntPtr.Zero) return _preeditInstance;
        return GetOrCreateInstanceInternal(true);
    }

    private static IntPtr GetOrCreateInstanceInternal(bool isPreedit)
    {
        if (_vTable == null)
        {
            _vTable = (TfDisplayAttributeInfoVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
                typeof(DisplayAttributeInfoImpl), sizeof(TfDisplayAttributeInfoVTable));

            _vTable->QueryInterface = &QueryInterface;
            _vTable->AddRef = &AddRef;
            _vTable->Release = &Release;
            _vTable->GetGUID = &GetGUID;
            _vTable->GetDescription = &GetDescription;
            _vTable->GetAttributeInfo = &GetAttributeInfo;
            _vTable->SetAttributeInfo = &SetAttributeInfo;
            _vTable->Reset = &Reset;
        }

        var pMem = (NativeLayout*)Marshal.AllocHGlobal(sizeof(NativeLayout));
        pMem->VTable = (IntPtr)_vTable;
        pMem->IsPreedit = (byte)(isPreedit ? 1 : 0);

        var instance = (IntPtr)pMem;
        if (isPreedit) _preeditInstance = instance;
        else _stealthInstance = instance;

        return instance;
    }

    private static bool IsPreedit(IntPtr thisPtr)
    {
        var layout = (NativeLayout*)thisPtr;
        return layout->IsPreedit != 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;
        *ppvObject = IntPtr.Zero;

        if (*riid == Guids.IidIUnknown || *riid == Guids.IidITfDisplayAttributeInfo)
        {
            *ppvObject = thisPtr;
            var vtable = *(TfDisplayAttributeInfoVTable**)thisPtr;
            vtable->AddRef(thisPtr);
            return HResult.Ok;
        }

        return HResult.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr) => 2;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetGUID(IntPtr thisPtr, Guid* pguid)
    {
        if (pguid == null) return HResult.Pointer;
        *pguid = IsPreedit(thisPtr) ? Guids.GuidDisplayAttributeInputPreedit : Guids.GuidDisplayAttributeInput;
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetDescription(IntPtr thisPtr, IntPtr* pbstrDesc)
    {
        if (pbstrDesc == null) return HResult.Pointer;
        string desc = IsPreedit(thisPtr)
            ? "BambooMintKey Input Display Attribute (Preedit)"
            : "BambooMintKey Input Display Attribute (Stealth)";
        *pbstrDesc = Marshal.StringToBSTR(desc);
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetAttributeInfo(IntPtr thisPtr, TfDisplayAttribute* pda)
    {
        if (pda == null) return HResult.Pointer;

        bool isPreedit = IsPreedit(thisPtr);

        pda->CrText.Type = TfDaColorType.TfCtNone;
        pda->CrText.IndexOrColorRef = 0;

        pda->CrBk.Type = TfDaColorType.TfCtNone;
        pda->CrBk.IndexOrColorRef = 0;

        if (isPreedit)
        {
            // Hiển thị gạch chân nét chấm chuẩn TSF
            pda->LsStyle = TfDaLineStyle.TfLsDot;
            pda->FBoldLine = 0;
            pda->CrLine.Type = TfDaColorType.TfCtNone;
            pda->CrLine.IndexOrColorRef = 0;
            pda->BAttr = TfDaAttrInfo.TfAttrInput;
        }
        else
        {
            // Ẩn hoàn toàn gạch chân (Stealth / Seamless như Notepad++)
            pda->LsStyle = TfDaLineStyle.TfLsNone;
            pda->FBoldLine = 0;
            pda->CrLine.Type = TfDaColorType.TfCtNone;
            pda->CrLine.IndexOrColorRef = 0;
            // Dùng TfAttrOther (-1) hoặc TfAttrConverted (2) thay vì TfAttrInput (0)
            // để đánh lừa các ứng dụng dùng chuẩn cũ (IMM32) hoặc Chrome Omnibox không vẽ gạch chân Input.
            pda->BAttr = (TfDaAttrInfo)255; // Giá trị ảo hoặc TfAttrOther
        }

        DebugLog.Write($"GetAttributeInfo: IsPreedit={isPreedit}, LsStyle={pda->LsStyle}, BAttr={pda->BAttr}");

        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SetAttributeInfo(IntPtr thisPtr, TfDisplayAttribute* pda)
    {
        return HResult.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Reset(IntPtr thisPtr)
    {
        return HResult.Ok;
    }
}
