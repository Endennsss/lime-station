---
trigger: always_on
---

# Rule: Lime Station codebase identity, project folders, and edit markers

This rule is mandatory for every task in this repository.

## Fixed repository identity

Do not infer the fork name from the checkout directory or from the current remote. This repository is the **Lime Station** fork of Space Station 14, even when it is temporarily checked out from or synchronized directly with the upstream `space-wizards/space-station-14` remote.

Use these values consistently:

| Setting | Value |
| --- | --- |
| Product name | `Lime Station` |
| Code/prototype prefix | `Lime` |
| Fork-owned folder | `_Lime` |
| Single-line edit marker | `Lime-Edit` |
| Changed block markers | `Lime edit start` / `Lime edit end` |
| Added block markers | `Lime added start` / `Lime added end` |

## Placement rules

1. Put new fork-owned C# code under the corresponding `Content.Client/_Lime`, `Content.Shared/_Lime`, or `Content.Server/_Lime` tree.
2. Put fork-owned prototypes, localization, textures, audio, and other resources under the nearest matching `Resources/**/_Lime` tree.
3. Keep changes to upstream-owned files minimal. Prefer a small hook into a partial Lime-owned implementation.
4. Never introduce `_Sunrise`, `_Scp`, `_Fish`, `_Lust`, or another fork's marker for new Lime Station work.
5. Existing upstream names and historical third-party identifiers may remain when they identify external code or data; do not rename them merely for branding.

## Marker syntax

Use the marker text exactly, adapting only the comment syntax:

- C#, C++, Java: `// Lime-Edit` or `// Lime added start - reason`.
- YAML, FTL, Python, Shell: `# Lime-Edit` or `# Lime added start - reason`.
- XML, HTML: `<!-- Lime-Edit -->` when a marker is genuinely required and comments are supported.

For a multi-line replacement, bracket only the changed region. For a new block, use the `added` pair. Include a short reason when it helps distinguish the fork behavior from upstream.

## Language

Write marker reason phrases and explanatory inline comments in Russian. Keep canonical marker tokens, API names, identifiers, localization keys, and C# symbols in their original English form.
