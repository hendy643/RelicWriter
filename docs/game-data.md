# Reading names from the game files

> The app no longer reads names from the game files at run time: `RelicWriter.Extractor gamedata` reads them once and writes them into the game database (`game-database.md`). This page describes the formats that tool (`RelicWriter.GameAssets`) reads. Only vehicle pictures are still read from the install at run time.

A save stores an item or vehicle only as a TweakDBID: the CRC-32 of the record name and the name's length. RelicWriter turns it into the in-game name at run time from the player's own install. Nothing from the game is bundled or copied into the repository. The code is in `RelicWriter.Common/GameData/`.

Everything below was read from the 2.5 files on one machine with Ghidra-free probing (hex dumps and test scripts), and checked against the 24 saves in `docs/save-node-instances.md`. Other game versions were not tried.

## The chain

1. The item's record id (hash, length) from the save.
2. The flat `<record>.displayName` in the TweakDB has the localisation key.
3. The on-screen text file maps the key to the English name.

The id of the flat is found **without knowing the record's name**: CRC-32 can be continued over more bytes, so the flat's hash is the record's hash carried on over the text `.displayName`, and its length is the record's length plus 12 (`TweakDb.Append`).

## TweakDB (`r6/cache/tweakdb.bin`, `tweakdb_ep1.bin`)

| Offset | Content |
|---|---|
| 0x00 | u32 magic `0x0BB1DB47`, u32 blob version (8), u32 parser version (4), u32 checksum |
| 0x10 | u32 offsets of the flats, records, queries and group tags sections (flats at 0x20) |
| 0x20 | u32 type count (22), then one 20-byte entry per flat type |

A type entry is: u64 FNV-1a hash of the type name (`gamedataLocKeyWrapper`, `TweakDBID`, `String`, `Float`, `array:...` and so on), u32 value count, u32 key count, u32 file offset of the type's data.

A type's data is: u32 value count, the values, u32 key count, then the keys. For the 8-byte types (checked for `gamedataLocKeyWrapper` and `TweakDBID`) a value is 8 bytes. A key is 12 bytes: u32 name hash, u8 name length, 3 padding bytes, u32 index into the values. Values are shared, which is why there are more keys than values. Variable-size types (`String`, arrays) were not decoded.

A `gamedataLocKeyWrapper` value is a u32 primary key followed by a u32 that is 0 in all but 16 of 11,602 values; those 16 are skipped (probably keys given by a hash of a text name). A key of 0 means no text.

`tweakdb_ep1.bin` (Phantom Liberty) has the same layout; its flats are added over the base ones.

**Not in the file:** which record a variant inherits from. About 17% of the items in our saves have no `displayName` flat of their own (crafting recipes have an empty key; many clothing variants have no flat). For variants RelicWriter tries the record name with its last `_nn` parts removed (`Items.Helmet_10_rich_01` then `Items.Helmet_10_rich`), which found a name for about half of them. The rest fall back to a readable form of the record name.

## Archives (`archive/pc/content/*.archive`, `archive/pc/ep1/*.archive`)

Format "RDAR", version 12. Header (40 bytes): magic, version, u64 index position, u32 index size, and more not used. The index has a 28-byte header (u32 values at 16, 20 and 24 are the file, segment and dependency counts), then 56-byte file entries (u64 FNV-1a hash of the lower-case path with backslashes, 8 bytes of time, u32 segment count, u32 first segment index at offset 20, and more), then 16-byte segments (u64 file offset, u32 stored size, u32 real size). The archive stores no file names. A segment whose stored size differs from its real size is `KARK`, the real size, then an Oodle Kraken stream.

Oodle is loaded from the game's own `bin/x64/oo2ext_7_win64.dll` (`OodleLZ_Decompress`). That DLL is a Windows library, so **reading the game files works on Windows only**: the Extractor, and the vehicle pictures in the app. Names come from the game database on every platform.

Only the first segment of a file is read; that is all the on-screen text needs.

## On-screen text (`base\localization\en-us\onscreens\onscreens.json` in `lang_en_text.archive`)

A CR2W file (version 195), 9.1 MB uncompressed, about 62,000 entries. Header tables used: strings (offset at 0x28) and names (offset at 0x34, count at 0x38, each entry a u32 string offset and a u32 hash). Entries are property lists: u16 name index, u16 type index, u32 size (counting itself), value. An entry starts with `primaryKey` (type `Uint64`, size 12) and then has `secondaryKey` and `femaleVariant` strings (the text shown) and sometimes `maleVariant`. A string is a length (first byte: bit 7 flag, bit 6 "more", 6 value bits; further bytes: 7 value bits plus a "more" bit in bit 7) and then UTF-8 bytes.

RelicWriter does not parse CR2W in general. It looks up the name indexes in the names table and finds entries by the byte pattern of `primaryKey`. That found all 62,229 entries.

Only English was tried. Other languages use other archives (`lang_<code>_text.archive`) and locale folders whose names were not checked.

## Finding the install (`GameLocator`)

In order: the `RELICWRITER_GAME_DIR` variable, the folder remembered from "Game folder...", then the launchers' own files. Each candidate counts only if `r6/cache/tweakdb.bin` exists in it.

- Steam (both systems): `libraryfolders.vdf` lists the libraries; `steamapps/appmanifest_1091500.acf` names the install folder (default `Cyberpunk 2077`). Windows also reads the Steam registry keys.
- GOG Galaxy (Windows): registry `SOFTWARE\WOW6432Node\GOG.com\Games\1423049311`, value `path`.
- Epic (Windows): `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item`, `InstallLocation` where `DisplayName` is Cyberpunk 2077.
- Lutris (Linux): `~/.config/lutris/games/*.yml` for a cyberpunk game: the folder three levels above `exe`, plus the usual GOG and Epic folders inside its Wine `prefix`.
- Heroic (Linux): `gog_store/installed.json` and `legendaryConfig/legendary/installed.json` under `~/.config/heroic` (also the Flatpak path), `install_path` of the Cyberpunk entry; and `~/Games/Heroic/Cyberpunk 2077`.

Tested only on the file formats, with sample text (`GameLocatorTests`); on real installs, only the Windows Steam one. The Linux and GOG/Epic paths come from the launchers' documented or observed layouts and have not been run on those systems.

## Not done or not checked

- Vehicle names come from the vehicle's own `displayName` ("Type-66 Avenger"). The maker is taken from the record name ("quadra"), because the manufacturer record has no name flat that I found.
- Item categories, descriptions and icons are not read.
- Item pictures are not read (only vehicle icons, below).

## Vehicle icons

Chain, read from the 2.5 files: vehicle record `.icon` (a TweakDBID flat) gives an icon record; its `.atlasResourcePath` (`raRef:CResource`, a path hash) names an `inkTextureAtlas` file in `basegame_4_gamedata.archive` and its `.atlasPartName` (CName) names a part. Flats of these types are stored like the others (u32 count, 8-byte values, then keys sorted by name hash); CName values are length-prefixed strings.

The atlas is a CR2W whose first slot imports `base\gameplay\gui\common\icons\vehicles_icons.xbm` and lists parts (`partName`, `clippingRectInUVCoords` with Left/Top/Right/Bottom floats; zero sides are omitted). The `.xbm` is a CR2W (object 0: `width`, `height`, `setup` with `compression`) whose pixels are the file's second archive segment: 4096x4096 BC3 (`TCM_DXTAlpha`), no mipmaps, 4x4 blocks in rows. Rows run bottom-up, so a part's rectangle is cut at `width - top - height` and flipped.

Checked on all 105 catalogue vehicles by looking at a contact sheet (all recognisable, none upside down) and by a test that each decodes to a non-blank image. Only BC3 is decoded and only the first (4096) slot is used; Phantom Liberty vehicles were found in the same atlas but a second PL atlas, if one exists, was not looked for. Windows only (Oodle).

The app does not decode these at run time: `RelicWriter.Extractor icons <game dir> <out dir>` writes each icon at half size as a PNG, and the PNGs are embedded in `RelicWriter.Common` (about 5 MB). Re-run it after a game update that changes the icons. The embedded images are CD PROJEKT RED's artwork; whether they may be redistributed was not checked.

## Perks and what they cost

Read from the 2.5 game files and the game's compiled scripts (`r6/cache/final.redscripts`, decompiled with the MIT-licensed redscript CLI); not tried on other versions.

- `gamedataNewPerkType` lists 194 perks. TweakDB has a record `NewPerks.<perk>` for each, whose `category` links to a Simple, Milestone or Master category, and one `NewPerkLevelData` inline record (`NewPerks.<perk>_inline<n>`) per level. The `pointsCost` flat of every level record is 0, so the cost is not in the data.
- The cost is in the script `PlayerDevelopmentData.BuyNewPerk`: every level takes 1 point (Primary for the five attribute trees, Espionage for the Relic tree), except the three Relic milestone perks (`Espionage_Central_Milestone_1`, `Espionage_Left_Milestone_Perk`, `Espionage_Right_Milestone_1`), which take 3. Selling a level gives the same back. A level can only be bought while the perks it requires are at their maximum level.
- `RelicWriter.Extractor/Seeds/new-perks.txt` (perk and maximum level, in the game's order) was made from the enum and a level-60 `AutoSave` that has most perks bought: 179 regular perks with 221 levels and 9 Relic perks with 9 levels. That save lacked the whole "Double Trouble" branch of the Cool tree (`Cool_Central_Milestone_2`, 2 levels, and `Cool_Central_Perk_2_1` to `2_4`), so those five were added from the TweakDB level records, which brings the regular perks to 227 levels (Relic: 15 points). `Cool_Right_Perk_3_3` is named "!OBSOLETE" in the game and is left out. The level counts of the five added perks are from TweakDB only; the first catalogue was wrong about them, so check others against it if a perk fails to max out in game.
- Perks a character has not bought are not stored. An entry that equals the default (`Body_Central_Milestone_1`, an attribute-data type of `BodyAttributeData`) is left out of the field, as with every enum.
- Not checked: that the game accepts a save with perks added or removed this way, and what unlocks a perk for purchase (a perk that was never unlocked may need an entry the editor does not write).
- Perk names: `NewPerks.<perk>.loc_name_key` is a `String` flat holding `LocKey#<number>` (three are spelled `Lockey#`), the primary key of an entry in the English `onscreens.json`. The base game's is `base\localization\en-us\onscreens\onscreens.json` in `archive/pc/content/lang_en_text.archive`; Phantom Liberty's is `ep1\localization\en-us\onscreens\onscreens.json` in `archive/pc/ep1/lang_en_text.archive` (a different path, not an override). All 188 perks the editor lists have a name, including the Relic ones. The description flat (`loc_desc_key`) holds markup and `{float_0}` placeholders the game fills in, so descriptions are not shown. Only English is read.
