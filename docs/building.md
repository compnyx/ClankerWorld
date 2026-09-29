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

## Scale, persistence and overview acceptance experiment (#151, #260, #127)

**Unexecuted review protocol, not supported-size or frame-rate evidence.** Do not
unlock Large/Huge/Mega, change the zoom floor, reduce save durability or prune
history merely because generation or a compact-map fixture passes.

Use a disposable synthetic world and deterministic seed set, no private playtest
save or credentials. Record commit, release/schema versions, OS, CPU/RAM, disk
and filesystem, runtime/Godot versions, graphics backend, window/render size and
UI scale. Disable hosted providers and report that choice. Run one resource-bound
job at a time; a resource cap is part of the result, not a hidden variable.

### Sampling matrix

| Axis | Required samples | Report |
| --- | --- | --- |
| Size | Current supported Small/Medium controls, then Large/Huge/Mega separately | Dimensions, tiles, actors, structures, resources and history volume |
| State age | Fresh, established settlement, and long-history synthetic checkpoint | How constructed; never call an empty huge map representative |
| Persistence | Encode alone, durable state-file save, reload+validate, manual overwrite, autosave | Bytes, p50/p95/max elapsed, allocation/RSS and disk-write bytes |
| Observation | Full baseline, unchanged refresh, camera movement and terrain delta | Serialized bytes, CPU time, held-client consistency |
| Simulation | Same population on/off camera, stable and urgent-needs cases | Tick latency/backlog and equal authoritative outcomes |
| Rendering | Current zoom floor plus wider proposed floors at 720p/1080p, 100%/200% UI | Visible tiles, p50/p95 frame time, marker legibility and selection accuracy |

Perform warmup separately from measured runs; retain each sample and failures,
not just an average. Compare at least three fixed seeds and repeat the same state
for persistence trials. Use monotonic timing around the actual operations;
`fsync` latency must not be relabelled as encoder time. Do not compare Linux
headless time to Windows interactive frame time as equivalent evidence.

### Invariants before performance claims

- A save/reload preserves semantic state and content locks. Atomic replacement
  failures leave either the old valid file or the new valid file, never reset it.
- Pause/quit persist the accepted state; crash recovery loss bounds are explicit.
  Any proposed deferred-write cadence needs a new crash-loss contract first.
- Manual overwrite recovery and history segments are counted separately from
  autosave rotation. Retention is an owner decision under #284, not a benchmark
  license to delete backups or digest-reachable history.
- Off-camera actors keep identical scheduling/needs/knowledge semantics.
  Viewport culling is presentation only. Agent inspection and event jumps must
  still work at each proposed overview floor.
- Map labels may simplify at overview zoom, but resources, structures, borders,
  camera position and hit targets must remain interpretable; record screenshots
  and concrete missed/ambiguous selections on Windows.

### Result artifact and adoption gate

Attach a machine-readable table with one row per sample:
`commit,seed,preset,state_age,operation,iteration,elapsed_ms,bytes,peak_rss_bytes,outcome`.
Keep render frame samples separate from save samples. Provide capture paths,
raw summary commands and any failure/reproduction notes. The recommendation must
name the slowest supported configuration and rejected configurations, not just
the fastest development machine.

Propose supported size/zoom bounds only after the full create/save/load/observe/
render/simulate path succeeds. If persistence dominates, compare an explicitly
specified cadence experiment without changing production defaults; if projection
or rendering dominates, isolate that boundary instead. Owner review selects the
tradeoff. This protocol contains no benchmark numbers and changes no supported
world sizes, zoom settings, save cadence or retention behavior.
