# Unit System — How It Works

This document explains how unit data is structured, where files live, and what your teammates need to know to add new units or modify existing ones. No programming knowledge required for most tasks.

---

## The Big Picture

There are three layers to every unit in this game:

```
UnitDefinition.asset    →   the stats and identity (shared by server and client)
UnitPrefabRegistry      →   which visual prefab to spawn (client only)
UnitData                →   the live instance during a match (runtime only, not saved)
```

Think of it like a card game:
- `UnitDefinition` = the card template (what the card says)
- `UnitPrefabRegistry` = the card artwork
- `UnitData` = a specific copy of that card currently in play

---

## Folder Structure

```
Assets/
    Data/
        UnitLibrary.asset           ← the master list of all unit definitions
        Units/
            Unit_001_Soldier.asset
            Unit_002_Archer.asset
            Unit_003_Knight.asset
            ...
        UnitPrefabRegistry.asset    ← maps each unit type to its prefab (client only)

    Scripts/
        Shared/
            UnitDefinition.cs       ← the blueprint for unit definition assets
            UnitLibrary.cs          ← loads and indexes all definitions
        Client/
            UnitPrefabRegistry.cs   ← maps uId to visual prefab
        Server/
            MatchSession.cs         ← spawns and manages live units during match
```

---

## UnitDefinition — The Unit Template

Each unit type has exactly one `.asset` file in `Assets/Data/Units/`.

**Fields:**
| Field | Description |
|---|---|
| `uId` | Unique permanent ID for this unit type. Never change this once set. |
| `unitName` | Display name shown in UI |
| `moveRange` | How many cells this unit can move per action |

> **Future fields:** hp, armor, ap, skills[ ]

**Rules:**
- Every `uId` must be unique across all unit definitions
- `uId` starts at 1, never use 0
- Once a `uId` is assigned and in production, never reuse it even if you delete the unit

---

## UnitLibrary — The Master List

`UnitLibrary.asset` is the single file that holds references to every unit definition.

**How to add a new unit:**
1. Right click `Assets/Data/Units/` → Create → SRPG → Unit Definition
2. Name it `Unit_00X_YourUnitName`
3. Fill in the fields in the Inspector
4. Open `UnitLibrary.asset` and drag your new asset into the `Definitions` array
5. Done — both server and client will now recognize this unit type

**Where it's used:**
- Dragged into `MatchSession` component on the `Server` GameObject in the scene
- Dragged into `ClientController` component on the `Client` GameObject in the scene

> The library validates itself on startup. Check the Console for warnings about duplicate or missing uIds.

---

## UnitPrefabRegistry — Visuals Lookup

`UnitPrefabRegistry.asset` maps each `uId` to a Unity prefab. This is client-side only — the server never touches prefabs.

**How to add an entry:**
1. Open `UnitPrefabRegistry.asset` in Inspector
2. Add a new entry to the `Entries` array
3. Set `uId` to match the unit's definition
4. Drag the unit's prefab into the `Prefab` slot

**Rules:**
- Every `uId` in `UnitLibrary` should have a matching entry here
- Never leave the prefab slot empty — it will cause a spawn error at runtime

---

## UnitData — The Live Instance

`UnitData` is not an asset — it exists only in memory during a match. The server creates one for each unit when the match starts, copying stats from `UnitDefinition`.

```
Match starts
    → Server reads UnitLibrary.Get(uId)
    → Creates UnitData with copied stats
    → UnitData tracks current position, hp, etc. for the rest of the match
Match ends
    → UnitData is destroyed
    → Results are sent to backend
```

You never create or edit `UnitData` manually. It is managed entirely by `MatchSession`.

---

## Adding a New Unit — Full Checklist

- [ ] Create `Unit_00X_Name.asset` in `Assets/Data/Units/`
- [ ] Assign a unique `uId` (check existing assets to avoid duplicates)
- [ ] Fill in `unitName` and `moveRange`
- [ ] Add it to `UnitLibrary.asset` Definitions array
- [ ] Create or assign a prefab for the unit visuals
- [ ] Add entry to `UnitPrefabRegistry.asset` with matching `uId` and prefab
- [ ] Run the game and check Console for any warnings

---

## Common Mistakes

| Mistake | What happens |
|---|---|
| Duplicate `uId` | Library logs a warning, second definition is ignored |
| Missing from `UnitLibrary` | Server can't spawn it, logs an error |
| Missing from `UnitPrefabRegistry` | Client can't display it, spawn fails silently |
| Prefab slot is null | NullReferenceException on client spawn |
| Changing a `uId` mid-production | Breaks all existing match data referencing old id |

---

## Key Rule

> **The DB only stores unit IDs — never stats.**
> Stats, skills, and move ranges live entirely in the Unity assets.
> Balance changes = edit the `.asset` file and ship a build. No database migration needed.