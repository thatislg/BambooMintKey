// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
namespace BambooMintKey.NativeBridge.Common;

/// <summary>
/// COM and Windows Text Services Framework (TSF) HRESULT return codes.
/// Mapping directly to Windows 11 SDK headers (WinError.h, msctf.h).
/// </summary>
public static class HResult
{
    // =========================================================================
    // COM Standard Success / Information Codes (WinError.h)
    // =========================================================================

    /// <summary>Operation successful. [SDK: S_OK (0x00000000)]</summary>
    public const int Ok = 0x00000000;

    /// <summary>Operation successful but returned false or no-op. [SDK: S_FALSE (0x00000001)]</summary>
    public const int False = 0x00000001;

    // =========================================================================
    // COM Standard Error Codes (WinError.h)
    // =========================================================================

    /// <summary>Unexpected failure. [SDK: E_UNEXPECTED (0x8000FFFF)]</summary>
    public const int Unexpected = unchecked((int)0x8000FFFF);

    /// <summary>Not implemented. [SDK: E_NOTIMPL (0x80004001)]</summary>
    public const int NotImplemented = unchecked((int)0x80004001);

    /// <summary>Failed to allocate necessary memory. [SDK: E_OUTOFMEMORY (0x8007000E)]</summary>
    public const int OutOfMemory = unchecked((int)0x8007000E);

    /// <summary>One or more arguments are invalid. [SDK: E_INVALIDARG (0x80070057)]</summary>
    public const int InvalidArgument = unchecked((int)0x80070057);

    /// <summary>No such interface supported. [SDK: E_NOINTERFACE (0x80004002)]</summary>
    public const int NoInterface = unchecked((int)0x80004002);

    /// <summary>Invalid pointer. [SDK: E_POINTER (0x80004003)]</summary>
    public const int Pointer = unchecked((int)0x80004003);

    /// <summary>Invalid handle. [SDK: E_HANDLE (0x80070006)]</summary>
    public const int Handle = unchecked((int)0x80070006);

    /// <summary>Operation aborted. [SDK: E_ABORT (0x80004004)]</summary>
    public const int Abort = unchecked((int)0x80004004);

    /// <summary>Unspecified failure. [SDK: E_FAIL (0x80004005)]</summary>
    public const int Fail = unchecked((int)0x80004005);

    /// <summary>General access denied error. [SDK: E_ACCESSDENIED (0x80070005)]</summary>
    public const int AccessDenied = unchecked((int)0x80070005);

    // =========================================================================
    // COM Class Factory Error Codes (WinError.h)
    // =========================================================================

    /// <summary>Class does not support aggregation. [SDK: CLASS_E_NOAGGREGATION (0x80040110)]</summary>
    public const int ClassNoAggregation = unchecked((int)0x80040110);

    /// <summary>Class not available. [SDK: CLASS_E_CLASSNOTAVAILABLE (0x80040111)]</summary>
    public const int ClassNotAvailable = unchecked((int)0x80040111);

    // =========================================================================
    // Windows TSF Specific Error Codes (msctf.h)
    // =========================================================================

    /// <summary>The document context is locked for read-only/read-write. [SDK: TF_E_LOCKED (0x80040500)]</summary>
    public const int TfLocked = unchecked((int)0x80040500);

    /// <summary>The edit session stack is full. [SDK: TF_E_STACKFULL (0x80040501)]</summary>
    public const int TfStackFull = unchecked((int)0x80040501);

    /// <summary>The caller does not own the text range. [SDK: TF_E_NOTOWNEDRANGE (0x80040502)]</summary>
    public const int TfNotOwnedRange = unchecked((int)0x80040502);

    /// <summary>No display attribute or property provider found. [SDK: TF_E_NOPROVIDER (0x80040503)]</summary>
    public const int TfNoProvider = unchecked((int)0x80040503);

    /// <summary>Context or thread manager is disconnected. [SDK: TF_E_DISCONNECTED (0x80040504)]</summary>
    public const int TfDisconnected = unchecked((int)0x80040504);

    /// <summary>The screen coordinate point is invalid. [SDK: TF_E_INVALIDPOINT (0x80040505)]</summary>
    public const int TfInvalidPoint = unchecked((int)0x80040505);

    /// <summary>Advise sink violation. [SDK: TF_E_ADVVIOLATION (0x80040506)]</summary>
    public const int TfAdviseViolation = unchecked((int)0x80040506);

    /// <summary>Document context is empty. [SDK: TF_E_EMPTYCONTEXT (0x80040507)]</summary>
    public const int TfEmptyContext = unchecked((int)0x80040507);

    /// <summary>Object or advise cookie already exists. [SDK: TF_E_ALREADY_EXISTS (0x80040508)]</summary>
    public const int TfAlreadyExists = unchecked((int)0x80040508);

    /// <summary>Operation requires an edit cookie lock. [SDK: TF_E_NOLOCK (0x80040509)]</summary>
    public const int TfNoLock = unchecked((int)0x80040509);

    /// <summary>The range is not in the document context. [SDK: TF_E_RANGE_NOT_IN_DOCUMENT (0x8004050A)]</summary>
    public const int TfRangeNotInDocument = unchecked((int)0x8004050A);

    /// <summary>Composition creation was rejected by the application. [SDK: TF_E_COMPOSITION_REJECTED (0x8004050B)]</summary>
    public const int TfCompositionRejected = unchecked((int)0x8004050B);

    // =========================================================================
    // Utility Methods
    // =========================================================================

    /// <summary>Evaluates to true if the HRESULT indicates success (>= 0). [SDK: SUCCEEDED(hr)]</summary>
    public static bool Succeeded(int hr) => hr >= 0;

    /// <summary>Evaluates to true if the HRESULT indicates failure (&lt; 0). [SDK: FAILED(hr)]</summary>
    public static bool Failed(int hr) => hr < 0;
}