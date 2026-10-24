# The `instances` node

`instances` holds the state of the game's scene system: one record per scene instance (for example `cs_sm_homeless_trashcan_player_scene`). It is the most complex node that the other node docs leave out. The layout below was read from the game's loader in `Cyberpunk2077.exe` (patch 2.5, about 45 small functions, addresses in brackets) and then tested on the saves.

## What was tested

- **Verified:** on all 24 local saves (save versions 193, 261, 269, 277) the parser reads the node id, the instance count and every instance, and it ends **exactly at the end of the node**, with no byte left over. A separate check found each instance's start by its text (three `en-us` strings in a row) and the parser's end position matched the next instance's start for **every** instance in four saves (one of each version: 9, 21, 60 and 23 instance boundaries).
- **Not exercised by any local save:** the parts marked *untested* below. Either every local array of that kind is empty, or the game only reads that branch below save version 193 (the oldest local save), so no local save can show it.
- The game's loader also looks for further optional sections after the instance list. None of the 24 saves contains one: the instance list uses the whole node.

## Notation

All integers are little-endian and packed (no padding). *name* is a string with the varint length prefix of [save-format.md](save-format.md) (the game's CName reader; in this node the names are stored as text). *string* is the same encoding. `v` is the save version. A count of type u32 is followed by that many elements. The game's version gates are written as `v >= N`, in decimal.

## Node

| Field | Type | Notes |
|---|---|---|
| node id | u32 | |
| instance count | u32 | 24 (193), 22 (261), 61 (269), 2 to 60 (277). |
| instances | Instance × count | |

## Instance

| Field | Type | Notes |
|---|---|---|
| key | u64, u64 | Two 64-bit values, a pair that identifies the instance. Meaning **open**. |
| header | [Header](#header) | |
| body | [Body](#body) | |

## Header

[14073f780]

| Field | Type | When | Notes |
|---|---|---|---|
| a, b | u8, u8 | | `0` and `0` in nearly every instance. |
| languages | name × 3 | | `en-us` three times in every save seen. |
| c | u8 | | `0` or `1`. |
| place | name | | For example `around_player`, `None`. |
| scene | string | | The scene file or id, for example `cs_sm_homeless_trashcan_player_scene`; empty in the 193 and 261 saves. |
| d | u8 | | |
| e | u8 | `v >= 162` | |
| tags count | u32 | `v >= 182` | A u8 below 182. *untested* (below 193). |
| tags | (name, u32) × count | | |
| f, g | u8, u8 | `v >= 44` | |
| h | u32 | `v >= 79` | |
| state | u32 | `v >= 169` | |
| extras | u32 count, then (u8, u64, u64, name) × count | `v >= 255` | Seen with one element in 1 instance. |

Between save versions 37 and 75 the game reads one more block here ([1401d8448], old form); no local save is in that range (**untested**).

## Body

[14073e810]. Five blocks in this order.

### Block 1: queue

[14073d53c]

| Field | Type | Notes |
|---|---|---|
| flag | u8 | |
| count | u32 | |
| entries | (u32, u8, u8, u8, u8 m, u64 × m) × count | `m` is the byte just before the u64 list. |

### Block 2: settings

[14073f044]

| Field | Type | When | Notes |
|---|---|---|---|
| a, b | u32, u32 | | |
| c | u64 | | |
| d | u8 | `v >= 29` | |
| names | u32 count, then name × count | `v >= 40` | |
| e | u8 | `v >= 56` | |
| groups | u32 count, then (Group, u64, u32) × count | `v >= 84` | |
| list 1 | [List X](#list-x) | | |
| list 2 | [List X](#list-x) | | |
| list 3 | [List X](#list-x) | `v >= 148` | |

**Group** [1411f6724]: u32 count, (u64, u32, u32, u32, u32) × count; then u32 count, (u32, u32, u32, u32) × count.

#### List X

[14073e8b0]

| Field | Type | Notes |
|---|---|---|
| first | u32 count, then Item P × count | |
| second | u32 | A count only. The game makes that many one-byte slots and stores nothing from the file in them. |
| third | u32 count, then Item Q × count | |

**Item P** [140740064]: u8, u64, u32, u64 (`v >= 33`), u64 (`v >= 123`), u32, u32, u8, u32.

**Item Q** [142d3c6ec]: u32, name, u16, u16, u32, u8, u64.

### Block 3: rules (`v >= 44`)

[1401d8e18], then [14073ef58]

| Field | Type | When | Notes |
|---|---|---|---|
| a | u8 | | |
| b | u8 | `v >= 145` | |
| options | u32 count, then Option × count | | |
| c | u8 | | |
| rules | [Rules](#rules) | `v >= 76` | |
| text | [Text](#text) | `v >= 90` | |
| d | u8 | `v >= 90` | |
| e | u8 | `v >= 142` | |

**Option** [142fca9d8]: u8, u8 kind; if kind is 9: name, u32; if kind is 10: name, u8; otherwise nothing. *untested*: no local save has an element.

Between versions 56 and 62 the game reads one more function here ([142d05a24]); **untested**.

#### Rules

[1401d87e8]

u32, u32, u8, u8, u8, u32, u32, u32, u8, u32, u32; then the **Table** below; then a u8 count and that many **Row**; then u8 and u32 (`v >= 90`).

- **Table** [1401d8994]: u32, u32, u32; u32 count, u32 × count; u32 count, **Triple** × count.
- **Triple** [14073da04]: **Id** (u64, u64, u64, u8, u8, and a u8 if `v >= 72`), then u32, u32, u32.
- **Row** [142d377fc]: u32; u32 (`v >= 127`); then three lists, each u32 count and Triple × count.

#### Text

[1401d8448] (for `v >= 90`)

| Field | Type | Notes |
|---|---|---|
| flag | u8 | |
| count | u32 | |
| lines | Line × count | The largest part of most instances: 384 instances, 420 lines in the local saves. |

**Line** [14073fcac]:

| Field | Type | When |
|---|---|---|
| id | u32 | |
| speaker | name | |
| voice | name | `v >= 215` |
| a | u8 | `v >= 108` |
| b, c, d | u8, u8, u8 | |
| e, f | u8, u8 | `v >= 127` |
| g, h | u32, u32 | |
| left, right | optional Cond each: a u8 that is 0 (none) or not 0 (one **Cond** follows) | `v >= 127` |
| conditions 1 | list of Cond, count u8 | |
| conditions 2 | list of Pair, count u8 | |
| conditions 3 | list of Item R, count u8 | |
| conditions 4 | list of Item S, count u8 | |
| h, i, j | u8, u8, u8 | |
| flags | u8 count, then u8 × count | |
| extra 1 | list of Item T, count u8 | `v >= 104` |
| k | u8 | `v >= 127` |
| conditions 5 .. 8 | the same four lists as 1 to 4 (the first is `Cond`) | |
| l | u8 | |
| flags 2 | u8 count, then u8 × count | |
| extra 2 | list of Item T, count u8 | `v >= 104` |
| extra 3 | list of Item U, count u8 | `v >= 113` |

- **Cond** [140f77ed4]: u8 kind; kind 1: name, u32, u8; kind 2: name, name, u8; any other kind: nothing more.
- **Pair** [14130c4cc]: u32, **Variant**, u8.
- **Variant** [142d2013c]: u8 kind; 0: string; 1: u32 count and name × count; 2: string, name; 3: name. *untested*: no local line used a Pair.
- **Item R** [141304bcc]: u32, string, u8.
- **Item S** [1401d859c]: u32, u8.
- **Item T** [141304c28 / 141304b70]: u8, string.
- **Item U** [14130c37c]: u8, u8.
- In this node a *list of X, count u8* is a u8 count followed by that many X.
- Between save versions 208 and 214 the game reads one more block in a line ([142d24a98]); **untested**.

### Block 4: counters (`v >= 62`)

[14073d6f8]

| Field | Type | Notes |
|---|---|---|
| sets | [Sets](#sets) | |
| flag | u8 | |
| a | u8, u32, u32 | From `v >= 127`. The decompiler lost the size of the first read; one byte is the only size for which all instance boundaries match. Below 127 the game reads different fields; **untested**. |
| entries | u32 count, then (u32, u8, u32, u8, and u8, u8 if `v >= 113`) × count | |

#### Sets

[14073da70]

1. u32 count, **Id** × count.
2. u32 count, then (u64, **Pair List**) × count.
3. u32 count, then (**Triple**, u8, u32) × count.

**Pair List** [14073d8d8]: u32 count and Triple × count; u32 count and Triple × count; u32 (`v >= 127`).

### Block 5: events (`v >= 198`)

[14073dd48]

| Field | Type | Notes |
|---|---|---|
| count | u32 | |
| events | Event × count | Two events in all local saves. |

**Event** [141933ccc]: u32, u16, u16, u32, u8 kind. Then:

- kind 0: nothing more.
- kind 1: u8 sub. If sub is 1: u8, u32, and 8 bytes. If sub is 2: name and 1 byte. Otherwise nothing more.
- kind 10: u32, u16, u16, u64, and 1 byte.
- any other kind: nothing more.

The layouts for kind 1 (both sub kinds) and kind 10 come from the game's code. The local saves contain two events, so only part of this is tested; the sizes of the trailing fields are read from the decompiler and **may be wrong**.

## Open

- What the key pair is, and what the many small fields mean. Nothing here is named by the game's data; the names above are only to tell fields apart.
- The branches for save versions below 193, which are marked *untested*.
- Whether any save has the optional sections after the instance list. The game reads them if present.
- Writing the node back: not tried.
