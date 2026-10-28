"""
The Rush Ahead app icon ("Four slices", option 6, polished) and every file the stores need:

    python Design/app_icon.py

Writes into Assets/_Project/Art/Icon (the editor's AppIcons step installs them in Player Settings):
  AppIcon-1024.png            the master: full bleed, no transparency (Apple requires none)
  AppIcon-Round-1024.png      a round version for Android's legacy round icon
  AdaptiveBackground-432.png  Android 8+ adaptive icon, back layer: the four coloured slices
  AdaptiveForeground-432.png  Android 8+ adaptive icon, front layer: the raft and its crew, inside the safe zone
and a preview sheet in Design/Icon options showing it under the shapes phones cut it to.

An adaptive layer is 108 dp square, of which the middle 72 dp is the visible icon and a 66 dp circle is always safe.
The 1024 design maps onto that visible 72 dp, so each layer is drawn on a canvas one and a half times as wide.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

import make_icons as mk

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "Assets", "_Project", "Art", "Icon")
PREVIEW = os.path.join(HERE, "Icon options", "Final - Four slices.png")

NAVY_IN, NAVY_OUT = (66, 98, 168), (22, 34, 78)
SEAT_ANGLES = [-135, -45, 45, 135]          # screen angles of the four seats: up-left, up-right, down-right, down-left
CREW = ["cat", "bear", "pig", "bunny"]
SLICE_COLOURS = mk.PLAYERS                   # the game's player colours: red, blue, yellow, green


def setup(pixels, design_units, offset):
    mk.S = pixels
    mk.K = pixels / design_units
    mk.OFFSET = offset


def background(ic):
    """Navy, glowing behind the raft, with four rounded slices of colour radiating from it."""
    ic.radial(NAVY_IN, NAVY_OUT, cx=512, cy=512, radius=820)
    for i, colour in enumerate(SLICE_COLOURS):
        centre = SEAT_ANGLES[i]
        rounded_slice(ic, 512, 512, 318, 482, centre - 41, centre + 41, colour)


def rounded_slice(ic, cx, cy, inner, outer, a0, a1, colour, corner=24):
    """An annular sector with rounded corners, lighter toward the raft. The corners are rounded by blurring the
    sharp shape and cutting it again at half strength, which rounds every corner by about the blur's width."""
    def ring(r, frm, to, steps=60):
        return [(cx + r * math.cos(math.radians(frm + (to - frm) * k / steps)),
                 cy + r * math.sin(math.radians(frm + (to - frm) * k / steps))) for k in range(steps + 1)]

    sharp = Image.new("L", (mk.S, mk.S), 0)
    ImageDraw.Draw(sharp).polygon([(mk.p(x), mk.p(y)) for x, y in ring(outer, a0, a1) + ring(inner, a1, a0)], fill=255)
    soft = sharp.filter(ImageFilter.GaussianBlur(mk.q(corner) * 0.55))
    mask = soft.point(lambda v: 255 if v > 128 else 0).filter(ImageFilter.GaussianBlur(mk.q(1.2)))

    # Lighter near the raft, the full colour outward.
    shade = Image.new("RGBA", (mk.S, mk.S))
    sd = ImageDraw.Draw(shade)
    light = mk.lighten(colour, 0.35)
    for k in range(60, 0, -1):
        t = k / 60
        r = inner + (outer - inner + 30) * t
        c = tuple(int(light[j] + (colour[j] - light[j]) * t) for j in range(3)) + (255,)
        sd.ellipse([mk.p(cx - r), mk.p(cy - r), mk.p(cx + r), mk.p(cy + r)], fill=c)
    ic.im.paste(shade, (0, 0), mask)


def foreground(ic):
    """The raft from above with its four animals, each facing out over its own slice, with air streaming out across it."""
    # A soft shadow lifts the raft off the slices.
    shadow = Image.new("RGBA", (mk.S, mk.S))
    r = 318
    ImageDraw.Draw(shadow).ellipse([mk.p(512 - r + 14), mk.p(512 - r + 22), mk.p(512 + r + 14), mk.p(512 + r + 22)], fill=(5, 10, 30, 120))
    ic.im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(mk.q(18))))

    # Air streaming out across each slice: three parallel wind lines, the middle one leading, each with a gentle wave.
    for angle in SEAT_ANGLES:
        a = math.radians(angle)
        out = (math.cos(a), math.sin(a))
        side = (-out[1], out[0])
        for offset, start, length in ((-44, 352, 62), (0, 336, 86), (44, 352, 62)):
            pts = []
            for k in range(11):
                u = k / 10
                along = start + u * length
                wave = math.sin(u * math.pi * 2) * 6
                pts.append((512 + out[0] * along + side[0] * (offset + wave), 512 + out[1] * along + side[1] * (offset + wave)))
            ic.stroke(pts, (255, 255, 255, 225), 14)

    ic.raft_top(512, 512, 300)
    for i, kind in enumerate(CREW):
        a = math.radians(SEAT_ANGLES[i])
        out = (math.cos(a), math.sin(a))
        ic.head(kind, 512 + out[0] * 150, 512 + out[1] * 150, 70, look=(out[0], out[1]))


def render(layer_names, pixels, design_units, offset, out_size, transparent=False):
    setup(pixels, design_units, offset)
    ic = mk.Icon((0, 0, 0, 0) if transparent else NAVY_OUT + (255,))
    if "background" in layer_names:
        background(ic)
    if "foreground" in layer_names:
        foreground(ic)
    return ic.im.resize((out_size, out_size), Image.LANCZOS)


def masked(image, shape):
    """The icon as a phone shows it: a circle, a rounded square, or a squircle."""
    size = image.width
    mask = Image.new("L", (size * 4, size * 4), 0)
    d = ImageDraw.Draw(mask)
    if shape == "circle":
        d.ellipse([0, 0, size * 4 - 1, size * 4 - 1], fill=255)
    elif shape == "rounded":
        d.rounded_rectangle([0, 0, size * 4 - 1, size * 4 - 1], radius=int(size * 4 * 0.225), fill=255)
    else:  # squircle: |x|^5 + |y|^5 <= 1
        pts = []
        for k in range(720):
            t = 2 * math.pi * k / 720
            c, s = math.cos(t), math.sin(t)
            x = math.copysign(abs(c) ** 0.4, c)
            y = math.copysign(abs(s) ** 0.4, s)
            pts.append(((x + 1) * size * 2, (y + 1) * size * 2))
        d.polygon(pts, fill=255)
    out = image.copy()
    out.putalpha(mask.resize((size, size), Image.LANCZOS))
    return out


def main():
    os.makedirs(ART, exist_ok=True)

    # The master: the design's 1024 square, opaque.
    master = render(("background", "foreground"), 2048, 1024, 0, 1024).convert("RGB")
    master.save(os.path.join(ART, "AppIcon-1024.png"))
    masked(master.convert("RGBA"), "circle").save(os.path.join(ART, "AppIcon-Round-1024.png"))

    # Adaptive layers: 1536 design units wide (the 1024 icon plus 256 each side), 432 px (xxxhdpi, 108 dp).
    back = render(("background",), 3072, 1536, 256, 432).convert("RGB")
    front = render(("foreground",), 3072, 1536, 256, 432, transparent=True)
    back.save(os.path.join(ART, "AdaptiveBackground-432.png"))
    front.save(os.path.join(ART, "AdaptiveForeground-432.png"))

    # Preview: the master, and the adaptive layers as three launchers would cut them (the middle 72 dp of 108).
    adaptive = back.convert("RGBA")
    adaptive.alpha_composite(front)
    visible = adaptive.crop((72, 72, 360, 360)).resize((300, 300), Image.LANCZOS)
    sheet = Image.new("RGBA", (5 * 340 + 40, 420), (246, 247, 250, 255))
    tiles = [masked(master.convert("RGBA").resize((300, 300), Image.LANCZOS), "rounded")] + \
            [masked(visible, shape) for shape in ("circle", "squircle", "rounded")] + \
            [masked(master.convert("RGBA").resize((300, 300), Image.LANCZOS), "rounded").resize((96, 96), Image.LANCZOS)]
    labels = ["iOS", "Android: circle", "Android: squircle", "Android: rounded", "Home screen size"]
    d = ImageDraw.Draw(sheet)
    from PIL import ImageFont
    font = ImageFont.truetype("C:\\Windows\\Fonts\\segoeuib.ttf", 24)
    for i, (tile, label) in enumerate(zip(tiles, labels)):
        x = 40 + i * 340
        y = 40 + (300 - tile.height) // 2
        sheet.alpha_composite(tile, (x + (300 - tile.width) // 2, y))
        d.text((x + 150, 375), label, font=font, fill=(42, 34, 56), anchor="mm")
    sheet.save(PREVIEW)
    print(f"Wrote the app icon files to {os.path.normpath(ART)} and a preview to {PREVIEW}")


if __name__ == "__main__":
    main()
