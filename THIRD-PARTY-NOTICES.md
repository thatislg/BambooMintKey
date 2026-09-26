# Third-Party Software and Data Notices

## Tổng Quan Bản Quyền Dữ Liệu

Toàn bộ mã nguồn BambooMintKey được phát hành theo **MIT License** (xem `LICENSE`).

Dữ liệu từ điển phân phối cùng sản phẩm (`dicts/vietnamese-syllables-mit.dict`,
`dicts/english-20k.dict`) là tập hợp các đơn vị từ vựng tồn tại khách quan trong
ngôn ngữ tự nhiên (âm tiết tiếng Việt và từ tiếng Anh thông dụng). Chúng được
**tự sinh** từ ma trận ngữ âm học và được làm sạch, chuẩn hóa trong dự án
BambooMintKey, phát hành theo **MIT License / Creative Commons Zero (CC0)**.
Chi tiết quy trình xem `scripts/generate_mit_dict.py` và tài liệu
`docs/2.Design/Phase8/008_02_MIT_Dictionary_And_Corpus_Design.md`.

Các nguồn bên dưới chỉ được dùng làm **ngữ liệu tham chiếu (corpus reference)**
trong quá trình tạo/lọc dữ liệu, KHÔNG được nhúng hay phân phối trực tiếp trong
binary. Đây là dữ kiện ngôn ngữ tự nhiên, không phải tác phẩm sáng tạo được
bảo hộ bản quyền.

---

## 1. IBus-Bamboo

- **Description:** Vietnamese syllable validation data (`vietnamese.cm.dict`)
- **Vai trò:** Ngữ liệu tham chiếu âm tiết tiếng Việt thực tế (corpus reference)
- **Author:** Luong Thanh Lam and IBus-Bamboo contributors
- **Source:** https://github.com/BambooEngine/ibus-bamboo
- **License:** GNU General Public License v3.0 (GPL-3.0)

## 2. Vietnamese Wordlist (Viet74K)

- **Description:** Vietnamese compound word list
- **Vai trò:** Ngữ liệu tham chiếu từ ghép tiếng Việt (corpus reference)
- **Author:** Ho Ngoc Duc (compiled/distributed by Duyet)
- **Source:** https://github.com/duyet/vietnamese-wordlist
- **Original:** http://www.informatik.uni-leipzig.de/~duc/software/misc/wordlist.html
- **License:** GNU General Public License (GPL)

## 3. Google 20,000 English Words

- **Description:** English frequency wordlist (20,000 most common words)
- **Vai trò:** Nguồn chuẩn hóa danh mục `dicts/english-20k.dict`
- **Author / Maintainer:** Josh Kaufman (first20hours)
- **Source:** https://github.com/first20hours/google-10000-english
- **Derived from:** Google Web Trillion Word Corpus (Peter Norvig)
- **License:** MIT License / Public Domain (Open Data)
