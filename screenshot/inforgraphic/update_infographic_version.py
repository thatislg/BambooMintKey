#!/usr/bin/env python3
"""
BambooMintKey - Script tự động điền số phiên bản lên ảnh phôi Infographic.
Copyright (c) 2026 BambooMintKey Contributors. SPDX-License-Identifier: MIT

Cách dùng:
  1. Đặt ảnh phôi (tạo từ Google ImageFX) vào cùng thư mục với tên: template_base.png
  2. Chạy:
       python update_infographic_version.py
  Hoặc truyền phiên bản tùy ý:
       python update_infographic_version.py --win v1.2.0 --linux v1.1.5 --mac v1.2.0
"""

import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

def render_infographic(
    base_image_path: str = "template_base.png",
    output_image_path: str = "BambooMintKey_Infographic_Latest.png",
    win_ver: str = "v1.2.0",
    linux_ver: str = "v1.1.5",
    mac_ver: str = "v1.2.0"
):
    script_dir = Path(__file__).resolve().parent
    base_path = script_dir / base_image_path if not Path(base_image_path).is_absolute() else Path(base_image_path)
    output_path = script_dir / output_image_path if not Path(output_image_path).is_absolute() else Path(output_image_path)

    if not base_path.exists():
        print(f"[-] Lỗi: Không tìm thấy file ảnh phôi tại '{base_path}'!")
        print("[i] Hướng dẫn: Dùng prompt trong file '2026-10-13_BambooMintKey_Infographic_Prompt.md' tạo ảnh trên Google ImageFX, sau đó lưu về đặt tên là 'template_base.png'.")
        return False

    print(f"[+] Đang mở ảnh phôi: {base_path}...")
    img = Image.open(base_path).convert("RGBA")
    draw = ImageDraw.Draw(img)
    width, height = img.size

    # Cỡ chữ tỷ lệ theo chiều cao ảnh (16:9)
    font_size_name = max(18, int(height * 0.032))
    font_size_ver = max(24, int(height * 0.045))

    font_name = None
    font_ver = None
    # Thử nạp font hệ thống thông dụng
    for font_cand in ["segoeui.ttf", "arialbd.ttf", "arial.ttf", "DejaVuSans-Bold.ttf"]:
        try:
            font_ver = ImageFont.truetype(font_cand, size=font_size_ver)
            font_name = ImageFont.truetype(font_cand, size=font_size_name)
            break
        except Exception:
            continue

    if font_ver is None:
        font_ver = font_name = ImageFont.load_default()

    # Màu sắc nhận diện BambooMintKey
    mint_color = (0, 229, 163, 255)       # #00E5A3
    white_glow = (240, 253, 250, 255)     # #F0FDFA

    # Vị trí tâm (X, Y) của 3 khung badge trên layout 16:9
    badges = [
        {"name": "Windows", "ver": win_ver, "pos": (int(width * 0.22), int(height * 0.38))},
        {"name": "Linux / SteamOS", "ver": linux_ver, "pos": (int(width * 0.50), int(height * 0.38))},
        {"name": "macOS", "ver": mac_ver, "pos": (int(width * 0.78), int(height * 0.38))},
    ]

    for b in badges:
        x, y = b["pos"]
        draw.text((x, y - int(height * 0.025)), b["name"], fill=white_glow, font=font_name, anchor="mm", align="center")
        draw.text((x, y + int(height * 0.025)), b["ver"], fill=mint_color, font=font_ver, anchor="mm", align="center")

    img.save(output_path, "PNG")
    print(f"[✓] Đã xuất thành công Infographic hoàn chỉnh: {output_path}")
    return True

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Tự động cập nhật phiên bản lên Infographic BambooMintKey")
    parser.add_argument("--base", default="template_base.png", help="Đường dẫn ảnh phôi")
    parser.add_argument("--output", default="BambooMintKey_Infographic_Latest.png", help="Đường dẫn ảnh xuất ra")
    parser.add_argument("--win", default="v1.2.0", help="Phiên bản Windows")
    parser.add_argument("--linux", default="v1.1.5", help="Phiên bản Linux/Steam Deck")
    parser.add_argument("--mac", default="v1.2.0", help="Phiên bản macOS")
    args = parser.parse_args()

    render_infographic(
        base_image_path=args.base,
        output_image_path=args.output,
        win_ver=args.win,
        linux_ver=args.linux,
        mac_ver=args.mac
    )
