#!/usr/bin/env bash
# Hell's Ad Break, the final cut from the three full-quality scenes (video.scene, MiniMax H3, 0.5 MP, PERFORM mode).
# Usage: build_final.sh <folder holding A.mp4 B.mp4 C.mp4 and set.png> <out.mp4>
# Scene C's Yooba cut (5.375-7.417 s) came out on the reference's flat grey (the model dropped the set): it is keyed out
# over the set plate with a slow push, warmed to the set's light; the scene's own sound runs on untouched. Critic's notes on v1 (6.2): the hero was
# frozen in A's wide (a slow push-in now, 5.46-11.54 s) and the silence before '...Great.' was short (0.6 s of held frame + room silence added).
# Then one loudness pass over the whole film.
set -euo pipefail
D=$1; OUT=$2; T=$(mktemp -d)
ffmpeg -y -loglevel error -i "$D/set.png" -vf "scale=1152:653,zoompan=z='1.0+0.04*on/50':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=50:s=960x544:fps=24,eq=brightness=-0.08" -frames:v 50 "$T/setbg.mp4"
ffmpeg -y -loglevel error -ss 5.375 -t 2.042 -i "$D/C.mp4" -i "$T/setbg.mp4" -filter_complex "[0:v]colorkey=0x292B2B:0.07:0.02,eq=saturation=0.92:contrast=1.04,colorbalance=rs=0.10:gs=-0.02:bs=-0.06:rm=0.08:bm=-0.04,format=yuva420p[fg];[1:v][fg]overlay=shortest=1,format=yuv420p[v]" -map "[v]" -an -r 24 "$T/y.mp4"
ffmpeg -y -loglevel error -t 5.375 -i "$D/C.mp4" -an -r 24 -pix_fmt yuv420p "$T/c1.mp4"
ffmpeg -y -loglevel error -ss 7.417 -i "$D/C.mp4" -an -r 24 -pix_fmt yuv420p -vf "tpad=start_mode=clone:start_duration=0.6" "$T/c3.mp4"
printf "file '%s/c1.mp4'\nfile '%s/y.mp4'\nfile '%s/c3.mp4'\n" "$T" "$T" "$T" > "$T/c.txt"
ffmpeg -y -loglevel error -f concat -safe 0 -i "$T/c.txt" -i "$D/C.mp4" -filter_complex "[1:a]atrim=0:7.417,asetpts=N/SR/TB[a1];anullsrc=r=48000:cl=stereo,atrim=0:0.6[sil];[1:a]atrim=7.417,asetpts=N/SR/TB[a3];[a1]aresample=48000[a1r];[a3]aresample=48000[a3r];[a1r][sil][a3r]concat=n=3:v=0:a=1[a]" -map 0:v -map "[a]" -c:v libx264 -crf 14 -pix_fmt yuv420p -c:a aac -b:a 192k "$T/Cfix.mp4"
ffmpeg -y -loglevel error -i "$D/A.mp4" -filter_complex "[0:v]split[a][b];[a]trim=0:5.458,setpts=PTS-STARTPTS[v1];[b]trim=5.458,setpts=PTS-STARTPTS,scale=1920:1088,zoompan=z='1+0.07*on/138':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=960x544:fps=24[v2];[v1][v2]concat=n=2:v=1:a=0[v]" -map "[v]" -map 0:a -c:v libx264 -crf 14 -r 24 -pix_fmt yuv420p -c:a aac -b:a 192k -ar 48000 "$T/A.mp4"
ffmpeg -y -loglevel error -i "$D/B.mp4" -c:v libx264 -crf 14 -r 24 -pix_fmt yuv420p -c:a aac -b:a 192k -ar 48000 "$T/B.mp4"
ffmpeg -y -loglevel error -i "$T/Cfix.mp4" -c:v copy -c:a aac -b:a 192k -ar 48000 "$T/C.mp4"
printf "file '%s/A.mp4'\nfile '%s/B.mp4'\nfile '%s/C.mp4'\n" "$T" "$T" "$T" > "$T/all.txt"
ffmpeg -y -loglevel error -f concat -safe 0 -i "$T/all.txt" -af "loudnorm=I=-16:TP=-1.5:LRA=11" -c:v libx264 -crf 16 -pix_fmt yuv420p -c:a aac -b:a 192k "$OUT"
cp "$T/y.mp4" "$(dirname "$OUT")/yooba_keyed.mp4"
rm -rf "$T"
