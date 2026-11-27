// BambooMintKey - Vietnamese Telex Input Method Editor for Windows
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.Core.Domain;
using BambooMintKey.NativeBridge.COM;
using BambooMintKey.NativeBridge.Common;
using BambooMintKey.NativeBridge.TSF;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BambooMintKey.NativeBridge.Tests;

/// <summary>
/// Bộ test hồi quy chuyên biệt cho kiến trúc Out-of-process Avalonia Popup (013_03):
/// 1. Cấm F# sprintf/failwithf trong Symbol Core (chặn crash NativeAOT 0xc0000409).
/// 2. Cấm gọi COM ITfContext::GetActiveView / ITfContextView::GetTextExt và cấm tạo cửa sổ Win32 GDI
///    trong NativeBridge (chặn triệt để crash WinUIEdit.dll / MSCTF.dll 0xc0000005).
/// 3. Kiểm chứng giao thức IPC SharedMemory (ShowOrUpdate / Hide) và fallback Inline Preview an toàn.
/// 4. Nạp trực tiếp binary NativeAOT thật publish\win-x64\BambooMintKey.dll để kiểm chứng COM activation.
/// </summary>
public unsafe class SymbolCrashRegressionTests
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    private static void RunOnStaThread(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "STA test thread timed out.");
        if (caught != null) throw new AggregateException("Exception on STA thread", caught);
    }

    // =========================================================================
    // Test 1: Cấm tuyệt đối F# sprintf/failwithf trong mã nguồn Symbol Core
    // =========================================================================

    [Fact]
    public void SourceCodeAudit_SymbolCoreFiles_MustNotUseNativeAotUnsafePrintf()
    {
        string repoRoot = FindRepoRoot();
        string[] files =
        [
            Path.Combine(repoRoot, "src", "BambooMintKey.Core", "Engine", "SymbolEngine.fs"),
            Path.Combine(repoRoot, "src", "BambooMintKey.Core", "Dictionary", "SymbolDictionary.fs")
        ];

        string[] forbidden = ["sprintf", "failwithf", "printfn", "kprintf"];
        foreach (string file in files)
        {
            Assert.True(File.Exists(file), $"Missing source file: {file}");
            string content = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                Assert.DoesNotContain(token, content, StringComparison.Ordinal);
            }
        }
    }

    // =========================================================================
    // Test 2: Cấm gọi COM GetActiveView/GetTextExt và cấm tạo Win32 HWND trong NativeBridge
    //         (đảm bảo UI chỉ vẽ bằng Avalonia ở tiến trình BambooMintKey.UI)
    // =========================================================================

    [Fact]
    public void SourceCodeAudit_NativeBridgePopup_MustBeOutOfProcessIpcOnly()
    {
        string repoRoot = FindRepoRoot();
        string[] files =
        [
            Path.Combine(repoRoot, "src", "BambooMintKey.NativeBridge", "TSF", "CandidatePopupWindow.cs"),
            Path.Combine(repoRoot, "src", "BambooMintKey.NativeBridge", "TSF", "TsfTextExtHelper.cs"),
            Path.Combine(repoRoot, "src", "BambooMintKey.NativeBridge", "TSF", "TextEditSession.cs")
        ];

        string[] forbidden = ["CreateWindowExW", "RegisterClassExW", "GetActiveView", "GetTextExt", "BeginPaint"];
        foreach (string file in files)
        {
            Assert.True(File.Exists(file), $"Missing source file: {file}");
            var codeOnly = string.Join('\n',
                File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (string token in forbidden)
            {
                Assert.DoesNotContain(token, codeOnly, StringComparison.Ordinal);
            }
        }
    }

    // =========================================================================
    // Test 3: CandidatePopupWindow IPC (SharedMemory) hoạt động sạch, không crash
    // =========================================================================

    [Fact]
    public void CandidatePopupIpc_ShowUpdateHide_DoesNotCrash()
    {
        var pages = new[]
        {
            new CandidatePage(["→", "←", "↑", "↓", "⇒", "⇔", "↔"], 0, 0, 1, 7, "mui"),
            new CandidatePage(["①", "②", "③", "④", "⑤", "⑥", "⑦"], 3, 0, 3, 20, "tron"),
            new CandidatePage(["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣"], 2, 0, 2, 11, "vuong"),
            new CandidatePage(["⚠️", "❗", "❓"], 0, 0, 1, 3, "canhbao")
        };

        for (int i = 0; i < 30; i++)
        {
            Assert.True(CandidatePopupWindow.ShowOrUpdate(pages[i % pages.Length]));
        }

        Assert.False(CandidatePopupWindow.ShowOrUpdate(new CandidatePage([], 0, 0, 0, 0, "before")));
        CandidatePopupWindow.Hide();
    }

    // =========================================================================
    // Test 4: TextEditSession + CompositionManager không bao giờ gọi GetActiveView,
    //         chỉ gọi SetText đúng 1 lần và chốt phím '1' -> "→" sạch sẽ
    // =========================================================================

    [Fact]
    public void TextEditSession_NeverCallsGetActiveView_AndCommitsCleanly()
    {
        RunOnStaThread(() =>
        {
            bool prevVn = BridgeStateManager.IsVietnameseMode;
            BridgeStateManager.IsVietnameseMode = true;
            try
            {
                using var mock = new MockTsfContext();
                var service = new BambooMintKeyTextService();

                BridgeStateManager.ResetState();
                var (_, a1) = BridgeStateManager.ProcessWordBreak(':');
                Assert.True(a1.IsUpdateComposition);
                var (_, a2) = BridgeStateManager.ProcessWordBreak(':');
                Assert.True(a2.IsUpdateComposition);
                var (stM, aM) = BridgeStateManager.ProcessKey('m');
                Assert.True(aM.IsUpdateComposition);

                CompositionManager.HandleEngineAction(service, mock.ContextPtr, aM, stM.TransformedText);

                // Tuyệt đối không gọi GetActiveView (tránh crash MSCTF.dll / WinUIEdit.dll)
                Assert.Equal(0, mock.GetActiveViewCallCount);
                Assert.Equal(1, mock.SetTextCallCount);

                // Chốt bằng phím '1' -> commit "→"
                var (stCommit, aCommit) = BridgeStateManager.ProcessKey('1');
                Assert.True(aCommit.IsCommit);
                CompositionManager.HandleEngineAction(service, mock.ContextPtr, aCommit, stCommit.TransformedText);
                Assert.Equal("→", mock.LastSetText);
            }
            finally
            {
                BridgeStateManager.IsVietnameseMode = prevVn;
            }
        });
    }

    // =========================================================================
    // Test 5: Nạp trực tiếp binary NativeAOT thật publish\win-x64\BambooMintKey.dll
    // =========================================================================

    private delegate int DllGetClassObjectDelegate(Guid* rclsid, Guid* riid, IntPtr* ppv);

    [Fact]
    public void NativeAotPublishedDll_ComActivationAndKeyEventSink_DoesNotCrash()
    {
        string repoRoot = FindRepoRoot();
        string dllPath = Path.Combine(repoRoot, "publish", "win-x64", "BambooMintKey.dll");
        if (!File.Exists(dllPath)) return;

        RunOnStaThread(() =>
        {
            IntPtr hModule = LoadLibraryW(dllPath);
            Assert.True(hModule != IntPtr.Zero, $"Failed to LoadLibraryW('{dllPath}')");
            try
            {
                IntPtr pGetClassObj = GetProcAddress(hModule, "DllGetClassObject");
                Assert.True(pGetClassObj != IntPtr.Zero);
                var getClassObj = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(pGetClassObj);

                Guid clsid = Guids.TextServiceClsid;
                Guid iidFactory = Guids.IidIClassFactory;
                IntPtr pFactory = IntPtr.Zero;
                Assert.Equal(0, getClassObj(&clsid, &iidFactory, &pFactory));
                Assert.True(pFactory != IntPtr.Zero);

                try
                {
                    var factoryVt = *(ClassFactoryVTable**)pFactory;
                    Guid iidTip = Guids.IidITfTextInputProcessorEx;
                    IntPtr pTip = IntPtr.Zero;
                    Assert.Equal(0, factoryVt->CreateInstance(pFactory, IntPtr.Zero, &iidTip, &pTip));
                    try
                    {
                        var tipVt = *(TfTextInputProcessorExVTable**)pTip;
                        Guid iidSink = Guids.IidITfKeyEventSink;
                        IntPtr pSink = IntPtr.Zero;
                        Assert.Equal(0, tipVt->QueryInterface(pTip, &iidSink, &pSink));
                        try
                        {
                            var sinkVt = *(TfKeyEventSinkVTable**)pSink;
                            int eaten = 0;
                            Assert.Equal(0, sinkVt->OnSetFocus(pSink, 1));
                            Assert.Equal(0, sinkVt->OnTestKeyDown(pSink, IntPtr.Zero, (UIntPtr)0x1B, IntPtr.Zero, &eaten));
                            Assert.Equal(0, sinkVt->OnTestKeyDown(pSink, IntPtr.Zero, (UIntPtr)0x09, IntPtr.Zero, &eaten));
                            Assert.Equal(0, sinkVt->OnTestKeyDown(pSink, IntPtr.Zero, (UIntPtr)0x28, IntPtr.Zero, &eaten));
                            Assert.Equal(0, sinkVt->OnSetFocus(pSink, 0));
                        }
                        finally { (*(TfKeyEventSinkVTable**)pSink)->Release(pSink); }
                    }
                    finally { (*(TfTextInputProcessorExVTable**)pTip)->Release(pTip); }
                }
                finally { (*(ClassFactoryVTable**)pFactory)->Release(pFactory); }
            }
            finally { FreeLibrary(hModule); }
        });
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BambooMintKey.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate BambooMintKey.slnx");
    }

    private sealed unsafe class MockTsfContext : IDisposable
    {
        private static MockTsfContext? _active;
        private readonly TfContextVTable* _ctxVt;
        private readonly TfContextCompositionVTable* _ctxCompVt;
        private readonly TfCompositionVTable* _compVt;
        private readonly TfRangeVTable* _rangeVt;
        private readonly IntPtr _ctxObj, _ctxCompObj, _compObj, _rangeObj;

        public IntPtr ContextPtr => _ctxObj;
        public int GetActiveViewCallCount { get; private set; }
        public int SetTextCallCount { get; private set; }
        public string LastSetText { get; private set; } = "";

        public MockTsfContext()
        {
            _active = this;
            _ctxVt = (TfContextVTable*)Marshal.AllocHGlobal(sizeof(TfContextVTable));
            *_ctxVt = default;
            _ctxVt->QueryInterface = &CtxQueryInterface;
            _ctxVt->AddRef = &NoOpRef;
            _ctxVt->Release = &NoOpRef;
            _ctxVt->RequestEditSession = &CtxRequestEditSession;
            _ctxVt->GetSelection = &CtxGetSelection;
            _ctxVt->SetSelection = &CtxSetSelection;
            _ctxVt->GetActiveView = &CtxGetActiveView;
            _ctxVt->GetProperty = &CtxGetProperty;

            _ctxCompVt = (TfContextCompositionVTable*)Marshal.AllocHGlobal(sizeof(TfContextCompositionVTable));
            *_ctxCompVt = default;
            _ctxCompVt->AddRef = &NoOpRef;
            _ctxCompVt->Release = &NoOpRef;
            _ctxCompVt->StartComposition = &CtxStartComposition;

            _compVt = (TfCompositionVTable*)Marshal.AllocHGlobal(sizeof(TfCompositionVTable));
            *_compVt = default;
            _compVt->AddRef = &NoOpRef;
            _compVt->Release = &NoOpRef;
            _compVt->GetRange = &CompGetRange;
            _compVt->EndComposition = &CompEndComposition;

            _rangeVt = (TfRangeVTable*)Marshal.AllocHGlobal(sizeof(TfRangeVTable));
            *_rangeVt = default;
            _rangeVt->AddRef = &NoOpRef;
            _rangeVt->Release = &NoOpRef;
            _rangeVt->SetText = &RangeSetText;
            _rangeVt->Collapse = &RangeCollapse;

            _ctxObj = Alloc((IntPtr)_ctxVt);
            _ctxCompObj = Alloc((IntPtr)_ctxCompVt);
            _compObj = Alloc((IntPtr)_compVt);
            _rangeObj = Alloc((IntPtr)_rangeVt);
        }

        private static IntPtr Alloc(IntPtr vt)
        {
            var p = (IntPtr*)Marshal.AllocHGlobal(sizeof(IntPtr));
            *p = vt;
            return (IntPtr)p;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static uint NoOpRef(IntPtr _) => 1;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CtxQueryInterface(IntPtr _, Guid* riid, IntPtr* ppv)
        {
            if (ppv == null || riid == null) return HResult.Pointer;
            if (*riid == Guids.IidITfContextComposition && _active != null) { *ppv = _active._ctxCompObj; return HResult.Ok; }
            *ppv = IntPtr.Zero;
            return HResult.NoInterface;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CtxRequestEditSession(IntPtr _, uint tid, IntPtr pSession, uint dwFlags, int* phr)
        {
            int hr = (*(TfEditSessionVTable**)pSession)->DoEditSession(pSession, 42);
            if (phr != null) *phr = hr;
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CtxGetSelection(IntPtr _, uint ec, uint idx, uint cnt, TfSelection* pSel, uint* fetched)
        {
            if (_active == null || pSel == null || fetched == null) return HResult.Fail;
            pSel[0] = new TfSelection { range = _active._rangeObj };
            *fetched = 1;
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static int CtxSetSelection(IntPtr _, uint ec, uint cnt, TfSelection* pSel) => HResult.Ok;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CtxGetActiveView(IntPtr _, IntPtr* ppView)
        {
            if (_active != null) _active.GetActiveViewCallCount++;
            if (ppView != null) *ppView = IntPtr.Zero;
            return HResult.Fail;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static int CtxGetProperty(IntPtr _, Guid* g, IntPtr* p) { if (p != null) *p = IntPtr.Zero; return HResult.Fail; }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CtxStartComposition(IntPtr _, uint ec, IntPtr r, IntPtr s, IntPtr* ppComp)
        {
            if (_active == null || ppComp == null) return HResult.Fail;
            *ppComp = _active._compObj;
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int CompGetRange(IntPtr _, IntPtr* ppRange)
        {
            if (_active == null || ppRange == null) return HResult.Fail;
            *ppRange = _active._rangeObj;
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static int CompEndComposition(IntPtr _, uint ec) => HResult.Ok;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
        private static int RangeSetText(IntPtr _, uint ec, uint f, char* pch, int cch)
        {
            if (_active != null)
            {
                _active.SetTextCallCount++;
                _active.LastSetText = pch != null && cch > 0 ? new string(pch, 0, cch) : "";
            }
            return HResult.Ok;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static int RangeCollapse(IntPtr _, uint ec, uint pos) => HResult.Ok;

        public void Dispose()
        {
            CandidatePopupWindow.Hide();
            CompositionManager.TerminateActiveComposition();
            BridgeStateManager.ResetState();
            _active = null;
            Marshal.FreeHGlobal(_ctxObj); Marshal.FreeHGlobal(_ctxCompObj); Marshal.FreeHGlobal(_compObj); Marshal.FreeHGlobal(_rangeObj);
            Marshal.FreeHGlobal((IntPtr)_ctxVt); Marshal.FreeHGlobal((IntPtr)_ctxCompVt); Marshal.FreeHGlobal((IntPtr)_compVt); Marshal.FreeHGlobal((IntPtr)_rangeVt);
        }
    }
}
