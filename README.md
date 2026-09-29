# ClankerWorld

**ClankerWorld** is a private world simulation where AI agents live their own
lives. They find food, keep warm, build, make friends, have children and grow old.
You watch the whole world, click on anyone to see what they are doing and
thinking, and can ask them to do things. Each agent has its own AI model, and a
strict rulebook decides what can actually happen, so every accepted action plays
out the same way every time.

The long-term goal is a small society that survives, builds, trades, forms
relationships and institutions, and safely expands its own world.

ClankerWorld is an **early private alpha**. The foundations are solid, but the
game around them is still thin.

## What works today

You play with a Windows game client, connected securely to a world server that
runs the simulation.

```text
Windows Godot client  ⇄  world server  →  simulation, saves and model calls
     (what you see)      (secure link)       (what actually happens)
```

- Create a world from a seed, choose where your Town goes, and add four founding
  agents before time starts. After that, births and descendants are possible.
- Agents move, get hungry and cold, gather and eat food, and build. Energy
  and sleep are not part of the game.
- The map has renewable resources, a calendar, seasons and weather.
- Click an agent to see their needs, plans, belongings, relationships,
  private thoughts and memories.
- Pause and resume time, and give agents suggestions or firm instructions.
- Agents can think with a built-in rulebook or with models from OpenAI or
  Ollama Cloud. An optional helper called Jev can handle small everyday choices.
- Worlds save and reload, and survive restarts.

Many deeper systems exist as building blocks but are not yet part of everyday
play. Trade and social life are still narrow, and factions, law, currency and
culture do not yet feel like a living civilization.

For details, see the [vision](docs/vision-interview.md) for the game we are
aiming at, [current state](docs/current-state.md) for what works now, and
[known bugs](docs/bugs.md) for what is broken.

## How it works

- **The server decides.** The game client shows the world and sends requests. It
  never changes the world directly.
- **Time only runs while you are playing.** The world pauses about five seconds
  after the last game client closes, and picks up exactly where it stopped when
  you return. Nothing happens while you are away. A world you paused stays paused.
- **Models choose, rules act.** A model picks from a short list of legal options.
  Fixed rules check and carry out everything else: movement, costs, collisions,
  work and results.
- **Your keys stay private.** API keys stay on the server in a separate
  restricted file. They are never sent back to the client, saved in a world or
  written to logs.

## Agents and their models

Each founding agent has its own model and API key. Jev is an optional helper for
the whole world. It does not replace an agent's own model. Urgent needs such as
hunger and cold come before longer plans. See the [vision](docs/vision-interview.md)
for where richer planning and society are headed.

## Repository layout

| Path | What it is |
| --- | --- |
| `src/ClankerWorld.Simulation` | The simulation: rules, agents, society, content and saving |
| `src/ClankerWorld.Viewer` | The world server: pairing, the secure API and the live world |
| `src/ClankerWorld.GodotClient` | The game you play, and its Windows build |
| `tests/ClankerWorld.Simulation.Tests` | Automated tests for the rules, protocol, saving, security and client |
| `docs/vision-interview.md` | What the finished game should be, and open questions |
| `docs/current-state.md` | What works today |
| `docs/bugs.md` | Confirmed bugs and gaps |

The web page the server also serves is a leftover diagnostic tool. It is not a
supported way to play.

## Build and test

The code uses C# 14 on .NET 10. The client uses Godot 4.7.2 with C# and builds
an unsigned Windows 11 x64 bundle.

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet test --configuration Release --no-restore
bash scripts/verify-godot-client.sh
bash scripts/verify-godot-windows-export.sh
```

GitHub Actions uploads the Windows bundle. It is not yet a signed release or
installer. See [building](docs/building.md) for more.

## Documentation

Start at the [documentation map](docs/README.md). It keeps the game we want and
the prototype we have separate:

1. [Vision](docs/vision-interview.md)
2. [Current state](docs/current-state.md)
3. [Architecture](docs/architecture.md)
4. [Known bugs](docs/bugs.md)

## What ClankerWorld is not yet

- a finished game or final art;
- a complete self-running economy or society;
- a signed installer or public release;
- a public server or multiplayer game;
- a safe place to run arbitrary generated code.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Base changes on something seen in play,
an agreed decision, or evidence you can reproduce. A data type or test fixture
is not a feature players can use.

## License

ClankerWorld is released under the [MIT License](LICENSE).
