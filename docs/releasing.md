# Building and releasing (GitHub Actions)

Two workflows in `.github/workflows/`: `build.yml` and `release.yml`. Neither has run on GitHub yet; the files were only checked with `actionlint`.

## Protecting master

Two repository rulesets (Settings, Rules) apply to the default branch, `master`, and were created with the GitHub API:

- **master: pull request with passing build and tests** (no bypass for anyone): `master` can only change through a pull request, and the Build workflow's checks `build (windows-latest)` and `build (ubuntu-latest)` (build plus tests) must pass first. No direct pushes, force pushes or deletion, for the owner too. No approving review is required, because the owner is the only person who could give one.
- **master: only owner and maintainers can merge**: only the Admin and Maintain roles may update `master`, which is what merging a pull request does. Anyone else with write access can open pull requests and push branches but cannot merge.

Nothing bypasses these rules, and no workflow pushes to `master`: a release is made by merging a pull request (below). Bootstrapping: the first pull request, which adds the Build workflow, cannot satisfy a check that does not exist yet, so the first rule needs to be set to "Disabled" (Settings, Rules, Rulesets) while that change is pushed, then set back to "Active".

## Build (`build.yml`)

On every pull request (and on nothing else): build `RelicWriter.slnx` in Release and run the tests, on Windows and on Linux. It then fails if the build changed any project file or manifest (`git diff --exit-code`): the build writes `version.json`'s numbers into them, so a change means they were committed out of date.

## Releasing

1. **Bump the version on a release branch.** Create a branch such as `release/0.2.0`, run `powershell -File packaging/bump-version.ps1 0.2.0` (or `0.2.0-beta.1`), commit what it changed (`version.json`, the project files and manifests, the Flatpak metainfo) and open a pull request to `master`. The Build check runs on it.
2. **Merge it** (the owner or a maintainer). The change to `version.json` on `master` starts the Release workflow, which builds and publishes. A version with a suffix is published as a pre-release; the updater only looks at the latest full release, and a pre-release counts as lower than the release it leads up to (see `docs/updates.md`).

The branch name is only a convention: the version in `version.json` decides what is released. `powershell -File packaging/release.ps1 1.0.0` does both steps and the back-merge into `develop` with the `gh` CLI (branch, bump, pull request into `master`, wait for the checks, merge with `--admin`, then the same into `develop`). It is untested against GitHub.

A merge that changes `version.json` without a new version (the `database` number, say) does nothing, because the tag already exists. A failed release is started again from Actions, Release, Run workflow on `master`; it reads the version from `version.json` and refuses if the tag exists.

Who may run it: the **prepare** job asks GitHub for the role of the person who started the run and stops unless it is `admin` (the repository owner is one) or `maintain` (a maintainer), and unless the run is on `master`. Merging is already limited to those roles by the ruleset, and a run started by hand by anyone else fails at once. Every other job depends on that job. (Setting the role "maintain" is done in the repository's Settings, Collaborators and teams.) There is no approval step: the merge is the decision. The `release` environment on GitHub is no longer used and can be deleted (Settings, Environments).

What the run does:

0. **check**: reads the version from `version.json` and stops quietly if its tag exists (a merge that is not a release).
1. **prepare** (Windows): checks the role and that the metainfo has a release entry for the version, builds, runs the tests and checks that the project files agree with `version.json`.
2. **windows**: `dotnet build RelicWriter.Installer -c Release` (self-contained, trimmed, ReadyToRun MSI, see `docs/installer.md`).
3. **linux-publish** and **flatpak**: the app is published for `linux-x64` (`packaging/flatpak/build.sh --publish-only`), then packed into `RelicWriter.flatpak` by the Flatpak builder action in the Flathub CI image (see `docs/flatpak.md`).
4. **publish**: creates the GitHub release and its tag `vX.Y.Z` on that commit, with `RelicWriter-<version>-win-x64.msi`, `RelicWriter-<version>-linux-x86_64.flatpak` and `version.json`. The app's updater needs that `version.json` among the release's files, and takes the first `.msi`.

The `database` number in `version.json` is not touched by a release: it follows the save version the game data was built for (the Extractor's `gamedata` command writes it).

## Needs and risks

- The workflow never commits. Nothing exists on GitHub until the last job creates the tag and release, so a failed run can simply be started again.
- Two releases at once are prevented (`concurrency`), and a tag that exists is refused.
- The Flatpak action, action versions (`@v4`, `@v6`, `@v2`) and the Flathub CI image tag are as of writing; the Flatpak build there is unproven (it was built in WSL with `build.sh`, not with that action).
- The installer is not signed, so Windows SmartScreen may warn.
- The rulesets are repository settings, not files; they are not in the repository and must be recreated if the repository moves.
