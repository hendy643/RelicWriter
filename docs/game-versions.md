# Game versions and what the editor allows

The editor limits what can be edited by the game patch of the save (`GameEdition` in `RelicWriter.Common/Model`), because the game's own limits changed over time. The patch comes from the game version in the save header; the Save Info tab shows it and can move the save to a later patch.

## Rules

| What | Before 2.0 | 2.0 and later | With Phantom Liberty (2.0 and later) |
|---|---|---|---|
| Level cap | 50 | 50 | 60 |
| Street cred cap | 50 | 50 | 50 |
| Attributes | 3 to 20 | 3 to 20 | 3 to 20 |
| Skills | the 12 old skills, cap 20 | the 5 new skills (Solo, Shinobi, Engineer, Netrunner, Headhunter), cap 60 | plus the Relic skill, cap 60 |
| Perks tab | not available | the 2.x trees | plus the Relic tree and Relic points |

Sources: CD PROJEKT RED's Update 2.0 notes (cyberpunk.net/en/news/49060/update-2-0): "We merged the previous Skills into five new ones ... can be leveled up to a max of 60", the perk trees overhauled and spent perk points refunded, and the 1.5 notes (all perk points reset by the perk rebalance). The level cap of 60 and the Relic tree belonging to the paid expansion is from coverage of CDPR's announcement of what is free in 2.0 and what needs Phantom Liberty; **no CDPR page with those exact words was found**, so treat that row as second hand. The pre-2.0 caps (level 50, skills 20) match every 1.21 to 1.63 save checked. Everything is checked against 105 real saves from 1.21 to 2.31; see [save-versions.md](save-versions.md).

Not covered yet, because no data says which patch or edition added them: vehicles and inventory items that only exist with Phantom Liberty or from a later patch. The game database has no such column.

## Details that came from the saves

- **The save does not record Phantom Liberty.** Every 2.0 and later save carries the Relic skill and points entries, owned or not. `GameEdition.LooksLikePhantomLiberty` guesses it from a level above 50, a Relic skill above 1, Relic points or a bought Relic perk. Otherwise the user ticks it on Save Info. It is not written to the file; it only decides what the editor allows. A save that shows signs of the expansion cannot be unticked.
- **2.x saves keep the 12 old skills** (carried over by the game's own 2.0 conversion) next to the new ones. They are hidden from the editor and left untouched.
- **Values outside the rules are never changed on load.** The maximum is the larger of the rule and the level the save has; the attribute minimum is the smaller of 3 and the save's value (a 2.21 save has attributes at 1).
- **Moving across save versions is not offered, on purpose.** Between neighbouring observed save versions only 0 to 13 of the game's node loaders change layout (counted from the table in [save-versions.md](save-versions.md)), so the byte layout alone would be manageable. The part that matters is that the editor's rules only differ at 2.0, and what 2.0 changed is conversion of the character's data, not file layout: the notes say spent perk points are refunded and old skill progress is carried into the five new skills, and a side quest introduces Cyberware Capacity. The game does this when it loads an older save. A save that RelicWriter marked as 2.x without that conversion would have no new-skill or new-perk data to edit, and the game's own conversion might not run on it. Within 2.x the rules are identical, so a conversion would unlock nothing. For a pre-2.0 save the app says to load it in the current game, save again, and open the new file. Header-only moves are offered between patches with the same save version, because that changes only the game version in the header. Patches with a different save version need the save's data converted; that is not built yet. The save version for a patch comes from a real save of it (`GameEdition.Patches`, null where none has been seen).

## Observed patches

| Save version | Game versions seen |
|---|---|
| 193 | 8 (1.04) |
| 197 | 1210, 1220 (1.21, 1.22) |
| 220 | 1500 (1.5) |
| 224 | 1600, 1610, 1630 (1.6, 1.61, 1.63) |
| 254 | 2000 (2.0) |
| 261 | 2100 (2.1) |
| 263 | 2120 (2.12) |
| 265 | 2210 (2.21) |
| 269 | 2300, 2310 (2.3, 2.31) |
| 277 | 2500 (2.5) |

No saves yet for 1.0, 1.1, 1.3, 1.4, 2.01, 2.2 and 2.4.
