// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BambooMintKey.Core.Domain;
using BambooMintKey.Core.Engine;

namespace BambooMintKey.Core.Native;

/// <summary>
/// Các hàm C-ABI của <c>BambooMintKeyCore.so</c>, được Fcitx5 addon (C++) gọi qua <c>dlopen</c>.
///
/// Quy ước gọi chuẩn C (cdecl) theo thiết kế 007_03_CoreNative_CABI_Design.md.
///
/// Mã hành động (ActionCode) trả về bởi các hàm xử lý phím:
/// <list type="bullet">
///   <item><c>0 (PassThrough)</c> — nhường phím cho ứng dụng xử lý.</item>
///   <item><c>1 (Consume)</c> — nuốt phím (để dành, engine hiện chưa phát sinh).</item>
///   <item><c>2 (UpdatePreedit)</c> — cập nhật chuỗi preedit đang gõ.</item>
///   <item><c>3 (CommitString)</c> — chốt từ và xóa preedit.</item>
/// </list>
/// </summary>
public static unsafe class Exports
{
    // =========================================================================
    // Action codes
    // =========================================================================

    private const int ActionPassThrough = 0;
    private const int ActionConsume = 1; // Reserved; engine hiện chỉ trả 0/2/3
    private const int ActionUpdatePreedit = 2;
    private const int ActionCommitString = 3;

    // =========================================================================
    // Leak tracking (chẩn đoán rò rỉ context)
    // =========================================================================

    private static int _contextCreated;
    private static int _contextFreed;

    // =========================================================================
    // Version
    // =========================================================================

    /// <summary>Trả về phiên bản ABI.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_version", CallConvs = [typeof(CallConvCdecl)])]
    public static int Version() => 1;

    // =========================================================================
    // Diagnostics
    // =========================================================================

    /// <summary>Ép buộc GC chạy (dùng cho test rò rỉ bộ nhớ TC-CABI-08).</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_gc_collect", CallConvs = [typeof(CallConvCdecl)])]
    public static void GcCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>Trả về số context đang sống (đã tạo chưa giải phóng). Dùng để phát hiện rò rỉ context.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_get_live_context_count", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetLiveContextCount()
    {
        return Volatile.Read(ref _contextCreated) - Volatile.Read(ref _contextFreed);
    }

    // =========================================================================
    // Lifecycle (M1.3)
    // =========================================================================

    /// <summary>Cấp phát một <see cref="EngineContext"/> mới, giữ alive qua GCHandle và trả về handle (khác 0).</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_context_create", CallConvs = [typeof(CallConvCdecl)])]
    public static IntPtr ContextCreate()
    {
        var context = new EngineContext();
        var gch = GCHandle.Alloc(context);
        Interlocked.Increment(ref _contextCreated);
        return GCHandle.ToIntPtr(gch);
    }

    /// <summary>Giải phóng bộ nhớ unmanaged của context và thu hồi GCHandle. Bỏ qua nếu handle = 0.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_context_free", CallConvs = [typeof(CallConvCdecl)])]
    public static void ContextFree(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var context = GetContext(handle);
        if (context == null)
        {
            return;
        }

        context.Free();
        FreeHandle(handle);
        Interlocked.Increment(ref _contextFreed);
    }

    /// <summary>Đặt lại trạng thái về rỗng và xóa hai bộ đệm. Bỏ qua nếu handle không hợp lệ.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_context_reset", CallConvs = [typeof(CallConvCdecl)])]
    public static void ContextReset(IntPtr handle)
    {
        GetContext(handle)?.Reset();
    }

    // =========================================================================
    // Key processing (M1.4)
    // =========================================================================

    /// <summary>Xử lý một ký tự Unicode (code point 32-bit) và trả về mã hành động.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_process_key", CallConvs = [typeof(CallConvCdecl)])]
    public static int ProcessKey(IntPtr handle, uint unicodeChar)
    {
        var context = GetContext(handle);
        if (context == null)
        {
            return ActionPassThrough;
        }

        lock (context.SyncRoot)
        {
            return ProcessInput(context, Types.KeyInput.NewChar((char)unicodeChar));
        }
    }

    /// <summary>Xử lý phím Backspace và trả về mã hành động.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_process_backspace", CallConvs = [typeof(CallConvCdecl)])]
    public static int ProcessBackspace(IntPtr handle)
    {
        var context = GetContext(handle);
        if (context == null)
        {
            return ActionPassThrough;
        }

        lock (context.SyncRoot)
        {
            return ProcessInput(context, Types.KeyInput.Backspace);
        }
    }

    /// <summary>Xử lý ký tự ngắt từ (space, enter, dấu câu...) và trả về mã hành động.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_process_wordbreak", CallConvs = [typeof(CallConvCdecl)])]
    public static int ProcessWordBreak(IntPtr handle, uint breakChar)
    {
        var context = GetContext(handle);
        if (context == null)
        {
            return ActionPassThrough;
        }

        lock (context.SyncRoot)
        {
            return ProcessInput(context, Types.KeyInput.NewWordBreak((char)breakChar));
        }
    }

    // =========================================================================
    // UTF-8 buffer extraction (M1.5)
    // =========================================================================

    /// <summary>Trả về con trỏ tới chuỗi UTF-8 preedit (read-only, null-terminated). Null nếu handle không hợp lệ.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_get_preedit_text", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* GetPreeditText(IntPtr handle)
    {
        var context = GetContext(handle);
        return context == null ? null : context.PreeditBuffer;
    }

    /// <summary>Trả về con trỏ tới chuỗi UTF-8 commit (read-only, null-terminated). Null nếu handle không hợp lệ.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_get_commit_text", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* GetCommitText(IntPtr handle)
    {
        var context = GetContext(handle);
        return context == null ? null : context.CommitBuffer;
    }

    /// <summary>Trả về độ dài (số byte) của chuỗi preedit hiện tại.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_get_preedit_length", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetPreeditLength(IntPtr handle)
    {
        var context = GetContext(handle);
        return context == null ? 0 : context.PreeditLength;
    }

    // =========================================================================
    // Configuration (M1.6)
    // =========================================================================

    /// <summary>Cập nhật nhanh các tùy chọn gõ bằng cờ nguyên thủy.</summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_set_options", CallConvs = [typeof(CallConvCdecl)])]
    public static void SetOptions(
        IntPtr handle,
        int isEnabled,
        int toneStyle,
        int autoRestoreEnglish,
        int allowRepeatUndo,
        int allowLeadingW,
        int allowFreeTone)
    {
        var context = GetContext(handle);
        if (context == null)
        {
            return;
        }

        context.SetOptions(
            isEnabled != 0,
            toneStyle,
            autoRestoreEnglish != 0,
            allowRepeatUndo != 0,
            allowLeadingW != 0,
            allowFreeTone != 0);
    }

    /// <summary>
    /// Nạp cấu hình từ chuỗi JSON UTF-8 (schema XDG). Trả về 0 nếu thành công, -1 nếu JSON không hợp lệ.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "bmk_load_config_json", CallConvs = [typeof(CallConvCdecl)])]
    public static int LoadConfigJson(IntPtr handle, byte* jsonUtf8)
    {
        var context = GetContext(handle);
        if (context == null)
        {
            return -1;
        }

        var json = ReadUtf8(jsonUtf8);
        if (string.IsNullOrWhiteSpace(json))
        {
            return -1;
        }

        bool isEnabled = JsonGetBool(json, "isVietnameseMode", true);
        int toneStyle = JsonGetInt(json, "toneStyle", 0);
        bool autoRestore = JsonGetBool(json, "autoRestoreEnglishWords", true);
        bool repeatUndo = JsonGetBool(json, "allowRepeatKeyUndo", true);
        bool leadingW = JsonGetBool(json, "allowLeadingWAsU", false);
        bool freeTone = JsonGetBool(json, "allowFreeTonePlacement", true);

        context.SetOptions(isEnabled, toneStyle, autoRestore, repeatUndo, leadingW, freeTone);
        return 0;
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================

    /// <summary>Gọi engine, cập nhật trạng thái và điều phối kết quả <c>EngineAction</c> vào bộ đệm + trả mã hành động.</summary>
    private static int ProcessInput(EngineContext context, Types.KeyInput input)
    {
        var result = TelexEngine.processKey(context.State, input, context.Config);
        context.State = result.Item1;
        var action = result.Item2;

        if (action.IsUpdateComposition)
        {
            var text = ((Types.EngineAction.UpdateComposition)action).newText;
            context.SetPreedit(text);
            return ActionUpdatePreedit;
        }

        if (action.IsCommit)
        {
            var text = ((Types.EngineAction.Commit)action).committedText;
            context.SetCommit(text);
            context.ClearPreedit();
            return ActionCommitString;
        }

        return ActionPassThrough;
    }

    /// <summary>Ánh xạ handle trở lại <see cref="EngineContext"/>. Trả về null nếu handle = 0 hoặc không hợp lệ.</summary>
    private static EngineContext? GetContext(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return GCHandle.FromIntPtr(handle).Target as EngineContext;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Thu hồi GCHandle từ handle. An toàn nếu handle = 0 hoặc không hợp lệ.</summary>
    private static void FreeHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            GCHandle.FromIntPtr(handle).Free();
        }
        catch
        {
            // Đã giải phóng hoặc handle không hợp lệ — bỏ qua an toàn.
        }
    }

    /// <summary>Đọc chuỗi UTF-8 (null-terminated) từ con trỏ thành <see cref="string"/>.</summary>
    private static string ReadUtf8(byte* ptr)
    {
        if (ptr == null)
        {
            return string.Empty;
        }

        int length = 0;
        while (ptr[length] != 0)
        {
            length++;
        }

        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, length));
    }

    /// <summary>Tìm vị trí bắt đầu giá trị của khóa JSON top-level; trả về -1 nếu không tìm thấy.</summary>
    private static int FindJsonValue(string json, string key)
    {
        var target = "\"" + key + "\"";
        int idx = json.IndexOf(target, StringComparison.Ordinal);
        if (idx < 0)
        {
            return -1;
        }

        int colon = json.IndexOf(':', idx + target.Length);
        if (colon < 0)
        {
            return -1;
        }

        int start = colon + 1;
        while (start < json.Length && (json[start] == ' ' || json[start] == '\t' || json[start] == '\r' || json[start] == '\n'))
        {
            start++;
        }

        return start < json.Length ? start : -1;
    }

    /// <summary>Đọc giá trị boolean của khóa JSON; trả về <paramref name="defaultValue"/> nếu không tìm thấy/không hợp lệ.</summary>
    private static bool JsonGetBool(string json, string key, bool defaultValue)
    {
        int start = FindJsonValue(json, key);
        if (start < 0)
        {
            return defaultValue;
        }

        var rest = json.AsSpan(start);
        if (rest.StartsWith("true"))
        {
            return true;
        }

        if (rest.StartsWith("false"))
        {
            return false;
        }

        return defaultValue;
    }

    /// <summary>Đọc giá trị số nguyên của khóa JSON; trả về <paramref name="defaultValue"/> nếu không tìm thấy/không hợp lệ.</summary>
    private static int JsonGetInt(string json, string key, int defaultValue)
    {
        int start = FindJsonValue(json, key);
        if (start < 0)
        {
            return defaultValue;
        }

        int i = start;
        int sign = 1;
        if (i < json.Length && json[i] == '-')
        {
            sign = -1;
            i++;
        }

        int result = 0;
        bool any = false;
        while (i < json.Length && json[i] >= '0' && json[i] <= '9')
        {
            result = (result * 10) + (json[i] - '0');
            i++;
            any = true;
        }

        return any ? sign * result : defaultValue;
    }
}
