using System;
using System.Runtime.InteropServices;
using Xunit;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.Interop;
using BambooMintKey.NativeBridge.TSF;

namespace BambooMintKey.NativeBridge.Tests;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct TestLangBarItemInfo
{
    public Guid clsidService;
    public Guid guidItem;
    public uint dwStyle;
    public uint ulSort;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDescription;
}

[ComImport, Guid("BA468C55-9956-4FB1-A59D-52A7DD7CC6AA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITestLangBarItemMgr
{
    void EnumItems(out ITestEnumLangBarItems ppEnum);
    void GetItem(ref Guid rguid, out IntPtr ppItem);
    void AddItem(IntPtr punk);
    void RemoveItem(IntPtr punk);
    void AdviseItemSink(IntPtr punk, IntPtr punkSink, ref Guid rguid, out uint pdwCookie);
    void UnadviseItemSink(uint dwCookie);
    void GetItemFloatingRect(uint dwCookie, ref Guid rguid, out IntPtr prc);
    void GetItemsStatus(uint ulCount, [In] Guid[] prgguid, [Out] uint[] pdwStatus);
    void GetItemStatus(ref Guid rguid, out uint pdwStatus);
    void GetShowFloatingStatus(out uint pdwFlags);
}

[ComImport, Guid("583F34D0-DE25-11D2-AFDD-00105A2799B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITestEnumLangBarItems
{
    [PreserveSig] int Clone(out ITestEnumLangBarItems ppEnum);
    [PreserveSig] int Next(uint ulCount, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] ppItem, out uint pcFetched);
    [PreserveSig] int Reset();
    [PreserveSig] int Skip(uint ulCount);
}

[ComImport, Guid("73540D69-EDEB-4EE9-96C9-23AA30B25916"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITestLangBarItem
{
    void GetInfo(out TestLangBarItemInfo pInfo);
    void GetStatus(out uint pdwStatus);
    void Show(bool fShow);
    void GetTooltipString(out IntPtr pbstrToolTip);
}

public unsafe class LangBarItemLifecycleTests
{
    [DllImport("msctf.dll")]
    private static extern int TF_CreateThreadMgr(out IntPtr ppThreadMgr);

    [DllImport("msctf.dll")]
    private static extern int TF_CreateLangBarItemMgr(out ITestLangBarItemMgr ppItemMgr);

    private static bool IsBambooMintKeyItemInLangBar()
    {
        int hr = TF_CreateLangBarItemMgr(out var mgr);
        if (hr != 0 || mgr == null) return false;

        mgr.EnumItems(out var enumerator);
        if (enumerator == null) return false;

        IntPtr[] items = new IntPtr[1];
        while (enumerator.Next(1, items, out uint fetched) == 0 && fetched == 1)
        {
            IntPtr pItem = items[0];
            try
            {
                var item = (ITestLangBarItem)Marshal.GetObjectForIUnknown(pItem);
                item.GetInfo(out var info);
                if (info.guidItem == Guids.GuidLbiInputMode && info.clsidService == Guids.TextServiceClsid)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore any third-party items that fail QI
            }
            finally
            {
                Marshal.Release(pItem);
            }
        }

        return false;
    }

    [Fact]
    public void LangBarItemButton_GetInfo_HasCorrectStyleAndGuid()
    {
        IntPtr pInstance = LangBarItemButton.Instance;
        Assert.NotEqual(IntPtr.Zero, pInstance);

        var vtable = *(TfLangBarItemButtonVTable**)pInstance;
        TfLangbariteminfo info;
        int hr = vtable->GetInfo(pInstance, &info);
        Assert.Equal(0, hr);

        Assert.Equal(Guids.TextServiceClsid, info.clsidService);
        Assert.Equal(Guids.GuidLbiInputMode, info.guidItem);
        Assert.Equal(TsfLangBarFlags.TfLbiStyleBtnButton | TsfLangBarFlags.TfLbiStyleShownInTray | TsfLangBarFlags.TfLbiStyleHiddenStatusControl, info.dwStyle);
    }

    [Fact]
    public void LangBarItemButton_GetStatus_ReflectsVietnameseMode()
    {
        IntPtr pInstance = LangBarItemButton.Instance;
        var vtable = *(TfLangBarItemButtonVTable**)pInstance;

        TF_CreateThreadMgr(out IntPtr pThreadMgr);
        try
        {
            // 1. Khi chưa đăng ký (unregistered) -> Phải trả về StatusHidden
            LangBarItemButton.Unregister();
            uint status = 0;
            int hr = vtable->GetStatus(pInstance, &status);
            Assert.Equal(0, hr);
            Assert.Equal(TsfLangBarFlags.TfLbiStatusHidden, status);

            // 2. Khi đã đăng ký và chế độ tiếng Việt (VN mode) -> Trả về BtnToggled
            LangBarItemButton.Register(pThreadMgr, 1);
            BridgeStateManager.IsVietnameseMode = true;
            hr = vtable->GetStatus(pInstance, &status);
            Assert.Equal(0, hr);
            Assert.Equal(TsfLangBarFlags.TfLbiStatusBtnToggled, status);

            // 3. Khi đã đăng ký và chế độ tiếng Anh (EN mode) -> Trả về 0
            BridgeStateManager.IsVietnameseMode = false;
            hr = vtable->GetStatus(pInstance, &status);
            Assert.Equal(0, hr);
            Assert.Equal(0u, status);

            // Restore to true & unregister
            BridgeStateManager.IsVietnameseMode = true;
            LangBarItemButton.Unregister();
        }
        finally
        {
            if (pThreadMgr != IntPtr.Zero) Marshal.Release(pThreadMgr);
        }
    }

    [Fact]
    public void LangBarItemButton_RegisterAndUnregister_AddsAndRemovesFromLanguageBar()
    {
        int hrMgr = TF_CreateThreadMgr(out IntPtr pThreadMgr);
        Assert.Equal(0, hrMgr);
        Assert.NotEqual(IntPtr.Zero, pThreadMgr);

        try
        {
            // 1. Trước khi Register: Không có BambooMintKey trong Language Bar (hoặc đảm bảo Unregistered)
            LangBarItemButton.Unregister();
            Assert.False(IsBambooMintKeyItemInLangBar(), "BambooMintKey item must NOT be in Language Bar after Unregister");

            // 2. Register: Phải xuất hiện trong Language Bar
            LangBarItemButton.Register(pThreadMgr, 1);
            Assert.True(IsBambooMintKeyItemInLangBar(), "BambooMintKey item MUST be in Language Bar after Register");

            // 3. Unregister: Phải bị gỡ hoàn toàn khỏi Language Bar (nhường slot cho IME khác)
            LangBarItemButton.Unregister();
            Assert.False(IsBambooMintKeyItemInLangBar(), "BambooMintKey item MUST be removed from Language Bar after Unregister");

            // 4. Register lại: Phải tái xuất hiện thành công
            LangBarItemButton.Register(pThreadMgr, 1);
            Assert.True(IsBambooMintKeyItemInLangBar(), "BambooMintKey item MUST reappear in Language Bar after re-registering");

            // Cleanup
            LangBarItemButton.Unregister();
            Assert.False(IsBambooMintKeyItemInLangBar());
        }
        finally
        {
            Marshal.Release(pThreadMgr);
        }
    }

    [Fact]
    public void TextService_ActivateAndDeactivate_LifecycleManagesLangBarItem()
    {
        int hrMgr = TF_CreateThreadMgr(out IntPtr pThreadMgr);
        Assert.Equal(0, hrMgr);
        Assert.NotEqual(IntPtr.Zero, pThreadMgr);

        IntPtr pService = BambooMintKeyTextService.CreateNativeInstance();
        Assert.NotEqual(IntPtr.Zero, pService);

        try
        {
            var processorVTable = *(TfTextInputProcessorExVTable**)pService;

            // 1. Đảm bảo trạng thái ban đầu sạch
            LangBarItemButton.Unregister();
            Assert.False(IsBambooMintKeyItemInLangBar());

            // 2. Kích hoạt bộ gõ (ActivateEx) -> IsBambooMintKeyActive bật, Taskbar TSF KHÔNG bị chiếm slot (chuẩn tách biệt Tray)
            int hrAct = processorVTable->ActivateEx(pService, pThreadMgr, 1, 0);
            Assert.Equal(0, hrAct);
            Assert.True(SharedMemoryManager.IsBambooMintKeyActive, "IsBambooMintKeyActive MUST be true when Activated");
            Assert.False(IsBambooMintKeyItemInLangBar(), "Taskbar LangBar slot MUST remain clean for other IMEs");

            // 3. Chuyển sang bộ gõ khác (Deactivate) -> IsBambooMintKeyActive tắt
            int hrDeact = processorVTable->Deactivate(pService);
            Assert.Equal(0, hrDeact);
            Assert.False(SharedMemoryManager.IsBambooMintKeyActive, "IsBambooMintKeyActive MUST be false when Deactivated");
            Assert.False(IsBambooMintKeyItemInLangBar());

            // 4. Kích hoạt lại (ActivateEx) -> IsBambooMintKeyActive bật lại
            hrAct = processorVTable->ActivateEx(pService, pThreadMgr, 1, 0);
            Assert.Equal(0, hrAct);
            Assert.True(SharedMemoryManager.IsBambooMintKeyActive);

            // 5. Cleanup
            processorVTable->Deactivate(pService);
            Assert.False(SharedMemoryManager.IsBambooMintKeyActive);
        }
        finally
        {
            var processorVTable = *(TfTextInputProcessorExVTable**)pService;
            processorVTable->Release(pService);
            Marshal.Release(pThreadMgr);
        }
    }

    [Fact]
    public void VietnameseTyping_PreservedAcrossActivateDeactivateCycles()
    {
        int hrMgr = TF_CreateThreadMgr(out IntPtr pThreadMgr);
        Assert.Equal(0, hrMgr);

        IntPtr pService = BambooMintKeyTextService.CreateNativeInstance();
        try
        {
            var processorVTable = *(TfTextInputProcessorExVTable**)pService;

            // Ensure Vietnamese mode is enabled
            BridgeStateManager.IsVietnameseMode = true;

            // Cycle 1: Activate -> Deactivate
            processorVTable->ActivateEx(pService, pThreadMgr, 1, 0);
            processorVTable->Deactivate(pService);

            // Cycle 2: Activate again
            processorVTable->ActivateEx(pService, pThreadMgr, 1, 0);

            // Test typing "tiengs" -> produces "tiếng"
            BridgeStateManager.ResetState();
            BridgeStateManager.ProcessKey('t');
            BridgeStateManager.ProcessKey('i');
            BridgeStateManager.ProcessKey('e');
            BridgeStateManager.ProcessKey('n');
            BridgeStateManager.ProcessKey('g');
            var (state, _) = BridgeStateManager.ProcessKey('s');

            // Khẳng định chữ tiếng Việt được tạo đúng và chế độ V vẫn giữ nguyên
            Assert.Equal("tiếng", state.TransformedText);
            Assert.True(BridgeStateManager.IsVietnameseMode);

            processorVTable->Deactivate(pService);
        }
        finally
        {
            var processorVTable = *(TfTextInputProcessorExVTable**)pService;
            processorVTable->Release(pService);
            Marshal.Release(pThreadMgr);
        }
    }
}
