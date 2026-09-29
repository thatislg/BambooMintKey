#!/usr/bin/env python3
# BambooMintKey - Vietnamese Telex Input Method Editor
# Copyright (c) 2026 Dương Gia Long and LMO contributors
# SPDX-License-Identifier: MIT
#
# Benchmark C-ABI cho BambooMintKeyCore.so.
# Đo hai loại chỉ số để đưa vào mục "Evaluation & Measurements":
#
#   1. Speed / throughput  — thời gian engine xử lý keystroke.
#      Đơn vị: ns/keystroke, µs/keystroke, keystrokes/giây.
#   2. Accuracy (lỗi chữ)  — tỉ lệ mục gõ đúng so với chuỗi mong đợi.
#
# Định dạng input (file text, UTF-8), mỗi dòng một mục gõ:
#   - Không có TAB: cả dòng được coi là chuỗi Telex -> đo speed.
#   - Có TAB: "telex<TAB>expected" -> đo accuracy (so khớp chuỗi commit).
#
# Nếu không truyền --input, mặc định dùng dicts/vietnamese-syllables-mit.dict
# (chỉ đo speed, vì các âm tiết trong dict không mang dấu thanh nên không có
# "chữ đúng" để đối chiếu accuracy).
#
# Cách dùng:
#   python3 scripts/tests/bench-cabi.py [đường dẫn .so]
#       [--input FILE] [--rounds N] [--limit N] [--mode speed|accuracy|all]
#
# Lưu ý đo lường:
#   - Gọi qua ctypes có overhead gọi hàm (cỡ chục ns/lần), nên con số latency
#     là CẬN TRÊN của tốc độ engine thuần. Muốn đo chính xác tuyệt đối thì
#     benchmark ngay trong addon C++.
#   - Có warm-up trước khi đo và lấy min (không lấy trung bình) để giảm nhiễu
#     từ scheduler/OS.

import argparse
import ctypes
import sys
import time

# Mã hành động (khớp với ActionCode trong cabibridge.h)
ACTION_PASS_THROUGH = 0
ACTION_CONSUME = 1
ACTION_UPDATE_PREEDIT = 2
ACTION_COMMIT_STRING = 3

DEFAULT_DICT = "dicts/vietnamese-syllables-mit.dict"


def load_library(path: str) -> ctypes.CDLL:
    """Nạp BambooMintKeyCore.so và khai báo đầy đủ chữ ký C-ABI qua ctypes."""
    lib = ctypes.CDLL(path)

    lib.bmk_version.restype = ctypes.c_int
    lib.bmk_version.argtypes = []

    lib.bmk_context_create.restype = ctypes.c_void_p
    lib.bmk_context_create.argtypes = []
    lib.bmk_context_free.restype = None
    lib.bmk_context_free.argtypes = [ctypes.c_void_p]
    lib.bmk_context_reset.restype = None
    lib.bmk_context_reset.argtypes = [ctypes.c_void_p]

    lib.bmk_process_key.restype = ctypes.c_int
    lib.bmk_process_key.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
    lib.bmk_process_backspace.restype = ctypes.c_int
    lib.bmk_process_backspace.argtypes = [ctypes.c_void_p]
    lib.bmk_process_wordbreak.restype = ctypes.c_int
    lib.bmk_process_wordbreak.argtypes = [ctypes.c_void_p, ctypes.c_uint32]

    lib.bmk_get_preedit_text.restype = ctypes.c_char_p
    lib.bmk_get_preedit_text.argtypes = [ctypes.c_void_p]
    lib.bmk_get_commit_text.restype = ctypes.c_char_p
    lib.bmk_get_commit_text.argtypes = [ctypes.c_void_p]
    lib.bmk_get_preedit_length.restype = ctypes.c_int
    lib.bmk_get_preedit_length.argtypes = [ctypes.c_void_p]

    lib.bmk_set_options.restype = None
    lib.bmk_set_options.argtypes = [
        ctypes.c_void_p,
        ctypes.c_int, ctypes.c_int, ctypes.c_int,
        ctypes.c_int, ctypes.c_int, ctypes.c_int,
        ctypes.c_int, ctypes.c_int,
    ]
    lib.bmk_load_config_json.restype = ctypes.c_int
    lib.bmk_load_config_json.argtypes = [ctypes.c_void_p, ctypes.c_char_p]

    lib.bmk_gc_collect.restype = None
    lib.bmk_gc_collect.argtypes = []
    lib.bmk_get_live_context_count.restype = ctypes.c_int
    lib.bmk_get_live_context_count.argtypes = []

    return lib


def commit_text(lib, h) -> str:
    p = lib.bmk_get_commit_text(h)
    return (p or b"").decode("utf-8")


def type_token(lib, h, s: str):
    """Gõ từng ký tự của chuỗi s vào context h, trả về danh sách mã hành động."""
    return [lib.bmk_process_key(h, ord(ch)) for ch in s]


# ---------------------------------------------------------------------------
# Input
# ---------------------------------------------------------------------------

def load_input(path: str):
    """Đọc file input, tách thành các dòng mục gõ.

    Trả về (tokens, pairs):
      - tokens: danh sách chuỗi Telex (cho speed).
      - pairs:  danh sách (telex, expected) (cho accuracy).
    """
    tokens = []
    pairs = []
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")
            if not line.strip():
                continue
            if "\t" in line:
                telex, expected = line.split("\t", 1)
                pairs.append((telex, expected))
            else:
                tokens.append(line)
    return tokens, pairs


def load_dict_as_tokens(path: str, limit: int | None):
    tokens = []
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.strip()
            if line:
                tokens.append(line)
    if limit:
        tokens = tokens[:limit]
    return tokens


# ---------------------------------------------------------------------------
# Speed benchmark
# ---------------------------------------------------------------------------

def bench_speed(lib, tokens, rounds):
    """Đo latency/throughput khi gõ liên tiếp toàn bộ token (mỗi token cách nhau
    bằng wordbreak = dấu cách). Trả về dict các chỉ số."""
    # Nối toàn bộ token thành một luồng keystroke duy nhất.
    keys = []
    for t in tokens:
        for ch in t:
            o = ord(ch)
            if 0x20 <= o <= 0x7E:  # chỉ lấy ký tự ASCII in được (đúng miền engine)
                keys.append(o)
        keys.append(ord(" "))  # word break để commit từ

    if not keys:
        return None

    # Warm-up: chạy 1 vòng không tính, để OS page cache + branch prediction ổn định.
    warm = lib.bmk_context_create()
    for k in keys:
        lib.bmk_process_key(warm, k)
    lib.bmk_context_free(warm)

    lib.bmk_gc_collect()  # loại nhiễu GC trước khi đo

    best_ns = float("inf")
    for _ in range(rounds):
        h = lib.bmk_context_create()
        t0 = time.perf_counter_ns()
        for k in keys:
            lib.bmk_process_key(h, k)
        t1 = time.perf_counter_ns()
        lib.bmk_context_free(h)
        best_ns = min(best_ns, t1 - t0)

    n = len(keys)
    return {
        "tokens": len(tokens),
        "keystrokes": n,
        "total_ns": best_ns,
        "ns_per_key": best_ns / n,
        "us_per_key": best_ns / n / 1e3,
        "keys_per_sec": n / (best_ns / 1e9),
    }


# ---------------------------------------------------------------------------
# Accuracy benchmark
# ---------------------------------------------------------------------------

def bench_accuracy(lib, pairs):
    """Gõ từng cặp (telex, expected), đối chiếu chuỗi commit. Trả về (đúng, tổng)."""
    correct = 0
    total = 0
    failures = []
    for telex, expected in pairs:
        h = lib.bmk_context_create()
        for ch in telex:
            lib.bmk_process_key(h, ord(ch))
        lib.bmk_process_wordbreak(h, ord(" "))
        got = commit_text(lib, h).strip()
        lib.bmk_context_free(h)
        total += 1
        if got == expected.strip():
            correct += 1
        else:
            failures.append((telex, expected.strip(), got))
    return correct, total, failures


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(description="Benchmark BambooMintKey C-ABI (speed + accuracy).")
    parser.add_argument("so_path", nargs="?",
                        default="publish/linux-x64/BambooMintKeyCore.so")
    parser.add_argument("--input", default=None,
                        help="File text làm input (mỗi dòng một mục gõ). Mặc định dùng dict.")
    parser.add_argument("--rounds", type=int, default=10,
                        help="Số vòng lặp đo speed (lấy min). Mặc định 10.")
    parser.add_argument("--limit", type=int, default=None,
                        help="Giới hạn số token từ dict (chỉ áp dụng khi không có --input).")
    parser.add_argument("--mode", choices=["speed", "accuracy", "all"], default="all")
    args = parser.parse_args()

    try:
        lib = load_library(args.so_path)
    except OSError as e:
        print(f"FAIL: không nạp được {args.so_path}: {e}")
        print("Gợi ý: chạy `dotnet publish ...` trước, rồi truyền đường dẫn tới .so.")
        return 2

    print(f"ABI version: {lib.bmk_version()}")
    print(f"Library: {args.so_path}\n")

    # Xác định nguồn input
    if args.input:
        tokens, pairs = load_input(args.input)
        print(f"Input: {args.input} ({len(tokens)} token tự do, {len(pairs)} cặp telex/expected)")
    else:
        tokens = load_dict_as_tokens(DEFAULT_DICT, args.limit)
        pairs = []
        print(f"Input: {DEFAULT_DICT} (mặc định, {len(tokens)} âm tiết — chỉ đo speed)")

    # Speed
    if args.mode in ("speed", "all") and tokens:
        r = bench_speed(lib, tokens, args.rounds)
        if r:
            print("\n=== SPEED / THROUGHPUT ===")
            print(f"  tokens            : {r['tokens']}")
            print(f"  keystrokes        : {r['keystrokes']}")
            print(f"  tổng thời gian    : {r['total_ns']/1e6:.3f} ms")
            print(f"  latency           : {r['ns_per_key']:.0f} ns/keystroke  ({r['us_per_key']:.2f} µs/keystroke)")
            print(f"  throughput        : {r['keys_per_sec']:,.0f} keystrokes/giây")

    # Accuracy
    if args.mode in ("accuracy", "all") and pairs:
        correct, total, failures = bench_accuracy(lib, pairs)
        pct = 100.0 * correct / total if total else 0.0
        print("\n=== ACCURACY (lỗi chữ) ===")
        print(f"  đúng/tổng         : {correct}/{total}")
        print(f"  tỉ lệ             : {pct:.2f}%")
        if failures:
            print(f"  {len(failures)} mục sai (in tối đa 10):")
            for telex, exp, got in failures[:10]:
                print(f"    {telex!r} -> got {got!r}, expected {exp!r}")

    if not tokens and not pairs:
        print("\nKhông có input. Dùng --input FILE hoặc để mặc định dùng dict.")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
