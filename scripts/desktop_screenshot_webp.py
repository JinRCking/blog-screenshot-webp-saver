import sys
from pathlib import Path

from PIL import Image, ImageOps

default_output_folder = Path(r"D:\1A-blog-webp-jietu\April")
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
            print(f"达到目标: {size_kb:.1f} KB (quality={quality})")
            break

        quality -= quality_step

        if quality < min_quality:
            print(f"已到最低质量 {min_quality}，当前大小: {size_kb:.1f} KB")
            break

        print(f"超过目标 {size_kb:.1f} KB，降低质量到 {quality} 后重新压缩...")


def main() -> int:
    if len(sys.argv) < 2:
        print("用法: python desktop_screenshot_webp.py <输入图片路径> [输出目录]")
        return 1

    input_path = Path(sys.argv[1])
    output_folder = Path(sys.argv[2]) if len(sys.argv) >= 3 else default_output_folder

    if not input_path.exists():
        print(f"找不到输入文件: {input_path}")
        return 2

    output_folder.mkdir(parents=True, exist_ok=True)
    output_path = output_folder / f"{input_path.stem}_ed.webp"

    with Image.open(input_path) as img:
        img = ImageOps.exif_transpose(img)
        img = img.convert("RGB")
        save_webp_with_limit(img, output_path)

    print(f"最终文件: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
