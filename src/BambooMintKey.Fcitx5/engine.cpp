// BambooMintKey - Vietnamese Telex Input Method Editor
// Copyright (c) 2026 Dương Gia Long and LMO contributors
// SPDX-License-Identifier: MIT
#include "engine.h"

#include <fcitx/inputcontext.h>
#include <fcitx/inputpanel.h>
#include <fcitx/instance.h>
#include <fcitx/text.h>
#include <fcitx/userinterfacemanager.h>
#include <fcitx/addonfactory.h>
#include <fcitx/addonmanager.h>
#include <fcitx-utils/keysym.h>
#include <fcitx-utils/utf8.h>
#include <fcitx-utils/capabilityflags.h>

#include <cctype>
#include <cstdlib>
#include <fstream>
#include <sstream>
#include <sys/inotify.h>
#include <unistd.h>

namespace bamboomintkey {

// =========================================================================
// BambooMintKeyDBus
// =========================================================================

BambooMintKeyDBus::BambooMintKeyDBus(fcitx::dbus::Bus &bus,
                                     BambooMintKeyEngine *engine)
    : engine_(engine) {
    bus.addObjectVTable("/org/fcitx/Fcitx5/BambooMintKey",
                        "org.fcitx.Fcitx5.BambooMintKey1", *this);
}

void BambooMintKeyDBus::setVietnameseMode(bool enabled) {
    engine_->setVietnameseMode(enabled);
}

bool BambooMintKeyDBus::getVietnameseMode() const {
    return engine_->vietnameseMode();
}

bool BambooMintKeyDBus::toggleVietnameseMode() {
    return engine_->toggleVietnameseMode();
}

// =========================================================================
// BambooMintKeyEngine
// =========================================================================

BambooMintKeyEngine::BambooMintKeyEngine(fcitx::Instance *instance)
    : instance_(instance) {
    instance_->inputContextManager().registerProperty("bambooMintKeyState",
                                                      &factory_);

    // D-Bus service (single-owner cho V/E).
    dbusBus_ =
        std::make_unique<fcitx::dbus::Bus>(fcitx::dbus::BusType::Session);
    dbusBus_->attachEventLoop(&instance_->eventLoop());
    dbusBus_->requestName("org.fcitx.Fcitx5.BambooMintKey",
                            fcitx::dbus::RequestNameFlag::ReplaceExisting);
    dbusObject_ = std::make_unique<BambooMintKeyDBus>(*dbusBus_, this);

    reloadConfigFromFile();
    setupConfigWatcher();
}

BambooMintKeyEngine::~BambooMintKeyEngine() {
    if (inotifyFd_ >= 0) {
        close(inotifyFd_);
        inotifyFd_ = -1;
    }
}

void BambooMintKeyEngine::activate(const fcitx::InputMethodEntry &entry,
                                   fcitx::InputContextEvent &event) {
    FCITX_UNUSED(entry);
    auto *ic = event.inputContext();
    auto *state = ic->propertyFor(&factory_);
    applyConfigToState(state);
}

void BambooMintKeyEngine::reset(const fcitx::InputMethodEntry &entry,
                                 fcitx::InputContextEvent &event) {
    FCITX_UNUSED(entry);
    auto *ic = event.inputContext();
    auto *state = ic->propertyFor(&factory_);
    state->reset();
    ic->inputPanel().reset();
    ic->inputPanel().setClientPreedit(fcitx::Text());
    ic->updatePreedit();
}

std::string BambooMintKeyEngine::overrideIcon(const fcitx::InputMethodEntry &entry) {
    FCITX_UNUSED(entry);
    return vietnameseMode_ ? "fcitx_bamboomintkey" : "fcitx_bamboomintkey_e";
}

// =========================================================================
// V/E single-owner
// =========================================================================

void BambooMintKeyEngine::setVietnameseMode(bool enabled) {
    if (vietnameseMode_ == enabled) {
        return;
    }
    vietnameseMode_ = enabled;
    dbusObject_->modeChanged(vietnameseMode_);
}

bool BambooMintKeyEngine::toggleVietnameseMode() {
    setVietnameseMode(!vietnameseMode_);
    return vietnameseMode_;
}

// =========================================================================
// keyEvent pipeline (M2.4)
// =========================================================================

void BambooMintKeyEngine::keyEvent(const fcitx::InputMethodEntry &entry,
                                   fcitx::KeyEvent &keyEvent) {
    FCITX_UNUSED(entry);

    // 1. Bỏ qua sự kiện nhả phím.
    if (keyEvent.isRelease()) {
        return;
    }

    // Hotkey cứng: phím ` (grave, dưới Esc) đổi V/E — giống chuẩn Kata/Romaji.
    if (keyEvent.key().sym() == FcitxKey_grave &&
        !isSystemModifier(keyEvent.key())) {
        toggleVietnameseMode();
        keyEvent.filterAndAccept();
        // Làm mới icon V/E (dùng context thật, không nullptr).
        keyEvent.inputContext()->updateUserInterface(
            fcitx::UserInterfaceComponent::StatusArea, true);
        return;
    }

    // 2. Nếu đang ở chế độ tiếng Anh (E), nhường phím cho ứng dụng.
    if (!vietnameseMode_) {
        return;
    }

    // 3. Bỏ qua tổ hợp phím tắt hệ thống (Ctrl/Alt/Super).
    if (isSystemModifier(keyEvent.key())) {
        return;
    }

    auto *ic = keyEvent.inputContext();
    auto *state = ic->propertyFor(&factory_);
    auto *handle = state->handle();

    const auto sym = keyEvent.key().sym();

    // 4. Backspace.
    if (sym == FcitxKey_BackSpace) {
        handleAction(ic, state, bmk_process_backspace(handle), keyEvent);
        return;
    }

    const uint32_t unicode = fcitx::Key::keySymToUnicode(sym);
    if (unicode == 0) {
        return; // Ký tự không in được -> bỏ qua.
    }

    // 5. Phím ngắt từ (space/enter/tab/dấu câu).
    if (isWordBreak(unicode)) {
        handleAction(ic, state, bmk_process_wordbreak(handle, unicode),
                     keyEvent);
        return;
    }

    // 6. Ký tự gõ thông thường (ASCII in được).
    if (unicode >= 0x20 && unicode <= 0x7E) {
        handleAction(ic, state, bmk_process_key(handle, unicode), keyEvent);
    }
}

void BambooMintKeyEngine::handleAction(fcitx::InputContext *ic,
                                       BambooMintKeyState *state, int action,
                                       fcitx::KeyEvent &keyEvent) {
    // Hiện tại dùng preedit (Level 1) cho mọi app, vì direct commit (Level 2)
    // phụ thuộc deleteSurroundingText — một số app (vd Zed) báo có hỗ trợ
    // SurroundingText nhưng thực tế không xử lý xóa, gây lỗi x2/x3 nội dung.
    switch (action) {
    case ActionUpdatePreedit:
        updatePreedit(ic, state);
        keyEvent.filterAndAccept();
        break;
    case ActionCommitString:
        commitText(ic, state);
        keyEvent.filterAndAccept();
        break;
    case ActionConsume:
        keyEvent.filterAndAccept();
        break;
    case ActionPassThrough:
    default:
        break;
    }
}

// =========================================================================
// Direct commit (Level 2)
// =========================================================================

void BambooMintKeyEngine::directCommit(fcitx::InputContext *ic,
                                       BambooMintKeyState *state) {
    const char *text = bmk_get_preedit_text(state->handle());
    const std::string str = text ? text : "";
    const int newLen = static_cast<int>(fcitx::utf8::length(str));

    if (state->prevLen > 0) {
        ic->deleteSurroundingText(-state->prevLen,
                                  static_cast<unsigned int>(state->prevLen));
    }
    ic->commitString(str);
    state->prevLen = newLen;
}

void BambooMintKeyEngine::directCommitFinal(fcitx::InputContext *ic,
                                            BambooMintKeyState *state) {
    const char *text = bmk_get_commit_text(state->handle());
    const std::string str = text ? text : "";

    if (state->prevLen > 0) {
        ic->deleteSurroundingText(-state->prevLen,
                                  static_cast<unsigned int>(state->prevLen));
    }
    if (!str.empty()) {
        ic->commitString(str);
    }
    state->prevLen = 0;
}

// =========================================================================
// Preedit UI (M2.5)
// =========================================================================

void BambooMintKeyEngine::updatePreedit(fcitx::InputContext *ic,
                                        BambooMintKeyState *state) {
    const char *text = bmk_get_preedit_text(state->handle());

    fcitx::Text preedit;
    if (text && text[0] != '\0') {
        // Không dùng underline: tiếng Việt có dấu nặng (chấm dưới) nên gạch chân gây rối mắt.
        preedit.append(text);
        preedit.setCursor(-1);
    }

    // Set cả preedit popup (candidate window) lẫn client preedit (inline trong app).
    ic->inputPanel().setPreedit(preedit);
    ic->inputPanel().setClientPreedit(preedit);
    ic->updatePreedit();
}

void BambooMintKeyEngine::commitText(fcitx::InputContext *ic,
                                     BambooMintKeyState *state) {
    const char *text = bmk_get_commit_text(state->handle());
    ic->inputPanel().reset();
    ic->inputPanel().setClientPreedit(fcitx::Text());
    ic->updatePreedit();
    if (text && text[0] != '\0') {
        ic->commitString(text);
    }
}

// =========================================================================
// Phân loại phím
// =========================================================================

bool BambooMintKeyEngine::isSystemModifier(const fcitx::Key &key) const {
    return key.states().testAny(fcitx::KeyState::Ctrl) ||
           key.states().testAny(fcitx::KeyState::Alt) ||
           key.states().testAny(fcitx::KeyState::Super);
}

bool BambooMintKeyEngine::isWordBreak(uint32_t unicode) {
    if (unicode > 0x7F) {
        return false; // Ký tự ngoài ASCII không coi là ngắt từ.
    }
    return std::isspace(static_cast<int>(unicode)) ||
           std::ispunct(static_cast<int>(unicode));
}

// =========================================================================
// Cấu hình (M2.7)
// =========================================================================

namespace {

std::string configFilePath() {
    const char *xdg = std::getenv("XDG_CONFIG_HOME");
    std::string base;
    if (xdg && xdg[0] != '\0') {
        base = xdg;
    } else {
        const char *home = std::getenv("HOME");
        base = std::string(home ? home : "/tmp") + "/.config";
    }
    return base + "/bamboomintkey/config.json";
}

std::string readFile(const std::string &path) {
    std::ifstream in(path);
    if (!in) {
        return {};
    }
    std::ostringstream ss;
    ss << in.rdbuf();
    return ss.str();
}

bool jsonGetBool(const std::string &json, const std::string &key,
                 bool defaultValue) {
    auto pos = json.find("\"" + key + "\"");
    if (pos == std::string::npos) {
        return defaultValue;
    }
    pos = json.find(':', pos + key.size() + 2);
    if (pos == std::string::npos) {
        return defaultValue;
    }
    pos = json.find_first_not_of(" \t\r\n", pos + 1);
    if (pos == std::string::npos) {
        return defaultValue;
    }
    if (json.compare(pos, 4, "true") == 0) {
        return true;
    }
    if (json.compare(pos, 5, "false") == 0) {
        return false;
    }
    return defaultValue;
}

int jsonGetInt(const std::string &json, const std::string &key,
               int defaultValue) {
    auto pos = json.find("\"" + key + "\"");
    if (pos == std::string::npos) {
        return defaultValue;
    }
    pos = json.find(':', pos + key.size() + 2);
    if (pos == std::string::npos) {
        return defaultValue;
    }
    pos = json.find_first_not_of(" \t\r\n", pos + 1);
    if (pos == std::string::npos) {
        return defaultValue;
    }
    char *end = nullptr;
    long value = std::strtol(json.c_str() + pos, &end, 10);
    if (end == json.c_str() + pos) {
        return defaultValue;
    }
    return static_cast<int>(value);
}

} // namespace

void BambooMintKeyEngine::reloadConfigFromFile() {
    const auto json = readFile(configFilePath());
    if (json.empty()) {
        return;
    }

    toneStyle_ = jsonGetInt(json, "toneStyle", ToneModern);
    autoRestoreEnglish_ = jsonGetBool(json, "autoRestoreEnglishWords", true);
    allowRepeatUndo_ = jsonGetBool(json, "allowRepeatKeyUndo", true);
    allowLeadingW_ = jsonGetBool(json, "allowLeadingWAsU", false);
    allowFreeTone_ = jsonGetBool(json, "allowFreeTonePlacement", true);
    vietnameseMode_ = jsonGetBool(json, "isVietnameseMode", true);
}

void BambooMintKeyEngine::applyConfigToState(BambooMintKeyState *state) {
    bmk_set_options(state->handle(),
                    /*isEnabled=*/true, toneStyle_, autoRestoreEnglish_,
                    allowRepeatUndo_, allowLeadingW_, allowFreeTone_);
}

void BambooMintKeyEngine::setupConfigWatcher() {
    inotifyFd_ = inotify_init1(IN_NONBLOCK | IN_CLOEXEC);
    if (inotifyFd_ < 0) {
        return;
    }

    const auto path = configFilePath();
    const auto slash = path.find_last_of('/');
    const auto dir = slash == std::string::npos ? "." : path.substr(0, slash);

    if (inotify_add_watch(inotifyFd_, dir.c_str(),
                          IN_CLOSE_WRITE | IN_MOVED_TO | IN_CREATE) < 0) {
        close(inotifyFd_);
        inotifyFd_ = -1;
        return;
    }

    inotifyEvent_ = instance_->eventLoop().addIOEvent(
        inotifyFd_, fcitx::IOEventFlag::In,
        [this](fcitx::EventSourceIO *, int, fcitx::IOEventFlags) {
            onConfigFileChanged();
            return true;
        });
}

void BambooMintKeyEngine::onConfigFileChanged() {
    // Đọc hết các sự kiện inotify để tránh lặp lại.
    char buf[4096];
    while (read(inotifyFd_, buf, sizeof(buf)) > 0) {
    }
    reloadConfigFromFile();
}

} // namespace bamboomintkey

// =========================================================================
// Addon factory
// =========================================================================

namespace {

class BambooMintKeyFactory : public fcitx::AddonFactory {
public:
    fcitx::AddonInstance *create(fcitx::AddonManager *manager) override {
        return new bamboomintkey::BambooMintKeyEngine(manager->instance());
    }
};

} // namespace

FCITX_ADDON_FACTORY(BambooMintKeyFactory);
