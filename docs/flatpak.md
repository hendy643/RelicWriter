# Flatpak

`packaging/flatpak/` holds the Flatpak for Linux (app id `io.github.hendy643.RelicWriter`): the manifest (`io.github.hendy643.RelicWriter.yml`), the desktop file, the AppStream metadata and `build.sh`.

`packaging/flatpak/build.sh` (run on Linux; needs the .NET 10 SDK, `flatpak-builder` and the freedesktop 25.08 Platform and Sdk runtimes) publishes `RelicWriter.Desktop` self-contained for `linux-x64`, trimmed (partial, as in `docs/installer.md`) and ReadyToRun, with invariant globalization and no symbols, into `packaging/flatpak/publish`. `flatpak-builder` then packs that folder (the runtime comes with the app, so the app only uses the freedesktop runtime for system libraries such as fontconfig and Mesa), installs it for the current user and writes `packaging/flatpak/RelicWriter.flatpak`. Run it with `flatpak run io.github.hendy643.RelicWriter`.

## The sandbox

The app can use the display (X11 or XWayland; Avalonia has no Wayland backend), OpenGL and the network (the check for a newer release; on Linux an update is only announced), plus these folders, which `SaveLocator` and `GameLocator` look in: `~/.local/share/Steam`, `~/.steam`, the Steam, Lutris and Heroic Flatpaks' folders, `~/.config/lutris` and `~/.config/heroic` (read only) `~/Games`, and the usual mount points of other drives (`/mnt`, `/media`, `/run/media`; read-write, because Steam keeps the saves in the Proton prefix inside the library). A library mounted anywhere else needs `flatpak override --user --filesystem=<dir> io.github.hendy643.RelicWriter`. Saves elsewhere are opened through the file chooser portal. The game database lives in the app's own data folder (`~/.var/app/io.github.hendy643.RelicWriter/data/RelicWriter/gamedata.db`).

## Icons

The icons in `packaging/flatpak/icons/hicolor` (16 to 512 px and the scalable SVG) are made from `RelicWriter/Assets/Icon.svg` by `packaging/icons/make-icons.ps1`, which also writes the Windows `Icon.ico` (16, 24, 32, 48, 64, 128, 256 px) and `Icon.png` used by the app; sizes up to 32 px are drawn without the fine detail. Rerun the script after changing the SVG.

## Not done or not tested

- Built and started once under WSL2 (Ubuntu, Flatpak 1.16, freedesktop 25.08): `build.sh` succeeds (the bundle is about 29 MB), `flatpak run` keeps running for 15 s without errors under WSLg and creates `gamedata.db` in the app's data folder. Not tried: opening a save, finding a Steam or Lutris install (none in that WSL), the file chooser portal, hardware OpenGL, native Linux with Wayland, or installing the `.flatpak` bundle on another machine.
- The manifest packs a folder published beforehand, so it is not suitable for Flathub, which builds from source offline (that would need a generated list of the NuGet packages, such as the one `flatpak-dotnet-generator` makes).
- The metainfo says `LicenseRef-proprietary` because the project has no licence yet, and its release entry (version and date) is written by hand; keep it in line with `version.json`.
- x86_64 only.
