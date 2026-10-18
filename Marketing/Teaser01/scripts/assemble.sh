#!/usr/bin/env bash
# Puts Teaser01's picture together (45.0 s, 1920x1080, 30 fps) and lays the mix under it.
#   0.0-17.5  exterior.mp4 (the village, push into the doorway, fade to black)
#  17.5-18.3  black
#  18.3-34.0  interior.mp4 (the hut, the line, the eyes)
#  34.0-45.0  end.mp4      (boom: the village after, the title)
# Usage: assemble.sh <exterior.mp4> <interior.mp4> <end.mp4> <mix.wav> <out.mp4>
set -euo pipefail
EXT="$1"; INT="$2"; END="$3"; MIX="$4"; OUT="$5"
ffmpeg -v error -y -i "$EXT" -i "$INT" -i "$END" -i "$MIX" -filter_complex "
  color=c=black:s=1920x1080:r=30:d=0.8,format=yuv420p[blk];
  [0]fps=30,format=yuv420p,setsar=1[a];[1]fps=30,format=yuv420p,setsar=1[b];[2]fps=30,format=yuv420p,setsar=1[c];
  [a][blk][b][c]concat=n=4:v=1:a=0,trim=0:45[v]" \
  -map "[v]" -map 3:a -c:v libx264 -preset slow -crf 16 -profile:v high -pix_fmt yuv420p -movflags +faststart \
  -c:a aac -b:a 256k -ar 48000 -shortest "$OUT"
ffprobe -v error -show_entries format=duration:stream=codec_name,width,height,r_frame_rate -of compact "$OUT"
