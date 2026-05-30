#!/usr/bin/env python3
"""Capture HTML animation as video using Playwright."""

import os
import subprocess
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

HTML_FILE = Path("/home/user/Salon/marketing_animation.html").resolve()
FRAMES_DIR = Path("/tmp/salon_frames")
OUTPUT = "/home/user/Salon/marketing_video.mp4"
FPS = 30
WIDTH, HEIGHT = 720, 1280

# Scene timings: (scene_id, duration_seconds, settle_seconds)
# settle = how long to wait before we start capturing (for entrance animations)
SCENES = [
    ("scene-intro",        4.5, 0.3),
    ("scene-tagline",      5.0, 0.3),
    ("scene-features",     7.0, 0.3),
    ("scene-appointments", 5.0, 0.3),
    ("scene-reports",      5.0, 0.3),
    ("scene-employees",    5.0, 0.3),
    ("scene-cta",          5.5, 0.3),
]

TOTAL_DURATION = sum(d for _, d, _ in SCENES)


def easeInOut(t):
    return t * t * (3 - 2 * t)


def apply_fade(frame_data, alpha):
    """Apply fade by darkening frame bytes."""
    if alpha >= 1.0:
        return frame_data
    from PIL import Image
    import io, numpy as np
    img = Image.open(io.BytesIO(frame_data)).convert("RGB")
    arr = np.array(img, dtype=np.float32)
    arr = (arr * alpha).clip(0, 255).astype(np.uint8)
    out = Image.fromarray(arr)
    buf = io.BytesIO()
    out.save(buf, format="PNG")
    return buf.getvalue()


def capture_frames():
    FRAMES_DIR.mkdir(parents=True, exist_ok=True)
    # Clean old frames
    for f in FRAMES_DIR.glob("*.png"):
        f.unlink()

    frame_idx = 0

    with sync_playwright() as p:
        browser = p.chromium.launch(args=[
            "--no-sandbox",
            "--disable-dev-shm-usage",
            "--disable-gpu",
        ])
        page = browser.new_page(viewport={"width": WIDTH, "height": HEIGHT})

        # Load page (use file:// URL)
        page.goto(f"file://{HTML_FILE}")
        page.wait_for_load_state("networkidle")

        # Wait a moment for fonts/initial render
        time.sleep(0.5)

        for scene_id, duration, settle in SCENES:
            print(f"  📸 {scene_id} ({duration}s)...")

            # Switch scene
            page.evaluate(f"window.showScene('{scene_id}')")
            time.sleep(settle)  # let entrance animations begin

            total_frames = int(duration * FPS)
            fade_frames = int(0.4 * FPS)  # 0.4s fade in/out

            for f in range(total_frames):
                # Calculate fade alpha
                if f < fade_frames:
                    alpha = easeInOut(f / fade_frames)
                elif f > total_frames - fade_frames:
                    alpha = easeInOut((total_frames - f) / fade_frames)
                else:
                    alpha = 1.0

                screenshot = page.screenshot(type="png")

                if alpha < 0.99:
                    screenshot = apply_fade(screenshot, alpha)

                fname = FRAMES_DIR / f"frame_{frame_idx:06d}.png"
                fname.write_bytes(screenshot)
                frame_idx += 1

        browser.close()

    print(f"  ✅ Captured {frame_idx} frames")
    return frame_idx


def get_ffmpeg():
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return "ffmpeg"


def encode_video():
    print("🎬 Encoding video with ffmpeg...")
    cmd = [
        get_ffmpeg(), "-y",
        "-framerate", str(FPS),
        "-i", str(FRAMES_DIR / "frame_%06d.png"),
        "-c:v", "libx264",
        "-preset", "fast",
        "-crf", "20",
        "-pix_fmt", "yuv420p",
        "-movflags", "+faststart",
        OUTPUT,
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print("ffmpeg error:", result.stderr[-2000:])
        raise RuntimeError("ffmpeg failed")
    size = os.path.getsize(OUTPUT) / (1024 * 1024)
    print(f"✅ Video saved: {OUTPUT} ({size:.1f} MB, ~{TOTAL_DURATION:.0f}s)")


if __name__ == "__main__":
    print(f"🎬 Starting capture ({TOTAL_DURATION:.0f}s video at {FPS}fps)...")
    n = capture_frames()
    encode_video()
