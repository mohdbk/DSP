#!/usr/bin/env node
// Works out where each text key is used and writes it to the "usage" block of the shipped texts, so
// Settings → Translations can show and filter "Portal", "Back office", "Both" and "Services".
//
//   node tools/i18n/usage.mjs          update src/Services/Platform/.../Seed/translations.json
//   node tools/i18n/usage.mjs --check  fail if the file is out of date (CI)
//
// A key counts as used where it appears as a string literal ('key', "key" or `key`; one-word keys only inside
// t(), text(), Format() or Of()), or where a template builds it
// from a prefix (t(`status.${s}`) uses every status.* key). Files in web/packages count for the portal(s) that use
// what they export, directly or through other package files. C# files under src/ count as "services": those
// messages are rendered by the APIs and can appear in either portal.
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';

const root = new URL('../..', import.meta.url).pathname;
const seedPath = join(root, 'src/Services/Platform/Dsp.Platform.Infrastructure/Seed/translations.json');
const check = process.argv.includes('--check');

function files(dir, exts) {
  const out = [];
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name === 'bin' || name === 'obj' || name === 'dist' || name.startsWith('.')) continue;
    const path = join(dir, name);
    if (statSync(path).isDirectory()) out.push(...files(path, exts));
    else if (exts.some(e => name.endsWith(e))) out.push(path);
  }
  return out;
}

const raw = readFileSync(seedPath, 'utf8');
const seed = JSON.parse(raw);
const keys = Object.keys(seed.strings);

/** Keys a source text uses: exact literals plus template prefixes. */
function keysIn(text) {
  const found = new Set();
  const literals = new Set();
  for (const m of text.matchAll(/(['"`])([a-zA-Z][\w-]*(?:\.[\w-]+)+)\1/g)) literals.add(m[2]);
  // One-word keys ("save") only count inside a text call, since the same word is common in code.
  for (const m of text.matchAll(/\b(?:t|text|Format|Of)\(\s*(['"`])([a-zA-Z][\w-]*)\1/g)) literals.add(m[2]);
  for (const k of keys) if (literals.has(k)) found.add(k);
  const prefixes = [...text.matchAll(/[`"]([a-zA-Z][\w-]*(?:\.[\w-]+)*\.)(?:\$\{|\{)/g)].map(m => m[1]);
  for (const p of prefixes) for (const k of keys) if (k.startsWith(p)) found.add(k);
  return found;
}

const exts = ['.ts', '.tsx'];
const apps = { portal: files(join(root, 'web/apps/portal/src'), exts), backoffice: files(join(root, 'web/apps/backoffice/src'), exts) };
const packageFiles = files(join(root, 'web/packages'), exts).filter(f => !f.endsWith('.d.ts'));
const read = new Map([...apps.portal, ...apps.backoffice, ...packageFiles].map(f => [f, readFileSync(f, 'utf8')]));

// Which portals reach each package file: through its exports, used by an app or by another package file.
const exportsOf = new Map(packageFiles.map(f => [f,
  [...read.get(f).matchAll(/export\s+(?:default\s+)?(?:async\s+)?(?:function|const|class|let)\s+(\w+)/g)].map(m => m[1])]));
const mentions = (text, names) => names.some(n => new RegExp(`\\b${n}\\b`).test(text));
const reach = new Map(packageFiles.map(f => [f, new Set()]));
for (const [app, list] of Object.entries(apps))
  for (const f of packageFiles)
    if (list.some(a => mentions(read.get(a), exportsOf.get(f)))) reach.get(f).add(app);
for (let changed = true; changed;) {
  changed = false;
  for (const f of packageFiles)
    for (const g of packageFiles) {
      if (f === g || !mentions(read.get(g), exportsOf.get(f))) continue;
      for (const app of reach.get(g)) if (!reach.get(f).has(app)) { reach.get(f).add(app); changed = true; }
    }
}

const usage = Object.fromEntries(keys.map(k => [k, new Set()]));
const mark = (text, where) => { for (const k of keysIn(text)) where.forEach(w => usage[k].add(w)); };
for (const [app, list] of Object.entries(apps)) for (const f of list) mark(read.get(f), [app]);
for (const f of packageFiles) mark(read.get(f), [...reach.get(f)]);
for (const f of files(join(root, 'src'), ['.cs', '.json']).filter(f => f !== seedPath)) mark(readFileSync(f, 'utf8'), ['services']);

const order = ['portal', 'backoffice', 'services'];
seed.usage = Object.fromEntries(keys.map(k => [k, order.filter(o => usage[k].has(o))]));
const next = JSON.stringify(seed, null, 1) + '\n';

const unused = keys.filter(k => seed.usage[k].length === 0);
const count = w => keys.filter(k => seed.usage[k].includes(w)).length;
console.log(`${keys.length} keys · portal ${count('portal')} · back office ${count('backoffice')} · services ${count('services')} · unused ${unused.length}`);
if (unused.length) console.log(`unused: ${unused.join(', ')}`);

if (check) {
  if (next !== raw) {
    console.error(`${relative(root, seedPath)} has out-of-date usage. Run: node tools/i18n/usage.mjs`);
    process.exit(1);
  }
} else {
  writeFileSync(seedPath, next);
}
