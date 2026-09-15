---
name: ss14-upstream-maintenance
description: Guide to working with Space Station 14 forks using the Lime Station `_Lime` project-folder pattern to minimize merge conflicts with the upstream. Use when modifying vanilla code or prototypes.
---

# 🛡️ Working with Upstream code and minimizing conflicts

This skill describes the Lime Station standards for working with Space Station 14 code inherited from upstream while isolating fork-owned work under `_Lime`.
**Main goal:** Maintain the ability to easily receive updates from the upstream (merge), minimizing manual edits in case of conflicts.

Before making changes, first determine the active codebase prefix, project folder and edit markers using the `ss14-codebase-prefix-detection` rule.

After the active fork is determined, keep all new fork-owned code in that fork's project folder. Do not switch folders because the touched behavior is vanilla, inherited from another fork, or looks "not fork-specific". For Lime Station, the correct folder is always `_Lime`.

## ⚠️ Golden rule

> [!IMPORTANT]
> **Minimizing changes to vanilla files is MORE IMPORTANT than "pretty" architecture.**
> It's better to leave the "dirty" hack in one line of the vanilla file than to rewrite half the system, creating hell when merging.

## � Folder structure and Project Folder

To clearly separate vanilla code from our modifications, the `_Lime` project folder is used.

**Why `_`?**
- The folder is always at the top of the file list and is easy to find.
- Visually separates “our” code from “their” (vanilla) code.

**What should I put in `_ProjectName`?**
1. **New files:** Completely new systems, components, prototypes.
2. **Partial classes:** Extensions of vanilla classes (see below).
3. **Assets:** New sprites, sounds, textures.

The selected project folder is a consequence of the active fork, not of the subsystem being touched. For this repository, always write new project code under `_Lime`, regardless of the upstream origin of the touched behavior.

> [!TIP]
> **Isolation principle:**
> Try to keep 99% of your unique code inside the `_ProjectName` folder.
> Only **minimal** edits (hooks, events) that connect vanilla code to yours should remain in vanilla folders.

## 🛠️ Modification of C# code

When modifying existing vanilla code (outside the project folder), use the following patterns.

### 1. Pattern `Edit Start` / `Edit End`

Used when you need to change existing logic inside a method or property.
Makes it easy to see your changes against the background of vanilla code.

**Format:**
```csharp
// SERVERNAME edit start - brief reason for changes
```
...your code...
```csharp
// SERVERNAME edit end
```
*Where `SERVERNAME` is the project name (`Lime`).*

**Example (change value):**
```csharp
component.Field2 = 321;
// Lime edit start - изменение баланса радиуса
component.Field = 123;
// Lime edit end
```

**Example (changing logic):**
```csharp
// Lime edit start - fixing double gateways
if (TryComp<AirlockComponent>(uid, out var airlock))
{
    // ...new logic...
}
// Lime edit end
```

### 2. Pattern `Added Start` / `Added End`

Used when you add a **new** block of code (eg calling an event, checking) that was not in the original.

**Format:**
```csharp
// SERVERNAME added start - brief reason for adding
```
...new code...
```csharp
// SERVERNAME added end
```

**Example:**
```csharp
// Lime added start - публикуем событие попадания
_eventBus.RaiseLocalEvent(uid, new ProjectileHitEvent(projectile, entity));
// Lime added end
```

### 3. Partial Classes

If you need to add a **new field, property or method** to an existing class or system, **DO NOT** write it in a vanilla file.
Instead, create a `partial` class in your project folder (`_Lime`).

**Pattern:**
1. Find the vanilla class (eg `SharedDoorSystem`).
2. Create a file in your folder: `Content.Shared/_Lime/Doors/Systems/SharedDoorSystem.Abilities.cs`.
3. Declare the class as `partial` with the same namespace.
4. **Important:** Suppress the namespace mismatch warning if necessary.

**Example:**
*Vanilla file (`Content.Shared/Doors/Systems/SharedDoorSystem.cs`):*
```csharp
namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem : EntitySystem
{
    // Vanilla code...
}
```

*Your file (`Content.Shared/_Lime/Doors/Systems/SharedDoorSystem.Store.cs`):*
```csharp
using Content.Shared.Doors.Systems; // We use vanilla namespace

// We suppress warning, since the file is physically located in another folder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem
{
    // Your new logic, accessible "as if" inside the original class
    public void MyNewMethod() { ... }
}
```

## 🧬 Modifying Prototypes (YAML)

Changing vanilla YAML files (`Resources/Prototypes/Entities/...`) is **BAD PRACTICE**. This is a guaranteed conflict with any change to this file in the upstream.

### 🌌 Ideal Prototype Change Pattern

Instead of editing the original, we create a **replacement heir**.

**Algorithm:**
1. Find the vanilla entity ID (for example, `AirlockHatchSyndicate`).
2. Create a **new** YAML file in your project folder (for example, `Resources/Prototypes/_Lime/.../access.yml`).
3. Create a new entity:
    - `id`: Add a suffix or prefix (for example, `AirlockHatchSyndicateLocked`).
    - `parent`: Specify a vanilla ID.
    - Make the necessary changes (add components, change fields).
4. **Migration Magic:** Register the replacement in `Resources/migration.yml`.

**Implementation example:**

*1. New prototype (`_Lime/Entities/Structures/Doors/Airlocks/access.yml`):*
```yaml
- type: entity
  parent: AirlockHatchSyndicate  # Inherit from the original
  id: AirlockHatchSyndicateLocked # New ID
  suffix: Syndicate, Locked
  categories: [ HideSpawnMenu ] # Lime added - скрываем технический прототип из меню спавна
  components:
  - type: AccessReader
    access: [["SyndicateAgent"]] # Add the required changes
```

*2. Migration file (`Resources/migration.yml`):*
Add an entry to the end of the file or to the appropriate section.
```yaml
# ... existing migrations ...

# Lime-Edit
AirlockHatchSyndicate: AirlockHatchSyndicateLocked
```

**Result:**
When loading the map, the engine will automatically replace all `AirlockHatchSyndicate` with `AirlockHatchSyndicateLocked`.
When updating the upstream, if new components are added to `AirlockHatchSyndicate`, your `AirlockHatchSyndicateLocked` will automatically receive them through inheritance (`parent`). File conflicts - **0**.

> [!WARNING]
> **Migration DOES NOT update links in other prototypes!**
> The `migration.yml` file tells the engine to replace the entity ONLY when spawning on the map and in saves.
> If the old ID (`AirlockHatchSyndicate`) is used in:
> - Spawn Pools
> - Crafting recipes
> - Fields of other components (for example, `SpawnOnDeath`)
>
> ...the old essence will remain there! You need to find all uses of the old ID and replace them with the new one manually (via the `edit` pattern or overriding).

### 🛠️ Minor changes in vanilla files

If migration is not possible (but try to use it!), use `edit` comments directly in YAML, but try to do it on one line.

```yaml
- type: entity
  id: VanillaEntity
  components:
  - type: Item
    path: _FORKFOLDER/Objects/123.rsi # SERVERNAME edit - sprite replacement
```

### 🚫 Anti-patterns (What NOT to do)

❌ **Direct code removal.**
Instead of deleting, comment out the code and leave the mark `edit`.
```csharp
// BAD:
// public void DeletedMethod() { }

// GOOD:
// SERVERNAME edit start - deleted because interferes with mechanics X
// public void DeletedMethod() { ... }
// SERVERNAME edit end
```

❌ **Rewriting entire files.**
If you copy the entire file into your folder and disable the original, you lose all future updates to that file. DO this ONLY if the logic changes fundamentally and irreversibly.

❌ **Replacement of entity ID without inheritance.**
If you simply copy the YAML of an entity and change it, you will not receive updates to the parent components from upstream. Always use `parent`.
