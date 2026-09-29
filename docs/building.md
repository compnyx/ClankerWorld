---
title: Build and Test the Current Prototype
type: development-reference
status: active
updated: 2026-09-29
---

# Build and Test the Current Prototype

The current prototype uses **C# 14 on .NET 10 LTS** for the authoritative, headless
simulation kernel. The exact SDK baseline is `10.0.401`, selected by
[`global.json`](../global.json); patch updates within that feature band are
permitted. .NET 10 is an LTS release supported through November 2028.

This choice concerns the world brain, not the player-facing renderer. Godot is
the intended player-facing client and may share C# with the core, but it must
never become a dependency of the authoritative simulation project. The first
Godot observer uses Godot 4.7.2's .NET project SDK and targets `net8.0`, the
desktop script target supported by that engine. That target is intentionally
local to the client; the authoritative projects remain on .NET 10.

The first end-user export target is **Windows 11 x64**. The repository now has
an unsigned portable-export path and a CI artifact configuration for that
target; the Godot editor remains a development-only tool. This is not an
installer choice, code-signing provider, or public release. The separate
Windows smoke test and paired reconnect were completed on 2026-09-21.

The export check proves that a reproducible bundle is produced, not that a
person can use it on Windows; it does not substitute for the completed Windows
11 x64 smoke test or for final product playtesting. The tester needs the
portable bundle and Tailnet reachability, not the Godot editor.

## Project boundary

```text
src/ClankerWorld.Simulation/        pure authoritative simulation library
src/ClankerWorld.Viewer/            separate ASP.NET Core observation host
src/ClankerWorld.GodotClient/       separate Godot paired-owner projection/request client
tests/ClankerWorld.Simulation.Tests/ deterministic, replay, and viewer-contract tests
```

`ClankerWorld.Simulation` must remain runnable and testable without Godot, a
window manager, an LLM provider, or a network connection after dependencies are
restored. The headless HTTP host references the simulation, never the reverse,
and projects its own protocol DTOs. The Godot client does not reference the
simulation; it uses the headless host's versioned paired-owner HTTP contract.
It signs requests with a device key, but every observation, pause/resume,
instruction, and authoring result still comes from server validation and commit.
No client mutates world state directly.

The Windows client uses a non-exportable current-user CNG P-256 device key for
normal pairing. Its saved registration contains only non-secret metadata; the
private key is never bundled into an export or written into a world save. The
full bootstrap, revocation, and transport policy lives in
[current private-host device pairing](pairing.md).

## Test and dependency policy

- **xUnit** is the current test framework.
- `Microsoft.NET.Test.Sdk` runs tests through `dotnet test`.
- NuGet lock files are committed for every project with external packages.
  Routine verification uses `--locked-mode`, so dependency changes are
  deliberate reviewable diffs rather than surprise downloads.
- Shared compiler, nullability, warning, analyzer, deterministic-build, and
  prerelease-version settings live in
  [`Directory.Build.props`](../Directory.Build.props). Its `VersionPrefix`
  and `VersionSuffix` are the single source for future .NET package/runtime
  metadata; do not add a hand-maintained version constant.

## Reproducibility proof

After `dotnet restore --locked-mode`, run `dotnet format --no-restore` before
committing C# changes to apply the repository formatting rules. Then run
`dotnet format --verify-no-changes --no-restore` to check that no formatting
changes remain.

On a fresh clone with the selected SDK:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
bash scripts/verify-godot-client.sh
bash scripts/verify-godot-windows-export.sh
```

`verify-godot-client.sh` downloads the exact Godot 4.7.2 .NET engine archive,
checks its SHA-256, builds the C# scripts, and starts the scene headlessly.
`verify-godot-windows-export.sh` separately verifies the pinned editor and
export-template archives, emits an unsigned Windows x64 PE bundle, and writes a
SHA-256 manifest. It proves export reproducibility on the build host; it does
not substitute for running the bundle on Windows 11. GitHub Actions is
configured to run these checks and upload the Windows bundle as an artifact for
pushes and pull requests using the pinned .NET 10 SDK/runtime host.

## Windows documentation checks

The `windows-documentation` CI job restores the locked test dependencies using
`global.json` and runs the documentation checks on Windows. Front-matter checks
exercise both LF and CRLF text, so a valid Windows checkout does not fail merely
because Git converted line endings. This focused job is not a Windows Godot
playtest or a claim that the complete runtime suite runs on Windows.

## Windows paired-world verification record

Use this protocol for [#285](https://github.com/compoodment/ClankerWorld/issues/285).
It is a checklist for collecting evidence, **not a report of completed playtests**.
Source and headless checks do not fill the Windows result column. Do not deploy,
repair accounting, alter credentials or modify the active playtest save merely
to run this checklist: obtain the separate operational authorization first.
Use a disposable world for setup, placement and damaged-save cases.

Record client commit/export digest, host commit, Windows build, date, viewport,
render resolution and UI scale. For each case record Pass/Fail/Blocked, exact
steps, observed result, and a screenshot or bounded diagnostic reference with
private keys, pairing codes, thoughts and raw provider payloads excluded. Keep
failures linked to their focused issue; a successful unrelated case cannot close
an entire multi-case report. Keep live operational evidence separate from client
presentation evidence when the deployed server predates the client.

| Case | Reproduction and pass condition |
| --- | --- |
| Hosted decisions and accounting | After the separately authorized meter migration, explicitly resume with an authenticated client. Confirm each configured founder can receive an accepted model choice and a new private thought; note waiting, limit or provider errors without claiming every idle action is a failure. Pause afterward. Reconcile the known phantom reservations separately; do not count them as paid calls or erase them as part of this UI check. |
| Fresh Town, no legacy camp | Create a disposable fresh world, inspect before site choice, accept a Town site and inspect again. No legacy camp objects or camp-derived border should appear; exactly the accepted generated layout should remain. |
| Stable Town and agent text | Pause, open Town, wait across several refreshes, switch agents and reselect the first. Contents, relationships and existing thought/memory text stay visible; an unchanged refresh must not clear them. |
| Agent card at 720p | Select and Find agents near every screen edge at 1280×720. The selected marker remains visible and Speak/Send controls are reachable by scrolling, without controls extending off-screen. |
| Status lifetime | Enter Town-site and founder-move modes and wait through refreshes. Instructions remain while the mode is active; submit a refused request and confirm its explanation remains readable without a false disconnect. |
| Initial camera | Open a new unset world and a saved Town near a wrapping seam. The initial view shows relevant dry land/Town/agents, not an arbitrary open-sea center or the wrong side of a seam. |
| Tile card and short labels | Select ground near the bottom edge; inspect at several zooms. The complete card fits, absent facts are omitted, and tiny marker labels disappear rather than render fragments. |
| Modal input | Open Pause Menu and try top-bar Start/Pause/founder actions. The modal blocks them. In Town-site mode confirm Cancel is visible; inspect pairing/back controls and Settings caption alignment. |
| Keyboard focus | Click a top-bar button, then pan with arrow keys and use Space. Arrows pan rather than cycle focus, Space toggles pause once, Tab/Enter still reach controls. Evaluate diagonal movement while held, and compare the F1 list with actual shortcuts. |
| Disconnected land | In a disposable world add an adult on land without a food route to the Town, then reconnect. Observation remains available and reports the missing route; Main Menu still accepts pause even if a later refresh fails. |
| Preview while running | With a disposable current world running, request New World preview. Preview succeeds without changing current-world identity/state; Create still performs its separate pause/switch flow. |
| Main Menu Settings pointer | From Main Menu open Settings and change an installation preference with the mouse. The overlay must not swallow input. Restore the preference after recording the result. |
| Hover and marker priority | At several zooms hover bare ground and multiple same-tile agents. Ground outline is visible, each agent is individually selectable, and nearby tiles do not activate its marker. |
| No Main Menu World Settings | With no loaded world, open Main Menu Settings. No World Settings category or hidden navigation path enters a world. |
| Terrain seams | Inspect contiguous terrain at representative zooms. No black tile-gap grid appears; this does not approve provisional textures as final art. |
| Hover and condition stability | Hold the pointer over an agent through several observations while its card is open. Tooltip and warmth/illness/diet/equipment stay visible without per-refresh flicker. |
| Slow provider | Using a separately authorized controlled delay or an unavailable test endpoint, keep the client connected while a model remains pending. Other agents/world systems continue; pause and reload must reject the old reply. Never prolong real paid calls solely to create this test. |

**Exit criterion:** each applicable row has evidence against the stated builds.
Blocked live migration or missing Windows access remains Blocked, not Pass. The
owner's review of this protocol does not approve a deployment or certify the build.
