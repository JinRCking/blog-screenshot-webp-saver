import sys
from pathlib import Path

from PIL import Image, ImageOps


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: convert_to_webp.py <input> <output>", file=sys.stderr)
        return 2

    input_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])

    output_path.parent.mkdir(parents=True, exist_ok=True)

    with Image.open(input_path) as img:
        img = ImageOps.exif_transpose(img)
        if img.mode not in ("RGB", "RGBA"):
            img = img.convert("RGBA")

        img.save(
            output_path,
            format="WEBP",
            quality=88,
            method=6
        )

    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
