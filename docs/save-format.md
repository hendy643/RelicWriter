# Cyberpunk 2077 `sav.dat` container format

Working spec, derived by reading real saves. Everything is marked **verified** (checked against real saves) or **open**.
See [save-versions.md](save-versions.md) for which versions were checked and how node sets differ between them.

All integers are little-endian. Four-character tags are stored byte-reversed on disk, so `VASC` on disk is `CSAV`.

**Verified against:** save versions 193, 224, 254, 261, 263, 269 and 277 (game versions 7, 8 and 9, 1600, 2010, 2100, 2120, 2310, 2500). The container layout below is identical in all four, apart from the sizes noted. Other versions are untested.

## Save folder

`%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077\<SaveName>\` contains `sav.dat`, `metadata.9.json` and `screenshot.png`.
`metadata.9.json` holds readable summary fields (level, life path, `buildPatch`, `playTime` and so on) and is what the load menu reads. **open:** its exact schema and how it relates to `sav.dat`.

## File layout

| Offset | Size | Field | Status |
|---|---|---|---|
| 0 | 4 | Magic `VASC` (`CSAV`) | verified |
| 4 | 4 | Save version | verified |
| 8 | 4 | Game version | verified |
| 12 | 1 | Padding byte, always `00` (see below) | verified |
| 13 | 4 | Clock, packed | verified |
| 17 | 4 | Date, packed | verified |
| 21 | 4 | Archive version (195 in every save seen) | verified |
| 25 | 4 | Tag `FZLC` (`CLZF`) | verified |
| 29 | 4 | Chunk count `N` | verified |
| 33 | 12 × N | Chunk table | verified |
| 33 + 12N | to `firstChunkOffset` | Zero padding (may hold stale bytes), see below | verified |
| `firstChunkOffset` | | `N` LZ4 chunks, back to back | verified |
| node table offset | | Node table | verified |
| end - 8 | 4 | Absolute offset of the node table | verified |
| end - 4 | 4 | Tag `ENOD` (`DONE`) | verified |

### Header fields at offsets 12 to 24

Layout taken from CyberCAT's `SaveFileHeader` and checked against our saves (see [references.md](references.md)).

- **Game definition path (offset 12):** a length-prefixed string. CyberCAT treats it as an unused padding byte, while CSE and WolvenKit's save reader (`GameDefPath`) read a string. It is empty (`00`) in every save seen, so all three readings agree for our saves. What the game writes here when it isn't empty is **open**. (`metadata.9.json` has a matching `gameDefinition` field, also empty in our saves.)
- **Timestamp (offset 13), `u64`:** WolvenKit reads the clock and date below as one 64-bit value, with the clock in the low half. The split into two packed `u32` fields is CyberCAT's.
- **Clock (offset 13), packed `u32`:** hour = `clock >> 22`, minutes = `(clock >> 16) & 63`, seconds = `(clock >> 10) & 63`, milliseconds = `clock & 1023`.
- **Date (offset 17), packed `u32`:** year = `date >> 20`, month = `1 + (date >> 15) % 32`, day = `1 + (date >> 10) % 32`.
- **Archive version (offset 21):** 195 in all four of our saves, including patch 1.04.

Checked on `ManualSave-6` (written 2026-10-23 21:22): clock `0x0556b578` decodes to 21:22, date `0x7ea4d800` to 2026-10-23. **verified**
CSE only reads the archive version when the save version is 83 or higher and treats missing as 192. Our oldest save is 193, so that doesn't come up for us.

### Chunk table

Each entry is `(offset u32, compressedSize u32, uncompressedSize u32)`. `offset` is absolute in the file.
Chunks are contiguous: each entry's `offset + compressedSize` equals the next offset, and the last one ends exactly at the node table offset. **verified**
The uncompressed size is 262,144 for every chunk except the last, which holds the remainder. **verified**

The table has a **fixed capacity**, and the space after the `N` real entries is zero padding up to the first chunk:

| Save versions | `firstChunkOffset` | Capacity | Chunk size |
|---|---|---|---|
| 193, 261 | 3,105 (`0xC21`) | (3105 - 33) / 12 = 256 entries | 262,144 |
| 269, 277 | 6,177 (`0x1821`) | (6177 - 33) / 12 = 512 entries | 262,144 |
| not seen in our saves | | 1,024 entries | 524,288 |

- CyberCAT recognises two layouts and finds the capacity as `(firstChunkOffset - 33) / 12`: 256 entries with 256 KiB chunks, and 1,024 entries with 512 KiB chunks. The 512-entry layout in versions 269 and later is a third one that CyberCAT's original code doesn't list. **verified** for our saves; the 1,024 layout is from CyberCAT only.
- CSE writes the table with a minimum `firstChunkOffset` of `0xC21`, which is the 3,105 we see in the older saves.
- In at least one of our saves the padding held entries that don't belong to the file, probably left over from an earlier write. **A reader must trust only the count at offset 29.**
- Whether the game picks the layout by save version or by save size is **open**.

### Chunks

Each chunk is the tag `4ZLX` (`XLZ4`), a `u32` uncompressed size (equal to the table entry), then a raw LZ4 block (no frame header).
Concatenating the decompressed chunks gives the **node data stream**. **verified**

### Logical offsets

Node offsets (below) live in a logical space: bytes `0..firstChunkOffset` are the file's own bytes, then the decompressed stream follows.
So `logical = firstChunkOffset + position in the decompressed stream`.
In all four versions the lowest node offset equals `firstChunkOffset` and the highest node end equals `firstChunkOffset + total decompressed size`. **verified**

## Node table

Located by the `u32` at `end - 8`. Layout:

1. Tag `EDON` (`NODE`).
2. Node count, as a varint.
3. For each node: name, then `nextId (i32)`, `childId (i32)`, `offset (u32)`, `size (u32)`.
4. Trailer: `u32` node table offset (again) and the tag `ENOD`.

**Varint:** the first byte holds a sign flag in bit 7, a continuation flag in bit 6 and 6 value bits; each later byte holds 7 value bits and a continuation flag in bit 7. **verified** (in every save the whole table is consumed exactly)
**Name:** a varint length, then the characters. The length is stored negated, which is why the sign bit is set. CyberCAT reads it as `-length` single-byte characters. Every name in our saves is stored this way. A positive length is never seen in our saves and CyberCAT doesn't handle it, so how it would be encoded is **open**.
**Names include version suffixes.** `PersistencySystem` in version 193 became `PersistencySystem2` from 261, and names like `ProgressAggregator_v5` carry a schema version. See [save-versions.md](save-versions.md).

### Tree

- Nodes form a tree. `childId` is the node's first child and `nextId` is its next sibling; `-1` means none. Both are indexes into the node table.
- Starting at node 0 and following `nextId` gives the root nodes (61 to 88 of them, depending on the save). Following `childId` and `nextId` reaches every node exactly once. **verified**
- `nextId` is always greater than the node's own index, so the table is in depth-first order. **verified**
- A parent's byte range contains all its children's ranges. **verified**
- A parent's payload is not made up only of its children. For example `GameSessionDesc` (58 bytes) has its first child at +4, so there is data before it. **open:** what that data is.

## Open questions

- Whether the string at offset 12 can ever be non-empty, and what the length prefix looks like for long strings.
- How a positive name length would be encoded.
- Console saves: CSE detects chunks that lack the `4ZLX` tag (it calls this "ps4w") and treats them as uncompressed, and also accepts the magic `SAVE`. Neither is in our PC saves, so the details are **open**.
- Internal format of each node's payload. Needs the game's RTTI dump and Ghidra.
- Why node sets differ between versions beyond suffix changes (see save-versions.md).
- Rewriting a save: node sizes, offsets, chunk table and trailer all change when a node grows, and the game may check more than these.

## Bytes between sibling nodes

A node's recorded range does not always end where the next sibling starts: in all four save versions some siblings have a gap of 4 to 16 bytes (for example between `UniqueItemCounter` and `inventory`, and between `itemData` nodes). The gap belongs to no node's size. A writer that wants an identical node table must keep these bytes. The gaps are not decoded.

## Data after the child nodes

A node's own data does not always end where its first child begins. Some nodes write further fields after their last child, inside the same node range. A parser that stops at the first child misses them. Known cases: `TimeSystem` (17 bytes), `GPSSystem` (one 12-byte position per child), `GameAudio` (a string, 261+), `ItemDropStorageManager` (u32), and `inventory` (24 or 36 bytes in some 277 saves: the sub-inventory records that follow the last item). The per-node layouts are in [save-node-specs.md](save-node-specs.md).
