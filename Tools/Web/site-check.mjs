#!/usr/bin/env node
// Checks the website content this repo owns (Tools/Web/site) against a checkout of the website, without Unity:
// every page's links, images, scripts and #anchors resolve (in this folder or the website), every site.json insert
// finds its place, every page has a footer for the deploy's credit and a phone viewport, and nothing says what the
// game never says (the retired name Gloob, office jokes, internal hosts).
//   node Tools/Web/site-check.mjs [path to sickofitgamewebsite] [--install <dir>]
// --install copies the website into <dir> and installs the content there the way the deploy does (step 7d,
// Assets/Scripts/Tools/Editor/SiteContent.cs: keep the two in step), for a local look.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const source = path.join(here, "site");
const args = process.argv.slice(2);
const installAt = args.includes("--install") ? args[args.indexOf("--install") + 1] : null;
const website = path.resolve(args.find((a, i) => !a.startsWith("--") && args[i - 1] !== "--install") || path.join(here, "../../../sickofitgamewebsite"));

const problems = [];
const problem = (where, what) => problems.push(`${where}: ${what}`);
const walk = (dir, base = dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
  const full = path.join(dir, e.name);
  return e.isDirectory() ? walk(full, base) : [path.relative(base, full).split(path.sep).join("/")];
});
const isCopied = (rel) => rel !== "site.json" && rel !== "README.md" && !rel.split("/").some((p) => p.startsWith("_") || p.startsWith("."));

// The same as SiteContent.ApplyInsert.
function applyInsert(html, id, after, until, snippet) {
  const nl = html.includes("\r\n") ? "\r\n" : "\n";
  const start = `<!-- Hawkes site: ${id} -->`, end = `<!-- /Hawkes site: ${id} -->`;
  const block = start + nl + snippet.replace(/\r\n/g, "\n").replace(/\n+$/, "").split("\n").join(nl) + nl + end;
  const esc = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const existing = new RegExp(esc(start) + "[\\s\\S]*?" + esc(end)).exec(html);
  if (existing) return html.slice(0, existing.index) + block + html.slice(existing.index + existing[0].length);
  const place = new RegExp(after).exec(html);
  if (!place) return null;
  const at = place.index + place[0].length;
  if (!until) return html.slice(0, at) + nl + block + html.slice(at);
  const re = new RegExp(until, "g"); re.lastIndex = at;
  const stop = re.exec(html);
  if (!stop) return null;
  const lineStart = html.lastIndexOf("\n", Math.max(0, stop.index - 1)) + 1;
  const before = html.slice(lineStart, stop.index);
  const indent = lineStart > at && before.trim() === "" ? before : "";
  return html.slice(0, at) + nl + block + nl + indent + html.slice(stop.index);
}

if (!fs.existsSync(path.join(website, "blog"))) {
  console.error(`No website checkout at ${website}`);
  process.exit(2);
}

// The site as the deploy would leave it: our files over the website's, then the inserts.
const pages = new Map(); // rel -> html
const owned = walk(source).filter(isCopied);
for (const rel of owned) if (rel.endsWith(".html")) pages.set(rel, fs.readFileSync(path.join(source, rel), "utf8"));
const manifest = JSON.parse(fs.readFileSync(path.join(source, "site.json"), "utf8"));
for (const ins of manifest.inserts) {
  const rel = ins.page;
  const html = pages.get(rel) ?? (fs.existsSync(path.join(website, rel)) ? fs.readFileSync(path.join(website, rel), "utf8") : null);
  if (html == null) { problem(`insert ${ins.id}`, `no page ${rel} in the website`); continue; }
  const snippet = fs.readFileSync(path.join(source, ins.file), "utf8");
  const once = applyInsert(html, ins.id, ins.after, ins.until, snippet);
  if (once == null) { problem(`insert ${ins.id}`, `'${ins.after}' not found in ${rel}`); continue; }
  if (applyInsert(once, ins.id, ins.after, ins.until, snippet) !== once) problem(`insert ${ins.id}`, "running it twice changes the page");
  pages.set(rel, once);
}

const exists = (rel) => owned.includes(rel) || fs.existsSync(path.join(website, rel));
const read = (rel) => pages.get(rel) ?? (fs.existsSync(path.join(website, rel)) ? fs.readFileSync(path.join(website, rel), "utf8") : "");
const banned = [[/gloob/i, "the retired name Gloob"], [/\b(monday|inbox|meeting|coffee|office|cubicle|deadline)s?\b/i, "an office/work joke"],
  [/127\.0\.0\.1|localhost|C:\\\\|\\\\Projects|hawkesstudio|:879\d/i, "an internal address"], [/sk-[A-Za-z0-9]{12,}|api[_-]?key/i, "something like a key"]];

let refs = 0;
for (const [rel, html] of pages) {
  const ownedPage = owned.includes(rel);
  if (ownedPage) {
    if (!/<meta name="viewport" content="width=device-width/.test(html)) problem(rel, "no phone viewport");
    if (!/<\/footer>/i.test(html)) problem(rel, "no <footer> (the deploy's credit goes there)");
    if (/<!-- Hawkes credit:/.test(html)) problem(rel, "carries a credit block (the deploy writes it, step 7c)");
  }
  const text = ownedPage ? html : manifest.inserts.filter((i) => i.page === rel).map((i) => fs.readFileSync(path.join(source, i.file), "utf8")).join("\n");
  for (const [re, what] of banned) {
    const m = re.exec(text.replace(/<!--[\s\S]*?-->/g, ""));
    if (m) problem(rel, `${what}: "${m[0]}"`);
  }
  for (const m of text.matchAll(/\b(?:href|src|data-bg)="([^"]+)"|url\(([^)]+)\)/g)) {
    const raw = (m[1] || m[2] || "").replace(/^["']|["']$/g, "");
    if (!raw || /^(https?:|mailto:|data:|javascript:|\$\{)/.test(raw)) continue;
    refs++;
    const [p, frag] = raw.split("#");
    let target = p ? path.posix.normalize(path.posix.join(path.posix.dirname(rel), p)) : rel;
    if (target.startsWith("/")) target = target.slice(1);
    if (target.startsWith("..")) { problem(rel, `${raw} leaves the site`); continue; }
    if (p.endsWith("/") || target === "." || target === "") target = path.posix.join(target === "." ? "" : target, "index.html");
    if (target.startsWith("Game/")) continue; // the game's own folder: the build's, not checked here
    if (!exists(target)) { problem(rel, `${raw} -> ${target} is missing`); continue; }
    if (frag && target.endsWith(".html") && !new RegExp(`id="${frag}"`).test(read(target))) problem(rel, `${raw}: no #${frag} in ${target}`);
  }
}
// CSS url()s in our stylesheets
for (const rel of owned.filter((r) => r.endsWith(".css"))) {
  for (const m of fs.readFileSync(path.join(source, rel), "utf8").matchAll(/url\("?([^")]+)"?\)/g)) {
    const target = path.posix.normalize(path.posix.join(path.posix.dirname(rel), m[1]));
    refs++;
    if (!exists(target)) problem(rel, `${m[1]} -> ${target} is missing`);
  }
}

const imgBytes = owned.filter((r) => /\.(webp|png|jpe?g)$/.test(r)).reduce((n, r) => n + fs.statSync(path.join(source, r)).size, 0);
console.log(`${pages.size} pages (${owned.filter((r) => r.endsWith(".html")).length} owned, ${manifest.inserts.length} inserts), ${refs} references checked, images ${(imgBytes / 1024).toFixed(0)} KB`);

if (installAt) {
  fs.rmSync(installAt, { recursive: true, force: true });
  fs.cpSync(website, installAt, { recursive: true, filter: (src) => !/[\\/](\.git|Game|videos|music|shorts|backend)([\\/]|$)/.test(path.relative(website, src)) });
  for (const rel of owned) { fs.mkdirSync(path.dirname(path.join(installAt, rel)), { recursive: true }); fs.copyFileSync(path.join(source, rel), path.join(installAt, rel)); }
  for (const [rel, html] of pages) if (!owned.includes(rel)) fs.writeFileSync(path.join(installAt, rel), html);
  console.log(`installed into ${installAt}`);
}

if (problems.length) {
  console.log(problems.map((p) => "  PROBLEM " + p).join("\n"));
  process.exit(1);
}
console.log("OK");
