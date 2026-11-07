#!/usr/bin/env python3
"""Generate the original Honor 9 HarmonyOS installer icon."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
SIZE = 256


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    fonts = Path("C:/Windows/Fonts")
    for candidate in (name, "seguisb.ttf", "arialbd.ttf"):
        path = fonts / candidate
        if path.exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
pixels = image.load()
for y in range(SIZE):
    for x in range(SIZE):
        mix = (x + y) / (2 * (SIZE - 1))
        pixels[x, y] = (
            int(19 + 4 * mix),
            int(52 + 124 * mix),
            int(92 + 102 * mix),
            255,
        )

mask = Image.new("L", (SIZE, SIZE), 0)
ImageDraw.Draw(mask).rounded_rectangle((4, 4, 251, 251), radius=52, fill=255)
image.putalpha(mask)
draw = ImageDraw.Draw(image)

# Phone body and display.
draw.rounded_rectangle((59, 27, 197, 229), radius=25, fill=(7, 25, 45, 210),
                       outline=(255, 255, 255, 240), width=7)
draw.rounded_rectangle((72, 49, 184, 194), radius=13, fill=(12, 54, 82, 245))
draw.rounded_rectangle((106, 37, 150, 42), radius=3, fill=(255, 255, 255, 205))
draw.ellipse((123, 207, 133, 217), fill=(255, 255, 255, 220))

# Download arrow.
accent = (83, 232, 205, 255)
draw.rounded_rectangle((119, 67, 137, 122), radius=8, fill=accent)
draw.polygon(((96, 111), (160, 111), (128, 145)), fill=accent)
draw.rounded_rectangle((94, 151, 162, 163), radius=6, fill=accent)

# Device family mark.
label_font = font("segoeuib.ttf", 31)
text = "H9"
box = draw.textbbox((0, 0), text, font=label_font)
draw.text(((SIZE - (box[2] - box[0])) / 2, 166), text, font=label_font,
          fill=(255, 255, 255, 245), anchor="ma")

png = ROOT / "installer-icon.png"
ico = ROOT / "installer-icon.ico"
image.save(png, optimize=True)
image.save(ico, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48),
                                     (64, 64), (128, 128), (256, 256)])
print(png)
print(ico)
