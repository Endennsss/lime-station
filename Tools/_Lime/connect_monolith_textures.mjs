import fs from 'node:fs';
import path from 'node:path';

// Расширенное подключение уже проверенной графики: игровые механики не переносим.
const root = 'Resources/Textures/';
const base = root + '_Lime/Imported/Monolith/';
const source = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3]);
if (output.startsWith(process.cwd() + path.sep)) throw Error('Патчи должны находиться вне репозитория');
const read = filename => fs.readFileSync(filename, 'utf8');
const json = filename => JSON.parse(read(filename).replace(/^\uFEFF/, ''));
const manifest = json(base + 'import_manifest.json');
const assets = manifest.assetManifests.flatMap(name => json(base + name))
    .filter(asset => fs.existsSync(asset.destination + '/meta.json'));
const goobBase = root + '_Lime/Imported/Goob/';
const goob = json(goobBase + 'import_manifest.json').assetManifests.flatMap(name => json(goobBase + name));
const byDestination = new Map([...goob, ...assets].map(asset => [asset.destination.slice(root.length), asset]));
const metaCache = new Map();
const meta = relative => {
    if (!metaCache.has(relative)) metaCache.set(relative, json(root + relative + '/meta.json'));
    return metaCache.get(relative);
};
const compatible = (old, next) => old.size.x === next.size.x && old.size.y === next.size.y
    && old.states.every(state => next.states.some(nextState => state.name === nextState.name
        && (state.directions ?? 1) === (nextState.directions ?? 1)));
function* walk(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        const filename = path.join(directory, entry.name);
        if (entry.isDirectory()) yield* walk(filename);
        else if (/\.ya?ml$/.test(filename)) yield filename;
    }
}
const prototypes = new Map();
const componentReferences = new Map();
for (const filename of walk(path.join(source, 'Resources/Prototypes'))) {
    for (const block of read(filename).split(/\r?\n(?=- type:)/)) {
        const id = block.match(/^  id: (\S+)/m)?.[1];
        if (!id || !/^- type: entity\r?$/m.test(block)) continue;
        const references = [...block.matchAll(/(?:[_A-Za-z0-9.-]+\/)+[_A-Za-z0-9.-]+\.rsi/g)].map(match => match[0]);
        prototypes.set(id, references);
        const fields = new Map();
        for (const component of block.split(/\n(?=  - type:)/).slice(1)) {
            const type = component.match(/^  - type: (\S+)/)?.[1];
            for (const field of component.matchAll(/^    ([A-Za-z][A-Za-z0-9]*): ((?:[_A-Za-z0-9.-]+\/)+[_A-Za-z0-9.-]+\.rsi)/gm))
                fields.set(type + ':' + field[1], field[2]);
        }
        componentReferences.set(id, fields);
    }
}
const canonical = relative => relative.replace(/^_[^/]+\//, '').replaceAll('_', '-');
const identity = relative => {
    const asset = byDestination.get(relative);
    return asset ? [asset.source.slice(root.length), ...(asset.replaces ?? [])] : [relative];
};
const priority = asset => asset.source.includes('/_Mono/') ? 0 : asset.source.includes('/_NF/') ? 1
    : !asset.source.slice(root.length).startsWith('_') ? 2 : 3;
assets.sort((a, b) => priority(a) - priority(b) || a.source.localeCompare(b.source));
const candidates = new Map();
for (const asset of assets) {
    const key = canonical(asset.source.slice(root.length));
    if (!candidates.has(key)) candidates.set(key, []);
    candidates.get(key).push(asset);
}
const bindings = new Map(), used = new Set(), changes = [];
const nativeEffects = 'Structures/Doors/Airlocks/Effects/airlock-effects.rsi';
const importedEffects = '_Lime/Imported/Monolith/' + nativeEffects;
const effects = fs.existsSync(root + importedEffects + '/meta.json') && compatible(meta(nativeEffects), meta(importedEffects))
    ? importedEffects : nativeEffects;
const airlockAliases = {
    basic: 'airlock-base', maint: 'airlock-maints', engineering: 'engineering-base',
    atmospherics: 'engineering-atmos', cargo: 'cargo-base', salvage: 'cargo-salvage', mining: 'cargo-mining',
    medical: 'medical-base', virology: 'medical-viro', chemistry: 'medical-chem', science: 'science-base',
    command: 'command-base', security: 'security-base', hydroponics: 'service-botany', freezer: 'service-freezer',
    centcomm: 'centcomm-base', syndicate: 'syndicate-base', external: 'airlock-external', hatch: 'airlock-hatch',
    hatch_maint: 'airlock-hatch-maints', hatch_syndicate: 'airlock-hatch-syndicate',
    shuttle: 'shuttle-base', shuttle_syndicate: 'shuttle-syndicate',
};
const airlockTarget = relative => {
    for (const original of identity(relative)) {
        const match = original.match(/^Structures\/Doors\/Airlocks\/(Standard|Glass)\/([^/]+)\.rsi$/);
        if (!match) continue;
        const name = airlockAliases[match[2]] ?? match[2];
        const asset = assets.find(asset => asset.source === root + `Structures/Doors/Airlocks/${match[1]}/${name}.rsi`);
        if (!asset) continue;
        const next = meta(asset.destination.slice(root.length));
        if (['closed', 'open', 'opening', 'closing'].every(name => next.states.some(state => state.name === name && state.directions === 4)))
            return asset.destination.slice(root.length);
    }
};
for (const filename of walk('Resources/Prototypes')) {
    const before = read(filename).replaceAll('\r\n', '\n');
    const after = before.split(/\n(?=- type:)/).map(block => {
        const id = block.match(/^  id: (\S+)/m)?.[1];
        const sourceRefs = prototypes.get(id) ?? [];
        block = block.replace(/(?:\/?Textures\/)?(?:[_A-Za-z0-9.-]+\/)+[_A-Za-z0-9.-]+\.rsi/g, (match, offset) => {
            const lineStart = block.lastIndexOf('\n', offset) + 1;
            if (block.slice(lineStart, offset).trimStart().startsWith('#')) return match;
            const prefix = match.match(/^\/?Textures\//)?.[0] ?? '';
            const relative = match.slice(prefix.length);
            if (relative.startsWith('_Lime/Imported/Monolith/')) return match;
            if (!fs.existsSync(root + relative + '/meta.json')) return match;
            const origins = identity(relative).map(canonical);
            const componentType = [...block.slice(0, offset).matchAll(/^  - type: (\S+)/gm)].at(-1)?.[1];
            const fieldName = block.slice(lineStart, offset).match(/^    ([A-Za-z][A-Za-z0-9]*): $/)?.[1];
            const sourcePath = componentReferences.get(id)?.get(componentType + ':' + fieldName);
            const possible = [...new Set([...assets.filter(asset => asset.source === root + sourcePath), ...origins.flatMap(key => candidates.get(key) ?? []),
                ...assets.filter(asset => sourceRefs.includes(asset.source.slice(root.length))
                    && path.basename(canonical(asset.source)).replace('.rsi', '') === path.basename(canonical(origins[0])).replace('.rsi', ''))])];
            const next = possible.find(asset => compatible(meta(relative), meta(asset.destination.slice(root.length))));
            if (!next) return match;
            const target = next.destination.slice(root.length);
            bindings.set(relative, target); used.add(target);
            return prefix + target;
        });
        if (filename.includes(`${path.sep}Doors${path.sep}Airlocks${path.sep}`)) {
            let adapted = false;
            block = block.replace(/^  - type: Sprite\n([\s\S]*?)(?=^  - type: |(?![\s\S]))/m, (original, body) => {
                const current = body.match(/^    sprite: (\S+)/m)?.[1];
                if (!current?.startsWith('_Lime/Imported/Goob/')) return original;
                const target = airlockTarget(current);
                if (!target) return original;
                // Основа и анимации Monolith — четырёхнаправленные; новые эффекты штатные.
                let state;
                const lines = body.split('\n').map(line => {
                    const found = line.match(/^    - state: (\S+)/);
                    if (found) state = found[1];
                    if (/^    sprite:/.test(line)) return `    sprite: ${target} # Lime-Edit - основа шлюза Monolith`;
                    if (/^    snapCardinals:/.test(line)) return '    snapCardinals: false # Lime-Edit - четырёхнаправленные шлюзы';
                    if (/^      sprite: _Lime\/Imported\/Goob\//.test(line)) {
                        const sprite = state === 'closed' ? target : effects;
                        return `      sprite: ${sprite} # Lime-Edit - совместимые слои шлюза`;
                    }
                    return line;
                });
                adapted = true; used.add(target); bindings.set(current, target);
                return '  - type: Sprite\n' + lines.join('\n');
            });
            if (adapted) block = block.replace(/^    openUnlitVisible: false[^\n]*/m,
                '    openUnlitVisible: true # Lime-Edit - штатные эффекты содержат open_unlit');
        }
        return block.split('\n').map(line => {
            if (!line.includes('_Lime/Imported/Monolith/') || line.trimStart().startsWith('#')) return line;
            return line.includes('Lime-Edit') ? line.replace('текстуры Goob', 'текстуры Monolith')
                : line + ' # Lime-Edit - графика Monolith';
        }).join('\n');
    }).join('\n');
    if (before !== after) changes.push({ filename: filename.replaceAll('\\', '/'), before, after });
}
fs.mkdirSync(output, { recursive: true });
const patchFiles = [];
for (const [index, change] of changes.entries()) {
    const oldLines = change.before.split('\n'), newLines = change.after.split('\n');
    if (oldLines.length !== newLines.length) throw Error('Неожиданное изменение структуры ' + change.filename);
    const ranges = [];
    for (let i = 0; i < oldLines.length; i++) {
        if (oldLines[i] === newLines[i]) continue;
        const start = Math.max(0, i - 3), end = Math.min(oldLines.length, i + 4);
        if (ranges.length && start <= ranges.at(-1)[1]) ranges.at(-1)[1] = end;
        else ranges.push([start, end]);
    }
    const lines = ['*** Begin Patch', '*** Update File: ' + change.filename];
    for (const [start, end] of ranges) {
        lines.push('@@');
        for (let i = start; i < end; i++) lines.push(...(oldLines[i] === newLines[i] ? [' ' + oldLines[i]] : ['-' + oldLines[i], '+' + newLines[i]]));
    }
    lines.push('*** End Patch');
    const filename = path.join(output, `connect_${index}.patch`);
    fs.writeFileSync(filename, lines.join('\n') + '\n'); patchFiles.push(filename);
}
console.log(JSON.stringify({ changedFiles: changes.length, newlyReferenced: used.size, bindings: Object.fromEntries(bindings), patchFiles }));
