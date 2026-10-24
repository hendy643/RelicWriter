# References

Sources used for the save-format docs. CyberCAT, CyberCAT-SimpleGUI and CSE are MIT licensed. **WolvenKit is GPL-3.0.**

- Facts about the file format (field layouts, version numbers, node names) are not copyrightable, and the docs record them in our own words.
- We must not copy or translate WolvenKit's code into RelicWriter. Doing so would put the whole app under GPL-3.0. Reading it to learn the format and then writing our own implementation is fine. If we ever want to reuse its code directly, that is a licence decision for the maintainer to make first.
- For the MIT projects, copied code must keep their copyright notice. We plan our own implementation either way and treat all of these as references, not dependencies.

| Source | Language | Last change | What it gives us |
|---|---|---|---|
| [WolvenKit/CyberCAT](https://github.com/WolvenKit/CyberCAT) | C# | Feb 2022 (archived) | Header layout and packed date/time, chunk table handling, node table read and write, the two known chunk table layouts, parsers for a handful of nodes. |
| [Deweh/CyberCAT-SimpleGUI](https://github.com/Deweh/CyberCAT-SimpleGUI) | C# | Jul 2025 | The GUI. It loads saves through WolvenKit's save reader (below). It still ships the old compiled `CyberCAT.Core.dll`, which we decompiled and found identical in parser set to the archived CyberCAT source. |
| [PixelRick/CyberpunkSaveEditor](https://github.com/PixelRick/CyberpunkSaveEditor) (CSE) | C++ | May 2024 (patch 2.12) | The most recent readable parser: header, chunk and node handling, version gates per node, tests that re-serialise nodes and compare bytes, and notes on console saves. |
| [WolvenKit](https://github.com/WolvenKit/WolvenKit) `WolvenKit.RED4/Save` | C# | Jul 2025 (as pinned by SimpleGUI) | The newest readable save parser, and the one SimpleGUI actually uses. Header with the timestamp and game definition path, the node name list, 50 or so node parsers, and the game's `gameGameVersion` enum. **Licence is GPL-3.0, see below.** |
| [Red Modding wiki: save file](https://wiki.redmodding.org/cyberpunk-2077-modding/for-mod-creators-theory/files-and-what-they-do/file-formats/save-file-.dat) | prose | | Container overview. Matches what we found. |
| [cp2077-nativedb](https://github.com/rayshader/cp2077-nativedb) and [RED4ext.SDK](https://wiki.redmodding.org/cyberpunk-2077-modding/for-mod-creators-theory/core-mods-explained) | | | Type definitions from the game's reflection data. Not yet used. Candidate source for node payload formats. |

## Where each document draws on them

- `save-format.md`: header layout from CyberCAT, checked on our saves. Chunk table layouts from CyberCAT and CSE, checked on our saves. Node table checked on our saves and consistent with both tools.
- `save-versions.md`: the game version table comes from WolvenKit's enum. The version gate table comes only from CSE, CyberCAT and WolvenKit source and is **unverified** against saves.
- `save-nodes.md`: node names come from our saves. The "parsed" column comes from WolvenKit's node name list.

## Caveats about the references

- The Red Modding wiki's CyberCAT page warns that edits can leave a save that loads but has broken quests. CSE re-serialises each node it understands and compares the bytes with the original before allowing edits (`test_reserialize` in `savegame.h`), which is a check we should copy.
- CSE's README points users who want an easy editor to CyberCAT-SimpleGUI. Where the tools and our saves disagree, we trust our saves.
- Neither tool has been updated for patch 2.3 or later, and our three newest saves (261, 269, 277) are all beyond what their version gates cover.

## Data bundled in RelicWriter.Common

- `Resources/record-names.txt.gz` (fallback when the game files can't be read, and for parent-record lookups): record names (`Items.*`, `Ammo.*`, `Keycards.*`, `Vehicle.*`) taken from CSE's `assets/TweakDBIDs.json` (MIT, built for game 2.12), plus about 2,000 in-game item names from CyberCAT's `Names.json` (MIT). A TweakDBID in a save is the CRC-32 of the record name plus its length, so names are matched by hashing. Checked on our 24 saves: every item id found in them resolves except one. Most names are record names made readable, not the in-game text, which needs the game's localisation files.
- `Model/VehicleCatalogue.cs`: the vehicle record list from CP2077SaveEditor's `Vehicles.json` (MIT).
- `RelicWriter/ViewModels/InventoryViewModel.cs`: sub-inventory ids and names from CyberCAT's `SubInventoryTabItem` (MIT). Ids it does not list are shown as "Other storage N".
