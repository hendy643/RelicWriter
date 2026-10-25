# Node payload formats

Formats of what is inside individual nodes. The container and node table are in [save-format.md](save-format.md); the node list is in [save-nodes.md](save-nodes.md); version numbers are in [save-versions.md](save-versions.md).

Every format here is **verified** the same way: a throwaway parser written from the reference sources ([references.md](references.md)) was run over a save of each version (193, 261, 269, 277) and had to consume every node's bytes exactly, with nothing left over and nothing missing. A format that fails that test is not listed as verified. Field names are ours; "unknown" means the bytes parse but their meaning isn't known.

Covered here: `FactsDB`, `FactsTable`, `inventory`, `itemData`, `CharacetrCustomization_Appearances`, `CommunitySystem`, `PlayerSystem`, `JournalManager`, and the starts of `TypeDatabase_v2` and `PersistencySystem` / `PersistencySystem2`. Nodes stored in the generic "system blob" format (`StatsSystem`, `StatPoolsSystem`, `ScriptableSystemsContainer` and others) are in [save-system-blobs.md](save-system-blobs.md). The remaining node names in [save-nodes.md](save-nodes.md) are still open.

## Primitives

- **Integers** are little-endian. `u8`, `u16`, `u32`, `u64`, `f32` have their usual sizes.
- **Varint** and **string** are as in the node table (see save-format.md): the first varint byte carries a sign flag in bit 7, a continuation flag in bit 6 and 6 value bits; later bytes carry 7 value bits and a continuation flag in bit 7. A string is a varint length (negative, meaning single-byte characters) followed by the characters.
- **TweakDBID** is 8 bytes: a `u32` CRC-32 of the record name, a `u8` name length, and three zero bytes. For example `Items.money` is `crc32("Items.money")`, length 11. Verified by finding `Items.money` in the inventory of every save version. Turning an ID back into a name needs a list of known record names, which a save doesn't contain.

## Common node header

Every node's data starts with a `u32` node id. It equals the node's index in the node table. **verified** for every node in every version tested.
A node's own data comes first, then its children's ranges. Data can also sit *between* child ranges, which is how `inventory` works (below).

## FactsTable

Node name `FactsTable`. A table of quest facts (named integers).

| Field | Type | Notes |
|---|---|---|
| node id | u32 | |
| count | varint | |
| hashes | `u32` × count | CRC-32 of each fact's name (per CSE and CyberCAT; not re-derived here). Sorted ascending in all 40 tables tested. |
| values | `u32` × count | One per hash, in the same order. |

The node has no children. **verified**
Fact names are not stored, only hashes. Showing readable names needs a separate list of known fact names; nothing in the save gives them. Up to about 6,500 facts per table were seen.

## FactsDB

Node name `FactsDB`. The parent of the fact tables.

| Field | Type | Notes |
|---|---|---|
| node id | u32 | |
| table count | u8 | Equals the number of `FactsTable` children (10 in every save tested). |

Children are the `FactsTable` nodes. **verified**
CyberCAT mentions a blob of trailing bytes after the last table. Whether our saves have one wasn't checked.

## inventory

Node name `inventory`. Children are all `itemData` nodes, one per item, in order.

| Field | Type | Notes |
|---|---|---|
| node id | u32 | |
| sub-inventory count | u32 | 5 to 13 in our saves. |
| sub-inventories | repeated | See below. |

Each sub-inventory:

| Field | Type | Notes |
|---|---|---|
| id | u64 | `1` is the first, `1,000,000` (0xF4240) the second; others look like hashes or entity ids. What each one is, is **open**. |
| item count | u32 | May be 0. |
| items | repeated | Each is an **item id header** followed by the item's `itemData` child node. |

The item id header is the same record that starts the child (see ItemID below). It is written in the inventory's own data, in the gap just before the child's range, and then the child repeats it. In all four versions every header equals the first bytes of its child after the child's node id. **verified**
Children are in sub-inventory order, so the first sub-inventory's items are the first children, and so on. The item counts add up to the number of children in every save tested. **verified**

Consequence for reading: an inventory can't be read from its own data alone. The headers and sub-inventory records live between the child ranges.

### ItemID

| Field | Type | Present when |
|---|---|---|
| record | TweakDBID (8 bytes) | always |
| seed | u32 | always |
| kind byte | u8 | save version ≥ 97 |
| unknown | u16 | save version ≥ 190 |
| unknown | u8 | save version ≥ 221 |

So the record is 15 bytes in our 193 save and 16 bytes in 261, 269 and 277. **verified** at 193 and 261; the exact thresholds 97, 190 and 221 come from CSE and are consistent with these two points but not tested at the boundaries.
The first field is the item record, such as `Items.money`. The seed is `2` on the first item of every save we looked at; its exact meaning is **open**.

## itemData

Node name `itemData`. One item. No children.

| Field | Type | Present when |
|---|---|---|
| node id | u32 | always |
| item id | ItemID | always |
| flags | u8 | always |
| unknown | u32 | always |
| quantity | u32 | kind is not 2 |
| mod root header | 16 bytes | kind is not 1 |
| mod tree | ItemMod | kind is not 1 |

**kind** decides which of the optional parts exist. It comes from the item id's kind byte and seed:

| Kind byte | Kind |
|---|---|
| 1 | 2 |
| 2 | 1 |
| 3 | 0 |
| anything else | 2 if seed ≠ 2, otherwise 1 |

| Kind | Has quantity | Has mod tree | Count in the 193 save |
|---|---|---|---|
| 0 | yes | yes | 7 |
| 1 | yes | no | 51 |
| 2 | no | yes | 165 |

The `kind` rule and the field order come from CSE (`itemData.h`) and CyberCAT. **verified**: using them, all itemData nodes in one save of each version were consumed exactly, with no leftovers. The other versions' counts are in the same ranges (kind 1 is the second most common, kind 0 the rarest).
What kinds 0, 1 and 2 mean in game terms (for example whether kind 2 is gear) hasn't been checked against item names.

**Adding an item** (`Inventory.Add`): the node is a copy of the shortest itemData of the same shape (in the newest saves gear with an empty mod tree is 87 bytes and a quantity-only item 25), with the record hash and length written over the copy's, a new random seed for gear (never 2, which marks the quantity-only kind in saves without a kind byte) and the quantity for the other shapes. It goes after the last item of the bag (sub-inventory 1); that item's gap, which held the next record, moves behind the copy and now holds the copy's item id header, the bag's count is raised by one and node ids are renumbered, since in every save checked a node's id is its position in the walk. Re-reading 100 real saves after adding one item of each shape agrees, and writing again is byte for byte the same. Whether the game accepts it is untested.

**Mod root header** (16 bytes): TweakDBID, `u32` unknown, `f32` unknown.

### ItemMod

A recursive record for an item and the mods attached to it.

| Field | Type | Present when |
|---|---|---|
| item id | ItemID | always |
| name | string | always |
| record | TweakDBID | always (the attachment slot, per CyberCAT) |
| child count | varint | always |
| children | ItemMod × count | |
| unknown | u32 | always |
| trailer | 16 bytes (TweakDBID, `u32`, `f32`) | save version ≥ 192 |

The trailer at ≥ 192 matches the layout of the mod root header. At 193 and above it is always present in our saves; below 192 is untested.

## TypeDatabase_v2

Node name `TypeDatabase_v2`; in saves 261, 269 and 277, not in 193. No children. Its job comes from the game's persistency loader (read in Ghidra, see [save-rtti.md](save-rtti.md)): the save carries its own table of types, and the loader compares the node's hash with the running game's. If they match it reads the persistent objects directly; if not, it rebuilds the type table and remaps. The loader only reads this node when the save version is 205 or above.

| Field | Type | Notes |
|---|---|---|
| node id | u32 | `3` in all three saves. |
| blob size | u32 | Equals the node's data length minus 8. |
| format version | u32 | `3` in all three. The loader treats values above 1 as present and handles 2 specially. |
| type table hash | u64 | Differs in every save (changes with the game's type data). |
| count A | u32 | 716, 735, 737 in 261, 269, 277. |
| count B | u32 | 496, 509, 511. |
| count C | u32 | 8073, 8221, 8230. |
| table A | 20 bytes × count A | Types, see below. |
| table B | 8 bytes × count B | Classes, see below. |
| table C | 12 bytes × count C | Properties, see below. |

**verified**: the header is what the game's loader reads, and the sizes add up exactly: 24 + 20·A + 8·B + 12·C equals the blob size in 261, 269 and 277 (115,188, 117,448 and 117,612 bytes), so the node has nothing else in it.

So the node is the save's own copy of the type layout of the persistent classes, with names as hashes (FNV-1a 64-bit, as in [save-property-streams.md](save-property-streams.md)). The loader compares its hash with the running game's. When they differ, the game uses these tables to read the old data and rewrites it for the current layout.

**Table A: types** (per entry: `u64`, `u64`, `u32`)

| Field | Notes |
|---|---|
| name hash | `u64`. The hash of a type name: a primitive (`Bool`), an enum, a class or struct, or a composite such as `array:ForcedStateData` or `handle:gameMountEventOptions`. 590 of 737 resolve to a name in the 2.31 type dump. |
| element hash | `u64`. For arrays and handles, the hash of the element or target type (123 of the 134 non-zero ones resolve); `0` otherwise. |
| size and kind | `u32`. Size in bytes in the bits from 12 up, a kind code in the low 8 bits. |

Kind codes seen: `0x10` Bool and Uint8 (size 1), `0x11` 16-bit integers (2), `0x12` 32-bit integers and Float (4), `0x13` 64-bit integers (8), `0x03` CName (8), `0x42` TweakDBID (8), `0x43` NodeRef (8), `0x50` enum (size 4, or 1 for `vehicleCameraPerspective`; the 29 enum sizes found by trial in the 277 save all agree), `0x22`, `0x23` and `0x24` classes and structs (430 of the `0x23`), `0x33` arrays, `0x93` handles. For classes the size is probably the object's size in memory (for example 120 for `ExplosiveDeviceControllerPS`). I only checked the kind and size of the primitives and enums against known values; the rest of the codes are from how the entries group by name.

**Table B: classes** (per entry: four `u16`)

| Field | Notes |
|---|---|
| type index | Index into table A of the class. Entries are sorted by it. |
| unknown | Differs for every class in all three saves, even where nothing seems to change; a checksum is plausible. **open** |
| count and flag | The high byte is the number of properties; the low byte is 0, 1 or (once) 2. |
| zero | Always 0. |

The counts of all B entries add up to the number of C entries (8,230 in the 277 save). **verified** C is cut into consecutive runs, one per B entry in order, and a class's run holds that class's properties: 5,562 of the 8,221 properties in the 277 save's runs for classes known to the dump are properties of that class in the dump (the rest are probably inherited from classes whose list is shared, or not saved by the dump's flags; not checked).

**Table C: properties** (per entry: `u64`, `u32`)

| Field | Notes |
|---|---|
| name hash | `u64`. FNV-1a 64-bit hash of the property name. 8,222 of 8,230 match a property name in the 2.31 dump. |
| offset and type | `u32`. The low 12 bits are the index of the property's type in table A (**verified**: `Bool` = index 705 whose entry is `Bool`, and the same for `Uint32`, `Int32`, `TweakDBID`, `CName` and an enum). Bits 12 to 19 are always 0. The bits from 20 up are a byte offset (for example 80, 136 and 232 for `Bool` properties); I did not confirm what it is an offset in. |

The same property name can appear under several classes. Entries are not sorted.

## PersistencySystem and PersistencySystem2

The persistent state of game objects (`PSData`, quest and world state). `PersistencySystem` (193, save version below 205) has `PSData` children; `PersistencySystem2` (261 and later) has no children and keeps everything in its own data.

Start of the node (**verified** in all four saves; the first two rows match the loader's read order):

| Field | Type | Notes |
|---|---|---|
| node id | u32 | |
| id count | u32 | 1,454 (193), 1,681 (261), 2,161 (269), 102 (277). |
| ids | u32 × count | All unique, not sorted, values around 9,000,000 (0x89xxxx) and the value `1`. The loader puts them in a set. What they identify is **open**. |

In `PersistencySystem` (193) the node ends here; the rest is in the `PSData` children, not decoded. In `PersistencySystem2` it continues:

| Field | Type | Notes |
|---|---|---|
| skip size | u32 | `0` in all three saves. The loader skips this many bytes if it is not 0. |
| entry count | u32 | 140,463 (261), 117,945 (269), 70,068 (277). |
| entries | repeated | See below. |

**Entry** (from the game's loader, then **verified**: it consumes every byte of the node in 261, 269 and 277, all persistent ids unique):

| Field | Type | Notes |
|---|---|---|
| persistent id | u64 | If `0` the entry ends here (8 bytes, nothing follows). Such empty entries are common: 52,413 of 140,463 in 261; 26,960 in 269; 12,120 in 277. What they stand for is **open**. |
| class | u64 | FNV-1a 64-bit hash of the class name (**verified**: all 141, 145 and 124 distinct classes in 261, 269, 277 match a class name in the 2.31 type dump). The classes are the `...PS` persistent-state classes, for example `LootContainerObjectAnimatedByTransformPS`, `BaseDestructibleControllerPS`, `DoorControllerPS`, `TVControllerPS`. |
| size | u32 | Length of the data, 0 to about 6,400 bytes. |
| data | bytes × size | The object's state as a property list: [save-property-streams.md](save-property-streams.md). |

Of the entries in each save, 88,050 (261), 90,985 (269) and 57,948 (277) have a non-zero id. All of them parse as property streams and consume their data exactly. **verified**

## CommunitySystem

Node name `CommunitySystem`; the largest node in every save (1.1 MB in 193, 1.8 to 2.2 MB in the others), no children. It stores the state of the world's communities and crowds. Read from the game's loader and saver in `Cyberpunk2077.exe` (see [save-rtti.md](save-rtti.md)), then **verified**: the layout below consumes every byte in one save of each version (193, 261, 269, 277). Field names are mine; nothing about what the fields mean is known.

In this section **string** is a varint length (negative, ASCII) followed by the characters, as in the node table. A **list of ids** is a `u32` count followed by that many `u64`.

| Part | Contents |
|---|---|
| node id | u32 |
| A: keys | list of ids (10,289 in 277) |
| A: values | For each key, in order: a list of ids, then for each id in it a **leaf** followed by a **string** (the string is `default` in the first one seen) |
| B: keys | list of ids (744 in 277) |
| B: leaves | One **leaf** per key, in order, with no string |
| C | `u32` count (140 in 277), then per entry: `u64`, 64 bytes, `u32`, then a `u32` count and that many 12-byte records |
| D | `u32` count, then that many 12-byte records. 0 in all four saves. |
| E | `u32` count, then per entry a string and a `u8`. 0 in all four saves. |
| F | Save version 124 or above only: `u32` count, then that many 16-byte records (0 in 193, 261 and 277; 486 in 269). |

**Leaf:**

| Field | Type | Present when |
|---|---|---|
| list 1 | list of ids | always |
| unknown | u16, then u8 | always |
| list 2 | list of ids | always (the game reads it only if the stream has a version; every save has one) |
| unknown | u16 | save version 15 or above |
| unknown | u16 | save version 143 or above |
| list 3 | list of ids | save version 180 or above |
| records | `u32` count, then that many 16-byte records | save version 254 or above |

The version thresholds 15, 143, 180, 254 and 124 are from the game's loader, and the save versions 193, 261, 269 and 277 straddle 180 and 254 (193 has no records list, 261 and later do), which the saves confirm. The other thresholds are untested here as all four saves are above 15, 124 and 143. Parts D and E are empty in all four saves, so their layouts are read from the game's saver only. The loader also has an extra branch for save versions 16 to 41 that I did not read.

## PlayerSystem

Node name `PlayerSystem`; 20 bytes, no children, in all four saves. From the game's loader, then checked against the saves for 193, 261, 269 and 277.

| Field | Type | Notes |
|---|---|---|
| node id | u32 | |
| player entity id | u64 | `1` in all four saves. The loader looks the entity up by this id. |
| name | u64 | A name (CName) of the player's puppet or spawn; `0` in the 277 save. Not resolved: see below. |

For save versions below 101 (0x65) the loader reads a single `u32` instead of the `u64` and has no name field. Our saves are all above that, so this was not tested. The name field is a CName; in a node it is a raw `u64` from save version 193 up, and a string below (the loader tests for 193 exactly, which is our oldest save).

The values 0x199db17687, 0x279241c18c and 0x28e32266b4 have small high halves (25, 39, 40), which fits a CName stored as `length << 32 | 32-bit hash`. I could not confirm it: none of them matches a name in the 2.31 type dump with that scheme.

## JournalManager

Node name `JournalManager` (quest journal state); no children. 13 KB (277) to 71 KB (269). Read from the game's loader and saver in `Cyberpunk2077.exe`, then **verified**: this layout consumes every byte in all 24 saves on the maintainer's machine (all four versions). Field names are mine.

| Field | Type | Present when |
|---|---|---|
| node id | u32 | always |
| entry count | u32 | always |
| entries | 15 bytes × count (see below) | save version 217 or above. Below, 16 bytes each. |
| unknown | u32 | always |
| list A | `u32` count, then `u64` × count | save version above 24 |
| list B | `u32` count, then 16-byte records × count | save version 111 or above |
| list B (old) | `u32` count, then `u64` × count | save version 107 to 110 |
| list C | `u32` count, then 16-byte records × count | save version above 119 |
| list D | `u32` count, then `u64` × count | save version above 232 |
| unknown | u32 | save version above 121 |
| list E | `u32` count, then `u32` × count | save version 234 or above |

**Entry (version 217 and above, 15 bytes):**

| Field | Type | Notes |
|---|---|---|
| id | u32 | Unique within the node (880 distinct of 880 in 277). The game looks entries up by this value; it is a hash, not a name. |
| state | u32 | 1 to 4 only (780 of 880 are 2 in the 277 save). Probably a journal entry state (inactive, active, succeeded, failed) but I did not confirm. |
| time | u32 | 46 distinct values in 277, with `43200` the most common (577 times); 43200 is 12 hours in seconds, which suggests a game-time stamp in seconds. Not confirmed. |
| count | u16 | Mostly 0 to 15. Meaning unknown. |
| flag | u8 | 0 or 1. |

The 16-byte entry used below version 217 has the same first three `u32`s and a fourth `u32` that packs the last two fields (28 bits and 4 bits), as the loader unpacks it; only the 193 save has this form here, and it parsed exactly.

The thresholds 24, 107, 111, 119, 121, 217, 232 and 234 come from the loader. 193 is the only save below 217 and above 111; the thresholds below 111 and the 107 to 110 form of list B are untested, as are the versions between 193 and 261. In the current-version saver the lists are written in the same order as above.

## CharacetrCustomization_Appearances

Node name `CharacetrCustomization_Appearances` (the misspelling is the game's). Stores the character's appearance choices. No children. Parsed with the layout below in **all 24 saves** on the maintainer's machine, all four versions: every byte of every node was consumed. The layout comes from CSE and CyberCAT; field names are ours except where marked.

In this table, **string** is a varint length (negative, meaning single-byte characters) followed by the characters, as in the node table.

| Field | Type | Present when |
|---|---|---|
| node id | u32 | always |
| data exists | u8 | always. `1` in every save seen. |
| unknown | u32 | always. `0` in the 277 save. |
| unknown | u32 | data exists. `13` in the 277 save. |
| unknown | u8 | data exists. `0` in the 277 save. |
| unknown | u8 | data exists. `0` in the 277 save. |
| character group list | GroupList | data exists (groups such as `TPP`, `FPP`, `hairs`, `face`) |
| weapon group list | GroupList | data exists (groups such as `holstered_default_tpp`, `unholstered_strong`) |
| body group list | GroupList | data exists (groups such as `FPP_Body`, `TPP_Body`, `character_creation`) |
| mapping count | u32, then Mapping × count | data exists |
| name count | varint | data exists and save version above 171 |
| names | string × name count | same |

If `data exists` is `0`, nothing follows the first unknown `u32`.

**GroupList:** a `u32` count, then that many Group records.

**Group:**

| Field | Type | Present when |
|---|---|---|
| name | string | always (for example `TPP`, `FPP`, `hairs`, `character_customization`, `TPP_photomode`, `face`) |
| part count | u32 | always |
| parts | Part × part count | |
| choice count | u32 | always |
| choices | Choice × choice count | |

**Part:**

| Field | Type | Present when |
|---|---|---|
| name | u64 if archive version is 195 or above, otherwise a string | always. Our saves are all at archive version 195. |
| resource name | string | always (for example `h0_000_pwa__basehead__face_rig`) |
| tag | string | save version 168 or above (for example `tpp_head_face_rig`, `skin_type_05`) |
| unknown | u32 | always |
| unknown | u32 | always |

**Choice:** a string (for example `eyes`), a string (for example `h041`), then two `u32`.

**Mapping:** three strings, such as `holstered_default`, `holstered_default_fpp`, `holstered_default_tpp`. The game uses these to tie a base name to its first-person and third-person variants.

Observations:

- The first list has 4 groups in the 193 save and 10 in the others; the second 17, 18, 18 and 35 groups; the third 7 in all. There are 6 mappings in all four.
- The name list holds `cold_white` and `Buzz` in the 277 save. Those look like hair colour and hairstyle names, but I have not checked that. The list was empty in the 261 save.
- The 64-bit `name` of each part is probably a hash of a string. The hash function is not identified.
- The thresholds 168 and 171 come from CSE and are consistent with the saves but untested at the boundaries; our oldest save is 193.

## Open questions

- What each sub-inventory id means (player, stash, vehicles, and so on).
- The meaning of the seed, flags and unknown fields in the item record and the item.
- Whether `FactsDB` can have trailing data in a save that has changes in quest sections we haven't seen.
- What the unknown fields in the appearance node mean, and which part of the node holds body type, skin tone, face shape and so on.
- The data inside each `PersistencySystem2` entry, the meaning of the `TypeDatabase_v2` table fields, and the remaining nodes, starting with `PSData`, `PlayerSystem`, `questSystem` and `JournalManager`.
