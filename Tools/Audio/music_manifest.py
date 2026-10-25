#!/usr/bin/env python3
"""Validator for Tools/Audio/music_manifest.json (no Unity, no GPU).

  python3 Tools/Audio/music_manifest.py --check   exit 1 on a problem
  python3 Tools/Audio/music_manifest.py --price   what the 'to-make' tracks cost (the manifest's hawkesgen_estimate)

A 'to-make' track needs a prompt, bpm, key, seconds, takes, an out path and a stand-in clip that exists; an 'exists'
track's file must exist. No 'to-make' track may already have its file (it would be made twice).
"""
import json, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
m = json.load(open(os.path.join(ROOT, 'Tools/Audio/music_manifest.json'), encoding='utf-8'))
problems = []
seen = set()
for t in m['tracks']:
    i = t['id']
    if i in seen:
        problems.append(f'{i}: duplicate id')
    seen.add(i)
    made = os.path.exists(os.path.join(ROOT, t['out']))
    if t['status'] == 'exists':
        if not made:
            problems.append(f'{i}: marked exists but {t["out"]} is missing')
        continue
    for k in ('prompt', 'bpm', 'key', 'seconds', 'takes', 'out', 'standIn'):
        if not t.get(k):
            problems.append(f'{i}: no {k}')
    if made:
        problems.append(f'{i}: {t["out"]} exists, status should be "exists"')
    if t.get('standIn') and not os.path.exists(os.path.join(ROOT, 'Assets', t['standIn'])):
        problems.append(f'{i}: stand-in {t["standIn"]} is missing')
todo = [t for t in m['tracks'] if t['status'] == 'to-make']
takes = sum(t['takes'] for t in todo)
if takes != m['price']['takes']:
    problems.append(f'price is for {m["price"]["takes"]} takes, the tracks need {takes}: run hawkesgen_estimate again')
if '--price' in sys.argv:
    p = m['price']
    print(f'{len(todo)} tracks, {takes} takes: ${p["gpuUsd"]:.2f} GPU (${p["withIdleTailUsd"]:.2f} with the idle tail), ~{p["wallMinutes"]} min on {p["servers"]} server. Approval: {m["approval"]["gpu"]}')
for x in problems:
    print('MUSIC', x)
sys.exit(1 if problems else 0)
