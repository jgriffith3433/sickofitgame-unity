#!/usr/bin/env node
// Bakes video.i2v clips (flat 00ff00 background) into the sprite sheets art.mjs would make, with only ffmpeg and node
// (no Pillow/numpy/DoD checkout): for a machine where `art.mjs --build` can't run its Python.
//
//   node Tools/Art/bake_clips.mjs <job.json> [--dry]
//
// job.json: { "work": "<folder for decoded frames>", "scale": 0.65, "srcFps": 16, "outFps": 12, "margin": 3,
//   "entities": [ { "folder": "Assets/Art/Enemies/LauncherThief", "prefix": "LauncherThief", "ppu": 94.829,
//       "anchorFrom": "<video>",  // frame 0 of this video: its keyed bbox centre is the shared pivot point of every clip of the entity
//       "clips": [ { "name": "idle", "video": "<mp4>", "from": 0, "to": 32, "also": ["move"] } ] } ] }
//
// Keying: a pixel is background when it is much greener than it is red or blue (g - max(r,b), soft 40..70), so the
// pale green box some i2v frames carry goes with it and cyan glow stays. Green is despilled on what is kept, specks
// under 60 px go, the clip is cropped to the union box of its frames and scaled by `scale` (premultiplied area average).
// The sheet layout, sidecar (`*.sheet.json`, Unity-style y from the bottom) and pivot mirror Tools/Art/sheets.py.

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const jobFile = process.argv[2];
if (!jobFile) { console.error('Usage: node Tools/Art/bake_clips.mjs <job.json>'); process.exit(2); }
const job = JSON.parse(fs.readFileSync(jobFile, 'utf8'));
const REPO = path.resolve(path.dirname(new URL(import.meta.url).pathname), '..', '..');
const SIZE = job.size ?? 640;
const MAX_SHEET = 2048;

function ffmpeg(args, input) {
  const r = spawnSync('ffmpeg', ['-v', 'error', '-y', ...args], { input, maxBuffer: 1 << 30 });
  if (r.status !== 0) throw new Error(`ffmpeg ${args.join(' ')}: ${r.stderr}`);
  return r.stdout;
}

function decode(video) {
  const buf = ffmpeg(['-i', video, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], undefined);
  const fb = SIZE * SIZE * 3;
  const out = [];
  for (let o = 0; o + fb <= buf.length; o += fb) out.push(buf.subarray(o, o + fb));
  return out;
}

/** RGB frame -> RGBA (Uint8Array) keyed and despilled. */
function key(rgb) {
  const n = SIZE * SIZE;
  const px = new Uint8Array(n * 4);
  for (let i = 0; i < n; i++) {
    const r = rgb[i * 3], g = rgb[i * 3 + 1], b = rgb[i * 3 + 2];
    const m = Math.max(r, b);
    const d = g - m;
    let a = d >= 70 ? 0 : d <= 40 ? 1 : (70 - d) / 30;
    const gg = g > m ? m : g;
    px[i * 4] = r; px[i * 4 + 1] = gg; px[i * 4 + 2] = b; px[i * 4 + 3] = Math.round(a * 255);
  }
  return px;
}

/** Drops alpha components under `min` pixels (the video's compression speckle). */
function despeck(px, min = 60) {
  const n = SIZE * SIZE;
  const seen = new Uint8Array(n);
  const stack = new Int32Array(n);
  for (let s = 0; s < n; s++) {
    if (seen[s] || px[s * 4 + 3] < 128) continue;
    let sp = 0, count = 0;
    const comp = [];
    stack[sp++] = s; seen[s] = 1;
    while (sp) {
      const p = stack[--sp]; comp.push(p); count++;
      const x = p % SIZE, y = (p / SIZE) | 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= SIZE || ny >= SIZE) continue;
        const q = ny * SIZE + nx;
        if (!seen[q] && px[q * 4 + 3] >= 128) { seen[q] = 1; stack[sp++] = q; }
      }
    }
    if (count < min) for (const p of comp) px[p * 4 + 3] = 0;
  }
  // soft-alpha leftovers next to nothing
  for (let i = 0; i < n; i++) if (px[i * 4 + 3] < 128 && px[i * 4 + 3] > 0) { /* keep the soft edge */ }
  return px;
}

function bbox(px) {
  let x0 = SIZE, y0 = SIZE, x1 = -1, y1 = -1;
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
    if (px[(y * SIZE + x) * 4 + 3] >= 128) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  }
  return { x0, y0, x1, y1 };
}

/** Crop (src box) and scale to w x h, premultiplied area average; returns RGBA Uint8Array. */
function cropScale(px, box, w, h, s) {
  const out = new Uint8Array(w * h * 4);
  for (let y = 0; y < h; y++) {
    const sy0 = box.y0 + y / s, sy1 = box.y0 + (y + 1) / s;
    for (let x = 0; x < w; x++) {
      const sx0 = box.x0 + x / s, sx1 = box.x0 + (x + 1) / s;
      let r = 0, g = 0, b = 0, a = 0, wsum = 0;
      for (let yy = Math.floor(sy0); yy < Math.ceil(sy1); yy++) {
        if (yy < 0 || yy >= SIZE) { wsum += 0; continue; }
        const wy = Math.min(yy + 1, sy1) - Math.max(yy, sy0);
        for (let xx = Math.floor(sx0); xx < Math.ceil(sx1); xx++) {
          if (xx < 0 || xx >= SIZE) continue;
          const wx = Math.min(xx + 1, sx1) - Math.max(xx, sx0);
          const wgt = wx * wy; const i = (yy * SIZE + xx) * 4; const al = px[i + 3] / 255;
          r += px[i] * al * wgt; g += px[i + 1] * al * wgt; b += px[i + 2] * al * wgt; a += al * wgt; wsum += wgt;
        }
      }
      const o = (y * w + x) * 4;
      if (wsum > 0 && a > 0) { out[o] = Math.round(r / a); out[o + 1] = Math.round(g / a); out[o + 2] = Math.round(b / a); out[o + 3] = Math.round((a / wsum) * 255); }
    }
  }
  return out;
}

function writePng(file, rgba, w, h) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const png = ffmpeg(['-f', 'rawvideo', '-pix_fmt', 'rgba', '-s', `${w}x${h}`, '-i', '-', '-frames:v', '1', '-f', 'image2pipe', '-c:v', 'png', '-'], Buffer.from(rgba.buffer, rgba.byteOffset, rgba.length));
  fs.writeFileSync(file, png);
}

const round3 = (v) => Math.round(v * 1e6) / 1e6;

for (const ent of job.entities) {
  const anchorFrames = decode(ent.anchorFrom);
  const sc = ent.scale ?? job.scale;
  const ab = bbox(despeck(key(anchorFrames[ent.anchorFrame ?? 0])));
  const anchor = { x: (ab.x0 + ab.x1) / 2, y: (ab.y0 + ab.y1) / 2 };
  console.log(`${ent.prefix}: anchor bbox ${JSON.stringify(ab)} h=${ab.y1 - ab.y0 + 1}px -> ${((ab.y1 - ab.y0 + 1) * sc).toFixed(0)}px at scale ${sc}`);
  for (const clip of ent.clips) {
    const frames = decode(clip.video);
    const from = clip.from ?? 0, to = Math.min(clip.to ?? frames.length - 1, frames.length - 1);
    const picks = [];
    const step = job.srcFps / job.outFps;
    for (let i = 0; from + Math.round(i * step) <= to; i++) picks.push(from + Math.round(i * step));
    const keyed = picks.map((f) => despeck(key(frames[f])));
    const boxes = keyed.map(bbox);
    const u = { x0: Math.min(...boxes.map((b) => b.x0)), y0: Math.min(...boxes.map((b) => b.y0)), x1: Math.max(...boxes.map((b) => b.x1)), y1: Math.max(...boxes.map((b) => b.y1)) };
    const m = job.margin ?? 3;
    const sbox = { x0: u.x0 - m / sc, y0: u.y0 - m / sc, x1: u.x1 + m / sc, y1: u.y1 + m / sc };
    const w = Math.ceil((sbox.x1 - sbox.x0 + 1) * sc), h = Math.ceil((sbox.y1 - sbox.y0 + 1) * sc);
    const cells = keyed.map((px) => cropScale(px, sbox, w, h, sc));
    const cols = Math.max(1, Math.min(cells.length, Math.floor(MAX_SHEET / w)));
    const rows = Math.ceil(cells.length / cols);
    if (cols * w > MAX_SHEET || rows * h > MAX_SHEET) throw new Error(`${ent.prefix}_${clip.name}: sheet ${cols * w}x${rows * h} over ${MAX_SHEET}`);
    const sheet = new Uint8Array(cols * w * rows * h * 4);
    const sprites = [];
    cells.forEach((cell, i) => {
      const cx = (i % cols) * w, cy = Math.floor(i / cols) * h;
      for (let y = 0; y < h; y++) sheet.set(cell.subarray(y * w * 4, (y + 1) * w * 4), ((cy + y) * cols * w + cx) * 4);
      sprites.push({ name: `${clip.name}_${String(i).padStart(2, '0')}`, x: cx, y: (rows - 1 - Math.floor(i / cols)) * h, w, h });
    });
    const pivot = [round3((anchor.x - sbox.x0) * sc / w), round3(1 - (anchor.y - sbox.y0) * sc / h)];
    for (const name of [clip.name, ...(clip.also ?? [])]) {
      const base = path.join(REPO, ent.folder, `${ent.prefix}_${name}`);
      writePng(`${base}.png`, sheet, cols * w, rows * h);
      const side = { generatedBy: 'Tools/Art/bake_clips.mjs', pixelsPerUnit: ent.ppu, pivot, sprites: sprites.map((s) => ({ ...s, name: s.name.replace(clip.name + '_', name + '_') })) };
      fs.writeFileSync(`${base}.sheet.json`, JSON.stringify(side, null, 1) + '\n');
      console.log(`  ${ent.prefix}_${name}: ${cells.length} frames ${w}x${h}, sheet ${cols * w}x${rows * h}, pivot ${pivot}, src ${from}-${to}`);
    }
  }
}
