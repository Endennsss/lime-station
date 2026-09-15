import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

// Планирует уборку только импортов: используемые RSI и исходные лицензии сохраняются.
const output = path.resolve(process.argv[2]);
const repo = process.cwd();
if (output.startsWith(repo + path.sep)) throw Error('Резервная копия должна быть вне репозитория');
const importedRoot = 'Resources/Textures/_Lime/Imported/';
const json = filename => JSON.parse(fs.readFileSync(filename, 'utf8').replace(/^\uFEFF/, ''));
const text = execFileSync('rg', ['-o', '--no-filename', '_Lime/Imported/(?:Goob|Monolith|Forge|DeadSpace)/[A-Za-z0-9_./-]+\\.rsi',
    'Resources', 'Content.Client', 'Content.Shared', 'Content.Server',
    '-g', '!Resources/Textures/_Lime/Imported/**', '-g', '*.cs', '-g', '*.yml', '-g', '*.yaml',
    '-g', '*.json', '-g', '*.xaml', '-g', '*.ftl'], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
const used = new Set(text.trim().split(/\r?\n/).map(relative => 'Resources/Textures/' + relative));
const targets = [], patchFiles = [], summaries = [];
fs.mkdirSync(output, { recursive: true });
for (const provider of ['Goob', 'Monolith', 'Forge']) {
    const base = importedRoot + provider + '/';
    const filename = base + 'import_manifest.json';
    const before = fs.readFileSync(filename, 'utf8');
    const manifest = json(filename);
    const assets = manifest.assetManifests.flatMap(name => json(base + name));
    const removed = new Set(manifest.cleanup?.removedDestinations ?? []);
    let count = 0, bytes = 0;
    for (const asset of assets) {
        if (used.has(asset.destination) || removed.has(asset.destination)) continue;
        const from = path.resolve(repo, asset.destination);
        if (!from.startsWith(path.resolve(repo, base) + path.sep) || !from.endsWith('.rsi')) throw Error('Небезопасный путь');
        if (!fs.existsSync(from)) throw Error('Неучтённый отсутствующий ресурс: ' + asset.destination);
        for (const name of Object.keys(asset.sha256)) bytes += fs.statSync(path.join(from, name)).size;
        targets.push({ from, to: path.resolve(output, 'backup', asset.destination), destination: asset.destination });
        removed.add(asset.destination); count++;
    }
    manifest.cleanup = { policy: 'Unreferenced imported RSI removed from checkout; historical attribution and hashes retained.',
        retainedRsi: assets.length - removed.size, removedRsi: removed.size, removedDestinations: [...removed].sort() };
    summaries.push({ provider, removedRsi: count, retainedRsi: manifest.cleanup.retainedRsi, removedBytes: bytes });
    const after = JSON.stringify(manifest, null, 2);
    const oldLines = before.trimEnd().split(/\r?\n/), newLines = after.split('\n'), diff = [];
    let i = 0, j = 0;
    while (i < oldLines.length || j < newLines.length) {
        if (i < oldLines.length && j < newLines.length && oldLines[i] === newLines[j]) {
            diff.push(' ' + oldLines[i++]); j++; continue;
        }
        let oldEnd = oldLines.length, newEnd = newLines.length, distance = Infinity;
        for (let a = i; a < Math.min(oldLines.length, i + 160); a++)
            for (let b = j; b < Math.min(newLines.length, j + 160); b++)
                if (a - i + b - j < distance && oldLines[a] === newLines[b]
                    && oldLines[a + 1] === newLines[b + 1] && oldLines[a + 2] === newLines[b + 2]) {
                    oldEnd = a; newEnd = b; distance = a - i + b - j;
                }
        diff.push(...oldLines.slice(i, oldEnd).map(line => '-' + line), ...newLines.slice(j, newEnd).map(line => '+' + line));
        i = oldEnd; j = newEnd;
    }
    const ranges = [];
    for (let index = 0; index < diff.length; index++) {
        if (diff[index].startsWith(' ')) continue;
        const start = Math.max(0, index - 3), end = Math.min(diff.length, index + 4);
        if (ranges.length && start <= ranges.at(-1)[1]) ranges.at(-1)[1] = end;
        else ranges.push([start, end]);
    }
    const patch = ['*** Begin Patch', '*** Update File: ' + filename];
    for (const [start, end] of ranges) patch.push('@@', ...diff.slice(start, end));
    patch.push('*** End Patch');
    const target = path.join(output, provider + '.patch');
    fs.writeFileSync(target, patch.join('\n') + '\n'); patchFiles.push(target);
}
for (const reference of used) if (!fs.existsSync(path.join(repo, reference, 'meta.json'))) throw Error('Сломанная ссылка: ' + reference);
fs.writeFileSync(path.join(output, 'move_plan.json'), JSON.stringify({ repo, output, targets }, null, 2));
console.log(JSON.stringify({ summaries, targets: targets.length, patchFiles, backup: path.join(output, 'backup') }));
