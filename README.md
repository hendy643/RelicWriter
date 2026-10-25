# RelicWriter

[![Latest release](https://img.shields.io/github/v/release/hendy643/RelicWriter)](https://github.com/hendy643/RelicWriter/releases/latest) [![Build](https://github.com/hendy643/RelicWriter/actions/workflows/build.yml/badge.svg?event=pull_request)](https://github.com/hendy643/RelicWriter/actions/workflows/build.yml)

A save game editor for **Cyberpunk 2077**, written in C# with Avalonia (Windows and Linux).

Open a `sav.dat`, change your character, perks, appearance presets, inventory and vehicles, and save it back. Parts of the save that RelicWriter doesn't understand are written back byte for byte, so unknown data survives an edit.

**Download:** [RelicWriter on Nexus Mods](https://www.nexusmods.com/cyberpunk2077/mods/35763), or the [GitHub releases](https://github.com/hendy643/RelicWriter/releases).

> **Back up your saves before editing them.** This is an unofficial, in-development tool. A bad edit can corrupt a save or break it in game. You use it at your own risk.

## AI disclosure

**This application was built with heavy use of AI.** Most of the code, the tests and the documentation were written by [Claude Code](https://claude.com/claude-code) (Anthropic's coding agent), working under the direction of the repository owner, who set the goals, ran the tool against real saves, and decided what to keep.

> [!NOTE]
> No code was committed without human review, even if it was superficial in some places.

What that means in practice:

- **Code:** the UI, save-file library, code generator, extractor and tests were largely written by an AI model. Treat it like code from a contributor you haven't met: read it before trusting it.
- **Reverse engineering:** the save format notes in `docs/` and `docs/save-spec.xml` are derived from analysing the game's own loaders (for example in Ghidra) and from comparing real saves across game versions. An AI model helped write and organise those notes, and the notes can be wrong. Layouts marked `open` in the spec have not been confirmed.
- **Testing:** the automated tests use synthetic saves built in memory. The AI cannot play the game, so whether an edited save loads and behaves correctly in game has to be checked by a person.
- **Errors:** AI-written code can be confidently wrong. If you find a bug, a wrong claim in the docs or a bad edit, please open an issue.

If you do not want to use AI-assisted software, this project is not for you, and that is a fair choice.

## Features

| Tab | What it does |
|---|---|
| Character | Level, attributes and skills |
| Perks | Level of every perk, with unspent perk and Relic points kept in step |
| Appearance | Face-shape presets (eyes, nose, mouth, jaw). The creator's parts are shown read-only. No 3D preview yet. Skin, hair and makeup can't be changed |
| Inventory | Items, shown by their in-game names |
| Vehicles | Garage, with tile pictures cut from your own game install |
| Save Info | Header and version details |
| Nodes | The raw node tree of the save |
| About | Version, credits and what's new |

Saves from observed save versions 193 to 277 are supported.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows or Linux. Vehicle pictures need an installed copy of the game.

## Build and run

```
dotnet build RelicWriter.slnx
dotnet run --project RelicWriter.Desktop
dotnet run --project RelicWriter.Desktop -- path\to\sav.dat   # open a save at startup
dotnet test RelicWriter.Tests
```

Your saves are normally in `%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077\`.

## Project layout

| Project | Purpose |
|---|---|
| `RelicWriter` | Shared Avalonia UI: views, view models, theme |
| `RelicWriter.Desktop` | Entry point |
| `RelicWriter.Common` | Save container, codecs, model and game database (no UI dependencies) |
| `RelicWriter.Generator` | Generates codecs from `docs/save-spec.xml` |
| `RelicWriter.Extractor` | Updates the spec and game database for new game builds |
| `RelicWriter.GameAssets` | Reads the game's own archives and tweak data (Windows only) |
| `RelicWriter.Model3D` | Work-in-progress character preview library |
| `RelicWriter.Tests` | xunit tests |

The save format is documented in [`docs/`](docs/), starting with [`save-format.md`](docs/save-format.md). `CLAUDE.md` holds the working notes the AI agent reads when changing the code.

## Game content and legal

RelicWriter is an unofficial fan tool. It is not affiliated with or endorsed by CD PROJEKT RED.

*Cyberpunk 2077* and its names, text and assets belong to CD PROJEKT S.A. The game database shipped with the app contains item, vehicle and perk names taken from the game. Pictures are read from your own install at runtime and nothing else from the game is bundled. Redistribution of the database text still needs checking before a public release.

## Contributing

Issues and pull requests are welcome. If you use an AI tool for a contribution, say so in the pull request.

## License

No license has been chosen yet.
