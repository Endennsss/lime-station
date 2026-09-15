# Lime Station Repository Agent Instructions

This repository is the **Lime Station** fork of Space Station 14. Treat the checkout folder and the currently configured Git remote as transport details; they do not change the product identity.

The canonical agent guidance lives here:

- Rules: `.agents/rules`
- Skills: `.agents/skills`
- Compatibility bridges: `.agent`, `.claude`, `.cursor`, `.github`

At the start of a new dialogue, after context compaction, or when the task changes subsystem or file type, read the always-on rules in `.agents/rules` and select the relevant skills from `.agents/skills`. Bridge files only point to canonical files under `.agents`; the canonical files win if guidance differs.

Always apply these Lime Station conventions:

- Product name: `Lime Station`
- Code and prototype prefix: `Lime`
- Fork-owned project folder: `_Lime`
- Single-line marker: `Lime-Edit`
- Block markers: `Lime edit start/end` and `Lime added start/end`
- Explanatory inline comments and marker reasons: Russian
- Identifiers, API names, localization keys, C# symbols, and canonical marker tokens: English/original spelling

Before planning or editing, use these entrypoints:

- `.agents/rules/ss14-skill-preflight-and-refresh.md`
- `.agents/rules/ss14-codebase-prefix-detection.md`
- `.agents/rules/ss14-testing-guidelines.md`
- `.agents/rules/ss14-interaction-flow.md`
- `.agents/rules/AUTHORING_POLICY.md`
- `.agents/skills/AUTHORING_POLICY.md`

Prefer repository-aware IDE tools when they are available and suitable. Otherwise use focused repository searches and the narrowest relevant build or test command. Never assume a particular IDE integration exists.

For code changes, run the narrowest verification that proves the touched area is healthy. Never run more than two test commands concurrently, and stop any client/server process started for runtime verification before finishing.

When rules or skills are added, renamed, changed, or removed, update all compatibility bridges in the same change and run:

```powershell
pwsh ./.agents/rules/check-rule-bridges.ps1
pwsh ./.agents/skills/check-skill-bridges.ps1
```
