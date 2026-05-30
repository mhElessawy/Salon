#!/usr/bin/env python3
"""Marketing video generator for Salon management system."""

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import arabic_reshaper
from bidi.algorithm import get_display
from moviepy import ImageClip, concatenate_videoclips, CompositeVideoClip
import os
import math

# Video settings
WIDTH, HEIGHT = 720, 1280
FPS = 30
FONT_ARABIC = "/usr/share/fonts/truetype/noto/NotoNaskhArabic-Bold.ttf"
FONT_ARABIC_REG = "/usr/share/fonts/truetype/noto/NotoNaskhArabic-Regular.ttf"

# Color palette - luxury dark gold
BG_DARK = (10, 10, 20)
BG_CARD = (20, 20, 40)
GOLD = (212, 175, 55)
GOLD_LIGHT = (255, 215, 100)
WHITE = (255, 255, 255)
GRAY = (180, 180, 200)
ACCENT = (180, 130, 30)

def arabic(text):
    """Convert Arabic text for proper rendering."""
    reshaped = arabic_reshaper.reshape(text)
    return get_display(reshaped)

def load_font(size, bold=True):
    path = FONT_ARABIC if bold else FONT_ARABIC_REG
    return ImageFont.truetype(path, size)

def draw_gradient_bg(img, color1=BG_DARK, color2=(25, 15, 50)):
    """Draw a vertical gradient background."""
    draw = ImageDraw.Draw(img)
    for y in range(HEIGHT):
        r = int(color1[0] + (color2[0] - color1[0]) * y / HEIGHT)
        g = int(color1[1] + (color2[1] - color1[1]) * y / HEIGHT)
        b = int(color1[2] + (color2[2] - color1[2]) * y / HEIGHT)
        draw.line([(0, y), (WIDTH, y)], fill=(r, g, b))
    return img

def draw_decorative_lines(draw):
    """Draw thin gold decorative lines."""
    draw.line([(40, 0), (40, HEIGHT)], fill=(*GOLD, 40), width=1)
    draw.line([(WIDTH - 40, 0), (WIDTH - 40, HEIGHT)], fill=(*GOLD, 40), width=1)
    draw.line([(0, 80), (WIDTH, 80)], fill=(*GOLD, 30), width=1)
    draw.line([(0, HEIGHT - 80), (WIDTH, HEIGHT - 80)], fill=(*GOLD, 30), width=1)

def draw_stars(draw, seed=42):
    """Draw random star dots."""
    rng = np.random.RandomState(seed)
    for _ in range(60):
        x = rng.randint(0, WIDTH)
        y = rng.randint(0, HEIGHT)
        size = rng.randint(1, 3)
        alpha = rng.randint(80, 200)
        draw.ellipse([x - size, y - size, x + size, y + size],
                     fill=(*WHITE, alpha))

def draw_centered_text(draw, text, y, font, color=WHITE, shadow=True):
    """Draw centered text with optional shadow."""
    bbox = draw.textbbox((0, 0), text, font=font)
    w = bbox[2] - bbox[0]
    x = (WIDTH - w) // 2
    if shadow:
        draw.text((x + 3, y + 3), text, font=font, fill=(0, 0, 0, 120))
    draw.text((x, y), text, font=font, fill=color)

def draw_gold_divider(draw, y, width=200):
    """Draw a decorative gold divider."""
    cx = WIDTH // 2
    draw.line([(cx - width // 2, y), (cx + width // 2, y)], fill=GOLD, width=2)
    draw.ellipse([cx - 4, y - 4, cx + 4, y + 4], fill=GOLD)
    draw.ellipse([cx - width // 2 - 4, y - 4, cx - width // 2 + 4, y + 4],
                 fill=GOLD)
    draw.ellipse([cx + width // 2 - 4, y - 4, cx + width // 2 + 4, y + 4],
                 fill=GOLD)

def draw_feature_card(draw, text, icon, y_center, font):
    """Draw a feature card."""
    card_w, card_h = 580, 120
    x = (WIDTH - card_w) // 2
    y = y_center - card_h // 2

    # Card shadow
    for i in range(8, 0, -1):
        draw.rounded_rectangle(
            [x + i, y + i, x + card_w + i, y + card_h + i],
            radius=20, fill=(0, 0, 0, 20))

    # Card background
    draw.rounded_rectangle(
        [x, y, x + card_w, y + card_h],
        radius=20, fill=(30, 25, 60))

    # Gold left border
    draw.rounded_rectangle(
        [x, y, x + 8, y + card_h],
        radius=4, fill=GOLD)

    # Icon
    icon_font = load_font(48, bold=False)
    draw.text((x + 30, y + card_h // 2 - 30), icon, font=icon_font, fill=GOLD)

    # Text
    bbox = draw.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    draw.text((x + card_w - tw - 20, y + card_h // 2 - 22),
              text, font=font, fill=WHITE)

# ─── SCENES ───────────────────────────────────────────────────────────────────

def scene_intro(duration=3.5):
    """Opening brand slide."""
    frames = []
    total = int(duration * FPS)

    for f in range(total):
        t = f / FPS
        progress = min(t / 1.5, 1.0)
        ease = 1 - (1 - progress) ** 3

        img = Image.new("RGB", (WIDTH, HEIGHT))
        img = draw_gradient_bg(img, (5, 5, 15), (30, 10, 60))
        draw = ImageDraw.Draw(img, "RGBA")
        draw_stars(draw, seed=1)
        draw_decorative_lines(draw)

        alpha = int(ease * 255)

        # Main logo circle
        r = int(ease * 120)
        cx, cy = WIDTH // 2, HEIGHT // 2 - 100
        draw.ellipse([cx - r, cy - r, cx + r, cy + r],
                     outline=(*GOLD, alpha), width=3)
        if r > 15:
            draw.ellipse([cx - r + 10, cy - r + 10, cx + r - 10, cy + r - 10],
                         outline=(*GOLD, alpha // 2), width=1)

        # Scissors icon text
        icon_font = load_font(80, bold=False)
        icon = "✂"
        bbox = draw.textbbox((0, 0), icon, font=icon_font)
        iw = bbox[2] - bbox[0]
        draw.text((cx - iw // 2, cy - 50), icon,
                  font=icon_font, fill=(*GOLD, alpha))

        # Brand name
        font_big = load_font(60)
        name = arabic("صالون برو")
        bbox = draw.textbbox((0, 0), name, font=font_big)
        nw = bbox[2] - bbox[0]
        y_name = cy + r + 30
        draw.text((cx - nw // 2, y_name), name,
                  font=font_big, fill=(*GOLD, alpha))

        # Tagline
        font_small = load_font(32, bold=False)
        tag = arabic("نظام إدارة متكامل للصالونات")
        bbox = draw.textbbox((0, 0), tag, font=font_small)
        tw = bbox[2] - bbox[0]
        draw.text((cx - tw // 2, y_name + 70), tag,
                  font=font_small, fill=(*GRAY, alpha))

        frames.append(np.array(img))

    clip = ImageClip(np.array(frames[0]), duration=duration)
    return clip

def make_static_frame(draw_fn):
    """Helper: create a single frame image."""
    img = Image.new("RGB", (WIDTH, HEIGHT))
    img = draw_gradient_bg(img, (5, 5, 15), (30, 10, 60))
    draw = ImageDraw.Draw(img, "RGBA")
    draw_stars(draw, seed=7)
    draw_decorative_lines(draw)
    draw_fn(img, draw)
    return img

def scene_tagline(duration=4.0):
    img = make_static_frame(lambda img, draw: _draw_tagline(draw))
    return ImageClip(np.array(img), duration=duration)

def _draw_tagline(draw):
    font_xl = load_font(68)
    font_md = load_font(38, bold=False)
    font_sm = load_font(30, bold=False)

    line1 = arabic("أدِر صالونك")
    line2 = arabic("باحترافية")
    sub = arabic("كل ما تحتاجه في مكان واحد")

    draw_centered_text(draw, line1, 480, font_xl, GOLD_LIGHT)
    draw_centered_text(draw, line2, 560, font_xl, WHITE)
    draw_gold_divider(draw, 660, 250)
    draw_centered_text(draw, sub, 690, font_md, GRAY)

def scene_features(duration=6.0):
    img = make_static_frame(lambda img, draw: _draw_features(draw))
    return ImageClip(np.array(img), duration=duration)

def _draw_features(draw):
    font_title = load_font(44)
    font_feat = load_font(32)

    title = arabic("الميزات الرئيسية")
    draw_centered_text(draw, title, 160, font_title, GOLD)
    draw_gold_divider(draw, 225, 200)

    features = [
        ("📅", arabic("إدارة المواعيد بسهولة")),
        ("👥", arabic("قاعدة بيانات العملاء")),
        ("💈", arabic("إدارة الموظفين والرواتب")),
        ("📊", arabic("تقارير وإحصائيات فورية")),
        ("🛍️", arabic("إدارة المخزون والمشتريات")),
        ("💳", arabic("نقاط البيع والمدفوعات")),
    ]

    start_y = 290
    spacing = 150
    for i, (icon, text) in enumerate(features):
        draw_feature_card(draw, text, icon, start_y + i * spacing, font_feat)

def scene_appointment(duration=4.5):
    img = make_static_frame(lambda img, draw: _draw_appointment(draw))
    return ImageClip(np.array(img), duration=duration)

def _draw_appointment(draw):
    font_lg = load_font(52)
    font_md = load_font(36, bold=False)
    font_sm = load_font(28, bold=False)

    # Icon
    icon_font = load_font(120, bold=False)
    cx = WIDTH // 2
    bbox = draw.textbbox((0, 0), "📅", font=icon_font)
    iw = bbox[2] - bbox[0]
    draw.text((cx - iw // 2, 250), "📅", font=icon_font)

    title = arabic("إدارة المواعيد")
    draw_centered_text(draw, title, 420, font_lg, GOLD)
    draw_gold_divider(draw, 500, 220)

    points = [
        arabic("• حجز مواعيد فوري للعملاء"),
        arabic("• تذكير تلقائي بالمواعيد"),
        arabic("• جدولة ذكية للموظفين"),
        arabic("• إدارة الإلغاءات والتحويلات"),
    ]
    for i, p in enumerate(points):
        bbox = draw.textbbox((0, 0), p, font=font_sm)
        pw = bbox[2] - bbox[0]
        draw.text((cx - pw // 2, 540 + i * 70), p, font=font_sm, fill=GRAY)

def scene_reports(duration=4.5):
    img = make_static_frame(lambda img, draw: _draw_reports(draw))
    return ImageClip(np.array(img), duration=duration)

def _draw_reports(draw):
    font_lg = load_font(52)
    font_md = load_font(36, bold=False)
    font_sm = load_font(28, bold=False)

    icon_font = load_font(120, bold=False)
    cx = WIDTH // 2
    bbox = draw.textbbox((0, 0), "📊", font=icon_font)
    iw = bbox[2] - bbox[0]
    draw.text((cx - iw // 2, 250), "📊", font=icon_font)

    title = arabic("تقارير وإحصائيات")
    draw_centered_text(draw, title, 420, font_lg, GOLD)
    draw_gold_divider(draw, 500, 220)

    # Mini bar chart
    bar_data = [0.5, 0.7, 0.9, 0.6, 0.85, 0.75, 1.0]
    bar_w = 55
    bar_max_h = 180
    start_x = cx - (bar_w * 7 + 10 * 6) // 2
    base_y = 750
    for i, val in enumerate(bar_data):
        bx = start_x + i * (bar_w + 10)
        bh = int(val * bar_max_h)
        color = GOLD if i == 6 else (100, 80, 200)
        draw.rounded_rectangle(
            [bx, base_y - bh, bx + bar_w, base_y],
            radius=6, fill=color)

    caption = arabic("تابع أداء صالونك يومياً")
    bbox = draw.textbbox((0, 0), caption, font=font_sm)
    cw = bbox[2] - bbox[0]
    draw.text((cx - cw // 2, 780), caption, font=font_sm, fill=GRAY)

def scene_salary(duration=4.5):
    img = make_static_frame(lambda img, draw: _draw_salary(draw))
    return ImageClip(np.array(img), duration=duration)

def _draw_salary(draw):
    font_lg = load_font(52)
    font_sm = load_font(28, bold=False)
    font_num = load_font(36)

    icon_font = load_font(120, bold=False)
    cx = WIDTH // 2
    bbox = draw.textbbox((0, 0), "💰", font=icon_font)
    iw = bbox[2] - bbox[0]
    draw.text((cx - iw // 2, 250), "💰", font=icon_font)

    title = arabic("إدارة الرواتب والمكافآت")
    draw_centered_text(draw, title, 420, font_lg, GOLD)
    draw_gold_divider(draw, 500, 220)

    employees = [
        (arabic("أحمد محمد"), arabic("مصفف شعر"), "4,500"),
        (arabic("سارة علي"), arabic("خبيرة تجميل"), "3,800"),
        (arabic("محمود خالد"), arabic("حلاق"), "3,200"),
    ]

    card_w = 580
    for i, (name, role, salary) in enumerate(employees):
        y = 540 + i * 140
        x = (WIDTH - card_w) // 2
        draw.rounded_rectangle(
            [x, y, x + card_w, y + 110],
            radius=15, fill=(25, 20, 55))
        draw.rounded_rectangle(
            [x, y, x + 6, y + 110],
            radius=3, fill=GOLD)

        draw.text((x + 20, y + 12), name, font=load_font(28), fill=WHITE)
        draw.text((x + 20, y + 55), role, font=load_font(24, bold=False),
                  fill=GRAY)

        sal_text = f"{salary} ر.س"
        bbox = draw.textbbox((0, 0), sal_text, font=load_font(28))
        sw = bbox[2] - bbox[0]
        draw.text((x + card_w - sw - 20, y + 35), sal_text,
                  font=load_font(28), fill=GOLD)

def scene_cta(duration=5.0):
    img = make_static_frame(lambda img, draw: _draw_cta(draw))
    return ImageClip(np.array(img), duration=duration)

def _draw_cta(draw):
    font_xl = load_font(62)
    font_lg = load_font(44)
    font_md = load_font(34, bold=False)
    font_sm = load_font(28, bold=False)

    # Glow effect
    cx, cy = WIDTH // 2, HEIGHT // 2 - 50
    for r in range(200, 0, -20):
        alpha = int((200 - r) / 200 * 30)
        draw.ellipse([cx - r, cy - r, cx + r, cy + r],
                     fill=(*GOLD, alpha))

    title1 = arabic("ابدأ رحلة نجاحك")
    title2 = arabic("مع صالون برو")
    draw_centered_text(draw, title1, 420, font_xl, WHITE)
    draw_centered_text(draw, title2, 500, font_xl, GOLD)

    draw_gold_divider(draw, 600, 300)

    sub = arabic("نظام إدارة متكامل ومحترف")
    draw_centered_text(draw, sub, 630, font_md, GRAY)

    # Button-like element
    btn_w, btn_h = 400, 80
    bx = (WIDTH - btn_w) // 2
    by = 720
    draw.rounded_rectangle(
        [bx, by, bx + btn_w, by + btn_h],
        radius=40, fill=GOLD)
    btn_text = arabic("تواصل معنا الآن")
    bbox = draw.textbbox((0, 0), btn_text, font=font_lg)
    btw = bbox[2] - bbox[0]
    draw.text((WIDTH // 2 - btw // 2, by + 18),
              btn_text, font=font_lg, fill=BG_DARK)

    # Bottom tagline
    bottom = arabic("✨  جودة | احترافية | سهولة  ✨")
    bbox = draw.textbbox((0, 0), bottom, font=font_sm)
    bw = bbox[2] - bbox[0]
    draw.text((cx - bw // 2, HEIGHT - 120), bottom, font=font_sm, fill=GRAY)


def add_fade(clip, fade_in=0.5, fade_out=0.5):
    """Add fade in/out to a clip."""
    from moviepy.video.fx import FadeIn, FadeOut
    clip = clip.with_effects([FadeIn(fade_in), FadeOut(fade_out)])
    return clip


def main():
    print("🎬 Creating marketing video...")

    clips = [
        add_fade(scene_intro(3.5)),
        add_fade(scene_tagline(4.0)),
        add_fade(scene_features(6.0)),
        add_fade(scene_appointment(4.5)),
        add_fade(scene_reports(4.5)),
        add_fade(scene_salary(4.5)),
        add_fade(scene_cta(5.0)),
    ]

    print("✂️  Concatenating clips...")
    final = concatenate_videoclips(clips, method="compose")

    output = "/home/user/Salon/marketing_video.mp4"
    print(f"💾 Rendering to {output} ...")
    final.write_videofile(
        output,
        fps=FPS,
        codec="libx264",
        audio=False,
        preset="fast",
        ffmpeg_params=["-crf", "23", "-pix_fmt", "yuv420p"],
        logger=None,
    )
    size = os.path.getsize(output) / (1024 * 1024)
    print(f"✅ Done! File size: {size:.1f} MB")
    print(f"📁 Saved: {output}")


if __name__ == "__main__":
    main()
