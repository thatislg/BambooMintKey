#!/usr/bin/env python3
"""
Generate src/media/bamboomintkey.ico from src/media/rendered_v_64x64.png.
Produces a multi-resolution ICO with 16x16, 24x24, 32x32, 48x48 and 64x64.
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src" / "media" / "rendered_v_64x64.png"
DEST = ROOT / "src" / "media" / "bamboomintkey.ico"
SIZES = [16, 24, 32, 48, 64]


def main() -> None:
    src = Image.open(SRC)
    if src.mode != "RGBA":
        src = src.convert("RGBA")

    imgs = [src.resize((s, s), Image.Resampling.LANCZOS) for s in SIZES]
    # Save from the largest image; append_images works reliably this way in Pillow.
    imgs[-1].save(DEST, format="ICO", append_images=imgs[:-1][::-1])

    with Image.open(DEST) as ico:
        print(f"Generated {DEST} with sizes: {sorted(ico.ico.sizes())}")


if __name__ == "__main__":
    main()
