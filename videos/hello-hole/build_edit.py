import json
C=[("overlord","a_1b44a663c25fc9c4",0,2.5,"WS Overlord walks away"),
("ring","a_882bfa7425f826e6",0,1.5,"WS ring burns into empty meadow"),
("hole","a_d9df371b23f74251",0,1.04,"CU hero: That's a hole."),
("horn","a_66363cf706ff893f",0,2.0,"insert horn rolls past boot into hole"),
("where","a_ba610923eb3122ea",0,2.7,"CU hero: So that's where they keep coming from."),
("hello","a_be245dd8cd630504",0,0.7,"CU hero leans: Hello?"),
("echo1","a_013b18db8aa67246",0,1.5,"WS echo 1"),
("react1","a_301a7b9e74ea5ce5",0,0.7,"CU hero eyes widen"),
("echo2","a_a61b801607a3932a",0,1.5,"OTS echo 2 push-in"),
("react2","a_301a7b9e74ea5ce5",1.5,2.2,"CU hero flat again"),
("echo3","a_7540e33de3aba97e",0,1.6,"WS echo 3 fire column"),
("eyes","a_ba95f34b25471e23",0,1.2,"ECU eyes"),
("nope","a_2e05125641050f26",0,1.04,"CU hero: Nope."),
("suck","a_aef0653cc0fa115c",0,2.0,"WS hero sucked in"),
("annoyed","a_9b00959aaf0d824b",0,0.5,"CU annoyed"),
("yooba_in","a_6822c0d9baef0657",0,1.6,"WS Yooba bounces in"),
("yooba","a_89db2078bd4f2c34",0,2.375,"MS Yooba: Mmf. Wait for meee"),
("slurp","a_80bbe7ee27e2e851",0,1.0,"WS Yooba slurped"),
("seal","a_03edeb53416e7c77",0,2.2,"WS circle seals, diamond rolls"),
("diamond","a_5e3a9717ddf3f04d",0,1.3,"ECU diamond stops"),
("fall","a_a9cea53b651bd3c6",0,1.9,"WS hero falls into Floor 1000"),
("lava","a_949085fff03bdacf",0,1.3,"CU hero reacts to lava"),
("endcard","a_ee806a90e11d7125",0,1.5,"end card")]
t={};x=0;clips=[]
for k,a,i,o,l in C:
    t[k]=x;x+=o-i;clips.append({"asset":a,"in":i,"out":o,"beat":k,"label":l})
S={"hole":"a_d27fd45473b449ba","where":"a_feb9ee8bc422fb3c","hello":"a_b0a222aa1a35c61e","h1":"a_e082d7e4133610d6","h2":"a_6b57415c4f08cfb3","h3":"a_c413bae4b86f310c","nope":"a_79374fa234d063a8","yooba":"a_7ae678f7d3fcd83c","ann":"a_4958a99a4ad9bbe5"}
def au(a,s,sec,lab,d=False,**k):
    r={"asset":a,"start":round(s,2),"seconds":sec,"dialogue":d,"label":lab};r.update(k);return r
audio=[au(S["hole"],t["hole"],1.04,"HERO: That's a hole.",True),
au("a_15c36bf35336aa2c",t["horn"]+1.3,1.5,"clink"),
au(S["where"],t["where"],2.7,"HERO: So that's where they keep coming from.",True),
au(S["hello"],t["hello"],0.7,"HERO: Hello?",True),
au(S["h1"],t["echo1"],1.9,"HELL: Hello?",True),
au(S["h2"],t["echo2"],1.9,"HELL: Hello?",True),
au(S["h3"],t["echo3"],1.4,"HELL: HELLO.",True),
au(S["nope"],t["nope"],1.04,"HERO: Nope.",True),
au("a_c1b55709cd40bedf",t["suck"]-0.2,2.4,"inhale"),
au(S["yooba"],t["yooba"],2.3,"YOOBA: Mmf. Wait for meee—",True),
au("a_591686d5a805de0c",t["slurp"]+0.2,1.5,"slurp"),
au(S["ann"],t["fall"]+0.1,2.4,"ANNOUNCER: FLOOR ONE THOUSAND!",True),
au("a_bd4d069c1c0af8fa",t["suck"],round(x-t["suck"],2),"Hell drone",False,volume=0.5,fadeIn=1.5,fadeOut=1.2,duck=True)]
cap=lambda s,a,b:{"text":s,"start":round(t[a],2),"end":round(t[a]+b,2)}
caps=[cap("That's a hole.","hole",1.04),cap("So that's where they keep coming from.","where",2.7),cap("Hello?","hello",0.7),
cap("Hello?","echo1",1.4),cap("Hello?","echo2",1.4),cap("HELLO.","echo3",1.5),cap("Nope.","nope",1.04),
cap("Mmf. Wait for meee—","yooba",2.3),cap("FLOOR ONE THOUSAND!","fall",1.9),cap("SICK OF IT\nMade with Hawkes Gen · hawkesgen.ai","endcard",1.5)]
edit={"width":1080,"height":1920,"fps":24,"burn":True,"fit":"crop","clips":clips,"audio":audio,"captions":caps}
json.dump(edit,open("edit.json","w"),indent=1,ensure_ascii=False)
# compose inputs
ci=[c["asset"] for c in clips]; ai=[a["asset"] for a in audio]
tl={"clips":[{"clip":i,"in":c["in"],"out":c["out"]} for i,c in enumerate(clips)],
"audio":[{k2:v for k2,v in {"audio":i,"start":a["start"],"seconds":a["seconds"],"dialogue":a["dialogue"],"volume":a.get("volume"),"fadeIn":a.get("fadeIn"),"fadeOut":a.get("fadeOut"),"duck":a.get("duck")}.items() if v is not None} for i,a in enumerate(audio)],
"captions":[{"text":c["text"],"start":c["start"],"end":c["end"]} for c in caps]}
json.dump({"clips":ci,"audio":ai,"timeline":tl,"total":x,"t":t},open("/tmp/compose.json","w"))
print(x,t)
