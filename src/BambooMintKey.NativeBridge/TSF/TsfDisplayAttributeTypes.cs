// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

public enum TfDaColorType
{
    TfCtNone = 0,
    TfCtSysColor = 1,
    TfCtColorRef = 2
}

[StructLayout(LayoutKind.Sequential)]
public struct TfDaColor
{
    public TfDaColorType Type;
    public int IndexOrColorRef;
}

public enum TfDaLineStyle
{
    TfLsNone = 0,
    TfLsSolid = 1,
    TfLsDot = 2,
    TfLsDash = 3,
    TfLsSquiggle = 4
}

public enum TfDaAttrInfo
{
    TfAttrInput = 0,
    TfAttrTargetConverted = 1,
    TfAttrConverted = 2,
    TfAttrTargetNotConverted = 3,
    TfAttrInputError = 4,
    TfAttrFixedConverted = 5,
    TfAttrOther = -1
}

[StructLayout(LayoutKind.Sequential)]
public struct TfDisplayAttribute
{
    public TfDaColor CrText;
    public TfDaColor CrBk;
    public TfDaLineStyle LsStyle;
    public int FBoldLine;
    public TfDaColor CrLine;
    public TfDaAttrInfo BAttr;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfDisplayAttributeProviderVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfDisplayAttributeProvider (3 - 4)
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumDisplayAttributeInfo;
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetDisplayAttributeInfo;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfDisplayAttributeInfoVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfDisplayAttributeInfo (3 - 7)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> GetGUID;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetDescription;
    public delegate* unmanaged[Stdcall]<IntPtr, TfDisplayAttribute*, int> GetAttributeInfo;
    public delegate* unmanaged[Stdcall]<IntPtr, TfDisplayAttribute*, int> SetAttributeInfo;
    public delegate* unmanaged[Stdcall]<IntPtr, int> Reset;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct EnumTfDisplayAttributeInfoVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // IEnumTfDisplayAttributeInfo (3 - 6)
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> Clone;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, uint*, int> Next;
    public delegate* unmanaged[Stdcall]<IntPtr, int> Reset;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> Skip;
}
