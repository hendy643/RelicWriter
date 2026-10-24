# Game type data (RTTI)

The game's own list of classes and enums gives what the save files alone can't: which types are enums, which fields a class has, and which of those the game saves. This page records where that data comes from, what was checked against our saves, and what it can't tell us.

## Source

The [cp2077-nativedb](https://github.com/rayshader/cp2077-nativedb) project (MIT) publishes a dump of the game's runtime type information, made by RED4.RTTIDumper. We used its `cp2077_dump_json.zip` and `enums.json`.

- **Game version of the dump:** 2.31. Its `gameGameVersion` enum ends with `CP77_Patch_2_3_Hotfix1 = 2310` and `Current = 2310`. That matches our 269 save (game version 2310) and is older than our 277 save (2500).
- **Size:** 15,832 classes and 1,524 enums. Each class file lists its parent, flags and properties (type, name, 64-bit flags); the full dump includes native classes that are not exposed to scripts.
- The dump stores no default values and no save-format information; everything about the save format below is a comparison with our saves.

## What was checked

All objects in the system-blob nodes ([save-system-blobs.md](save-system-blobs.md)) of one save per version (193, 261, 269, 277), 3,855 objects with 8,212 fields in total, were compared with their class in the dump (including inherited properties).

| Check | Result |
|---|---|
| Field type name in the save equals the property's type in the dump | **8,212 of 8,212** matched in every version, no mismatch |
| Field name exists on the class in the dump | all but 6 |
| Classes missing from the dump | `gameCurveStatModifierData` and `gameConstantStatModifierData` (only in the 193 save, renamed `..._Deprecated` in later versions); `CyberwareCustomizationSystem` (277 save) |

The 6 unknown fields are `DataTrackingSystem.achievementsMask` in the 193 save (gone by 2.31) and the five new `StatsSystem` fields in the 277 save (`valuesOptimized`, `sharedBufferSlices`, `sharedInactiveStats`, `sharedRecordId`, `sharedDataBuffer`). Two classes seen only in the 277 save are not in the dump either: `gameSavedStatsDataOptimized` and `gameBufferSlice`. So these are the pieces that changed after 2.31, and the dump can't describe them. They need data from the 2.5 game itself.

In short, for the nodes tested here the field layout has been stable from the 193 save through 2.31 apart from renames and removals, and the only structural change found in 2.5 is in the stats system. Nodes outside the system-blob format were not tested this way.

## Resolving enums and structs

A two-byte field value is either an enum value (a string pool index) or an empty struct (`00 00`); the save doesn't say which. The dump does: a type is an enum if it is listed in the enum list.

Of the 27 type names that my first parser had treated as enums, **19 are enums and 8 are structs** (`gameStatPoolModifier`, `gameSEquipSlot`, `UIScriptableSystemAttributeLevel`, `gameGodModeData`, `STrait`, `SAttributeData`, `SNewPerk`, `RecipientData`). Every type name in the sample except the two 277-only classes is a primitive, an enum or a class in the dump. This closes the "enum problem" in save-system-blobs.md, provided the enum list is up to date for the save's game version.

## Which fields are saved

Each property has a 64-bit flags value. Comparing flags with what is actually in the saves gave a rule that held for every serialised field:

- A property is saved if bit 28 is set (scripted classes) or bit 14 is set (native save structures such as `gameStatPoolsSystemSave`).
- For the **123 (class, field) pairs** that appeared in our saves, all 123 satisfy the rule. Of the pairs that never appeared in our saves, 180 fail it and 47 pass it.
- The 47 that pass but never appeared are consistent with "fields holding their default value are left out", which is also how array elements look in the saves.

This is an empirical pattern from the data, not documentation of what the bits mean. It was derived from the 269 save's classes and not confirmed on other game versions' flags.

## What this still can't tell us

- The 2.5 changes (`StatsSystem` classes above, `CyberwareCustomizationSystem`, and anything else added after 2.31).
- Default values, so we can't yet say which absent fields are defaults and what their values are.
- Native node formats. The dump describes the classes inside system blobs, not nodes that are hand-written binary such as `inventory` or `PersistencySystem2`.
- Save version history: when each format change happened between releases.

## Getting a 2.5 dump

Two ways:

1. Run RED4.RTTIDumper in the game. This needs RED4ext installed into the game folder and the game started with the plugin. It changes the user's game installation, so it needs the maintainer's go-ahead. **Not done.**
2. Read the registrations out of `Cyberpunk2077.exe` with Ghidra. **Works, one class at a time**, see below.

## Ghidra readings from the 2.5 exe

`Cyberpunk2077.exe` was analysed in Ghidra 12.2 (headless, with the demangler, address-table, switch and a few other analyzers off: the first run with everything on ran out of memory at a 12 GB heap). No game files were changed.

How the type data is found: each class has a registration function that sets its name, and a second function that registers its properties, one call per field with the field name, the offset in the in-memory object, and a type getter. The class-name and field-name strings are found by searching the exe and following their cross-references to those functions. Offsets are in-memory (not saved) offsets, so they give field order and size but not the save layout.

Read this way for the two classes the 2.31 dump lacks and for `StatsSystem`'s root:

| Class | Fields (in-memory offset) |
|---|---|
| `gameStatsStateMapStructure` | `keys` 0x30, `values` 0x40, `valuesOptimized` 0x50, `sharedBufferSlices` 0x60, `sharedInactiveStats` 0x70, `sharedRecordId` 0x80, `sharedDataBuffer` 0x90 |
| `gameSavedStatsDataOptimized` | `statModifiers` 0x00 (array), `bufferSliceIndices` 0x10 (array), `inactiveStatsIndices` 0x20 (array), `entityHash` 0x30 (8 bytes), `seed` 0x38, `recordIDIndex` 0x3C (2 bytes), `idType` 0x3E (1 byte) |
| `gameBufferSlice` | `offset` 0x0 (4 bytes), `combined` 0x4 (4 bytes) |

- `statModifiers` is a field the save never contained in our sample, so it is probably not saved; it does not appear in the 277 save's objects.
- Field sizes come from the gaps between offsets, not from the type getters, which I did not follow.
- For the old combined modifier class the game's in-memory order is `refStatType` 0x48, `refObject` 0x4C, `operation` 0x50, `value` 0x54 (all 4 bytes). That is the reason the order of those two fields in the 2.5 buffer is marked open in [save-system-blobs.md](save-system-blobs.md).
- The stat-modifier record layouts in the shared buffer are not in these registrations: the buffer is written by separate code that I haven't read. Its layout in the doc is from the save bytes.

### The persistency loader

Following the cross-references of the string `PersistencySystem2` leads to the node's load function (about 1,900 bytes of code) and its save function. Reading the load function gave:

- Node names are looked up through small functions that return a 64-bit hash of the name: `PersistencySystem`, `PersistencySystem2` and `TypeDatabase_v2`. The hash is FNV-1a-64 of the name, the same as for stat names. Checked for `PersistencySystem2` and `TypeDatabase_v2`, and it is also how the class of each persistency entry is stored.
- The save version is tested against `0xCD` (205). Below it the old `PersistencySystem` node is read; from 205 the `TypeDatabase_v2` header and `PersistencySystem2` are read. In the old path a further check at `0x7C` (124) decides whether the id array is present.
- `TypeDatabase_v2` is read as a 24-byte header (version above 1, a 64-bit hash compared with the running game's, then three counts), then three flat arrays with strides of 20, 8 and 12 bytes. The saves match all of that exactly. Layouts are in [save-node-formats.md](save-node-formats.md).
- `PersistencySystem2` reads an id array, an optional skipped section, an entry count, and then per entry an id, a class-name hash and a length-prefixed data block. The helper functions behind these reads are plain `u32` / `u64` / `u32 + bytes` stream reads. All of it matches the saves exactly.
- The data block of each entry is written by a generic object serialiser (`FUN_140279248`) and read by `FUN_1403ea008`. Reading them gave the property stream format: per property a name hash, a type hash and a value, with nested lists closed by a zero, and only non-default properties written. The format is in [save-property-streams.md](save-property-streams.md); the saves match it exactly.
- The same loader converts old entries when the stored `TypeDatabase_v2` hash differs from the game's: it reads each entry with the stored layout and writes it back with the current one. The tables used for that are described in save-node-formats.md.

Still unread: the code that writes the stats shared buffer, and the other hand-written nodes (`PlayerSystem`, `questSystem`, `JournalManager`). The same method (string, then cross-reference, then decompile) gets to them, but the decompiled code has to be read by hand.

### Mapping every node to its loader

All 138 node names in [save-nodes.md](save-nodes.md) were found as plain strings in the exe. Each has a small getter function (about 71 bytes) that returns its name hash, and the functions that call the getter are that node's loader and saver (203 distinct functions across the 138 names, some shared by several nodes). The 65 of them that compare the save version are tabulated in [save-versions.md](save-versions.md#version-checks-in-the-games-own-node-loaders). The addresses there are for the 2.5 exe (product version 2.5, file version 3.0.5813403, 66,704,848 bytes); they will move in any other build.

What is finished and what isn't, as a list of the Ghidra questions this project had:

- The 2.5 `StatsSystem` classes and fields: done (above).
- The `StatsSystem` shared buffer: layouts decoded from the save and verified; the game's writer was not found (the runtime modifier classes have no registered properties, so there is nothing to follow from a name). The order of two fields in the combined record stays open.
- The persistency loader, `TypeDatabase_v2` and the property stream format: done.
- Save version checks: the thresholds are tabulated; their meaning is known for a handful only.
- Hand-written formats of the other nodes: `PersistencySystem2`, `TypeDatabase_v2`, `CommunitySystem` (the largest node, about a quarter of the 277 save), `PlayerSystem` and `JournalManager` are done, each tested against all the saves that contain it. Layouts for 116 further nodes, `WardrobeSystem` included, are in [save-node-specs.md](save-node-specs.md), generated from specs that were each run against all the saves. `instances` has its own document, [save-node-instances.md](save-node-instances.md), and `PSData` is decoded as a system blob.
