#!/usr/bin/env python3
"""
BambooMintKey - Vietnamese Telex Input Method Editor
Copyright (c) 2026 Dương Gia Long and LMO contributors
SPDX-License-Identifier: MIT

generate_mit_dict.py — Sinh & chuẩn hóa nguồn dữ liệu từ điển MIT/CC0 (Phase 8 / Milestone 1).

Mục tiêu:
  - M1.1: Sinh ma trận ngữ âm học tiếng Việt (27 phụ âm đầu x ~50 cụm nguyên âm
          x 8 phụ âm cuối x 6 thanh điệu) với đầy đủ ràng buộc chính tả Quốc ngữ.
  - M1.2: Xuất danh mục âm tiết chuẩn -> dicts/vietnamese-syllables-mit.dict
  - M1.3: Chuẩn hóa danh mục 20.000 từ tiếng Anh -> dicts/english-20k.dict

Dữ liệu tạo ra thuộc phạm vi công cộng (ngôn ngữ tự nhiên), phát hành theo
MIT License / CC0. Không phụ thuộc bất kỳ nguồn dữ liệu GPL nào.

Tham chiếu thiết kế:
  - docs/2.Design/Phase8/008_02_MIT_Dictionary_And_Corpus_Design.md
  - docs/2.Design/Phase8/008_03_Phonotactic_Rules_Fix_Design.md

Sử dụng:
  python3 scripts/generate_mit_dict.py [--output-dir dicts]
"""

import argparse
import os
import sys
import unicodedata

# =============================================================================
# 1. Danh mục thành phần ngữ âm học tiếng Việt
# =============================================================================

# 27 phụ âm đầu + trường hợp rỗng (âm tiết không có phụ âm đầu)
INITIALS = [
    "", "b", "c", "ch", "d", "đ", "g", "gh", "gi", "h", "k", "kh", "l",
    "m", "n", "ng", "ngh", "nh", "p", "ph", "qu", "r", "s", "t", "th",
    "tr", "v", "x",
]

# Cụm nguyên âm (đã ở dạng Unicode chuẩn, gồm dấu phụ sẵn).
# Bao gồm: nguyên âm đơn, nhị trùng âm, tam trùng âm.
VOWEL_CLUSTERS = [
    # Nguyên âm đơn
    "a", "ă", "â", "e", "ê", "i", "o", "ô", "ơ", "u", "ư", "y",
    # Nhị trùng âm (kết thúc bằng bán nguyên âm i/u/o/y)
    "ai", "ao", "au", "ay", "âu", "ây", "eo", "êu",
    # Nhị trùng âm khác
    "ia", "iê", "iu", "oa", "oă", "oe", "oi", "ôi", "ơi",
    "ua", "uâ", "uô", "uê", "ui", "uy", "uơ", "ưa", "ươ", "ưi", "ưu", "yê",
    # Tam trùng âm
    "oai", "oay", "oao", "oeo",
    "uai", "uay", "uoi", "uôi", "ươi", "ươu", "uya", "uyê", "uyu", "uây",
    "iêu", "yêu",
]

# 8 phụ âm cuối + trường hợp rỗng (âm tiết mở)
FINALS = ["", "c", "ch", "m", "n", "ng", "nh", "p", "t"]

# 6 thanh điệu: (tên, key ánh xạ)
TONES = ["ngang", "acute", "grave", "hook", "tilde", "dot"]

# Phụ âm tắc vô thanh cuối chỉ nhận thanh Sắc (acute) hoặc Nặng (dot)
STOP_FINALS = {"c", "ch", "p", "t"}

# Bảng ánh xạ (ký tự nền, thanh điệu) -> ký tự đã mang dấu thanh
TONE_TABLE = {
    "a": {"acute": "á", "grave": "à", "hook": "ả", "tilde": "ã", "dot": "ạ"},
    "ă": {"acute": "ắ", "grave": "ằ", "hook": "ẳ", "tilde": "ẵ", "dot": "ặ"},
    "â": {"acute": "ấ", "grave": "ầ", "hook": "ẩ", "tilde": "ẫ", "dot": "ậ"},
    "e": {"acute": "é", "grave": "è", "hook": "ẻ", "tilde": "ẽ", "dot": "ẹ"},
    "ê": {"acute": "ế", "grave": "ề", "hook": "ể", "tilde": "ễ", "dot": "ệ"},
    "i": {"acute": "í", "grave": "ì", "hook": "ỉ", "tilde": "ĩ", "dot": "ị"},
    "o": {"acute": "ó", "grave": "ò", "hook": "ỏ", "tilde": "õ", "dot": "ọ"},
    "ô": {"acute": "ố", "grave": "ồ", "hook": "ổ", "tilde": "ỗ", "dot": "ộ"},
    "ơ": {"acute": "ớ", "grave": "ờ", "hook": "ở", "tilde": "ỡ", "dot": "ợ"},
    "u": {"acute": "ú", "grave": "ù", "hook": "ủ", "tilde": "ũ", "dot": "ụ"},
    "ư": {"acute": "ứ", "grave": "ừ", "hook": "ử", "tilde": "ữ", "dot": "ự"},
    "y": {"acute": "ý", "grave": "ỳ", "hook": "ỷ", "tilde": "ỹ", "dot": "ỵ"},
}

# Nguyên âm mang dấu phụ (được coi là âm chính khi đặt dấu thanh)
DIACRITIC_VOWELS = "êơưâă"


# =============================================================================
# 2. Thuật toán xác định âm chính (target vowel index)
#    Tham chiếu: ToneRules.getTargetVowelIndex + 008_03 Vấn đề 3
# =============================================================================

def get_target_vowel_index(vowels: str, has_final: bool) -> int:
    n = len(vowels)
    if n <= 1:
        return 0

    if n == 2:
        if has_final:
            # Âm tiết khép: dấu luôn ở nguyên âm thứ 2
            return 1
        # Âm tiết mở
        if vowels in ("oa", "oe", "uy"):
            # Modern style: dấu ở nguyên âm đầu (hóa, xòe, thúy)
            return 0
        if vowels[1] in DIACRITIC_VOWELS:
            return 1
        if vowels[0] in DIACRITIC_VOWELS:
            return 0
        return 0

    # n >= 3 (tam trùng âm)
    if "ươ" in vowels or "ưo" in vowels:
        idx = vowels.find("ơ")
        return idx if idx >= 0 else 1
    for i, c in enumerate(vowels):
        if c in DIACRITIC_VOWELS:
            return i
    return (len(vowels) - 1) if has_final else 1


def apply_tone(vowels: str, tone: str, has_final: bool) -> str:
    """Áp dấu thanh lên âm chính của cụm nguyên âm."""
    if tone == "ngang":
        return vowels
    idx = get_target_vowel_index(vowels, has_final)
    base = vowels[idx]
    toned = TONE_TABLE.get(base, {}).get(tone)
    if toned is None:
        return vowels
    chars = list(vowels)
    chars[idx] = toned
    return "".join(chars)


# =============================================================================
# 3. Ràng buộc chính tả Quốc ngữ
# =============================================================================

def is_valid_syllable(initial: str, cluster: str, final: str, tone: str) -> bool:
    first_vowel = cluster[0] if cluster else ""

    # Ràng buộc âm tắc cuối: c/ch/p/t chỉ nhận Sắc/Nặng
    if final in STOP_FINALS and tone not in ("acute", "dot"):
        return False

    # Ràng buộc nguyên âm ngắn (ă/â) phải có phụ âm cuối
    if cluster[-1] in ("ă", "â") and final == "":
        return False

    # Ràng buộc tương thích phụ âm đầu - nguyên âm:
    #   k  -> e, ê, i, y (và iê, ia, yê)
    #   gh, ngh -> e, ê, i (và iê, ia)
    #   c, g, ng -> KHÔNG đi với e, ê, i
    if initial == "k":
        if first_vowel not in ("e", "ê", "i", "y"):
            return False
    if initial in ("gh", "ngh"):
        if first_vowel not in ("e", "ê", "i"):
            return False
    if initial in ("c", "g", "ng"):
        if first_vowel in ("e", "ê", "i"):
            return False

    # Ràng buộc 'c' cấm đi với e, ê, i, oe (ngăn core/more thành coe).
    # Lưu ý: KHÔNG cấm 'ua' vì 'cua', 'của' là âm tiết hợp lệ.
    if initial == "c" and cluster in ("oe",):
        return False

    # Ràng buộc cụm 'oe' chỉ đi với ch/h/kh/l/ng/nh/th/t/x hoặc rỗng
    if cluster == "oe" and initial not in ("", "ch", "h", "kh", "l", "ng", "nh", "th", "t", "x"):
        return False

    return True


# Các cụm nguyên âm bắt buộc âm tiết khép (không tồn tại ở âm tiết mở)
# trong tiếng Việt thực tế. Vd: "biê" không tồn tại, chỉ có "biên/biếc/biệt".
CLOSED_ONLY_CLUSTERS = {"iê", "uô", "uâ", "oă", "ươ", "yê", "uyê"}

# Cụm nguyên âm tam trùng âm đặc thù chỉ đi với một tập phụ âm cuối rất hẹp
RARE_TRIPHTHONG_FINALS = {
    "oai": {"", "n", "ng"},
    "oay": {""},
    "oao": {""},
    "oeo": {""},
    "uai": {"", "n", "ng"},
    "uay": {""},
    "uoi": {"", "n", "ng", "c", "t"},
    "uôi": {"", "n", "ng", "c", "t"},
    "ươi": {"", "n", "ng", "c", "t"},
    "ươu": {""},
    "iêu": {"", "n", "ng", "c", "t"},
    "yêu": {"", "n", "ng", "c", "t"},
}


def is_closed_only_cluster(cluster: str) -> bool:
    return cluster in CLOSED_ONLY_CLUSTERS


def generate_vietnamese_syllables() -> set:
    """Sinh toàn bộ âm tiết tiếng Việt hợp lệ từ ma trận ngữ âm.

    Tầng 1 (ma trận ngữ âm) sinh ra các âm tiết LÝ THUYẾT hợp lệ.
    Bộ lọc corpus (Vấn đề 2 của 008_02) sẽ thu hẹp về âm tiết THỰC TẾ.
    """
    syllables = set()
    for initial in INITIALS:
        for cluster in VOWEL_CLUSTERS:
            # Ràng buộc cụm nguyên âm bắt buộc khép
            if is_closed_only_cluster(cluster):
                allowed_finals = [f for f in FINALS if f != ""]
            elif cluster in RARE_TRIPHTHONG_FINALS:
                allowed_finals = RARE_TRIPHTHONG_FINALS[cluster]
            else:
                allowed_finals = FINALS

            for final in allowed_finals:
                for tone in TONES:
                    if not is_valid_syllable(initial, cluster, final, tone):
                        continue
                    has_final = final != ""
                    nucleus = apply_tone(cluster, tone, has_final)
                    word = initial + nucleus + final
                    syllables.add(unicodedata.normalize("NFC", word))
    return syllables


# =============================================================================
# 4. Chuẩn hóa danh mục từ tiếng Anh (20.000 từ)
# =============================================================================

def is_valid_english_word(word: str) -> bool:
    """Giữ lại các từ tiếng Anh thuần La-tinh, loại bỏ từ rác."""
    if not word:
        return False
    # Chỉ giữ ký tự a-z (sau khi lowercase)
    if not all(("a" <= c <= "z") for c in word):
        return False
    # Loại bỏ từ 1 ký tự, trừ a và i
    if len(word) == 1 and word not in ("a", "i"):
        return False
    return True


def load_corpus_reference(path: str) -> set:
    """Nạp danh mục âm tiết thực tế (dữ kiện ngôn ngữ tự nhiên) dùng làm
    bộ lọc corpus. Đây KHÔNG phải bản sao tác phẩm sáng tạo — chỉ là danh sách
    các đơn vị từ vựng tồn tại khách quan trong tiếng Việt (xem 008_02 mục 3).
    """
    words = set()
    with open(path, "r", encoding="utf-8") as f:
        for raw_line in f:
            line = raw_line.strip()
            if line:
                words.add(unicodedata.normalize("NFC", line).lower())
    return words


def normalize_english(src_path: str) -> set:
    words = set()
    with open(src_path, "r", encoding="utf-8") as f:
        for raw_line in f:
            line = raw_line.strip()
            if not line:
                continue
            # Chỉ lấy token đầu (một số wordlist có kèm tần suất)
            token = line.split()[0] if line.split() else ""
            word = unicodedata.normalize("NFC", token).lower()
            if is_valid_english_word(word):
                words.add(word)
    return words


# =============================================================================
# 5. Ghi file
# =============================================================================

def write_dict(path: str, words: set) -> int:
    """Ghi danh sách từ đã sắp xếp, mỗi từ một dòng, UTF-8 không BOM."""
    ordered = sorted(words, key=lambda w: (unicodedata.normalize("NFC", w).lower(), w))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        for w in ordered:
            f.write(w + "\n")
    return len(ordered)


def main() -> int:
    parser = argparse.ArgumentParser(description="Sinh & chuẩn hóa từ điển MIT/CC0 cho BambooMintKey")
    parser.add_argument("--output-dir", default="dicts", help="Thư mục xuất file .dict (mặc định: dicts)")
    parser.add_argument("--english-src", default="dicts/google-20000-english.dict",
                        help="File nguồn danh mục 20k từ tiếng Anh")
    parser.add_argument("--corpus", default="",
                        help="File corpus reference âm tiết thực tế (lọc tầng 2)")
    args = parser.parse_args()

    out_dir = args.output_dir
    os.makedirs(out_dir, exist_ok=True)

    # M1.1: Sinh ma trận ngữ âm (tầng 1 — âm tiết lý thuyết)
    vi_syllables = generate_vietnamese_syllables()

    # M1.2: Lọc corpus (tầng 2 — âm tiết thực tế) nếu có corpus reference
    if args.corpus and os.path.exists(args.corpus):
        corpus = load_corpus_reference(args.corpus)
        vi_syllables = vi_syllables & corpus
        print(f"[INFO] Corpus filter: {len(corpus)} reference syllables loaded")
    elif args.corpus:
        print(f"[WARN] Không tìm thấy corpus reference: {args.corpus}", file=sys.stderr)

    vi_path = os.path.join(out_dir, "vietnamese-syllables-mit.dict")
    vi_count = write_dict(vi_path, vi_syllables)
    print(f"[OK] vietnamese-syllables-mit.dict : {vi_count} syllables -> {vi_path}")

    # M1.3: Chuẩn hóa danh mục tiếng Anh
    if os.path.exists(args.english_src):
        en_words = normalize_english(args.english_src)
        en_path = os.path.join(out_dir, "english-20k.dict")
        en_count = write_dict(en_path, en_words)
        print(f"[OK] english-20k.dict : {en_count} words -> {en_path}")
    else:
        print(f"[WARN] Không tìm thấy nguồn tiếng Anh: {args.english_src}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
