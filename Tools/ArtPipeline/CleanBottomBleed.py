#!/usr/bin/env python3
"""Remove a neighbouring sheet cell that bled below an otherwise isolated sprite."""

import argparse
from pathlib import Path
from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("image", type=Path)
    parser.add_argument("--from-row", type=int, required=True)
    args = parser.parse_args()
    image = Image.open(args.image).convert("RGBA")
    pixels = image.load()
    for y in range(args.from_row, image.height):
        for x in range(image.width):
            pixels[x, y] = (0, 0, 0, 0)
    image.save(args.image, optimize=True)


if __name__ == "__main__":
    main()
