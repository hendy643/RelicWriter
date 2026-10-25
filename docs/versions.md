# Versions

## Versions

`version.json` in the repository root is the only place versions are edited:

```json
{ "app": "0.1.0", "database": 277, "components": { "RelicWriter.Desktop": "0.1.0", "RelicWriter.Common": "0.1.0", ... } }
```

- `app` is the application version. It is what the app shows (`AppVersion`).
- `components` has one version per project, by project name. A project not listed takes the `app` version. These are the assembly versions of each DLL/exe.
- `database` is the save version the game database was designed for. The Extractor sets it when it writes a migration, and a test checks it equals the newest save version the built-in data covers (`AppVersion.DatabaseVersion`).

**Pre-build sync.** `Directory.Build.targets` runs the `SyncVersion` task before every build of every project. It writes the project's version into its own project file (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`, added if missing) and into any `*.manifest` / `*.appxmanifest` beside it (`assemblyIdentity version`, `Identity Version`; today only `RelicWriter.Desktop/app.manifest`), and only when a value differs. The same values are used for that build, so the DLLs are right even in the build that changed the files, and the app and database versions are added to each assembly's metadata. A component version may carry a pre-release suffix (`0.2.0-beta`); assembly and manifest versions use the `x.y.z.0` part. Expect the IDE to offer to reload the project files after you change `version.json`.

**Shown version.** The About tab and the header show `AppVersion.DisplayText`: the plain version for a formal release, and `version+hash` (the short hash of the current commit, from `git rev-parse --short HEAD`, written into the assembly metadata as `GitHash` by `Directory.Build.targets`) for every other build. The Release workflow sets `RELICWRITER_RELEASE=true`, which leaves the hash out; a build outside a git checkout has no hash either. The About tab's change log is `CHANGELOG.md` in the repository root. The `GenerateChangelog` task in `RelicWriter.Common.csproj` turns it into `ChangelogData.Entries` (`obj/Changelog.g.cs`) on every build, so the app holds no copy of the file (`## heading` then `- line` per change). Add each change under `## Unreleased` as it is made, and `packaging/bump-version.ps1` renames that heading to `## <version> · <date>` when a release is prepared (it refuses if there is none).

There is no self-updater: new versions are found on Nexus Mods or the GitHub releases page.
