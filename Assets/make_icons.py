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


# ---------- Knoepfe oben rechts: wie die Original-Knoepfe (Zahnrad, Karte ...) ----------
# Farben aus einem Screenshot des Spiels abgemessen: grauer Rahmen mit Verlauf, dunkle Kante, hellgraues Feld,
# Symbol zweifarbig grau (oben links heller) mit dunkler Kontur.
HUD_RIM_TOP = (128, 133, 137)
HUD_RIM_BOTTOM = (54, 57, 61)
HUD_EDGE = (12, 12, 12)
HUD_BEVEL = (32, 33, 34)
HUD_PANEL = (174, 175, 181)
HUD_GLYPH_LIGHT = (141, 142, 144)
HUD_GLYPH_DARK = (114, 114, 117)
HUD_GLYPH_LINE = (42, 41, 42)


def hud_button():
    """Leerer Knopf im Stil des Spiels."""
    from PIL import ImageFilter  # noqa: F401
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # Rahmen mit senkrechtem Verlauf (oben hell, unten dunkel)
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        c = tuple(int(HUD_RIM_TOP[i] + (HUD_RIM_BOTTOM[i] - HUD_RIM_TOP[i]) * t) for i in range(3)) + (255,)
        gd.line((0, y, S, y), fill=c)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((8, 8, S - 8, S - 8), 70, fill=255)
    img.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((8, 8, S - 8, S - 8), 70, outline=HUD_EDGE, width=12)
    d.rounded_rectangle((60, 60, S - 60, S - 60), 14, fill=HUD_BEVEL, outline=HUD_EDGE, width=6)
    d.rounded_rectangle((100, 100, S - 100, S - 100), 8, fill=HUD_PANEL)
    return img


def hud_glyph(img, draw_mask, details=None):
    """Symbol zweifarbig (diagonal geteilt) mit dunkler Kontur auf den Knopf zeichnen."""
    from PIL import ImageFilter
    mask = Image.new("L", (S, S), 0)
    draw_mask(ImageDraw.Draw(mask))
    outline = mask.filter(ImageFilter.MaxFilter(23))
    img.paste(Image.new("RGBA", (S, S), HUD_GLYPH_LINE + (255,)), (0, 0), outline)
    two = Image.new("RGBA", (S, S), HUD_GLYPH_DARK + (255,))
    light = Image.new("L", (S, S), 0)
    ImageDraw.Draw(light).polygon([(0, 0), (S, 0), (0, S)], fill=255)
    two.paste(Image.new("RGBA", (S, S), HUD_GLYPH_LIGHT + (255,)), (0, 0), light)
    img.paste(two, (0, 0), mask)
    if details:
        details(ImageDraw.Draw(img))
    return img


def icon_hud_roleinfo():
    """Crewmate mit Info-Abzeichen (Rollen-Info). Ohne eigenen Knopfrahmen - der kommt vom Spiel-Knopf."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    def shape(m):
        # Crewmate gross und zentriert (Koerper als Bohne, Rucksack links, Visier oben)
        m.rounded_rectangle((168, 150, 300, 350), 58, fill=255)   # Koerper
        m.rounded_rectangle((126, 200, 172, 300), 22, fill=255)   # Rucksack
        m.rounded_rectangle((206, 178, 300, 236), 24, fill=255)   # Visier (buendig mit dem Koerper)

    def details(d):
        # Visier hell
        d.rounded_rectangle((218, 188, 292, 228), 18, fill=HUD_PANEL)
        # i-Abzeichen unten rechts
        cx, cy, r = 350, 352, 62
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=HUD_GLYPH_LINE, outline=HUD_PANEL, width=12)
        d.ellipse((cx - 9, cy - 32, cx + 9, cy - 14), fill=HUD_PANEL)
        d.rounded_rectangle((cx - 9, cy - 6, cx + 9, cy + 34), 6, fill=HUD_PANEL)

    return hud_glyph(img, shape, details)


def icon_hud_settings():
    """Drei saubere Schieberegler (Rollen-Einstellungen). Ohne eigenen Knopfrahmen - der kommt vom Spiel-Knopf."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    rows = ((176, 324), (256, 198), (336, 342))  # (Hoehe, Knopf-x)

    def shape(m):
        for y, knob in rows:
            m.rounded_rectangle((136, y - 11, 376, y + 11), 11, fill=255)
            m.ellipse((knob - 33, y - 33, knob + 33, y + 33), fill=255)

    def details(d):
        for y, knob in rows:
            d.ellipse((knob - 15, y - 15, knob + 15, y + 15), fill=HUD_PANEL)

    return hud_glyph(img, shape, details)


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


def pumpkin(d, cx, cy, r, face=True):
    orange = (245, 135, 30, 255)
    dark = (205, 95, 15, 255)
    d.rounded_rectangle((cx - 18, cy - r - 55, cx + 22, cy - r + 25), 12, fill=(90, 150, 60, 255), outline=OUTLINE, width=W)
    for dx in (-0.55, 0.55):
        ellipse(d, (cx + dx * r - r * 0.62, cy - r * 0.85, cx + dx * r + r * 0.62, cy + r * 0.85), dark)
    ellipse(d, (cx - r * 0.75, cy - r * 0.9, cx + r * 0.75, cy + r * 0.9), orange)
    if face:
        eye = (255, 230, 90, 255)
        poly(d, [(cx - 95, cy - 10), (cx - 40, cy - 10), (cx - 67, cy - 65)], eye, 14)
        poly(d, [(cx + 40, cy - 10), (cx + 95, cy - 10), (cx + 67, cy - 65)], eye, 14)
        mouth = [(cx - 110, cy + 35), (cx - 60, cy + 55), (cx - 30, cy + 35), (cx, cy + 60),
                 (cx + 30, cy + 35), (cx + 60, cy + 55), (cx + 110, cy + 35), (cx, cy + 110)]
        poly(d, mouth, eye, 14)


def icon_pumpkin():
    img, d = canvas()
    pumpkin(d, S / 2, S / 2 + 35, 205)
    return img


def icon_scarecrow():
    """Vogelscheuche: Hut, Sackgesicht, ausgebreitete Arme auf einem Pfahl."""
    img, d = canvas()
    c = S / 2
    d.rectangle((c - 16, 230, c + 16, 490), fill=(130, 85, 45, 255), outline=OUTLINE, width=16)
    d.rounded_rectangle((60, 250, S - 60, 300), 20, fill=(130, 85, 45, 255), outline=OUTLINE, width=W)
    poly(d, [(c - 95, 230), (c + 95, 230), (c + 115, 400), (c - 115, 400)], (90, 130, 200, 255))
    for x in (70, S - 70):
        d.line((x, 300, x + (25 if x < c else -25), 345), fill=(240, 200, 80, 255), width=14)
    ellipse(d, (c - 95, 80, c + 95, 250), (225, 190, 130, 255))
    d.line((c - 50, 140, c - 20, 170), fill=OUTLINE, width=16)
    d.line((c - 20, 140, c - 50, 170), fill=OUTLINE, width=16)
    d.line((c + 20, 140, c + 50, 170), fill=OUTLINE, width=16)
    d.line((c + 50, 140, c + 20, 170), fill=OUTLINE, width=16)
    d.line((c - 45, 210, c + 45, 210), fill=OUTLINE, width=12)
    poly(d, [(c - 150, 105), (c + 150, 105), (c + 70, 80), (c + 40, 20), (c - 40, 20), (c - 70, 80)], (120, 80, 40, 255))
    return img


def icon_cat():
    """Schwarze Katze: Katzenkopf mit gelben Augen vor violettem Mond."""
    img, d = canvas()
    c = S / 2
    ellipse(d, (60, 60, S - 60, S - 60), (120, 80, 170, 255))
    black = (35, 32, 45, 255)
    poly(d, [(c - 150, 230), (c - 135, 95), (c - 50, 170)], black)
    poly(d, [(c + 150, 230), (c + 135, 95), (c + 50, 170)], black)
    ellipse(d, (c - 160, 150, c + 160, 420), black)
    for sx in (-1, 1):
        ex = c + sx * 65
        d.ellipse((ex - 40, 240, ex + 40, 300), fill=(250, 215, 60, 255), outline=OUTLINE, width=10)
        d.ellipse((ex - 8, 244, ex + 8, 296), fill=OUTLINE)
    poly(d, [(c - 18, 330), (c + 18, 330), (c, 350)], (240, 130, 160, 255), 8)
    for sy in (340, 365):
        d.line((c - 60, sy, c - 175, sy - 15 + (sy - 340)), fill=(200, 200, 210, 255), width=6)
        d.line((c + 60, sy, c + 175, sy - 15 + (sy - 340)), fill=(200, 200, 210, 255), width=6)
    return img


def leaf(d, cx, cy, size, angle, fill):
    """Spitzes Herbstblatt (Linsenform) mit Mittelrippe."""
    pts = []
    for i in range(0, 21):
        t = -1 + i / 10
        pts.append((t * size, (1 - t * t) * size * 0.5))
    for i in range(20, -1, -1):
        t = -1 + i / 10
        pts.append((t * size, -(1 - t * t) * size * 0.5))
    ca, sa = math.cos(angle), math.sin(angle)
    pts = [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]
    poly(d, pts, fill, 12)
    d.line((cx - ca * size * 0.8, cy - sa * size * 0.8, cx + ca * size * 0.8, cy + sa * size * 0.8), fill=OUTLINE, width=7)


def icon_leaves():
    """Laubhaufen: Erdhuegel mit vielen bunten Herbstblaettern."""
    img, d = canvas()
    d.chord((40, 250, S - 40, 560), 180, 360, fill=(150, 85, 40, 255), outline=OUTLINE, width=W)
    cols = [(205, 60, 30, 255), (240, 150, 40, 255), (250, 205, 60, 255), (225, 105, 35, 255), (175, 50, 40, 255)]
    spots = [(120, 380, 0.4), (390, 385, -0.5), (255, 360, 0.0), (175, 310, -0.9), (335, 305, 0.8),
             (255, 255, 0.25), (210, 400, 1.3), (300, 400, -1.2), (90, 330, -0.3), (420, 330, 0.3), (255, 175, -0.6)]
    for i, (x, y, a) in enumerate(spots):
        leaf(d, x, y, 85, a, cols[i % len(cols)])
    return img


def icon_cursed():
    """Fluch-Zeichen hinter dem Namen: violetter Kreis mit Totenkopf."""
    img, d = canvas()
    c = S / 2
    ellipse(d, (40, 40, S - 40, S - 40), (140, 70, 190, 255), 30)
    bone = (245, 240, 250, 255)
    d.ellipse((c - 130, 105, c + 130, 345), fill=bone, outline=OUTLINE, width=18)
    d.rounded_rectangle((c - 80, 290, c + 80, 410), 30, fill=bone, outline=OUTLINE, width=18)
    d.ellipse((c - 95, 200, c - 25, 275), fill=(60, 20, 80, 255))
    d.ellipse((c + 25, 200, c + 95, 275), fill=(60, 20, 80, 255))
    poly(d, [(c - 18, 320), (c + 18, 320), (c, 285)], (60, 20, 80, 255), 8)
    for x in (c - 40, c, c + 40):
        d.line((x, 360, x, 405), fill=OUTLINE, width=12)
    return img


def icon_frost():
    """Rachegeist: eisblaue Schneeflocke."""
    img, d = canvas()
    c = S / 2
    ice = (150, 220, 255, 255)
    for k in range(6):
        a = math.radians(k * 60 + 90)
        ex, ey = c + math.cos(a) * 200, c - math.sin(a) * 200
        d.line((c, c, ex, ey), fill=OUTLINE, width=62)
        for t, l in ((0.55, 70), (0.8, 50)):
            bx, by = c + math.cos(a) * 200 * t, c - math.sin(a) * 200 * t
            for side in (-1, 1):
                b = a + side * math.radians(45)
                d.line((bx, by, bx + math.cos(b) * l, by - math.sin(b) * l), fill=OUTLINE, width=44)
    for k in range(6):
        a = math.radians(k * 60 + 90)
        ex, ey = c + math.cos(a) * 200, c - math.sin(a) * 200
        d.line((c, c, ex, ey), fill=ice, width=26)
        for t, l in ((0.55, 70), (0.8, 50)):
            bx, by = c + math.cos(a) * 200 * t, c - math.sin(a) * 200 * t
            for side in (-1, 1):
                b = a + side * math.radians(45)
                d.line((bx, by, bx + math.cos(b) * l, by - math.sin(b) * l), fill=ice, width=16)
    ellipse(d, (c - 45, c - 45, c + 45, c + 45), (230, 248, 255, 255), 16)
    return img


def icon_boards():
    """Waechtergeist: Vent-Gitter mit zwei gekreuzten Brettern."""
    img, d = canvas()
    c = S / 2
    d.rounded_rectangle((70, 130, S - 70, S - 130), 30, fill=(120, 130, 140, 255), outline=OUTLINE, width=W)
    for y in range(175, S - 160, 38):
        d.line((110, y, S - 110, y), fill=(70, 75, 85, 255), width=16)
    wood = (190, 135, 75, 255)
    for ang in (-25, 25):
        a = math.radians(ang)
        dx, dy = math.cos(a) * 230, math.sin(a) * 230
        nx, ny = -math.sin(a) * 38, math.cos(a) * 38
        pts = [(c - dx - nx, c - dy - ny), (c + dx - nx, c + dy - ny), (c + dx + nx, c + dy + ny), (c - dx + nx, c - dy + ny)]
        poly(d, pts, wood, 18)
        for t in (-0.75, 0.75):
            d.ellipse((c + dx * t - 10, c + dy * t - 10, c + dx * t + 10, c + dy * t + 10), fill=OUTLINE)
    return img


def icon_radar():
    """Radar: Schirm mit Ringen, Strahl und Punkt."""
    img, d = canvas()
    c = S / 2
    ellipse(d, (50, 50, S - 50, S - 50), (40, 90, 60, 255), 24)
    for r in (150, 95):
        d.ellipse((c - r, c - r, c + r, c + r), outline=(110, 220, 140, 255), width=10)
    d.pieslice((60, 60, S - 60, S - 60), 300, 345, fill=(140, 255, 170, 200))
    d.line((c, c, c + 150, c - 110), fill=(160, 255, 180, 255), width=12)
    ellipse(d, (c + 60, c - 120, c + 110, c - 70), (255, 240, 120, 255), 12)
    ellipse(d, (c - 16, c - 16, c + 16, c + 16), (160, 255, 180, 255), 8)
    return img


def icon_eat():
    """Zombie: abgenagter Knochen."""
    img, d = canvas()
    c = S / 2
    bone = (240, 232, 210, 255)
    ang = math.radians(-35)
    dx, dy = math.cos(ang) * 150, math.sin(ang) * 150
    nx, ny = -math.sin(ang) * 42, math.cos(ang) * 42
    shaft = [(c - dx - nx, c - dy - ny), (c + dx - nx, c + dy - ny), (c + dx + nx, c + dy + ny), (c - dx + nx, c - dy + ny)]
    for sx in (-1, 1):
        ex, ey = c + sx * dx, c + sx * dy
        for k in (-1, 1):
            ox, oy = ex + k * nx * 1.1 + sx * math.cos(ang) * 25, ey + k * ny * 1.1 + sx * math.sin(ang) * 25
            ellipse(d, (ox - 62, oy - 62, ox + 62, oy + 62), bone, 18)
    poly(d, shaft, bone, 18)
    # Bissspuren (gruen)
    for t in (-0.25, 0.15):
        bx, by = c + dx * t + nx, c + dy * t + ny
        d.ellipse((bx - 30, by - 30, bx + 30, by + 30), fill=(110, 170, 60, 255), outline=OUTLINE, width=12)
    return img


def icon_vent():
    """Vent-Gitter (Jester)."""
    img, d = canvas()
    d.rounded_rectangle((60, 120, S - 60, S - 120), 34, fill=(130, 140, 150, 255), outline=OUTLINE, width=W)
    for y in range(165, S - 150, 40):
        d.rounded_rectangle((105, y, S - 105, y + 18), 9, fill=(55, 60, 70, 255))
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
    "pumpkin": icon_pumpkin,
    "scarecrow": icon_scarecrow,
    "cat": icon_cat,
    "leaves": icon_leaves,
    "cursed": icon_cursed,
    "frost": icon_frost,
    "boards": icon_boards,
    "radar": icon_radar,
    "eat": icon_eat,
    "vent": icon_vent,
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
