# Installer

`RelicWriter.Installer` is a WiX 6 project (`WixToolset.Sdk` 6.0.2, restored from NuGet; not part of the normal solution build). `dotnet build RelicWriter.Installer -c Release` makes `RelicWriter.Installer/bin/Release/RelicWriter.msi`, the file to publish with a release.

- Before it builds, the project runs `dotnet restore` and `dotnet publish` of `RelicWriter.Desktop` (Release, `win-x64`, self-contained, trimmed, ReadyToRun, no symbols) into `RelicWriter.Installer/obj/publish`, and installs that folder's files and nothing else. The .NET runtime is part of the publish output, so nothing is downloaded and no runtime needs to be installed.
- `Package.wxs` installs per user to `%LocalAppData%\Programs\Relic Writer`, with a Start menu shortcut. It never asks for administrator rights (`Scope="perUser"`; nothing is written to `Program Files` or `HKLM`). `MajorUpgrade` replaces older versions; user data (`%LocalAppData%\RelicWriter\gamedata.db`) is left alone on uninstall.
- The version is the `"app"` number in `version.json`, read by the project itself (`SyncVersion` skips WiX projects).

## Release size

The publish uses `PublishTrimmed` with `TrimMode=partial`: the linker removes unused code from the .NET libraries (the runtime's core library goes from 16 MB to 3 MB; the publish folder from about 112 MB to about 61 MB without symbols) but leaves the application's and the NuGet packages' own assemblies whole. Whole-application trimming (`TrimMode=full`) is not used: the UI binds by reflection (`{Binding}`, `ViewLocator`) and Entity Framework Core relies on reflection, neither of which the linker can follow. `InvariantGlobalization` drops the ICU libraries (culture-aware sorting and formatting use the invariant culture; the app has no localised UI). The native symbol files (`*.pdb`, about 105 MB) are left out of the MSI. `PublishReadyToRun` precompiles the assemblies to native code, which makes the app start about twice as fast (the window with a save loaded in 1.3 s instead of 2.9 s on the development machine) and makes the install about 93 MB and the MSI about 34 MB. Not done: removing the Linux and macOS Avalonia assemblies (they are referenced from the platform detection code, so removing them is risky) and single-file publishing.

## Not tested

- The trimmed build was started once with a real save: the window opened, the save loaded and the game database opened. The rest (every tab, the file dialogs, the vehicle pictures) was not exercised, so a trimmed framework member could still be missing somewhere. If one is, add it to a `TrimmerRootDescriptor` or turn trimming off by deleting the `PublishTrimmed` switches in the `.wixproj`.
- The MSI builds and was checked to be per user (no `ALLUSERS`), but no install, upgrade or uninstall has been run,.
- ICE38, ICE60, ICE64 and ICE91 are switched off: they only complain that a per-user install writes to the user profile.
- x64 only. No Linux package yet.
