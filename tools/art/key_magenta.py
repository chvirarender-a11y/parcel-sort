#!/usr/bin/env python3
"""Turns AI-generated art on a pure magenta (#FF00FF) background into a trimmed,
transparent PNG.

usage: key_magenta.py INPUT OUTPUT [--max-size 512] [--normalize]

Alpha comes from how magenta a pixel is (min(R, B) - G); edge pixels are
de-spilled so no pink fringe remains. --normalize stretches a grayscale asset so its
brightest pixel is white, which keeps colors vivid when the game tints it.
Needs Pillow and NumPy.
"""
import argparse

import numpy as np
from PIL import Image

# min(R, B) - G at or below SOLID is fully opaque, at or above CLEAR fully transparent.
SOLID, CLEAR = 40, 200


def key(img):
    rgb = np.asarray(img.convert("RGB")).astype(np.float32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    magenta = np.minimum(r, b) - g
    alpha = 1.0 - np.clip((magenta - SOLID) / (CLEAR - SOLID), 0.0, 1.0)

    # Remove the magenta cast from what is left: pull R and B down toward G.
    spill = np.clip(magenta, 0, None)
    out = rgb.copy()
    out[..., 0] = r - spill
    out[..., 2] = b - spill
    rgba = np.dstack([np.clip(out, 0, 255), alpha * 255]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def normalize(img):
    a = np.asarray(img).astype(np.float32)
    opaque = a[..., 3] > 200
    a[..., :3] *= 255.0 / a[..., :3][opaque].max()
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def trim(img, margin=0.03):
    alpha = np.asarray(img)[..., 3]
    ys, xs = np.nonzero(alpha > 8)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    img = img.crop(box)
    pad = int(max(img.size) * margin)
    canvas = Image.new("RGBA", (img.width + 2 * pad, img.height + 2 * pad), (0, 0, 0, 0))
    canvas.paste(img, (pad, pad))
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("input")
    parser.add_argument("output")
    parser.add_argument("--max-size", type=int, default=512, help="longest side in pixels")
    parser.add_argument("--normalize", action="store_true", help="brighten a grayscale asset meant for tinting")
    args = parser.parse_args()

    img = trim(key(Image.open(args.input)))
    if args.normalize:
        img = normalize(img)
    scale = args.max_size / max(img.size)
    if scale < 1:
        img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    img.save(args.output, optimize=True)
    print(f"{args.output}: {img.width}x{img.height}")


if __name__ == "__main__":
    main()
