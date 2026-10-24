# Credits

RelicWriter is an unofficial fan project. It stands on the work of the people and projects below.

**Lead developer:** [hendy643](https://github.com/hendy643), leading a team of one human and two AIs (Claude and GitHub Copilot, below).

## CD PROJEKT RED

Cyberpunk 2077 and Phantom Liberty are made by [CD PROJEKT RED](https://www.cdprojektred.com/). Cyberpunk 2077 and Phantom Liberty are trademarks of CD PROJEKT S.A. RelicWriter is not affiliated with or endorsed by CD PROJEKT S.A. or CD PROJEKT RED.

- The save format, record names, item, vehicle and perk names, perk costs and perk requirements are the game's own data. The game database in RelicWriter (`Database/*.sql`, `gamedata.db`) holds text and numbers read from the game's files; they belong to CD PROJEKT RED and are not covered by RelicWriter's license.
- Vehicle pictures are cut at run time from the player's own installed copy of the game (Windows only). Nothing from the game's files is bundled for that.
- The archive reader uses the Oodle decompressor that ships with the player's installed game. It is not included in RelicWriter.

## AI assistants

- **Claude** (Claude Code, by [Anthropic](https://www.anthropic.com/)) wrote most of the code, tests and documentation, working under the direction of the repository owner. See the AI disclosure in the README.
- **GitHub Copilot** (the Copilot coding agent) made the Flatpak build script executable so the Linux publish job in CI could run it ([pull request #3](https://github.com/hendy643/RelicWriter/pull/3)).

## Save-format research

Facts about the save format were learned from these projects and checked against real saves; their code was not copied (see `references.md`). The data files below are the exception, and are used under their MIT licenses.

| Project | License | What it gave us |
|---|---|---|
| [CyberCAT](https://github.com/WolvenKit/CyberCAT) (Copyright (c) 2020 Mathies Föls) | MIT | Header, chunk and node table layouts; `Names.json` item names; the sub-inventory ids (`SubInventoryTabItem`) |
| [CyberpunkSaveEditor](https://github.com/PixelRick/CyberpunkSaveEditor) (Copyright (c) 2020 PixelRick) | MIT | Version gates per node, node handling; `TweakDBIDs.json` record names |
| [CyberCAT-SimpleGUI](https://github.com/Deweh/CyberCAT-SimpleGUI) (Copyright (c) 2021 DeweySalt) | MIT | Reference for how a save editor loads and edits saves |
| CP2077SaveEditor | MIT | The vehicle list (`Vehicles.json`) |
| [WolvenKit](https://github.com/WolvenKit/WolvenKit) | GPL-3.0 | Read for the node name list and the game version table only; none of its code is used in RelicWriter |
| [Red Modding wiki](https://wiki.redmodding.org/) | | Container overview of the save file |
| [Ghidra](https://ghidra-sre.org/) | Apache-2.0 | Reading the game's own save loaders |

## Libraries

| Library | License | Used for |
|---|---|---|
| [Avalonia](https://avaloniaui.net/) (UI, Fluent theme, Desktop) | MIT | The user interface |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | View models and commands |
| [K4os.Compression.LZ4](https://github.com/MiloszKrajewski/K4os.Compression.LZ4) | MIT | Reading and writing the save's compressed chunks |
| [BCnEncoder.Net](https://github.com/Nominom/BCnEncoder.NET) | MIT | Decoding the game's block-compressed textures |
| [Entity Framework Core](https://github.com/dotnet/efcore) and SQLite | MIT, public domain | The game database |
| [xUnit.net](https://xunit.net/) | Apache-2.0 | The tests |

## Fonts

- **Rajdhani**, by Indian Type Foundry, SIL Open Font License 1.1 (`RelicWriter/Assets/Fonts/OFL.txt`).
- **Inter**, by Rasmus Andersson, SIL Open Font License 1.1 (through Avalonia.Fonts.Inter).

## Packaging and build

- [WiX Toolset](https://wixtoolset.org/) builds the Windows installer.
- [Flatpak](https://flatpak.org/), [flatpak-builder](https://github.com/flatpak/flatpak-builder) and the freedesktop runtime build the Linux package.
- GitHub Actions and the actions `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`, `actions/download-artifact`, `flatpak/flatpak-github-actions` and `softprops/action-gh-release` run the builds and releases.
- [.NET](https://dotnet.microsoft.com/) by Microsoft.

## Thanks

To everyone who has documented the Cyberpunk 2077 save format and modding internals over the years, and to the players who keep the game alive.
