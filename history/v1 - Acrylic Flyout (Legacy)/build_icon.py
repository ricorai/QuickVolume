"""
Generates the differentiated Quick Volume icon in multiple resolutions.
Visual style:
- Same recognizable speaker + sound wave structure as the Windows taskbar audio icon.
- Differentiated with vivid Electric Cyan accent (#00E5FF) and dual waves (signifying the 2 volume presets: 25% & 100%).
- Multi-resolution ICO (16x16 up to 256x256) for crisp rendering on all Windows display scaling (100%, 125%, 150%, 200%).
"""

import os
from PIL import Image, ImageDraw

def create_highres_icon():
    canvas = 1024
    s = canvas / 256.0

    img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Palette
    COLOR_WHITE = (255, 255, 255, 255)
    COLOR_CYAN = (0, 229, 255, 255)       # Electric Cyan #00E5FF
    COLOR_CYAN_GLOW = (0, 200, 255, 180)

    stroke_width = int(16 * s)

    # 1. Speaker Body (Classic geometric outline)
    # Rectangle part (left): x from 28 to 72, y from 98 to 158
    # Flare part (right): from (72, 98) up to (132, 54), down to (132, 202), back to (72, 158)
    points = [
        (28 * s, 98 * s),
        (72 * s, 98 * s),
        (134 * s, 52 * s),
        (134 * s, 204 * s),
        (72 * s, 158 * s),
        (28 * s, 158 * s),
    ]

    # Draw speaker outline in crisp white
    draw.polygon(points, outline=COLOR_WHITE, width=stroke_width)

    # 2. Sound Waves (Dual Cyan arcs: Wave 1 for 25%, Wave 2 for 100%)
    center_x = 84 * s
    center_y = 128 * s

    def draw_arc(radius, width, color, start_angle=-45, end_angle=45):
        bbox = [
            center_x - radius,
            center_y - radius,
            center_x + radius,
            center_y + radius,
        ]
        draw.arc(bbox, start=start_angle, end=end_angle, fill=color, width=width)

    # Inner Arc (representing 25% preset) - Cyan
    draw_arc(82 * s, int(16 * s), COLOR_CYAN, start_angle=-38, end_angle=38)

    # Outer Arc (representing 100% preset) - Cyan
    draw_arc(126 * s, int(16 * s), COLOR_CYAN, start_angle=-48, end_angle=48)

    return img

def export_icons(output_dir):
    os.makedirs(output_dir, exist_ok=True)
    highres = create_highres_icon()

    # Save high-res PNG
    png_path = os.path.join(output_dir, "QuickVolume.png")
    highres.resize((256, 256), Image.Resampling.LANCZOS).save(png_path, "PNG")

    # Generate multi-resolution ICO
    ico_path = os.path.join(output_dir, "QuickVolume.ico")
    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    
    # Pre-render each size with Lanczos for maximum sharpness
    icon_images = [highres.resize(size, Image.Resampling.LANCZOS) for size in sizes]
    
    # Save as ICO
    icon_images[-1].save(
        ico_path,
        format="ICO",
        sizes=sizes,
        append_images=icon_images[:-1]
    )
    print(f"Exported icon to:\n  - {png_path}\n  - {ico_path}")

if __name__ == "__main__":
    out = os.path.dirname(os.path.abspath(__file__))
    export_icons(out)
