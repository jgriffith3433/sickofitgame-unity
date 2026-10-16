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

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const DOD = process.env.DOD_ROOT || 'C:\\Projects\\doseofdestructionwebsite';
const SPECS = path.join(REPO, 'Assets', 'GameConfig', 'art');
const ENTITIES = path.join(REPO, 'Assets', 'GameConfig', 'content', 'entities');
const SHEETS = path.join(REPO, 'Assets', 'Art', 'Enemies');
const SHEETS_PY = path.join(REPO, 'Tools', 'Art', 'sheets.py');
const UNITY = process.env.UNITY_EXE || 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.7f1\\Editor\\Unity.exe';
const PYTHON = [path.join(DOD, '.venv', 'Scripts', 'python.exe'), path.join(DOD, '.venv', 'bin', 'python')].find((p) => fs.existsSync(p))
  ?? (process.platform === 'win32' ? 'python' : 'python3');

/** What a spec leaves out. Assets/GameConfig/art/_defaults.json overrides these for every enemy. */
const DEFAULTS = {
  style: 'Dark fantasy video game character art: painterly, detailed, gritty, warm rim light from hellfire, readable silhouette.',
  background: 'Plain flat pure green background (#00ff00), no floor, no shadow, no scenery, even studio lighting.',
  takes: { stand: 3, pose: 2, clip: 1 },
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

// Animator state for each clip name; the engine knows these (Docs/ArtPipeline.md "Wiring").
const STATE = { idle: 'Idle', move: 'Move', attack: 'Attack', hurt: 'Hurt', die: 'Die' };
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
  spec.entity ??= id;
  spec.name ??= pascal(id);
  spec.prefix ??= pascal(spec.entity);
  spec.poses = { stand: 'standing in its normal stance', ...(raw.poses ?? {}) };
  spec.clips = raw.clips ?? {};
  const problems = [];
  if (!spec.description) problems.push('"description" is required');
  if (!(spec.height > 0)) problems.push('"height" (world units the standing figure is tall) is required');
  if (!spec.clips.idle) problems.push('"clips" needs at least "idle"');
  for (const [name, c] of Object.entries(spec.clips)) {
    if (!STATE[name]) problems.push(`clip "${name}": use one of ${Object.keys(STATE).join(', ')}`);
    if (c.video) continue;
    for (const k of ['from', 'to']) if (c[k] && !spec.poses[c[k]]) problems.push(`clip "${name}": no pose "${c[k]}" in "poses"`);
    if (!c.action) problems.push(`clip "${name}": "action" is required (what happens in it)`);
  }
  if (problems.length) throw new Error(`${path.relative(REPO, file)}:\n  ${problems.join('\n  ')}`);
  return spec;
}

function workRoot() {
  const game = readJson(path.join(REPO, 'Assets', 'GameConfig', 'game.json'), {});
  return path.join(game.characterArt?.enemySourceRoot || 'C:\\Projects\\SickOfItGameEnemies', 'Generated');
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
  for (const pose of Object.keys(spec.poses)) if (pose !== 'stand') k[`pose:${pose}`] = sha({ stand: k.stand, a: spec.poses[pose], b: spec.background });
  for (const [name, c] of Object.entries(spec.clips)) {
    if (c.video) continue;
    const from = c.from ?? 'stand';
    const to = c.to ?? from;
    k[`clip:${name}`] = sha({ from: poseKey(k, from), to: poseKey(k, to), p: clipPrompt(spec, name, c), s: c.seconds ?? 2, n: spec.frameSize, f: spec.frameFill });
  }
  return k;
}
const poseKey = (k, pose) => (pose === 'stand' ? k.stand : k[`pose:${pose}`]);
const upToDate = (file, key) => fs.existsSync(file) && readJson(file.replace(/\.[^.]+$/, '.json'), {}).key === key;

function resolveAsset(ref) {
  const file = ref.split('#')[0];
  return path.isAbsolute(file) ? file : path.join(REPO, 'Assets', file);
}

function clipPrompt(spec, name, c) {
  const from = c.from ?? 'stand';
  const to = c.to ?? from;
  const frames = from === to ? `It starts and ends ${spec.poses[from]}.` : `It starts ${spec.poses[from]} and ends ${spec.poses[to]}.`;
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
    const o = r.value.outputs?.[0];
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
    input = P.pose('stand');
    const air = /air|fly|hover|float|leap|jump/i.test(`${spec.poses[pose]} ${spec.poses.stand}`);
    prompt = fill(spec.prompts.pose, { action: spec.poses[pose], background: spec.background, ground: air ? '' : ', feet on the same ground line as image 1' });
  }
  const n = opts.takes ?? (pose === 'stand' ? spec.takes.stand : spec.takes.pose);
  const seeds = Array.from({ length: n }, (_, i) => seedFor(opts, i, key, opts.redo.has(pose)));
  fs.mkdirSync(P.poseTakes, { recursive: true });
  const params = { prompt };
  const made = await takes('image.edit', {
    label: pose, n, seeds, key: `${spec.id}:${pose}:${key}`, inputs: { images: [input] }, params,
    labels: { entity: spec.entity, pose }, file: (seed) => path.join(P.poseTakes, `${pose}-${seed}.png`),
  });
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
  const params = { prompt, negative, width: spec.frameSize, height: spec.frameSize, frames, fps: WAN_FPS, ...(await clipSettings(name, clip.settings)) };
  const n = opts.takes ?? spec.takes.clip;
  const seeds = Array.from({ length: n }, (_, i) => seedFor(opts, i, key, opts.redo.has(name)));
  fs.mkdirSync(P.clipTakes, { recursive: true });
  const made = await takes('video.i2v', {
    label: `${name} (${frames} frames)`, n, seeds, key: `${spec.id}:${name}:${key}`,
    inputs: { start: P.framed(from), end: P.framed(to) }, params,
    labels: { entity: spec.entity, clip: name }, file: (seed) => path.join(P.clipTakes, `${name}-${seed}.mp4`),
  });
  for (const m of made) {
    writeJson(P.recipe(m.file), { key, clip: name, from, to, seed: m.seed, prompt, negative, tool: 'video.i2v', settings: params, job: m.job, asset: m.asset, frames, fps: WAN_FPS, made: new Date().toISOString() });
  }
  chooseClip(P, name, made[0].file);
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
  const mover = abilities.find((a) => a.animBool && ['chase', 'patrol', 'flee', 'wander', 'orbit', 'hover'].includes(a.type)) ?? abilities.find((a) => a.animBool);
  return {
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
  for (const name of Object.keys(STATE)) {
    if (!built[name]) continue;
    const m = built[name];
    const events = name === 'attack' && w.attackEvent ? [{ frame: m.hit ?? Math.floor(m.frames / 2), functionName: w.attackEvent }] : [];
    states.push({ stateName: STATE[name], spriteSheet: sheet(name), fps: m.fps, loop: m.loop, events });
  }
  const has = (n) => Boolean(built[n]);
  const calm = ['Idle', ...(has('move') ? ['Move'] : [])];
  if (has('move')) {
    params.push({ name: w.moveParam, type: 'Bool', defaultValue: 0 });
    t('Idle', 'Move', { conditions: [cond(w.moveParam, 'If')] });
    t('Move', 'Idle', { conditions: [cond(w.moveParam, 'IfNot')] });
  }
  if (has('attack')) {
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
      event: name === 'attack', measure: handMade || !frame,
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
    else if (a.startsWith('--')) throw new Error(`Unknown flag ${a}`);
    else o.ids.push(a);
  }
  return o;
}

async function make(id, opts) {
  const spec = loadSpec(id);
  const P = paths(spec);
  fs.mkdirSync(P.work, { recursive: true });
  log(`${spec.name} (${spec.entity}) -> work folder ${P.work}`);

  for (const p of opts.pick) {
    const [what, seed] = p.split('=');
    if (spec.poses[what]) choosePose(P, what, path.join(P.poseTakes, `${what}-${seed}.png`));
    else if (spec.clips[what]) chooseClip(P, what, path.join(P.clipTakes, `${what}-${seed}.mp4`));
    else throw new Error(`--pick ${p}: no pose or clip "${what}"`);
  }

  if (!opts.buildOnly) {
    let k = keys(spec, P);
    if (opts.redo.has('stand') || !upToDate(P.pose('stand'), k.stand)) {
      log('stand: making the standing side view');
      await makePose(spec, P, 'stand', k.stand, opts);
    }
    for (const pose of Object.keys(spec.poses)) {
      if (pose === 'stand') continue;
      const used = Object.values(spec.clips).some((c) => !c.video && ((c.from ?? 'stand') === pose || (c.to ?? c.from) === pose));
      if (!used) continue;
      if (opts.redo.has(pose) || !upToDate(P.pose(pose), k[`pose:${pose}`])) {
        log(`pose ${pose}: ${spec.poses[pose]}`);
        await makePose(spec, P, pose, k[`pose:${pose}`], opts);
      }
    }
    await frameAll(spec, P);
    k = keys(spec, P);
    for (const [name, c] of Object.entries(spec.clips)) {
      if (c.video) continue;
      const hand = fs.existsSync(P.clip(name)) && !fs.existsSync(P.recipe(P.clip(name)));
      if (hand && !opts.redo.has(name)) continue; // A hand-made clip dropped in clips/ stays.
      if (opts.redo.has(name) || !upToDate(P.clip(name), k[`clip:${name}`])) {
        log(`clip ${name}: ${c.action}`);
        await makeClip(spec, P, name, k[`clip:${name}`], opts);
      }
    }
  } else if (fs.existsSync(P.pose('stand'))) {
    await frameAll(spec, P);
  }

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
    if (opts.status) {
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
