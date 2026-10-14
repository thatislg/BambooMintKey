#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Vietnamese IME Benchmark - Tier 2: Real-App Integration Runner (macOS)
Mô phỏng gõ phím vào ứng dụng thật trên macOS (TextEdit, VS Code...) qua CoreGraphics / AppleScript.
DÙNG CHUNG ĐƯỢC CHO MỌI BỘ GÕ TRÊN MACOS (BambooMintKey, Vietnamese Telex mặc định, EVKey, OpenKey...).

Cách dùng:
    python3 scripts/benchmark/tier2_macos_runner.py --app textedit --words 1000 --delay 0.04
    python3 scripts/benchmark/tier2_macos_runner.py --app vscode --words 1000 --delay 0.05
"""

import argparse
import os
import subprocess
import sys
import time

def parse_args():
    parser = argparse.ArgumentParser(description="Tier 2 Real-App Benchmark Runner for macOS")
    parser.add_argument("--app", choices=["textedit", "vscode"], default="textedit",
                        help="Ứng dụng mục tiêu: textedit, vscode (Mặc định: textedit)")
    parser.add_argument("--words", type=int, default=1000, help="Số lượng từ kiểm thử (Mặc định: 1000)")
    parser.add_argument("--delay", type=float, default=0.04, help="Độ trễ giữa các phím tính bằng giây (Mặc định: 0.04s ~ 25 phím/s)")
    return parser.parse_args()

def word_to_telex(word):
    res = word.lower()
    res = res.replace('ươ', 'uow').replace('ưa', 'uwa').replace('đ', 'dd')
    res = res.replace('â', 'aa').replace('ă', 'aw').replace('ê', 'ee')
    res = res.replace('ô', 'oo').replace('ơ', 'ow').replace('ư', 'uw')

    acute = {'á':'a','ắ':'aw','ấ':'aa','é':'e','ế':'ee','í':'i','ó':'o','ố':'oo','ớ':'ow','ú':'u','ứ':'uw','ý':'y'}
    grave = {'à':'a','ằ':'aw','ầ':'aa','è':'e','ề':'ee','ì':'i','ò':'o','ồ':'oo','ờ':'ow','ù':'u','ừ':'uw','ỳ':'y'}
    hook  = {'ả':'a','ẳ':'aw','ẩ':'aa','ẻ':'e','ể':'ee','ỉ':'i','ỏ':'o','ổ':'oo','ở':'ow','ủ':'u','ử':'uw','ỷ':'y'}
    tilde = {'ã':'a','ẵ':'aw','ẫ':'aa','ẽ':'e','ễ':'ee','ĩ':'i','õ':'o','ỗ':'oo','ỡ':'ow','ũ':'u','ữ':'uw','ỹ':'y'}
    dot   = {'ạ':'a','ặ':'aw','ậ':'aa','ẹ':'e','ệ':'ee','ị':'i','ọ':'o','ộ':'oo','ợ':'ow','ụ':'u','ự':'uw','ỵ':'y'}

    for k, v in acute.items():
        if k in res: res = res.replace(k, v) + 's'; break
    for k, v in grave.items():
        if k in res: res = res.replace(k, v) + 'f'; break
    for k, v in hook.items():
        if k in res: res = res.replace(k, v) + 'r'; break
    for k, v in tilde.items():
        if k in res: res = res.replace(k, v) + 'x'; break
    for k, v in dot.items():
        if k in res: res = res.replace(k, v) + 'j'; break

    return res

def send_keys_applescript(app_name, key_stream, delay):
    # Dùng AppleScript gửi chuỗi phím vào ứng dụng đang focus
    script = f'''
    tell application "{app_name}" to activate
    delay 0.5
    tell application "System Events"
        repeat with k in {{{", ".join([f'"{c}"' for c in key_stream])}}}
            keystroke k
            delay {delay}
        end repeat
    end tell
    '''
    subprocess.run(["osascript", "-e", script], check=False)

def main():
    args = parse_args()
    print("=" * 80)
    print("       VIETNAMESE IME BENCHMARK - TẦNG 2: REAL APP INTEGRATION (MACOS)       ")
    print("=" * 80)
    print(f"Ứng dụng mục tiêu : {args.app}")
    print(f"Số lượng từ       : {args.words}")
    print(f"Tốc độ bắn phím   : {args.delay * 1000:.0f} ms/phím (~{1.0 / args.delay:.0f} phím/giây)")
    print()

    # Nạp corpus
    script_dir = os.path.dirname(os.path.abspath(__file__))
    corpus_path = os.path.normpath(os.path.join(script_dir, "../../dicts/vi-corpus-freq.tsv"))

    if not os.path.exists(corpus_path):
        print(f"[ERROR] Không tìm thấy file corpus tại: {corpus_path}")
        sys.exit(1)

    words = []
    with open(corpus_path, "r", encoding="utf-8") as f:
        for line in f:
            if len(words) >= args.words:
                break
            parts = line.strip().split("\t")
            if parts and parts[0]:
                w = parts[0].strip().lower()
                if w and "-" not in w and " " not in w and "." not in w:
                    words.append(w)

    print(f"[1/4] Đã nạp thành công {len(words)} từ vựng tiếng Việt phổ biến.")

    # Mở app mục tiêu
    mac_app_name = "TextEdit" if args.app == "textedit" else "Visual Studio Code"
    print(f"[2/4] Đang kích hoạt ứng dụng '{mac_app_name}'...")
    subprocess.run(["open", "-a", mac_app_name], check=False)
    time.sleep(2.0)

    print(f"[3/4] Đang bắn phím mô phỏng vào '{mac_app_name}'...")
    start_time = time.perf_counter()

    batch_size = 50
    for i in range(0, len(words), batch_size):
        batch = words[i:i + batch_size]
        keystrokes = []
        for w in batch:
            keys = word_to_telex(w)
            for c in keys:
                keystrokes.append(c)
            keystrokes.append(" ")
        send_keys_applescript(mac_app_name, keystrokes, args.delay)
        print(f"      -> Đã gõ: {min(i + batch_size, len(words))} / {len(words)} từ...")

    elapsed = time.perf_counter() - start_time
    print(f"      Hoàn tất gõ trong {elapsed:.2f} giây.")

    # Thu thập kết quả qua Clipboard
    print(f"[4/4] Đang đọc kết quả từ ứng dụng qua Clipboard...")
    time.sleep(0.5)

    # Cmd + A, Cmd + C
    copy_script = '''
    tell application "System Events"
        keystroke "a" using command down
        delay 0.1
        keystroke "c" using command down
        delay 0.2
    end tell
    '''
    subprocess.run(["osascript", "-e", copy_script], check=False)
    actual_text = subprocess.check_output(["pbpaste"]).decode("utf-8")
    actual_words = actual_text.split()

    passed = 0
    failed = 0
    sample_errors = []

    for i in range(len(words)):
        expected = words[i]
        actual = actual_words[i] if i < len(actual_words) else ""
        if actual == expected:
            passed += 1
        else:
            failed += 1
            if len(sample_errors) < 10:
                sample_errors.append(f"Từ #{i+1}: mong đợi='{expected}', thực tế='{actual}'")

    accuracy = (passed / len(words)) * 100.0
    wpm = (len(words) / (elapsed / 60.0))

    print()
    print("=" * 80)
    print(f"                KẾT QUẢ BENCHMARK TẦNG 2 TRÊN {mac_app_name.upper()} (MACOS)    ")
    print("=" * 80)
    print(f"Tổng số từ kiểm thử : {len(words)}")
    print(f"Số từ đạt chính xác : {passed}")
    print(f"Số từ bị lỗi / hỏng : {failed}")
    print(f"Độ đúng (Accuracy)  : {accuracy:.2f} %")
    print(f"Thời gian thực thi  : {elapsed:.2f} s")
    print(f"Tốc độ gõ giả lập   : {wpm:.1f} WPM")
    print("=" * 80)

    if sample_errors:
        print("\nMẫu các từ không khớp:")
        for err in sample_errors:
            print(f"  - {err}")

if __name__ == "__main__":
    main()
