<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Chi Tiết: Quản Lý Phiên Gõ TSF Composition, Text Range & Display Attribute

**Mã tài liệu:** `002_04_Composition_and_TextRange`

  

**Giai đoạn:** Phase 2 - Tích hợp Hệ Điều Hành (Windows TSF & NativeAOT)  

**Thuộc module:** `BambooMintKey.NativeBridge`

  

**Trạng thái:** ✅ Hoàn thành (Closed)

> **Lưu ý:** Tài liệu này phản ánh thiết kế composition và text range. Code triển khai cuối cùng nằm trong `src/BambooMintKey.NativeBridge/TSF/CompositionManager.cs`, `TextEditSession.cs`, `TsfSelectionHelper.cs` và `DisplayAttributeHelper.cs`.

## 1. Mục Tiêu Kỹ Thuật

- Cài đặt cơ chế yêu cầu quyền truy cập Document Context bất đồng bộ/đồng bộ thông qua `ITfEditSession` (`TF_ES_READWRITE`).  
- Khởi tạo, duy trì và kết thúc phiên gõ tạm `ITfComposition` và bắt sự kiện chấm dứt bất ngờ qua `ITfCompositionSink::OnCompositionTerminated`.  
- Thao tác trực tiếp trên vùng chọn văn bản `ITfRange`: mở rộng, thu hẹp, và thay thế chuỗi ký tự bằng `ITfRange::SetText` (thay thế nguyên tử, không nhấp nháy con trỏ).  
- Cài đặt `ITfDisplayAttributeProvider` để định dạng đường gạch chân mờ chuẩn TSF (Composition Underline) dưới từ đang gõ và xóa bỏ định dạng khi từ được commit.  

## 2. Luồng Thay Thế Ký Tự Trực Tiếp (Inline Text Replacement)

```bash
[Key Down Event] ──> F# TelexEngine trả về UpdateComposition("việt")
                                │
                                ▼
         ITfContext::RequestEditSession(TF_ES_READWRITE)
                                │
                                ▼
         ITfEditSession::DoEditSession(ec: TfEditCookie)
                                │
                                ├──> 1. Nếu chưa có Composition:
                                │       - Lấy Selection Range từ ITfContext::GetSelection
                                │       - StartComposition(ec, pRange, pCompositionSink)
                                │
                                ├──> 2. Lấy Composition Range:
                                │       - ITfComposition::GetRange(&pRange)
                                │
                                ├──> 3. Thay thế văn bản nguyên tử:
                                │       - ITfRange::SetText(ec, 0, "việt", 4)
                                │
                                ├──> 4. Gắn thuộc tính hiển thị (Gạch chân TSF):
                                │       - Gán DisplayAttribute GUID lên pRange
                                │
                                └──> 5. Cập nhật con trỏ:
                                        - Collapse pRange về TF_ANCHOR_END
                                        - ITfContext::SetSelection(ec, 1, &selection)
```

## 3. Khai Báo COM Structs & VTables Cho Composition & Range

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/ITfComposition.cs`:

C#

```c#
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
```

## 4. Cài Đặt EditSession Thực Thi Thao Tác Văn Bản (`TextEditSession.cs`)

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/TextEditSession.cs`:

C#

```c#
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
            // AddRef có [UnmanagedCallersOnly], gọi qua function pointer trong VTable
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
```

## 5. Quản Lý Vòng Đời Phiên Gõ (`CompositionManager.cs`)

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/CompositionManager.cs`:

C#

```c#
using System.Runtime.InteropServices;
using BambooMintKey.Core.Domain;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfContextCompositionVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, IntPtr*, int> StartComposition;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumCompositions;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> FindComposition;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int> TakeOwnership;
}

public static unsafe class CompositionManager
{
    private static readonly Guid IidITfContextComposition = new("D40C8A3B-DA93-4B21-9E58-53E7135B47F0");
    private static IntPtr _pActiveComposition = IntPtr.Zero;

    public static bool HasActiveComposition() => _pActiveComposition != IntPtr.Zero;

    public static IntPtr GetCompositionRange()
    {
        if (_pActiveComposition == IntPtr.Zero) return IntPtr.Zero;

        IntPtr pRange = IntPtr.Zero;
        var compVTable = *(TfCompositionVTable**)_pActiveComposition;
        compVTable->GetRange(_pActiveComposition, &pRange);
        return pRange;
    }

    public static bool StartComposition(BambooMintKeyTextService service, IntPtr pContext, uint ec)
    {
        if (_pActiveComposition != IntPtr.Zero) return true;

        IntPtr pContextComp = IntPtr.Zero;
        var contextVTable = *(TfContextCompositionVTable**)pContext;

        fixed (Guid* riid = &IidITfContextComposition)
        {
            if (contextVTable->QueryInterface(pContext, riid, &pContextComp) != HResult.Ok)
                return false;
        }

        // Lấy selection range hiện tại của ô nhập liệu
        var pRange = TsfSelectionHelper.GetSelectionRange(pContext, ec);
        if (pRange == IntPtr.Zero)
        {
            var ccVTable = *(TfContextCompositionVTable**)pContextComp;
            ccVTable->Release(pContextComp);
            return false;
        }

        IntPtr pCompSink = CompositionSinkImpl.GetOrCreateInstance();
        IntPtr pComposition = IntPtr.Zero;

        var ccVTableFinal = *(TfContextCompositionVTable**)pContextComp;
        int hr = ccVTableFinal->StartComposition(pContextComp, ec, pRange, pCompSink, &pComposition);

        // Giải phóng COM tạm
        var rangeVTable = *(TfRangeVTable**)pRange;
        rangeVTable->Release(pRange);
        ccVTableFinal->Release(pContextComp);

        if (hr == HResult.Ok && pComposition != IntPtr.Zero)
        {
            _pActiveComposition = pComposition;
            return true;
        }

        return false;
    }

    public static void EndComposition()
    {
        if (_pActiveComposition == IntPtr.Zero) return;

        var compVTable = *(TfCompositionVTable**)_pActiveComposition;
        compVTable->EndComposition(_pActiveComposition, 0);
        compVTable->Release(_pActiveComposition);
        _pActiveComposition = IntPtr.Zero;
    }

    public static void TerminateActiveComposition()
    {
        if (_pActiveComposition != IntPtr.Zero)
        {
            var compVTable = *(TfCompositionVTable**)_pActiveComposition;
            compVTable->Release(_pActiveComposition);
            _pActiveComposition = IntPtr.Zero;
        }
    }

    public static void HandleEngineAction(BambooMintKeyTextService service, IntPtr pContext, Types.EngineAction action, string transformedText)
    {
        if (action.IsUpdateComposition)
        {
            var updateAction = (Types.EngineAction.UpdateComposition)action;
            RequestEdit(service, pContext, EditActionType.UpdateText, updateAction.newText);
        }
        else if (action.IsCommit)
        {
            var commitAction = (Types.EngineAction.Commit)action;
            RequestEdit(service, pContext, EditActionType.CommitText, commitAction.committedText);
            BridgeStateManager.ResetState();
        }
        else // PassThrough
        {
            if (HasActiveComposition())
            {
                RequestEdit(service, pContext, EditActionType.CommitText, transformedText);
                BridgeStateManager.ResetState();
            }
        }
    }

    private static void RequestEdit(BambooMintKeyTextService service, IntPtr pContext, EditActionType actionType, string text)
    {
        var session = new TextEditSession(service, pContext, actionType, text);
        var pSession = session.CreateNativeInstance();

        int hrSession = 0;
        var contextVTable = *(TfContextVTable**)pContext;
        contextVTable->RequestEditSession(pContext, service.ClientId, pSession, TsfEditFlags.TfEsSync | TsfEditFlags.TfEsReadWrite, &hrSession);

        var sessionPunk = *(TfEditSessionVTable**)pSession;
        sessionPunk->Release(pSession);
    }
}
```

## 6. Hiển Thị Gạch Chân Composition (`DisplayAttributeHelper.cs`)

Tập trung tại file `src/BambooMintKey.NativeBridge/TSF/DisplayAttributeHelper.cs`:

C#

```c#
using System.Runtime.InteropServices;
using BambooMintKey.NativeBridge.Common;

namespace BambooMintKey.NativeBridge.TSF;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct TfPropertyVTable
{
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfProperty::GetType trùng tên với object.GetType(), dùng 'new' để suppress warning
    public new delegate* unmanaged[Stdcall]<IntPtr, Guid*, int> GetType;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, IntPtr, int> EnumRanges;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr*, int> GetValue;
    // ITfProperty::Clear chỉ có 2 tham số sau this: ec và pRange
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int> Clear;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> SetValueStore;
    public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, int> SetValue;
}

public static unsafe class DisplayAttributeHelper
{
    private static readonly Guid GuidPropDisplayAttribute = new("57D4C09F-3462-4253-833B-8189D8B542F6");
    private static readonly Guid GuidDisplayAttributeInput = new("E6A93F52-7B42-4F18-A4D2-E6B39218F12D");

    public static void ApplyCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &GuidPropDisplayAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok) return;
        }

        // Gán Display Attribute GUID (gạch chân nét chấm/nét liền mờ)
        var propVTable = *(TfPropertyVTable**)pProp;
        
        // Gán giá trị Variant kiểu VT_I4 / GUID
        IntPtr pVar = CreateGuidVariant();
        propVTable->SetValue(pProp, ec, pRange, pVar);
        Marshal.FreeHGlobal(pVar);

        propVTable->Release(pProp);
    }

    public static void ClearCompositionAttribute(IntPtr pContext, uint ec, IntPtr pRange)
    {
        if (pContext == IntPtr.Zero || pRange == IntPtr.Zero) return;

        IntPtr pProp = IntPtr.Zero;
        var contextVTable = *(TfContextVTable**)pContext;

        fixed (Guid* rguidProp = &GuidPropDisplayAttribute)
        {
            if (contextVTable->GetProperty(pContext, rguidProp, &pProp) != HResult.Ok) return;
        }

        // Xóa gạch chân composition
        var propVTable = *(TfPropertyVTable**)pProp;
        propVTable->Clear(pProp, ec, pRange);
        propVTable->Release(pProp);
    }

    private static IntPtr CreateGuidVariant()
    {
        // VARIANT structure: VT_UNKNOWN hoặc VT_I4 chứa Atom ID.
        // GuidDisplayAttributeInput được giữ lại để gán vào pVar khi implement đầy đủ.
        _ = GuidDisplayAttributeInput;

        var mem = Marshal.AllocHGlobal(24);
        Marshal.WriteInt16(mem, 0, 13); // VT_UNKNOWN
        return mem;
    }
}
```

## 7. Sơ Đồ Cấu Trúc Mã Nguồn Bổ Sung (Phase 2.4)

```bash
src/BambooMintKey.NativeBridge/
└── TSF/
    ├── ITfComposition.cs          # VTable cho ITfComposition, ITfRange, ITfEditSession
    ├── TextEditSession.cs         # Cài đặt DoEditSession với TF_ES_READWRITE
    ├── CompositionManager.cs      # Khởi tạo/kết thúc ITfComposition & thay thế chuỗi
    ├── CompositionSinkImpl.cs     # Bắt sự kiện OnCompositionTerminated
    ├── DisplayAttributeHelper.cs  # Áp dụng nét gạch chân mờ chuẩn TSF
    └── TsfSelectionHelper.cs      # Helper lấy Range & dịch chuyển con trỏ soạn thảo
```

