# Property streams

Some nodes store an object as a list of its properties. Each property carries the hash of its name and the hash of its type, so a reader can find the property in a class and skip what it doesn't know. This is how the entries of `PersistencySystem2` are stored ([save-node-formats.md](save-node-formats.md)). It is different from the "system blob" format in [save-system-blobs.md](save-system-blobs.md), which stores names as strings.

How this was found: the format was read from the game's serialiser in `Cyberpunk2077.exe` with Ghidra ([save-rtti.md](save-rtti.md)), then tested on the saves. Property and type names come from the 2.31 type dump. **Verified** on the 261, 269 and 277 saves: all entries of `PersistencySystem2` (88,050, 90,985 and 57,948) parse with this description and consume every byte. The 193 save has no `PersistencySystem2`, and the older `PSData` node uses the system-blob format instead ([save-system-blobs.md](save-system-blobs.md#psdata-193-only)).

## Layout

All integers little-endian. A **property list** is a sequence of properties, followed by a terminator in nested lists:

| Field | Type | Notes |
|---|---|---|
| name | u64 | FNV-1a 64-bit hash of the property name (for example `isInitialized`). `0` ends a nested list. |
| type | u64 | FNV-1a 64-bit hash of the property's type name, as the game spells it (`Bool`, `TweakDBID`, `array:Int32`, `handle:DeviceOperationsContainer`, `EDeviceStatus`, a class name). |
| value | | Depends on the type, see below. |

The FNV-1a 64-bit hash is: start at `0xcbf29ce484222325`; for each byte, xor it in and multiply by `0x100000001b3`, modulo 2^64.

Only properties that differ from their default are written. The game's writer checks each property against the default value and skips equal ones, and it drops a nested object entirely if nothing in it is written. So a missing property means "default", and the default has to come from the game's class definition (which the save doesn't contain).

The top-level list of an entry has no terminator of its own; it ends where the entry's data ends. Entries often end with some `u64` zeros (0 to 506 seen, mostly 0 or 1). They are only ever at the end, never followed by more properties. Why the number varies is **open**; the likeliest cause is how nested lists in arrays are closed, but I did not trace it.

## Values

| Type | Encoding |
|---|---|
| `Bool`, `Int8`, `Uint8` | 1 byte |
| `Int16`, `Uint16` | 2 bytes |
| `Int32`, `Uint32`, `Float` | 4 bytes |
| `Int64`, `Uint64`, `CName`, `TweakDBID`, `CRUID`, `NodeRef` | 8 bytes. `CName` and `NodeRef` are hashes (a name or path is not stored). `TweakDBID` is as in [save-node-formats.md](save-node-formats.md). |
| enum | The enum's size in bytes, raw value (not a name). The size is 4 for nearly all enums and 1 for `vehicleCameraPerspective`. The sizes are in the save's own type table: see [TypeDatabase_v2](save-node-formats.md#typedatabase_v2) (table A, kind `0x50`). |
| `array:T` | `u32` count, then count elements of type `T`. Primitives and enums are packed back to back. A struct element is a nested property list ending in a `u64` zero. |
| fixed arrays (`static:N,T` and `[N]T`) | The same as `array:T`: a `u32` count, then elements. Only 4 such types occurred in the sample, so this is lightly tested. |
| `handle:T` | `u64`: the FNV hash of the **actual** class of the object (it can be a subclass of `T`), then a nested property list ending in a `u64` zero. A null handle is not written. |
| struct or class | A nested property list ending in a `u64` zero. |

What decides how to read a type is its kind in the game's type data: primitive, enum, struct or class, array, handle. The type dump gives this for 2.31 types. The save's `TypeDatabase_v2` repeats the sizes and kinds, but a reader would still need the property lists and defaults from the game's own class definitions in order to write values back.

## What was not tested

- Anything older than the 261 save, and any entry class the three saves don't contain.
- Arrays whose elements are handles, arrays of arrays and the fixed array types, which are rare in the sample.
- Writing: nothing here was written back and loaded into the game.
