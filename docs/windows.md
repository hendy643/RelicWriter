# Windows build

`packaging/windows/build.sh` makes `packaging/windows/publish/RelicWriter.exe`, the file a release publishes for Windows, zipped as `RelicWriter-<version>-win-x64.zip` by the release workflow. There is no installer: the exe is the whole app, and nothing needs to be installed (the .NET runtime is inside it).

- It publishes `RelicWriter.Desktop` for `win-x64`, self-contained, as `PublishSingleFile` with the native libraries inside (`IncludeNativeLibrariesForSelfExtract`: extracted to a temp folder on first run) and compression, `PublishReadyToRun` (precompiled native code, faster start) and `PublishTrimmed` with `TrimMode=partial` (unused code in the .NET libraries is removed; the app's own and the NuGet packages' assemblies are left whole). Every `.pdb` is deleted afterwards: the native Skia and HarfBuzz ones are copied from their NuGet packages whatever the symbol switches say. The result is about 49 MB.
- Not done: NativeAOT, which cannot work here (the UI binds by reflection with `{Binding}`, `ViewLocator` and Entity Framework Core use reflection, and it cannot be cross-compiled from Linux), and `TrimMode=full` for the same reasons.
- The game database (`%LocalAppData%\RelicWriter\gamedata.db`) and the remembered game folder are the app's only files outside the exe. Deleting the exe removes the app; the data folder is left.
- The version is `"app"` in `version.json` (see `versions.md`); the exe's file version is the Desktop project's.

## Not tested

- The exe was published from Linux and started under Wine with a save (it kept running; no window was seen). Trimming can still leave out a framework member that only a rarely used tab needs: if so, add it to a `TrimmerRootDescriptor` or delete the `PublishTrimmed` switches in `packaging/windows/build.sh`. The release workflow's Windows job has not run.
- The release workflow signs the exe with our own self-signed certificate once its secrets exist (`signing.md`); it is not trusted by Windows, so SmartScreen still warns. No Start menu entry or file association is made.
- x64 only.
