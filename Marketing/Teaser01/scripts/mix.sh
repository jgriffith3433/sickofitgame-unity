#!/usr/bin/env bash
# The teaser's sound: Farm Life, the meadow, the snores, the line and the booms, cut to the picture (45 s).
# Loudness about -14 LUFS (YouTube), peaks limited to -1 dBFS.
# Usage: mix.sh <audio dir with the Gen assets> <farm life.mp3> <out.wav>
set -euo pipefail
A="$1"; FARM="$2"; OUT="$3"
# times (s) in the teaser
SNORE=19.6     # helmet snores (a_7af49da37f1a2d94): snores at +0.2, +2.9, +5.2; trimmed at +7
LINE=28.6      # "I'm tired... of sleeping..." (a_f9ff15aa45dc4ace)
BOOM=33.95     # the boom that ends the nice afternoon (a_8c848401d25e5f26)
TITLE=38.2     # the title slams in (a_7986f437c3a92491, quieter)
ms() { awk "BEGIN{printf \"%d\", $1*1000}"; }

# Farm Life: full over the village, softer in the dark and the hut, ducked under the line, cut dead on the
# boom; back softly under the title and faded out.
FARM_VOL="if(lt(t,15.6),1,if(lt(t,18.3),1-0.45*(t-15.6)/2.7,if(lt(t,28.2),0.55,if(lt(t,28.6),0.55-0.41*(t-28.2)/0.4,if(lt(t,33.9),0.14,if(lt(t,33.94),0.14*(33.94-t)/0.04,if(lt(t,$TITLE),0,if(lt(t,$TITLE+0.6),0.42*(t-$TITLE)/0.6,if(lt(t,42.5),0.42,max(0,0.42*(45-t)/2.5))))))))))"
ffmpeg -v error -y \
  -i "$FARM" \
  -i "$A/a_3a9633efc410fd82.mp3" \
  -i "$A/a_7af49da37f1a2d94.mp3" \
  -i "$A/a_f9ff15aa45dc4ace.mp3" \
  -i "$A/a_8c848401d25e5f26.mp3" \
  -i "$A/a_7986f437c3a92491.mp3" \
  -filter_complex "
    [0]atrim=0:45,asetpts=N/SR/TB,aresample=48000,volume='$FARM_VOL':eval=frame[farm];
    [1]aresample=48000,aloop=loop=1:size=960000,atrim=0:34,volume='if(lt(t,17.5),0.9,0.22)':eval=frame,afade=t=in:d=1.5,afade=t=out:st=33.85:d=0.1[meadow];
    [2]aresample=48000,atrim=0:7,afade=t=out:st=6.5:d=0.5,volume=1.1,adelay=$(ms $SNORE):all=1[snore];
    [3]aresample=48000,volume=1.6,adelay=$(ms $LINE):all=1[line];
    [4]aresample=48000,volume=0.75,adelay=$(ms $BOOM):all=1[boom];
    [5]aresample=48000,volume=0.55,adelay=$(ms $TITLE):all=1[boom2];
    [farm][meadow][snore][line][boom][boom2]amix=inputs=6:normalize=0:duration=longest,atrim=0:45,volume=1.6,
      alimiter=limit=0.89:level=false,aformat=sample_fmts=s16:channel_layouts=stereo[out]" \
  -map "[out]" -ar 48000 "$OUT"
