# App updates

## Versions

`version.json` in the repository root is the only place versions are edited:

```json
{ "app": "0.1.0", "database": 277, "components": { "RelicWriter.Desktop": "0.1.0", "RelicWriter.Common": "0.1.0", ... } }
```

- `app` is the application version. It is what the update check compares, and what the app shows (`AppVersion.Current`).
- `components` has one version per project, by project name. A project not listed takes the `app` version. These are the assembly versions of each DLL/exe.
- `database` is the save version the game database was designed for. The Extractor sets it when it writes a migration, and a test checks it equals the newest save version the built-in data covers (`AppVersion.DatabaseVersion`).

**Pre-build sync.** `Directory.Build.targets` runs the `SyncVersion` task before every build of every project. It writes the project's version into its own project file (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`, added if missing) and into any `*.manifest` / `*.appxmanifest` beside it (`assemblyIdentity version`, `Identity Version`; today only `RelicWriter.Desktop/app.manifest`), and only when a value differs. The same values are used for that build, so the DLLs are right even in the build that changed the files, and the app and database versions are added to each assembly's metadata. A component version may carry a pre-release suffix (`0.2.0-beta`); assembly and manifest versions use the `x.y.z.0` part. Expect the IDE to offer to reload the project files after you change `version.json`.

## How an update is found

On start the app checks (in the background, `MainViewModel.StartUpdateCheck`) the **latest GitHub release** of `hendy643/RelicWriter`:

1. The release must have a file named **`version.json`** among its assets (the same file as in the repository root). Without it, the release is ignored.
2. If its `app` version is higher (a pre-release is lower than the same numbers without one, so `1.2.0-rc.1` < `1.2.0`; the order is semver.org's, `SemVer`) than the running version, there is an update.
3. The update is the release's installer for this platform: on Windows the first `.msi`, else the first `.exe`. The size (and the SHA-256, when GitHub gives one in the asset's `digest`) is checked; a download that doesn't match is deleted and never run. A file already downloaded and matching is reused.
4. With **no save open** the installer is started (`.msi` through `msiexec /i … /passive`, `.exe` as it is) and the app closes so it can replace its files. With a save open the app only says the update is ready, and the next start installs it, so unsaved edits are never lost.

Every failure (offline, no release, a bad file, a timeout) is ignored and the app carries on. Set `RELICWRITER_NO_UPDATE` to skip the check; it is also skipped when a debugger is attached.

## Not done

- **Not tested against GitHub.** The release process and installer do not exist yet; the tests use a stand-in for GitHub (`UpdateTests`). The installer file names (`.msi`, `.exe`), the exact flags and whether the installer can replace a running app are assumptions to check once one exists.
- **Linux gets the announcement only** ("RelicWriter x is available: <release page>"); there is no Linux installer format.
- No unsigned-installer protection beyond the size and digest check: sign the installer and verify the signature before relying on this.
- No settings page to turn the check off.
- A manifest or project file that the task can't match (no `PropertyGroup`, an unusual manifest) is left as it is.
- The release process must publish `version.json` with the installer; nothing does that yet.
