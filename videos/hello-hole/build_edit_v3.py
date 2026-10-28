import json
# v3: (key, asset, in, out, label, zoom)
Z=lambda a,b,x=0.5,y=0.5:{"from":a,"to":b,"x":x,"y":y}
C=[("open","a_9795d13796aa9174",0,1.5,"WS hero and Overlord: he walks away, lasers rake the grass",Z(1,1.12,0.5,0.4)),
("ring","a_882bfa7425f826e6",0,1.0,"WS ring burns into the empty meadow",None),
("hole","a_fe34085e2781dc92",0,1.04,"MCU hero blank stare: That's a hole.",None),
("horn","a_66363cf706ff893f",0,1.5,"insert horn rolls past boot into hole",None),
("where","a_cddb8e47d38eec22",0,2.7,"MCU hero dry half-smile: So that's where they keep coming from.",Z(1,1.22,0.5,0.25)),
("hello","a_be245dd8cd630504",0,0.7,"CU hero leans: Hello?",None),
("echo1","a_d08fba4ba7ef1418",0,1.5,"OTS hero at rim, echo 1, small calm glow",Z(1,1.08,0.7,0.7)),
("react1","a_301a7b9e74ea5ce5",0,0.7,"CU hero eyes widen",None),
("echo2","a_37817342b34a4fc9",0,1.5,"WS echo 2, rim flares, Yooba peeks from the hut",Z(1,1.2,0.5,0.75)),
("react2","a_301a7b9e74ea5ce5",1.5,2.2,"CU hero flat again",None),
("echo3","a_3e6e2715aa0b2f5c",0,1.5,"WS echo 3, fire column, hero blown back",Z(1,1.3,0.4,0.75)),
("flinch","a_e88d39ffaa90a88c",0,1.0,"MCU hero flinch, eyes wide",None),
("eyes","a_ba95f34b25471e23",0,0.9,"ECU eyes, then look to camera",None),
("nope","a_2e05125641050f26",0,1.04,"CU hero: Nope.",None),
("suck","a_4832c7defe1cef50",0,2.0,"WS the hole inhales, hero dragged off his feet, arms out",None),
("annoyed","a_9b00959aaf0d824b",0,0.5,"CU annoyed",None),
("yoobaIn","a_52a56264c4048288",0,1.6,"WS empty meadow, Yooba bounces in from the left",None),
("yooba","a_9e7fabfb671f3f8e",0,2.375,"MS Yooba, cheeks full: Mmf. Wait for meee",None),
("slurp","a_1d8dab1f7a14e568",0,1.2,"WS Yooba slurped in",None),
("seal","a_eb725b0a353e172b",0,1.4,"WS flash, the circle seals, diamond rolls out",None),
("diamond","a_eb725b0a353e172b",1.4,2.04,"ECU punch-in on the diamond stopping",Z(1,2.4,0.83,0.4)),
("fall","a_a9cea53b651bd3c6",0,1.9,"WS hero falls into Floor 1000",None),
("lava","a_949085fff03bdacf",0,1.0,"CU hero deadpan, lava glow",None),
("endcard","a_afb54f86203d8a19",0,1.5,"end card",None)]
t={};x=0;clips=[]
for k,a,i,o,l,z in C:
    t[k]=x;x+=o-i
    c={"asset":a,"in":i,"out":o,"beat":k,"label":l}
    if z:c["zoom"]=z
    clips.append(c)
S={"hole":"a_d27fd45473b449ba","where":"a_feb9ee8bc422fb3c","hello":"a_b0a222aa1a35c61e","h1":"a_e082d7e4133610d6","h2":"a_6b57415c4f08cfb3","h3":"a_c413bae4b86f310c","nope":"a_79374fa234d063a8","yooba":"a_7ae678f7d3fcd83c","ann":"a_4958a99a4ad9bbe5"}
def au(a,s,sec,lab,d=False,**k):
    r={"asset":a,"start":round(s,2),"seconds":sec,"dialogue":d,"label":lab};r.update(k);return r
audio=[au(S["hole"],t["hole"],1.04,"HERO: That's a hole.",True),
au("a_15c36bf35336aa2c",t["horn"]+1.0,1.5,"clink"),
au(S["where"],t["where"],2.7,"HERO: So that's where they keep coming from.",True),
au(S["hello"],t["hello"],0.7,"HERO: Hello?",True),
au(S["h1"],t["echo1"],1.9,"HELL: Hello?",True),
au(S["h2"],t["echo2"],1.9,"HELL: Hello?",True),
au(S["h3"],t["echo3"],1.4,"HELL: HELLO.",True),
au(S["nope"],t["nope"],1.04,"HERO: Nope.",True),
au("a_c1b55709cd40bedf",t["suck"]-0.2,2.4,"inhale"),
au(S["yooba"],t["yooba"],2.3,"YOOBA: Mmf. Wait for meee—",True),
au("a_591686d5a805de0c",t["slurp"]+0.2,1.5,"slurp"),
au("a_15c36bf35336aa2c",t["diamond"]+0.3,1.5,"diamond clink"),
au(S["ann"],t["fall"]+0.1,2.4,"ANNOUNCER: FLOOR ONE THOUSAND!",True),
au("a_bd4d069c1c0af8fa",t["suck"],round(x-t["suck"],2),"Hell drone",False,volume=0.5,fadeIn=1.5,fadeOut=1.2,duck=True)]
cap=lambda s,a,b:{"text":s,"start":round(t[a],2),"end":round(t[a]+b,2)}
caps=[cap("That's a hole.","hole",1.04),cap("So that's where they keep coming from.","where",2.7),cap("Hello?","hello",0.7),
cap("HELL:\nHello?","echo1",1.4),cap("HELL:\nHello?","echo2",1.4),cap("HELL:\nHELLO.","echo3",1.5),cap("Nope.","nope",1.04),
cap("Mmf. Wait for meee—","yooba",2.3),cap("FLOOR ONE THOUSAND!","fall",1.9),cap("Made with Hawkes Gen\nhawkesgen.ai","endcard",1.5)]
edit={"width":1080,"height":1920,"fps":24,"burn":True,"fit":"crop","clips":clips,"audio":audio,"captions":caps}
json.dump(edit,open("edit.json","w"),indent=1,ensure_ascii=False)
ci=[c["asset"] for c in clips]; ai=[a["asset"] for a in audio]
tl={"clips":[dict({"clip":i,"in":c["in"],"out":c["out"]},**({"zoom":c["zoom"]} if "zoom" in c else {})) for i,c in enumerate(clips)],
"audio":[{k2:v for k2,v in {"audio":i,"start":a["start"],"seconds":a["seconds"],"dialogue":a["dialogue"],"volume":a.get("volume"),"fadeIn":a.get("fadeIn"),"fadeOut":a.get("fadeOut"),"duck":a.get("duck")}.items() if v is not None} for i,a in enumerate(audio)],
"captions":[{"text":c["text"],"start":c["start"],"end":c["end"]} for c in caps]}
json.dump({"clips":ci,"audio":ai,"timeline":tl,"total":x,"t":t},open("/tmp/compose.json","w"))
print(x);print(json.dumps({"clips":["asset:"+a for a in ci],"audio":["asset:"+a for a in ai]}));print(json.dumps(tl))
