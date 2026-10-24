# Node names

Every node name found in our saves, with whether a reference tool can parse its payload. Generated from the node tables of all 24 saves on the maintainer's machine (save versions 193, 261, 269 and 277), then compared with the node names listed in WolvenKit's save reader (used by CyberCAT-SimpleGUI), which has a parser for most of them. See [references.md](references.md).

How to read it:

- **Parsed** means the name is in WolvenKit's node name list, which a parser is registered for in almost every case (the `choices` parser is commented out in the source, for one). It does not mean the code is correct for every save version. Payload formats are not documented here yet.
- **Not parsed** means no reference tool reads the payload. The editor must carry those nodes through unchanged. They are the nodes we would need the game's own type data to understand.
- A name with a version suffix (`_v2`, `Ver3`) is a different schema from the unsuffixed name, so they are listed separately.
- An `x` under a version means at least one save of that version has the node. Nodes that appear only after progressing far into the game may be missing from a save that is early in the story, so a missing `x` is weak evidence.
- Nodes whose names start with a quote (such as `"Core"`) are child nodes inside other nodes. The quote is part of the name as stored.


138 distinct names: 51 parsed by at least one tool, 87 not parsed by any.

| Node name | Parsed | 193 | 261 | 269 | 277 |
|---|---|---|---|---|---|
| `"Core"` | no | x | x | x | x |
| `"DelayedStructs"` | no |  | x | x | x |
| `"ForbiddenArea"` | no | x | x | x | x |
| `"Timers"` | no | x | x | x | x |
| `AchievementSystem` | no |  | x | x | x |
| `ActivityCardsSystem` | no |  | x | x | x |
| `AnimationPersistentDataSystem` | no | x | x | x | x |
| `AnimationPersistentDataSystem_InstancedCrowdBlend` | no | x | x | x | x |
| `Animations` | no | x | x | x | x |
| `ArcadeSystem` | yes |  | x | x | x |
| `CAttitudeManager` | yes | x | x | x | x |
| `CCoverManager` | yes | x | x | x | x |
| `CameraComponentRenderLayer` | no | x | x | x | x |
| `CarrySystem` | no | x | x | x | x |
| `CharacetrCustomization_Appearances` | yes | x | x | x | x |
| `CommunitySystem` | yes | x | x | x | x |
| `ComponentsStateSystem` | no | x | x | x | x |
| `ComponetsState` | no | x | x | x | x |
| `ContainerManager` | yes | x | x | x | x |
| `ContainerManager_InjectedLoot` | yes | x | x | x | x |
| `ContainerManager_LootReinitBuffer` | no |  | x | x | x |
| `ContainerManager_LootSlotAvailability` | yes | x | x | x | x |
| `ContainerManager_NPCLootBags_Ver2` | yes | x | x | x | x |
| `ContainerManager_NPCLootBags_Ver3_LootedIDs` | yes | x | x | x | x |
| `ControlledLootIntroduction` | no |  | x | x | x |
| `DS_DynamicConnections` | yes | x | x | x | x |
| `DelamainTaxi` | no |  |  | x | x |
| `DelaySystem` | yes |  | x | x | x |
| `DelaySystem_V2` | no |  | x | x | x |
| `DelayedTicks` | no |  | x | x | x |
| `DestructionPersistencySystem` | no | x | x | x | x |
| `DeviceSystem` | yes | x | x | x | x |
| `DirectorSystem` | yes | x | x | x | x |
| `DynamicEntityIDSystem` | yes | x | x | x | x |
| `EffectSpawnerSavedata` | no |  | x | x | x |
| `FactsDB` | yes | x | x | x | x |
| `FactsTable` | yes | x | x | x | x |
| `FadeOutSave` | no | x | x |  | x |
| `GPSSystem` | no | x | x | x | x |
| `GameAudio` | yes | x | x | x | x |
| `GameSessionDesc` | yes | x | x | x | x |
| `GuardAreaManager` | no |  | x | x | x |
| `HandicapAmmoInContainers` | no |  | x | x | x |
| `InterestingConversations` | no |  | x | x | x |
| `IsInMirrorsAreaSave` | no | x | x | x | x |
| `ItemDropStorage` | yes | x | x | x | x |
| `ItemDropStorageManager` | yes | x | x | x | x |
| `JournalManager` | yes | x | x | x | x |
| `LevelAssignmentSystem_Ver1` | no | x | x | x | x |
| `LevelGainedAggregator` | no | x | x | x | x |
| `LocalPlayer` | no | x | x | x | x |
| `LootManager_PersistentTables` | no |  | x | x | x |
| `LootManager_PlaythroughSeed` | no |  | x | x | x |
| `MappinSystemRefactored` | no | x | x | x | x |
| `MappinSystem_CustomPos` | no | x | x | x | x |
| `MappinSystem_DelamainMappin` | no |  |  | x | x |
| `MappinSystem_POI` | no | x | x | x | x |
| `MappinSystem_SavedStealthMappinData` | no |  | x | x | x |
| `MappinSystem_TrackedMappin` | no | x | x | x | x |
| `MappinSystem_UnlockedLocations` | no | x | x | x | x |
| `ModdingSystem` | no |  | x | x | x |
| `MountingStorage` | no | x | x | x | x |
| `MovingPlatformSystem` | yes | x | x | x | x |
| `MusicSystem` | yes | x | x | x | x |
| `ObjectCarrySystem` | no | x | x | x | x |
| `PSData` | yes | x |  |  |  |
| `PathInstance_PlayerTracked_v1` | no | x | x | x | x |
| `PathInstance_Quest_v1` | no |  | x | x | x |
| `PersistencySystem` | yes | x |  |  |  |
| `PersistencySystem2` | yes |  | x | x | x |
| `PhantomEntity` | no | x | x | x | x |
| `PhoneManager` | no | x | x | x | x |
| `PhotoMode_BugFixing` | no |  |  |  | x |
| `PhotoMode_LightSettings` | no |  |  | x | x |
| `PhotoMode_NewStickersEditor` | no |  | x | x | x |
| `PhotoMode_OutfitWeather` | no |  |  | x | x |
| `PhotoMode_PresetsBugFixing` | no |  |  |  | x |
| `PhotoMode_QuestRequest` | no | x | x | x | x |
| `PhotoMode_Settings` | no | x | x | x | x |
| `PlayerSystem` | yes | x | x | x | x |
| `PreventionSpawnSystem` | no |  | x | x | x |
| `ProgressAggregator_v1` | no | x | x | x | x |
| `ProgressAggregator_v2` | no | x | x | x | x |
| `ProgressAggregator_v3` | no | x | x | x | x |
| `ProgressAggregator_v5` | no | x | x | x | x |
| `ProgressAggregator_v6` | no |  | x | x | x |
| `QuestMusicHistory` | yes | x | x | x | x |
| `QuestProgressedAggregator` | no | x | x | x | x |
| `QuestProgressedAggregator_v2` | no | x | x | x | x |
| `QuestProgressedAggregator_v3` | no | x | x | x | x |
| `QueuedData` | no | x | x | x | x |
| `RadioSystem` | yes | x | x | x | x |
| `ReactionSystem` | yes |  | x | x | x |
| `ReactionSystem_v2` | yes |  | x | x | x |
| `RenderGameplayEffectsManagerSystem` | yes | x | x | x | x |
| `SceneSystem` | no | x | x | x | x |
| `ScriptableSystemsContainer` | yes | x | x | x | x |
| `StatPoolsSystem` | yes | x | x | x | x |
| `StatsDataSystem` | no | x | x | x | x |
| `StatsSystem` | yes | x | x | x | x |
| `TelemetrySystem` | no | x | x | x | x |
| `TimeSystem` | no | x | x | x | x |
| `TransformAnimator` | no | x | x | x | x |
| `TutorialManager` | no | x | x | x | x |
| `TypeDatabase_v2` | no |  | x | x | x |
| `UIGameControllersState` | no | x | x | x | x |
| `UniqueItemCounter` | yes | x | x | x | x |
| `VehicleSystem` | no | x | x | x | x |
| `WardrobeSystem` | yes |  | x | x | x |
| `WardrobeSystem_ClothingSets` | yes |  | x | x | x |
| `WeatherWorldState` | no |  | x | x | x |
| `WorkspotInstancesSavedata` | yes | x | x | x | x |
| `WorldStateSystem` | no | x | x | x | x |
| `choiceHubSystem` | no | x | x | x | x |
| `choices` | yes | x | x | x | x |
| `countersState` | no | x | x | x | x |
| `currentFocusMode` | no | x | x | x | x |
| `customArray` | yes | x | x | x | x |
| `delayedReveals` | no | x | x | x | x |
| `entitiesState` | no | x | x | x | x |
| `eventManager` | yes | x | x | x | x |
| `game::SessionConfig` | yes | x | x | x | x |
| `godModeSystem` | yes | x | x | x | x |
| `inspectionServices` | no | x | x | x | x |
| `instances` | no | x | x | x | x |
| `inventory` | yes | x | x | x | x |
| `itemData` | yes | x | x | x | x |
| `nodeInstanceDataManager` | no | x | x | x | x |
| `photoModeSystem` | no | x | x | x | x |
| `playerObjectListenerMapper` | no | x | x | x | x |
| `questContentSystem` | no | x | x | x | x |
| `questDebugLogManager` | yes | x | x | x | x |
| `questPhaseFreezer` | no | x | x | x | x |
| `questPrefabsHandler` | no | x | x | x | x |
| `questSceneDataHelper` | no | x | x | x | x |
| `questSystem` | yes | x | x | x | x |
| `scanningController` | yes | x | x | x | x |
| `tierSystem` | yes | x | x | x | x |

## Open questions

- The payload format of every node. Only the container is documented so far.
- Which of the unparsed nodes matter for editing.
- Whether mods add nodes. No modded save has been checked.
