# Save node layouts (verified)

Generated from executable specs. Every layout here was run against all 24 local saves (save versions 193, 261, 269, 277). A node is **verified** only if the parser consumes every byte of every instance of it; **partial** means some bytes are left undecoded.

The same layouts in machine-readable form: [save-spec.xml](save-spec.xml) (the complete record: container, versions, types, formats and every node).

Conventions: all integers are little-endian. `v` in a *When* column is the save version (header offset 4). `a` is the archive version. A *CName* is a u64 hash when `v >= 193`. A *varint* count is the container varint from [save-format.md](save-format.md). Gates marked "untested" come from the game code, and no local save exercises them.

**Data after the child nodes.** A node's payload is not finished when its first child starts. Some nodes write more fields after the last child. Those are listed under *Data after the children*.


## AchievementSystem

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| achievements | array, u32 count |  | 32 bytes each; the node is only read from save version 257 on (loader gate). Element: 32 bytes. |


## ActivityCardsSystem

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| data | array, u32 count |  | 0 bytes in every save seen. Element: 1 bytes. |


## AnimationPersistentDataSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| groups | parallel arrays, u32 count |  | n x u64 group ids, then one group per id. One count, then each column in turn: 8 bytes, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ values | parallel arrays, u32 count |  | second array of each group is n x u32. One count, then each column in turn: 8 bytes, 4 bytes. |


## AnimationPersistentDataSystem_InstancedCrowdBlend

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| ids | array, u32 count |  | 0 in every save seen. Element: 8 bytes. |


## Animations

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Layout from game code functions 140987984, 140988054, 14098811c, 1409881fc, 140988408.

| Field | Type | When | Notes |
|---|---|---|---|
| groups | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ key | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ poses | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ key | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ transform | 28 bytes |  | 7 x u32 read one by one, then repacked by the game (probably a position and a quaternion) |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | 21 bytes each |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ key | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 |  |  |


## ArcadeSystem

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| values | array, u32 count |  | 3 floats in every save seen. Element: f32. |


## CameraComponentRenderLayer

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| layers | parallel arrays, varint count |  | ids first (n x u64), then n camera lists. One count, then each column in turn: struct, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ cameras | parallel arrays, varint count |  | second column is one flag byte per camera name. One count, then each column in turn: struct, 1 bytes. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |


## CarrySystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| carried | array, u32 count |  | 80 raw bytes each; the saver writes them as one block. Element: 80 bytes. |


## CAttitudeManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| groups | array, u32 count |  | node is read from save version 98 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ group | u64 |  | hash of the attitude group |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u32 |  | 0, 1 or 2 in every save seen |
| a | u16 |  | 0xFFFF in every save seen |
| b | u32 |  | 0xFFFFFFFF in every save seen |
| overrides1 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  | element layout from the game code: u64, u16, u32, then lists; untested |
| overrides2 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  | element layout not decoded; untested |
| overrides3 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  | element layout not decoded; untested |


## CCoverManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| covers | array, u32 count |  | game code reads (u64, u64, u8) per entry |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ key | u64 |  | looks like a hash |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ value | u64 |  | small number, 0x89.... or 0x98.... in the saves seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  | 0 in the samples |


## choiceHubSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| hubs | array, u32 count |  | 0 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ subs | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| choices | array, u32 count |  | 0 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ d | u64 |  |  |
| flag | u8 |  | 0 in every save seen |


## choices

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ y | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ z | u32 |  |  |
| b | array, u32 count | `v>193` | second list; absent in the 193 save and present in the later ones. The exact version where it appears is not known (the loader gate was not found) |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ values | array, u32 count |  | 16-byte elements. Element: 16 bytes. |


## ComponentsStateSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## ComponetsState

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| components | parallel arrays, varint count |  | ids first (n x u64), then n name lists; same layout as CameraComponentRenderLayer. One count, then each column in turn: struct, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ names | parallel arrays, varint count |  | second column is one flag byte per name. One count, then each column in turn: struct, 1 bytes. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |


## ContainerManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Map of container id to its recorded contents. Layout from game code function 14073a1b8 and element reader 14218ec40.

| Field | Type | When | Notes |
|---|---|---|---|
| containers | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  | container entity id |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ index | u16 |  | 0xffff in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, varint count | `v>=261` | read from save version 261 on |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ rngSeed | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 | `v>220` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ count | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u64 |  | TweakDBID/CName |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 | `v>=192` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tags | array, varint count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tag | string (varint length) |  | e.g. Ammo, ShowStackPrice, Currency |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, varint count |  | item ids held by the container |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ rngSeed | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 | `v>220` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 | `v>262` |  |


## ContainerManager_InjectedLoot

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| containers | parallel arrays, varint count |  | n x u64 container ids, then for each a list of injected items; fields after the first four are gated by save version. One count, then each column in turn: 8 bytes, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, varint count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ quantity | u32 |  | probably |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 | `v>118` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u16 | `v>219` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tags | array, varint count | `v>235` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tag | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ source | u64 | `v>238` | TweakDBID |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ sourceSeed | u32 | `v>238` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ sourceOther | u32 | `v>238 and v>=192` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 | `v>262` |  |


## ContainerManager_LootReinitBuffer

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| buffers | parallel arrays, varint count |  | n x u64 ids, then n records; node is read from save version 238 on. One count, then each column in turn: 8 bytes, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, varint count |  | CNames. Element: u64. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 | `v>246` |  |


## ContainerManager_LootSlotAvailability

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| slots | parallel arrays, varint count |  | two parallel arrays after one count: n x u64 ids, then n x u8 flags. One count, then each column in turn: 8 bytes, 1 bytes. |


## ContainerManager_NPCLootBags_Ver2

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| bags | array, varint count |  | empty (one 0 byte) in every save after 193, so only the 193 layout is tested; the gates above come from the game code |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  | e.g. arasakaDropBag |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ position | 12 bytes |  | 3 x f32 probably |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, varint count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ quantity | u32 |  | probably |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 | `v>118` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u16 | `v>219` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tags | array, varint count | `v>235` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tag | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ source | u64 | `v>238` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ sourceSeed | u32 | `v>238` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ sourceOther | u32 | `v>238` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 | `v>262` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ extraId | u64 | `v>188` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ extraValue | u32 | `v>188` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |


## ContainerManager_NPCLootBags_Ver3_LootedIDs

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| ids | array, varint count |  | ids of looted bags; node is read from save version 103 on. Element: u64. |


## ControlledLootIntroduction

Status: **verified**. 23 instances in versions 261, 269, 277.

Nested map; layout from game code functions 14073ac0c and 140740530. Field meanings unknown.

| Field | Type | When | Notes |
|---|---|---|---|
| groups | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ key | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u16 | `v<253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ d | u16 | `v<253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 | `v>=253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 | `v>=253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ key | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ p | u16 | `v<253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ q | u16 | `v<253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ p | u32 | `v>=253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ q | u32 | `v>=253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ r | u32 | `v>=268` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ s | u32 | `v>=268` |  |
| legacyMap | group | `v<253` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ legacy | array, u32 count |  | untested: only read below save version 253, from game code |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ key | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ p | u16 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ q | u16 |  |  |
| trailer | u32 |  | read after the map, from save version 245 on; differs per save, looks like a hash or seed |


## Core

Status: **verified**. 47 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| delay | group | `_len==4` | child of DelaySystem: 4 bytes |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ value | u32 |  | child of DelaySystem |
| time | group | `_len==20` | child of TimeSystem: 20 bytes |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ gameTime | u64 |  | f64 seconds (looks like it) |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ scale | u32 |  | f32 (0.26 in one save) |


## countersState

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| value | u32 |  |  |


## currentFocusMode

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| mode | u32 |  |  |


## customArray

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| values | array, u32 count |  | loader 14073a0d0 reads the count, then count x 2 bytes in one block. Element: u16. |


## DelamainTaxi

Status: **verified**. 21 instances in versions 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| position | 12 bytes |  | 3 x f32 probably; all zero in every save seen |


## delayedReveals

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| entries | array, u32 count |  | 0 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | CName (u64, or string if v<193) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |


## DelayedStructs

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| queues | array, u32 count |  | queue count, 4 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |


## DelayedTicks

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| groups | array, u32 count |  | 1 group in every save; the group list is saved as a count then each group |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ ticks | array, u32 count |  | inner list empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |


## DelaySystem

Status: **verified**. 23 instances in versions 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## DelaySystem_V2

Status: **verified**. 23 instances in versions 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## DestructionPersistencySystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| destroyed | array, u32 count |  | ids of destroyed objects. Element: u64. |
| sizes | array, u32 count |  | 8 entries in every save seen. Element: u32. |
| objects | parallel arrays, u32 count | `v>197` | four parallel arrays after one count: n x u64, n x u64, n x u64, n x u32; present in the saves from 261 on, absent in the 193 save (the exact version where it appears is between them, probably 198 from the loader gates). One count, then each column in turn: 8 bytes, 8 bytes, 8 bytes, 4 bytes. |
| groups | parallel arrays, u32 count | `v>197` | two parallel arrays after one count: n x u32, then n x 32 bytes. One count, then each column in turn: 4 bytes, 32 bytes. |


## DeviceSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## DirectorSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| scenes | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ scene | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ tier | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ extra | u32 |  | the game always writes 0 here |


## DS_DynamicConnections

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| devices | parallel arrays, varint count |  | n x u64 device ids, then n records. One count, then each column in turn: 8 bytes, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ links | varint counts x2, then arrays |  | two varint counts, then two arrays of u64 ids. Element sizes: 8, 8 bytes. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ position | 12 bytes |  | 3 x f32 probably |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ tag | string (varint length) | `v>=185` | only from save version 185 |


## DynamicEntityIDSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| nextId | u32 |  | counter in the 10,000,000 range |
| nextId2 | u32 | `v>7` | counter in the 9,000,000 range |
| c | u32 |  | 0 in the saves seen; the game code checks it is below 1001 |
| names | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  | entity name, e.g. p_q001_04_ripperdoc_... |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u32 |  |  |
| trailer | u32 |  |  |


## EffectSpawnerSavedata

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| spawners | array, u32 count |  | the loader has a second version gate at save version 242 that no save on this machine exercises |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ effect | string (varint length) |  | e.g. radio_idle, dust_01, blood, hit_wall |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ tag | string (varint length) |  | "None" or a second name |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ extra | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ f1 | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ f2 | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ f3 | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ f4 | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ f5 | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  | f32 probably |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ y | u32 |  | f32 probably |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ pairs | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ z | u32 |  |  |


## entitiesState

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| entities | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ state0 | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ h | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ i | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ j | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ k | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ l | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ m | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ n | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ o | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ p | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ q | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ r | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ s | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ t | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ u | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ w | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ state1 | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ h | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ i | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ j | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ k | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ l | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ m | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ n | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ o | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ p | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ q | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ r | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ s | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ t | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ u | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ w | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ state2 | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ h | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ i | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ j | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ k | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ l | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ m | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ n | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ o | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ p | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ q | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ r | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ s | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ t | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ u | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ w | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, u32 count |  | pairs of item ids |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ key | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ rngSeed | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 | `v>220` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId2 | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ rngSeed2 | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a2 | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b2 | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c2 | u8 | `v>220` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ list2 | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ position | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ s2 | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ h | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ i | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ j | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ k | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ things | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ l | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ m | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ list3 | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ position | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ s | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ h | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ i | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ j | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ list4 | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ flagA | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ hasExtra | u8 | `v>193` | not present in the 193 save |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ extra | group | `v>193 and hasExtra!=0` | only when hasExtra is not 0 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ block | 16 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | u64 |  |  |
| flags | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| ids | array, u32 count |  | . Element: u64. |


## eventManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| events | array, u32 count |  | loader 14073a024 reads the count, then count x 16 bytes in one block; the split into u64, u32, u32 is a guess |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ key | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |


## FadeOutSave

Status: **verified**. 4 instances in versions 193, 261, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| flag | u8 |  | 1 in every save seen |
| value | u32 |  | all bits set in every save seen (0xFF in the first byte) |


## ForbiddenArea

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| areas | array, u32 count |  | 16 bytes each: an 8-byte hash, then 8 bytes that look like a raw pointer, so they differ between saves. Element: 16 bytes. |


## game::SessionConfig

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | u64 |  |  |
| sessionId | u64 |  | differs per save |
| district | string (varint length) |  | e.g. 03_night_city |
| d | u64 |  | 8 bytes that look like an f64 |
| e | u32 |  |  |
| f | u64 | `v>218` |  |


## GameAudio

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id (u32) and child nodes.

**Data after the children:**

| Field | Type | When | Notes |
|---|---|---|---|
| state | string (varint length) | `v>193` | "None" in every save that has it; absent in the 193 save |


## GameSessionDesc

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## GPSSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id (u32) and child nodes.

**Data after the children:**

| Field | Type | When | Notes |
|---|---|---|---|
| repeat to end of data |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ position | 12 bytes |  | 3 x f32, one entry per child node |


## HandicapAmmoInContainers

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| records | array, varint count |  | 0 in every save seen. Element: 8 bytes. |


## inspectionServices

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| services | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ list1 | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ list2 | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ list3 | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |


## InterestingConversations

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| conversations | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | string (varint length) | `kind in (0,1)` | only when kind is 0 or 1 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | string (varint length) | `kind==0` | only when kind is 0 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 |  |  |


## IsInMirrorsAreaSave

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| entities | parallel arrays, varint count |  | two parallel arrays after one count: n x u64 ids, then n x u8 flags. One count, then each column in turn: 8 bytes, 1 bytes. |


## ItemDropStorage

Status: **verified**. 309 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| name | string (varint length) |  | "None" in every save seen |
| flag | u8 |  |  |
| a | u32 |  |  |
| b | 12 bytes |  | three u32/f32 |
| c | u32 |  |  |
| d | u32 |  |  |
| e | u32 |  |  |
| f | u32 |  |  |
| itemCount | u32 |  | The number of items; each is an `itemData` child node of this node. |
| firstItem | group | `itemCount != 0` | Only the first item's header is in the node's data. Each item is a 15-byte header followed by its `itemData` child, so the header of item N+1 is in the gap after child N (`SaveNode.After`). |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ item | CName (u64, or string if v<193) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ y | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ z | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ w | u8 | `v>220` |  |

Checked on the game 1.03 and 1.04 saves that have more than one item (2 and 6 items): the count equals the number of `itemData` children and the later headers are in the gaps. The first read of this node said "one array of elements"; that fitted only because nearly every instance has one item. The loader is `0x14073c9cc` (the instance body is `0x14073cc68`).


## ItemDropStorageManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| count | u32 |  | number of child nodes |

**Data after the children:**

| Field | Type | When | Notes |
|---|---|---|---|
| trailer | u32 |  | written after the child nodes; 0 in every save seen |


## LevelAssignmentSystem_Ver1

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| level | f32 |  | probably the level |
| names | array, varint count |  | 0 in every save seen; followed by the same number of u16 values (untested). Element: 8 bytes. |


## LevelGainedAggregator

Status: **verified**. 5 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| levels | parallel arrays, varint count |  | n x u32 levels, then n x f32 values (looks like street-cred or XP thresholds). One count, then each column in turn: 4 bytes, 4 bytes. |
| last | u32 |  |  |


## LocalPlayer

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| classHash | u32 |  | 0x15982adf in every save seen |
| size | u32 |  | 28 in every save seen |
| state | 32 bytes |  | 8 x f32 probably: a position and an orientation |


## LootManager_PersistentTables

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| tables | array, u32 count |  | node is read from save version 232 on (loader gate) |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ table | u64 |  | CName of a loot table (length<<32 \| FNV-1a-32 of the name) |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ items | array, varint count |  | CNames of items. Element: u64. |


## LootManager_PlaythroughSeed

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| seed | u32 |  | differs per save |


## MappinSystem_CustomPos

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| x | f32 |  |  |
| y | f32 |  |  |
| z | f32 |  |  |


## MappinSystem_DelamainMappin

Status: **verified**. 21 instances in versions 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| hash | u64 |  | 0 in the 277 save |


## MappinSystem_POI

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| pois | parallel arrays, u32 count |  | two parallel arrays after one count: n x u32 hashes, then n x 6 bytes (meaning of the 6 bytes unknown). One count, then each column in turn: 4 bytes, 6 bytes. |


## MappinSystem_SavedStealthMappinData

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| mappins | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ state | u8 |  |  |


## MappinSystem_TrackedMappin

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| hash | u64 |  | 0 in most saves |


## MappinSystem_UnlockedLocations

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| count | u32 |  | 0 in every save seen; element layout not traced |


## MappinSystemRefactored

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| mappins | parallel arrays, u32 count |  | two parallel arrays after one count: n x u32 hashes, then n x u8 flags (the second array is absent when n is 0). One count, then each column in turn: 4 bytes, 1 bytes. |


## ModdingSystem

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| value | u8 |  | 0 in every save seen |


## MountingStorage

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| mounts | parallel arrays, u32 count |  | three parallel arrays of n x u64, one after the other. One count, then each column in turn: 8 bytes, 8 bytes, 8 bytes. |


## MusicSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| current | string (varint length) |  | "None" when no music state is active |
| active | group | `current!='None'` | only when current is not None |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ trigger | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ stopTrigger | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ event | string (varint length) |  | e.g. music_activated |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ extra | u32 |  | the game always writes 0 here |


## nodeInstanceDataManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| nodes | array, u32 count |  | 16 bytes each; node is read from save version 20 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u16 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u16 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ hash | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |


## ObjectCarrySystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| carried | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ transform | 32 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ legacyName | string (varint length) | `v<=199` | "None"; only up to save version 199 |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ legacyCName | u64 | `v<=199` | only up to save version 199 |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ legacyFlag | u8 | `v<=199` | only up to save version 199 |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ legacyValue | u32 | `v<=199` | only up to save version 199 |
| objects | array, u32 count |  | kind 1, 2 and 3 carry a 32-byte transform block; other kinds carry nothing more |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 1 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ transform | 32 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 2 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ transform | 32 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 3 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ transform | 32 bytes |  |  |


## PathInstance_PlayerTracked_v1

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## PathInstance_Quest_v1

Status: **verified**. 23 instances in versions 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## PhantomEntity

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| entries | array, u32 count |  | 0 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | CName (u64, or string if v<193) |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 |  |  |


## PhoneManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| state | 20 bytes | `v<201` |  |
| state | 32 bytes | `201<=v<207` |  |
| state | 40 bytes | `207<=v<243` |  |
| state | 48 bytes | `243<=v<277` |  |
| state | 56 bytes | `v>=277` |  |
| contact | string (varint length) |  | e.g. #holocalls_studio; empty in the 277 saves |
| extra | string (varint length) | `v>136` |  |


## PhotoMode_BugFixing

Status: **verified**. 20 instances in versions 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sectionA | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ flags | array, u32 count |  | empty in every save seen. Element: u8. |
| sectionB | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ flags | array, u32 count |  | empty in every save seen. Element: u8. |


## PhotoMode_LightSettings

Status: **verified**. 21 instances in versions 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sectionA | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list1 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | 40 bytes |  | ten u32/f32 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list2 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  | size of this field was not recoverable; 4 assumed |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | 20 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ y | u32 |  |  |
| sectionB | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list1 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | 40 bytes |  | ten u32/f32 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ g | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list2 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  | size of this field was not recoverable; 4 assumed |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | 20 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ f | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ x | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ y | u32 |  |  |


## PhotoMode_NewStickersEditor

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sets | array, u32 count |  | 3 in every save; the game writes the count as the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ values | array, varint count |  | . Element: 4 bytes. |
| sets | array, u32 count |  | 3 in every save; the game writes the count as the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ values | array, varint count |  | . Element: 4 bytes. |


## PhotoMode_OutfitWeather

Status: **verified**. 21 instances in versions 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sectionA | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list1 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  | size of this field was not recoverable; 4 assumed |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v1 | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v2 | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v3 | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list2 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |
| sectionB | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list1 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  | size of this field was not recoverable; 4 assumed |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v1 | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v2 | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v3 | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ list2 | array, u32 count |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ v | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u32 |  |  |


## PhotoMode_PresetsBugFixing

Status: **verified**. 20 instances in versions 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sectionA | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | 12 bytes |  | 3 x f32 probably |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | 12 bytes |  |  |
| sectionB | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | 12 bytes |  | 3 x f32 probably |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | 12 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | 12 bytes |  |  |


## PhotoMode_QuestRequest

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| requests | array, u32 count |  | 0 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |


## PhotoMode_Settings

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sectionA | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | array, varint count |  | empty in every save seen. Element: u32. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | array, varint count |  | empty in every save seen. Element: 16 bytes. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ id | u32 |  | -1 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ mode | string (varint length) |  | "None" in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ params | 28 bytes |  | seven u32/f32, zero in every save seen |
| sectionB | group |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ entries | array, u32 count |  | the game writes the constant 3 |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  | empty in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | array, varint count |  | empty in every save seen. Element: u32. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | array, varint count |  | empty in every save seen. Element: 16 bytes. |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ id | u32 |  | -1 in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ mode | string (varint length) |  | "None" in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ params | 28 bytes |  | seven u32/f32, zero in every save seen |


## photoModeSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| ids | array, varint count |  | u64 ids. Element: 8 bytes. |


## playerObjectListenerMapper

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| listeners | array, u32 count |  | node is read from save version 164 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ hash | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  | 1 in the samples seen |


## ProgressAggregator_v1

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| id | string (varint length) |  | 16 hex digits |


## ProgressAggregator_v2

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| buildStamp | string (varint length) |  | game build, e.g. "3.0.5813403  P4CL: 13555055  Stream: //R6.Root/R6.Release " |


## ProgressAggregator_v3

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| id | string (varint length) |  | quest-progress string such as q001_jackie_talk; empty in the 193 save |


## ProgressAggregator_v5

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | string (varint length) |  | empty in the 277 saves |
| b | string (varint length) |  | 16 hex digits |


## ProgressAggregator_v6

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| value | u8 |  | 0 in every save seen |


## questContentSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | u32 |  |  |
| b | f32 |  |  |
| ids | array, u32 count |  | u64 ids. The loader has a second path below save version 204 with 16-byte elements, but the 193 save already uses 8 bytes, so that path is for older saves that are not tested. Element: 8 bytes. |
| records | array, u32 count |  | 24-byte elements. Element: 24 bytes. |
| c | u32 | `v>85` |  |


## questDebugLogManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| log | string (varint length) |  | text log of quest events, one line per event, the newest last; the game keeps it for debugging |


## QuestMusicHistory

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| entries | array, u32 count |  | element is 32 raw bytes, and part of it looks like uninitialised memory (pointer fragments), so it is not stable between saves; empty before 277. Element: 32 bytes. |


## questPhaseFreezer

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| data | u32 size + bytes |  | u32 size then an opaque inner structure; node is read from save version 135 on |


## questPrefabsHandler

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| prefabs | array, u32 count |  | node is read from save version 22 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ hash | 12 bytes |  | the same 12 bytes in every element of every save seen |


## QuestProgressedAggregator

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| legacy | u32 | `v<218` | only present before save version 218 |
| value | f32 |  |  |
| quests | array, varint count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | string (varint length) |  | e.g. q004 |


## QuestProgressedAggregator_v2

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| quests | array, varint count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | string (varint length) |  | e.g. ma_std_rcr_01 |


## QuestProgressedAggregator_v3

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| entries | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | string (varint length) |  | e.g. q001_active, "q001_wakako_street_cred reward" |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ value | f32 |  |  |


## questSceneDataHelper

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| data | u32 size + bytes |  | u32 size then an opaque inner structure (see the inner layout notes) |


## questSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## QueuedData

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | array, varint count |  | empty in every save seen. Element: u64. |
| b | array, u32 count |  | empty in every save seen. Element: u32. |


## RadioSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| station | string (varint length) |  | name of the current radio station, or None |


## ReactionSystem

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | array, varint count |  | 32-byte elements; empty in every save seen. Element: 32 bytes. |
| b | array, varint count |  | u64 ids. Element: 8 bytes. |
| c | array, varint count |  | 16-byte elements; empty in every save seen. Element: 16 bytes. |


## ReactionSystem_v2

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| records | array, varint count |  | 0 in every save seen; element size taken from the game code, not tested. Element: 16 bytes. |


## scanningController

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| saveData | u32 size + bytes |  | a serialised gameScanningControllerSaveData object (u32 size, then the bytes); its class name is visible inside |
| value | f32 | `v>=227` | 1.0 in every save that has it |
| name | string (varint length) | `v>=228` | "None" in every save that has it |


## SceneSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## StatsDataSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| value | u32 |  | 3 in every save |


## TelemetrySystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## Timers

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| timers | array, u32 count |  | timers scheduled by the game; node is read from save version 21 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u8 |  | 1 in every save seen; kind 2 exists in the game code with another layout |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 1 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name1 | string (varint length) |  | "None" in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name2 | string (varint length) |  | "None" in every save seen |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ d | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ e | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ otherwise |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ (undecoded) | bytes |  | unknown kind |


## TimeSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id (u32) and child nodes.

**Data after the children:**

| Field | Type | When | Notes |
|---|---|---|---|
| gameTime | u64 |  | f64 seconds (looks like it) |
| flag | u8 |  |  |
| clock | u64 |  | f64 seconds (looks like it) |


## TransformAnimator

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Holds only the node id.

Holds only the node id (u32) and child nodes.


## TutorialManager

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| ids | array, u32 count |  | . Element: 4 bytes. |


## UIGameControllersState

Status: **partial**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| controllers | array, u32 count |  | kind 1 and 2 objects are 8 and 4 bytes when nothing is queued |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ kind | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 1 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ data | 8 bytes |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 2 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ queued | u32 |  | number of queued notifications; in the 261 saves 4 queued notifications follow in a notification-specific layout that is not decoded |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ (undecoded) | bytes | `queued>0` | notification data |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ if `kind` == 3 |  |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ widgets | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  | HUD widget name |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ state | u32 |  |  |

Undecoded: 1224 bytes in 2 instances.


## UniqueItemCounter

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| counter | u16 |  |  |


## VehicleSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| value | u8 |  | 1 in every save seen |


## WardrobeSystem

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| items | array, u32 count |  | node is read from save version 224 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ slot | string (varint length) |  | e.g. l1_formal_04_basic_03_ |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  | TweakDBID of the item |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ rngSeed | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ b | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 | `v>220` |  |


## WardrobeSystem_ClothingSets

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| sets | array, varint count |  | node is read from save version 224 on |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ slots | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ slot | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ tweakDbId | u64 |  | TweakDBID of the item |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ rngSeed | u32 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ a | u8 | `v>96` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ b | u16 | `v>189` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;↳ c | u8 | `v>220` |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | u64 |  | CName of the set (0 when unnamed) |
| extra | u32 |  | trailing u32 of unknown meaning, zero in the saves seen |


## WeatherWorldState

Status: **verified**. 23 instances in versions 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| a | u32 |  |  |
| b | u32 |  | -2 in the 277 saves |
| c | u8 |  |  |
| d | u32 |  |  |
| e | u32 |  |  |
| p1 | u64 |  |  |
| p2 | u64 |  |  |
| p3 | u64 |  |  |
| p4 | u64 |  | four 8-byte values that look like f64 coordinates (unconfirmed) |
| f | u32 |  |  |
| weatherState | string (varint length) |  | e.g. q307_cutscene_sunny, q304_wake, q001_scav_chase |
| g1 | u32 |  |  |
| g2 | u32 |  | 1.0 in the saves seen |
| g3 | u32 |  |  |
| g4 | u32 |  |  |
| h | u32 |  |  |
| other | string (varint length) |  | e.g. "Runt..." in some saves (second weather name) |
| i1 | u32 |  |  |
| i2 | u32 |  |  |
| i3 | u32 |  |  |
| i4 | u32 |  |  |
| j1 | u32 |  |  |
| j2 | u32 |  |  |


## WorkspotInstancesSavedata

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

| Field | Type | When | Notes |
|---|---|---|---|
| workspots | array, u32 count |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ id | u64 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ flag | u8 |  |  |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ hash | u64 | `v>=191` | read from save version 191 on |


## WorldStateSystem

Status: **verified**. 24 instances in versions 193, 261, 269, 277.

Own data of the node (before the child nodes). Layout from game code function 1409bcdec. The gate v>=10 is from the loader and is not tested locally.

| Field | Type | When | Notes |
|---|---|---|---|
| namedFlags | parallel arrays, varint count | `v>=10` | names, then one byte per name. One count, then each column in turn: struct, 1 bytes. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ name | string (varint length) |  |  |
| hashFlags | parallel arrays, varint count | `v>=10` | u64 hashes, then one byte per hash. One count, then each column in turn: 8 bytes, 1 bytes. |
| hashLists | parallel arrays, varint count | `v>=10` | u64 hashes, then for each hash a varint count and that many 16-byte values. One count, then each column in turn: 8 bytes, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ values | array, varint count |  | 16 bytes each. Element: 16 bytes. |
| hashLists2 | parallel arrays, varint count | `v>=10` | same shape as the previous group. One count, then each column in turn: 8 bytes, struct. |
| &nbsp;&nbsp;&nbsp;&nbsp;↳ values | array, varint count |  | 16 bytes each. Element: 16 bytes. |


## PSData (save version 193 only)

A system blob, not a node spec. Fully decoded and verified on the 193 save: see [PSData in save-system-blobs.md](save-system-blobs.md#psdata-193-only).

## instances

A large nested node with its own document: [save-node-instances.md](save-node-instances.md). Verified on all 24 saves.

## Not yet decoded

None of the 118 nodes remains undecoded. Some parts of some layouts are untested; they are marked in each section.
