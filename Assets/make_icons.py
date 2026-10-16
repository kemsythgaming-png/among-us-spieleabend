"""Zeichnet die Button-Symbole des Mods (flach, bunt, dunkle Kontur - wie die Buttons im Spiel).
Aufruf:  python Assets/make_icons.py      -> schreibt Assets/Icons/*.png (128x128) und Assets/icons_preview.png
Wird in die DLL eingebettet (siehe AmongUsSpieleabend.csproj)."""
import math
import pathlib

from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).parent
OUT = HERE / "Icons"
S = 512          # Zeichengroesse (4x), wird auf 128 verkleinert
OUTLINE = (25, 22, 30, 255)
W = 22           # Konturstaerke bei 512 px


def canvas():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def poly(d, pts, fill, width=W):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=OUTLINE, width=width, joint="curve")
    for x, y in pts:
        d.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill=OUTLINE)


def ellipse(d, box, fill, width=W):
    d.ellipse(box, fill=fill, outline=OUTLINE, width=width)


def shield_points(cx, top, w, h):
    pts = []
    for i in range(0, 181, 6):  # gerundete Unterseite
        a = math.radians(i)
        pts.append((cx + math.cos(a) * w / 2, top + h * 0.45 + math.sin(a) * h * 0.55))
    pts.append((cx - w / 2, top + h * 0.12))
    pts.append((cx - w * 0.25, top))
    pts.append((cx, top + h * 0.06))
    pts.append((cx + w * 0.25, top))
    pts.append((cx + w / 2, top + h * 0.12))
    return pts


def crewmate(d, cx, cy, scale, fill):
    s = scale
    d.rounded_rectangle((cx - 70 * s, cy - 40 * s, cx - 30 * s, cy + 50 * s), 18 * s, fill=fill, outline=OUTLINE, width=int(W * s))
    d.rounded_rectangle((cx - 45 * s, cy - 90 * s, cx + 65 * s, cy + 95 * s), 55 * s, fill=fill, outline=OUTLINE, width=int(W * s))
    d.rounded_rectangle((cx + 5 * s, cy - 55 * s, cx + 90 * s, cy - 5 * s), 25 * s, fill=(150, 205, 235, 255), outline=OUTLINE, width=int(W * s))


def icon_shoot():
    img, d = canvas()
    c = S / 2
    gold = (248, 205, 70, 255)
    ellipse(d, (70, 70, S - 70, S - 70), (0, 0, 0, 0), 60)
    d.ellipse((100, 100, S - 100, S - 100), outline=gold, width=34)
    for a, b in (((c, 30), (c, 175)), ((c, S - 175), (c, S - 30)), ((30, c), (175, c)), ((S - 175, c), (S - 30, c))):
        d.line((a, b), fill=OUTLINE, width=62)
        d.line((a, b), fill=gold, width=30)
    ellipse(d, (c - 42, c - 42, c + 42, c + 42), (230, 60, 50, 255))
    return img


def icon_shield():
    img, d = canvas()
    poly(d, shield_points(S / 2, 50, 380, 420), (90, 220, 160, 255))
    c = S / 2
    d.rounded_rectangle((c - 34, 150, c + 34, 370), 14, fill=(255, 255, 255, 255), outline=OUTLINE, width=14)
    d.rounded_rectangle((c - 110, 226, c + 110, 294), 14, fill=(255, 255, 255, 255), outline=OUTLINE, width=14)
    d.rectangle((c - 22, 238, c + 22, 282), fill=(255, 255, 255, 255))
    return img


def icon_guard():
    img, d = canvas()
    poly(d, shield_points(S / 2, 50, 380, 420), (93, 129, 217, 255))
    crewmate(d, S / 2 - 15, 245, 1.45, (255, 255, 255, 255))
    return img


def icon_check():
    img, d = canvas()
    c = S / 2
    pts = []
    for i in range(0, 361, 5):
        a = math.radians(i)
        pts.append((c + math.cos(a) * 220, c + math.sin(a) * 120 * (1 if math.sin(a) >= 0 else 1)))
    eye = []
    for i in range(0, 181, 5):  # obere Lidkante
        t = i / 180
        eye.append((40 + t * (S - 80), c - math.sin(t * math.pi) * 150))
    for i in range(180, -1, -5):  # untere Lidkante
        t = i / 180
        eye.append((40 + t * (S - 80), c + math.sin(t * math.pi) * 150))
    poly(d, eye, (250, 250, 250, 255))
    ellipse(d, (c - 100, c - 100, c + 100, c + 100), (97, 178, 108, 255))
    ellipse(d, (c - 45, c - 45, c + 45, c + 45), (20, 20, 25, 255), 1)
    d.ellipse((c - 70, c - 75, c - 30, c - 35), fill=(255, 255, 255, 230))
    return img


def icon_camo():
    img, d = canvas()
    crewmate(d, 170, 300, 1.45, (110, 110, 118, 255))
    crewmate(d, 330, 320, 1.45, (170, 170, 178, 255))
    # grosses Fragezeichen
    d.arc((300, 10, 470, 160), 200, 90, fill=OUTLINE, width=54)
    d.arc((300, 10, 470, 160), 200, 90, fill=(255, 255, 255, 255), width=24)
    d.ellipse((364, 170, 408, 214), fill=(255, 255, 255, 255), outline=OUTLINE, width=12)
    return img


def icon_bite():
    img, d = canvas()
    c = S / 2
    mouth = []
    for i in range(0, 361, 5):
        a = math.radians(i)
        mouth.append((c + math.cos(a) * 200, c + 10 + math.sin(a) * (110 if math.sin(a) > 0 else 70)))
    poly(d, mouth, (150, 0, 30, 255))
    for x in (c - 95, c + 95):
        poly(d, [(x - 45, c - 40), (x + 45, c - 40), (x, c + 110)], (255, 255, 255, 255), 16)
    # Blutstropfen
    drop = [(c + 160, 300)]
    for i in range(-60, 241, 10):
        a = math.radians(i)
        drop.append((c + 160 + math.cos(a) * 38, 390 + math.sin(a) * 38))
    poly(d, drop, (200, 0, 20, 255), 14)
    return img


def icon_curse():
    img, d = canvas()
    # Hexenhut
    poly(d, [(60, 400), (452, 400), (420, 440), (92, 440)], (90, 40, 120, 255))
    poly(d, [(130, 400), (382, 400), (300, 80), (360, 40), (250, 70)], (140, 70, 190, 255))
    d.rectangle((150, 340, 362, 385), fill=(120, 220, 80, 255), outline=OUTLINE, width=14)
    # Funkelstern
    cx, cy = 400, 150
    star = []
    for i in range(8):
        r = 70 if i % 2 == 0 else 22
        a = math.radians(i * 45 - 90)
        star.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    poly(d, star, (255, 235, 120, 255), 12)
    return img


def icon_infect():
    img, d = canvas()
    c = S / 2
    ellipse(d, (50, 50, S - 50, S - 50), (110, 170, 60, 255))
    for k, off in enumerate((-110, 0, 110)):
        x0, y0, x1, y1 = c + off - 45, 150, c + off + 30, 360
        d.line((x0, y0, x1, y1), fill=OUTLINE, width=70)
        d.line((x0, y0, x1, y1), fill=(40, 70, 20, 255), width=40)
    # Tropfen
    d.ellipse((330, 380, 380, 430), fill=(180, 240, 90, 255), outline=OUTLINE, width=12)
    return img


def icon_stake():
    img, d = canvas()
    # Pflock diagonal: Spitze unten rechts
    pts = [(120, 60), (190, 60), (420, 380), (450, 470), (370, 420), (110, 120)]
    poly(d, [(95, 95), (160, 40), (430, 360), (470, 470), (360, 430)], (175, 120, 65, 255))
    d.line((150, 110, 400, 400), fill=(130, 85, 40, 255), width=16)
    # kleines Kreuz
    d.rounded_rectangle((330, 50, 370, 210), 8, fill=(230, 230, 240, 255), outline=OUTLINE, width=14)
    d.rounded_rectangle((290, 95, 410, 135), 8, fill=(230, 230, 240, 255), outline=OUTLINE, width=14)
    d.rectangle((342, 107, 358, 123), fill=(230, 230, 240, 255))
    return img


def icon_douse():
    img, d = canvas()
    # Benzinkanister
    d.rounded_rectangle((110, 130, 400, 460), 40, fill=(210, 40, 40, 255), outline=OUTLINE, width=W)
    poly(d, [(330, 130), (330, 70), (400, 50), (440, 110), (400, 130)], (90, 90, 95, 255), 18)
    d.rounded_rectangle((150, 70, 300, 125), 20, fill=(150, 25, 25, 255), outline=OUTLINE, width=16)
    d.line((160, 190, 350, 400), fill=(255, 150, 140, 255), width=24)
    d.line((350, 190, 160, 400), fill=(255, 150, 140, 255), width=24)
    return img


def icon_ignite():
    img, d = canvas()
    c = S / 2

    def flame(cx, base, w, h, fill, width):
        pts = []
        for i in range(0, 181, 6):  # runder Boden
            a = math.radians(i)
            pts.append((cx + math.cos(a) * w / 2, base - w / 2 + math.sin(a) * w / 2))
        pts.append((cx - w / 2, base - w / 2))
        pts.append((cx - w * 0.35, base - h * 0.7))
        pts.append((cx - w * 0.05, base - h * 0.55))
        pts.append((cx, base - h))
        pts.append((cx + w * 0.3, base - h * 0.6))
        pts.append((cx + w / 2, base - w / 2))
        poly(d, pts, fill, width)

    flame(c, 470, 300, 430, (238, 90, 30, 255), W)
    flame(c, 450, 180, 280, (255, 170, 40, 255), 0 + 1)
    flame(c, 440, 90, 150, (255, 240, 150, 255), 1)
    return img


def icon_phase():
    img, d = canvas()
    # Mauer links (Ziegel), Geist schwebt hindurch
    for row in range(5):
        y0 = 70 + row * 76
        off = 0 if row % 2 == 0 else -60
        for col in range(3):
            x0 = 40 + off + col * 120
            left, right = max(30, x0), min(250, x0 + 110)
            if right - left < 30:
                continue
            d.rounded_rectangle((left, y0, right, y0 + 66), 8, fill=(160, 90, 70, 255), outline=OUTLINE, width=12)
    ghost = []
    for i in range(180, 361, 6):  # Kopf
        a = math.radians(i)
        ghost.append((330 + math.cos(a) * 120, 220 + math.sin(a) * 120))
    for k in range(0, 7):          # gewellter Saum
        x = 450 - k * 40
        ghost.append((x, 430 if k % 2 == 0 else 390))
    poly(d, ghost, (225, 245, 255, 235))
    ellipse(d, (275, 175, 315, 235), OUTLINE, 1)
    ellipse(d, (350, 175, 390, 235), OUTLINE, 1)
    return img


def icon_guess():
    img, d = canvas()
    ellipse(d, (40, 40, S - 40, S - 40), (60, 200, 230, 255))
    d.ellipse((105, 105, S - 105, S - 105), outline=OUTLINE, width=22)
    font = ImageFont.truetype("arialbd.ttf", 300)
    d.text((S / 2, S / 2 + 8), "?", font=font, anchor="mm", fill=(255, 255, 255, 255), stroke_width=20, stroke_fill=OUTLINE)
    return img


def hud_frame(d):
    """Rahmen wie die runden Knoepfe oben rechts im Spiel (dunkler Rand, grauer Innenteil)."""
    d.rounded_rectangle((16, 16, S - 16, S - 16), 70, fill=(48, 50, 56, 255), outline=(15, 15, 18, 255), width=26)
    d.rounded_rectangle((60, 60, S - 60, S - 60), 46, fill=(205, 210, 218, 255), outline=(15, 15, 18, 255), width=16)


def icon_hud_roleinfo():
    img, d = canvas()
    hud_frame(d)
    # Rollenkarte mit Crewmate und "i"
    d.rounded_rectangle((120, 110, 330, 400), 22, fill=(250, 250, 250, 255), outline=OUTLINE, width=16)
    crewmate(d, 225, 230, 0.85, (230, 70, 80, 255))
    d.rectangle((150, 330, 300, 350), fill=(120, 120, 130, 255))
    d.rectangle((150, 365, 260, 382), fill=(120, 120, 130, 255))
    ellipse(d, (300, 260, 420, 380), (60, 150, 230, 255), 16)
    d.rectangle((351, 300, 369, 355), fill=(255, 255, 255, 255))
    d.ellipse((350, 275, 370, 295), fill=(255, 255, 255, 255))
    return img


def icon_hud_settings():
    img, d = canvas()
    hud_frame(d)
    # Regler (Schieberegler) - "Rollen-Einstellungen"
    for k, (y, knob) in enumerate(((170, 300), (256, 200), (342, 340))):
        d.rounded_rectangle((120, y - 14, 392, y + 14), 14, fill=(90, 95, 105, 255), outline=OUTLINE, width=10)
        ellipse(d, (knob - 34, y - 34, knob + 34, y + 34), (255, 120, 200, 255) if k == 1 else (250, 250, 250, 255), 14)
    return img


def icon_candy():
    """Bonbon im Among-Us-Stil: runder Gummi-Crewmate in Bonbonpapier mit dicker Kontur."""
    img, d = canvas()
    c = S / 2
    # Papier links und rechts
    for side in (-1, 1):
        base = c + side * 105
        tip = c + side * 225
        wrapper = [(base, c - 35), (tip, c - 95), (tip - side * 22, c), (tip, c + 95), (base, c + 35)]
        poly(d, wrapper, (255, 205, 70, 255), 18)
        d.line((base + side * 25, c - 12, tip - side * 35, c - 45), fill=(225, 155, 35, 255), width=10)
        d.line((base + side * 25, c + 12, tip - side * 35, c + 45), fill=(225, 155, 35, 255), width=10)
    pink = (245, 80, 135, 255)
    # Rucksack
    d.rounded_rectangle((c - 150, c - 45, c - 85, c + 70), 26, fill=pink, outline=OUTLINE, width=W)
    # runder Koerper
    d.rounded_rectangle((c - 110, c - 125, c + 105, c + 135), 100, fill=pink, outline=OUTLINE, width=W)
    # Visier
    d.rounded_rectangle((c - 25, c - 80, c + 125, c - 5), 36, fill=(160, 215, 240, 255), outline=OUTLINE, width=W)
    d.rounded_rectangle((c + 10, c - 65, c + 70, c - 45), 10, fill=(235, 250, 255, 255))
    # Glanz auf dem Gummi
    d.ellipse((c - 85, c + 20, c - 50, c + 80), fill=(255, 170, 200, 255))
    return img


ICONS = {
    "candy": icon_candy,
    "phase": icon_phase,
    "guess": icon_guess,
    "hud_roleinfo": icon_hud_roleinfo,
    "hud_settings": icon_hud_settings,
    "shoot": icon_shoot,
    "shield": icon_shield,
    "guard": icon_guard,
    "check": icon_check,
    "camo": icon_camo,
    "bite": icon_bite,
    "curse": icon_curse,
    "infect": icon_infect,
    "stake": icon_stake,
    "douse": icon_douse,
    "ignite": icon_ignite,
}

if __name__ == "__main__":
    OUT.mkdir(exist_ok=True)
    tiles = []
    for name, fn in ICONS.items():
        img = fn().resize((128, 128), Image.LANCZOS)
        img.save(OUT / f"{name}.png")
        tiles.append((name, img))
    per_row = 8
    rows = (len(tiles) + per_row - 1) // per_row
    sheet = Image.new("RGBA", (per_row * 140 + 10, rows * 170), (60, 64, 80, 255))
    sd = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(tiles):
        x, y = (i % per_row) * 140 + 10, (i // per_row) * 170
        sheet.alpha_composite(img, (x, y + 10))
        sd.text((x + 30, y + 145), name, fill="white")
    sheet.resize((sheet.width * 2 // 2, sheet.height)).save(HERE / "icons_preview.png")
    print("ok", len(tiles))
