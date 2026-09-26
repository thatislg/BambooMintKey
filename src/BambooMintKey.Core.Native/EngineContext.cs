// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using System.Text;
using BambooMintKey.Core.Domain;

namespace BambooMintKey.Core.Native;

/// <summary>
/// Đại diện cho một phiên gõ độc lập, tương ứng với một <c>InputContext</c> của Fcitx5.
///
/// Khác với mô hình Windows TSF (in-process, mỗi ứng dụng một bản DLL riêng), trên Linux
/// toàn bộ desktop dùng chung một tiến trình daemon <c>fcitx5</c>. Do đó trạng thái gõ
/// không được dùng biến static mà phải cách ly tuyệt đối theo từng context handle.
///
/// Bộ đệm preedit/commit được cấp phát bằng <see cref="NativeMemory"/> (unmanaged) để
/// con trỏ <c>byte*</c> trả về cho caller C++ luôn ổn định, không bị GC di chuyển và
/// không phát sinh cấp phát động khi gõ phím (zero-allocation).
///
/// Thiết kế theo 007_03_CoreNative_CABI_Design.md (Mục 2.2 &amp; 4).
/// </summary>
public sealed unsafe class EngineContext
{
    /// <summary>Kích thước cố định (byte) của các bộ đệm UTF-8.</summary>
    public const int BufferSize = 256;

    // =========================================================================
    // Engine state
    // =========================================================================

    /// <summary>Trạng thái từ vựng hiện tại của engine (WordState của F# Core).</summary>
    public Types.WordState State;

    /// <summary>Cấu hình gõ áp dụng cho context này (EngineConfig của F# Core).</summary>
    public EngineConfig.EngineConfig Config;

    // =========================================================================
    // UTF-8 buffers (owned by Core.Native — caller C++ only reads, never frees)
    // =========================================================================

    /// <summary>Con trỏ tới bộ đệm UTF-8 preedit (đang gõ dở), cố định <see cref="BufferSize"/> byte.</summary>
    public byte* PreeditBuffer;

    /// <summary>Độ dài chuỗi preedit hiện tại tính theo byte.</summary>
    public int PreeditLength;

    /// <summary>Con trỏ tới bộ đệm UTF-8 commit (đã chốt), cố định <see cref="BufferSize"/> byte.</summary>
    public byte* CommitBuffer;

    /// <summary>Độ dài chuỗi commit hiện tại tính theo byte.</summary>
    public int CommitLength;

    // =========================================================================
    // Thread-safety
    // =========================================================================

    private readonly object _syncRoot = new();

    /// <summary>Đối tượng khóa nhẹ per-context, dùng trong <c>lock (context.SyncRoot)</c>.</summary>
    public object SyncRoot => _syncRoot;

    /// <summary>Khởi tạo context mới với trạng thái rỗng, cấu hình mặc định và hai bộ đệm UTF-8 unmanaged.</summary>
    public EngineContext()
    {
        State = Types.WordState.Empty;
        Config = EngineConfig.EngineConfig.Default;
        PreeditBuffer = (byte*)NativeMemory.AllocZeroed(BufferSize);
        CommitBuffer = (byte*)NativeMemory.AllocZeroed(BufferSize);
        PreeditLength = 0;
        CommitLength = 0;
    }

    /// <summary>
    /// Cập nhật cấu hình gõ từ các cờ nguyên thủy (không cần xử lý chuỗi).
    /// Thứ tự tham số khớp với constructor của <c>EngineConfig.EngineConfig</c>.
    /// </summary>
    public void SetOptions(bool isEnabled, int toneStyle, bool autoRestoreEnglish, bool allowRepeatUndo, bool allowLeadingW, bool allowFreeTone)
    {
        var tone = toneStyle == 1 ? Types.TonePlacementStyle.Traditional : Types.TonePlacementStyle.Modern;
        Config = new EngineConfig.EngineConfig(
            isEnabled,
            autoRestoreEnglish,
            allowRepeatUndo,
            allowLeadingW,
            tone,
            allowFreeTone,
            true,   // enableVietnameseDictionary (M4)
            true);  // enableEnglishBacktracking (M4)
    }

    /// <summary>Ghi chuỗi <paramref name="text"/> dưới dạng UTF-8 vào bộ đệm preedit và cập nhật độ dài.</summary>
    public void SetPreedit(string text)
    {
        PreeditLength = WriteUtf8(PreeditBuffer, text);
    }

    /// <summary>Ghi chuỗi <paramref name="text"/> dưới dạng UTF-8 vào bộ đệm commit và cập nhật độ dài.</summary>
    public void SetCommit(string text)
    {
        CommitLength = WriteUtf8(CommitBuffer, text);
    }

    /// <summary>Xóa rỗng bộ đệm preedit (chuỗi rỗng, độ dài 0).</summary>
    public void ClearPreedit()
    {
        PreeditBuffer[0] = 0;
        PreeditLength = 0;
    }

    /// <summary>Xóa rỗng bộ đệm commit (chuỗi rỗng, độ dài 0).</summary>
    public void ClearCommit()
    {
        CommitBuffer[0] = 0;
        CommitLength = 0;
    }

    /// <summary>Đặt lại trạng thái về rỗng và xóa sạch hai bộ đệm (dùng khi chuyển focus hoặc hủy gõ dở).</summary>
    public void Reset()
    {
        lock (_syncRoot)
        {
            State = Types.WordState.Empty;
            ClearPreedit();
            ClearCommit();
        }
    }

    /// <summary>Giải phóng bộ nhớ unmanaged của hai bộ đệm. Chỉ gọi một lần khi context bị hủy.</summary>
    public void Free()
    {
        NativeMemory.Free(PreeditBuffer);
        NativeMemory.Free(CommitBuffer);
        PreeditBuffer = null;
        CommitBuffer = null;
    }

    /// <summary>
    /// Ghi chuỗi thành UTF-8 vào <paramref name="buffer"/> (dung lượng <see cref="BufferSize"/> byte,
    /// chừa 1 byte cho null terminator) và trả về số byte đã ghi.
    /// </summary>
    private static int WriteUtf8(byte* buffer, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            buffer[0] = 0;
            return 0;
        }

        int written = Encoding.UTF8.GetBytes(text, new Span<byte>(buffer, BufferSize - 1));
        buffer[written] = 0;
        return written;
    }
}
