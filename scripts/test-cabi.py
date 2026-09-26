#!/usr/bin/env python3
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Test runner C-ABI cho libBambooMintKeyCore.so (M1.7).
# Gọi trực tiếp các hàm export qua ctypes và kiểm thử theo ma trận TC-CABI-01 -> 08
# (xem docs/2.Design/Phase7/007_03_CoreNative_CABI_Design.md, Mục 5).
#
# Cách dùng:
#   python3 scripts/test-cabi.py [đường dẫn tới BambooMintKeyCore.so]
import ctypes
import sys

# Mã hành động
ACTION_PASS_THROUGH = 0
ACTION_CONSUME = 1
ACTION_UPDATE_PREEDIT = 2
ACTION_COMMIT_STRING = 3


def load_library(path: str) -> ctypes.CDLL:
    lib = ctypes.CDLL(path)

    # version
    lib.bmk_version.restype = ctypes.c_int
    lib.bmk_version.argtypes = []

    # lifecycle
    lib.bmk_context_create.restype = ctypes.c_void_p
    lib.bmk_context_create.argtypes = []
    lib.bmk_context_free.restype = None
    lib.bmk_context_free.argtypes = [ctypes.c_void_p]
    lib.bmk_context_reset.restype = None
    lib.bmk_context_reset.argtypes = [ctypes.c_void_p]

    # key processing
    lib.bmk_process_key.restype = ctypes.c_int
    lib.bmk_process_key.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
    lib.bmk_process_backspace.restype = ctypes.c_int
    lib.bmk_process_backspace.argtypes = [ctypes.c_void_p]
    lib.bmk_process_wordbreak.restype = ctypes.c_int
    lib.bmk_process_wordbreak.argtypes = [ctypes.c_void_p, ctypes.c_uint32]

    # buffer extraction
    lib.bmk_get_preedit_text.restype = ctypes.c_char_p
    lib.bmk_get_preedit_text.argtypes = [ctypes.c_void_p]
    lib.bmk_get_commit_text.restype = ctypes.c_char_p
    lib.bmk_get_commit_text.argtypes = [ctypes.c_void_p]
    lib.bmk_get_preedit_length.restype = ctypes.c_int
    lib.bmk_get_preedit_length.argtypes = [ctypes.c_void_p]

    # configuration
    lib.bmk_set_options.restype = None
    lib.bmk_set_options.argtypes = [
        ctypes.c_void_p,
        ctypes.c_int, ctypes.c_int, ctypes.c_int,
        ctypes.c_int, ctypes.c_int, ctypes.c_int,
        ctypes.c_int, ctypes.c_int,
    ]
    lib.bmk_load_config_json.restype = ctypes.c_int
    lib.bmk_load_config_json.argtypes = [ctypes.c_void_p, ctypes.c_char_p]

    # diagnostics
    lib.bmk_gc_collect.restype = None
    lib.bmk_gc_collect.argtypes = []
    lib.bmk_get_live_context_count.restype = ctypes.c_int
    lib.bmk_get_live_context_count.argtypes = []

    return lib


def preedit(lib, h) -> str:
    p = lib.bmk_get_preedit_text(h)
    return (p or b"").decode("utf-8")


def commit(lib, h) -> str:
    p = lib.bmk_get_commit_text(h)
    return (p or b"").decode("utf-8")


def type_str(lib, h, s: str):
    """Gõ từng ký tự, trả về danh sách mã hành động."""
    return [lib.bmk_process_key(h, ord(ch)) for ch in s]


def tc_cabi_01_lifecycle(lib):
    h = lib.bmk_context_create()
    if not h:
        return False, "bmk_context_create trả về handle = 0"
    lib.bmk_context_reset(h)
    lib.bmk_context_free(h)
    lib.bmk_context_free(0)  # free handle rỗng phải an toàn
    return True, "create/reset/free không gây crash (không SIGSEGV)"


def tc_cabi_02_basic_telex(lib):
    h = lib.bmk_context_create()
    actions = type_str(lib, h, "tieengs")
    pre = preedit(lib, h)
    lib.bmk_context_free(h)
    ok = pre == "tiếng" and all(a == ACTION_UPDATE_PREEDIT for a in actions)
    return ok, f"preedit={pre!r}, actions={actions}"


def tc_cabi_03_complex(lib):
    cases = [
        ("thuyeenf", "thuyền"),
        ("nghieeng", "nghiêng"),
        ("ddoongf", "đồng"),
    ]
    for s, expected in cases:
        h = lib.bmk_context_create()
        type_str(lib, h, s)
        pre = preedit(lib, h)
        lib.bmk_context_free(h)
        if pre != expected:
            return False, f"{s!r} -> {pre!r} (mong đợi {expected!r})"
    return True, "thuyền / nghiêng / đồng đều chuẩn NFC"


def tc_cabi_04_backspace(lib):
    h = lib.bmk_context_create()
    type_str(lib, h, "tieengs")
    lib.bmk_process_backspace(h)
    pre1 = preedit(lib, h)
    if pre1 != "tiêng":
        lib.bmk_context_free(h)
        return False, f"sau 1 Backspace preedit={pre1!r} (mong đợi 'tiêng')"

    # Backspace tiếp cho tới hết
    for _ in range(10):
        lib.bmk_process_backspace(h)
    pre_empty = preedit(lib, h)
    lib.bmk_context_free(h)
    return pre_empty == "", f"backspace tới rỗng: {pre_empty!r}"


def tc_cabi_05_wordbreak(lib):
    h = lib.bmk_context_create()
    type_str(lib, h, "vieetj")
    pre_before = preedit(lib, h)
    action = lib.bmk_process_wordbreak(h, ord(" "))
    com = commit(lib, h)
    pre_after = preedit(lib, h)
    lib.bmk_context_free(h)
    ok = (pre_before == "việt" and action == ACTION_COMMIT_STRING
          and com == "việt " and pre_after == "")
    return ok, f"preedit={pre_before!r}, action={action}, commit={com!r}, preedit_after={pre_after!r}"


def tc_cabi_06_english(lib):
    h = lib.bmk_context_create()
    type_str(lib, h, "internet")
    pre = preedit(lib, h)
    lib.bmk_context_free(h)
    return pre == "internet", f"preedit={pre!r}"


def tc_cabi_07_multicontext(lib):
    a = lib.bmk_context_create()
    b = lib.bmk_context_create()
    a_chars = "tieengs"
    b_chars = "vieetj"
    n = max(len(a_chars), len(b_chars))
    for i in range(n):
        if i < len(a_chars):
            lib.bmk_process_key(a, ord(a_chars[i]))
        if i < len(b_chars):
            lib.bmk_process_key(b, ord(b_chars[i]))
    pre_a = preedit(lib, a)
    pre_b = preedit(lib, b)
    lib.bmk_context_free(a)
    lib.bmk_context_free(b)
    ok = pre_a == "tiếng" and pre_b == "việt"
    return ok, f"A={pre_a!r}, B={pre_b!r}"


def current_rss_kb() -> int:
    with open("/proc/self/statm") as f:
        resident_pages = int(f.read().split()[1])
    return resident_pages * 4096 // 1024  # KB


def tc_cabi_08_stress(lib):
    n = 10000
    before = lib.bmk_get_live_context_count()
    rss_before = current_rss_kb()

    for _ in range(n):
        h = lib.bmk_context_create()
        type_str(lib, h, "tieengs")
        lib.bmk_process_wordbreak(h, ord(" "))
        lib.bmk_context_free(h)

    after = lib.bmk_get_live_context_count()
    rss_after = current_rss_kb()
    leaked = after - before

    # Rò rỉ context (unmanaged) là tiêu chí quyết định; RSS chỉ để tham khảo
    # vì NativeAOT GC + glibc malloc không trả bộ nhớ về OS ngay.
    ok = leaked == 0
    return ok, f"{n} chu kỳ, context leak={leaked} (mong đợi 0), RSS delta={rss_after - rss_before} KB"


def tc_cabi_09_config_bonus(lib):
    """Bonus: xác minh bmk_set_options và bmk_load_config_json (M1.6)."""
    h = lib.bmk_context_create()

    # Tắt chế độ V -> phím phải PassThrough
    lib.bmk_set_options(h, 0, 0, 1, 1, 0, 1, 1, 1)
    if lib.bmk_process_key(h, ord("a")) != ACTION_PASS_THROUGH:
        lib.bmk_context_free(h)
        return False, "chế độ tắt (isEnabled=0) phải trả PassThrough"

    # Reset để xóa ký tự 'a' đã gõ khi tắt (tránh dính vào trạng thái)
    lib.bmk_context_reset(h)

    # Nạp lại cấu hình từ JSON (bật V, kiểu dấu mới)
    json = (b'{"isVietnameseMode":true,"toneStyle":0,'
            b'"autoRestoreEnglishWords":true,"allowRepeatKeyUndo":true,'
            b'"allowLeadingWAsU":false,"allowFreeTonePlacement":true}')
    buf = ctypes.create_string_buffer(json)
    rc = lib.bmk_load_config_json(h, buf)
    if rc != 0:
        lib.bmk_context_free(h)
        return False, f"bmk_load_config_json trả rc={rc} (mong đợi 0)"

    type_str(lib, h, "tieengs")
    pre = preedit(lib, h)
    lib.bmk_context_free(h)
    return pre == "tiếng", f"preedit sau load_config_json={pre!r}"


def main() -> int:
    so_path = sys.argv[1] if len(sys.argv) > 1 else "publish/linux-x64/BambooMintKeyCore.so"

    try:
        lib = load_library(so_path)
    except OSError as e:
        print(f"FAIL: không nạp được {so_path}: {e}")
        print("Gợi ý: chạy `dotnet publish ...` trước, rồi truyền đường dẫn tới .so.")
        return 2

    print(f"ABI version: {lib.bmk_version()}")
    print(f"Library: {so_path}\n")

    tests = [
        ("TC-CABI-01", tc_cabi_01_lifecycle),
        ("TC-CABI-02", tc_cabi_02_basic_telex),
        ("TC-CABI-03", tc_cabi_03_complex),
        ("TC-CABI-04", tc_cabi_04_backspace),
        ("TC-CABI-05", tc_cabi_05_wordbreak),
        ("TC-CABI-06", tc_cabi_06_english),
        ("TC-CABI-07", tc_cabi_07_multicontext),
        ("TC-CABI-08", tc_cabi_08_stress),
        ("TC-CABI-09 (bonus: config)", tc_cabi_09_config_bonus),
    ]

    passed = 0
    for name, fn in tests:
        ok, detail = fn(lib)
        status = "PASS" if ok else "FAIL"
        print(f"[{status}] {name}: {detail}")
        if ok:
            passed += 1

    total = len(tests)
    print(f"\nKết quả: {passed}/{total} PASS")
    return 0 if passed == total else 1


if __name__ == "__main__":
    sys.exit(main())
