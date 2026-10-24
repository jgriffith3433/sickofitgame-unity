"""v7: clean single-line voice takes (the table-read stems bled into each other), nothing truncated."""
import json,sys
e=json.load(open("edit.json"))
SUCK=sys.argv[1] if len(sys.argv)>1 else None   # optional new suck clip asset
if SUCK:
    for c in e["clips"]:
        if c["beat"]=="suck": c["asset"]=SUCK; c.pop("zoom",None)
A={a["label"]:a for a in e["audio"]}
def put(label,**k): A[label].update(k)
put("HERO: Hello?",asset="a_e2c8b8ec6284c0a0",start=7.55,seconds=0.95,fadeOut=0.1)
for i,s in ((0,8.44),(1,10.64)):
    pass
hell=[a for a in e["audio"] if a["label"].startswith("HELL")]
hell[0].update(seconds=1.45,fadeOut=0.25); hell[1].update(seconds=1.45,fadeOut=0.25); hell[2].update(seconds=1.3,fadeOut=0.2)
put("HERO: Nope.",asset="a_92ff1119f299fe4f",start=16.3,seconds=0.76)
put("YOOBA: Mmf. Wait for meee—",asset="a_b8fea8b12f82f2bc",start=21.48,seconds=2.27,fadeOut=0.05)
put("ANNOUNCER: FLOOR ONE THOUSAND!",asset="a_8b59e538267b535b",start=27.3,seconds=1.88)
put("diamond clink",seconds=0.8)
for c in e["captions"]:
    t=c["text"]
    if t=="Hello?": c["start"],c["end"]=7.55+0.2,8.44
    if t=="Nope.": c["start"],c["end"]=16.3,17.06
    if t.startswith("Mmf"): c["start"],c["end"]=21.48,23.75
    if t.startswith("FLOOR"): c["start"],c["end"]=27.3,29.18
json.dump(e,open("edit.json","w"),indent=1,ensure_ascii=False)
ci=[c["asset"] for c in e["clips"]]; ai=[a["asset"] for a in e["audio"]]
tl={"clips":[dict({"clip":i,"in":c["in"],"out":c["out"],"sound":False},**({"zoom":c["zoom"]} if "zoom" in c else {})) for i,c in enumerate(e["clips"])],
"audio":[{k:v for k,v in {"audio":i,"start":a["start"],"seconds":a["seconds"],"dialogue":a["dialogue"],"volume":a.get("volume"),"fadeIn":a.get("fadeIn"),"fadeOut":a.get("fadeOut"),"duck":a.get("duck")}.items() if v is not None} for i,a in enumerate(e["audio"])],
"captions":[{"text":c["text"],"start":c["start"],"end":c["end"]} for c in e["captions"]]}
json.dump({"clips":["asset:"+a for a in ci],"audio":["asset:"+a for a in ai],"timeline":tl},open("/tmp/compose.json","w"))
print(json.dumps({"clips":["asset:"+a for a in ci],"audio":["asset:"+a for a in ai]}))
print(json.dumps(tl))
