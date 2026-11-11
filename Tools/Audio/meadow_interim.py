# Interim "A nice day" loop: plucked pentatonic arpeggio over a soft drone, D major, 84 BPM, 8 bars (22.86 s), seamless.
import math, random, struct, subprocess, sys
R=44100; bpm=84; beat=60/bpm; bars=8; n=int(R*beat*4*bars)
random.seed(7)
out=[0.0]*n
def pluck(freq, start, dur, amp):
    N=int(R/freq); buf=[random.uniform(-1,1) for _ in range(N)]
    length=int(dur*R); i=0
    for k in range(length):
        s=buf[i%N]; nxt=buf[(i+1)%N]
        buf[i%N]=0.4985*(s+nxt); i+=1
        idx=(start+k)%n                       # wraps: the tail runs into the loop's start
        out[idx]+=s*amp*(1-k/length)**0.5
D=[293.66,329.63,369.99,440.0,493.88,587.33,659.26]  # D E F# A B D E
pat=[0,2,3,5,3,2, 1,3,4,5,4,3, 0,2,3,6,5,3, 1,3,5,4,3,2]
step=beat/2
t=0
for bar in range(bars):
    for j in range(8):
        note=D[pat[(bar*8+j)%len(pat)]]*(1 if (bar%4)<2 else 1)
        pluck(note, int((bar*4*beat+j*step)*R), 1.6, 0.22)
    if bar%2==0: pluck(146.83 if bar%4==0 else 110.0, int(bar*4*beat*R), 3.5, 0.3)
# drone pad: two detuned sines, breathing over the loop (whole cycles so it loops)
T=n/R
for i in range(n):
    x=i/R
    pad=0.05*math.sin(2*math.pi*round(73.42*T)/T*x)+0.04*math.sin(2*math.pi*round(110.0*T+1)/T*x)
    out[i]+=pad*(0.6+0.4*math.sin(2*math.pi*x/T))
peak=max(abs(v) for v in out); g=0.6/peak
raw=b''.join(struct.pack('<h',int(max(-1,min(1,v*g))*32767)) for v in out)
subprocess.run(['ffmpeg','-v','error','-y','-f','s16le','-ar',str(R),'-ac','1','-i','-','-af','aformat=channel_layouts=stereo,loudnorm=I=-16:TP=-1.5','-ar','44100','-c:a','libvorbis','-q:a','5',sys.argv[1]],input=raw,check=True)
