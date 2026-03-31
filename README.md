# Blog Screenshot WebP Saver

Blog Screenshot WebP Saver is a public Windows utility for bloggers, technical writers, and anyone who frequently inserts screenshots into articles.

When people use `Win + Shift + S`, the screenshot goes to the clipboard first. In many writing workflows, that image is later saved and uploaded with a larger-than-needed file size. Large screenshots slow down blog pages, consume more bandwidth, and waste storage.

This project solves that by running as a small tray app in the background. Whenever a new screenshot image appears in the clipboard, it converts the image to `.webp` automatically and saves it to a local output folder.

Typical benefits:

- Faster page loads for image-heavy articles
- Smaller uploads for blog CMS workflows
- Lower storage usage on servers and CDNs
- Less manual work during writing

## What It Does

- Runs in the Windows system tray
- Listens for clipboard image updates triggered by tools like `Win + Shift + S`
- Saves screenshots as `.webp`
- Includes a helper Python script for standalone image conversion
- Includes a second Python script with a size-target compression loop

## Default Output Folder

By default, the app saves converted images to:

`%USERPROFILE%\Pictures\ScreenshotWebpSaver`

You can override this by setting the environment variable:

`SCREENSHOT_WEBP_OUTPUT_DIR`

## How The App Works

The desktop app uses this workflow:

1. Register a clipboard listener
2. Detect when a new image is placed in the clipboard
3. Export the clipboard image to a temporary PNG
4. Call a Python conversion script
5. Save the final `.webp` image into the output folder

The app also hashes recent clipboard images to avoid processing the same screenshot repeatedly within a short time window.

## Python Runtime

The project uses Python + Pillow for WebP conversion.

At runtime, the app searches for Python in this order:

1. `SCREENSHOT_WEBP_PYTHON`
2. `py`
3. `python`
4. common local installation paths, including Thonny and standard Python installs

This means the repository no longer depends on any single hard-coded username or machine-specific path.

## Included Python Scripts

The repository includes two Python scripts:

- `src/ScreenshotWebpSaver/convert_to_webp.py`
  Purpose: the desktop app calls this script internally. It accepts an input image path and an output image path, then writes a `.webp` file.

- `scripts/desktop_screenshot_webp.py`
  Purpose: a standalone helper script for manually converting an image. It keeps a simple quality-reduction loop and tries to compress toward a target file size.

## Compression Logic

The standalone helper script uses:

- target size: `50 KB`
- starting quality: `80`
- minimum quality: `30`
- step size: `5`

Flow:

1. Save as `.webp` with the current quality
2. Check the file size
3. If it is still too large, lower the quality
4. Repeat until the target is met or the minimum quality is reached

This is useful for blog screenshots where a small file size matters more than preserving every pixel perfectly.

## Project Structure

```text
blog-screenshot-webp-saver/
├─ README.md
├─ .gitignore
├─ src/
│  └─ ScreenshotWebpSaver/
│     ├─ Program.cs
│     ├─ NativeMethods.cs
│     ├─ ScreenshotMonitorForm.cs
│     ├─ ScreenshotWebpSaver.csproj
│     ├─ convert_to_webp.py
│     └─ start_screenshot_webp_saver.cmd
├─ scripts/
│  └─ desktop_screenshot_webp.py
└─ release/
   ├─ ScreenshotWebpSaver-win10-x64.zip
   └─ win10-x64/
      ├─ ScreenshotWebpSaver.exe
      ├─ ScreenshotWebpSaver.dll
      ├─ ScreenshotWebpSaver.deps.json
      ├─ ScreenshotWebpSaver.runtimeconfig.json
      ├─ convert_to_webp.py
      └─ start_screenshot_webp_saver.cmd
```

## Usage

### Run the Tray App

Open:

`release/win10-x64/`

Then run either:

- `ScreenshotWebpSaver.exe`
- `start_screenshot_webp_saver.cmd`

After that, use:

`Win + Shift + S`

Each new clipboard screenshot will be converted to `.webp` and saved into the output folder.

### Run the Standalone Python Script

Example:

```powershell
python .\scripts\desktop_screenshot_webp.py "C:\path\to\image.jpg"
```

Optional custom output directory:

```powershell
python .\scripts\desktop_screenshot_webp.py "C:\path\to\image.jpg" "C:\path\to\output"
```

## Requirements

- Windows 10 or newer
- .NET Desktop Runtime 9
- Python
- Pillow

Install Pillow with:

```powershell
pip install pillow
```

## Who This Is For

- bloggers
- technical writers
- documentation authors
- developers writing tutorials
- anyone who wants smaller screenshot files automatically

## Notes

The repository is intentionally machine-agnostic. If you want a different output folder, Python executable, or compression behavior, update the environment variables or adjust the source code to fit your workflow.
