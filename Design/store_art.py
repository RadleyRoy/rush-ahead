"""
Google Play graphics for Rush Ahead, from the app icon and screenshots taken in a release build:

    python Design/store_art.py <shots folder>

The shots folder holds 1920x1080 captures from the Windows release player (see Store/RELEASE.md): a "clean" race
shot without the screens (-rushahead-no-hud) for the feature graphic, and ordinary ones for the screenshots.
Writes Store/Graphics: icon-512.png, feature-graphic.png and screenshot-*.jpg.
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
OUT = os.path.join(ROOT, "Store", "Graphics")
ICON = os.path.join(ROOT, "Assets", "_Project", "Art", "Icon", "AppIcon-1024.png")
FONTS = "C:\\Windows\\Fonts"

# Which captures become which screenshot, in the order Play shows them.
SCREENSHOTS = [
    ("screenshot-1-whirlpool.jpg", "rapids/4.png"),
    ("screenshot-2-team-race.jpg", "team/4.png"),
    ("screenshot-3-lake.jpg", "lake/4.png"),
    ("screenshot-4-menu.jpg", "tour/01-menu.png"),
    ("screenshot-5-your-animal.jpg", "tour/03-animal-changed.png"),
    ("screenshot-6-results.jpg", "tour/15-results.png"),
    ("screenshot-7-team-lobby.jpg", "tour/08-lobby-team.png"),
]
FEATURE_SHOT = "clean5/4.png"
FEATURE_CROP = (0, 350, 1300, 985)  # a 1024:500 window with the raft at the right, clear of the title


def font(names, size):
    for name in names:
        path = os.path.join(FONTS, name)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def feature_graphic(shots):
    """1024 x 500: a race on the river, darkened on the left behind the name and the pitch."""
    shot = Image.open(os.path.join(shots, FEATURE_SHOT)).convert("RGB")
    art = shot.crop(FEATURE_CROP).resize((1024, 500), Image.LANCZOS)

    shade = Image.new("RGBA", art.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(shade)
    for x in range(art.width):
        alpha = int(205 * max(0.0, 1.0 - x / (art.width * 0.62)) ** 1.4)
        d.line([(x, 0), (x, art.height)], fill=(9, 22, 48, alpha))
    art = Image.alpha_composite(art.convert("RGBA"), shade)

    icon = Image.open(ICON).convert("RGBA").resize((150, 150), Image.LANCZOS)
    mask = Image.new("L", (600, 600), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, 599, 599], radius=135, fill=255)
    icon.putalpha(mask.resize((150, 150), Image.LANCZOS))
    art.alpha_composite(icon, (56, 80))

    d = ImageDraw.Draw(art)
    title = font(["seguibl.ttf", "segoeuib.ttf", "arialbd.ttf"], 96)
    pitch = font(["segoeuib.ttf", "arialbd.ttf"], 34)
    d.text((56, 250), "Rush Ahead", font=title, fill=(255, 255, 255), stroke_width=4, stroke_fill=(9, 22, 48))
    d.text((60, 372), "Four animals. One raft.", font=pitch, fill=(255, 201, 60), stroke_width=2, stroke_fill=(9, 22, 48))
    d.text((60, 414), "No steering wheel.", font=pitch, fill=(255, 201, 60), stroke_width=2, stroke_fill=(9, 22, 48))
    art.convert("RGB").save(os.path.join(OUT, "feature-graphic.png"))


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    shots = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)

    Image.open(ICON).convert("RGBA").resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, "icon-512.png"))
    feature_graphic(shots)
    for name, source in SCREENSHOTS:
        image = Image.open(os.path.join(shots, source)).convert("RGB")
        assert image.size == (1920, 1080), f"{source} is {image.size}, not 1920x1080"
        image.save(os.path.join(OUT, name), quality=90, optimize=True)
    print(f"Wrote the Play graphics to {OUT}")


if __name__ == "__main__":
    main()
