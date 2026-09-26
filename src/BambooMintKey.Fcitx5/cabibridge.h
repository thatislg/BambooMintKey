// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#pragma once

#include <cstdint>

// Khai báo C-ABI của libBambooMintKeyCore.so (xem 007_03_CoreNative_CABI_Design.md).
// Addon liên kết trực tiếp tới thư viện này qua target_link_libraries.
extern "C" {

// Phiên bản ABI.
int bmk_version(void);

// Chẩn đoán (dùng cho test rò rỉ bộ nhớ).
void bmk_gc_collect(void);
int bmk_get_live_context_count(void);

// Vòng đời context.
void *bmk_context_create(void);
void bmk_context_free(void *handle);
void bmk_context_reset(void *handle);

// Xử lý phím. Trả về ActionCode (xem enum bên dưới).
int bmk_process_key(void *handle, uint32_t unicodeChar);
int bmk_process_backspace(void *handle);
int bmk_process_wordbreak(void *handle, uint32_t breakChar);

// Trích xuất buffer UTF-8 (read-only, null-terminated; caller không free).
const char *bmk_get_preedit_text(void *handle);
const char *bmk_get_commit_text(void *handle);
int bmk_get_preedit_length(void *handle);

// Cấu hình runtime.
void bmk_set_options(void *handle, int isEnabled, int toneStyle,
                     int autoRestoreEnglish, int allowRepeatUndo,
                     int allowLeadingW, int allowFreeTone,
                     int enableVietnameseDictionary, int enableEnglishBacktracking);
int bmk_load_config_json(void *handle, const char *jsonUtf8);
}

namespace bamboomintkey {

// Mã hành động trả về bởi các hàm xử lý phím C-ABI.
enum ActionCode {
    ActionPassThrough = 0,     // Nhường phím cho ứng dụng.
    ActionConsume = 1,         // Nuốt phím (engine hiện chưa phát sinh).
    ActionUpdatePreedit = 2,   // Cập nhật chuỗi preedit.
    ActionCommitString = 3,    // Chốt từ vào ứng dụng.
};

// Cờ cấu hình đặt dấu (toneStyle): 0 = kiểu mới (hòa), 1 = kiểu cũ (hoà).
enum ToneStyle {
    ToneModern = 0,
    ToneTraditional = 1,
};

} // namespace bamboomintkey
