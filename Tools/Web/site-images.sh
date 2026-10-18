#!/usr/bin/env bash
# Rebuilds the lore pages' images (Tools/Web/site/blog/lore/img/) from the game's own art: the Shaft's Circle paintings
# and Warden figures, the hero's marker, Yooba's idle portrait sheet, a Magma King frame and the Overlord from the
# website's Video1 sheet. Small WebP files, so the lore pages stay fast. Needs ffmpeg with libwebp.
# Usage: Tools/Web/site-images.sh [path to sickofitgamewebsite]
set -euo pipefail
cd "$(dirname "$0")/../.."
site="${1:-../sickofitgamewebsite}"
out=Tools/Web/site/blog/lore/img
mkdir -p "$out"
webp() { ffmpeg -v error -y -i "$1" -vf "$2" -c:v libwebp -quality "$3" -compression_level 6 "$4"; }

for c in I II III IV V VI VII VIII IX X; do
  webp "Assets/Art/Shaft/band_$c.png" "scale=480:-1" 58 "$out/band-$c.webp"
  webp "Assets/Art/Shaft/warden_$c.png" "scale=320:-1" 72 "$out/warden-$c.webp"
done
webp Assets/Art/Shaft/meadow.png "scale=768:-1" 66 "$out/meadow.webp"
webp Assets/Art/Shaft/marker.png "scale=320:-1" 78 "$out/hero.webp"
# Yooba's idle loop: the 4x4 sheet at half size (128 px frames), played by CSS steps().
webp Assets/Art/Yooba/yooba_idle.png "scale=512:512" 78 "$out/yooba-idle.webp"
# The Magma King on his throne: the first frame of his idle sheet (8 x 3 frames).
webp Assets/Art/Enemies/MagmaKing/MagmaKing_idle.png "crop=iw/8:ih/3:0:0,scale=244:-1" 78 "$out/magma-king.webp"
webp Assets/Art/Effects/Portal/Portal_scorch.png "scale=320:-1" 72 "$out/scorch.webp"
# The Overlord over the hills: one frame of the website's Video1 sheet (2 x 3 frames above a black strip).
if [ -f "$site/images/Overlord.png" ]; then
  webp "$site/images/Overlord.png" "crop=iw/2:ih*0.28:0:0,scale=480:-1" 70 "$out/overlord.webp"
fi
# Small thumbnails of the website's older lore art (the originals are megabytes).
for name in Fireball Bork Slobber Sintinel; do
  lower=$(echo "$name" | tr '[:upper:]' '[:lower:]')
  [ -f "$site/images/$name.png" ] && webp "$site/images/$name.png" "scale=160:160:force_original_aspect_ratio=decrease" 75 "$out/thumb-$lower.webp"
done
ls -l "$out"
