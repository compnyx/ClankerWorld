# Contributing to ClankerWorld

ClankerWorld is a private, persistent world simulation inhabited by autonomous
agents. It is an early private alpha: the technical foundation is solid, but the
connected gameplay is still thin. This guide explains how work is proposed,
reviewed and merged, so that contributions from people and from coding agents
look the same and are easy to check.

## Where to start

Read these before proposing a change. Each question has one canonical home, so
link to it rather than copying it.

| Question | Read |
| --- | --- |
| What should the finished game be? | [Vision ledger](docs/vision-interview.md) |
| What actually works today? | [Current state](docs/current-state.md) |
| What is broken or missing? | [Known bugs](docs/bugs.md) |
| How are the pieces separated? | [Architecture](docs/architecture.md) |
| How do I build and test? | [Building](docs/building.md) |
| How do releases and versions work? | [Releasing](docs/releasing.md) |

The [documentation map](docs/README.md) explains which document wins when two
disagree. A data type, a fixture or a passing unit test does not make a feature
playable. A feature counts as playable only once it is connected to the default
private world and the normal Godot client.

## Useful contributions right now

- Connect existing pieces into the **Living Settlement** loop.
- Fix a confirmed problem from the known-bugs register.
- Add reproducible evidence for a gameplay or architecture claim.
- Make the UI clearer, or the operator path simpler, without weakening server
  authority.
- Make player-facing text plainer (see
  [Writing player-facing text](#writing-player-facing-text)).
- Remove duplicated or stale documentation.
- Challenge an accepted design choice with concrete evidence.

## Issues

Open one issue per problem or decision, using the matching template. Search
open issues first.

| Kind | Use it when | Labels | The issue must include |
| --- | --- | --- | --- |
| **Bug** | Something behaves wrongly | `bug` | Steps, expected result, actual result, build or commit, seed or save if relevant |
| **Implementation** | The design is settled and needs building | `type:implementation` | The decision or vision section it follows, acceptance criteria, what is out of scope |
| **Decision** | A design question needs an answer from the owner | `type:decision` | The question, options with trade-offs, a recommendation, what it blocks |
| **Prototype** | Something must be tried before it can be decided | `type:prototype` | The hypothesis, the smallest experiment, how the result will be judged |
| **Playtest report** | You played and noticed things | none | Build, date, what happened, what felt wrong, screenshots if useful |

Add an `area:` label when one fits, and `accessibility` for barriers that
affect people with disabilities. Use `gate:blocker` only for work that must
close before the current milestone can pass.

Guidelines for every issue:

- Write a title that says what happens or what is wanted, in plain words.
- Say who is affected: a player, an agent or an operator.
- Keep it to one topic. Split combined reports.
- Define "done" with checks someone else can run or see.
- Confirmed defects and product gaps are also recorded in
  [docs/bugs.md](docs/bugs.md). A closed issue does not remove an entry there
  until the fix is merged and verified through the normal game path.
- Never paste API keys, pairing codes, private saves or raw provider payloads.

## Pull requests

### Before you open one

1. Link the issue, or explain in the description why none exists.
2. Read the relevant current-state, vision and architecture documents.
3. Check open pull requests that touch the same files. If yours overlaps,
   say so in the description and agree an order. Do not silently stack on an
   unmerged branch.
4. Keep the change to **one concern**. A wording pass, a bug fix and a
   refactor are three pull requests.

### Title and description

- Title: a short imperative sentence about the effect, for example
  `Show fullness instead of hunger on the agent panel`.
- Fill in every section of the
  [pull request template](.github/pull_request_template.md). If a section does
  not apply, say so instead of deleting it.
- Write for a reviewer who has not seen your branch: what changed, why, and how
  you know it works.

### Change rules

1. Keep the authoritative simulation independent of Godot and model providers.
2. Treat model output as untrusted input. Legal actions and state transitions
   stay server-owned.
3. Never add API keys, private saves, pairing material or raw provider payloads
   to the repository, logs or bug reports.
4. Log outcomes at meaningful boundaries with stable event names and named
   fields, and never log secrets (see [AGENTS.md](AGENTS.md)).
5. A change to canonical state, events, saves or replay needs migration and
   replay coverage, and must say so in the template.
6. Update the canonical document in the same pull request when behavior, UI,
   operations, compatibility, known bugs or scope change.
7. Add or update tests for behavior changes. Wording-only and documentation-only
   changes need no new tests, but must not break existing ones.

### Verification gate

Run what CI runs, and put the results in the description:

```bash
dotnet restore --locked-mode
bash scripts/verify-godot-client.sh
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
```

Changes to the Windows client should also pass
`bash scripts/verify-godot-windows-export.sh`. If a check cannot run in your
environment, say which one and why. Do not report it as passing.

### Changelog and documentation

- Every player-visible gameplay, UI, world-runtime, save-compatibility,
  deployment, packaging or security change adds an entry under **Unreleased**
  in [CHANGELOG.md](CHANGELOG.md), written for players or operators. Skip
  entries for refactors, test-only changes and documentation edits.
- Update the affected canonical document in the same change. Do not copy
  volatile status into overview files.
- If another open pull request also edits the changelog, expect a small merge
  conflict and resolve it by keeping both entries.

### Review and merge

- CI must be green.
- Get one approving review before merging.
- Squash-merge, with the pull request title as the commit subject. Follow the
  [release policy](docs/releasing.md) for versions and tags.
- Delete the branch after merging.

## Writing player-facing text

This covers everything a player reads: buttons, tooltips, hints, status
messages, errors and event-log lines.

- **Write for a person, not a system.** Say what happened and what to do next.
- **Keep it short.** One sentence for a tooltip and two at most for a hint.
  Put longer explanations in an optional help view.
- **Use ordinary words.** Avoid protocol, storage and architecture terms such as
  *cognition*, *inhabitant*, *tick*, *revision*, *origin*, *pinned*, *atomic*,
  *signed request*, *basis points*, *fallback* and *provider role*.
- **Name things as the player sees them,** and use the same word everywhere:
  *agent*, *model*, *key*, *Town*, *House*.
- **Be kind about failures.** Say what went wrong, whether anything was lost, and
  what the player can do.
- **Make numbers readable.** Use percentages for needs and make the direction
  obvious: *Fullness 40%*, not *Hunger 40%* when 100% means full.
- **Use sentence case,** with no all-caps labels.

| Instead of | Write |
| --- | --- |
| `submitting one-use signed owner request…` | `Sending…` |
| `disconnected · holding accepted tick 5040` | `Connection lost · showing the world as it was at 12:30` |
| `Deterministic (fallback) · take it easy` | `Model unavailable · the built-in rules chose to take it easy` |
| `Inspect ancestry and partnerships, including deceased relatives.` | `See their family, including those who have passed.` |

Text meant only for developers belongs behind **Developer tools** and may stay
technical.

## Commit messages

- First line: an imperative summary of about 70 characters or fewer.
- Body: why the change was made, when that is not obvious.
- Reference the issue with `Fixes #123` or `Refs #123`.

## Security

Report a vulnerability privately to the repository owner, not in a public issue.
Never include credentials, pairing material or private saves in any report.

## Conduct

Be respectful and assume good faith. Disagree with the design, never the person.
Concrete evidence beats opinion, and unresolved design questions belong in a
**Decision** issue.
