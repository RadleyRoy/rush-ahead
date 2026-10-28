"""
Draws the ten candidate app icons for Rush Ahead, plus a contact sheet to choose from.

    python Design/make_icons.py

Each icon is drawn at 2048 px and scaled down to 1024 px for clean edges. Full-bleed squares: the stores and Android's
adaptive icons apply their own corner masks, so the sheet only previews the rounding.
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "Icon options")
S = 2048          # drawing size in pixels
K = S / 1024      # pixels per design unit; designs are laid out in a 1024 square
OFFSET = 0        # design units added before scaling, so a layer can extend past the icon (Android adaptive layers)

INK = (42, 34, 56, 255)          # outline
WHITE = (255, 255, 255, 255)
CREAM = (255, 248, 238, 255)
PEACH = (255, 179, 138, 255)     # raft tube
BAND = (40, 181, 168, 255)       # raft band
FLOOR = (220, 246, 236, 255)
ORANGE = (255, 140, 38, 255)     # leaf blower
PIPE = (61, 61, 74, 255)
PINK = (255, 156, 178, 255)
FUR = {
    "bear": (158, 107, 71, 255), "cat": (247, 161, 58, 255), "bunny": (245, 239, 230, 255),
    "pig": (255, 176, 190, 255), "otter": (116, 80, 58, 255), "raccoon": (154, 154, 163, 255),
}
PLAYERS = [(255, 97, 77, 255), (77, 140, 255, 255), (255, 214, 51, 255), (102, 224, 102, 255)]


def p(v):
    """A position in design units, as pixels."""
    return (v + OFFSET) * K


def q(v):
    """A length (a width or a size) in design units, as pixels."""
    return v * K


def lighten(c, t):
    return tuple(int(c[i] + (255 - c[i]) * t) for i in range(3)) + (255,)


def darken(c, t):
    return tuple(int(c[i] * (1 - t)) for i in range(3)) + (255,)


class Icon:
    def __init__(self, background):
        self.im = Image.new("RGBA", (S, S), background)
        self.d = ImageDraw.Draw(self.im)

    # --- backgrounds -------------------------------------------------------------------------------------------
    def radial(self, inner, outer, cx=512, cy=420, radius=760):
        """A soft radial gradient: bright behind the subject, deeper at the edges."""
        g = Image.new("RGBA", (S, S))
        gd = ImageDraw.Draw(g)
        steps = 90
        for i in range(steps, 0, -1):
            t = i / steps
            col = tuple(int(inner[k] + (outer[k] - inner[k]) * t) for k in range(3)) + (255,)
            r = radius * t
            gd.ellipse([p(cx - r), p(cy - r), p(cx + r), p(cy + r)], fill=col)
        self.im.alpha_composite(Image.new("RGBA", (S, S), outer))
        self.im.alpha_composite(g)

    def vertical(self, top, bottom, y0=0, y1=1024):
        for y in range(int(p(y0)), int(p(y1))):
            t = (y - p(y0)) / max(1, p(y1) - p(y0))
            col = tuple(int(top[k] + (bottom[k] - top[k]) * t) for k in range(3)) + (255,)
            self.d.line([(0, y), (S, y)], fill=col)

    # --- primitives ---------------------------------------------------------------------------------------------
    def ellipse(self, cx, cy, rx, ry, fill, line=8, ink=INK):
        if line:
            self.d.ellipse([p(cx - rx - line), p(cy - ry - line), p(cx + rx + line), p(cy + ry + line)], fill=ink)
        self.d.ellipse([p(cx - rx), p(cy - ry), p(cx + rx), p(cy + ry)], fill=fill)

    def circle(self, cx, cy, r, fill, line=8, ink=INK):
        self.ellipse(cx, cy, r, r, fill, line, ink)

    def polygon(self, points, fill, line=8, ink=INK):
        pts = [(p(x), p(y)) for x, y in points]
        if line:
            self.d.polygon(pts, fill=ink, outline=ink, width=int(q(line) * 2))
        self.d.polygon(pts, fill=fill)

    def stroke(self, points, color, width, round_caps=True):
        pts = [(p(x), p(y)) for x, y in points]
        self.d.line(pts, fill=color, width=int(q(width)), joint="curve")
        if round_caps:
            for x, y in (pts[0], pts[-1]):
                r = q(width) / 2
                self.d.ellipse([x - r, y - r, x + r, y + r], fill=color)

    def arc(self, cx, cy, r, start, end, color, width):
        """A thick arc with round ends, angles in degrees (0 = right, clockwise, as on screen)."""
        steps = max(8, int(abs(end - start) / 3))
        pts = [(cx + r * math.cos(math.radians(start + (end - start) * i / steps)),
                cy + r * math.sin(math.radians(start + (end - start) * i / steps))) for i in range(steps + 1)]
        self.stroke(pts, color, width)

    def bar(self, x0, y0, x1, y1, thick, fill, line=8):
        """A rounded bar from one point to another (a blower body or pipe)."""
        dx, dy = x1 - x0, y1 - y0
        length = math.hypot(dx, dy)
        nx, ny = -dy / length * thick / 2, dx / length * thick / 2
        quad = [(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)]
        if line:
            self.polygon(quad, INK, line=line)
            self.circle(x0, y0, thick / 2, INK, line=line)
            self.circle(x1, y1, thick / 2, INK, line=line)
        self.polygon(quad, fill, line=0)
        self.circle(x0, y0, thick / 2, fill, line=0)
        self.circle(x1, y1, thick / 2, fill, line=0)

    def text(self, x, y, words, size, color, font="seguibl.ttf", anchor="mm", outline=0):
        try:
            f = ImageFont.truetype(os.path.join("C:\\Windows\\Fonts", font), int(q(size)))
        except OSError:
            f = ImageFont.truetype("arialbd.ttf", int(q(size)))
        self.d.text((p(x), p(y)), words, font=f, fill=color, anchor=anchor,
                    stroke_width=int(q(outline)), stroke_fill=INK)

    # --- the game's things --------------------------------------------------------------------------------------
    def eyes(self, cx, cy, r, look=(0.0, 0.0)):
        for side in (-1, 1):
            ex, ey = cx + side * 0.36 * r, cy - 0.1 * r
            self.circle(ex, ey, 0.19 * r, WHITE, line=max(3, 0.04 * r))
            self.circle(ex + look[0] * 0.07 * r, ey + look[1] * 0.07 * r, 0.1 * r, INK, line=0)
            self.circle(ex + look[0] * 0.07 * r - 0.035 * r, ey + look[1] * 0.07 * r - 0.04 * r, 0.035 * r, WHITE, line=0)

    def head(self, kind, cx, cy, r, look=(0.0, 0.0)):
        fur = FUR[kind]
        light = lighten(fur, 0.55) if kind not in ("bunny", "pig") else lighten(fur, 0.35)
        line = max(5, 0.07 * r)
        if kind == "bear":
            for s in (-1, 1):
                self.circle(cx + s * 0.72 * r, cy - 0.7 * r, 0.36 * r, fur, line)
                self.circle(cx + s * 0.72 * r, cy - 0.7 * r, 0.18 * r, light, 0)
        elif kind in ("cat", "raccoon"):
            for s in (-1, 1):
                self.polygon([(cx + s * 0.88 * r, cy - 0.3 * r), (cx + s * 0.62 * r, cy - 1.18 * r), (cx + s * 0.12 * r, cy - 0.78 * r)], fur, line)
                tip = WHITE if kind == "raccoon" else PINK
                self.polygon([(cx + s * 0.73 * r, cy - 0.5 * r), (cx + s * 0.62 * r, cy - 0.98 * r), (cx + s * 0.32 * r, cy - 0.76 * r)], tip, 0)
        elif kind == "bunny":
            for s in (-1, 1):
                self.ellipse(cx + s * 0.36 * r, cy - 1.25 * r, 0.24 * r, 0.62 * r, fur, line)
                self.ellipse(cx + s * 0.36 * r, cy - 1.22 * r, 0.12 * r, 0.46 * r, PINK, 0)
        elif kind == "pig":
            for s in (-1, 1):
                self.polygon([(cx + s * 0.9 * r, cy - 0.45 * r), (cx + s * 0.78 * r, cy - 1.05 * r), (cx + s * 0.3 * r, cy - 0.82 * r)], darken(fur, 0.05), line)
        elif kind == "otter":
            for s in (-1, 1):
                self.circle(cx + s * 0.86 * r, cy - 0.42 * r, 0.2 * r, fur, line)

        self.circle(cx, cy, r, fur, line)
        if kind == "raccoon":
            self.ellipse(cx, cy - 0.1 * r, 0.82 * r, 0.27 * r, (52, 46, 60, 255), 0)
        if kind in ("bear", "raccoon"):
            self.ellipse(cx, cy + 0.36 * r, 0.42 * r, 0.3 * r, light if kind == "bear" else CREAM, 0)
        if kind == "otter":
            self.ellipse(cx, cy + 0.34 * r, 0.6 * r, 0.32 * r, light, 0)
        if kind == "cat":
            self.ellipse(cx, cy + 0.36 * r, 0.36 * r, 0.24 * r, lighten(fur, 0.6), 0)
        self.eyes(cx, cy, r, look)
        if kind == "pig":
            self.ellipse(cx, cy + 0.32 * r, 0.36 * r, 0.25 * r, darken(fur, 0.12), max(4, 0.05 * r))
            for s in (-1, 1):
                self.ellipse(cx + s * 0.13 * r, cy + 0.32 * r, 0.06 * r, 0.1 * r, darken(fur, 0.45), 0)
        else:
            nose = PINK if kind in ("cat", "bunny") else INK
            self.ellipse(cx, cy + 0.22 * r, 0.12 * r, 0.085 * r, nose, 0)
        # cheeks
        for s in (-1, 1):
            self.ellipse(cx + s * 0.6 * r, cy + 0.3 * r, 0.12 * r, 0.07 * r, (255, 120, 140, 120), 0)

    def blower(self, x, y, angle, length, body=ORANGE, scale=1.0):
        """A leaf blower held at (x, y), nozzle pointing along angle (degrees, screen)."""
        a = math.radians(angle)
        bx, by = x + math.cos(a) * 0.32 * length, y + math.sin(a) * 0.32 * length
        nx, ny = x + math.cos(a) * length, y + math.sin(a) * length
        self.bar(bx, by, nx, ny, 34 * scale, PIPE, line=7 * scale)
        self.bar(x - math.cos(a) * 0.1 * length, y - math.sin(a) * 0.1 * length, bx, by, 92 * scale, body, line=8 * scale)
        self.circle(x - math.cos(a) * 0.02 * length, y - math.sin(a) * 0.02 * length, 26 * scale, darken(body, 0.25), line=0)
        return nx, ny

    def gust(self, x, y, angle, length, spread=40, width=26, color=WHITE, lines=3, puff=1.0):
        """Curvy air streaks leaving a nozzle at (x, y) along angle, ending in puffs (scaled by puff; 0 for none)."""
        a = math.radians(angle)
        for i in range(lines):
            off = (i - (lines - 1) / 2) * spread
            pts = []
            for t in range(13):
                u = t / 12
                along = 20 + u * length
                wob = math.sin(u * math.pi * 1.6 + i) * 14 * u + off * u
                pts.append((x + math.cos(a) * along - math.sin(a) * wob, y + math.sin(a) * along + math.cos(a) * wob))
            self.stroke(pts, color, width * (1 - 0.25 * abs(off) / max(1, spread)))
        # puffs at the end
        if puff <= 0:
            return
        ex, ey = x + math.cos(a) * (length + 30 * puff), y + math.sin(a) * (length + 30 * puff)
        for k, (dx, dy, rr) in enumerate([(0, 0, 46), (-38, -26, 32), (34, -30, 30), (30, 30, 26)]):
            self.circle(ex + dx * puff, ey + dy * puff, rr * puff, color, line=0)

    def cloud(self, x, y, r, color=WHITE, line=0):
        for dx, dy, rr in [(-0.7, 0.1, 0.55), (0.7, 0.15, 0.5), (0, -0.2, 0.75), (-0.25, 0.35, 0.5), (0.35, 0.35, 0.5)]:
            self.circle(x + dx * r, y + dy * r, rr * r, color, line)

    def raft_top(self, cx, cy, r, handles=True):
        """The round inflatable raft seen from above."""
        self.circle(cx, cy, r * 1.04, BAND, line=10)
        self.circle(cx, cy, r, PEACH, line=0)
        self.circle(cx, cy, r * 0.97, lighten(PEACH, 0.15), line=0)
        self.circle(cx, cy, r * 0.68, INK, line=0)
        self.circle(cx, cy, r * 0.66, FLOOR, line=0)
        if handles:
            for k in range(8):
                a = math.radians(22.5 + k * 45)
                self.bar(cx + math.cos(a) * r * 0.86 - math.sin(a) * 18, cy + math.sin(a) * r * 0.86 + math.cos(a) * 18,
                         cx + math.cos(a) * r * 0.86 + math.sin(a) * 18, cy + math.sin(a) * r * 0.86 - math.cos(a) * 18, 12, PIPE, line=0)

    def raft_side_back(self, cx, cy, rx, ry):
        """Back half of the raft in three-quarter view: draw the crew after this, then raft_side_front."""
        self.ellipse(cx, cy + ry * 0.18, rx * 1.03, ry * 1.08, BAND, line=10)
        self.ellipse(cx, cy, rx, ry, PEACH, line=0)
        self.ellipse(cx, cy - ry * 0.06, rx * 0.8, ry * 0.62, INK, line=0)
        self.ellipse(cx, cy - ry * 0.06, rx * 0.78, ry * 0.58, FLOOR, line=0)

    def raft_side_front(self, cx, cy, rx, ry):
        """The front of the tube, drawn over the crew's bodies."""
        outer, inner = [], []
        for i in range(0, 181, 4):
            a = math.radians(i)
            outer.append((cx + rx * math.cos(a), cy + ry * math.sin(a)))
            inner.append((cx + rx * 0.8 * math.cos(a), cy - ry * 0.06 + ry * 0.58 * math.sin(a)))
        shape = outer + inner[::-1]
        self.polygon(shape, PEACH, line=8)
        hi = [(x, y - ry * 0.12) for x, y in outer[8:38]]
        self.stroke(hi, lighten(PEACH, 0.45), 18)

    def waves(self, y, color, amplitude=14, wavelength=150, width=14, x0=-40, x1=1064):
        pts = [(x, y + math.sin(x / wavelength * 2 * math.pi) * amplitude) for x in range(x0, x1, 12)]
        self.stroke(pts, color, width)

    def droplets(self, points, color=WHITE):
        for x, y, r in points:
            self.ellipse(x, y, r * 0.8, r, color, line=0)

    def save(self, path):
        self.im.resize((1024, 1024), Image.LANCZOS).save(path)


# ------------------------------------------------------------------------------------------------------------------
# The ten designs
# ------------------------------------------------------------------------------------------------------------------

def crew_from_above():
    ic = Icon((30, 159, 214, 255))
    ic.radial((84, 214, 245), (24, 140, 205), cy=512, radius=720)
    for k in range(6):
        ic.arc(512, 512, 430 + k * 14, 200 + k * 25, 240 + k * 25, (255, 255, 255, 70), 10)
    kinds = ["cat", "bear", "pig", "bunny"]
    ic.raft_top(512, 512, 320)
    for i, kind in enumerate(kinds):
        a = math.radians(-135 + 90 * i)
        hx, hy = 512 + math.cos(a) * 135, 512 + math.sin(a) * 135
        ic.head(kind, hx, hy, 74, look=(math.cos(a), math.sin(a)))
        side = math.radians(-135 + 90 * i + 50)
        bx, by = hx + math.cos(side) * 62, hy + math.sin(side) * 62
        nx, ny = ic.blower(bx, by, math.degrees(a), 165, scale=0.62)
        ic.gust(nx, ny, math.degrees(a), 60, spread=24, width=18, lines=3)
    return ic


def bear_blast():
    ic = Icon((255, 176, 59, 255))
    ic.radial((255, 214, 120), (250, 140, 40), cx=420, cy=470, radius=780)
    ic.head("bear", 330, 460, 230, look=(1, 0.2))
    nx, ny = ic.blower(420, 640, -12, 330, body=(40, 181, 168, 255), scale=1.5)
    ic.gust(nx, ny, -12, 210, spread=70, width=40, lines=3)
    ic.raft_side_front(420, 870, 560, 190)
    ic.droplets([(150, 760, 18), (900, 760, 16), (820, 690, 12)])
    return ic


def otter_and_raccoon():
    ic = Icon((143, 216, 255, 255))
    ic.vertical((150, 222, 255), (110, 200, 245), 0, 600)
    ic.vertical((60, 196, 236), (24, 150, 214), 600, 1024)
    ic.cloud(800, 170, 70, (255, 255, 255, 230))
    ic.cloud(190, 230, 50, (255, 255, 255, 200))
    ic.raft_side_back(512, 690, 400, 175)
    ic.head("otter", 395, 520, 128, look=(-0.6, 0.2))
    ic.head("raccoon", 635, 515, 128, look=(0.6, 0.2))
    nx, ny = ic.blower(330, 610, 195, 190, scale=1.0)
    ic.gust(nx, ny, 195, 110, spread=34, width=24)
    nx, ny = ic.blower(700, 610, -15, 190, body=(255, 214, 51, 255), scale=1.0)
    ic.gust(nx, ny, -15, 110, spread=34, width=24)
    ic.raft_side_front(512, 690, 400, 175)
    ic.waves(930, (255, 255, 255, 140), amplitude=10, width=12)
    ic.droplets([(120, 640, 16), (900, 630, 18), (960, 700, 12), (70, 720, 12)])
    return ic


def rush_arrow():
    ic = Icon((31, 111, 224, 255))
    ic.radial((70, 150, 245), (20, 80, 190), cx=600, cy=420, radius=800)
    # A great swooshing arrow from bottom left to top right.
    body = [(120 + t * 7, 900 - t * 6.4 - math.sin(t / 100 * math.pi) * 120) for t in range(0, 101, 4)]
    ic.stroke(body, (255, 255, 255, 255), 130)
    tip_x, tip_y = body[-1]
    ic.polygon([(tip_x + 150, tip_y - 130), (tip_x - 120, tip_y - 70), (tip_x + 70, tip_y + 130)], WHITE, line=0)
    for k in range(4):
        ic.stroke([(130 + k * 60, 300 + k * 70), (300 + k * 60, 300 + k * 70)], (255, 255, 255, 110), 18)
    ic.raft_top(560, 470, 130, handles=False)
    for i, kind in enumerate(["bear", "bunny"]):
        ic.head(kind, 520 + i * 80, 470 - i * 10, 46)
    return ic


def r_monogram():
    ic = Icon((255, 111, 94, 255))
    ic.radial((255, 150, 120), (235, 80, 70), cx=512, cy=470, radius=760)
    ic.text(500, 470, "R", 700, (200, 60, 55, 255))  # a soft shadow
    ic.text(484, 452, "R", 700, WHITE)
    ic.gust(600, 850, 180, 380, spread=36, width=28, lines=3)
    ic.raft_top(760, 790, 150, handles=False)
    ic.head("cat", 722, 790, 52, look=(-1, 0))
    ic.head("bear", 800, 790, 52, look=(-1, 0))
    return ic


def four_slices():
    ic = Icon((35, 58, 107, 255))
    ic.radial((60, 90, 150), (25, 40, 85), cy=512, radius=760)
    for i, col in enumerate(PLAYERS):
        start = -135 + 90 * i - 42
        box = [p(512 - 470), p(512 - 470), p(512 + 470), p(512 + 470)]
        layer = Image.new("RGBA", (S, S))
        ImageDraw.Draw(layer).pieslice(box, start, start + 84, fill=col[:3] + (225,))
        ic.im.alpha_composite(layer)
    ic.raft_top(512, 512, 300)
    for i, kind in enumerate(["cat", "bear", "pig", "bunny"]):
        a = math.radians(-135 + 90 * i)
        ic.head(kind, 512 + math.cos(a) * 140, 512 + math.sin(a) * 140, 72, look=(math.cos(a), math.sin(a)))
    return ic


def blower_blast():
    ic = Icon((91, 227, 176, 255))
    ic.radial((150, 245, 205), (50, 200, 150), cx=620, cy=380, radius=800)
    ic.gust(400, 640, -42, 380, spread=90, width=48, lines=3)
    ic.cloud(735, 330, 120)
    ic.blower(60, 990, -42, 470, scale=2.6)
    ic.raft_side_back(720, 300, 150, 66)
    ic.head("bunny", 720, 255, 62, look=(0.4, -0.2))
    ic.raft_side_front(720, 300, 150, 66)
    return ic


def splash():
    ic = Icon((108, 203, 255, 255))
    ic.vertical((130, 215, 255), (95, 190, 250), 0, 580)
    ic.vertical((50, 190, 235), (20, 140, 205), 580, 1024)
    # the splash crown
    for k in range(9):
        a = math.radians(200 + k * 17.5)
        x, y = 512 + math.cos(a) * 420, 690 + math.sin(a) * 300
        ic.ellipse(x, y, 52, 120, (255, 255, 255, 235), line=0)
    ic.ellipse(512, 760, 470, 120, WHITE, line=0)
    ic.raft_side_back(512, 650, 340, 150)
    ic.blower(400, 560, 205, 170, scale=0.9)
    ic.blower(630, 560, -25, 170, body=(255, 97, 77, 255), scale=0.9)
    ic.head("pig", 410, 500, 110, look=(-0.4, -0.3))
    ic.head("cat", 620, 495, 110, look=(0.4, -0.3))
    ic.raft_side_front(512, 650, 340, 150)
    ic.droplets([(150, 380, 24), (880, 360, 26), (240, 260, 16), (790, 250, 18), (512, 180, 20), (90, 520, 14), (950, 520, 14)])
    return ic


def finish_flag():
    ic = Icon((255, 210, 63, 255))
    ic.radial((255, 232, 140), (250, 185, 30), cx=512, cy=420, radius=760)
    # a chequered flag rippling across the top
    cells, size = 9, 96
    for row in range(3):
        for col in range(cells):
            x = 80 + col * size
            y = 110 + row * size + math.sin(col * 0.8) * 30
            colour = INK if (row + col) % 2 == 0 else WHITE
            ic.polygon([(x, y), (x + size, y + math.sin((col + 1) * 0.8) * 30 - math.sin(col * 0.8) * 30),
                        (x + size, y + size + math.sin((col + 1) * 0.8) * 30 - math.sin(col * 0.8) * 30), (x, y + size)], colour, line=0)
    ic.raft_side_back(512, 720, 380, 165)
    ic.blower(390, 640, 160, 190, scale=1.0)
    ic.blower(640, 640, 20, 190, body=(77, 140, 255, 255), scale=1.0)
    ic.head("bear", 400, 570, 118, look=(0, 0.3))
    ic.head("bunny", 630, 575, 112, look=(0, 0.3))
    ic.raft_side_front(512, 720, 380, 165)
    for k in range(3):
        ic.stroke([(80, 930 - k * 40), (260, 930 - k * 40)], (255, 255, 255, 170), 16)
        ic.stroke([(760, 930 - k * 40), (940, 930 - k * 40)], (255, 255, 255, 170), 16)
    return ic


def bunny_dryer():
    ic = Icon((255, 158, 196, 255))
    ic.radial((255, 205, 225), (245, 120, 170), cx=470, cy=480, radius=760)
    blue = (90, 120, 255, 255)
    ic.head("bunny", 330, 500, 190, look=(0.8, 0.2))
    # the hair dryer: a handle, a round back and a barrel
    ic.bar(520, 650, 495, 800, 64, darken(blue, 0.15), line=9)
    ic.bar(520, 610, 720, 575, 100, blue, line=10)
    ic.circle(520, 610, 92, blue, line=10)
    ic.circle(520, 610, 46, lighten(blue, 0.45), line=0)
    ic.bar(700, 578, 760, 568, 112, darken(blue, 0.3), line=8)
    ic.gust(770, 566, -10, 150, spread=46, width=30, lines=3)
    ic.raft_side_front(512, 900, 560, 170)
    for x, y, r in [(150, 190, 22), (880, 690, 18), (230, 760, 14)]:
        ic.polygon([(x, y - r * 2), (x + r * 0.5, y - r * 0.5), (x + r * 2, y), (x + r * 0.5, y + r * 0.5), (x, y + r * 2),
                    (x - r * 0.5, y + r * 0.5), (x - r * 2, y), (x - r * 0.5, y - r * 0.5)], WHITE, line=0)
    return ic


DESIGNS = [
    ("Crew from above", crew_from_above),
    ("Bear blast", bear_blast),
    ("Otter and raccoon", otter_and_raccoon),
    ("Rush arrow", rush_arrow),
    ("R monogram", r_monogram),
    ("Four slices", four_slices),
    ("Blower blast", blower_blast),
    ("Big splash", splash),
    ("Finish flag", finish_flag),
    ("Bunny and dryer", bunny_dryer),
]


def rounded(image, size, radius_fraction=0.225):
    """Preview the corner rounding a phone applies."""
    small = image.resize((size, size), Image.LANCZOS)
    mask = Image.new("L", (size * 4, size * 4), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size * 4 - 1, size * 4 - 1], radius=int(size * 4 * radius_fraction), fill=255)
    small.putalpha(mask.resize((size, size), Image.LANCZOS))
    return small


def contact_sheet(icons):
    cols, tile, small, pad = 5, 300, 64, 44
    width = cols * (tile + pad) + pad
    height = 2 * (tile + small + 150) + 130
    sheet = Image.new("RGBA", (width, height), (246, 247, 250, 255))
    d = ImageDraw.Draw(sheet)
    title = ImageFont.truetype("C:\\Windows\\Fonts\\seguibl.ttf", 44)
    label = ImageFont.truetype("C:\\Windows\\Fonts\\segoeuib.ttf", 26)
    d.text((pad, 34), "Rush Ahead: icon options (pick a number)", font=title, fill=(42, 34, 56))
    for i, (name, image) in enumerate(icons):
        x = pad + (i % cols) * (tile + pad)
        y = 120 + (i // cols) * (tile + small + 150)
        sheet.alpha_composite(rounded(image, tile), (x, y))
        d.text((x + tile // 2, y + tile + 30), f"{i + 1}. {name}", font=label, fill=(42, 34, 56), anchor="mm")
        # how it reads at home-screen size
        sheet.alpha_composite(rounded(image, small), (x + tile // 2 - small - 8, y + tile + 58))
        sheet.alpha_composite(rounded(image, 40), (x + tile // 2 + 12, y + tile + 70))
    return sheet


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    made = []
    for i, (name, draw) in enumerate(DESIGNS):
        icon = draw()
        path = os.path.join(OUT_DIR, f"{i + 1:02d} {name}.png")
        icon.save(path)
        made.append((name, Image.open(path).convert("RGBA")))
    contact_sheet(made).save(os.path.join(OUT_DIR, "All options.png"))
    print(f"Wrote {len(made)} icons and a contact sheet to {OUT_DIR}")


if __name__ == "__main__":
    main()
