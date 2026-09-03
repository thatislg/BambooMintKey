# 003_09_IssuesSolution.md — Thiết kế giải pháp đồng bộ và ổn định Icon Taskbar Language Bar V/E

> **Tài liệu tham chiếu liên quan:**  
> - Issue ghi nhận lỗi: `docs/2.Design/Phase3/003_08_Issue.md`  
> - Log ghi nhận: `docs/2.Design/Phase3/BambooMintKey_Runtime.log`  
> - Thiết kế nút Taskbar COM: `docs/2.Design/Phase3/003_03_TaskbarButton_COM.md`  
> - Thiết kế vẽ icon GDI: `docs/2.Design/Phase3/003_04_IconHelper_DynamicRendering.md`  
> - Phạm vi ảnh hưởng: `BambooMintKey.NativeBridge.Common.SharedMemoryManager`, `BambooMintKey.NativeBridge.TSF.LangBarItemButton`, `BambooMintKey.NativeBridge.TSF.IconHelper`, `BambooMintKey.NativeBridge.TSF.BambooMintKeyTextService`

---

## 1. Bối cảnh & Bản chất vấn đề

Trong Windows 10 và 11, thanh tác vụ (Taskbar / System Tray) hiển thị biểu tượng bộ gõ và cho phép chuyển đổi chế độ gõ giữa Tiếng Việt (**V**) và Tiếng Anh (**E**). 

Thực tế kiểm thử và file log runtime `BambooMintKey_Runtime.log` ghi nhận hiện tượng:
- Click chuột trái vào icon đôi khi đổi chế độ đúng, nhưng đôi khi chế độ trong engine đã đổi mà icon trên màn hình không đổi theo.
- Icon đôi khi biến mất hoặc giật/flicker khi chuyển đổi giữa các cửa sổ ứng dụng (Notepad, Chrome, VS Code,...).
- Đan xen các dòng log: `_pLangBarSink is NULL, cannot notify` và `OnUpdate sent to Windows Taskbar hr=0x00000000`, nhưng sau đó Taskbar không gọi lại `GetIcon` để vẽ lại.

Tài liệu này phân tích cặn kẽ 4 nguyên nhân kỹ thuật gốc rễ và đưa ra giải pháp thiết kế toàn diện, đồng bộ đa tiến trình chuẩn mực cho Windows TSF.

---

## 2. Bóc tách & Phân tích nguyên nhân gốc rễ (Root Causes)

### 2.1. Đính chính số liệu 705 icon trong Runtime Log
- Log từ dòng 1 đến 7768 mang PID `[17368]`, đây là tiến trình chạy test tự động `BambooMintKey.DevHarness` (`StressTestIconHelper` 500 icon + `ParallelIconTest` 200 icon + 5 icon mẫu = 705 icon). 
- Trong phiên gõ thực tế của người dùng từ dòng 7769 trở đi, `GetIcon` chỉ được gọi **đúng 1 lần duy nhất** khi bộ gõ khởi động.
- **Vấn đề thực sự:** Khi `OnUpdate` được gửi thành công 14 lần qua `ITfLangBarItemSink::OnUpdate`, Windows Taskbar **không hề gọi lại `GetIcon`** để lấy hình mới!

### 2.2. Nguyên nhân 1: Xung đột vòng đời đa tiến trình & Hiện tượng "Zombie COM Instance"
`BambooMintKey.NativeBridge.dll` là in-process COM DLL được nạp trực tiếp vào **mọi tiến trình** có vùng nhập văn bản:
1. **Tiến trình A** (ví dụ Notepad mở đầu tiên) gọi `ActivateEx` $\rightarrow$ gọi `LangBarItemButton.Register(pThreadMgr)` $\rightarrow$ `AddItem(_comInstance_A)` thành công. Windows Taskbar gọi `AdviseSink` trên `_comInstance_A` $\rightarrow$ tại Tiến trình A, `_pLangBarSink != NULL`.
2. **Tiến trình B** (ví dụ Chrome mở sau) gọi `ActivateEx` $\rightarrow$ `AddItem(_comInstance_B)` với cùng `GUID_LBI_INPUTMODE`. Windows TSF trả về lỗi `TF_E_ALREADY_EXISTS` (hoặc bỏ qua) vì item đã tồn tại. Windows **không gọi `AdviseSink` trên Tiến trình B** $\rightarrow$ tại Tiến trình B, `_pLangBarSink` mãi mãi là `NULL`!
3. Trong `BambooMintKeyTextService.cs`, hàm `LangBarItemButton.Unregister()` bị comment bỏ tại `DeactivateImpl`.
4. Khi Tiến trình A bị đóng: Vùng nhớ chứa `_comInstance_A` bị hệ điều hành thu hồi. Windows Taskbar lúc này giữ một con trỏ COM chết (**Zombie/Dangling COM Pointer**). 
5. Mọi click chuột vào Taskbar sau đó gọi vào COM object đã chết $\rightarrow$ sinh lỗi RPC disconnect (`0x800706BA`), icon biến mất hoặc đơ không phản hồi. Đồng thời, các tiến trình còn lại (như Chrome) khi gõ phím tắt đổi mode chỉ có `_pLangBarSink == NULL` nên `NotifyStateChanged()` bị bỏ qua hoàn toàn.

### 2.3. Nguyên nhân 2: Win32 Auto-Reset Event nuốt chửng tín hiệu giữa các tiến trình
File `SharedMemoryManager.cs` dòng 155 khởi tạo Win32 Event:
```csharp
_hEvent = CreateEventW(pSaPtr, false /* AutoReset */, false, EventName);
```
- Win32 Auto-Reset Event (`bManualReset = false`) có cơ chế: **Khi `SetEvent` được gọi, chỉ duy nhất 1 thread/tiến trình đang chờ được đánh thức**, và Event tự động hạ về non-signaled ngay lập tức.
- Khi có nhiều tiến trình chạy song song: Tiến trình B (không có sink) bấm hotkey đổi mode $\rightarrow$ gọi `SignalStateChanged()`. Tiến trình nuốt mất event lại chính là Tiến trình B (hoặc DevHarness), còn Tiến trình A (tiến trình duy nhất đang nắm Sink tới Taskbar) không hề nhận được event để gọi `NotifyStateChanged()`.

### 2.4. Nguyên nhân 3: Thiếu cập nhật TSF Input Mode Compartment (Chuẩn Windows 10/11)
- Trên Windows 8/10/11, Taskbar System Tray / Input Indicator quản lý trạng thái hiển thị của IME thông qua cơ chế Compartment: `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION` (`{CCF05DD7-4A87-11D7-A6E2-00065B84435C}`).
- Trong `TsfRegistration.cs`, dự án đã đăng ký Category `GuidTfCatTipCapInputModeCompartment`, nhưng trong mã nguồn chưa từng có logic cập nhật Compartment này khi chuyển đổi V/E.
- Do thiếu đồng bộ Compartment, Windows Shell không nhận được thông báo cấp hệ thống, dẫn đến việc Taskbar không kích hoạt redraw cycle cho `ITfLangBarItemButton`.

### 2.5. Nguyên nhân 4: Rủi ro hiệu năng & flicker khi tạo lại toàn bộ HICON
- Mỗi lần `GetIcon` được gọi, `IconHelper.CreateBambooIcon` tạo mới 2 DC, 2 Bitmap, Brush, Pen, Font, RoundRect, DrawText, CreateIconIndirect rồi hủy 8 GDI object.
- Dù WinSDK quy định caller chịu trách nhiệm `DestroyIcon`, cách làm chuẩn công nghiệp (tương tự Google Mozc) là **cache sẵn 2 HICON gốc (V và E) theo DPI hệ thống** và dùng Win32 `CopyIcon(hCachedIcon)` để trao bản sao độc lập cho Windows. `CopyIcon` tốn dưới 1 microsecond, không cấp phát font/DIB, triệt tiêu hoàn toàn flicker.

---

## 3. Thiết kế giải pháp kỹ thuật chi tiết

Giải pháp gồm 4 trụ cột kỹ thuật đồng bộ:

```
+-----------------------------------------------------------------------------+
|                      Cross-Process Shared Memory                            |
|  - IsVietnameseMode (byte 0)                                                |
|  - StateSequence (uint @ byte 8) -> Tăng dần mỗi lần đổi trạng thái         |
|  - Manual-Reset Event (Broadcast tới TẤT CẢ các tiến trình đang chờ)        |
+-----------------------------------------------------------------------------+
         |                                                 |
         v                                                 v
+-----------------------------+               +-----------------------------+
|  Tiến trình A (Notepad)     |               |  Tiến trình B (Chrome)      |
|  - StartEventListener       |               |  - StartEventListener       |
|  - Thấy Sequence thay đổi   |               |  - Thấy Sequence thay đổi   |
|  - Nếu có _pLangBarSink:    |               |  - Cập nhật Engine Config   |
|    -> Gửi OnUpdate()        |               |  - Cập nhật Compartment     |
|  - Cập nhật Compartment     |               +-----------------------------+
+-----------------------------+
         |
         v
+-----------------------------------------------------------------------------+
|                     Windows Taskbar / System Tray                           |
|  - Nhận OnUpdate() từ tiến trình nắm Sink                                    |
|  - Nhận Compartment Changed (Conversion Mode On/Off)                        |
|  - Gọi GetIcon() -> IconHelper trả về CopyIcon(_cachedIcon) ngay lập tức    |
+-----------------------------------------------------------------------------+
```

---

### 3.1. Trụ cột 1: Chuẩn hóa Đồng bộ Broadcast trong `SharedMemoryManager`

#### 3.1.1. Cấu trúc Vùng nhớ Dùng chung (Shared Memory Layout - 64 bytes)
| Offset | Kích thước | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|---|
| `0` | 1 byte | `byte` | `IsVietnameseMode` (1 = V, 0 = E) |
| `1` | 1 byte | `byte` | `ToneStyle` (0 = Mới, 1 = Cũ) |
| `2` | 1 byte | `byte` | `AutoRestoreEnglishWords` (1 = Bật, 0 = Tắt) |
| `3` | 1 byte | `byte` | `AllowRepeatKeyUndo` (1 = Bật, 0 = Tắt) |
| `4` | 1 byte | `byte` | `AllowLeadingWAsU` (1 = Bật, 0 = Tắt) |
| `5 - 7` | 3 bytes | - | Reserved / Padding |
| `8 - 11` | 4 bytes | `uint` | **`StateSequence`**: Số đếm phiên bản trạng thái (tăng 1 mỗi lần có thay đổi) |
| `12 - 63`| 52 bytes | - | Reserved cho cấu hình mở rộng tương lai |

#### 3.1.2. Chuyển đổi sang Manual-Reset Event
- Thay đổi tham số `bManualReset` trong `CreateEventW`:
  ```csharp
  _hEvent = CreateEventW(pSaPtr, true /* ManualReset */, false, EventName);
  ```
- Phương thức phát tín hiệu `SignalStateChanged`:
  ```csharp
  public static void SignalStateChanged()
  {
      if (_pShared != null)
      {
          // Tăng StateSequence an toàn đa luồng
          fixed (byte* p = &_pShared[8])
          {
              System.Threading.Interlocked.Increment(ref *(int*)p);
          }
      }
      if (_hEvent != IntPtr.Zero)
      {
          // Đánh thức TẤT CẢ các tiến trình đang chờ
          SetEvent(_hEvent);
          // Hạ tín hiệu để sẵn sàng cho lần thay đổi tiếp theo
          ResetEvent(_hEvent);
      }
  }
  ```

#### 3.1.3. Cơ chế Lắng nghe `StartEventListener` trong mọi tiến trình
Mỗi tiến trình duy trì biến cục bộ `uint _localSequence = 0;`:
```csharp
uint currentSeq = SharedMemoryManager.StateSequence;
if (currentSeq != _localSequence)
{
    _localSequence = currentSeq;
    bool currentMode = SharedMemoryManager.IsVietnameseMode;
    // Bắn thông báo cập nhật Taskbar nếu tiến trình này đang sở hữu Sink
    NotifyStateChanged();
    // Đồng bộ trạng thái vào TSF Compartment của Thread hiện tại
    SyncInputModeCompartment(currentMode);
}
```
Nhờ cơ chế `StateSequence`, ngay cả khi một tiến trình khởi động sau hoặc trễ nhịp tín hiệu, nó vẫn luôn đồng bộ chính xác phiên bản trạng thái mới nhất.

---

### 3.2. Trụ cột 2: Tích hợp TSF Input Mode Compartment (`TsfCompartmentHelper`)

Windows 10/11 Input Indicator giám sát compartment `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`. Khi chuyển sang Tiếng Việt (V), compartment nhận giá trị `1` (`TF_CONVERSIONMODE_ALPHANUMERIC` hoặc `TF_CONVERSIONMODE_NATIVE`). Khi chuyển sang Tiếng Anh (E), compartment nhận giá trị `0`.

#### 3.2.1. Khai báo GUID và VTable
```csharp
// GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION (ctffunc.h)
public static readonly Guid GuidCompartmentKeyboardInputModeConversion = 
    new("CCF05DD7-4A87-11D7-A6E2-00065B84435C");

// ITfCompartmentMgr (msctf.h)
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfCompartmentMgrVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfCompartmentMgr (3 - 6)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> GetCompartment;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> ClearCompartment;
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int> EnumCompartments;
}

// ITfCompartment (msctf.h)
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ITfCompartmentVTable
{
    // IUnknown (0 - 2)
    public delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int> QueryInterface;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> AddRef;
    public delegate* unmanaged[Stdcall]<IntPtr, uint> Release;

    // ITfCompartment (3 - 4)
    public delegate* unmanaged[Stdcall]<IntPtr, uint, VARIANT*, int> SetValue;
    public delegate* unmanaged[Stdcall]<IntPtr, VARIANT*, int> GetValue;
}
```

#### 3.2.2. Logic gán giá trị Compartment
Khi chế độ thay đổi, hàm `SetInputModeCompartment(IntPtr pThreadMgr, uint clientId, bool isVietnamese)`:
1. Lấy `ITfCompartmentMgr` từ `pThreadMgr` qua `QueryInterface`.
2. Gọi `GetCompartment(GuidCompartmentKeyboardInputModeConversion, &pComp)`.
3. Tạo struct `VARIANT` kiểu `VT_I4` (Integer 32-bit):
   - Tiếng Việt: `val = 1`
   - Tiếng Anh: `val = 0`
4. Gọi `pComp->SetValue(clientId, &varValue)`.
5. Giải phóng con trỏ `ITfCompartment` và `ITfCompartmentMgr`.

---

### 3.3. Trụ cột 3: Tối ưu Caching & Win32 `CopyIcon` trong `IconHelper`

Để loại bỏ hoàn toàn độ trễ vẽ GDI và triệt tiêu nguy cơ rò rỉ handle:

#### 3.3.1. Kiến trúc Caching tĩnh
- Duy trì 2 con trỏ HICON tĩnh làm mẫu: `_cachedIconV` và `_cachedIconE`.
- Khởi tạo 1 lần khi DLL được nạp hoặc khi DPI màn hình thay đổi.
- Khi Windows gọi `GetIcon(phIcon)`:
  ```csharp
  public static IntPtr GetBambooIconHandle(string text)
  {
      EnsureIconsCreated();
      IntPtr sourceIcon = (text == "V") ? _cachedIconV : _cachedIconE;
      // Win32 CopyIcon tạo một bản sao độc lập bàn giao quyền sở hữu cho Windows
      return CopyIcon(sourceIcon);
  }
  ```
- **Lợi ích**:
  - Thời gian phản hồi < 1 microsecond.
  - Không tạo/xóa DC, Bitmap, Font liên tục.
  - Tuân thủ 100% hợp đồng COM của Windows TSF: Windows tự do gọi `DestroyIcon` trên bản sao mà không ảnh hưởng tới icon mẫu.

---

### 3.4. Trụ cột 4: Chuẩn hóa `LangBarItemButton` và Quản lý Sink An toàn Đa luồng

#### 3.4.1. Chuẩn hóa `AdviseSink`
- Bỏ hoàn toàn fallback mù quáng `_pLangBarSink = punk`.
- Chỉ lưu `_pLangBarSink` khi `QueryInterface(IID_ITfLangBarItemSink)` thành công (`hr == HResult.Ok && pSink != IntPtr.Zero`).
- Quản lý `_pLangBarSink` bằng biến `volatile` kèm giải phóng an toàn `NativeCom.Release`.

#### 3.4.2. Vòng đời Đăng ký Nút trên Taskbar (`Register` và `Unregister`)
- Không hủy item khi chỉ mất focus tạm thời giữa 2 cửa sổ có cùng bộ gõ.
- Thay vào đó, mỗi tiến trình khi kích hoạt (`ActivateEx`) sẽ kiểm tra trạng thái đăng ký của mình. Nếu là tiến trình đang có focus mà chưa có sink, tiến hành kết nối hoặc làm mới trạng thái hiển thị.
- Khi người dùng click chuột trái vào icon Taskbar:
  - `OnClick` gọi `BridgeStateManager.ToggleVietnameseMode()`.
  - `ToggleVietnameseMode()` cập nhật Shared Memory và gọi `SignalStateChanged()`.
  - TẤT CẢ các tiến trình đang chạy đều nhận được tín hiệu qua Manual-Reset Event và `StateSequence`. Tiến trình đang nắm giữ Sink tới Taskbar sẽ lập tức gửi `OnUpdate` vẽ lại icon ngay trong frame hình hiện tại.

---

## 4. Kế hoạch Triển khai (Implementation Steps)

| Bước | Thành phần | File thực hiện | Nội dung chi tiết |
|---|---|---|---|
| **1** | Shared Memory | `src/.../Common/SharedMemoryManager.cs` | Thêm `StateSequence`, đổi sang Manual-Reset Event, thêm `Pulse/ResetEvent`. |
| **2** | Icon Caching | `src/.../TSF/IconHelper.cs` | Thêm `CopyIcon` P/Invoke, tạo cache tĩnh `_cachedIconV`/`_cachedIconE`, tối ưu `CreateBambooIcon`. |
| **3** | Compartment Sync | `src/.../TSF/TsfCompartmentHelper.cs` | Tạo helper quản lý `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`. |
| **4** | Taskbar Button | `src/.../TSF/LangBarItemButton.cs` | Chuẩn hóa `AdviseSink`, cập nhật `StartEventListener` dựa theo `StateSequence`. |
| **5** | Service Lifecycle | `src/.../TSF/BambooMintKeyTextService.cs` | Đồng bộ Compartment khi `ActivateEx`, kết nối `StateSequence`. |
| **6** | Validation | `BambooMintKey.DevHarness` | Thêm kịch bản test chuyển đổi đa tiến trình giả lập, đo GDI handles và kiểm tra tính toàn vẹn. |

---

## 5. Tiêu chí Đánh giá & Nghiệm thu (Acceptance Criteria)

1. **Phản hồi Click chuột tức thì:** Click chuột trái vào icon Taskbar V/E lập tức chuyển đổi màu sắc/ký tự giữa V và E mà không cần độ trễ, không có hiện tượng "click lần 1 không ăn, lần 2 mới đổi".
2. **Không phụ thuộc vào tiến trình đầu tiên:** Mở Notepad 1 $\rightarrow$ Mở Notepad 2 $\rightarrow$ Đóng Notepad 1 $\rightarrow$ Icon trên Taskbar vẫn hoạt động bình thường trên Notepad 2, không biến mất, không đơ.
3. **Đồng bộ Phím tắt xuyên suốt:** Nhấn phím tắt toggle (`Ctrl + Shift` hoặc `Alt + Z`) ở bất kỳ ứng dụng nào (kể cả trong game, trình duyệt, terminal) thì icon Taskbar cũng đổi trạng thái theo ngay lập tức.
4. **Không rò rỉ tài nguyên (Zero GDI Leak):** Số lượng GDI Objects của các tiến trình không tăng khi click liên tục 500 lần vào icon Taskbar.
