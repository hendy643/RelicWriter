# System blobs

Many nodes store their data as a "system blob": a small serialised object graph that names its own classes and fields. This is the format behind `StatsSystem`, `StatPoolsSystem` and `ScriptableSystemsContainer`, which together hold stats, player development (attributes, skills, perks), the crafting book, vendors and a lot of other player state. Container and node table are in [save-format.md](save-format.md); simple nodes are in [save-node-formats.md](save-node-formats.md).

Verification: a throwaway parser written from CSE's source ([references.md](references.md)) was run on one save of each version (193, 261, 269, 277). In every node of the types below where the container applies, **all objects and all fields were consumed exactly**: about 3,900 objects and 13,700 fields, with nothing left over and no field the parser couldn't place. A second check: decoded values match the save's `metadata.9.json` (attributes of 20, level 60, life path Nomad in the 277 save).

Because each object lists its own field names and type names, the *structure* can be read without knowing the game's class definitions. What can't be read without them is which two-byte values are enums and which are empty structs (see below).

## Which nodes use it

Detected by checking that the node's data has the header below with a size that fits exactly:

| Node | Seen in | Notes |
|---|---|---|
| `StatsSystem` | 193, 261, 269, 277 | See [StatsSystem](#statssystem). |
| `StatPoolsSystem` | all four | One object, `gameStatPoolsSystemSave`. |
| `ScriptableSystemsContainer` | all four | 16 to 20 root objects, one per sub-system, plus handle objects. |
| `godModeSystem` | all four | |
| `tierSystem` | all four | |
| `MovingPlatformSystem` | all four | |
| `RenderGameplayEffectsManagerSystem` | all four | |
| `GuardAreaManager` | 261, 269, 277 | |
| `PreventionSpawnSystem` | 261, 269, 277 | |
| `PSData` | 193 only | The old persistent-state store. See [PSData](#psdata-193-only). |
| `scanningController` | 193 only | In 261 to 277 it doesn't have this layout. |
| `questSceneDataHelper` | **does not fit** | Different layout in the versions that have it (header fields are inconsistent). Not decoded. |

The list is what matched in our four saves. Other nodes may use the format and just didn't match, or the node may be absent from the sample.

## Blob layout

All integers little-endian. Offsets inside the blob are relative to `base`, the position right after the header (and after the root name list, if present).

| Field | Type | Notes |
|---|---|---|
| node id | u32 | As for every node. |
| blob size | u32 | Equals the node's own data length minus 8. |
| unknown | u16 | `4` in the stats blobs checked. |
| unknown | u16 | `6` in the stats blobs checked. |
| root count | u32 | Header field. `1` means no root name list. |
| string descriptors offset | u32 | `0` in the stats blobs checked. |
| string data offset | u32 | Also the size of the string descriptor table. |
| object descriptors offset | u32 | |
| object data offset | u32 | |
| root names | u32 count, then u64 × count | Only present when the header root count is above 1. The count repeats the header's. |
| string pool | descriptors, then data | See below. |
| object descriptors | (u32, u32) × N | See below. |
| object data | | One blob per object. |

Ordering rule that held everywhere: string data offset ≤ object descriptors offset ≤ object data offset. **verified**

In `ScriptableSystemsContainer` the root names are 64-bit hashes (one per sub-system, 16 to 20 of them) and, per CSE, the first that-many objects are the root objects. The remaining objects are "handle objects" that roots point at. The hash function is **open** (not checked).

### String pool

- Starts at `base`. The first `string data offset` bytes are descriptors, 4 bytes each, so the number of strings is that offset divided by 4.
- Each descriptor is a `u32`: the low 24 bits are the string's offset from `base`, the high 8 bits are its length including a terminating zero byte. The length is only 8 bits, so a string of 256 characters or more stores its length modulo 256 (found in the `PSData` blob of a game 1.05 save, with node-path strings up to about 270 characters). The zero byte at the end is what gives the real length, and the stored byte equals (length including the zero) mod 256. Checked on that one save; the other saves have no string that long.
- The strings follow the descriptors and are zero-terminated. **verified** (every string ended in a zero byte, every index in range)

The pool holds class names, field names, type names and also **enum values and name (CName) values**, so each distinct string appears once.

### Object descriptors

Each is `(name index u32, data offset u32)`. The name is a class name from the string pool. Data offsets are relative to `base`, increase from one object to the next, and the first one equals the object data offset. An object's data ends where the next one's begins; the last one ends at the end of the blob. **verified**

## Object layout

| Field | Type | Notes |
|---|---|---|
| field count | u16 | May be 0. |
| field descriptors | (name index u16, type index u16, data offset u32) × count | Indexes are into the string pool. Offsets are relative to the start of the object. |
| field data | | In descriptor order, no padding. |

The first field's offset equals `2 + 8 × count`, offsets increase, and each field runs up to the next field's offset (the last to the end of the object). **verified** for every object.

An empty object, or a struct with no fields, is just `00 00`. Fields that hold the type's default value appear to be left out: array elements often list fewer fields than their class has (for example a skill entry with only `currentLevel`). This is inferred from what was seen and from a comment in CSE; no class definitions were checked to confirm it.

## Field values by type name

The type name in the descriptor decides how the bytes are read.

| Type name | Encoding |
|---|---|
| `Bool`, `Uint8`, `Int8` | 1 byte |
| `Uint16`, `Int16` | 2 bytes |
| `Uint32`, `Int32`, `Float` | 4 bytes |
| `Uint64`, `Int64` | 8 bytes |
| `TweakDBID` | 8 bytes, as in [save-node-formats.md](save-node-formats.md) |
| `CRUID` | 8 bytes |
| `CName` | `u16` index into the string pool |
| `NodeRef` | `u16` length, then that many characters (a path such as `~/../marker_1`) |
| `DataBuffer` | `u32` length, then that many bytes |
| `array:T` | `u32` count, then count elements of type `T` |
| `[N]T` or `static:N,T` (fixed size) | `u32` count (equal to N), then N elements of type `T`. Both spellings occur; `PSData` uses `static:`. |
| `handle:T` | `u32` index into the blob's list of objects |
| an enum type, such as `gamedataStatType` | `u16` index into the string pool; the string is the value name, for example `Cool` or `Nomad` |
| anything else (a struct) | a nested object, in the object layout above |

The sizes of the 1, 2, 4 and 8-byte types come from every occurrence in the sample having exactly that size. Handle indexes were checked to be inside the object list, but not that they point at the class they claim to.

**The enum problem:** an enum value takes two bytes, and so does an empty struct (`00 00`). The bytes alone don't say which, and the type name doesn't either. The game's type data does: a type is an enum if it is in the enum list. Of 27 names my first parser took for enums, 19 were enums and 8 were structs (see [save-rtti.md](save-rtti.md)). A reader needs that enum list, and it must match the save's game version. **resolved for 2.31 and earlier; types added in 2.5 are not covered**

## Examples

- Player development (decoded from the 277 save): attributes `Cool`, `Intelligence`, `Reflexes`, `Strength`, `TechnicalAbility` each 20, skills with a current level (`CoolSkill` 60 and so on), and a `lifePath` of `Nomad`. All of it matches the metadata file.
- Entity ids look like `{ "hash": 1 }`: a struct with one `Uint64` field. The owner of the player development data is `1`.

## StatsSystem

One root object, `gameStatsStateMapStructure`, plus child objects. It changes shape between versions:

| Save versions | Root fields | Modifier classes |
|---|---|---|
| 193, 261, 269 | `keys: array:gameStatsObjectID`, `values: array:gameSavedStatsData` | `gameConstantStatModifierData`, `gameCurveStatModifierData`. From 261 the names end in `_Deprecated`. |
| 277 | `valuesOptimized: array:gameSavedStatsDataOptimized`, `sharedBufferSlices: array:gameBufferSlice`, `sharedInactiveStats: array:gamedataStatType`, `sharedRecordId: array:TweakDBID`, `sharedDataBuffer: DataBuffer` | none as separate objects |

Sizes: 167 KB (193), 380 KB (261), 561 KB (269) and 45 KB (277). The 2.5 layout is a compact rewrite: modifiers are packed into one shared buffer instead of being one object each.

- **Old form:** each entry in `values` has a `recordID` (TweakDBID) and a `seed`, with modifier objects reached by handle. A constant modifier has `statType`, `modifierType` and a `value` float, for example `DamageReductionExplosion`, `Additive`, `0.35`.
- **2.5 form:** `valuesOptimized` entries hold `bufferSliceIndices`, `inactiveStatsIndices`, `entityHash`, `seed` and `idType` (such as `ItemID`). Each entry points at one or more slices of `sharedDataBuffer`.

### The 2.5 shared buffer

Decoded from the one 277 save we have. The 32,768-byte `sharedDataBuffer` holds 8,612 bytes of data and is zero-padded after that. **verified** on that save only: the records below consume bytes 0 to 8,612 exactly (190 records), the rest is all zeros, and all 30 slice offsets fall on record boundaries.

The game's own class definitions for the 2.5 types are in [save-rtti.md](save-rtti.md#ghidra-readings-from-the-25-exe). In short: `gameBufferSlice` is two `u32`s (`offset`, `combined`); `gameSavedStatsDataOptimized` has `statModifiers` (an array, not seen in our save), `bufferSliceIndices` and `inactiveStatsIndices` (arrays), `entityHash` (8 bytes), `seed` (4), `recordIDIndex` (2) and `idType` (1).

**Slices.** `sharedBufferSlices` entries are `{offset, combined}`. `combined & 0x3FFFFFFF` is the slice's size in bytes; bit 30 is set on some slices (meaning unknown). Slices are contiguous: each offset is the previous offset plus its size, and the last ends at 8,612. A slice is a run of whole records.

**Records.** Each record starts with a string (varint length, then characters, as for node names) naming the class, then fixed fields, little-endian:

| Class | Fields after the name |
|---|---|
| `gameConstantStatModifier` | `u64` stat, `u32` modifierType, `f32` value |
| `gameCombinedStatModifier` | `u64` stat, `u32` modifierType, `u64` refStat, `u32` operation, `u32` refObject, `f32` value |
| `gameCurveStatModifier` | `u64` stat, `u32` modifierType, string curveName, string columnName, `u64` curveStat, `u32` (0 in all 14 records) |

- **Stat** values are the FNV-1a 64-bit hash of the `gamedataStatType` value name (for example `LootLevel`). All 190 records' stat hashes resolved to a name in the 2.31 enum list. **verified**
- **modifierType** is the `gameStatModifierType` enum index: `Additive` 0, `AdditiveMultiplier` 1, `Multiplier` 2.
- Counts in the sample: 170 constant, 14 curve, 6 combined.
- The combined layout is taken from the old deprecated data class's field order and gives plausible values (`LootLevel`, `AdditiveMultiplier`, ref stat `NPCWeaponDropRandomizer`, 1.0), and the whole buffer parses with it. **The order of the two `u32`s is not settled:** I read them as `operation` = 0 (`Addition`), `refObject` = 2 (`Root`). But the game's in-memory class puts `refObject` (offset 0x4C) before `operation` (0x50), which would read as `refObject` = `Self` and `operation` = `Multiplication`. All 6 records in the sample have the same pair, so the data can't tell them apart. **open**
- Curve records name a game curve table (`quality_curves`, `random_distributions`) and a column in it.
- Whether other record classes exist, and the meaning of slice bit 30, need more 2.5 saves or the game code.

## StatPoolsSystem

One object, `gameStatPoolsSystemSave`, with `mapping: array:gameStatsObjectID` and `statPools: array:gameStatPoolData`. A pool has `ownerID`, `recordID`, `type` (an enum such as `Health`), `modifiers` (a fixed array of two `gameStatPoolModifier`), `stat`, `maxValue`, `changeMode` and `currentValue`. The same structure decodes in all four versions. **verified** structurally; whether editing `currentValue` is safe in the game is not tested.

## Open questions

- Handle semantics: the root versus handle object split, and what the `handle` target classes are.
- The root name hash function.
- Default values per class. The type data gives the field list and which fields are saved (see save-rtti.md) but not the defaults, which are needed to write blobs back.
- The 2.5 `sharedDataBuffer`: only one save was available; other record classes, slice bit 30, and the meaning of combined `operation` and `refObject`.
- `questSceneDataHelper` and `scanningController` (from 261 on), which don't match this layout.
- Whether other nodes in saves we haven't seen use it.

## Trailing root names

A blob can have more data after the main blob: a `u32` count and that many `u64` hashes. `PSData` has it (8,615 hashes in the 193 save). The blob size field does not cover it; the node's data ends exactly after it. **verified** in the 193 `PSData`; not checked in the other blob nodes.

## PSData (193 only)

The save-version-193 save keeps persistent object state in one `PSData` node of 5,786,758 bytes with no children. The newer saves use `PersistencySystem2` ([save-property-streams.md](save-property-streams.md)) instead. **Verified** on the one 193 save that has the node: every part below parses and consumes every byte.

| Part | Value |
|---|---|
| root names | 127,384 u64 hashes, all different, none 0 |
| string pool | 2,764 strings |
| objects | 167,295, in 119 root classes plus 41 more classes |
| trailing root names | 8,615 |

- **Roots.** The first 127,384 objects belong to the 127,384 root names, one each, in order. Their classes are `...PS` persistent-state classes such as `gameScanningComponentPS` (22,886), `gameVisionModeComponentPS` (21,623), `gameStatsComponentPS` (15,403), `gameInventoryPS` (9,107) and `BaseDestructibleControllerPS` (5,381). The other 39,911 objects are reached only through handles, for example `DeviceOperationsContainer` (12,713) and `PlayEffectDeviceOperation` (7,320). What a root name is (an id of the persistent object) was **not** worked out: the hash did not show up in the object data in a sample check.
- **Handles.** All handle indexes are inside the object list (the largest is the last object).
- **Fields.** Every object's field offsets are consistent and every field value parses with the rules above. The field type names are the game's (`Bool`, `EDeviceStatus`, `array:handle:DeviceOperationBase`, `static:4,vehicleWheelRuntimePSData`, `Vector3`, `gameCommunityID` and so on). 54 struct types and 40 enum types occur.
- **Enums.** A type is read as an enum if it is in the 2.31 enum list. Every enum value found (a string-pool entry) is a valid value of its enum in that list. A struct whose only content is empty would look the same as an enum, so this check shows the lists agree, not that no empty struct was taken for an enum. **resolved for 2.31 and earlier**
- **Arrays of structs.** An element's size is not stored. A reader finds an element's end by parsing it: the end of its last field.

Not done: what the root hashes identify, and writing the data back.
