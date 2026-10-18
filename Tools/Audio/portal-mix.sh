#!/usr/bin/env bash
# Makes the prologue's two hand-made portal sounds from generated clips (ffmpeg; run from the repo root):
#   vo_portal_echo  Hell answering the hero: his "Hello?" (vo_portal_hello_1) comes back three times out of the
#                   portal, each repeat pitched and slowed further down, darker and more distorted (the loop answering).
#   portal_swallow  the portal's suction whoosh (portal_suck_whoosh) with his yelp (vo_portal_grabbed_1) falling away
#                   into it: pitched down, filtered and fading as he shrinks.
# Both have no "prompt" in audio.json, so the Sound Generator leaves them alone. Re-run this after remaking a source.
set -euo pipefail
G=Assets/Audio/Generated
hello=$G/vo_portal_hello_1.mp3
yelp=$G/vo_portal_grabbed_1.mp3
whoosh=$G/portal_suck_whoosh.mp3

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

# The shout alone: silence trimmed off both ends, mono.
ffmpeg -v error -y -i "$hello" -af "silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,aformat=sample_rates=44100:channel_layouts=mono" "$tmp/hello.wav"

# One repeat: rate (pitch and speed drop together), low-pass, crush (bits, mix), volume, echo, start (ms).
# Each repeat starts a beat after the one before (they get longer as they slow down); the tail rings out.
repeat() { ffmpeg -v error -y -i "$tmp/hello.wav" -af "asetrate=44100*$1,aresample=44100,lowpass=f=$2,acrusher=bits=$3:mix=$4:mode=log,volume=$5,apad=pad_dur=0.8,$6,adelay=$7:all=1" "$tmp/echo$8.wav"; }
repeat 0.84 3200 10 0.15 0.85 "aecho=0.8:0.7:180|340:0.35|0.2"           350 1
repeat 0.68 2000 7  0.35 0.80 "aecho=0.8:0.7:260|520:0.45|0.3"           1500 2
repeat 0.52 1200 5  0.55 0.85 "aecho=0.8:0.75:380|760|1100:0.5|0.35|0.2" 2900 3
ffmpeg -v error -y -i "$tmp/echo1.wav" -i "$tmp/echo2.wav" -i "$tmp/echo3.wav" -filter_complex "[0:a][1:a][2:a]amix=inputs=3:normalize=0:duration=longest,volume=2,apad=pad_dur=0.4,aecho=0.85:0.6:90|170:0.25|0.15,areverse,silenceremove=start_periods=1:start_threshold=-60dB,afade=t=in:d=1.0,areverse,alimiter=limit=0.9" \
  -ac 1 -ar 44100 -b:a 128k "$G/vo_portal_echo.mp3"

ffmpeg -v error -y -i "$whoosh" -i "$yelp" -filter_complex "
  [1:a]aformat=sample_rates=44100:channel_layouts=mono,asetrate=44100*0.94,aresample=44100,
       volume='max(0.05,1-0.32*t)':eval=frame,lowpass=f=2600,aecho=0.8:0.6:120|260:0.35|0.2,adelay=250:all=1[y];
  [0:a]aformat=sample_rates=44100:channel_layouts=mono,volume=0.9[w];
  [w][y]amix=inputs=2:normalize=0:duration=longest,areverse,afade=t=in:d=0.8,areverse,alimiter=limit=0.9" \
  -ac 1 -ar 44100 -b:a 128k "$G/portal_swallow.mp3"
echo "made $G/vo_portal_echo.mp3 and $G/portal_swallow.mp3"
