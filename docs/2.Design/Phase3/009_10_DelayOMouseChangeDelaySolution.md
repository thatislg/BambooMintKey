# 009_10_DelayOnMouseChangeDelaySolution.md — Thiết kế giải pháp triệt tiêu độ trễ và tối ưu phản hồi click chuột trên Taskbar Button

> **Tài liệu tham chiếu:**
>
> - File ghi nhận issue: `docs/2.Design/Phase3/003_08_Issue.md`
> - Bản thiết kế nền tảng: `docs/2.Design/Phase3/003_09_IssuesSolution.md`
> - Báo cáo kiểm thử: `walkthrough.md`
> - Các file liên quan: `LangBarItemButton.cs`, `BambooMintKeyTextService.cs`, `TsfCompartmentHelper.cs`, `TsfLangBarTypes.cs`

---

## 1. Bối cảnh & Hiện trạng sau đợt cập nhật đầu tiên

Sau khi triển khai 4 trụ cột kỹ thuật tại `003_09_IssuesSolution.md`:

- Biểu tượng Taskbar **V / E** đã ổn định hơn rõ rệt, hiện tượng giật lag/flicker đã giảm đáng kể.
- Khi chuyển sang **E** (Tiếng Anh), bộ gõ tắt hẳn tiếng Việt chuẩn xác (không bị gõ dính dấu).
- Tuy nhiên, qua trải nghiệm thực tế của người dùng, vẫn còn 2 điểm nghẽn về trải nghiệm chuột:
  1. **Độ trễ khi click chuột đổi mode:** Đổi bằng chuột mất từ **500ms đến 1000ms** thì icon trên Taskbar mới chuyển màu/chữ.
  2. **Click chuột liên tiếp nhanh thì không nhận:** Nếu người dùng nhấp chuột liên tục để đổi qua lại V $\leftrightarrow$ E, có những cú click bị trôi hoặc không ăn, phải đợi khoảng 1 giây mới bấm tiếp được.

Tài liệu này đi sâu phân tích cơ chế nội tại của Windows TSF khi xử lý click chuột trên Taskbar Shell và đưa ra giải pháp kỹ thuật triệt để để đưa độ trễ về **0ms (phản hồi tức thì)** và bắt trọn mọi cú click liên tiếp.

---

## 2. Phân tích nguyên nhân gốc rễ (Root Causes)

### 2.1. Nguyên nhân 1: Hàm `OnClick` chưa cập nhật TSF Input Mode Compartment

Trong kiến trúc Windows 10 và Windows 11, thanh tác vụ (Taskbar / System Tray) không chỉ lắng nghe `ITfLangBarItemSink::OnUpdate`, mà nó ưu tiên giám sát **TSF Compartment**: `GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION`.

Ở bản cập nhật trước, chúng ta đã tích hợp `TsfCompartmentHelper.SetConversionMode()` vào 2 vị trí:

- Khi khởi động TIP: `BambooMintKeyTextService.ActivateExImpl()`
- Khi bấm phím tắt: `KeyEventSinkImpl.OnKeyDown()` và `OnPreservedKey()`

**Tuy nhiên, trong `LangBarItemButton.OnClick()`, chúng ta hoàn toàn quên gọi `SetConversionMode()`!**

```csharp
// Mã hiện tại trong LangBarItemButton.cs:
private static int OnClick(IntPtr thisPtr, uint click, POINT pt, RECT* prcArea)
{
    if (click == TsfLangBarFlags.TfLbiClkLeft)
    {
        bool newMode = BridgeStateManager.ToggleVietnameseMode();
        NotifyStateChanged(); // <-- Chỉ mới gửi OnUpdate tới Sink, CHƯA GỬI COMPARTMENT!
    }
    return HResult.Ok;
}
```

**Hậu quả:**
Khi click chuột, icon nhận lệnh đổi trạng thái trong bộ nhớ và gửi `OnUpdate`. Nhưng vì Windows Shell Compartment chưa nhận được giá trị mới, Taskbar Input Indicator của Windows 10/11 **không chủ động vẽ lại ngay**, mà phải đợi **chu kỳ polling định kỳ (Background Polling Cycle kéo dài từ 500ms đến 1000ms)** của Windows Shell thì thanh Taskbar mới quét lại và cập nhật icon. Đây chính là lý do tạo ra độ trễ 500 - 1000ms!

---

### 2.2. Nguyên nhân 2: Thời gian Double-Click (500ms) của Windows và bộ lọc `click == TfLbiClkLeft`

Win32 và Windows Shell có cơ chế mặc định xác định nhấp đúp qua hàm `GetDoubleClickTime()` (thông thường là **500ms**):

- Khi người dùng click lần 1: Windows gửi sự kiện click chuột trái (`click = 2` tương ứng `TF_LBI_CLK_LEFT`).
- Khi người dùng click lần 2 liên tiếp nhanh (trong vòng 500ms): Windows Taskbar có thể gửi mã sự kiện khác (như click kép, hoặc trạng thái button pending) hoặc Shell đang giữ trạng thái "chờ" xem người dùng có double click hay không.
- Mã nguồn hiện tại chỉ chấp nhận đúng một giá trị duy nhất:
  ```csharp
  if (click == TsfLangBarFlags.TfLbiClkLeft) // Chỉ nhận đúng giá trị 2
  ```

  Mọi mã click khác (kể cả click dồn dập hoặc Windows gửi trạng thái mở rộng) đều bị `if` loại bỏ, dẫn đến việc cú click thứ 2 bị bỏ qua hoàn toàn cho đến khi hết cửa sổ thời gian 500ms.

---

### 2.3. Nguyên nhân 3: Kiểu dáng nút `dwStyle` chưa sử dụng `TF_LBI_STYLE_BTN_TOGGLE`

Trong `LangBarItemButton.GetInfo`:

```csharp
pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnButton | TsfLangBarFlags.TfLbiStyleShownInTray;
```

- `TF_LBI_STYLE_BTN_BUTTON` (`0x00010000`): Khai báo đây là một nút nhấn thông thường (Push Button). Windows Taskbar coi đây là một nút bấm thực thi một hành động tạm thời (như mở hộp thoại), và thường áp dụng hiệu ứng animation "nhấn xuống / nảy lên" trước khi sẵn sàng nhận click tiếp theo.
- `TF_LBI_STYLE_BTN_TOGGLE` (`0x00040000`): Microsoft WinSDK quy định đây là phong cách **Nút chuyển trạng thái (Toggle Button)**. Khi mang kiểu dáng này, Windows Taskbar hiểu rõ nút này hoạt động như một công tắc hai chiều (On/Off hay V/E). Mỗi lần click, Taskbar sẽ đảo trạng thái tức thì mà không cần chờ hiệu ứng animation giải phóng nút bấm.

---

## 3. Thiết kế giải pháp kỹ thuật chi tiết

```
                           [ Người dùng Click chuột trái ]
                                         │
                                         ▼
                 ┌────────────────────────────────────────────────┐
                 │        LangBarItemButton.OnClick               │
                 │  - Chấp nhận mọi click khác Chuột Phải         │
                 │  - Toggle BridgeStateManager.IsVietnameseMode  │
                 └───────────────────────┬────────────────────────┘
                                         │
                 ┌───────────────────────┴────────────────────────┐
                 │                                                │
                 ▼ (Kênh 1: Nano-giây)                            ▼ (Kênh 2: Tức thì)
   ┌───────────────────────────┐                    ┌───────────────────────────┐
   │ NotifyStateChanged()      │                    │ TsfCompartmentHelper      │
   │ -> ITfLangBarItemSink     │                    │ -> SetConversionMode()    │
   │    OnUpdate()             │                    │    (1 = V, 0 = E)         │
   └─────────────┬─────────────┘                    └─────────────┬─────────────┘
                 │                                                │
                 └───────────────────────┬────────────────────────┘
                                         │
                                         ▼
                 ┌────────────────────────────────────────────────┐
                 │       Windows 10/11 Taskbar Shell              │
                 │  - Nhận cả Sink Update VÀ Compartment Change   │
                 │  - Đổi icon ngay trong Frame hiện tại (<16ms)  │
                 │  - Sẵn sàng nhận cú click tiếp theo ngay       │
                 └────────────────────────────────────────────────┘
```

### 3.1. Giải pháp 1: Lưu trữ Context và gọi `SetConversionMode` ngay trong `OnClick`

#### 3.1.1. Lưu trữ `pThreadMgr` và `clientId` trong `LangBarItemButton`

Khi `Register` được gọi từ `BambooMintKeyTextService`:

```csharp
// Trong LangBarItemButton.cs:
private static IntPtr _pThreadMgr = IntPtr.Zero;
private static uint _clientId = 0;

public static void Register(IntPtr pThreadMgr, uint clientId)
{
    _pThreadMgr = pThreadMgr;
    _clientId = clientId;
    // ... logic đăng ký giữ nguyên ...
}
```

#### 3.1.2. Cập nhật đồng thời trong `OnClick`

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static int OnClick(IntPtr thisPtr, uint click, POINT pt, RECT* prcArea)
{
    DebugLog.Write($"LangBarItemButton OnClick ENTER click={click}");

    // Xử lý tất cả các cú click chuột trái (hoặc bất kỳ click nào không phải chuột phải)
    if (click != TsfLangBarFlags.TfLbiClkRight)
    {
        bool newMode = BridgeStateManager.ToggleVietnameseMode();

        // 1. Báo cho Sink vẽ lại Icon ngay lập tức
        NotifyStateChanged();

        // 2. Báo cho Windows Shell Compartment biết chế độ gõ đã chuyển đổi ngay lập tức
        if (_pThreadMgr != IntPtr.Zero)
        {
            TsfCompartmentHelper.SetConversionMode(_pThreadMgr, _clientId, newMode);
        }

        DebugLog.Write($"LangBarItemButton OnClick toggled IsVietnameseMode={newMode} (Sink + Compartment synchronized)");
    }

    return HResult.Ok;
}
```

**Hiệu quả:**
Cả 2 kênh thông báo của Windows Shell đều được kích hoạt tại đúng thời điểm click chuột:

- Kênh 1 (`OnUpdate`): Yêu cầu vẽ lại icon.
- Kênh 2 (`SetConversionMode`): Cập nhật trạng thái bộ gõ cấp hệ thống.
  $\rightarrow$ Triệt tiêu hoàn toàn thời gian chờ 500 - 1000ms, icon phản hồi tức thì (< 16ms, bằng đúng 1 khung hình hiển thị).

---

### 3.2. Giải pháp 2: Sử dụng Style `TF_LBI_STYLE_BTN_TOGGLE` trong `GetInfo`

Cập nhật thuộc tính `dwStyle` trong `LangBarItemButton.GetInfo`:

```csharp
pInfo->dwStyle = TsfLangBarFlags.TfLbiStyleBtnToggle |
                 TsfLangBarFlags.TfLbiStyleShownInTray;
```

- Thay thế `TfLbiStyleBtnButton` bằng `TfLbiStyleBtnToggle`.
- Giúp Taskbar đối xử với icon như một nút đảo trạng thái nhanh, loại bỏ trạng thái pending giữa các cú click liên tiếp.

---

### 3.3. Giải pháp 3: Nới lỏng điều kiện lọc click chuột

Thay vì kiểm tra nghiêm ngặt `click == TsfLangBarFlags.TfLbiClkLeft (2)`:

- Chuyển sang kiểm tra:
  ```csharp
  if (click != TsfLangBarFlags.TfLbiClkRight)
  ```
- Chuột phải (`TF_LBI_CLK_RIGHT = 1`) sẽ dành riêng cho context menu (theo thiết kế `003_05_TaskbarContextMenu.md`).
- Mọi tín hiệu click chuột trái (dù nhấp đơn, nhấp kép hay nhấp nhanh liên hoàn) đều được đón nhận và xử lý đảo trạng thái ngay tức thì mà không bị nuốt sự kiện.

---

## 4. Kế hoạch triển khai mã nguồn

| File                                                                                                                            | Thành phần thay đổi | Nội dung cụ thể                                                                                |
| ------------------------------------------------------------------------------------------------------------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------- |
| [`LangBarItemButton.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs)               | Fields                  | Bổ sung`_pThreadMgr` và `_clientId`.                                                        |
| [`LangBarItemButton.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs)               | `Register`            | Nhận thêm tham số`uint clientId` và lưu vào biến tĩnh.                                  |
| [`LangBarItemButton.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs)               | `GetInfo`             | Chuyển`dwStyle` sang `TfLbiStyleBtnToggle \| TfLbiStyleShownInTray`.                          |
| [`LangBarItemButton.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/LangBarItemButton.cs)               | `OnClick`             | Đổi điều kiện`click != TfLbiClkRight` và gọi `TsfCompartmentHelper.SetConversionMode`. |
| [`BambooMintKeyTextService.cs`](file:///d:/Kojin/BambooMintKey/src/BambooMintKey.NativeBridge/TSF/BambooMintKeyTextService.cs) | `ActivateExImpl`      | Truyền`(pThreadMgr, tfClientId)` vào `LangBarItemButton.Register`.                          |

---

## 5. Tiêu chí Đánh giá & Nghiệm thu

1. **Độ trễ click chuột (Latency):**
   - Click chuột trái vào icon Taskbar: icon đổi ngay lập tức giữa **V** và **E** trong thời gian mắt thường cảm nhận là tức thì (< 30ms, không còn cảm giác bị khựng 0.5s - 1s).
2. **Click liên tiếp nhanh (Rapid Clicks):**
   - Nhấp chuột liên tục 5 - 10 lần với tốc độ cao: icon chuyển đổi liên tục tương ứng với số lần bấm, không có cú click nào bị trôi hoặc phải "đợi 1 lúc mới bấm lại được".
3. **Tính tương thích:**
   - Trạng thái gõ tiếng Việt trong ứng dụng đồng bộ 100% với chữ hiển thị trên icon (V thì gõ được tiếng Việt, E thì tắt hẳn tiếng Việt).
