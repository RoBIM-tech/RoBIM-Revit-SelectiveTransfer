"""Generates 32x32 light and dark PNG ribbon icons for RoBIM Selective Transfer."""
from pathlib import Path
from PIL import Image, ImageDraw

def generate_icons(output_dir):
    out = Path(output_dir)
    out.mkdir(parents=True, exist_ok=True)

    for suffix, dark in [('', False), ('.dark', True)]:
        size = 128
        im = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)

        if not dark:
            line_color = '#247A71'      # Brand Teal
            arrow_color = '#17544E'     # Deep accent
            check_color = '#17544E'
            sub_line = '#3B9E93'
        else:
            line_color = '#5CD6C6'      # Bright mint for dark background
            arrow_color = '#8AEDE0'
            check_color = '#8AEDE0'
            sub_line = '#6AE3D3'

        # Document 1: Source Document (Left)
        d.rounded_rectangle([10, 14, 52, 114], radius=8, outline=line_color, width=9)
        d.line([(20, 36), (42, 36)], fill=sub_line, width=7)
        # Checkmark on item 2 (transferred item)
        d.line([(18, 64), (25, 71), (38, 56)], fill=check_color, width=7)
        d.line([(20, 92), (42, 92)], fill=sub_line, width=7)

        # Document 2: Target Document (Right)
        d.rounded_rectangle([76, 14, 118, 114], radius=8, outline=line_color, width=9)
        d.line([(86, 36), (108, 36)], fill=sub_line, width=7)
        d.line([(86, 92), (108, 92)], fill=sub_line, width=7)

        # Transfer Arrow spanning from left document to right document
        d.line([(42, 64), (88, 64)], fill=arrow_color, width=13)
        d.polygon([(82, 44), (108, 64), (82, 84)], fill=arrow_color)

        icon32 = im.resize((32, 32), Image.Resampling.LANCZOS)
        out_path = out / f'icon{suffix}.png'
        icon32.save(out_path)
        print(f'Generated: {out_path}')

if __name__ == '__main__':
    pkg = Path(__file__).resolve().parents[1] / 'package'
    generate_icons(pkg)
