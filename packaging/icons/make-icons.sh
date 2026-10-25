#!/usr/bin/env bash
# Renders RelicWriter/Assets/Icon.svg into every icon the app ships with, using headless Chrome, Chromium or Edge (else rsvg-convert) for the rendering.
#   RelicWriter/Assets/Icon.ico               Windows icon (16 to 256 px) for the window and the exe
#   RelicWriter/Assets/Icon.png               256 px, for Avalonia code that wants a bitmap
#   packaging/flatpak/icons/hicolor/...       the Linux sizes (16 to 512 px, plus the scalable SVG)
# Sizes up to 32 px are rendered without the fine detail (elements with class "detail"), which turns to mush that small.
# Needs python3 to pack the .ico. Run from anywhere: packaging/icons/make-icons.sh
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
svg="$root/RelicWriter/Assets/Icon.svg"
app_id=io.github.hendy643.RelicWriter

browser=""
for b in google-chrome google-chrome-stable chromium chromium-browser microsoft-edge microsoft-edge-stable; do
    command -v "$b" >/dev/null && { browser="$b"; break; }
done
if [ -z "$browser" ] && ! command -v rsvg-convert >/dev/null; then echo 'Chrome, Chromium, Edge or rsvg-convert is needed to render the SVG.' >&2; exit 1; fi
command -v python3 >/dev/null || { echo 'python3 is needed to write the .ico.' >&2; exit 1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

render() { # size path
    local size="$1" path="$2" file="$work/icon-$1.svg" style=''
    mkdir -p "$(dirname "$path")"
    if [ "$size" -le 32 ]; then
        sed 's#</defs>#<style>.detail{display:none}</style></defs>#' "$svg" > "$file"
    else
        cp "$svg" "$file"
    fi
    if [ -n "$browser" ]; then
        echo "<html><body style='margin:0;background:transparent'><img src='file://$file' width='$size' height='$size' style='display:block'></body></html>" > "$work/icon-$size.html"
        "$browser" --headless --disable-gpu --hide-scrollbars --default-background-color=00000000 --force-device-scale-factor=1 \
            --screenshot="$path" --window-size="$size,$size" "file://$work/icon-$size.html" >/dev/null 2>&1
    else
        rsvg-convert -w "$size" -h "$size" "$file" -o "$path"
    fi
    [ -f "$path" ] || { echo "Rendering $size px failed." >&2; exit 1; }
}

# Linux: the sizes AppStream and the icon themes accept, and the scalable original.
hicolor="$root/packaging/flatpak/icons/hicolor"
rm -rf "$hicolor"
for size in 16 32 48 64 128 256 512; do render "$size" "$hicolor/${size}x${size}/apps/$app_id.png"; done
mkdir -p "$hicolor/scalable/apps"
cp "$svg" "$hicolor/scalable/apps/$app_id.svg"

# Avalonia: a bitmap, and the Windows icon with PNG-compressed images of every size.
cp "$hicolor/256x256/apps/$app_id.png" "$root/RelicWriter/Assets/Icon.png"
sizes=(16 24 32 48 64 128 256)
for size in "${sizes[@]}"; do render "$size" "$work/ico-$size.png"; done
python3 - "$work" "$root/RelicWriter/Assets/Icon.ico" "${sizes[@]}" <<'PY'
import struct, sys
work, out, sizes = sys.argv[1], sys.argv[2], [int(s) for s in sys.argv[3:]]
images = [(s, open(f"{work}/ico-{s}.png", "rb").read()) for s in sizes]
data = struct.pack("<HHH", 0, 1, len(images))
offset = 6 + 16 * len(images)
for size, png in images:
    data += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(png), offset)  # 256 is stored as 0
    offset += len(png)
open(out, "wb").write(data + b"".join(png for _, png in images))
PY
echo 'Icons written.'
