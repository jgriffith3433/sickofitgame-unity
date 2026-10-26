#!/usr/bin/env node
// Makes the stills an effect job bakes (Tools/Art/fx/<job>.json "gen"), through Hawkes Gen, then bakes the job.
//
//   node Tools/Art/fx_make.mjs Tools/Art/fx/warn.json            make what's missing, then bake (python fx_sheets.py)
//   node Tools/Art/fx_make.mjs Tools/Art/fx/warn.json --redo glow make that still again (new seeds)
//   node Tools/Art/fx_make.mjs Tools/Art/fx/warn.json --pick glow=123   use that take
//
// A job's "gen" names each source image a clip uses and how to paint it:
//   "gen": { "glow.png": { "prompt": "...", "canvas": "000000", "takes": 3, "size": [1024, 1024] } }
// Each is image.edit (Qwen-Image-Edit 2511) over a flat canvas of that colour (black for light, ff00ff for solid things:
// fx_sheets.py's two keys), so the plate stays flat. Takes land in <src>/_takes/ with a .json recipe; the first take is
// used until --pick says otherwise. A still whose recipe still matches is not made again. The sources live outside the
// repo (SickOfItGameEnemies/Generated/fx/<job>/), like art.mjs's work folders.

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { HawkesGen } from './hawkesgen.mjs';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const DOD = process.env.DOD_ROOT || 'C:\\Projects\\doseofdestructionwebsite';
const PYTHON = process.env.SOI_ART_PYTHON
  ?? [path.join(DOD, '.venv', 'Scripts', 'python.exe'), path.join(DOD, '.venv', 'bin', 'python')].find((p) => fs.existsSync(p))
  ?? (process.platform === 'win32' ? 'python' : 'python3');
const sha = (v) => crypto.createHash('sha1').update(JSON.stringify(v)).digest('hex').slice(0, 12);
const log = (...a) => console.log(...a);

function run(cmd, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { cwd: REPO, stdio: ['ignore', 'pipe', 'inherit'] });
    let out = '';
    child.stdout.on('data', (d) => { out += d; process.stdout.write(d); });
    child.on('error', reject);
    child.on('close', (code) => (code === 0 ? resolve(out) : reject(new Error(`${path.basename(cmd)} exited ${code}`))));
  });
}

async function blank(file, size, hex) {
  if (fs.existsSync(file)) return;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const code = `from PIL import Image; Image.new('RGB', (${size[0]}, ${size[1]}), '#${hex}').save(r'${file}')`;
  await run(PYTHON, ['-c', code]);
}

async function main() {
  const args = process.argv.slice(2);
  const jobFile = path.resolve(args[0] ?? '');
  if (!fs.existsSync(jobFile)) throw new Error('Usage: node Tools/Art/fx_make.mjs Tools/Art/fx/<job>.json [--redo a,b] [--pick a=seed] [--no-bake] [--takes N]');
  const redo = new Set();
  const picks = [];
  let bake = true;
  let takesFlag = null;
  for (let i = 1; i < args.length; i++) {
    if (args[i] === '--redo') args[++i].split(',').forEach((x) => redo.add(x));
    else if (args[i] === '--pick') picks.push(args[++i]);
    else if (args[i] === '--no-bake') bake = false;
    else if (args[i] === '--takes') takesFlag = Number(args[++i]);
  }
  const job = JSON.parse(fs.readFileSync(jobFile, 'utf8'));
  const name = path.basename(jobFile, '.json');
  const src = path.join(REPO, '..', 'SickOfItGameEnemies', 'Generated', 'fx', name);
  fs.mkdirSync(path.join(src, '_takes'), { recursive: true });
  const gen = new HawkesGen({ project: 'sickofit' });
  for (const p of picks) {
    const [still, seed] = p.split('=');
    const take = path.join(src, '_takes', `${still.replace(/\.png$/, '')}-${seed}.png`);
    fs.copyFileSync(take, path.join(src, still.endsWith('.png') ? still : `${still}.png`));
    fs.copyFileSync(take.replace(/\.png$/, '.json'), path.join(src, `${still.replace(/\.png$/, '')}.json`));
    log(`${still}: using seed ${seed}`);
  }
  await Promise.all(Object.entries(job.gen ?? {}).map(async ([still, g]) => {
    const base = still.replace(/\.png$/, '');
    const out = path.join(src, `${base}.png`);
    const size = g.size ?? [1024, 1024];
    const key = sha({ p: g.prompt, c: g.canvas ?? '000000', s: size });
    const recipe = path.join(src, `${base}.json`);
    const current = fs.existsSync(out) && fs.existsSync(recipe) && JSON.parse(fs.readFileSync(recipe, 'utf8')).key === key;
    if (current && !redo.has(base)) return;
    const canvas = path.join(src, '_takes', `canvas-${g.canvas ?? '000000'}-${size.join('x')}.png`);
    await blank(canvas, size, g.canvas ?? '000000');
    const n = takesFlag ?? Math.min(g.takes ?? 1, Math.max(1, Number(process.env.SOI_ART_TAKES) || 1)); // one take unless asked
    const seeds = Array.from({ length: n }, (_, i) => (redo.has(base) ? crypto.randomInt(1, 2 ** 31) : (parseInt(sha(`${key}:${i}`).slice(0, 8), 16) % (2 ** 31 - 1)) + 1));
    log(`${still}: ${n} takes`);
    const jobs = await gen.submitMany(seeds.map((seed) => ({
      tool: 'image.edit', inputs: { images: [canvas] }, labels: { fx: name, still: base },
      params: { prompt: g.prompt, width: size[0], height: size[1], seed, count: 1 }, key: `sickofit:fx:${name}:${base}:${key}:${seed}`,
    })));
    const done = await Promise.all(jobs.map((j) => gen.wait(j.id, {})));
    let first = null;
    for (const d of done) {
      const o = d.outputs?.[0];
      if (!o) continue;
      const take = path.join(src, '_takes', `${base}-${o.seed}.png`);
      await gen.download(o.asset, take);
      fs.writeFileSync(take.replace(/\.png$/, '.json'), JSON.stringify({ key, still, seed: o.seed, prompt: g.prompt, canvas: g.canvas ?? '000000', job: d.id, asset: o.asset, made: new Date().toISOString() }, null, 1));
      first ??= take;
    }
    if (!first) throw new Error(`${still}: no take finished`);
    fs.copyFileSync(first, out);
    fs.copyFileSync(first.replace(/\.png$/, '.json'), recipe);
    log(`${still}: using ${path.basename(first)}`);
  }));
  if (bake) await run(PYTHON, [path.join(REPO, 'Tools', 'Art', 'fx_sheets.py'), jobFile, '--src', src, '--preview', path.join(src, 'preview.png')]);
}

main().catch((e) => { console.error(`error: ${e.message}`); process.exit(1); });
