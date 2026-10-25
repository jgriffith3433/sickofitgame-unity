#!/usr/bin/env python3
"""No-GPU animatics for Video5 The Count, Video7 Market Day, Video8 The Best Seat, in the Video3 way:
the game's own art (sheets cropped to cells), the table read's real voices, ffmpeg moves, captions as subtitles.
Mouths: Yooba's talk/laugh cells cycle on his lines; the Overlord's and the King's eyes/crown flare and the
announcer's drawn mouth works exactly while they speak (windows from the table read); the hero is helmed, his eye
slits flare. Usage: build_animatic.py <repo root> <count|market|seat>"""
import subprocess, sys, os, glob, json
R, WHICH = sys.argv[1], sys.argv[2]
SLUG = {"count": "the-count", "market": "market-day", "seat": "best-seat"}[WHICH]
O = f"{R}/videos/{SLUG}/out"; C = O + "/cells"; os.makedirs(C, exist_ok=True)
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
W, H = 1280, 704
A = R + "/Assets/Art"
def run(*a): subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *a], check=True)
def cells(sheet, cols, rows, prefix, n):
    from subprocess import check_output
    w, h = map(int, check_output(["ffprobe", "-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0:s=x", sheet]).decode().strip().split("x"))
    cw, ch = w // cols, h // rows
    for i in range(n):
        run("-i", sheet, "-vf", f"crop={cw}:{ch}:{(i%cols)*cw}:{(i//cols)*ch}", "-frames:v", "1", f"{C}/{prefix}{i:02d}.png")
SHEETS = {
 "mk": (A + "/Enemies/MagmaKing/MagmaKing_idle.png", 8, 3, 24),
 "fm": (A + "/Enemies/ImpForeman/ImpForeman_idle.png", 7, 4, 24),
 "yt": (A + "/Yooba/yooba_talk.png", 4, 4, 16),
 "yl": (A + "/Yooba/yooba_laugh.png", 4, 4, 16),
 "yi": (A + "/Yooba/yooba_idle.png", 4, 4, 16),
 "hs": (A + "/Sprites/Player/Shout.png", 6, 5, 25),
}
for k, (p, c, r, n) in SHEETS.items():
    if not os.path.exists(f"{C}/{k}00.png"): cells(p, c, r, k, n)
def lav(c0, c1, d): return ["-f", "lavfi", "-i", f"gradients=s={W}x{H}:c0={c0}:c1={c1}:x0=0:y0=0:x1=0:y1={H}:d={d}:r=24"]
def loop(k, fps): return ["-framerate", str(fps), "-stream_loop", "-1", "-start_number", "0", "-i", f"{C}/{k}%02d.png"]
def still(img): return ["-loop", "1", "-framerate", "24", "-i", img]
def dt(text, size, y, color="white", x="(w-text_w)/2", en=None):
    t = text.replace("'", "’").replace(":", "\\:").replace(",", "\\,")
    e = f":enable='{en}'" if en else ""
    return f"drawtext=fontfile={FONT}:text='{t}':fontsize={size}:fontcolor={color}:borderw=5:bordercolor=black:x={x}:y={y}{e}"
def push(z0, z1, d):
    n = int(d * 24)
    return f"zoompan=z='{z0}+({z1}-{z0})*on/{n}':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps=24"
def bars(): return f"drawbox=x=0:y=0:w={W}:h=14:color=black@0.9:t=fill,drawbox=x=0:y={H-14}:w={W}:h=14:color=black@0.9:t=fill"
def between(a, b): return f"between(t\\,{a}\\,{b})"
def sub(text, a, b, size=34): return dt(text, size, str(H - 80), "white", en=between(a, b))
def flare(x, y, w, a, b, color="0xff2a1a"):  # eyes/crown flare while speaking
    return f"drawbox=x={x}:y={y}:w={w}:h='4+10*abs(sin(t*11))':color={color}@0.95:t=fill:enable='{between(a,b)}'"
def amouth(a, b):  # the announcer's drawn mouth
    return (f"drawbox=x=500:y=440:w=280:h='26+80*abs(sin(t*17))':color=0x7a0018:t=fill:enable='{between(a,b)}',"
            f"drawbox=x=500:y=440:w=280:h=20:color=0xf3e6c8:t=fill:enable='{between(a,b)}'")
def seg(name, d, ins, fc):
    args = [x for i in ins for x in i]
    run(*args, "-filter_complex", fc, "-map", "[v]", "-t", str(d), "-r", "24", "-pix_fmt", "yuv420p", f"{O}/seg_{name}.mp4")
def ov(base, k, h, x, y, label):  # sprite overlay chain pieces
    return f"[{label}]scale=-1:{h}[sp];[{base}][sp]overlay=x={x}:y={y}"
segs = []
def add(name, d, ins, fc): segs.append((name, d)); seg(name, d, ins, fc)

if WHICH == "count":
    # table read: O1 0-4.16, O2 4.16-11.2, O3 11.2-14.49, O4 14.49-18.49, A 18.49-20.81, O5 20.81-23.85 ; ends 26.75
    d = 4.16
    add("c1", d, [lav("0x05050a", "0x20102a", d), loop("fm", 6)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x0a0a12:t=fill,drawbox=x=420:y=120:w=440:h=380:color=0x101018:t=fill[b];"
        f"{ov('b','fm',300,'60+t*70',360,'1')},{flare(560,170,40,2.0,4.0)},{dt('THE HALL OF BLACK GLASS',40,'40','0xc8c8d8',en='lt(t\\,2.4)')},"
        f"{sub('OVERLORD: Mm. ... One.',2.0,4.1)},{push(1.0,1.12,d)},{bars()}[v]")
    d = 7.04
    add("c2", d, [lav("0x05050a", "0x2a1020", d), loop("fm", 6)],
        f"[0]drawbox=x=100:y=470:w='min(1080\\,100+t*150)':h=26:color=0xe8f4ff:t=fill,drawbox=x=0:y=500:w={W}:h=204:color=0x0a0a12:t=fill[b];"
        f"{ov('b','fm',240,'1000-t*120',250,'1')},drawbox=x='100+t*150':y=380:w=34:h=90:color=0x9a8a7a:t=fill,"
        f"{sub('OVERLORD: Two. ... Three.',0.2,7.0)},{push(1.0,1.08,d)},{bars()}[v]")
    d = 3.29
    add("c3", d, [lav("0x05050a", "0x2a0e18", d)],
        f"[0]drawbox=x=0:y=470:w={W}:h=26:color=0xe8f4ff:t=fill,drawbox=x=1180:y=420:w=60:h=50:color=0x9a8a7a:t=fill,{dt('NOT ENOUGH',120,'180','0xff6a5a',en='gt(t\\,1.8)')},"
        f"{dt('(the slab ends. the row does not.)',34,'360','0xc8c8d8')},{sub('OVERLORD: Hhhhh... Not enough.',0.2,3.2)},{push(1.0,1.15,d)},{bars()}[v]")
    d = 4.0
    add("c4", d, [lav("0x05050a", "0x180820", d), still(A + "/Enemies/ImpForeman/ImpForeman_idle.png")],
        f"[0]drawbox=x=380:y=240:w=520:h=300:color=0x6a4a2a:t=fill,drawbox=x=560:y=500:w=160:h=14:color=0x120a05:t=fill,drawbox=x=592:y=410:w=14:h=14:color=0xe8f4ff:t=fill,"
        f"{dt('THE SACK. EMPTY.',60,'100','0xc8c8d8')},{dt('ONE PLACE IN THE ROW: EMPTY',40,'580','0xff6a5a',en='gt(t\\,2)')},"
        f"{sub('OVERLORD: Someone... has been keeping some.',0.2,3.9)},{push(1.0,1.2,d)},{bars()}[v]")
    d = 2.32
    add("c5", d, [lav("0x05050a", "0x101030", d)],
        f"[0]drawbox=x=620:y=40:w=40:h=14:color=0xfff0b0:t=fill,drawbox=x=580:y=300:w=40:h='4+10*abs(sin(t*11))':color=0xff2a1a:t=fill,drawbox=x=670:y=300:w=40:h='4+10*abs(sin(t*11))':color=0xff2a1a:t=fill,"
        f"{amouth(0,2.3).replace('y=440','y=560').replace('w=280','w=160').replace('x=500','x=560')},{dt('FAR ABOVE: A PINPOINT OF DAYLIGHT',36,'80','0xfff0b0')},{sub('ANNOUNCER (distant): HE IS COUNTING AGAIN, FOLKS!',0,2.3)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 5.9
    add("c6", d, [lav("0x020204", "0x100810", d)],
        f"[0]drawbox=x=580:y=300:w=40:h='4+10*abs(sin(t*11))':color=0xff2a1a:t=fill:enable='lt(t\\,3)',drawbox=x=670:y=300:w=40:h='4+10*abs(sin(t*11))':color=0xff2a1a:t=fill:enable='lt(t\\,3)',"
        f"drawbox=x=620:y=40:w=40:h=14:color=0xfff0b0:t=fill:enable='lt(t\\,5.0)',{sub('OVERLORD: Mm. ... Quiet.',0,3.0)},"
        f"{dt('THE COUNT',110,'250','white',en='gt(t\\,4)')},{dt('Made with Hawkes Gen · hawkesgen.com',26,'650','0xa0a0a0',en='gt(t\\,4.3)')},{push(1.0,1.08,d)},{bars()}[v]")
    AUDIO = glob.glob(O + "/tableread/*-dialogue.mp3")[0]; TOTAL = 29.7
elif WHICH == "market":
    # Y1 0-18.16 (V.O.), Y2 18.16-23.36, Y3 23.36-26.24, Y4 26.24-27.52, hero 27.52-30.16, Y 30.16-30.96, A 30.96-33.44
    d = 5.0
    add("m1", d, [lav("0x6ac0f0", "0xf8e8b0", d), loop("yi", 8)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x4a9a3a:t=fill,drawbox=x=120:y=300:w=220:h=200:color=0xb06a3a:t=fill,drawbox=x=150:y=240:w=40:h=70:color=0x8a8a8a:t=fill[b];"
        f"{ov('b','yi',330,700,210,'1')},{dt('THE MEADOW, DAWN',44,'40','0x2a4a2a')},{sub('YOOBA (V.O.): Okay so this is the village, right, this is the good part...',0,5,26)},{push(1.0,1.08,d)},{bars()}[v]")
    d = 6.0
    add("m2", d, [lav("0x6ac0f0", "0xf8e8b0", d), loop("yt", 12)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x4a9a3a:t=fill,drawbox=x=200:y=440:w=300:h=60:color=0x8a5a2a:t=fill[b];"
        f"{ov('b','yt',300,'100+t*130',260,'1')},{dt('THE POLISHING BENCH',44,'40','0x2a4a2a')},{sub('YOOBA (V.O.): ...we polish them once a year for the dance...',0,6)},{push(1.0,1.08,d)},{bars()}[v]")
    d = 7.16
    add("m3", d, [lav("0x6ac0f0", "0xf8e8b0", d), loop("yt", 12)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x4a9a3a:t=fill[b];{ov('b','yt',560,420,100,'1')},"
        f"{dt('...but I’m the best polisher!',56,'600','white',en='gt(t\\,5.0)')},{sub('YOOBA: ...and I’m not saying I’m the best polisher—',0,7)},{push(1.0,1.12,d)},{bars()}[v]")
    d = 5.2
    add("m4", d, [lav("0x6ac0f0", "0xf0c890", d), loop("hs", 4)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x4a9a3a:t=fill,drawbox=x=300:y=180:w=680:h=340:color=0x6a4a2a:t=fill,drawbox=x=380:y=230:w=520:h=240:color=0x203040:t=fill[b];"
        f"{ov('b','hs',300,490,190,'1')},{dt('Zzzz',60,'160','white',x='760',en='gt(t\\,1.0)')},{sub('YOOBA (V.O.): That’s him. Never comes to the dance. Says it’s loud. It’s music.',0,5.2,28)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 2.88
    add("m5", d, [lav("0x2a1020", "0xc03a10", d)],
        f"[0]drawbox=x=0:y=520:w={W}:h=184:color=0x2a2a1a:t=fill,drawbox=x=0:y=0:w={W}:h='60+t*30':color=0x300808@0.7:t=fill,{dt('THE SKY WENT RED AT THE EDGE',44,'250','white')},"
        f"{sub('YOOBA (V.O., slower): ...And then the sky did the thing.',0,2.9)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 1.28
    add("m6", d, [lav("0x6ac0f0", "0xf8e8b0", d), loop("yt", 14)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x4a9a3a:t=fill[b];{ov('b','yt',560,420,100,'1')},drawbox=x=560:y=520:w=160:h=60:color=0xfff0f0@0.8:t=fill,{sub('YOOBA (muffled): Hold these.',0,1.3)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 2.64
    add("m7", d, [lav("0x2a1008", "0x6a2a10", d), still(f"{C}/hs02.png")],
        f"[0]drawbox=x=0:y=560:w={W}:h=144:color=0x1a1008:t=fill[b];{ov('b','hs',560,430,60,'1')},{flare(618,150,48,0,2.6,'0xff7a1a')},"
        f"{dt('THE LANDING',40,'40','0xf5c542')},{sub('HERO: ...You’ve been carrying those the whole time.',0,2.6)},{push(1.0,1.12,d)},{bars()}[v]")
    d = 0.8
    add("m8", d, [lav("0x0e3a12", "0x2b9e33", d), loop("yl", 12)],
        f"[1]scale=-1:500[sp];[0][sp]overlay=x=400:y=100,{sub('YOOBA: ...Yes?',0,0.8)},{bars()}[v]")
    d = 3.2
    add("m9", d, [lav("0x6ac0f0", "0xf8e8b0", d)],
        f"[0]drawbox=x=0:y=500:w={W}:h=204:color=0x4a9a3a:t=fill,{dt('THE VILLAGE',90,'200','0x2a4a2a')},{amouth(0,2.5).replace('y=440','y=400')},"
        f"{sub('ANNOUNCER: THE VILLAGE: IT WAS A VERY NICE VILLAGE!',0,3.2)},{dt('Made with Hawkes Gen · hawkesgen.com',26,'650','0x404040',en='gt(t\\,2)')},{push(1.0,1.06,d)},{bars()}[v]")
    AUDIO = glob.glob(O + "/tableread/*-dialogue.mp3")[0]; TOTAL = 36.4
else:
    # K1 0-6.16, K2 6.16-9.6, K3 9.6-17.36, K4 17.36-31.84, K5 31.84-35.36, hero 35.36-38.64, K6 38.64-41.44, A 41.44-45.6
    d = 6.16
    add("k1", d, [lav("0x300800", "0xff6a10", d), still(f"{C}/mk00.png")],
        f"[0]drawbox=x=0:y=520:w={W}:h=184:color=0x5a1404:t=fill[b];[1]scale=-1:200,colorchannelmixer=rr=0.4:gg=0.2:bb=0.1[sp];[b][sp]overlay=x=540:y=440,"
        f"{dt('THE LAVA RIVER, DEEP, SLOW',40,'40','0xffd08a')},drawbox=x='200+t*40':y='600-mod(t*90\\,300)':w=14:h=14:color=0xffd08a@0.7:t=fill,{sub('KING (V.O.): Mm-hm-hm. ... In the beginning I was a rock.',0,6.1)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 3.44
    add("k2", d, [lav("0xff8a20", "0xffd08a", d)],
        f"[0]drawbox=x='100+t*300':y=420:w=60:h=22:color=0x3a8a2a:t=fill,drawbox=x='100+t*300+50':y=410:w=16:h=16:color=0x3a8a2a:t=fill,drawbox=x=800:y='500-t*40':w=240:h=140:color=0x2a1008:t=fill,"
        f"{dt('A SALAMANDER STOPS. LOOKS. NODS.',40,'40','0x3a1a08')},{sub('KING (V.O.): And something looked at me.',0,3.4)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 7.76
    add("k3", d, [lav("0x300800", "0xff6a10", d), still(f"{C}/mk04.png")],
        f"[0]drawbox=x=0:y=560:w={W}:h=144:color=0x5a1404:t=fill[b];[1]scale=-1:'160+n/24*30':eval=frame[sp];[b][sp]overlay=x=560:y='520-t*30',"
        f"{dt('FIRST REVIEW:',50,'80','0xffd08a',en='gt(t\\,3)')},{dt('★★★★ TOO WARM',90,'180','0xf5c542',en='gt(t\\,4.5)')},{sub('KING (V.O.): That was my first review. Four stars. Too warm.',0,7.7)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 14.48
    add("k4", d, [lav("0x300800", "0xe05a10", d), loop("mk", 6), loop("fm", 6)],
        f"[0]drawbox=x=0:y=600:w={W}:h=104:color=0x3a0c04:t=fill[b];"
        f"[1]scale=-1:'min(520\\,200+n/24*45)':eval=frame[k];[2]scale=-1:150[i];[b][k]overlay=x=470:y='max(80\\,640-t*45)'[c];"
        f"[c][i]overlay=x='60+mod(t*40\\,200)':y=500[d];[d]"
        f"{dt('THE WASTES COME TO THE SHOW',48,'30','0xffd08a',en='gt(t\\,5)')},{dt('NEVER GETS UP',48,'30','0xffd08a',en='lt(t\\,5)')},"
        f"{flare(560,'max(170\\,640-t*45+60)' if False else 300,60,0,14.4,'0xffd040')},{sub('KING (V.O.): The Wastes started coming. I never get up. Why would I? The best seat comes to the show.',0,14.4,22)},{push(1.0,1.08,d)},{bars()}[v]")
    d = 3.52
    add("k5", d, [lav("0x1a0a08", "0x8a2a08", d), loop("hs", 6)],
        f"[0]drawbox=x=0:y=560:w={W}:h=144:color=0x2a0a04:t=fill,drawbox=x=0:y=500:w={W}:h=12:color=0xff6a10:t='fill'[b];[1]scale=-1:'260-n/24*20':eval=frame[h];[b][h]overlay=x='100+t*300':y=300,"
        f"drawbox=x=950:y=200:w=240:h=300:color=0x3a1408:t=fill,drawbox=x=1010:y=140:w=120:h=60:color=0xff6a10:t=fill,{dt('SEATING: ONE',54,'560','0xffd08a',en='gt(t\\,1.6)')},"
        f"{sub('KING: Mm-hm-hm. ... Come up, then.',0,3.5)},{push(1.0,1.08,d)},{bars()}[v]")
    d = 3.28
    add("k6", d, [lav("0x1a0a08", "0x6a2008", d), still(f"{C}/hs02.png")],
        f"[0]drawbox=x=0:y=600:w={W}:h=104:color=0x2a0a04:t=fill[b];[1]scale=-1:500[h];[b][h]overlay=x=420:y=120,{flare(518,170,48,0,3.2,'0xff7a1a')},"
        f"{dt('(looking up)',34,'40','0xffd08a')},{sub('HERO: ...You’re not even the worst I’ve met.',0,3.2)},{push(1.0,1.1,d)},{bars()}[v]")
    d = 2.8
    add("k7", d, [lav("0x300800", "0xff6a10", d), still(f"{C}/mk00.png")],
        f"[0][1]overlay=x=380:y=-60:format=auto[o];[o]scale={W}:{H}[b];{flare(600,150,60,0,2.8,'0xffd040')},{dt('★★★★',120,'470','0xf5c542',en='gt(t\\,1.2)')},"
        f"{sub('KING: Mm. ... Four stars.',0,2.8)},{push(1.0,1.15,d)},{bars()}[v]" .replace("[0][1]overlay=x=380:y=-60:format=auto[o];[o]scale={W}:{H}[b];".format(W=W,H=H), "[1]scale=-1:640[k];[0][k]overlay=x=440:y=40,").replace("{flare","{flare"))
    d = 4.16
    add("k8", d, [lav("0x300800", "0xff6a10", d)],
        f"[0]{amouth(0,3.8).replace('y=440','y=300')},{dt('THE MAGMA KING',80,'100','0xffd08a')},{dt('UNDEFEATED IN SEATING',60,'500','white')},"
        f"{sub('ANNOUNCER: THE MAGMA KING: UNDEFEATED IN SEATING!',0,4.1)},{dt('Made with Hawkes Gen · hawkesgen.com',24,'650','0xe0c090',en='gt(t\\,2.5)')},{push(1.0,1.06,d)},{bars()}[v]")
    AUDIO = glob.glob(O + "/tableread/*-dialogue.mp3")[0]; TOTAL = 45.7
open(O + "/list.txt", "w").write("".join(f"file 'seg_{n}.mp4'\n" for n, _ in segs))
run("-f", "concat", "-safe", "0", "-i", O + "/list.txt", "-i", AUDIO, "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-t", str(TOTAL), O + "/animatic.mp4")
print("done", sum(d for _, d in segs))
