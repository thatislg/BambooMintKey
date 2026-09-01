using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

public enum EditActionType
{
    UpdateText,
    CommitText,
    CancelComposition
}

public unsafe class TextEditSession
{
    private static TfEditSessionVTable* _vTable;

    private int _refCount = 1;
    private readonly BambooMintKeyTextService _service;
    private readonly IntPtr _pContext;
    private readonly EditActionType _actionType;
    private readonly string _text;

    public TextEditSession(BambooMintKeyTextService service, IntPtr pContext, EditActionType actionType, string text)
    {
        _service = service;
        _pContext = pContext;
        _actionType = actionType;
        _text = text; // caller always provides non-null string
        InitializeVTable();
    }

    private static void InitializeVTable()
    {
        if (_vTable != null) return;

        _vTable = (TfEditSessionVTable*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(TextEditSession), sizeof(TfEditSessionVTable));

        _vTable->QueryInterface = &QueryInterface;
        _vTable->AddRef = &AddRef;
        _vTable->Release = &Release;
        _vTable->DoEditSession = &DoEditSession;
    }

    public IntPtr CreateNativeInstance()
    {
        var handle = GCHandle.Alloc(this, GCHandleType.Normal);
        var layout = (IntPtr*)Marshal.AllocHGlobal(sizeof(IntPtr) * 2);
        layout[0] = (IntPtr)_vTable;
        layout[1] = GCHandle.ToIntPtr(handle);
        return (IntPtr)layout;
    }

    private static TextEditSession GetTarget(IntPtr thisPtr)
    {
        var layout = (IntPtr*)thisPtr;
        var handle = GCHandle.FromIntPtr(layout[1]);
        return (TextEditSession)handle.Target!;
    }

    #region IUnknown Callbacks
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(IntPtr thisPtr, Guid* riid, IntPtr* ppvObject)
    {
        if (ppvObject == null || riid == null) return HResult.Pointer;
        *ppvObject = IntPtr.Zero;

        if (*riid == Guids.IidIUnknown || *riid == Guids.IidITfEditSession)
        {
            *ppvObject = thisPtr;
            var vtable = *(TfEditSessionVTable**)thisPtr;
            vtable->AddRef(thisPtr);
            return HResult.Ok;
        }

        return HResult.NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        return (uint)Interlocked.Increment(ref target._refCount);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(IntPtr thisPtr)
    {
        var target = GetTarget(thisPtr);
        var count = Interlocked.Decrement(ref target._refCount);
        if (count == 0)
        {
            var layout = (IntPtr*)thisPtr;
            var handle = GCHandle.FromIntPtr(layout[1]);
            handle.Free();
            Marshal.FreeHGlobal(thisPtr);
        }
        return (uint)count;
    }
    #endregion

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int DoEditSession(IntPtr thisPtr, uint ec)
    {
        var target = GetTarget(thisPtr);
        return target.ExecuteSession(ec);
    }

    private int ExecuteSession(uint ec)
    {
        switch (_actionType)
        {
            case EditActionType.UpdateText:
                return PerformUpdateText(ec);

            case EditActionType.CommitText:
                return PerformCommitText(ec);

            case EditActionType.CancelComposition:
                return PerformCancelComposition(ec);

            default:
                return HResult.Ok;
        }
    }

    private int PerformUpdateText(uint ec)
    {
        // 1. Kiểm tra / Mở mới ITfComposition nếu chưa có
        if (!CompositionManager.HasActiveComposition())
        {
            if (!CompositionManager.StartComposition(_service, _pContext, ec))
            {
                return HResult.Fail;
            }
        }

        // 2. Lấy vùng Text Range của Composition hiện tại
        var pRange = CompositionManager.GetCompositionRange();
        if (pRange == IntPtr.Zero) return HResult.Fail;

        // 3. Thay thế văn bản trực tiếp
        fixed (char* pChars = _text)
        {
            var rangeVTable = *(TfRangeVTable**)pRange;
            rangeVTable->SetText(pRange, ec, 0, pChars, _text.Length);
        }

        // 4. Định dạng gạch chân Composition
        DisplayAttributeHelper.ApplyCompositionAttribute(_pContext, ec, pRange);

        // 5. Di chuyển con trỏ (Selection) về cuối chuỗi vừa gõ
        TsfSelectionHelper.SetSelectionToEnd(_pContext, ec, pRange);

        return HResult.Ok;
    }

    private int PerformCommitText(uint ec)
    {
        if (CompositionManager.HasActiveComposition())
        {
            var pRange = CompositionManager.GetCompositionRange();
            if (pRange != IntPtr.Zero)
            {
                // Thay thế chuỗi chốt cuối cùng
                fixed (char* pChars = _text)
                {
                    var rangeVTable = *(TfRangeVTable**)pRange;
                    rangeVTable->SetText(pRange, ec, 0, pChars, _text.Length);
                }

                // Xóa gạch chân composition
                DisplayAttributeHelper.ClearCompositionAttribute(_pContext, ec, pRange);
                TsfSelectionHelper.SetSelectionToEnd(_pContext, ec, pRange);
            }

            // Chốt và giải phóng ITfComposition
            CompositionManager.EndComposition();
        }
        return HResult.Ok;
    }

    private int PerformCancelComposition(uint ec)
    {
        _ = ec; // reserved for future rollback logic
        if (CompositionManager.HasActiveComposition())
        {
            CompositionManager.EndComposition();
        }
        return HResult.Ok;
    }
}
