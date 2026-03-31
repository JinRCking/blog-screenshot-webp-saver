import sys
from pathlib import Path

from PIL import Image, ImageOps

default_output_folder = Path.home() / "Pictures" / "ScreenshotWebpSaver"
max_size_kb = 50
start_quality = 80
min_quality = 30
quality_step = 5


def save_webp_with_limit(image: Image.Image, path: Path) -> None:
    quality = start_quality

    while True:
        image.save(
            path,
            format="WEBP",
            quality=quality,
            method=6
        )

        size_kb = path.stat().st_size / 1024

        if size_kb <= max_size_kb:
            print(f"Reached target: {size_kb:.1f} KB (quality={quality})")
            break

        quality -= quality_step

        if quality < min_quality:
            print(f"Reached minimum quality {min_quality}. Current size: {size_kb:.1f} KB")
            break

        print(f"Still above target at {size_kb:.1f} KB. Retrying with quality={quality}...")


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: python desktop_screenshot_webp.py <input-image> [output-dir]")
        return 1

    input_path = Path(sys.argv[1])
    output_folder = Path(sys.argv[2]) if len(sys.argv) >= 3 else default_output_folder

    if not input_path.exists():
        print(f"Input file not found: {input_path}")
        return 2

    output_folder.mkdir(parents=True, exist_ok=True)
    output_path = output_folder / f"{input_path.stem}_ed.webp"

    with Image.open(input_path) as img:
        img = ImageOps.exif_transpose(img)
        img = img.convert("RGB")
        save_webp_with_limit(img, output_path)

    print(f"Output file: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
