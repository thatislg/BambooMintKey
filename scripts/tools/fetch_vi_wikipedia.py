#!/usr/bin/env python3
"""
BambooMintKey - Vietnamese Telex Input Method Editor
Copyright (c) 2026 Dương Gia Long and LMO contributors
SPDX-License-Identifier: MIT

fetch_vi_wikipedia.py — Trích xuất ngữ liệu âm tiết tiếng Việt thực tế từ
Wikipedia tiếng Việt (corpus mở, CC BY-SA 3.0 / CC0) để lọc ma trận ngữ âm
xuống tập hợp âm tiết có thực trong đời sống (Phase 8 / Milestone 1.2).

Script này CẦN kết nối internet (tải dump) hoặc file dump đã tải sẵn.
Đây là bước "corpus extraction" thực thụ theo thiết kế 008_02 (Vấn đề 2),
KHÔNG sao chép từ bất kỳ bộ từ điển đóng gói nào.

Quy trình:
  1. Tải dump text Wikipedia tiếng Việt (pages-articles).
  2. Stream-parse XML, trích toàn bộ nội dung bài viết.
  3. Tokenize & đếm tần suất từng âm tiết (đối chiếu ma trận ngữ âm hợp lệ).
  4. Lọc âm tiết có tần suất >= ngưỡng, xuất corpus reference.

Sử dụng (có internet):
  # Cách A: tải dump rồi parse (khuyến nghị — reproducible)
  wget -c https://dumps.wikimedia.org/viwiki/latest/viwiki-latest-pages-articles.xml.bz2
  python3 scripts/fetch_vi_wikipedia.py --input viwiki-latest-pages-articles.xml.bz2 \
      --output dicts/vi-corpus-syllables.dict --freq-output dicts/vi-corpus-freq.tsv

  # Cách B: tự tải + parse trong một lệnh
  python3 scripts/fetch_vi_wikipedia.py --download \
      --output dicts/vi-corpus-syllables.dict

Sau đó sinh từ điển MIT với corpus vừa tạo:
  python3 scripts/generate_mit_dict.py --output-dir dicts \
      --english-src dicts/google-20000-english.dict \
      --corpus dicts/vi-corpus-syllables.dict
"""

import argparse
import bz2
import os
import re
import sys
import unicodedata
import urllib.request
import xml.sax
from collections import Counter

# URL dump mới nhất (Wikipedia tiếng Việt, text các bài viết)
DUMP_URL = "https://dumps.wikimedia.org/viwiki/latest/viwiki-latest-pages-articles.xml.bz2"

# Regex tách "từ" (token) khỏi văn bản: các chuỗi chữ cái Unicode (gồm dấu),
# loại trừ chữ số và dấu gạch dưới.
TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)


def _local_name(tag: str) -> str:
    """Bỏ namespace prefix khỏi tên thẻ XML (vd '{ns}text' -> 'text')."""
    return tag.rsplit("}", 1)[-1]


class _WikiTextHandler(xml.sax.handler.ContentHandler):
    """SAX handler: stream-parse dump Wikipedia, chỉ giữ nội dung <text> hiện tại.

    Không build cây DOM -> RAM ổn định bất kể dump lớn đến đâu (vài GB).
    """

    def __init__(self, valid_syllables: set, freq: Counter):
        super().__init__()
        self.valid = valid_syllables
        self.freq = freq
        self.total_tokens = 0
        self._in_text = False
        self._buf = []

    def startElement(self, name, attrs):
        if _local_name(name) == "text":
            self._in_text = True
            self._buf = []

    def endElement(self, name):
        if _local_name(name) == "text":
            self._in_text = False
            self._process("".join(self._buf))
            self._buf = []

    def characters(self, content):
        if self._in_text:
            self._buf.append(content)

    def _process(self, text: str):
        for token in TOKEN_RE.findall(text):
            self.total_tokens += 1
            norm = unicodedata.normalize("NFC", token).lower()
            if norm in self.valid:
                self.freq[norm] += 1


def extract_syllable_frequencies(path: str, valid_syllables: set) -> Counter:
    """Đếm tần suất các âm tiết hợp lệ xuất hiện trong dump (stream SAX)."""
    freq = Counter()
    handler = _WikiTextHandler(valid_syllables, freq)
    opener = bz2.open if path.endswith(".bz2") else open
    with opener(path, "rt", encoding="utf-8", errors="ignore") as fh:
        xml.sax.parse(fh, handler)
    print(f"[INFO] Total tokens scanned: {handler.total_tokens}", file=sys.stderr)
    return freq


def download_dump(url: str, dest: str) -> str:
    """Tải file với progress bar + resume (dùng Range nếu file đã tồn tại một phần)."""
    import time

    existing = os.path.getsize(dest) if os.path.exists(dest) else 0
    headers = {"User-Agent": "BambooMintKey/1.0 (corpus fetch)"}
    if existing > 0:
        headers["Range"] = f"bytes={existing}-"
        print(f"[INFO] Resume download từ byte {existing}", file=sys.stderr)

    req = urllib.request.Request(url, headers=headers)
    mode = "ab" if existing > 0 else "wb"
    downloaded = existing

    with urllib.request.urlopen(req) as resp:
        total = downloaded + int(resp.headers.get("Content-Length") or 0)
        last_report = time.time()
        with open(dest, mode) as out:
            while True:
                chunk = resp.read(1024 * 1024)
                if not chunk:
                    break
                out.write(chunk)
                downloaded += len(chunk)
                now = time.time()
                if now - last_report >= 3:
                    pct = (downloaded / total * 100) if total else 0
                    print(f"[INFO] {downloaded/1024/1024:.1f} MB / {total/1024/1024:.1f} MB ({pct:.1f}%)",
                          file=sys.stderr, flush=True)
                    last_report = now

    print(f"[OK] Tải xong {downloaded/1024/1024:.1f} MB -> {dest}", file=sys.stderr)
    return dest


def main() -> int:
    p = argparse.ArgumentParser(description="Trích xuất corpus âm tiết tiếng Việt từ Wikipedia")
    p.add_argument("--input", help="Đường dẫn file dump XML (.bz2 hoặc .xml) đã tải")
    p.add_argument("--download", action="store_true", help="Tự tải dump mới nhất")
    p.add_argument("--output", default="dicts/vi-corpus-syllables.dict",
                   help="File output danh sách âm tiết (mặc định dicts/vi-corpus-syllables.dict)")
    p.add_argument("--freq-output", default="",
                   help="(Tùy chọn) File output tần suất TSV: syllable<TAB>count")
    p.add_argument("--min-freq", type=int, default=1,
                   help="Ngưỡng tần suất tối thiểu để giữ âm tiết (mặc định 1)")
    args = p.parse_args()

    # Nạp ma trận âm tiết hợp lệ (tầng 1) để chỉ đếm âm tiết tiếng Việt thực
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from generate_mit_dict import generate_vietnamese_syllables
    valid = generate_vietnamese_syllables()
    print(f"[INFO] Valid syllable matrix: {len(valid)} (theoretical)", file=sys.stderr)

    input_path = args.input
    if args.download:
        print(f"[INFO] Downloading {DUMP_URL} (~1.07 GB, có resume)", file=sys.stderr)
        input_path = download_dump(DUMP_URL, "viwiki-latest-pages-articles.xml.bz2")
    if not input_path or not os.path.exists(input_path):
        print("[ERROR] Cần --input hoặc --download (xem --help)", file=sys.stderr)
        return 2

    freq = extract_syllable_frequencies(input_path, valid)
    selected = {s for s, c in freq.items() if c >= args.min_freq}

    os.makedirs(os.path.dirname(args.output) or ".", exist_ok=True)
    with open(args.output, "w", encoding="utf-8", newline="\n") as f:
        for s in sorted(selected, key=lambda w: (unicodedata.normalize("NFC", w).lower(), w)):
            f.write(s + "\n")
    print(f"[OK] {len(selected)} syllables (freq >= {args.min_freq}) -> {args.output}")

    if args.freq_output:
        with open(args.freq_output, "w", encoding="utf-8", newline="\n") as f:
            for s, c in freq.most_common():
                f.write(f"{s}\t{c}\n")
        print(f"[OK] Frequency table -> {args.freq_output}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
