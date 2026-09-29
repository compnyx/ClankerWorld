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

## Proposed local Windows package and authorized deployment workflow (#221, #267)

**Draft operational design. No installer, end-to-end deployment command or live
deployment is delivered by this section.** The Windows client export is not yet
a self-contained authoritative game. The existing host/service template is not
a verified migration/rollback workflow.

### Local Windows distribution recommendation

Prefer an initially bundled companion host using the same authoritative runtime,
not a second simulation implementation. Owner review must select companion versus
embedded lifecycle before release. The launcher should start a single local host
bound only to loopback, wait for a versioned readiness handshake, then open the
client against that instance. It must not discover or default to computment's VPS.
Use a per-user writable data directory separate from versioned binaries, with
world saves, global usage accounting and protected provider configuration in
separate locations. Never put a provider key in launcher arguments, logs, world
files or a package manifest. Current-user DPAPI protection does not make keys
portable between Windows accounts.

Define child-process ownership explicitly: detect an existing compatible local
instance, reject conflicting ports/state locks, and do not kill arbitrary matching
process names. On normal exit, pause/cancel hosted work, finish durable save, then
stop only the launched host. A crash/disconnect must expire presence and stop paid
work even if the companion survives. Update/rollback replaces binaries, not user
data. Installer/portable packaging, runtime bundling and signing remain decisions;
no dependency installation is authorized by this recommendation.

Fresh-account acceptance: install/extract with no repository checkout or developer
SDK; configure own credentials through masked UI; create/place/start, pause,
save/quit/relaunch/reload with the VPS unreachable. Verify no server network
listener beyond loopback, no key material in exports/logs, saved fullscreen/scale,
usage accounting continuity, second launch, host crash, client crash and update
rollback. Network may still be needed for the user's chosen model provider.

### Future deployment command contract

Implement a staged command with explicit `plan`, `stage`, `apply`, `verify` and
`rollback` operations only after reviewing this design. These are proposed modes,
not commands that exist today. `plan` must be read-only and identify source commit,
artifact hashes/version, schema compatibility, destination service and separate
state/meter/credential paths without printing private contents. Refuse ambiguous
paths, dirty artifacts, unsupported schema migration and missing rollback space.

`stage` verifies build/restore/test/Godot/export gates, hashes the exact artifacts
and prepares a versioned release directory without touching the running service.
Before authorized `apply`, capture a coherent paused checkpoint and preserve
history segments, manual/auto/recovery saves, owner authority, global usage meter
and provider configuration with restrictive permissions. Do not use an unpaused
copy as a claimed consistent backup.

Meter migration must preserve spent attempts and pending reservations. If old and
new locations both contain accounting, refuse to choose silently; require a
reviewed reconciliation. An unreadable meter blocks paid calls rather than
resetting totals. Never restore a lower historical meter as part of binary
rollback: spending is monotonic across attempted deployments. Credential movement
must use protected storage and the intended OS account, not echo/cat or an
unprotected temporary export.

After service switch, verify actual running commit/process identity, protected
state paths, paused world tick, save reload, handshake and blocked paid work
before any explicit Resume. Failure rolls binaries/service configuration back
while preserving the latest compatible state and accounting. If schema/data is
not backward-compatible, stop for inspection rather than loading it with an old
binary or silently discarding progress. Logs/report contain versions, hashes,
bounded counts and outcomes, never secrets or file contents.

An operator rehearsal must inject failure before stop, after backup, during meter
migration, before readiness and after first save; prove rollback and accounting
continuity on disposable synthetic state. The active playtest host remains out of
scope until computment separately authorizes deployment. This proposal makes
release gates reviewable but is not evidence of a working local package or live
migration.
