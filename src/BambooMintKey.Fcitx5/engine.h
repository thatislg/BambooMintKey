// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#pragma once

#include <fcitx/inputmethodengine.h>
#include <fcitx/action.h>
#include <fcitx-config/configuration.h>
#include <fcitx-utils/dbus/bus.h>
#include <fcitx-utils/dbus/objectvtable.h>
#include <fcitx-utils/event.h>
#include <memory>
#include <string>

#include "cabibridge.h"
#include "state.h"

namespace fcitx {
class Instance;
}

namespace bamboomintkey {

// Cấu hình native hiển thị trong fcitx5-configtool (khi Configurable=True).
FCITX_CONFIGURATION(
    BambooMintKeyConfig,
    fcitx::Option<bool> isVietnameseMode{this, "IsVietnameseMode", "Bật gõ tiếng Việt (V)", true};
    fcitx::Option<int> toneStyle{this, "ToneStyle", "Kiểu đặt dấu: 0 = mới (hòa), 1 = cũ (hoà)", 0};
    fcitx::Option<bool> autoRestoreEnglishWords{this, "AutoRestoreEnglishWords", "Tự khôi phục từ tiếng Anh", true};
    fcitx::Option<bool> allowRepeatKeyUndo{this, "AllowRepeatKeyUndo", "Gõ lặp dấu để undo", true};
    fcitx::Option<bool> allowLeadingWAsU{this, "AllowLeadingWAsU", "w đầu từ thành ư", false};
    fcitx::Option<bool> allowFreeTonePlacement{this, "AllowFreeTonePlacement", "Bỏ dấu tự do", true};
);

class BambooMintKeyEngine;

/// D-Bus object điều khiển trạng thái V/E (Single-owner).
/// Service: org.fcitx.Fcitx5.BambooMintKey
/// Path:    /org/fcitx/Fcitx5/BambooMintKey
/// Interface: org.fcitx.Fcitx5.BambooMintKey1
class BambooMintKeyDBus
    : public fcitx::dbus::ObjectVTable<BambooMintKeyDBus> {
public:
    BambooMintKeyDBus(fcitx::dbus::Bus &bus, BambooMintKeyEngine *engine);

    void setVietnameseMode(bool enabled);
    bool getVietnameseMode() const;
    bool toggleVietnameseMode();

    FCITX_OBJECT_VTABLE_METHOD(setVietnameseMode, "SetVietnameseMode", "b", "");
    FCITX_OBJECT_VTABLE_METHOD(getVietnameseMode, "GetVietnameseMode", "", "b");
    FCITX_OBJECT_VTABLE_METHOD(toggleVietnameseMode, "ToggleVietnameseMode", "",
                               "b");
    FCITX_OBJECT_VTABLE_SIGNAL(modeChanged, "ModeChanged", "b");

private:
    BambooMintKeyEngine *engine_;
};

/// Engine lõi của addon: đón sự kiện phím, gọi C-ABI và hiển thị preedit.
class BambooMintKeyEngine final : public fcitx::InputMethodEngine {
public:
    explicit BambooMintKeyEngine(fcitx::Instance *instance);
    ~BambooMintKeyEngine() override;

    void keyEvent(const fcitx::InputMethodEntry &entry,
                  fcitx::KeyEvent &keyEvent) override;
    void activate(const fcitx::InputMethodEntry &entry,
                  fcitx::InputContextEvent &event) override;
    void reset(const fcitx::InputMethodEntry &entry,
               fcitx::InputContextEvent &event) override;

    // Icon động theo trạng thái V/E.
    std::string overrideIcon(const fcitx::InputMethodEntry &entry) override;

    // V/E single-owner.
    void setVietnameseMode(bool enabled);
    bool vietnameseMode() const { return vietnameseMode_; }
    bool toggleVietnameseMode();

    // Cấu hình native (Configurable=True).
    const fcitx::Configuration *getConfig() const override;
    void setConfig(const fcitx::RawConfig &config) override;

    // Nạp cấu hình từ file XDG và áp dụng cho một context mới.
    void reloadConfigFromFile();
    void applyConfigToState(BambooMintKeyState *state);

private:
    // Xử lý phím theo mã hành động trả về từ C-ABI.
    void handleAction(fcitx::InputContext *ic, BambooMintKeyState *state,
                      int action, fcitx::KeyEvent &keyEvent);
    void updatePreedit(fcitx::InputContext *ic, BambooMintKeyState *state);
    void commitText(fcitx::InputContext *ic, BambooMintKeyState *state);

    // Direct commit (Level 2): thay text đã commit thay vì hiển thị preedit.
    void directCommit(fcitx::InputContext *ic, BambooMintKeyState *state);
    void directCommitFinal(fcitx::InputContext *ic, BambooMintKeyState *state);

    // Phân loại phím -> ký tự Unicode / word-break.
    bool isSystemModifier(const fcitx::Key &key) const;
    static bool isWordBreak(uint32_t unicode);

    // inotify watcher.
    void setupConfigWatcher();
    void onConfigFileChanged();

    // Mở Settings GUI (launch bamboomintkey-ui).
    void launchSettingsApp();

    fcitx::Instance *instance_;
    fcitx::SimpleInputContextPropertyFactory<BambooMintKeyState> factory_;
    BambooMintKeyConfig config_;

    std::unique_ptr<fcitx::SimpleAction> settingsAction_;

    // Trạng thái V/E và tùy chọn engine (single-owner).
    bool vietnameseMode_ = true;
    int toneStyle_ = ToneModern;
    bool autoRestoreEnglish_ = true;
    bool allowRepeatUndo_ = true;
    bool allowLeadingW_ = false;
    bool allowFreeTone_ = true;

    std::unique_ptr<fcitx::dbus::Bus> dbusBus_;
    std::unique_ptr<BambooMintKeyDBus> dbusObject_;

    int inotifyFd_ = -1;
    std::unique_ptr<fcitx::EventSourceIO> inotifyEvent_;
    std::string configPath_;
};

} // namespace bamboomintkey
