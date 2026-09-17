<!--
  BambooMintKey - Vietnamese Telex Input Method Editor for Windows
  Copyright (c) 2026 Dương Gia Long and LMO contributors
  SPDX-License-Identifier: MIT
-->

# Thiết Kế Chi Tiết: Đồng Bộ Trạng Thái V/E Toàn Cục (Global Vietnamese/English State Synchronization)

**Mã tài liệu:** `005_06`

**Tài liệu:** `docs/2.Design/Phase5/005_06_Global_VE_State_Synchronization.md`

**Giai đoạn:** Phase 5 - Cải Tiến Trải Nghiệm Gõ

**Trạng thái:** 📝 Đang thiết kế (Design Specification)

**Chế độ triển khai:** Chỉ thiết kế, **chưa được phép viết code**.

**Issue liên quan:** `docs/3.Issue/004_EV_IconError.md`

---

## 1. Tóm tắt vấn đề

Hiện tại, trạng thái **V (Tiếng Việt)** / **E (Tiếng Anh)** của BambooMintKey không phải lúc nào cũng nhất quán giữa:

- Icon hiển thị trên Taskbar / Language Bar,
- Khả năng gõ tiếng Việt thực tế của engine,
- TSF Input Mode Compartment mà Windows Shell đọc.

Người dùng báo cáo: **icon hiển thị E nhưng vẫn gõ được tiếng Việt, hoặc icon V nhưng vẫn gõ tiếng Anh**. Đôi khi trạng thái bị "kẹt", không thể chuyển đổi.

Yêu cầu thiết kế cốt lõi: **Icon E/V phải nhất quán 100% với khả năng gõ tiếng Việt. Nếu icon báo V, người dùng phải gõ được tiếng Việt; nếu icon báo E, engine phải trả về ký tự thô.**

---

## 2. Nguyên tắc thiết kế (Design Principles)

### 2.1. Single Source of Truth

Chỉ có **một nguồn duy nhất** quyết định trạng thái V/E toàn hệ thống: **`SharedMemoryManager.IsVietnameseMode` (byte offset 0 của shared memory)**.

Mọi thành phần khác chỉ được **đọc** từ nguồn này. Không được giữ bản sao nội bộ có thể stale.

### 2.2. Atomic State Change

Mọi thao tác đổi trạng thái V/E phải là **atomic** trên toàn hệ thống. Khi có tranh chấp (race condition), thao tác bị hủy và quay về đọc state đúng.

### 2.3. Icon = Engine Capability

Icon hiển thị **không được** dựa trên biến cục bộ hay trạng thái cũ. Icon phải luôn được vẽ dựa trên giá trị mới nhất của `SharedMemoryManager.IsVietnameseMode`.

### 2.4. Compartment Synchronization

`GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` trong TSF phải luôn phản ánh đúng state. Nếu Windows Shell đọc compartment thấy khác, icon/taskbar sẽ sai.

### 2.5. No Silent Failure

Nếu đồng bộ compartment hoặc icon thất bại, phải ghi log và thử lại ở lần kích hoạt tiếp theo. Không được để trạng thái lệch mà không báo hiệu.

---

## 3. Kiến trúc hiện tại và các điểm lỗi

### 3.1. Các nơi lưu trạng thái hiện tại

| # | Nơi lưu | Code path | Vấn đề |
|---|---|---|---|
| 1 | `SharedMemoryManager._pShared[0]` | `IsVietnameseMode`, `ToggleVietnameseMode` | Không atomic. `Toggle` đọc rồi ghi trong 2 bước. |
| 2 | `BridgeStateManager._currentConfig.IsEnabled` | `Config` property | Có thể stale nếu không gọi lại `Config` sau khi state đổi. |
| 3 | TSF Compartment `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` | `TsfCompartmentHelper.SetConversionMode` | Được ghi sau toggle, có thể thất bại hoặc bị Windows override. |
| 4 | Icon bitmap / LangBar text | `GetIcon`, `GetText`, `GetTooltipString` | Chỉ vẽ lại khi `NotifyStateChanged()` được gọi. Nếu sink NULL hoặc event miss, icon không update. |

### 3.2. Các code path thay đổi state hiện tại

| # | Path | Hành vi | Thiếu sót |
|---|---|---|---|
| A | Click icon trái (`LangBarItemButton.OnClick`) | Toggle → Notify → SetCompartment | Không atomic, không kiểm tra tranh chấp. |
| B | Menu TSF / Native popup (`ExecuteMenuCommand`) | Toggle → Notify → SetCompartment | Giống A. |
| C | Phím tắt trong `KeyEventSinkImpl.OnKeyDown` | Toggle → Notify → SetCompartment | Có thể double-toggle với `OnPreservedKey`. |
| D | Phím tắt TSF preserved (`OnPreservedKey`) | Toggle → Notify → SetCompartment | Có thể double-toggle với `OnKeyDown`. |
| E | UI cấu hình (`MainWindow.axaml.fs`) | Ghi `SharedMemoryManager.IsVietnameseMode` | Không gọi `SetConversionMode`, không `NotifyStateChanged` trực tiếp. |
| F | `StateWatcher` trong mỗi process | Nghe event, gọi `NotifyStateChanged` | Không đồng bộ compartment. |

### 3.3. Các kịch bản lỗi đã xác định

1. **Race toggle:** Hai process cùng toggle. Cả hai đọc `current = V`, cả hai ghi `E`. Kết quả cuối cùng đúng là `E`, nhưng cả hai đều nghĩ "mình vừa chuyển từ V sang E". Nếu một process ghi `E` và process kia ghi `V` ngay sau → lộn xộn.
2. **Stale `BridgeStateManager.Config`:** Engine dùng config cũ trong khi shared memory đã đổi. → Icon E nhưng vẫn gõ được tiếng Việt.
3. **UI config không notify:** UI thay đổi state nhưng không cập nhật compartment/icon ngay. → Icon không đổi.
4. **Compartment bị override:** Windows/TSF ghi lại compartment. Bộ gõ không đọc ngược về. → Icon V nhưng compartment E (hoặc ngược lại).
5. **OnKeyDown + OnPreservedKey cùng chạy:** Một phím tắt được xử lý 2 lần → toggle 2 lần = không đổi, nhưng notify 1 lần.

---

## 4. Thiết kế mới: Global V/E State Manager

### 4.1. Single Source of Truth

```text
┌─────────────────────────────────────────────────────────────┐
│           GlobalVEState (static, cross-process)               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ SharedMemoryManager.IsVietnameseMode (byte offset 0)  │  │
│  │  - Interlocked read/write                              │  │
│  │  - Sequence number + event broadcast                    │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────────────┬──────────────────────────────┘
                               │
           ┌───────────────────┼───────────────────┐
           ▼                   ▼                   ▼
   ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
   │   Engine      │   │  Icon Helper  │   │  TSF          │
   │  TelexEngine  │   │ LangBarItem   │   │ Compartment   │
   │               │   │   Button      │   │               │
   └───────────────┘   └───────────────┘   └───────────────┘
```

### 4.2. API trung tâm (Centralized API)

Tạo class static `GlobalVEState` trong `BambooMintKey.NativeBridge.Common`:

```csharp
public static unsafe class GlobalVEState
{
    /// <summary>
    /// Đọc trạng thái V/E hiện tại từ shared memory một cách atomic.
    /// Đây là single source of truth duy nhất.
    /// </summary>
    public static bool IsVietnameseMode => SharedMemoryManager.IsVietnameseMode;

    /// <summary>
    /// Đặt trạng thái V/E mới một cách atomic và đồng bộ toàn hệ thống.
    /// Hàm này là cổng duy nhất được phép thay đổi state.
    /// </summary>
    public static bool SetVietnameseMode(bool value, SyncTarget target)
    {
        // 1. Atomic write to shared memory
        // 2. Update TSF compartment if requested
        // 3. Notify icon helper
        // 4. Broadcast state changed event
    }

    /// <summary>
    /// Đảo trạng thái V/E một cách atomic. Nếu phát hiện state đã bị đổi
    /// bởi process khác trong khoảng đọc-ghi, hủy bỏ và trả về state thực tế.
    /// </summary>
    public static bool ToggleVietnameseMode(SyncTarget target)
    {
        // 1. Interlocked read current
        // 2. Compute desired = !current
        // 3. Interlocked.CompareExchange to write
        // 4. If failed (state changed by other process), abort and read current
        // 5. Sync compartment + icon + broadcast
        // 6. Return actual state
    }

    /// <summary>
    /// Buộc đồng bộ compartment và icon với shared memory.
    /// Dùng khi nhận event từ process khác, hoặc khi nghi ngờ lệch pha.
    /// </summary>
    public static void ResyncFromSharedMemory(IntPtr pThreadMgr, uint clientId)
    {
        // 1. Read IsVietnameseMode
        // 2. SetConversionMode if different from last known compartment value
        // 3. NotifyStateChanged
    }
}
```

### 4.3. Atomic Toggle bằng Interlocked

```csharp
public static bool ToggleVietnameseMode(SyncTarget target)
{
    EnsureInitialized();
    if (_pShared == null)
    {
        // Fallback khi không có shared memory (hiếm)
        _fallbackVietnameseMode = !_fallbackVietnameseMode;
        return _fallbackVietnameseMode;
    }

    while (true)
    {
        byte current = (byte)System.Threading.Interlocked.CompareExchange(
            ref _pShared[0], 0, 0); // atomic read

        byte desired = (byte)(current == 0 ? 1 : 0);

        byte actual = (byte)System.Threading.Interlocked.CompareExchange(
            ref _pShared[0], desired, current);

        if (actual == current)
        {
            // Thành công, chúng ta là người thay đổi state
            SignalStateChanged();
            return desired != 0;
        }

        // State đã bị đổi bởi thread/process khác giữa lúc đọc và ghi.
        // Hủy bỏ toggle, đọc lại state mới nhất và trả về.
        // Không gọi SignalStateChanged vì chúng ta không đổi gì.
        return _pShared[0] != 0;
    }
}
```

> **Lưu ý:** `Interlocked.CompareExchange` trên `byte` trong C# unsafe context đòi hỏi `ref int` hoặc dùng `Volatile.Read`/`Volatile.Write`. Có thể cần chuyển `_pShared[0]` sang `ref int` hoặc dùng `System.Threading.Interlocked` với `int` ở offset 0.

### 4.4. Cơ chế đồng bộ khi có tranh chấp

```text
Process A muốn toggle:
  current = V (1)
  desired = E (0)
  CompareExchange(actual=1) → thành công → ghi E, broadcast event

Process B muốn toggle cùng lúc:
  current = V (1)   [đọc trước khi A ghi]
  desired = E (0)
  CompareExchange(actual=0) → thất bại vì A đã ghi E
  → abort, không ghi gì
  → return actual state = E
  → caller phải resync icon/compartment theo E
```

Như vậy, nếu A và B cùng toggle, kết quả cuối cùng là E. Process B sẽ biết mình không phải người thay đổi, nhưng vẫn cần đảm bảo icon của B hiển thị E.

### 4.5. Unified Sync Function

Mọi code path muốn đổi state đều gọi `GlobalVEState.SetVietnameseMode` hoặc `ToggleVietnameseMode`:

```csharp
public enum SyncTarget
{
    None = 0,
    SharedMemory = 1,
    Compartment = 2,
    Icon = 4,
    All = SharedMemory | Compartment | Icon
}
```

Hành vi:
- `SharedMemory`: ghi byte offset 0.
- `Compartment`: gọi `TsfCompartmentHelper.SetConversionMode` nếu có `pThreadMgr`.
- `Icon`: gọi `LangBarItemButton.NotifyStateChanged()` trong process hiện tại.
- Broadcast event: luôn gọi nếu state thay đổi.

---

## 5. Cập nhật các code path hiện có

### 5.1. Click icon / menu / phím tắt

Tất cả các path sau phải gọi `GlobalVEState.ToggleVietnameseMode(SyncTarget.All)`:

- `LangBarItemButton.OnClick`
- `LangBarItemButton.ExecuteMenuCommand`
- `KeyEventSinkImpl.OnKeyDown` (phím tắt)
- `KeyEventSinkImpl.OnPreservedKey` (phím tắt TSF preserved)

Không được gọi `BridgeStateManager.ToggleVietnameseMode()` trực tiếp nữa.

### 5.2. UI cấu hình

Khi người dùng thay đổi V/E trong UI (nếu có UI element này), gọi:

```csharp
GlobalVEState.SetVietnameseMode(newValue, SyncTarget.All);
```

Không ghi `SharedMemoryManager.IsVietnameseMode` trực tiếp.

### 5.3. Engine đọc state

`BridgeStateManager.Config` getter phải **luôn** đọc `GlobalVEState.IsVietnameseMode` mỗi lần được gọi, không cache trong `_currentConfig`.

```csharp
public static EngineConfig.EngineConfig Config
{
    get
    {
        bool isVn = GlobalVEState.IsVietnameseMode;
        // ... build new config every time
        return new EngineConfig.EngineConfig(...);
    }
}
```

Hoặc nếu vẫn muốn cache để tối ưu, phải invalidate cache ngay khi `StateChangedEvent` fire.

### 5.4. StateWatcher

Trong mỗi process, `StateWatcher` khi nhận event:

1. Đọc `GlobalVEState.IsVietnameseMode`.
2. So sánh với compartment local. Nếu khác → gọi `SetConversionMode`.
3. Gọi `NotifyStateChanged` để vẽ lại icon.
4. Invalidate `BridgeStateManager.Config` cache.

### 5.5. Compartment monitoring (nếu cần)

Windows Shell hoặc ứng dụng khác có thể ghi lại `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`. Có thể đăng ký `ITfCompartmentEventSink` để lắng nghe thay đổi từ bên ngoài và đồng bộ ngược về shared memory.

---

## 6. Sơ đồ luồng chuyển đổi V/E an toàn

### 6.1. Người dùng click icon

```text
LangBarItemButton.OnClick()
        │
        ▼
GlobalVEState.ToggleVietnameseMode(SyncTarget.All)
        │
        ├─ Interlocked.CompareExchange shared memory
        ├─ SetConversionMode(pThreadMgr, clientId, newState)
        ├─ NotifyStateChanged()
        └─ SignalStateChanged()
        │
        ▼
Engine sẽ đọc state mới ở lần gọi Config tiếp theo
Icon sẽ được vẽ lại qua ITfLangBarItemSink::OnUpdate
```

### 6.2. Process khác thay đổi state

```text
Process A thay đổi state
        │
        └─ SignalStateChanged()
                │
                ▼ (Manual-Reset event broadcast)
Process B StateWatcher nhận event
        │
        ▼
GlobalVEState.ResyncFromSharedMemory(pThreadMgr, clientId)
        │
        ├─ Read IsVietnameseMode
        ├─ SetConversionMode if local compartment differs
        └─ NotifyStateChanged()
```

---

## 7. Xử lý các edge cases

### 7.1. OnKeyDown và OnPreservedKey cùng chạy

Nếu một phím tắt được xử lý cả bởi `OnKeyDown` và `OnPreservedKey`, thì chỉ một trong hai path cần toggle.

Phương án:
- Đánh dấu đã toggle trong `OnPreservedKey` và bỏ qua `OnKeyDown`.
- Hoặc: `OnKeyDown` chỉ kiểm tra, còn `OnPreservedKey` mới thực hiện toggle.
- Hoặc: cả hai đều gọi `GlobalVEState.ToggleVietnameseMode`. Vì atomic, nếu A toggle trước, B sẽ abort và chỉ resync. Kết quả cuối cùng vẫn đúng, nhưng có thể gây 2 lần notify → cần debounce.

### 7.2. UI config thay đổi trong khi đang gõ

Khi UI thay đổi state, phải:
1. Ghi shared memory.
2. Broadcast event.
3. Các process đang gõ nhận event → resync → kết thúc composition hiện tại hoặc chuyển engine mode ngay.

### 7.3. Khởi động lại process

Khi một ứng dụng mới load `BambooMintKey.dll`:
1. Khởi tạo shared memory.
2. Đọc `IsVietnameseMode` từ shared memory.
3. Đồng bộ compartment local và icon.
4. Nếu `_pShared == null` (rất hiếm), dùng fallback `true`.

---

## 8. Definition of Done

- [ ] `GlobalVEState` class được tạo với API `IsVietnameseMode`, `SetVietnameseMode`, `ToggleVietnameseMode`, `ResyncFromSharedMemory`.
- [ ] `SharedMemoryManager.ToggleVietnameseMode` sử dụng atomic operation (`Interlocked.CompareExchange`).
- [ ] Tất cả code path đổi V/E đều đi qua `GlobalVEState`.
- [ ] `BridgeStateManager.Config` luôn đọc state mới nhất, không stale.
- [ ] `StateWatcher` resync compartment khi nhận event.
- [ ] Icon hiển thị luôn nhất quán với khả năng gõ tiếng Việt.
- [ ] Issue `004_EV_IconError.md` được cập nhật và đóng.

---

## 9. Action Items

| # | Công việc | File liên quan | Ưu tiên |
|---|---|---|---|
| 1 | Tạo `GlobalVEState.cs` | `src/BambooMintKey.NativeBridge/Common/GlobalVEState.cs` | Cao |
| 2 | Sửa `SharedMemoryManager.ToggleVietnameseMode` thành atomic | `src/BambooMintKey.NativeBridge/Common/SharedMemoryManager.cs` | Cao |
| 3 | Cập nhật `BridgeStateManager` dùng `GlobalVEState` | `src/BambooMintKey.NativeBridge/TSF/BridgeStateManager.cs` | Cao |
| 4 | Cập nhật `LangBarItemButton.OnClick` / `ExecuteMenuCommand` | `src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs` | Cao |
| 5 | Cập nhật `KeyEventSinkImpl.OnKeyDown` / `OnPreservedKey` | `src/BambooMintKey.NativeBridge/TSF/KeyEventSinkImpl.cs` | Cao |
| 6 | Cập nhật `BambooMintKeyTextService.StateWatcher` để resync compartment | `src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs` | Cao |
| 7 | Kiểm tra UI config gọi đúng API | `src/BambooMintKey.UI/MainWindow.axaml.fs` | Trung bình |
| 8 | Viết unit test / integration test cho toggle race | `tests/` | Trung bình |
| 9 | Thu thập log từ máy bị lỗi để xác nhận fix | `docs/3.Issue/004_EV_IconError.md` | Cao |

---

## 10. Tài liệu liên quan

- `docs/3.Issue/004_EV_IconError.md` — mô tả bug hiện tại và phương án debug.
- `docs/2.Design/Phase3/003_03_TaskbarButton_COM.md` — LangBarItemButton.
- `docs/2.Design/Phase3/003_04_IconHelper_DynamicRendering.md` — icon dynamic rendering.
- `docs/2.Design/Phase2/002_03_KeyEventSink_and_Core_Interop.md` — key event sink.
- Microsoft Docs: `ITfCompartment`, `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`.
