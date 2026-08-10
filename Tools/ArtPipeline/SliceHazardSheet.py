#!/usr/bin/env python3
"""Split a transparent 3x2 ImageGen hazard sheet into replaceable PNG modules."""

import argparse
from pathlib import Path
from PIL import Image


NAMES = ("straight", "corner", "end", "tee", "isolated", "bridge")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--prefix", default=None)
    args = parser.parse_args()

    image = Image.open(args.source).convert("RGBA")
    cell_width = image.width // 3
    cell_height = image.height // 2
    args.output.mkdir(parents=True, exist_ok=True)
    for index, name in enumerate(NAMES):
        column, row = index % 3, index // 3
        cell = image.crop((column * cell_width, row * cell_height,
                           (column + 1) * cell_width, (row + 1) * cell_height))
        alpha_box = cell.getchannel("A").getbbox()
        if alpha_box is None:
            raise RuntimeError(f"empty module: {name}")
        asset = cell.crop(alpha_box)
        canvas = Image.new("RGBA", (512, 512))
        asset.thumbnail((500, 500), Image.Resampling.LANCZOS)
        canvas.alpha_composite(asset, ((512 - asset.width) // 2, (512 - asset.height) // 2))
        filename = f"{args.prefix}-{index:02}.png" if args.prefix else f"{name}-01.png"
        canvas.save(args.output / filename, optimize=True)


if __name__ == "__main__":
    main()
