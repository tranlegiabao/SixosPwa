#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Sinh bo icon PWA tu mot anh logo nguon.  (ASCII-only source; xem README.md de biet cach dung)

Chay:
    python tools/generate-icons.py [duong-dan-logo-nguon]

Mac dinh doc logo Sixos cua HisSoft (667x667). Ghi 4 file vao SixosPwa/wwwroot/static/:

    icon-192.png              192x192  nen trong    - icon chuan cua manifest
    icon-512.png              512x512  nen trong    - icon lon cua manifest
    icon-maskable-512.png     512x512  nen dac      - Android bo goc, logo thu ve 72% cho an toan
    apple-touch-icon-180.png  180x180  nen dac      - iOS KHONG ho tro nen trong (se ra nen den)

Yeu cau: Pillow. KHONG dung lenh `convert` tren may nay - do la cong cu doi o dia cua Windows.
"""

import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT_DIR = os.path.join(ROOT, "SixosPwa", "wwwroot", "static")

DEFAULT_SOURCE = os.path.normpath(
    os.path.join(
        ROOT, "..", "master_3", "root", "HisSoft", "HisSoft",
        "wwwroot", "static", "faviicon.png",
    )
)

# Nen dac cho cac icon khong duoc phep trong suot.
SOLID_BG = (255, 255, 255, 255)

# (ten file, canh, ty le noi dung so voi khung, co nen dac khong)
#
# Ca 4 deu dung nen dac: logo Sixos nguon von co nen TRANG DAC (khong trong suot),
# neu de nen trong thi icon ra mot khoi trang noi giua vung trong suot - nhin lech
# tren nen toi. Neu sau nay thay bang logo that su co nen trong suot thi doi hai
# dong dau thanh False.
TARGETS = [
    ("icon-192.png", 192, 0.90, True),
    ("icon-512.png", 512, 0.90, True),
    ("icon-maskable-512.png", 512, 0.72, True),
    ("apple-touch-icon-180.png", 180, 0.86, True),
]


def content_bbox(img):
    """Tim khung bao quanh phan co noi dung: bo vien trong suot VA bo vien trang."""
    alpha = img.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is not None and bbox != (0, 0, img.width, img.height):
        return bbox

    # Anh khong co vien trong suot -> coi vung gan trang la nen.
    rgb = img.convert("RGB")
    white = Image.new("RGB", rgb.size, (255, 255, 255))
    from PIL import ImageChops

    diff = ImageChops.difference(rgb, white).convert("L")
    # Nguong 12/255 de bo qua nhieu nen JPEG hoac vien khu rang cua.
    mask = diff.point(lambda p: 255 if p > 12 else 0)
    return mask.getbbox() or (0, 0, img.width, img.height)


def square_canvas(content, side, scale, solid):
    """Dat `content` vao giua mot khung vuong `side` px, chiem `scale` phan khung."""
    inner = max(1, int(round(side * scale)))
    fitted = content.copy()
    fitted.thumbnail((inner, inner), Image.LANCZOS)

    bg = SOLID_BG if solid else (255, 255, 255, 0)
    canvas = Image.new("RGBA", (side, side), bg)
    canvas.paste(
        fitted,
        ((side - fitted.width) // 2, (side - fitted.height) // 2),
        fitted,
    )
    return canvas


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SOURCE
    if not os.path.isfile(source):
        sys.exit("Khong tim thay anh nguon: %s" % source)

    img = Image.open(source).convert("RGBA")
    content = img.crop(content_bbox(img))
    print("Nguon : %s  (%dx%d -> cat con %dx%d)" % (
        source, img.width, img.height, content.width, content.height))

    os.makedirs(OUT_DIR, exist_ok=True)
    for name, side, scale, solid in TARGETS:
        out_path = os.path.join(OUT_DIR, name)
        square_canvas(content, side, scale, solid).save(out_path, "PNG", optimize=True)
        print("  + %-26s %3dx%-3d  noi dung %d%%  nen %s" % (
            name, side, side, round(scale * 100), "dac" if solid else "trong"))

    print("Xong. Ghi vao: %s" % OUT_DIR)


if __name__ == "__main__":
    main()
