#!/usr/bin/env python3
"""Hell's Ad Break animatic, no GPU: the game's own art (Yooba's talk/laugh sheets, the hero's Shout sheet),
the table read's real voices, ffmpeg moves. Mouths: Yooba's sheet cells cycle on his lines; the announcer's
drawn mouth opens and closes on his; the hero is helmed (no mouth): his eye slits flare on his lines.
Usage: build_animatic.py <repo root>"""
import subprocess, sys, os, glob
R = sys.argv[1]; H = os.path.join(R, "videos/hell-ad-break"); O = H + "/out"; C = O + "/cells"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
TR = glob.glob(O + "/tableread/*-dialogue.mp3")[0]
W, Ht = 1280, 704
def run(*a):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *a], check=True)
def cells(sheet, cols, rows, prefix, n, cw=None, ch=None):
    from subprocess import check_output
    w, h = map(int, check_output(["ffprobe", "-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0:s=x", sheet]).decode().strip().split("x"))
    cw, ch = w // cols, h // rows
    for i in range(n):
        run("-i", sheet, "-vf", f"crop={cw}:{ch}:{(i%cols)*cw}:{(i//cols)*ch}", "-frames:v", "1", f"{C}/{prefix}{i:02d}.png")
cells(R + "/Assets/Art/Yooba/yooba_talk.png", 4, 4, "yt", 16)
cells(R + "/Assets/Art/Yooba/yooba_laugh.png", 4, 4, "yl", 16)
cells(R + "/Assets/Art/Sprites/Player/Shout.png", 6, 5, "hs", 25)
def bg(dur, c0, c1):
    return f"gradients=s={W}x{Ht}:c0={c0}:c1={c1}:x0=0:y0=0:x1=0:y1={Ht}:d={dur}:r=24"
def tv(label_in, text, dur, size=70, y="60"):
    pass
def seg(name, dur, inputs, fc, out="[v]"):
    args = []
    for i in inputs: args += i
    run(*args, "-filter_complex", fc, "-map", out, "-t", str(dur), "-r", "24", "-pix_fmt", "yuv420p", f"{O}/seg_{name}.mp4")
def dt(text, size, y, color="white", x="(w-text_w)/2", extra=""):
    t = text.replace("'", "’").replace(":", "\\:")
    return f"drawtext=fontfile={FONT}:text='{t}':fontsize={size}:fontcolor={color}:borderw=6:bordercolor=black:x={x}:y={y}{extra}"
def push(z0=1.0, z1=1.12, d=1, dur=3):
    n = int(dur * 24)
    return f"zoompan=z='{z0}+({z1}-{z0})*on/{n}':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s={W}x{Ht}:fps=24"
def tvframe():  # scanlines and a TV bezel: it is a cable ad break
    return f"drawbox=x=0:y=0:w={W}:h=14:color=black@0.9:t=fill,drawbox=x=0:y={Ht-14}:w={W}:h=14:color=black@0.9:t=fill,drawbox=x=0:y=0:w=14:h={Ht}:color=black@0.9:t=fill,drawbox=x={W-14}:y=0:w=14:h={Ht}:color=black@0.9:t=fill"
def loop(prefix, fps, n): return ["-framerate", str(fps), "-stream_loop", "-1", "-start_number", "0", "-i", f"{C}/{prefix}%02d.png"]
def lav(c0, c1, dur): return ["-f", "lavfi", "-i", bg(dur, c0, c1)]
S = [("s1", 5.48), ("s2", 6.04), ("s3", 7.52), ("s4", 2.80), ("s5", 2.96), ("s6", 2.16), ("s7", 3.28)]
D = dict(S)
# S1 announcer: the logo slams in, his mouth works
d = D["s1"]
seg("s1", d, [lav("0x2a0606", "0xc4350a", d)],
    f"[0]drawbox=x=380:y=330:w=520:h=22:color=0xf5c542:t=fill,{dt(chr(72)+'ELL’S AD BREAK', 78, '90', '0xf5c542')},{dt('1,000 FLOORS', 120, '190', 'white', extra=':alpha=1')},"
    f"drawbox=x=500:y='470':w=280:h='30+90*abs(sin(t*17))':color=0x7a0018:t=fill,drawbox=x=500:y=470:w=280:h=22:color=0xf3e6c8:t=fill,"
    f"{dt('THE ANNOUNCER', 34, '640', '0xf5c542')},{push(1.0, 1.10, 1, d)},{tvframe()}[v]")
# S2 announcer over the hero cycling his Shout poses, slammed words
d = D["s2"]
seg("s2", d, [lav("0x1a0404", "0xd8480c", d), loop("hs", 8, 25)],
    f"[1]scale=-1:560[h];[0][h]overlay=x=(W-w)/2+380:y=120,"
    f"{dt('FIRE!', 96, '110', 'white', x='90', extra=':enable=between(t\\,0.2\\,6)')},{dt('FIENDS!', 96, '250', 'white', x='90', extra=':enable=between(t\\,1.2\\,6)')},"
    f"{dt('FALLING ROCKS!', 96, '390', 'white', x='90', extra=':enable=between(t\\,2.4\\,6)')},{dt('NOW WITH MORE FLOORS', 54, '560', '0xf5c542', x='90', extra=':enable=between(t\\,3.9\\,6)')},"
    f"drawbox=x=90:y=640:w='min(1100\\,t*190)':h=14:color=0xf5c542:t=fill,{push(1.0, 1.08, 1, d)},{tvframe()}[v]")
# S3 Yooba talk sheet, sponsor card
d = D["s3"]
seg("s3", d, [lav("0x0e3a12", "0x2b9e33", d), loop("yt", 12, 16)],
    f"[1]scale=-1:520[y];[0]drawbox=x=0:y=0:w={W}:h=90:color=0x7a0018:t=fill,{dt('BROUGHT TO YOU BY YOOBA', 48, '20', '0xf5c542')}[b];"
    f"[b][y]overlay=x=140:y=140,{dt('DIAMONDS!', 84, '180', 'white', x='600')},{dt('FRESH. SHINY.', 44, '320', '0xf5c542', x='600')},{dt('LIGHTLY SWALLOWED.', 44, '380', '0xf5c542', x='600')},"
    f"{push(1.0, 1.12, 1, d)},{tvframe()}[v]")
# S4 hero deadpan: a still-ish pose, slow push, eye slits flare on the line
d = D["s4"]
seg("s4", d, [lav("0x120606", "0x5a1a08", d), ["-loop", "1", "-framerate", "24", "-i", f"{C}/hs00.png"]],
    f"[1]scale=-1:640[h];[0][h]overlay=x=(W-w)/2:y=40,drawbox=x=616:y=118:w=48:h='6+8*abs(sin(t*9))':color=0xff7a1a@0.9:t=fill,"
    f"{dt('You’re selling me the hole I’m stuck in.', 40, '600', 'white')},{push(1.0, 1.18, 1, d)},{tvframe()}[v]")
# S5 announcer ACT NOW flashing, mouth
d = D["s5"]
seg("s5", d, [lav("0x2a0606", "0xe0480c", d)],
    f"[0]{dt('ACT NOW!', 150, '150', 'white')},{dt('CLIMB THE WHOLE THING', 56, '340', '0xf5c542')},"
    f"drawbox=x=500:y=470:w=280:h='30+90*abs(sin(t*17))':color=0x7a0018:t=fill,drawbox=x=500:y=470:w=280:h=22:color=0xf3e6c8:t=fill,"
    f"drawbox=x=0:y=0:w={W}:h={Ht}:color=white@0.5:t=fill:enable='lt(mod(t\\,0.5)\\,0.06)',{push(1.0, 1.10, 1, d)},{tvframe()}[v]")
# S6 Yooba laughs
d = D["s6"]
seg("s6", d, [lav("0x0e3a12", "0x2b9e33", d), loop("yl", 12, 16)],
    f"[1]scale=-1:600[y];[0][y]overlay=x=(W-w)/2:y=60,{dt('Hee hee! He said yes!', 52, '590', 'white')},{push(1.0, 1.10, 1, d)},{tvframe()}[v]")
# S7 hero, held, ends on his line
d = D["s7"]
seg("s7", d, [lav("0x120606", "0x3a1206", d), ["-loop", "1", "-framerate", "24", "-i", f"{C}/hs02.png"]],
    f"[1]scale=-1:640[h];[0][h]overlay=x=(W-w)/2:y=40,{dt('...Great.', 96, '540', 'white')},{push(1.0, 1.10, 1, d)},{tvframe()}[v]")
lst = "".join(f"file 'seg_{n}.mp4'\n" for n, _ in S)
open(O + "/list.txt", "w").write(lst)
run("-f", "concat", "-safe", "0", "-i", O + "/list.txt", "-i", TR, "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-t", "30.2", O + "/animatic.mp4")
print("done")
