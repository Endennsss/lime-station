import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

// Импортирует только графику с явной лицензией; код и игровые механики Goob не переносит.
const repo = process.cwd();
const source = path.resolve(process.argv[2] ?? '.');
const mode = process.argv[3] ?? '--audit';
const root = 'Resources/Textures/';
const destination = `${root}_Lime/Imported/Goob/`;
const manifestPath = `${destination}import_manifest.json`;
const franchisePath = /(?:^|[/_.-])(amongus|among_us|jojo|kirby|mario|sonic|zelda|omniman|helldivers?|pokemon|warhammer|cosplay|nazgul|goku|naruto|luffy|deltarune|undertale)(?:$|[/_.-])/i;
const allowed = new Set(['CC0-1.0', 'CC-BY-3.0', 'CC-BY-4.0', 'CC-BY-SA-3.0', 'CC-BY-SA-4.0', 'MIT']);
const licenseUrls = {
    'CC0-1.0': 'https://creativecommons.org/publicdomain/zero/1.0/',
    'CC-BY-3.0': 'https://creativecommons.org/licenses/by/3.0/',
    'CC-BY-4.0': 'https://creativecommons.org/licenses/by/4.0/',
    'CC-BY-SA-3.0': 'https://creativecommons.org/licenses/by-sa/3.0/',
    'CC-BY-SA-4.0': 'https://creativecommons.org/licenses/by-sa/4.0/',
    MIT: 'https://opensource.org/license/mit/',
};
const json = filename => JSON.parse(fs.readFileSync(filename, 'utf8').replace(/^\uFEFF/, ''));
const sha = filename => crypto.createHash('sha256').update(fs.readFileSync(filename)).digest('hex');
const git = (cwd, args) => execFileSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
const files = cwd => new Map(git(cwd, ['ls-tree', '-r', 'HEAD', '--', root]).trim().split('\n').filter(Boolean)
    .map(line => { const [head, filename] = line.split('\t'); return [filename, head.split(' ')[2]]; }));

function validateRsi(directory, meta) {
    if (meta.version !== 1 || !Number.isInteger(meta.size?.x) || !Number.isInteger(meta.size?.y)
        || meta.size.x <= 0 || meta.size.y <= 0 || !Array.isArray(meta.states) || meta.states.length === 0)
        throw new Error('invalid-rsi-metadata');
    const names = new Set();
    for (const state of meta.states) {
        if (!state.name || state.name.includes('/') || state.name.includes('\\') || names.has(state.name))
            throw new Error('invalid-state-name');
        names.add(state.name);
        const png = fs.readFileSync(path.join(directory, state.name + '.png'));
        if (png.length < 24 || png.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a')
            throw new Error('invalid-png');
        const width = png.readUInt32BE(16), height = png.readUInt32BE(20);
        const directions = state.directions ?? 1;
        if (![1, 4, 8].includes(directions) || width % meta.size.x || height % meta.size.y)
            throw new Error('invalid-state-dimensions');
        const frames = state.delays ? state.delays.reduce((sum, delays) => sum + delays.length, 0) : directions;
        if (frames > width / meta.size.x * height / meta.size.y)
            throw new Error('insufficient-animation-frames');
        if (state.delays?.some(delays => !Array.isArray(delays) || delays.some(delay => !Number.isFinite(delay) || delay < 0)))
            throw new Error('invalid-animation-delays');
    }
}

function compatible(old, next) {
    if (old.size.x !== next.size.x || old.size.y !== next.size.y) return false;
    const states = new Map(next.states.map(state => [state.name, state]));
    return old.states.every(state => states.has(state.name)
        && (states.get(state.name).directions ?? 1) === (state.directions ?? 1));
}

function* walk(directory) {
    if (!fs.existsSync(directory)) return;
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        const filename = path.join(directory, entry.name);
        if (entry.isDirectory()) yield* walk(filename);
        else if (entry.isFile()) yield filename;
    }
}

if (mode === '--verify' || mode === '--verify-index') {
    const manifest = json(manifestPath);
    const imported = manifest.assetManifests.flatMap(filename => json(path.resolve(repo, destination, filename)));
    const index = mode === '--verify-index' ? new Map(git(repo, ['ls-files', '--stage', '--', destination])
        .trim().split('\n').map(line => { const [head, name] = line.split('\t'); return [name, head.split(' ')[1]]; })) : null;
    const objectFormat = index ? git(repo, ['rev-parse', '--show-object-format']).trim() : null;
    let count = 0;
    for (const asset of imported) {
        validateRsi(path.resolve(repo, asset.destination), json(path.resolve(repo, asset.destination, 'meta.json')));
        for (const [filename, hash] of Object.entries(asset.sha256)) {
            if (sha(path.resolve(repo, asset.destination, filename)) !== hash)
                throw new Error(`Changed imported file: ${asset.destination}/${filename}`);
            if (index) {
                const bytes = fs.readFileSync(path.resolve(repo, asset.destination, filename));
                const oid = crypto.createHash(objectFormat).update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
                if (index.get(`${asset.destination}/${filename}`) !== oid)
                    throw new Error(`Git index changed imported bytes: ${asset.destination}/${filename}`);
            }
            count++;
        }
    }
    console.log(JSON.stringify({ verifiedRsi: imported.length, verifiedFiles: count, verifiedGitIndex: Boolean(index) }));
    process.exit(0);
}

const origin = git(source, ['remote', 'get-url', 'origin']).trim().replace(/\.git$/, '').toLowerCase();
if (origin !== 'https://github.com/goob-station/goob-station'
    && origin !== 'git@github.com:goob-station/goob-station') throw new Error('unexpected-source-repository');
const sourceFiles = files(source), localFiles = files(repo);
const commit = git(source, ['rev-parse', 'HEAD']).trim();
const assets = [], excluded = [], unchanged = [];
const airlockNames = {
    'airlock-base': 'basic', 'airlock-maints': 'maint',
    'engineering-base': 'engineering', 'engineering-atmos': 'atmospherics',
    'cargo-base': 'cargo', 'cargo-salvage': 'salvage', 'cargo-mining': 'mining',
    'medical-base': 'medical', 'medical-viro': 'virology', 'medical-chem': 'chemistry',
    'science-base': 'science', 'command-base': 'command', 'security-base': 'security',
    'service-base': 'basic', 'service-botany': 'hydroponics', 'service-freezer': 'freezer',
    'centcomm-base': 'centcomm', 'syndicate-base': 'syndicate', 'airlock-external': 'external',
    'airlock-hatch': 'hatch', 'airlock-hatch-maints': 'hatch_maint',
    'airlock-hatch-syndicate': 'hatch_syndicate', 'shuttle-base': 'shuttle',
    'shuttle-syndicate': 'shuttle_syndicate',
};
const groups = new Map();
for (const filename of sourceFiles.keys()) {
    const match = filename.match(/^(.*\.rsi)\//);
    if (match) {
        if (!groups.has(match[1])) groups.set(match[1], []);
        groups.get(match[1]).push(filename);
    }
}
for (const [directory, contents] of groups) {
    const relative = directory.slice(root.length);
    if (!/(^|\/)(Objects|Clothing)\//.test(relative) && !/(^|\/)Structures\/(Walls|Doors)\//.test(relative)) continue;
    const category = /Structures\/Walls\//.test(relative) ? 'walls'
        : /Structures\/Doors\//.test(relative) ? 'doors'
        : /(^|\/)Clothing\//.test(relative) ? 'clothing' : 'objects';
    const images = contents.filter(filename => filename.endsWith('.png'));
    if (images.length && images.every(filename => sourceFiles.get(filename) === localFiles.get(filename))) {
        unchanged.push(relative);
        continue;
    }
    try {
        const meta = json(path.join(source, directory, 'meta.json'));
        if (!allowed.has(meta.license)) throw new Error(`license:${meta.license ?? 'missing'}`);
        if (franchisePath.test(relative) || /\b(Nintendo|SEGA|Disney|Games Workshop|Pokemon|Pokémon|Warhammer)\b/i.test(meta.copyright + ' ' + (meta.source ?? '')))
            throw new Error('third-party-franchise-review-required');
        if (typeof meta.copyright !== 'string' || !meta.copyright.trim()) throw new Error('missing-copyright');
        if (/\b(non.?commercial|no.?derivatives|all rights reserved|permission required)\b/i.test(meta.copyright))
            throw new Error('restricted-copyright-notice');
        for (const filename of contents.filter(name => name.endsWith('.license'))) {
            const notice = fs.readFileSync(path.join(source, filename), 'utf8');
            const identifiers = [...notice.matchAll(/SPDX-License-Identifier:\s*([^\r\n]+)/g)].map(match => match[1].trim());
            if (identifiers.length === 0 || identifiers.some(id => !allowed.has(id))) throw new Error('restricted-sidecar');
        }
        validateRsi(path.join(source, directory), meta);
        const oldMetaPath = path.join(repo, directory, 'meta.json');
        const replacement = fs.existsSync(oldMetaPath) && compatible(json(oldMetaPath), meta);
        assets.push({ source: directory, destination: destination + relative.replace(/^_/, ''), category,
            license: meta.license, copyright: meta.copyright, originalSource: meta.source ?? null,
            licenseUrl: licenseUrls[meta.license], replacement,
            sha256: Object.fromEntries(contents.map(filename => [filename.slice(directory.length + 1), sha(path.join(source, filename))])) });
    } catch (error) {
        excluded.push({ source: directory, category, reason: error.message.startsWith('ENOENT') ? 'missing-metadata-or-image' : error.message });
    }
}
assets.sort((a, b) => a.source.localeCompare(b.source));
const bindings = new Map(), airlockBindings = new Map();
const normalize = filename => filename.replaceAll('_', '-');
const localRsi = [...localFiles.keys()].filter(filename => filename.endsWith('.rsi/meta.json')).map(filename => filename.slice(0, -10));
for (const asset of assets) {
    asset.replaces = [];
    const matches = localRsi.filter(directory => normalize(directory) === normalize(asset.source));
    for (const directory of matches) {
        if (compatible(json(path.join(repo, directory, 'meta.json')), json(path.join(source, asset.source, 'meta.json')))) {
            asset.replaces.push(directory.slice(root.length));
            bindings.set(directory.slice(root.length), asset.destination.slice(root.length));
        }
    }
    for (const [oldName, newName] of Object.entries(airlockNames)) {
        for (const kind of ['Standard', 'Glass']) {
            const original = `Structures/Doors/Airlocks/${kind}/${oldName}.rsi`;
            if (asset.source !== `${root}Structures/Doors/Airlocks/${kind}/${newName}.rsi`
                || !fs.existsSync(path.join(repo, root, original, 'meta.json'))) continue;
            const meta = json(path.join(source, asset.source, 'meta.json'));
            const states = new Set(meta.states.map(state => state.name));
            if (!['closed', 'open', 'opening', 'closing', 'closed_unlit', 'opening_unlit', 'closing_unlit',
                'bolted_unlit', 'welded', 'emergency_unlit', 'panel_open', 'panel_opening', 'panel_closing',
                'deny_unlit'].every(state => states.has(state))) continue;
            asset.replaces.push(original);
            bindings.set(original, asset.destination.slice(root.length));
            airlockBindings.set(original, asset.destination.slice(root.length));
        }
    }
    asset.replacement = asset.replaces.length > 0;
}

function airlockLayers(sprite, native = false) {
    // Схема слоёв взята из Lime Station: в старых RSI Goob основа и эффекты находятся вместе.
    const asset = assets.find(asset => asset.destination.slice(root.length) === sprite);
    const states = asset ? new Set(json(path.join(source, asset.source, 'meta.json')).states.map(state => state.name)) : null;
    const effects = native ? 'Structures/Doors/Airlocks/Effects/airlock-effects.rsi'
        : destination.slice(root.length) + 'Structures/Doors/Airlocks/Standard/basic.rsi';
    return [
        ['closed', 'enum.DoorVisualLayers.Base'],
        ['closed_unlit', 'enum.DoorVisualLayers.BaseUnlit', 'unshaded', false],
        ['welded', 'enum.WeldableLayers.BaseWelded'],
        ['bolted_unlit', 'enum.DoorVisualLayers.BaseBolted', 'unshaded'],
        ['emergency_unlit', 'enum.DoorVisualLayers.BaseEmergencyAccess', 'unshaded'],
        ['panel_open', 'enum.WiresVisualLayers.MaintenancePanel'],
        ['electrified_ai', 'enum.ElectrifiedLayers.HUD', 'unshaded', false, 'Interface/Misc/ai_hud.rsi'],
        ['electrified', 'enum.ElectrifiedLayers.Sparks', 'unshaded', false, 'Effects/electricity.rsi'],
        ['sparks', 'enum.DoorVisualLayers.BaseEmagging', 'unshaded', false],
    ].map(([state, map, shader, visible, other]) =>
        `    - state: ${state}\n      sprite: ${other ?? (state === 'closed' ? sprite : native || !states?.has(state) ? effects : sprite)}\n      map: ["${map}"]\n`
        + (shader ? `      shader: ${shader}\n` : '')
        + (visible === undefined ? '' : `      visible: ${visible}\n`)).join('');
}

function adaptAirlocks(text) {
    return text.split(/\n(?=- type:)/).map(block => {
        // Новые ванильные декоративные состояния отсутствуют в старых шлюзах Goob.
        block = block.replace(/^  - type: Sprite\n([\s\S]*?)(?=^  - type: |(?![\s\S]))/m, (original, body) => {
            const current = body.match(/^    sprite: (\S+)/m)?.[1];
            if (!airlockBindings.has(current)) return original;
            body = body.replace(/(^    - state: docking-clamp\n)(?!      sprite:)/gm,
                `$1      sprite: ${current} # Lime-Edit - оригинальный RSI для отсутствующего состояния\n`);
            return `  - type: Sprite\n${body}`;
        });
        const id = block.match(/^  id: (\S+)/m)?.[1];
        if (id !== 'BaseAirlockIndestructible' && !(id?.startsWith('Airlock') && !/Assembly|Frame/.test(id))) return block;
        let touched = false, native = false;
        let updated = block.replace(/^  - type: Sprite\n([\s\S]*?)(?=^  - type: |(?![\s\S]))/m, (original, body) => {
            const current = body.match(/^    sprite: (\S+)/m)?.[1];
            if (!current) return original;
            native = !airlockBindings.has(current);
            if (native && (!current.startsWith('Structures/Doors/Airlocks/')
                || !json(path.join(repo, root, current, 'meta.json')).states.some(state => state.directions === 4))) return original;
            touched = true;
            const sprite = native ? current : airlockBindings.get(current);
            let fields = body.replace(/^    layers:\n[\s\S]*/m, '').trimEnd();
            if (!native) fields = fields.replace(/^    sprite: .*$/m, `    sprite: ${sprite} # Lime-Edit - текстуры Goob с сохранением лицензии`);
            if (!/^    snapCardinals:/m.test(fields)) fields += `\n    snapCardinals: ${!native} # Lime-Edit - ориентация исходного набора`;
            const ornaments = body.match(/^    - state: docking-clamp\n(?:^      .*\n)*/gm)?.join('') ?? '';
            const layers = native && /^    layers:/m.test(body) ? body.slice(body.indexOf('    layers:'))
                : `    layers:\n${ornaments}${airlockLayers(sprite, native)}`;
            let result = `  - type: Sprite\n${fields}\n${layers}`;
            if (!/^  - type: Airlock$/m.test(block))
                result += `  - type: Airlock\n    openUnlitVisible: ${native} # Lime-Edit - освещение соответствует выбранным RSI\n`;
            return result;
        });
        if (touched && !native) updated = updated.replace(/(^    openUnlitVisible:) true/m, '$1 false # Lime-Edit - старые RSI не содержат open_unlit');
        return updated;
    }).join('\n');
}
const edits = [], prototypeFiles = new Set();
for (const filename of walk(path.join(repo, 'Resources/Prototypes'))) {
    if (!filename.endsWith('.yml') && !filename.endsWith('.yaml')) continue;
    const before = fs.readFileSync(filename, 'utf8');
    const adapted = filename.includes(`${path.sep}Doors${path.sep}Airlocks${path.sep}`)
        ? adaptAirlocks(before.replaceAll('\r\n', '\n')) : before;
    const after = adapted.split('\n').map(line => {
        if (line.includes('оригинальный RSI для отсутствующего состояния')) return line;
        const updated = line.replace(/(?:\/?Textures\/)?(?:[_A-Za-z0-9.-]+\/)+[_A-Za-z0-9.-]+\.rsi/g, match => {
            const prefix = match.match(/^\/?Textures\//)?.[0] ?? '';
            const asset = match.slice(prefix.length);
            return bindings.has(asset) ? prefix + bindings.get(asset) : match;
        });
        if (updated === line || line.trimStart().startsWith('#')) return line;
        return updated.replace(/\r$/, '') + (updated.includes('Lime-Edit') ? '' : ' # Lime-Edit - текстуры Goob с сохранением лицензии') + (line.endsWith('\r') ? '\r' : '');
    }).join('\n');
    if (before.replaceAll('\r\n', '\n') !== after.replaceAll('\r\n', '\n')
        || before.includes(destination.slice(root.length))) prototypeFiles.add(filename);
    if (before !== after) edits.push({ filename: path.relative(repo, filename).replaceAll('\\', '/'), before, after });
}
const summary = {
    commit, imported: assets.length, replacements: assets.filter(asset => asset.replacement).length,
    replacedPaths: bindings.size, airlockPaths: airlockBindings.size,
    prototypeFiles: prototypeFiles.size, unchanged: unchanged.length, excluded: excluded.length,
    byCategory: Object.fromEntries(['walls', 'doors', 'objects', 'clothing'].map(category => [category,
        { imported: assets.filter(asset => asset.category === category).length,
            replacements: assets.filter(asset => asset.category === category && asset.replacement).length }])),
    byLicense: Object.fromEntries([...allowed].map(license => [license, assets.filter(asset => asset.license === license).length])),
    exclusions: Object.fromEntries([...new Set(excluded.map(asset => asset.reason))].map(reason => [reason, excluded.filter(asset => asset.reason === reason).length])),
};
if (mode === '--audit') {
    console.log(JSON.stringify({ ...summary, excludedAssets: excluded,
        structureReplacements: assets.filter(asset => asset.replacement && ['walls', 'doors'].includes(asset.category)).map(asset => asset.source) }, null, 2));
} else if (mode === '--copy') {
    for (const asset of assets) {
        for (const filename of Object.keys(asset.sha256)) {
            const target = path.resolve(repo, asset.destination, filename);
            if (!target.startsWith(path.resolve(repo, destination) + path.sep)) throw new Error('unsafe-destination');
            fs.mkdirSync(path.dirname(target), { recursive: true });
            if (fs.existsSync(target) && sha(target) !== asset.sha256[filename]) throw new Error(`Refusing overwrite: ${target}`);
            fs.copyFileSync(path.resolve(source, asset.source, filename), target);
        }
    }
    console.log(JSON.stringify(summary));
} else if (mode === '--plan') {
    const planDir = path.resolve(process.argv[4]);
    if (planDir.startsWith(repo + path.sep)) throw new Error('plan-must-be-outside-repository');
    fs.mkdirSync(planDir, { recursive: true });
    const patch = ['*** Begin Patch'];
    for (const edit of edits) {
        patch.push(`*** Update File: ${edit.filename}`);
        const before = edit.before.split(/\r?\n/), after = edit.after.split(/\r?\n/);
        if (process.argv.includes('--full-prototypes') && edit.filename.includes('/Doors/Airlocks/')) {
            patch.push('@@', ...before.map(line => '-' + line), ...after.map(line => '+' + line));
            continue;
        }
        let oldIndex = 0, newIndex = 0;
        const changes = [];
        while (oldIndex < before.length || newIndex < after.length) {
            if (before[oldIndex] === after[newIndex]) {
                changes.push(' ' + before[oldIndex]); oldIndex++; newIndex++; continue;
            }
            let oldEnd = before.length, newEnd = after.length, distance = Infinity;
            for (let i = oldIndex; i < Math.min(before.length, oldIndex + 160); i++) {
                for (let j = newIndex; j < Math.min(after.length, newIndex + 160); j++) {
                    if (i - oldIndex + j - newIndex >= distance) continue;
                    if (before[i] === after[j] && before[i + 1] === after[j + 1] && before[i + 2] === after[j + 2]) {
                        oldEnd = i; newEnd = j; distance = i - oldIndex + j - newIndex;
                    }
                }
            }
            changes.push(...before.slice(oldIndex, oldEnd).map(line => '-' + line),
                ...after.slice(newIndex, newEnd).map(line => '+' + line));
            oldIndex = oldEnd; newIndex = newEnd;
        }
        const ranges = [];
        for (let index = 0; index < changes.length; index++) {
            if (changes[index].startsWith(' ')) continue;
            const start = Math.max(0, index - 3), end = Math.min(changes.length, index + 4);
            if (ranges.length && start <= ranges.at(-1)[1]) ranges.at(-1)[1] = end;
            else ranges.push([start, end]);
        }
        for (const [start, end] of ranges) patch.push('@@', ...changes.slice(start, end));
    }
    const patchFiles = [];
    const savePatch = (name, lines) => {
        const filename = path.join(planDir, name);
        fs.writeFileSync(filename, lines.join('\n') + '\n');
        patchFiles.push(filename);
    };
    patch.push('*** End Patch');
    let batch = ['*** Begin Patch'], header = '', hunk = [], number = 0;
    const flushHunk = () => {
        if (!hunk.length) return;
        if (batch.join('\n').length + hunk.join('\n').length > 30000 && batch.length > 1) {
            batch.push('*** End Patch');
            savePatch(`prototypes_${number++}.patch`, batch);
            batch = ['*** Begin Patch'];
        }
        if (batch.at(-1) !== header && !batch.includes(header)) batch.push(header);
        batch.push(...hunk);
        hunk = [];
    };
    for (const line of patch.slice(1, -1)) {
        if (line.startsWith('*** Update File:')) { flushHunk(); header = line; }
        else if (line === '@@') { flushHunk(); hunk = [line]; }
        else hunk.push(line);
    }
    flushHunk();
    if (batch.length > 1) { batch.push('*** End Patch'); savePatch(`prototypes_${number}.patch`, batch); }
    const assetManifests = [];
    for (let offset = 0; offset < assets.length; offset += 50) {
        const name = `Manifests/assets_${String(offset / 50 + 1).padStart(3, '0')}.json`;
        assetManifests.push(name);
        const body = JSON.stringify(assets.slice(offset, offset + 50), null, 2);
        savePatch(`assets_${offset}.patch`, ['*** Begin Patch', `*** Add File: ${destination}${name}`,
            ...body.split('\n').map(line => '+' + line), '*** End Patch']);
    }
    const manifest = { repository: 'https://github.com/Goob-Station/Goob-Station', commit,
        policy: 'Explicit permissive or CC-BY/CC-BY-SA/CC0 asset license; original bytes and attribution preserved. No Goob code imported.',
        summary, assetManifests, excluded };
    savePatch('manifest.patch', ['*** Begin Patch', `*** Add File: ${manifestPath}`,
        ...JSON.stringify(manifest, null, 2).split('\n').map(line => '+' + line), '*** End Patch']);
    console.log(JSON.stringify({ summary, patchFiles,
        copiedButExcluded: excluded.filter(asset => fs.existsSync(path.join(repo, destination, asset.source.slice(root.length).replace(/^_/, '')))) }));
} else throw new Error(`Unknown mode: ${mode}`);
