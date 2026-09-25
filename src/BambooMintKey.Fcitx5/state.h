// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#pragma once

#include <fcitx/inputcontextproperty.h>
#include "cabibridge.h"

namespace bamboomintkey {

/// Thuộc tính mở rộng gắn với mỗi InputContext, giữ một context handle độc lập
/// từ Core.Native để cách ly tuyệt đối bộ đệm gõ giữa các cửa sổ.
class BambooMintKeyState : public fcitx::InputContextProperty {
public:
    BambooMintKeyState() : handle_(bmk_context_create()) {}

    ~BambooMintKeyState() override {
        if (handle_) {
            bmk_context_free(handle_);
            handle_ = nullptr;
        }
    }

    /// Con trỏ handle của context C-ABI.
    void *handle() const { return handle_; }

    /// Đặt lại trạng thái gõ dở của context (khi chuyển focus / reset).
    void reset() {
        if (handle_) {
            bmk_context_reset(handle_);
        }
        prevLen = 0;
    }

    /// Số ký tự (UTF-8 code points) đã commit của từ hiện tại, dùng cho direct commit.
    int prevLen = 0;

private:
    void *handle_ = nullptr;
};

} // namespace bamboomintkey
