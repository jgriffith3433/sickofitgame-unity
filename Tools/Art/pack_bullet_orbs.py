import zlib,struct,sys
def read(p):
    d=open(p,'rb').read(); pos=8; idat=b''
    while pos<len(d):
        n,t=struct.unpack('>I4s',d[pos:pos+8]); c=d[pos+8:pos+8+n]
        if t==b'IHDR': w,h,bd,ct=struct.unpack('>IIBB',c[:10]); assert bd==8 and c[12]==0
        if t==b'IDAT': idat+=c
        pos+=12+n
    ch={2:3,6:4,0:1,4:2}[ct]; raw=zlib.decompress(idat); st=w*ch; out=[]; prev=bytearray(st); i=0
    for y in range(h):
        f=raw[i]; line=bytearray(raw[i+1:i+1+st]); i+=1+st
        for x in range(st):
            a=line[x-ch] if x>=ch else 0; b=prev[x]; c_=prev[x-ch] if x>=ch else 0
            if f==1: line[x]=(line[x]+a)&255
            elif f==2: line[x]=(line[x]+b)&255
            elif f==3: line[x]=(line[x]+((a+b)>>1))&255
            elif f==4:
                pa=abs(b-c_);pb=abs(a-c_);pc=abs(a+b-2*c_)
                pr=a if pa<=pb and pa<=pc else (b if pb<=pc else c_)
                line[x]=(line[x]+pr)&255
        out.append(line); prev=line
    px=[]
    for line in out:
        row=[]
        for x in range(w):
            q=line[x*ch:x*ch+ch]
            if ch>=3: row.append((q[0],q[1],q[2]))
            else: row.append((q[0],)*3)
        px.append(row)
    return w,h,px
def write(p,w,h,px):
    raw=b''.join(b'\0'+bytes(v for q in row for v in q) for row in px)
    def ck(t,c): return struct.pack('>I',len(c))+t+c+struct.pack('>I',zlib.crc32(t+c))
    open(p,'wb').write(b'\x89PNG\r\n\x1a\n'+ck(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+ck(b'IDAT',zlib.compress(raw,9))+ck(b'IEND',b''))
def down(px,w,h,f):
    o=[]
    for y in range(0,h-f+1,f):
        row=[]
        for x in range(0,w-f+1,f):
            s=[0,0,0]
            for dy in range(f):
                for dx in range(f):
                    q=px[y+dy][x+dx]; s[0]+=q[0];s[1]+=q[1];s[2]+=q[2]
            n=f*f; row.append((s[0]/n,s[1]/n,s[2]/n))
        o.append(row)
    return o
def alpha(px,k,off):
    o=[]
    for row in px:
        r=[]
        for (R,G,B) in row:
            m=max(R,G,B); a=max(0.,min(1.,(m-off)*k/255.))
            if a<=0: r.append((0,0,0,0)); continue
            s=255./(a*255.) if False else 1.0
            r.append((min(255,int(R/a)) if a>0 else 0,min(255,int(G/a)),min(255,int(B/a)),int(a*255)))
        o.append(r)
    return o
src='/disk/projects/.work/c_dd082bdd32f4/Assets/Art/Effects/Source/'
dst='/disk/projects/.work/c_dd082bdd32f4/Assets/Resources/Effects/'
import os; os.makedirs(dst,exist_ok=True)
tiles=[]
for n in ('orbCyan','orbMagenta','orbWhite'):
    w,h,px=read(src+n+'.png'); tiles.append(alpha(down(px,w,h,4),1.6,0))
atlas=[tiles[0][y]+tiles[1][y]+tiles[2][y] for y in range(128)]
write(dst+'BulletOrbs.png',384,128,atlas)
w,h,px=read(src+'rocketAmber.png')
# rocket: opaque where not black, crop, downscale 2x
a=alpha(px,255/20.,6)
# keep original colours for opaque pixels (alpha formula divides by a; fine)
xs=[x for r in a for x,q in enumerate(r) if q[3]>20]; ys=[y for y,r in enumerate(a) for q in r if q[3]>20]
x0,x1,y0,y1=min(xs)-4,max(xs)+5,min(ys)-4,max(ys)+5
x0=max(0,x0);y0=max(0,y0)
crop=[r[x0:x1] for r in a[y0:y1]]
H=len(crop);W=len(crop[0]);H-=H%2;W-=W%2
out=[]
for y in range(0,H,2):
    row=[]
    for x in range(0,W,2):
        q=[crop[y][x],crop[y][x+1],crop[y+1][x],crop[y+1][x+1]]
        al=sum(t[3] for t in q)/4
        if al<=0: row.append((0,0,0,0));continue
        row.append(tuple(int(sum(t[i]*t[3] for t in q)/(al*4)) for i in range(3))+(int(al),))
    out.append(row)
write(dst+'HeroRocket.png',W//2,H//2,out); print(W//2,H//2)
