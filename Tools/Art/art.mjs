#!/usr/bin/env node
// Makes an entity's animated art from its art spec (Assets/GameConfig/art/<id>.json).
// See Docs/ArtPipeline.md. One command, from description to animator:
//
//   node Tools/Art/art.mjs imp                 everything missing or out of date, then build + apply
//   node Tools/Art/art.mjs imp --start         ...and start a stopped GPU server if none is ready
//   node Tools/Art/art.mjs imp --redo attack   make this pose or clip again (new seed); several allowed
//   node Tools/Art/art.mjs imp --pick stand=123  use that take of a pose (from poses/_takes/)
//   node Tools/Art/art.mjs imp --build         no GPU: rebuild sheets from the clips there are, apply
//   node Tools/Art/art.mjs imp --status        what exists, what's out of date, what's next
//   node Tools/Art/art.mjs --list              every spec and its state
//   flags: --no-unity (don't run the Unity batch import), --takes N, --seed N
//
// Hero clips ("kind": "hero" in the spec, Assets/GameConfig/art/hero.json): the hero is not an entity, so there is no
// animator to write. Poses start from Idle's first frame (--hero-ref idle, or "referenceIdle": true), clips are made at
// 1280x720, then baked by Tools/Art/player_clip_sheet.py to 604x340 cells, centre pivot, 100 pixels per unit (the size of
// his Idle sheet, so he never changes size) into Assets/Art/Sprites/Player/, and the state is added to player.json
// (PlayerSceneClip falls back to a state's sheet, so a new state needs nothing else):
//   node Tools/Art/art.mjs hero --check                 no GPU: hero.json "wired" against player.json, the knight look, the bake size
//   node Tools/Art/art.mjs hero --hero-ref idle --only dash,wallSlide   make just those clips (GPU, priced, needs J's OK)
//   node Tools/Art/art.mjs hero --build                 bake the clips there are and wire them
//
// Picks can also be made in Hawkes Gen's library (Studio's Library tab, Window > Hawkes Gen > Library): each run first
// writes those into the work folder and ends by bringing new takes into the library (hawkes.library.json; SOI_LIBRARY=off
// skips it).
//
// Steps, each skipped when its output exists and its recipe still matches the spec:
//   stand   Qwen-Image-Edit 2511: the description (or a reference image) -> standing side view, facing right
//   poses   Qwen-Image-Edit 2511: the standing view -> every other pose the clips start or end on
//   clips   Wan 2.2 first-last-frame: pose -> pose, as the clip's action says
//   build   key the green, align on the feet, pack sheets into Assets/Art/Enemies/<Name>/ (+ previews)
//   apply   write visual.animator into content/entities/<entity>.json, then import (Unity batch)
//
// GPU work goes through Hawkes Gen (C:\Projects\hawkesgen, http://127.0.0.1:8790; start it with its
// auto-start.bat): image.edit for poses, video.i2v for clips, project "sickofit". It owns the GPU
// fleet, the ComfyUI graphs and the tuned settings (its tool defaults are Dose Of Destruction's).
// Keying and cutout still use DoD's Python (DOD_ROOT, default C:\Projects\doseofdestructionwebsite).
// Nothing secret is read or written here.

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { HawkesGen } from './hawkesgen.mjs';
import { loadMapping, importFolders, exportFolders, slugOf } from './hawkesgen-folders.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const DOD = process.env.DOD_ROOT || 'C:\\Projects\\doseofdestructionwebsite';
const SPECS = path.join(REPO, 'Assets', 'GameConfig', 'art');
const ENTITIES = path.join(REPO, 'Assets', 'GameConfig', 'content', 'entities');
const SHEETS = path.join(REPO, 'Assets', 'Art', 'Enemies');
const PLAYER_SHEET_PY = path.join(REPO, 'Tools', 'Art', 'player_clip_sheet.py');
const PLAYER_JSON = path.join(REPO, 'Assets', 'GameConfig', 'player.json');
const HERO_SHEETS = path.join(REPO, 'Assets', 'Art', 'Sprites', 'Player');
const HERO_BAKE = { cell: [604, 340], ppu: 100, video: [1280, 720] };
// The hero's source videos (Idle.mp4, Walk.mp4, ... : SickOfItGamePlayer/<State>/Output/<State>.mp4). "player:Walk" in a spec names one:
// a hero stand's reference (its first frame) or a clip's driving "motion" (video.animate).
const PLAYER_SRC = process.env.SOI_PLAYER_SRC
  ?? [path.join(REPO, '..', 'SickOfItGamePlayer'), 'C:\\Projects\\SickOfItGamePlayer'].find((p) => fs.existsSync(p))
  ?? path.join(REPO, '..', 'SickOfItGamePlayer');
const SHEETS_PY = path.join(REPO, 'Tools', 'Art', 'sheets.py');
const UNITY = process.env.UNITY_EXE || 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.7f1\\Editor\\Unity.exe';
// SOI_ART_PYTHON: any Python with Pillow + numpy (the Linux server has no DoD .venv; rembg is only needed for takes
// whose backdrop isn't flat, see preflight).
const PYTHON = process.env.SOI_ART_PYTHON
  ?? [path.join(DOD, '.venv', 'Scripts', 'python.exe'), path.join(DOD, '.venv', 'bin', 'python')].find((p) => fs.existsSync(p))
  ?? (process.platform === 'win32' ? 'python' : 'python3');

// J (2026-10-25): one take of every pose and clip; a spec's "takes" is capped at this unless --takes N asks for more.
// Judge the single take and remake (--redo) only one that is clearly wrong. SOI_ART_TAKES raises the cap.
const TAKES_CAP = Math.max(1, Number(process.env.SOI_ART_TAKES) || 1);

/** What a spec leaves out. Assets/GameConfig/art/_defaults.json overrides these for every enemy. */
const DEFAULTS = {
  style: 'Dark fantasy video game character art: painterly, detailed, gritty, warm rim light from hellfire, readable silhouette.',
  background: 'Plain flat pure green background (#00ff00), no floor, no shadow, no scenery, even studio lighting.',
  takes: { stand: 1, pose: 1, clip: 1 },
  frameSize: 640,
  frameFill: 0.62,
  fps: 12,
  // Wan's negative prompt (from DoD's generation.json defaults); a clip's own "negative" is added to it.
  negative: '色调艳丽，过曝，静态，细节模糊不清，字幕，风格，作品，画作，画面，静止，整体发灰，最差质量，低质量，JPEG压缩残留，丑陋的，残缺的，多余的手指，画得不好的手部，画得不好的脸部，畸形的，毁容的，形态畸形的肢体，手指融合，静止不动的画面，杂乱的背景，三条腿，背景人很多，倒着走, camera movement, zoom, pan, cut, background change, floor, shadow, extra limbs',
  prompts: {
    standFromText: 'Paint {description}. Full-body side profile view, turned to face right, centered in the frame with plenty of empty space around it; the whole body is visible from head to toe. A single character, nothing else. {style} {background}',
    standFromReference: 'Show this exact creature in full-body side profile view, turned to face right. Keep its design, colors and proportions. {description} The whole body is visible from head to toe, centered, with empty space around it. {style} {background}',
    pose: 'Image 1 is this creature, side view, facing right. Show the exact same creature {action}. Keep everything about it the same as image 1: face, body, colors, proportions and art style, and the same size and position in the frame. Side view, facing right. The whole body is visible{ground}. {background}',
    clip: '{name}, {description}, {action}. {frames} Side view, facing right, full body, staying in place in the center of the frame. Locked camera, no camera movement, no zoom. {background} Clear, snappy motion for a 2D arcade game.',
  },
};

// Animator state for each clip name; the engine knows these (Docs/ArtPipeline.md "Wiring"). Any other clip name is a
// state of its own ("state", default the name in PascalCase), wired by the spec's "animator" graph.
const STATE = { idle: 'Idle', move: 'Move', attack: 'Attack', hurt: 'Hurt', die: 'Die' };
const stateOf = (spec, name) => spec.clips[name]?.state ?? STATE[name] ?? pascal(name);
const LOOPS = new Set(['idle', 'move']);

// ------------------------------------------------------------------ helpers

const sha = (v) => crypto.createHash('sha1').update(typeof v === 'string' ? v : JSON.stringify(v)).digest('hex').slice(0, 12);
const readJson = (f, fallback) => { try { return JSON.parse(fs.readFileSync(f, 'utf8')); } catch { return fallback; } };
const writeJson = (f, v) => { fs.mkdirSync(path.dirname(f), { recursive: true }); fs.writeFileSync(f, JSON.stringify(v, null, 1)); };
const pascal = (id) => id.replace(/(^|[-_\s])(\w)/g, (_, __, c) => c.toUpperCase()).replace(/^\w/, (c) => c.toUpperCase());
const log = (...a) => console.log(...a);

function fill(template, vars) {
  return String(template)
    .replace(/\{(\w+)\}/g, (_, k) => vars[k] ?? '')
    .replace(/\s*,\s*(?=[,.])/g, '')
    .replace(/\.\s*\./g, '.')
    .replace(/\s{2,}/g, ' ')
    .trim();
}

function run(cmd, args, { quiet = false } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { cwd: REPO, env: { ...process.env, PYTHONIOENCODING: 'utf-8', DOD_ROOT: DOD } });
    let out = '';
    let err = '';
    child.stdout.on('data', (d) => (out += d));
    child.stderr.on('data', (d) => { err += d; if (!quiet) process.stderr.write(d); });
    child.on('error', reject);
    child.on('close', (code) => (code === 0 ? resolve(out) : reject(Object.assign(new Error((err || out).trim().slice(-800) || `${path.basename(cmd)} exited ${code}`), { code }))));
  });
}
const py = async (...args) => JSON.parse((await run(PYTHON, [SHEETS_PY, ...args])).trim().split(/\r?\n/).pop());

// ------------------------------------------------------------------ the spec

function loadSpec(id) {
  const file = path.join(SPECS, `${id}.json`);
  if (!fs.existsSync(file)) throw new Error(`No art spec ${path.relative(REPO, file)}. Write one (Docs/ArtPipeline.md has the format).`);
  const raw = readJson(file, null);
  if (!raw) throw new Error(`${path.relative(REPO, file)} is not valid JSON.`);
  const base = readJson(path.join(SPECS, '_defaults.json'), {});
  const d = { ...DEFAULTS, ...base, takes: { ...DEFAULTS.takes, ...base.takes }, prompts: { ...DEFAULTS.prompts, ...base.prompts } };
  const spec = { ...d, ...raw, takes: { ...d.takes, ...raw.takes }, prompts: { ...d.prompts, ...raw.prompts } };
  spec.id = id;
  spec.raw = raw;
  spec.hero = raw.kind === 'hero';
  if (spec.hero) {
    spec.entity = 'player';
    spec.background = raw.background ?? 'Plain flat pure black background (#000000), no floor, no shadow, no scenery, even lighting.';
    spec.bake = { cell: raw.bake?.cell ?? HERO_BAKE.cell, ppu: raw.bake?.ppu ?? HERO_BAKE.ppu };
  }
  spec.entity ??= id;
  spec.name ??= pascal(id);
  spec.prefix ??= pascal(spec.entity);
  // A pose is a phrase, or { "from": "<pose>", "action": "..." } to be made from another pose instead of stand (a boss that
  // stands up: poses edited from a seated stand stay seated).
  spec.poseBase = {};
  spec.posePrompt = {};
  spec.poseRefs = {};
  spec.poseTool = {};
  spec.poseCanvas = {};
  spec.poses = { stand: 'standing in its normal stance' };
  for (const [pose, v] of Object.entries(raw.poses ?? {})) {
    spec.poses[pose] = typeof v === 'string' ? v : v.action;
    if (typeof v === 'object' && v.from && pose !== 'stand') spec.poseBase[pose] = v.from;
    // "prompt" replaces the pose template ("Show the exact same creature..."), e.g. to take him out of the picture.
    if (typeof v === 'object' && v.prompt && pose !== 'stand') spec.posePrompt[pose] = v.prompt;
    // "refs": other poses shown to image.edit as images 2 and 3 (the hero's weapon, made first as a pose of its own);
    // "tool": "image.generate" paints the pose from its prompt alone (a weapon concept), with no image in.
    if (typeof v === 'object' && Array.isArray(v.refs) && pose !== 'stand') spec.poseRefs[pose] = v.refs;
    if (typeof v === 'object' && v.tool && pose !== 'stand') spec.poseTool[pose] = v.tool;
    // "canvas": "00ff00" paints the pose from its prompt alone with image.edit over a flat canvas of that colour (no base pose).
    if (typeof v === 'object' && v.canvas && pose !== 'stand') { spec.poseTool[pose] = 'canvas'; spec.poseCanvas[pose] = v.canvas; }
  }
  spec.clips = raw.clips ?? {};
  const problems = [];
  if (!spec.description) problems.push('"description" is required');
  if (!(spec.height > 0)) problems.push('"height" (world units the standing figure is tall) is required');
  if (!spec.clips.idle && !spec.hero) problems.push('"clips" needs at least "idle"');
  for (const [name, c] of Object.entries(spec.clips)) {
    if (spec.hero && !c.state) problems.push(`clip "${name}": a hero clip needs "state" (its player.json stateName)`);
    if (!spec.hero && !STATE[name] && !spec.raw.animator?.transitions) problems.push(`clip "${name}": use one of ${Object.keys(STATE).join(', ')}, or wire it in the spec's "animator" graph`);
    if (c.video) continue;
    for (const k of ['from', 'to']) if (c[k] && !spec.poses[c[k]]) problems.push(`clip "${name}": no pose "${c[k]}" in "poses"`);
    if (!c.action) problems.push(`clip "${name}": "action" is required (what happens in it)`);
  }
  for (const [pose, base] of Object.entries(spec.poseBase)) {
    const order = Object.keys(spec.poses);
    if (!spec.poses[base] || order.indexOf(base) > order.indexOf(pose)) problems.push(`pose "${pose}": "from" must be a pose listed before it`);
  }
  for (const [pose, refs] of Object.entries(spec.poseRefs)) {
    const order = Object.keys(spec.poses);
    for (const r of refs) if (!spec.poses[r] || order.indexOf(r) > order.indexOf(pose)) problems.push(`pose "${pose}": ref "${r}" must be a pose listed before it`);
  }
  for (const [name, c] of Object.entries(spec.clips)) if (c.motion && !fs.existsSync(resolveMotion(c.motion))) problems.push(`clip "${name}": motion ${c.motion} not found (${resolveMotion(c.motion)})`);
  if (problems.length) throw new Error(`${path.relative(REPO, file)}:\n  ${problems.join('\n  ')}`);
  return spec;
}

function workRoot() {
  // SOI_ART_WORK overrides; a Windows path in game.json means nothing elsewhere (the cloud server), so there the work
  // folder sits beside the repo.
  if (process.env.SOI_ART_WORK) return process.env.SOI_ART_WORK;
  const game = readJson(path.join(REPO, 'Assets', 'GameConfig', 'game.json'), {});
  const root = game.characterArt?.enemySourceRoot || 'C:\\Projects\\SickOfItGameEnemies';
  if (process.platform !== 'win32' && /^[A-Za-z]:\\/.test(root)) return path.join(REPO, '..', 'SickOfItGameEnemies', 'Generated');
  return path.join(root, 'Generated');
}

/**
 * Before anything is paid for: the Python that keys and cuts out (Dose Of Destruction's .venv, DOD_ROOT) must import,
 * and a reference image must be a real file (not a Git LFS pointer). Fails with what's missing.
 */
async function preflight(spec) {
  if (spec.hero) return; // Pillow and numpy only; the DoD keying isn't used (black plate, player_clip_sheet.py)
  const probe = [
    'import json, sys, os',
    'missing = []',
    "for m in ('PIL', 'numpy'):",
    '    try: __import__(m)',
    '    except Exception as e: missing.append(f"{m} ({e.__class__.__name__})")',
    "sys.path[:0] = [os.environ['DOD_ROOT'], os.path.join(os.environ['DOD_ROOT'], 'tools')]",
    "for m in ('_build_anims', 'cutout'):",
    '    try: __import__(m)',
    '    except Exception as e: missing.append(f"DoD {m} ({e.__class__.__name__}: {e})")',
    'try: __import__("rembg"); cut = True',
    'except Exception: cut = False',
    'print(json.dumps({"missing": missing, "rembg": cut}))',
  ].join('\n');
  let missing;
  try {
    const r = JSON.parse((await run(PYTHON, ['-c', probe], { quiet: true })).trim().split(/\r?\n/).pop());
    missing = r.missing;
    // Qwen paints on flat green, which sheets.py keys by colour; BiRefNet (rembg) is only for a take whose backdrop
    // isn't flat, and without it such a take is keyed by colour anyway (and usually scores low).
    if (!r.rembg) log('note: rembg is not installed here; stills are keyed by colour only (fine on a flat green backdrop)');
  } catch (e) {
    missing = [`python (${PYTHON}): ${e.message.split('\n')[0]}`];
  }
  if (missing.length) {
    throw new Error(`The art pipeline's Python isn't ready, so nothing was sent to the GPU: ${missing.join('; ')}. It uses Dose Of Destruction's .venv (DOD_ROOT=${DOD}, now ${PYTHON}). Run it where that exists (J's PC), or set DOD_ROOT.`);
  }
  const ref = spec.reference ? resolveAsset(spec.reference) : null;
  if (ref && fs.existsSync(ref) && fs.statSync(ref).size < 1024 && fs.readFileSync(ref, 'utf8').startsWith('version https://git-lfs')) {
    throw new Error(`reference ${spec.reference} is a Git LFS pointer, not the image: run git lfs pull (git lfs pull --include "${path.relative(REPO, ref).replace(/\\/g, '/')}")`);
  }
}

function paths(spec) {
  const work = path.join(workRoot(), spec.id);
  return {
    work,
    pose: (p) => path.join(work, 'poses', `${p}.png`),
    poseTakes: path.join(work, 'poses', '_takes'),
    framed: (p) => path.join(work, 'framed', `${p}.png`),
    frameJson: path.join(work, 'framed', 'frame.json'),
    clip: (c) => path.join(work, 'clips', `${c}.mp4`),
    clipTakes: path.join(work, 'clips', '_takes'),
    recipe: (f) => f.replace(/\.[^.]+$/, '.json'),
    sheets: path.join(SHEETS, spec.prefix),
  };
}

/** What each pose and clip would be made from now; a file whose recipe has another key is out of date. */
function keys(spec, P) {
  const k = {};
  const standSrc = spec.reference ? resolveAsset(spec.reference) : null;
  k.stand = sha({ d: spec.description, s: spec.style, b: spec.background, a: spec.poses.stand, r: standSrc && fs.existsSync(standSrc) ? fs.statSync(standSrc).size : null });
  for (const pose of Object.keys(spec.poses)) {
    if (pose === 'stand') continue;
    const base = spec.poseBase[pose];
    const p = spec.posePrompt[pose];
    const refs = spec.poseRefs[pose];
    const tool = spec.poseTool[pose];
    k[`pose:${pose}`] = sha({ ...(tool ? { tool } : base ? { base: poseKey(k, base) } : { stand: k.stand }), a: spec.poses[pose], b: spec.background, ...(p ? { p } : {}), ...(refs ? { refs: refs.map((r) => poseKey(k, r)) } : {}) });
  }
  for (const [name, c] of Object.entries(spec.clips)) {
    if (c.video) continue;
    const from = c.from ?? 'stand';
    const to = c.to ?? from;
    k[`clip:${name}`] = sha({ from: poseKey(k, from), to: poseKey(k, to), p: clipPrompt(spec, name, c), s: c.seconds ?? 2, n: spec.frameSize, f: spec.frameFill, ...(c.motion ? { m: c.motion, mf: c.motionFrames ?? null } : {}), ...(c.motionVia ? { via: c.motionVia } : {}) });
  }
  return k;
}
const poseKey = (k, pose) => (pose === 'stand' ? k.stand : k[`pose:${pose}`]);
const upToDate = (file, key) => fs.existsSync(file) && readJson(file.replace(/\.[^.]+$/, '.json'), {}).key === key;

/** "player:Walk" = the hero's source video SickOfItGamePlayer/Walk/Output/Walk.mp4; anything else is a path (absolute, or under Assets/). */
function resolveMotion(ref) {
  const m = /^player:(\w+)$/.exec(ref);
  if (m) return path.join(PLAYER_SRC, m[1], 'Output', `${m[1]}.mp4`);
  return resolveAsset(ref);
}

function resolveAsset(ref) {
  if (/^player:/.test(ref)) return resolveMotion(ref);
  const file = ref.split('#')[0];
  return path.isAbsolute(file) ? file : path.join(REPO, 'Assets', file);
}

function clipPrompt(spec, name, c) {
  const from = c.from ?? 'stand';
  const to = c.to ?? from;
  const frames = c.motion ? '' : from === to ? `It starts and ends ${spec.poses[from]}.` : `It starts ${spec.poses[from]} and ends ${spec.poses[to]}.`;
  return fill(c.prompt ?? spec.prompts.clip, { name: spec.name, description: spec.description, action: c.action, frames, background: spec.background, style: spec.style });
}

// ------------------------------------------------------------------ the GPU (through Hawkes Gen)

const PROJECT = 'sickofit';
const WAN_FPS = 16; // what video.i2v makes; frames are counted at this rate

let gen;
/** The Hawkes Gen client, once a GPU server is running (or starting) to take the jobs. */
async function hawkes({ start }) {
  if (gen) return gen;
  const g = new HawkesGen({ project: PROJECT });
  await g.health();
  let fleet = null;
  try {
    fleet = await g.fleet();
  } catch {
    // A key without fleet rights: just queue; the job says what it waits for.
  }
  const up = fleet?.servers?.filter((s) => s.state === 'pending' || s.state === 'running') ?? [];
  if (fleet && !up.length && !fleet.settings?.auto) {
    if (!start) {
      throw new Error(`No GPU server is running (${fleet.servers?.length ?? 0} stopped). Run again with --start to start one (an L40S, about $2/hour; it stops itself when idle), or start one in Hawkes Gen's dashboard (${g.url}, Fleet tab).`);
    }
    let id = null;
    for (const s of fleet.servers.filter((x) => x.state === 'stopped')) {
      try {
        log(`starting GPU server ${s.id} (${s.region}) through Hawkes Gen...`);
        await g.fleetAction('start', { id: s.id, confirm: true });
        id = s.id;
        break;
      } catch (e) {
        log(`  ${s.id}: ${e.message}`);
      }
    }
    if (!id) throw new Error('No stopped GPU server could start (AWS capacity). Try again in a few minutes.');
    log(`  ${id} is starting; the jobs wait in Hawkes Gen's queue until it is ready (a few minutes)`);
  }
  gen = g;
  return gen;
}

/**
 * The seed of take i: --seed counts up from it; --redo picks new random ones; otherwise it comes
 * from the recipe key, so a rerun after a crash sends the same job keys and Hawkes Gen hands back
 * the jobs it already ran instead of paying again.
 */
function seedFor(opts, i, key, fresh) {
  if (opts.seed) return opts.seed + i;
  if (fresh) return crypto.randomInt(1, 2 ** 31);
  return (parseInt(sha(`${key}:${i}`).slice(0, 8), 16) % (2 ** 31 - 1)) + 1;
}

/** Queues one job per take (each with its own key), waits for all, saves each output to file(seed). */
async function takes(tool, { label, n, seeds, key, inputs, params, labels, file }) {
  const jobs = await gen.submitMany(seeds.slice(0, n).map((seed) => ({
    tool, inputs, params: { ...params, seed, count: 1 }, labels, key: `${PROJECT}:${key}:${seed}`,
  })));
  const t = Date.now();
  const results = await Promise.allSettled(jobs.map((job, i) => gen.wait(job.id, {
    onUpdate: (j) => log(`  ${label} take ${i + 1}/${n}: ${j.status}${j.detail ? ` (${j.detail})` : ''}`),
  })));
  const out = [];
  for (const r of results) {
    if (r.status === 'rejected') { log(`  ${label}: ${r.reason.message}`); continue; }
    const o = r.value.outputs?.find((x) => !/comparison/i.test(x.title ?? '')) ?? r.value.outputs?.[0]; // video.motion also returns a side-by-side
    if (!o) { log(`  ${label}: job ${r.value.id} saved nothing`); continue; }
    const dest = file(o.seed);
    await gen.download(o.asset, dest);
    out.push({ file: dest, seed: o.seed, job: r.value.id, asset: o.asset });
  }
  if (!out.length) throw new Error(`${label}: no take finished (see the jobs in Hawkes Gen's dashboard)`);
  log(`  ${label}: ${out.length} of ${n} takes in ${Math.round((Date.now() - t) / 1000)}s`);
  return out;
}

/** Makes takes of a still with Qwen (image.edit) and keeps the best-scoring one as the pose. */
async function makePose(spec, P, pose, key, opts) {
  if (spec.hero && pose === 'stand') return makeHeroStand(spec, P, key);
  await hawkes(opts);
  let input;
  let prompt;
  if (pose === 'stand') {
    const ref = spec.reference ? resolveAsset(spec.reference) : null;
    input = path.join(P.work, 'framed', '_input.png');
    fs.mkdirSync(path.dirname(input), { recursive: true });
    if (ref) {
      if (!fs.existsSync(ref)) throw new Error(`reference ${spec.reference} not found`);
      await run(PYTHON, [SHEETS_PY, 'frame', ref, input, '1024', '0.8', input.replace(/\.png$/, '.json')], { quiet: true });
      prompt = fill(spec.prompts.standFromReference, { description: spec.description, style: spec.style, background: spec.background });
    } else {
      await run(PYTHON, [SHEETS_PY, 'blank', input, '1024']);
      prompt = fill(spec.prompts.standFromText, { description: `${spec.description}, ${spec.poses.stand}`, style: spec.style, background: spec.background });
    }
  } else {
    input = P.pose(spec.poseBase[pose] ?? 'stand');
    const air = /air|fly|hover|float|leap|jump/i.test(`${spec.poses[pose]} ${spec.poses.stand}`);
    prompt = fill(spec.posePrompt[pose] ?? spec.prompts.pose, { action: spec.poses[pose], background: spec.background, ground: air ? '' : ', feet on the same ground line as image 1', description: spec.description, style: spec.style });
  }
  const n = opts.takes ?? Math.min(pose === 'stand' ? spec.takes.stand : spec.takes.pose, TAKES_CAP);
  const seeds = Array.from({ length: n }, (_, i) => seedFor(opts, i, key, opts.redo.has(pose)));
  fs.mkdirSync(P.poseTakes, { recursive: true });
  const tool = spec.poseTool[pose] ?? 'image.edit';
  const params = { prompt };
  let images = [input, ...(spec.poseRefs[pose] ?? []).map((r) => P.pose(r))];
  if (tool === 'image.generate') { params.width = 1024; params.height = 1024; images = null; }
  else if (tool === 'canvas') {
    const canvas = path.join(P.poseTakes, `canvas-${spec.poseCanvas[pose]}.png`);
    fs.mkdirSync(P.poseTakes, { recursive: true });
    if (!fs.existsSync(canvas)) await run(PYTHON, ['-c', `from PIL import Image; Image.new('RGB', (1024, 1024), '#${spec.poseCanvas[pose]}').save(r'${canvas}')`]);
    images = [canvas];
    params.width = 1024; params.height = 1024;
  }
  // The hero's poses stay 1280x720 like his videos, so he keeps his size and place in the frame (feet on Idle's ground line).
  else if (spec.hero) { params.width = HERO_BAKE.video[0]; params.height = HERO_BAKE.video[1]; }
  const made = await takes(tool === 'canvas' ? 'image.edit' : tool, {
    label: pose, n, seeds, key: `${spec.id}:${pose}:${key}`, inputs: images ? { images } : {}, params,
    labels: { entity: spec.entity, pose }, file: (seed) => path.join(P.poseTakes, `${pose}-${seed}.png`),
  });
  if (spec.hero) {
    // Black plate: no green to key, so no cutout score; the first take stands in until someone picks (--pick).
    for (const m of made) writeJson(P.recipe(m.file), { key, pose, seed: m.seed, prompt, tool: 'image.edit', settings: params, job: m.job, asset: m.asset, made: new Date().toISOString() });
    return choosePose(P, pose, made[0].file);
  }
  const scored = [];
  for (const m of made) {
    const cut = await py('cutout', m.file, m.file.replace(/\.png$/, '.cut.png'));
    writeJson(P.recipe(m.file), { key, pose, seed: m.seed, prompt, tool: 'image.edit', settings: params, job: m.job, asset: m.asset, score: cut.score, why: cut.why, made: new Date().toISOString() });
    log(`    seed ${m.seed}: score ${cut.score} (${cut.why})`);
    scored.push({ file: m.file, score: cut.score });
  }
  const best = scored.reduce((a, b) => (b.score > a.score ? b : a));
  if (best.score < 0.5) log(`  warning: the best ${pose} take scores ${best.score}; check ${best.file} or --redo ${pose}`);
  choosePose(P, pose, best.file);
}

/** The hero's stand is Idle's first frame, not a generated pose: it is what he looks like on screen. */
async function makeHeroStand(spec, P, key) {
  fs.mkdirSync(path.dirname(P.pose('stand')), { recursive: true });
  const ref = spec.reference ? resolveAsset(spec.reference) : null;
  if (/^player:/.test(spec.reference ?? '')) await run('ffmpeg', ['-v', 'error', '-y', '-i', ref, '-frames:v', '1', P.pose('stand')], { quiet: true });
  else if (spec.referenceIdle) await run(PYTHON, [PLAYER_SHEET_PY, '--idle-reference', ref, P.pose('stand')], { quiet: true });
  else if (ref && fs.existsSync(ref)) fs.copyFileSync(ref, P.pose('stand'));
  else throw new Error(`hero reference ${spec.reference} not found`);
  writeJson(P.recipe(P.pose('stand')), { key, pose: 'stand', source: spec.reference, referenceIdle: Boolean(spec.referenceIdle), made: new Date().toISOString() });
  log(`  stand: Idle's first frame (${spec.reference}) at ${HERO_BAKE.video.join('x')}`);
}

function choosePose(P, pose, take) {
  fs.mkdirSync(path.dirname(P.pose(pose)), { recursive: true });
  fs.copyFileSync(take, P.pose(pose));
  fs.copyFileSync(P.recipe(take), P.recipe(P.pose(pose)));
  log(`  ${pose}: using ${path.basename(take)}`);
}

async function frameAll(spec, P) {
  await py('frame', P.pose('stand'), P.framed('stand'), String(spec.frameSize), String(spec.frameFill), P.frameJson);
  for (const pose of Object.keys(spec.poses)) {
    if (pose !== 'stand' && fs.existsSync(P.pose(pose))) await py('frame-like', P.pose(pose), P.framed(pose), P.frameJson);
  }
}

let wanParams;
/** A clip's "settings" minus anything video.i2v doesn't take (Hawkes Gen rejects unknown params). */
async function clipSettings(name, settings = {}) {
  wanParams ??= Object.keys((await gen.tool('video.i2v')).params ?? {});
  const out = {};
  for (const [k, v] of Object.entries(settings)) {
    if (wanParams.includes(k)) out[k] = v;
    else log(`  ${name}: setting "${k}" isn't a video.i2v param (${wanParams.join(', ')}); ignored`);
  }
  return out;
}

/** Makes takes of a clip with Wan 2.2 first-last-frame (video.i2v) and uses the first. */
async function makeClip(spec, P, name, key, opts) {
  await hawkes(opts);
  const clip = spec.clips[name];
  const from = clip.from ?? 'stand';
  const to = clip.to ?? from;
  const prompt = clipPrompt(spec, name, clip);
  const frames = Math.max(5, Math.round(((clip.seconds ?? 2) * WAN_FPS) / 4) * 4 + 1);
  const negative = [spec.negative, clip.negative].filter(Boolean).join(', ');
  const [vw, vh] = spec.hero ? HERO_BAKE.video : [spec.frameSize, spec.frameSize];
  const params = { prompt, negative, width: vw, height: vh, frames, fps: WAN_FPS, ...(await clipSettings(name, clip.settings)) };
  const n = opts.takes ?? Math.min(spec.takes.clip, TAKES_CAP);
  const seeds = Array.from({ length: n }, (_, i) => seedFor(opts, i, key, opts.redo.has(name)));
  fs.mkdirSync(P.clipTakes, { recursive: true });
  if (clip.motion) return makeMotionClip(spec, P, name, key, { prompt, negative, vw, vh, n, seeds, from });
  const made = await takes('video.i2v', {
    label: `${name} (${frames} frames)`, n, seeds, key: `${spec.id}:${name}:${key}`,
    inputs: spec.hero ? { start: P.pose(from), end: P.pose(to) } : { start: P.framed(from), end: P.framed(to) }, params,
    labels: { entity: spec.entity, clip: name }, file: (seed) => path.join(P.clipTakes, `${name}-${seed}.mp4`),
  });
  for (const m of made) {
    writeJson(P.recipe(m.file), { key, clip: name, from, to, seed: m.seed, prompt, negative, tool: 'video.i2v', settings: params, job: m.job, asset: m.asset, frames, fps: WAN_FPS, made: new Date().toISOString() });
  }
  chooseClip(P, name, made[0].file);
}

/**
 * A clip that copies a driving video's motion (video.animate, Wan Animate): the pose `from` performs `motion` (a hero
 * source video, "player:Walk"), fitted first to 16 fps and the frame (video.fit). The hero's grips walk, jump and fall
 * exactly like his launcher videos, so every set has the same timing, size and ground line.
 */
async function makeMotionClip(spec, P, name, key, { prompt, negative, vw, vh, n, seeds, from }) {
  const clip = spec.clips[name];
  const src = resolveMotion(clip.motion);
  const fitted = path.join(P.work, 'motion', `${path.basename(src, path.extname(src))}-${vw}x${vh}.mp4`);
  if (!fs.existsSync(fitted)) {
    const job = await gen.run('video.fit', { inputs: { video: src }, params: { width: vw, height: vh, fps: WAN_FPS, maxFrames: 161 }, labels: { entity: spec.entity, motion: clip.motion } });
    await gen.download(job.outputs[0].asset, fitted);
  }
  const have = await framesOf(fitted);
  const frames = Math.max(5, Math.floor(((clip.motionFrames ?? have) - 1) / 4) * 4 + 1);
  if (clip.motionVia === 'pose') return makePoseMotionClip(spec, P, name, key, { prompt, negative, vw, vh, n, seeds, from, fitted, frames });
  const params = { prompt, negative, width: vw, height: vh, frames, fps: WAN_FPS };
  const made = await takes('video.animate', {
    label: `${name} (${frames} frames, ${clip.motion})`, n, seeds, key: `${spec.id}:${name}:${key}`,
    inputs: { reference: P.pose(from), motion: fitted }, params,
    labels: { entity: spec.entity, clip: name }, file: (seed) => path.join(P.clipTakes, `${name}-${seed}.mp4`),
  });
  for (const m of made) writeJson(P.recipe(m.file), { key, clip: name, from, motion: clip.motion, seed: m.seed, prompt, negative, tool: 'video.animate', settings: params, job: m.job, asset: m.asset, frames, fps: WAN_FPS, made: new Date().toISOString() });
  chooseClip(P, name, made[0].file);
}

/**
 * "motionVia": "pose": the motion as pose maps (video.pose, no pixels of whoever performed it), performed by the pose
 * with video.motion (Wan Animate 2). Wan Animate on the raw video copies what the performer holds (the hero's launcher
 * into his bare-handed clips). video.motion makes 480x848: the start pose goes in as the middle of its frame and each
 * take comes back into the same place of a vw x vh frame, so the figure keeps the size of every other clip.
 */
async function makePoseMotionClip(spec, P, name, key, { prompt, negative, vw, vh, n, seeds, from, fitted, frames }) {
  const clip = spec.clips[name];
  const [mw, mh] = [480, 848];
  const cw = Math.round((vh * mw) / mh / 2) * 2; // the middle of the vw x vh frame that maps onto mw x mh
  const pose = fitted.replace(/\.mp4$/, '-pose.mp4');
  if (!fs.existsSync(pose)) {
    const job = await gen.run('video.pose', { inputs: { video: fitted }, labels: { entity: spec.entity, motion: clip.motion } });
    await gen.download(job.outputs[0].asset, pose);
  }
  const ref = path.join(P.work, 'motion', `ref-${from}-${mw}x${mh}.png`);
  await run('ffmpeg', ['-v', 'error', '-y', '-i', P.pose(from), '-vf', `crop=${cw}:${vh}:(iw-${cw})/2:0,scale=${mw}:${mh}`, ref], { quiet: true });
  const count = Math.min(frames, 81);
  const params = { prompt: `${prompt} Plain flat pure black background.`, frames: count };
  const made = await takes('video.motion', {
    label: `${name} (${count} frames, ${clip.motion} as pose maps)`, n, seeds, key: `${spec.id}:${name}:${key}:pose`,
    inputs: { image: ref, video: pose }, params, labels: { entity: spec.entity, clip: name },
    file: (seed) => path.join(P.clipTakes, `${name}-${seed}.raw.mp4`),
  });
  for (const m of made) {
    const out = m.file.replace(/\.raw\.mp4$/, '.mp4');
    await run('ffmpeg', ['-v', 'error', '-y', '-i', m.file, '-vf', `scale=${cw}:${vh},pad=${vw}:${vh}:(ow-iw)/2:0:black,fps=${WAN_FPS}`, '-an', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '14', out], { quiet: true });
    m.file = out;
    writeJson(P.recipe(out), { key, clip: name, from, motion: clip.motion, motionVia: 'pose', seed: m.seed, prompt: params.prompt, negative, tool: 'video.motion', settings: params, job: m.job, asset: m.asset, frames: count, fps: WAN_FPS, made: new Date().toISOString() });
  }
  chooseClip(P, name, made[0].file);
}

async function framesOf(mp4) {
  const out = await run('ffprobe', ['-v', 'error', '-count_frames', '-select_streams', 'v:0', '-show_entries', 'stream=nb_read_frames', '-of', 'csv=p=0', mp4], { quiet: true });
  return parseInt(out.trim(), 10) || 81;
}

function chooseClip(P, name, take) {
  fs.mkdirSync(path.dirname(P.clip(name)), { recursive: true });
  fs.copyFileSync(take, P.clip(name));
  fs.copyFileSync(P.recipe(take), P.recipe(P.clip(name)));
  log(`  ${name}: using ${path.basename(take)}`);
}

// ------------------------------------------------------------------ build + apply

/** The entity's JSON with its "extends" chain applied the way GameContent does: objects merge, the rest replaces. */
function resolvedEntity(id, seen = new Set()) {
  if (seen.has(id)) return {};
  seen.add(id);
  const own = readJson(path.join(ENTITIES, `${id}.json`), null);
  if (!own) return {};
  const merge = (a, b) => {
    const out = { ...a };
    for (const [k, v] of Object.entries(b)) out[k] = v && typeof v === 'object' && !Array.isArray(v) && a[k] && typeof a[k] === 'object' && !Array.isArray(a[k]) ? merge(a[k], v) : v;
    return out;
  };
  return own.extends ? merge(resolvedEntity(own.extends, seen), own) : own;
}

/** Which animator parameters and events the entity's abilities already use. */
function wiring(spec, entity) {
  const abilities = [...(entity.abilities ?? []), ...(entity.states ?? []).flatMap((st) => st.abilities ?? [])];
  const attacker = abilities.find((a) => a.animTrigger || a.fireOnEvent || a.hitEvent);
  const moves = ['chase', 'patrol', 'flee', 'wander', 'orbit', 'hover'];
  const mover = abilities.find((a) => a.animBool && moves.includes(a.type)) ?? abilities.find((a) => a.animBool && !a.windupBool && a.type !== 'laserSweep');
  // An attack held by a bool (a charge's windup, a laser warning and sweeping) instead of pulsed by a trigger.
  const holder = abilities.find((a) => a.windupBool) ?? abilities.find((a) => a.animBool && !moves.includes(a.type) && a !== mover);
  return {
    attackBool: spec.animator?.attackBool ?? (!attacker?.animTrigger && holder ? holder.windupBool || holder.animBool : null),
    attackParam: spec.animator?.attackParam ?? attacker?.animTrigger ?? 'Attack',
    attackEvent: spec.animator?.attackEvent ?? attacker?.fireOnEvent ?? attacker?.hitEvent ?? null,
    moveParam: spec.animator?.moveParam ?? mover?.animBool ?? 'IsMoving',
    hasAttacker: Boolean(attacker?.animTrigger),
    hasMover: Boolean(mover),
  };
}

function animatorFor(spec, built, w) {
  const sheet = (name) => `Art/Enemies/${spec.prefix}/${built[name].sheet}`;
  const states = [];
  const params = [];
  const transitions = [];
  const t = (fromState, toState, extra) => transitions.push({ fromState, toState, hasExitTime: false, exitTime: 1, conditions: [], ...extra });
  const cond = (parameterName, mode) => ({ parameterName, mode, threshold: 0 });
  const order = [...Object.keys(STATE), ...Object.keys(spec.clips).filter((n) => !STATE[n])];
  for (const name of order) {
    if (!built[name]) continue;
    const m = built[name];
    const fn = spec.clips[name]?.event ?? (name === 'attack' ? w.attackEvent : null);
    const events = fn ? [{ frame: m.hit ?? Math.floor(m.frames / 2), functionName: fn }] : [];
    states.push({ stateName: stateOf(spec, name), spriteSheet: sheet(name), fps: m.fps, loop: m.loop, events });
  }
  const graph = spec.animator?.transitions;
  if (graph) {
    // The spec draws the graph itself: { from, to, when: [bool or trigger names, "!Name" = not], exit: true }.
    // Transitions to or from a state without a sheet yet are left out until it is made.
    const made = new Set(states.map((s) => s.stateName));
    for (const g of graph) {
      if (!made.has(g.to) || !(g.from === 'Any' || made.has(g.from))) continue;
      t(g.from, g.to, {
        hasExitTime: Boolean(g.exit), exitTime: g.exitTime ?? 1,
        conditions: (g.when ?? []).map((c) => (c.startsWith('!') ? cond(c.slice(1), 'IfNot') : cond(c, 'If'))),
      });
    }
    for (const p of spec.animator.parameters ?? []) params.push({ name: p.name, type: p.type ?? 'Bool', defaultValue: p.defaultValue ?? 0 });
    return { parameters: params, states, transitions, defaultStateName: spec.animator.default ?? 'Idle' };
  }
  const has = (n) => Boolean(built[n]);
  const calm = ['Idle', ...(has('move') ? ['Move'] : [])];
  if (has('move')) {
    params.push({ name: w.moveParam, type: 'Bool', defaultValue: 0 });
    t('Idle', 'Move', { conditions: [cond(w.moveParam, 'If')] });
    t('Move', 'Idle', { conditions: [cond(w.moveParam, 'IfNot')] });
  }
  if (has('attack') && w.attackBool) {
    params.push({ name: w.attackBool, type: 'Bool', defaultValue: 0 });
    for (const s of calm) t(s, 'Attack', { conditions: [cond(w.attackBool, 'If')] });
    t('Attack', 'Idle', { conditions: [cond(w.attackBool, 'IfNot')] });
  } else if (has('attack')) {
    params.push({ name: w.attackParam, type: 'Trigger', defaultValue: 0 });
    for (const s of calm) t(s, 'Attack', { conditions: [cond(w.attackParam, 'If')] });
    t('Attack', 'Idle', { hasExitTime: true, exitTime: 1 });
  }
  if (has('hurt')) {
    // The engine pulses "Hurt" on every hit that doesn't kill. Not from Attack: that would drop its fire event.
    params.push({ name: 'Hurt', type: 'Trigger', defaultValue: 0 });
    for (const s of calm) t(s, 'Hurt', { conditions: [cond('Hurt', 'If')] });
    t('Hurt', 'Idle', { hasExitTime: true, exitTime: 1 });
  }
  // "Die" has no transitions: the engine plays it as the death effect.
  return { parameters: params, states, transitions, defaultStateName: 'Idle' };
}

async function build(spec, P) {
  const frame = readJson(P.frameJson, null);
  const clips = {};
  for (const [name, c] of Object.entries(spec.clips)) {
    const video = c.video ? resolveAsset(c.video) : P.clip(name);
    if (!fs.existsSync(video)) { log(`  ${name}: no clip yet, skipped`); continue; }
    const handMade = Boolean(c.video) || !fs.existsSync(P.recipe(P.clip(name)));
    clips[name] = {
      video, loop: c.loop ?? LOOPS.has(name), fps: c.fps ?? spec.fps, speed: c.speed ?? 1, start: c.start, end: c.end,
      flip: c.flip, tolerance: c.tolerance, size: c.size, hit: c.hit, autotrim: c.autotrim,
      event: name === 'attack' || Boolean(c.event), measure: handMade || !frame,
    };
  }
  if (!clips.idle) throw new Error('There is no idle clip to build from yet.');
  const entity = resolvedEntity(spec.entity);
  const visualScale = spec.visualScale ?? entity.visual?.scale ?? 1;
  const refHeight = spec.pixels ?? Math.max(96, Math.min(400, Math.round(spec.height * 280)));
  const ppu = (refHeight * visualScale) / spec.height;
  const job = path.join(P.work, 'build.json');
  writeJson(job, {
    entity: spec.entity, frame: frame ? P.frameJson : writeFallbackFrame(P, spec), out: P.sheets, previewDir: P.work,
    prefix: spec.prefix, refHeight, pixelsPerUnit: ppu, clips,
  });
  log(`building sheets (${refHeight}px standing, ${ppu.toFixed(1)} px/unit) into ${path.relative(REPO, P.sheets)}`);
  const built = JSON.parse((await run(PYTHON, [SHEETS_PY, 'build', job])).trim().split(/\r?\n/).pop());
  writeJson(path.join(P.work, 'built.json'), built);
  return { built, visualScale };
}

function writeFallbackFrame(P, spec) {
  writeJson(P.frameJson, { size: spec.frameSize, scale: 1, ground: spec.frameSize * 0.94, center: spec.frameSize / 2, standHeight: spec.frameSize * spec.frameFill });
  return P.frameJson;
}

function apply(spec, built, visualScale) {
  const file = path.join(ENTITIES, `${spec.entity}.json`);
  const own = readJson(file, null);
  if (!own) throw new Error(`No entity ${path.relative(REPO, file)} to give the art to (make the entity first).`);
  const w = wiring(spec, resolvedEntity(spec.entity));
  own.visual ??= {};
  own.visual.animator = animatorFor(spec, built, w);
  own.visual.facesLeft = false;
  if (spec.visualScale) own.visual.scale = spec.visualScale;
  // Real art replaces placeholder tints (Docs/Story.md: the look comes from the pipeline, never a tint).
  const tinted = own.visual.color && !spec.keepTint;
  if (tinted) delete own.visual.color;
  fs.writeFileSync(file, `${stringify(own)}\n`);
  log(`wrote visual.animator into ${path.relative(REPO, file)}`);
  const notes = [];
  if (tinted) notes.push('removed the placeholder visual.color tint (set "keepTint": true in the spec to keep it)');
  for (const [name, c] of Object.entries(spec.clips)) if (built[name] && c.event) notes.push(`${stateOf(spec, name)} fires "${c.event}" on frame ${built[name].hit}: the ability it times needs "fireOnEvent": "${c.event}"`);
  if (built.attack && !w.hasAttacker) notes.push(`no ability pulses an attack parameter: set "animTrigger": "${w.attackParam}" on its shoot/melee ability so Attack plays`);
  if (built.attack && !w.attackEvent) notes.push('the Attack state fires no event: give the shoot ability "fireOnEvent" (or melee "hitEvent") to time hits to the animation');
  if (built.move && !w.hasMover) notes.push(`no ability sets "${w.moveParam}": add "animBool": "${w.moveParam}" to its movement ability so Move plays`);
  const ent = resolvedEntity(spec.entity);
  const radius = (ent.body?.radius ?? 1) * visualScale;
  notes.push(`size check: the figure is ${spec.height} units tall; its body ${ent.body?.shape ?? 'Circle'} radius is ${radius.toFixed(2)} units (body.radius x visual.scale)`);
  return notes;
}

/** JSON in the config's style: two-space indent, short arrays of numbers/strings on one line. */
function stringify(v, ind = '') {
  const next = `${ind}  `;
  if (Array.isArray(v)) {
    if (!v.length) return '[]';
    if (v.every((x) => x === null || typeof x !== 'object') && JSON.stringify(v).length < 80) return `[${v.map((x) => JSON.stringify(x)).join(', ')}]`;
    return `[\n${v.map((x) => next + stringify(x, next)).join(',\n')}\n${ind}]`;
  }
  if (v && typeof v === 'object') {
    const e = Object.entries(v);
    if (!e.length) return '{}';
    return `{\n${e.map(([k, x]) => `${next}${JSON.stringify(k)}: ${stringify(x, next)}`).join(',\n')}\n${ind}}`;
  }
  return JSON.stringify(v);
}

/** Another Unity (the editor, or another batch job) has the project open while Temp/UnityLockfile is locked. */
function projectBusy() {
  const lock = path.join(REPO, 'Temp', 'UnityLockfile');
  if (!fs.existsSync(lock)) return false;
  try {
    fs.rmSync(lock); // A stale lock (no Unity holds it) can be removed; a held one can't on Windows.
    return false;
  } catch {
    return true;
  }
}

async function unityImport() {
  if (!fs.existsSync(UNITY)) return log(`Unity not found at ${UNITY}; open the editor to import.`);
  const logFile = path.join(REPO, 'Logs', 'ArtImport.log');
  for (let attempt = 1; attempt <= 10; attempt++) {
    if (projectBusy()) {
      if (attempt === 1) log('Unity has the project open (the editor, or another batch job): waiting up to 5 minutes for it...');
      await new Promise((r) => setTimeout(r, 30000));
      continue;
    }
    log('importing in Unity (batch mode; about a minute)...');
    try {
      await run(UNITY, ['-batchmode', '-nographics', '-projectPath', REPO, '-logFile', logFile, '-executeMethod', 'SickOfIt.Tools.Editor.GameConfig.GameConfigCommands.Import'], { quiet: true });
      return log('  imported (Logs/GameConfig.log has the report)');
    } catch (e) {
      const text = fs.existsSync(logFile) ? fs.readFileSync(logFile, 'utf8') : '';
      // Unity quits right after opening the project when someone else got the lock first.
      if (projectBusy() || /another Unity instance/i.test(text)) continue;
      return log(`  Unity import failed (exit ${e.code}); read Logs/ArtImport.log and Logs/GameConfig.log.`);
    }
  }
  log('  Unity kept the project busy: focus the editor (it imports by itself) or run GameConfigCommands.Import later (AGENTS.md).');
}

// ------------------------------------------------------------------ hero clips

const HERO_BANNED = /\b(jacket|coat|messy|hair|man in his thirties|unshaven|t-shirt)\b/i;

/** player.json's states, by name. */
const playerStates = () => new Map((readJson(PLAYER_JSON, {}).animationConfig?.states ?? []).map((s) => [s.stateName, s]));

/** No GPU: hero.json against reality. Every clip's "wired" must say what player.json does, the look must be the knight. */
function checkHero(spec) {
  const problems = [];
  const states = playerStates();
  const sheetOf = (s) => (s.spriteSheet ?? s.lazySheet ?? '').split('/').pop(); // a weapon grip's is loaded on demand (lazySheet)
  for (const [name, c] of Object.entries(spec.clips)) {
    const st = states.get(c.state);
    const own = `${c.state}.png`;
    if (![false, 'own', 'standIn'].includes(c.wired)) problems.push(`clip "${name}": "wired" must be "own", "standIn" or false`);
    else if (c.wired === false && st) problems.push(`clip "${name}": marked unwired but player.json has a ${c.state} state (${sheetOf(st)})`);
    else if (c.wired && !st) problems.push(`clip "${name}": marked wired but player.json has no ${c.state} state`);
    else if (c.wired === 'own' && sheetOf(st) !== own) problems.push(`clip "${name}": marked "own" but ${c.state} plays ${sheetOf(st)}, not ${own}`);
    else if (c.wired === 'standIn' && (sheetOf(st) === own || sheetOf(st) !== c.standIn)) problems.push(`clip "${name}": marked "standIn" ${c.standIn} but ${c.state} plays ${sheetOf(st)}`);
    if (c.wired === 'own' && !fs.existsSync(path.join(HERO_SHEETS, own))) problems.push(`clip "${name}": ${own} is missing from Assets/Art/Sprites/Player`);
  }
  if (!/knight/i.test(spec.description) || !/launcher/i.test(spec.description)) problems.push('description must say the hero is an armoured knight with the launcher');
  const texts = [['description', spec.description], ...Object.entries(spec.poses).map(([k, v]) => [`pose ${k}`, v]), ...Object.entries(spec.clips).map(([k, c]) => [`clip ${k}`, c.action])];
  for (const [where, text] of texts) if (HERO_BANNED.test(text)) problems.push(`${where}: "${text.match(HERO_BANNED)[0]}" is not the knight (armour, helm, gauntlets)`);
  if (spec.bake.cell.join('x') !== HERO_BAKE.cell.join('x') || spec.bake.ppu !== HERO_BAKE.ppu) problems.push(`bake must be ${HERO_BAKE.cell.join('x')} at ${HERO_BAKE.ppu} PPU (Idle's size)`);
  const py = fs.readFileSync(PLAYER_SHEET_PY, 'utf8');
  if (!new RegExp(`CELL_W, CELL_H = ${HERO_BAKE.cell[0]}, ${HERO_BAKE.cell[1]}`).test(py) || !new RegExp(`PPU = ${HERO_BAKE.ppu}\\b`).test(py)) problems.push('player_clip_sheet.py defaults are not 604x340 at 100 PPU');
  return problems;
}

/** Bakes every clip that has a video to Assets/Art/Sprites/Player/<State>.png, then adds or points its player.json state. */
async function buildHero(spec, P, opts) {
  const baked = [];
  const jobs = [];
  for (const [name, c] of Object.entries(spec.clips)) {
    const video = c.video ? resolveAsset(c.video) : P.clip(name);
    if (!fs.existsSync(video)) { log(`  ${name}: no clip yet, skipped`); continue; }
    const fps = c.fps ?? WAN_FPS;
    const args = [PLAYER_SHEET_PY, video, c.state, HERO_SHEETS, '--fps', String(fps), '--cell', spec.bake.cell.join('x'), '--ppu', String(spec.bake.ppu)];
    if (c.start) args.push('--start', String(c.start));
    if (c.end) args.push('--end', String(c.end));
    baked.push({ state: c.state, fps: c.playFps ?? 12, loop: c.loop ?? false, sheet: `Art/Sprites/Player/${c.state}.png`, wired: c.wired });
    // A sheet newer than its clip, baked with the same settings, is up to date (a bake is ~2 minutes a state).
    const sheet = path.join(HERO_SHEETS, `${c.state}.png`);
    const stamp = path.join(P.work, 'bake', `${c.state}.json`);
    const settings = JSON.stringify(args.slice(2));
    const fresh = fs.existsSync(sheet) && fs.statSync(sheet).mtimeMs > fs.statSync(video).mtimeMs
      && (!fs.existsSync(stamp) || readJson(stamp, {}).settings === settings);
    if (fresh && !opts.redo.has(name)) { log(`  ${c.state}: sheet up to date`); continue; }
    jobs.push({ args, stamp, settings });
  }
  // SOI_BAKE_PARALLEL states at a time (default 3).
  const parallel = Math.max(1, Number(process.env.SOI_BAKE_PARALLEL ?? 3));
  const worker = async () => {
    for (let j = jobs.shift(); j; j = jobs.shift()) {
      log((await run(PYTHON, j.args)).trim());
      fs.mkdirSync(path.dirname(j.stamp), { recursive: true });
      writeJson(j.stamp, { settings: j.settings, baked: new Date().toISOString() });
    }
  };
  await Promise.all(Array.from({ length: parallel }, worker));
  if (!baked.length) return log('no hero clip to bake yet');
  wirePlayerStates(baked);
  log('hero.json: set "wired" to "own" on the clips just baked (node Tools/Art/art.mjs hero --check says what disagrees)');
  if (!opts.noUnity) await unityImport();
}

/** Edits player.json as text so its other numbers and layout stay as they are: a state's sheet is replaced, a new one appended. */
function wirePlayerStates(baked) {
  let text = fs.readFileSync(PLAYER_JSON, 'utf8');
  for (const b of baked) {
    const re = new RegExp(`("stateName": "${b.state}",\\s*"spriteSheet": )"[^"]*"`);
    if (re.test(text)) { text = text.replace(re, `$1"${b.sheet}"`); log(`  player.json: ${b.state} now plays ${b.sheet}`); continue; }
    // A weapon grip loaded on demand (LazySheets.cs): "spriteSheet": null, then its "lazySheet".
    const lazy = new RegExp(`("stateName": "${b.state}",\\s*"spriteSheet": null,\\s*"lazySheet": )"[^"]*"`);
    if (lazy.test(text)) { text = text.replace(lazy, `$1"${b.sheet}"`); log(`  player.json: ${b.state} now plays ${b.sheet} (on demand)`); continue; }
    const block = `,\n      {\n        "stateName": "${b.state}",\n        "spriteSheet": "${b.sheet}",\n        "spriteNameFilter": "",\n        "firstFrame": 0,\n        "frameCount": 0,\n        "fps": ${b.fps},\n        "loop": ${b.loop},\n        "events": [],\n        "speed": 1.0\n      }`;
    const at = text.indexOf('\n    ],\n    "transitions"');
    if (at < 0) throw new Error('player.json: cannot find the end of animationConfig.states');
    text = text.slice(0, at) + block + text.slice(at);
    log(`  player.json: added state ${b.state} (${b.sheet}); PlayerSceneClip falls back to it`);
  }
  fs.writeFileSync(PLAYER_JSON, text);
}

// ------------------------------------------------------------------ status + main

function status(spec) {
  const P = paths(spec);
  const k = keys(spec, P);
  const rows = [];
  const state = (file, key) => (!fs.existsSync(file) ? 'missing' : upToDate(file, key) ? 'ok' : fs.existsSync(P.recipe(file)) ? 'out of date' : 'hand-made');
  rows.push(['pose stand', state(P.pose('stand'), k.stand)]);
  for (const pose of Object.keys(spec.poses)) if (pose !== 'stand') rows.push([`pose ${pose}`, state(P.pose(pose), k[`pose:${pose}`])]);
  for (const [name, c] of Object.entries(spec.clips)) rows.push([`clip ${name}`, c.video ? (fs.existsSync(resolveAsset(c.video)) ? 'given video' : 'video missing') : state(P.clip(name), k[`clip:${name}`])]);
  const built = readJson(path.join(P.work, 'built.json'), null);
  rows.push(['sheets', built ? Object.keys(built).join(', ') : 'not built']);
  return { P, rows };
}

function parseArgs(argv) {
  const o = { ids: [], redo: new Set(), pick: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--start') o.start = true;
    else if (a === '--build') o.buildOnly = true;
    else if (a === '--status') o.status = true;
    else if (a === '--list') o.list = true;
    else if (a === '--no-unity') o.noUnity = true;
    else if (a === '--redo') argv[++i].split(',').forEach((x) => o.redo.add(x));
    else if (a === '--pick') o.pick.push(argv[++i]);
    else if (a === '--takes') o.takes = Number(argv[++i]);
    else if (a === '--seed') o.seed = Number(argv[++i]);
    else if (a === '--hero-ref') o.heroRef = argv[++i];
    else if (a === '--only') o.only = new Set(argv[++i].split(','));
    else if (a === '--check') o.check = true;
    else if (a === '--poses-only') o.posesOnly = true;
    else if (a.startsWith('--')) throw new Error(`Unknown flag ${a}`);
    else o.ids.push(a);
  }
  return o;
}

/**
 * The work folders and Hawkes Gen's library (hawkesgen/docs/library.md), both ways, as hawkes.library.json maps them:
 * "export" writes the picks made in the library (Studio's Library tab, the Unity window) into the work folder before a
 * run, "import" brings the takes this run made into the library so they can be reviewed and picked there. A Gen that
 * isn't answering doesn't stop the art: the step is skipped with a note.
 */
async function librarySync(direction, spec, made = new Set()) {
  const file = path.join(REPO, 'hawkes.library.json');
  if (!fs.existsSync(file) || process.env.SOI_LIBRARY === 'off') return;
  try {
    const m = { ...loadMapping(file), root: workRoot() };
    const g = new HawkesGen({ project: PROJECT });
    // The poses and clips this run made or picked win over the library's current (adopt): otherwise the next run's
    // export writes the library's older pick back over them, and a --redo or --pick never sticks.
    const r = direction === 'export' ? await exportFolders(g, m, { only: spec.id }) : await importFolders(g, m, { only: spec.id, adopt: [...made] });
    if (direction === 'import') {
      // What the library's own "Make takes" uses (the enemy kind in hawkesgen/presets/sickofit.json): the look, and
      // each pose's and clip's action, with a clip's from and to poses.
      const slots = {};
      for (const [pose, action] of Object.entries(spec.poses)) slots[`pose/${slugOf(pose)}`] = { brief: action };
      for (const [name, c] of Object.entries(spec.clips)) {
        if (c.video) continue;
        const from = slugOf(c.from ?? 'stand');
        slots[`clip/${slugOf(name)}`] = { brief: c.action, from, to: slugOf(c.to ?? c.from ?? 'stand') };
      }
      await g.updateEntity(`${PROJECT}/${slugOf(spec.id)}`, { name: spec.name, data: { look: spec.description, slots } }).catch(() => {});
    }
    const changed = r.filter((x) => !['same', 'differs'].includes(x.action));
    if (changed.length) log(`library: ${direction === 'export' ? 'picks written into the work folder' : 'takes brought into the library'}: ${changed.length}`);
  } catch (e) {
    log(`library ${direction} skipped: ${String(e.message).split(/\r?\n/)[0]}`);
  }
}

async function make(id, opts) {
  const spec = loadSpec(id);
  if (opts.heroRef === 'idle') spec.referenceIdle = true;
  if (spec.referenceIdle) spec.reference = 'Art/Sprites/Player/Idle.png';
  if (opts.only && spec.hero) {
    // Only these clips (and the poses they use): a hero batch is priced clip by clip.
    for (const n of Object.keys(spec.clips)) if (!opts.only.has(n)) delete spec.clips[n];
    if (!Object.keys(spec.clips).length) throw new Error(`--only ${[...opts.only].join(',')}: no such clip in ${id}`);
  }
  const P = paths(spec);
  await preflight(spec);
  fs.mkdirSync(P.work, { recursive: true });
  log(`${spec.name} (${spec.entity}) -> work folder ${P.work}`);
  await librarySync('export', spec);

  const made = new Set(); // the library slots this run made or picked: pose/<name>, clip/<name>
  for (const p of opts.pick) {
    const [what, seed] = p.split('=');
    made.add(`${spec.poses[what] ? 'pose' : 'clip'}/${slugOf(what)}`);
    if (spec.poses[what]) choosePose(P, what, path.join(P.poseTakes, `${what}-${seed}.png`));
    else if (spec.clips[what]) chooseClip(P, what, path.join(P.clipTakes, `${what}-${seed}.mp4`));
    else throw new Error(`--pick ${p}: no pose or clip "${what}"`);
  }

  if (!opts.buildOnly) {
    let k = keys(spec, P);
    if (opts.redo.has('stand') || !upToDate(P.pose('stand'), k.stand)) {
      log('stand: making the standing side view');
      await makePose(spec, P, 'stand', k.stand, opts);
      made.add('pose/stand');
    }
    // The poses the clips start and end on, and the poses those are made from or shown with (bases and refs).
    const needed = new Set(Object.values(spec.clips).filter((c) => !c.video).flatMap((c) => [c.from ?? 'stand', c.to ?? c.from ?? 'stand']));
    for (const pose of [...Object.keys(spec.poses)].reverse()) {
      if (!needed.has(pose)) continue;
      if (spec.poseBase[pose]) needed.add(spec.poseBase[pose]);
      for (const r of spec.poseRefs[pose] ?? []) needed.add(r);
    }
    // Poses in waves: every pose whose base and refs are ready is made at once (each its own Hawkes Gen jobs), then the next wave.
    const todo = Object.keys(spec.poses).filter((pose) => pose !== 'stand' && needed.has(pose) && (opts.redo.has(pose) || !upToDate(P.pose(pose), k[`pose:${pose}`])));
    const pending = new Set(todo);
    while (pending.size) {
      const wave = [...pending].filter((pose) => ![spec.poseBase[pose], ...(spec.poseRefs[pose] ?? [])].some((d) => d && pending.has(d)));
      if (!wave.length) throw new Error(`poses wait on each other: ${[...pending].join(', ')}`);
      await Promise.all(wave.map(async (pose) => {
        log(`pose ${pose}: ${spec.poses[pose]}`);
        await makePose(spec, P, pose, k[`pose:${pose}`], opts);
        made.add(`pose/${slugOf(pose)}`);
        pending.delete(pose);
      }));
    }
    if (!spec.hero) await frameAll(spec, P);
    if (opts.posesOnly) {
      await librarySync('import', spec, made);
      return log('poses made (--poses-only): pick takes with --pick, then run again for the clips');
    }
    k = keys(spec, P);
    const clipsTodo = [];
    for (const [name, c] of Object.entries(spec.clips)) {
      if (c.video) continue;
      const hand = fs.existsSync(P.clip(name)) && !fs.existsSync(P.recipe(P.clip(name)));
      if (hand && !opts.redo.has(name)) continue; // A hand-made clip dropped in clips/ stays.
      if (opts.redo.has(name) || !upToDate(P.clip(name), k[`clip:${name}`])) clipsTodo.push(name);
    }
    // Clips side by side (SOI_ART_PARALLEL at a time, default 1 for enemies, 12 for the hero): Hawkes Gen spreads them over its servers.
    const parallel = Math.max(1, Number(process.env.SOI_ART_PARALLEL ?? (spec.hero ? 12 : 1)));
    const failed = [];
    const worker = async () => {
      for (let name = clipsTodo.shift(); name; name = clipsTodo.shift()) {
        log(`clip ${name}: ${spec.clips[name].action}`);
        try { await makeClip(spec, P, name, k[`clip:${name}`], opts); made.add(`clip/${slugOf(name)}`); } catch (e) { failed.push(name); log(`  ${name} failed: ${e.message}`); }
      }
    };
    await Promise.all(Array.from({ length: parallel }, worker));
    if (failed.length) {
      await librarySync('import', spec, made);
      throw new Error(`clips failed: ${failed.join(', ')} (run again to retry them)`);
    }
  } else if (fs.existsSync(P.pose('stand')) && !spec.hero) {
    await frameAll(spec, P);
  }

  await librarySync('import', spec, made);
  if (spec.hero) return buildHero(spec, P, opts);
  const { built, visualScale } = await build(spec, P);
  const notes = apply(spec, built, visualScale);
  if (!opts.noUnity) await unityImport();
  log(`\npreview: ${path.join(P.work, 'preview.png')} (+ preview-<clip>.gif)`);
  for (const n of notes) log(`note: ${n}`);
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  if (opts.list || (!opts.ids.length && opts.status)) {
    for (const f of fs.readdirSync(SPECS).filter((f) => f.endsWith('.json') && !f.startsWith('_'))) {
      const id = f.replace(/\.json$/, '');
      try {
        const { rows } = status(loadSpec(id));
        log(`${id}: ${rows.map(([a, b]) => `${a} ${b}`).join(' | ')}`);
      } catch (e) {
        log(`${id}: ${e.message}`);
      }
    }
    return;
  }
  if (!opts.ids.length) throw new Error('Usage: node Tools/Art/art.mjs <id> [--start] [--redo x,y] [--pick pose=seed] [--build] [--status] [--no-unity]   (or --list)');
  for (const id of opts.ids) {
    if (opts.check) {
      const spec = loadSpec(id);
      if (!spec.hero) throw new Error('--check is for hero specs ("kind": "hero")');
      const problems = checkHero(spec);
      if (problems.length) throw new Error(`${id} check failed:\n  ${problems.join('\n  ')}`);
      log(`${id}: ok (${Object.keys(spec.clips).length} clips; wired against player.json, knight look, ${spec.bake.cell.join('x')} @ ${spec.bake.ppu} PPU)`);
    } else if (opts.status) {
      const { P, rows } = status(loadSpec(id));
      log(`${id}  (${P.work})`);
      for (const [a, b] of rows) log(`  ${a.padEnd(14)} ${b}`);
    } else {
      await make(id, opts);
    }
  }
}

main().catch((e) => {
  console.error(`\nerror: ${e.message}`);
  process.exit(1);
});
