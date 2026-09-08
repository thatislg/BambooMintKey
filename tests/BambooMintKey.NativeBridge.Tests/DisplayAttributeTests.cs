using System;
using System.Runtime.InteropServices;
using Xunit;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.TSF;
using System.Reflection;

namespace BambooMintKey.NativeBridge.Tests;

public unsafe class DisplayAttributeTests
{
    [Fact]
    public void Guids_MatchWindowsSdk()
    {
        // 1. Kiểm tra IIDs chuẩn
        Assert.Equal(new Guid("fee47777-163c-4769-996a-6e9c50ad8f54"), Guids.IidITfDisplayAttributeProvider);
        Assert.Equal(new Guid("70528852-2f26-4aea-8c96-215150578932"), Guids.IidITfDisplayAttributeInfo);
        Assert.Equal(new Guid("7cef04d7-cb75-4e80-a7ab-5f5bc7d332de"), Guids.IidIEnumTfDisplayAttributeInfo);
        Assert.Equal(new Guid("34b45670-7526-11d2-a147-00105a2799b5"), Guids.GuidPropAttribute);
    }

    [Fact]
    public void DisplayAttributeInfo_Stealth_HasCorrectAttributes()
    {
        // Use reflection to call GetStealthInstance since it might be internal
        var method = typeof(DisplayAttributeInfoImpl).GetMethod("GetStealthInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        
        IntPtr ptr = (IntPtr)method.Invoke(null, null)!;
        Assert.NotEqual(IntPtr.Zero, ptr);

        TfDisplayAttributeInfoVTable* vtable = *(TfDisplayAttributeInfoVTable**)ptr;
        
        // Test GetGUID
        Guid guid;
        int hrGuid = vtable->GetGUID(ptr, &guid);
        Assert.Equal(0, hrGuid);
        Assert.Equal(Guids.GuidDisplayAttributeInput, guid);

        // Test GetAttributeInfo
        TfDisplayAttribute attr;
        int hrAttr = vtable->GetAttributeInfo(ptr, &attr);
        Assert.Equal(0, hrAttr);
        Assert.Equal(TfDaLineStyle.TfLsNone, attr.LsStyle);
        Assert.Equal(TfDaColorType.TfCtNone, attr.CrLine.Type);
    }

    [Fact]
    public void DisplayAttributeInfo_Preedit_HasCorrectAttributes()
    {
        var method = typeof(DisplayAttributeInfoImpl).GetMethod("GetPreeditInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        
        IntPtr ptr = (IntPtr)method.Invoke(null, null)!;
        Assert.NotEqual(IntPtr.Zero, ptr);

        TfDisplayAttributeInfoVTable* vtable = *(TfDisplayAttributeInfoVTable**)ptr;
        
        // Test GetGUID
        Guid guid;
        int hrGuid = vtable->GetGUID(ptr, &guid);
        Assert.Equal(0, hrGuid);
        Assert.Equal(Guids.GuidDisplayAttributeInputPreedit, guid);

        // Test GetAttributeInfo
        TfDisplayAttribute attr;
        int hrAttr = vtable->GetAttributeInfo(ptr, &attr);
        Assert.Equal(0, hrAttr);
        Assert.Equal(TfDaLineStyle.TfLsDot, attr.LsStyle);
    }

    [Fact]
    public void EnumDisplayAttributeInfo_ReturnsBothInstances()
    {
        var method = typeof(EnumDisplayAttributeInfoImpl).GetMethod("CreateInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);

        IntPtr ptr = (IntPtr)method.Invoke(null, null)!;
        Assert.NotEqual(IntPtr.Zero, ptr);

        EnumTfDisplayAttributeInfoVTable* vtable = *(EnumTfDisplayAttributeInfoVTable**)ptr;

        // Reset
        vtable->Reset(ptr);

        // Next(1) - Lần 1
        IntPtr item1;
        uint fetched1;
        int hr1 = vtable->Next(ptr, 1, &item1, &fetched1);
        Assert.Equal(0, hr1);
        Assert.Equal(1u, fetched1);

        // Verify item 1 is Stealth
        TfDisplayAttributeInfoVTable* item1Vtable = *(TfDisplayAttributeInfoVTable**)item1;
        Guid guid1;
        item1Vtable->GetGUID(item1, &guid1);
        Assert.Equal(Guids.GuidDisplayAttributeInput, guid1);

        // Next(1) - Lần 2
        IntPtr item2;
        uint fetched2;
        int hr2 = vtable->Next(ptr, 1, &item2, &fetched2);
        Assert.Equal(0, hr2);
        Assert.Equal(1u, fetched2);

        // Verify item 2 is Preedit
        TfDisplayAttributeInfoVTable* item2Vtable = *(TfDisplayAttributeInfoVTable**)item2;
        Guid guid2;
        item2Vtable->GetGUID(item2, &guid2);
        Assert.Equal(Guids.GuidDisplayAttributeInputPreedit, guid2);

        // Next(1) - Lần 3 (End of enumeration)
        IntPtr item3;
        uint fetched3;
        int hr3 = vtable->Next(ptr, 1, &item3, &fetched3);
        Assert.Equal(1, hr3); // S_FALSE
        Assert.Equal(0u, fetched3);
    }

    [Fact]
    public void COM_DisplayAttributeMgr_IntegrationTest()
    {
        var method = typeof(BambooMintKeyTextService).GetMethod("CreateNativeInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);

        IntPtr rootPtr = (IntPtr)method.Invoke(null, null)!;
        Assert.NotEqual(IntPtr.Zero, rootPtr);

        // The interface pointer for ITfDisplayAttributeProvider is at offset 3 * sizeof(IntPtr)
        IntPtr providerPtr = rootPtr + (sizeof(IntPtr) * 3);
        
        TfDisplayAttributeProviderVTable* vtable = *(TfDisplayAttributeProviderVTable**)providerPtr;

        // Query GetDisplayAttributeInfo for Stealth
        Guid stealthGuid = Guids.GuidDisplayAttributeInput;
        IntPtr pInfoStealth;
        int hrStealth = vtable->GetDisplayAttributeInfo(providerPtr, &stealthGuid, &pInfoStealth);
        Assert.Equal(0, hrStealth);
        Assert.NotEqual(IntPtr.Zero, pInfoStealth);

        // Verify Stealth LsStyle
        TfDisplayAttributeInfoVTable* infoStealthVtable = *(TfDisplayAttributeInfoVTable**)pInfoStealth;
        TfDisplayAttribute attrStealth;
        infoStealthVtable->GetAttributeInfo(pInfoStealth, &attrStealth);
        Assert.Equal(TfDaLineStyle.TfLsNone, attrStealth.LsStyle);

        // Query GetDisplayAttributeInfo for Preedit
        Guid preeditGuid = Guids.GuidDisplayAttributeInputPreedit;
        IntPtr pInfoPreedit;
        int hrPreedit = vtable->GetDisplayAttributeInfo(providerPtr, &preeditGuid, &pInfoPreedit);
        Assert.Equal(0, hrPreedit);
        Assert.NotEqual(IntPtr.Zero, pInfoPreedit);

        // Verify Preedit LsStyle
        TfDisplayAttributeInfoVTable* infoPreeditVtable = *(TfDisplayAttributeInfoVTable**)pInfoPreedit;
        TfDisplayAttribute attrPreedit;
        infoPreeditVtable->GetAttributeInfo(pInfoPreedit, &attrPreedit);
        Assert.Equal(TfDaLineStyle.TfLsDot, attrPreedit.LsStyle);
    }
}
