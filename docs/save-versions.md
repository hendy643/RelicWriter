# Save versions

What we know about the differences between save versions. Source: real saves from the maintainer's machine, one per version.
The container layout is described in [save-format.md](save-format.md).

## Observed versions

| Save version | Game version | Patch (`buildPatch`) | Chunk table capacity | Nodes in sample | Status |
|---|---|---|---|---|---|
| 193 | 8 | 1.04 | 256 | 366 | container verified |
| 197 | 1210, 1220 | 1.21, 1.22 | | | parsed, node trees round-trip (see [game-versions.md](game-versions.md)) |
| 220 | 1500 | 1.5 | | | same |
| 224 | 1600, 1610, 1630 | 1.6, 1.61, 1.63 | | | same |
| 254 | 2000 | 2.0 | | | same |
| 261 | 2100 | 2.1 | 256 | 593 | container verified |
| 263 | 2120 | 2.12 | | | parsed, node trees round-trip |
| 265 | 2210 | 2.21 | | | same |
| 269 | 2310 | 2.31 | 512 | 574 | container verified |
| 277 | 2500 | 2.5 | 512 | 207 to 224 | container verified |

"Container verified" means the chunks decompress, the node table parses to the last byte, and the node ranges cover the stream exactly.
It does not mean the inner node payloads are understood. None of them are yet.

Node counts vary with how far into the game the save is, so they are not a version signal.

**Missing:** every save version between these, and anything from the first release (1.0) up to 1.04. The save version for patches 1.x is probably in the 190s, but only 193 has been seen. Game version `8` for patch 1.04 looks like an earlier numbering scheme than the 2100-style values used later; **open**.

## Version numbers in the header

The header carries three numbers (see [save-format.md](save-format.md)). CSE names them `v1`, `v2`, `v3`; CyberCAT names them save version, game version and archive version.

| Name | Our saves | Notes |
|---|---|---|
| Save version (`v1`) | 193, 261, 269, 277 | This is what node parsers branch on. |
| Game version (`v2`) | 8, 2100, 2310, 2500 | The game's own `gameGameVersion` enum, listed below. `8` is `CP77_PatchDay0_Hotfix2_V2`: the early builds used small sequence numbers, and patch-based numbering (patch times 1000) starts at 1.1. So a 1.04 save with game version 8 is expected. |
| Archive version (`v3`) | 195 in all four | CSE accepts up to 195 and rejects anything higher ("will change soon", per its comment). |

## Game version values

From the `gameGameVersion` enum in WolvenKit, which comes from the game's reflection data (see [references.md](references.md)). The enum stops at 2.3, so it does not name 2.31 (2310) or 2.5 (2500); those two values come from our saves, and the `buildPatch` in their metadata agrees.

| Value | Name | Value | Name |
|---|---|---|---|
| 0 to 4 | Initial, Development, GoldMaster, ActualGoldMaster, AlmostPatchDay0 | 1500 | 1.5 |
| 5 | PatchDay0 | 1510 | 1.5 hotfix 1 |
| 6 | PatchDay0 hotfix 1 | 1520 | 1.5 actual hotfix 1 |
| 7 | PatchDay0 hotfix 2 | 1599 | 1.5 development |
| 8 | PatchDay0 hotfix 2 v2 (our 193 save) | 1600 | 1.6 |
| 9 | PatchDay0 hotfix 3 | 1610, 1620, 1630 | 1.6 hotfixes 1 to 3 |
| 10 | Christmas hotfix | 2000 | 2.0 |
| 11 | Hotfix 4 (internal) | 2010, 2020 | 2.0 hotfixes 1 and 2 |
| 12 | Patch 1 internal | 2100 | 2.1 (our 261 save) |
| 1100, 1110, 1120 | 1.1 and hotfixes 1 and 2 | 2110, 2120, 2137 | 2.1 hotfixes 1 to 3 |
| 1200 | 1.2 | 2200, 2210 | 2.2 and hotfix 1 |
| 1210, 1220, 1230, 1240 | 1.2 hotfixes 1 to 4 (1240 is the first DLC build) | 2300 | 2.3 (the enum's "current") |
| 1300, 1301, 1399 | 1.3, hotfix 1, development | 2310 | 2.31 (our 269 save, not in the enum) |
| 1310 | 1.4 (a lower number than 1.3) | 2500 | 2.5 (our 277 save, not in the enum) |

The 1.4 value being lower than the 1.3 development build is the enum's own order; do not assume game versions only go up with patch number.
So the "1.04" patch label on our 193 save is the metadata's `buildPatch`, and its game version `8` is a pre-1.1 build number, since patch 1.04 shipped before the patch-based numbering began.

## Version-gated parsing in the reference tools

These are the places where CyberCAT and CSE read a node differently depending on the header version. Everything here comes from their source code, not from our own checks, so treat it as **unverified** until we test it on saves.
The two rows labelled "game code" are not from the reference tools: they were read out of `Cyberpunk2077.exe` with Ghidra (see [save-rtti.md](save-rtti.md)). Nothing newer than CSE v0.6.1 (May 2024, built for patch 2.12) is covered, and the version checks in it stop at save version 221.

| Condition | Where | Effect |
|---|---|---|
| save version ≥ 83 | header | The archive version field is present. Below that it is assumed to be 192. |
| save version ≤ 168 and game version = 4 | header | CSE refuses to load. |
| save version ≥ 97 | `inventory` | An extra byte blob is read for each item id. |
| save version ≥ 190 | `inventory` | Further extra data in the item id. |
| save version ≥ 221 | `inventory` | Further extra data in the item id. |
| save version ≥ 192 | `itemData` | An extra field is read and written. |
| save version > 171 | `CharacetrCustomization_Appearances` | An extra list of strings follows the string triples. |
| save version ≥ 168 | `CharacetrCustomization_Appearances` | A second string field per entry (CSE has this as a comment, not code). |
| archive version ≥ 195 | `CharacetrCustomization_Appearances` | The hash is a `u64`. Below 195 the layout is different (CSE has a separate branch). |
| save version < 205 (0xCD) | game code, persistency loader | Reads the old `PersistencySystem` node (with `PSData` children). From 205 it reads `TypeDatabase_v2` and `PersistencySystem2` instead. **From the game's own code** (patch 2.5 exe), checked against our four saves: 193 has the old node, 261 and later the new one. |
| save version > 124 (0x7C) | game code, old `PersistencySystem` path | The node starts with the id array described in [save-node-formats.md](save-node-formats.md). |
| game version = 2120 | CSE scripting | Only used to flag blueprint compatibility with patch 2.12. |

Observations that follow from this:

- The thresholds are 83, 97, 168, 171, 190, 192 and 221. Our 193 save is above all of them except 221. Our 261, 269 and 277 saves are above all of them.
- Neither tool gates anything on a save version above 221, which covers our 261, 269 and 277. The game's own loaders do (next section), so for nodes the tools parse, such as `itemData` and `inventory`, the game has a threshold at 248 that the tools don't. Whether it changes bytes in those nodes is **open**: the tools parse our 261, 269 and 277 saves without it.

## Version checks in the game's own node loaders

The reference tools only gate on save versions up to 221. The game itself has many more. This table was produced from `Cyberpunk2077.exe` (product version 2.5): for each node name the exe holds a small function that returns the name's hash; the functions that call it are the node's loader and saver. Of those, 65 compare the stream's save version with constants. The table gives each constant as "the layout changes at save version ≥ N" (a test `K < version` becomes N = K + 1).

How far to trust it: the method was checked against the reference tools. The item loaders compare against 96, 189 and 220, which are the tools' thresholds 97, 190 and 221 once the direction of the test is applied, and the appearance loader has 168, as in CSE. The `StatsSystem` loader has a threshold at 276, and the 2.5 layout change found in our 277 save ([save-system-blobs.md](save-system-blobs.md)) sits between our 269 and 277 saves, consistent with it.

The limits: this is read from decompiled code with a pattern match, not from the format. A loader with several nodes in one row (the function handles all of them) doesn't say which node a threshold belongs to. Small values (below about 30) and any threshold in the `PersistencySystem` row other than 125 and 205 may come from a variable that is reused for something else and are less trustworthy. What each change *is* isn't known unless it is described elsewhere in these docs. The highest threshold in the exe is 277 (`PhoneManager`), so 277 is the newest save version this build has a layout for.

| Node(s) | Loader (2.5 exe) | Thresholds: layout changes at save version ≥ |
|---|---|---|
| `AchievementSystem` | `0x1422c79b0` (198 bytes) | 257 |
| `ActivityCardsSystem` | `0x1415614dc` (298 bytes) | 213 |
| `AnimationPersistentDataSystem`, `AnimationPersistentDataSystem_InstancedCrowdBlend` | `0x140792c6c` (619 bytes) | 132, 173 |
| `CameraComponentRenderLayer`, `ComponentsStateSystem`, `ComponetsState` | `0x1412e6edc` (313 bytes) | 184 |
| `CarrySystem` | `0x140bea05c` (224 bytes) | 19 |
| `CAttitudeManager` | `0x140ede850` (304 bytes) | 98 |
| `CCoverManager`, `WorkspotInstancesSavedata` | `0x140ab85c0` (895 bytes) | 128, 144 |
| `CharacetrCustomization_Appearances` | `0x140972a68` (608 bytes) | 34, 68, 115, 168 |
| `ComponetsState`, `FadeOutSave`, `IsInMirrorsAreaSave` … | `0x1409bcdec` (1917 bytes) | 10, 106, 184 |
| `ContainerManager`, `ContainerManager_InjectedLoot`, `ContainerManager_LootReinitBuffer` … | `0x14073b7f8` (2647 bytes) | 43, 45, 48, 91, 103, 238, 245, 252, 253, 268 |
| `Core`, `TimeSystem`, `Timers` | `0x140a09e24` (1310 bytes) | 18, 21, 50, 66, 175 |
| `Core`, `DelaySystem`, `DelaySystem_V2` … | `0x141133b2c` (1401 bytes) | 212, 225 |
| `currentFocusMode`, `delayedReveals`, `scanningController` | `0x1412e6cdc` (564 bytes) | 94, 176 |
| `DelamainTaxi`, `MappinSystemRefactored`, `MappinSystem_CustomPos` … | `0x14103f420` (1124 bytes) | 14, 27, 28, 41, 170, 260, 267, 269 |
| `DestructionPersistencySystem` | `0x14062f864` (1897 bytes) | 4, 181, 198, 206 |
| `DirectorSystem` | `0x140a15620` (445 bytes) | 71, 177 |
| `DS_DynamicConnections`, `DeviceSystem` | `0x140a06e78` (269 bytes) | 141 |
| `DynamicEntityIDSystem` | `0x14103f0f0` (564 bytes) | 8, 11, 59 |
| `EffectSpawnerSavedata` | `0x140c8f280` (388 bytes) | 240, 242 |
| `ForbiddenArea`, `VehicleSystem` | `0x1409c4364` (596 bytes) | 39, 73 |
| `game::SessionConfig` | `0x1412e6b00` (512 bytes) | 67, 160, 219 |
| `GameAudio` | `0x140a15460` (350 bytes) | 38 |
| `godModeSystem` | `0x1409a4688` (560 bytes) | 78 |
| `GuardAreaManager` | `0x14144b0e0` (458 bytes) | 230 |
| `inventory`, `itemData` | `0x14074104c` (1234 bytes) | 97, 190, 221, 248 |
| `inventory` | `0x142b0e94c` (555 bytes) | 97, 190, 221 |
| `itemData` | `0x14073e3e8` (852 bytes) | 12, 36, 51, 85, 97, 190, 221 |
| `itemData` | `0x14074019c` (913 bytes) | 12, 36, 51, 85, 97, 190, 221, 227 |
| `itemData` | `0x1412f11e0` (594 bytes) | 97, 190, 221 |
| `itemData` | `0x142a952e4` (379 bytes) | 97, 190, 221 |
| `ItemDropStorage`, `ItemDropStorageManager` | `0x14073c9cc` (545 bytes) | 178 |
| `JournalManager` | `0x1409b8684` (2594 bytes) | 5, 25, 107, 111, 120, 122, 217, 233, 234 |
| `LevelAssignmentSystem_Ver1` | `0x14156664c` (241 bytes) | 61 |
| `LootManager_PersistentTables`, `LootManager_PlaythroughSeed` | `0x140be77b4` (338 bytes) | 210, 232 |
| `ModdingSystem` | `0x141847814` (202 bytes) | 229 |
| `MovingPlatformSystem` | `0x1404de8e8` (494 bytes) | 17 |
| `MusicSystem` | `0x140a15750` (619 bytes) | 82, 140 |
| `nodeInstanceDataManager` | `0x140739eec` (310 bytes) | 20 |
| `ObjectCarrySystem` | `0x140be9020` (265 bytes) | 163 |
| `PhantomEntity` | `0x14184a668` (125 bytes) | 92 |
| `PhoneManager` | `0x1415ad0d4` (424 bytes) | 117, 137, 201, 207, 243, 277 |
| `PhotoMode_BugFixing`, `PhotoMode_LightSettings`, `PhotoMode_NewStickersEditor` … | `0x14035d850` (1108 bytes) | 70, 81, 138, 195, 265, 266, 274, 275 |
| `playerObjectListenerMapper` | `0x140438048` (378 bytes) | 164 |
| `PlayerSystem` | `0x14132aa3c` (337 bytes) | 9, 101 |
| `PreventionSpawnSystem` | `0x140e184a0` (409 bytes) | 237 |
| `PSData`, `PersistencySystem`, `PersistencySystem2` … | `0x14059fb28` (1873 bytes) | 2, 3, 24, 125, 205 |
| `questContentSystem` | `0x14150f260` (671 bytes) | 24, 86, 204 |
| `questDebugLogManager` | `0x140739c80` (183 bytes) | 183 |
| `questPhaseFreezer` | `0x1407398a0` (284 bytes) | 135 |
| `questPrefabsHandler` | `0x140739d2c` (476 bytes) | 22 |
| `QuestProgressedAggregator`, `QuestProgressedAggregator_v2`, `QuestProgressedAggregator_v3` | `0x14119440c` (411 bytes) | 218 |
| `questSceneDataHelper` | `0x1407397ac` (242 bytes) | 146 |
| `questSystem` | `0x140739578` (803 bytes) | 139, 231 |
| `QueuedData` | `0x1407392e4` (659 bytes) | 139 |
| `RenderGameplayEffectsManagerSystem` | `0x1413e1ba8` (182 bytes) | 131, 166 |
| `SceneSystem`, `choiceHubSystem`, `choices` … | `0x14073d0fc` (814 bytes) | 193, 199 |
| `ScriptableSystemsContainer` | `0x140e18240` (328 bytes) | 3 |
| `StatPoolsSystem` | `0x140a075b0` (181 bytes) | 6 |
| `StatsDataSystem` | `0x14138c89c` (414 bytes) | 133 |
| `StatsSystem` | `0x140a96e68` (1037 bytes) | 52, 165, 226, 276 |
| `tierSystem` | `0x14140be50` (508 bytes) | 30 |
| `TutorialManager` | `0x1418174a0` (237 bytes) | 121 |
| `UIGameControllersState` | `0x140c79414` (243 bytes) | 102 |
| `UniqueItemCounter` | `0x141846114` (233 bytes) | 190 |
| `WardrobeSystem`, `WardrobeSystem_ClothingSets` | `0x141080dd0` (680 bytes) | 223, 224 |

Known meanings: `StatsSystem` 276 is the shared-buffer layout. `PersistencySystem` 205 is the switch to `TypeDatabase_v2` and `PersistencySystem2`, and 125 is whether the id array exists (see [save-node-formats.md](save-node-formats.md)). `inventory` and `itemData` 97, 190 and 221 are the item id fields; 248 is a threshold the tools don't have. `CharacetrCustomization_Appearances` 168 is the tag field.

## Reference tool support

- **CyberCAT-SimpleGUI** README lists patch 1.5X and 1.23 as fully supported and 1.3 as partial, but the README is out of date. The current code loads saves through WolvenKit's `CyberpunkSaveReader` (in `WolvenKit.RED4/Save`, included as a submodule), which **refuses any save with a game version below 2000 (patch 2.0)**. So the current SimpleGUI cannot open our 193 save at all.
- The `CyberCAT.Core.dll` that SimpleGUI still ships was decompiled for this project. It has exactly the same parser set as the archived WolvenKit/CyberCAT source, so it adds nothing beyond that repo.
- WolvenKit's reader has one game-version gate: from game version 2110 (patch 2.1 hotfix 1) a node `ContainerManager_InjectedLoot` carries one more byte per sub-entry. Its enum stops at 2.3 (2300).
- **WolvenKit/CyberCAT** (the original) was archived with its last change in Feb 2022, so it predates Phantom Liberty and 2.x.
- **CyberpunkSaveEditor (CSE)** is the most recent source we can read, up to patch 2.12 (May 2024).
- None of the three covers 2.3 or later. Our own saves are the only evidence for 269 and 277.

## Node set changes between observed versions

Only node names were compared, not payload contents. The lists show names that appear in one sample and not the other, so they can be affected by how far each save had progressed.

**193 to 261:** new nodes `AchievementSystem`, `ActivityCardsSystem`, `ArcadeSystem`, `DelaySystem`, `DelaySystem_V2`, `EffectSpawnerSavedata`, `GuardAreaManager`, `ModdingSystem`, `PreventionSpawnSystem`, `ReactionSystem`, `ReactionSystem_v2`, `PhotoMode_NewStickersEditor` and others. Gone: `PSData` and `PersistencySystem`. `PersistencySystem2`, `TypeDatabase_v2`, `PathInstance_Quest_v1` and `ProgressAggregator_v6` appear.

**261 to 269:** new `DelamainTaxi`, `MappinSystem_DelamainMappin`, `PhotoMode_LightSettings`, `PhotoMode_OutfitWeather`. Gone: `FadeOutSave`.

**269 to 277:** new `PhotoMode_BugFixing`, `PhotoMode_PresetsBugFixing`. Gone: `LevelGainedAggregator`.

## Versioned node names

Some node names end in a schema version, for example `TypeDatabase_v2`, `PersistencySystem2`, `ReactionSystem_v2`, `DelaySystem_V2`, `ProgressAggregator_v1` to `_v6`, `PathInstance_Quest_v1`.
The suffix appears to be how the game versions an individual node's payload format, separate from the file's overall save version. Whether a changed suffix always means a changed payload layout is **open**.

## Open questions

- Which save versions exist between and before the four observed ones, and what each changed.
- How the game's own loader treats older versions: does it upgrade nodes on load, and which node suffixes does it accept? Needs Ghidra.
- Whether any save version changes the container layout itself. All four seen share the same layout except for the chunk table capacity.
