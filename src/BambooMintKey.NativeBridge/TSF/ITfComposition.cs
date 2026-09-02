// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;

namespace BambooMintKey.NativeBridge.TSF;

public static class TsfEditFlags
{
    public const uint TfEsAsyncdontcare = 0x00000000;
    public const uint TfEsSync          = 0x00000001;
    public const uint TfEsRead          = 0x00000002;
    public const uint TfEsReadWrite     = 0x00000006;
    public const uint TfEsAsync         = 0x00000008;

    public const uint TfAnchorStart     = 0;
    public const uint TfAnchorEnd       = 1;
}

[StructLayout(LayoutKind.Sequential)]
public struct TfSelection
{
    public IntPtr range; // ITfRange*
    public uint styleAse;
    public uint styleFse;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfEditSessionVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> DoEditSession;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCompositionSinkVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int> OnCompositionTerminated;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfCompositionVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> GetRange;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int> EndComposition;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfRangeVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, char*, int, int*, int> GetText;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, char*, int, int> SetText;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, char*, int, int*, int> GetFormattedText;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int> Collapse;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> ShiftStart;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int> ShiftEnd;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int, int*, int> ShiftStartRegion;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int, int*, int> ShiftEndRegion;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, int*, int> IsEmpty;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, uint, int*, int> CompareStart;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, uint, int*, int> CompareEnd;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int> SetStoreOps;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> Clone;
}